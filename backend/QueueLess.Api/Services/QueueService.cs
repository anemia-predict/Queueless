using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QueueLess.Api.Data;
using QueueLess.Api.DTOs;
using QueueLess.Api.Hubs;
using QueueLess.Api.Models;

namespace QueueLess.Api.Services;

public class QueueService : IQueueService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<QueueHub> _hub;
    private readonly INotificationService _notifications;
    private readonly IAIPredictionService _ai;

    public QueueService(
        ApplicationDbContext db,
        IHubContext<QueueHub> hub,
        INotificationService notifications,
        IAIPredictionService ai)
    {
        _db = db;
        _hub = hub;
        _notifications = notifications;
        _ai = ai;
    }

    public async Task<List<QueueSessionDto>> GetQueueSessionsAsync(Guid hospitalId, Guid? doctorId = null)
    {
        await ExpireStalePreviousDaySessionsAsync(hospitalId);

        var query = _db.QueueSessions
            .Where(qs => qs.HospitalId == hospitalId)
            .Include(qs => qs.Doctor)
            .Include(qs => qs.Department)
            .Include(qs => qs.Bookings)
                .ThenInclude(b => b.Patient)
            .AsQueryable();

        if (doctorId.HasValue)
        {
            query = query.Where(qs => qs.DoctorId == doctorId.Value);
        }

        var sessions = await query.OrderByDescending(qs => qs.CreatedAt).ToListAsync();
        return sessions.Select(MapSessionToDto).ToList();
    }

    public async Task<QueueSessionDto> GetQueueSessionDetailsAsync(Guid hospitalId, Guid sessionId)
    {
        var session = await _db.QueueSessions
            .Where(qs => qs.HospitalId == hospitalId && qs.Id == sessionId)
            .Include(qs => qs.Doctor)
            .Include(qs => qs.Department)
            .Include(qs => qs.Bookings)
                .ThenInclude(b => b.Patient)
            .FirstOrDefaultAsync();

        if (session == null)
        {
            throw new KeyNotFoundException("Queue session not found in this hospital.");
        }

        return MapSessionToDto(session);
    }

    public async Task<QueueSessionDto> OpenQueueSessionAsync(Guid hospitalId, OpenQueueSessionDto dto)
    {
        // Enforce tenant boundary: doctor & department belong to this hospital
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == dto.DoctorId && d.HospitalId == hospitalId);
        if (doctor == null) throw new KeyNotFoundException("Doctor does not belong to this hospital.");

        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == dto.DepartmentId && d.HospitalId == hospitalId);
        if (dept == null) throw new KeyNotFoundException("Department does not belong to this hospital.");

        var sessionDate = !string.IsNullOrWhiteSpace(dto.SessionDate) && DateOnly.TryParse(dto.SessionDate, out var parsedDate)
            ? parsedDate
            : DateOnly.FromDateTime(DateTime.UtcNow);

        if (!TimeSpan.TryParse(dto.StartTime, out var startTime)) startTime = new TimeSpan(9, 0, 0);
        if (!TimeSpan.TryParse(dto.EndTime, out var endTime)) endTime = new TimeSpan(17, 0, 0);

        var session = new QueueSession
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            DoctorId = dto.DoctorId,
            DepartmentId = dto.DepartmentId,
            ScheduleId = dto.ScheduleId,
            SessionDate = sessionDate,
            StartTime = startTime,
            EndTime = endTime,
            Status = QueueSessionStatus.Open,
            MaxCapacity = dto.MaxCapacity > 0 ? dto.MaxCapacity : 30,
            CurrentTokenNumber = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.QueueSessions.Add(session);
        await _db.SaveChangesAsync();

        return await GetQueueSessionDetailsAsync(hospitalId, session.Id);
    }

    public async Task<QueueActionResponseDto> CallNextAsync(Guid hospitalId, Guid sessionId, Guid operatorUserId)
    {
        var session = await _db.QueueSessions
            .Include(qs => qs.Bookings)
                .ThenInclude(b => b.Patient)
            .Include(qs => qs.Hospital)
            .Include(qs => qs.Doctor)
            .Include(qs => qs.Department)
            .FirstOrDefaultAsync(qs => qs.Id == sessionId && qs.HospitalId == hospitalId);

        if (session == null) throw new KeyNotFoundException("Queue session not found.");

        if (session.Status == QueueSessionStatus.Paused)
        {
            throw new InvalidOperationException("Cannot call patients while queue is paused.");
        }

        // Step 1: Forfeit any previously CALLED booking that exceeded the 2-minute doctor room check-in window
        var now = DateTime.UtcNow;
        var expiredBookings = session.Bookings
            .Where(b => b.Status == BookingStatus.Called && b.ActualCallTime.HasValue && now > b.ActualCallTime.Value.AddMinutes(2))
            .ToList();

        foreach (var expired in expiredBookings)
        {
            expired.Status = BookingStatus.NoShow;
            expired.CancellationReason = "Forfeited: Did not check into doctor consultation room within 2 minutes of token call.";
            expired.UpdatedAt = now;

            _db.QueueEvents.Add(new QueueEvent
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalId,
                QueueSessionId = session.Id,
                BookingId = expired.Id,
                EventType = QueueEventType.NoShow,
                Timestamp = now,
                TriggeredByUserId = operatorUserId,
                MetadataJson = JsonSerializer.Serialize(new { Reason = "Doctor room check-in timeout (2 minutes exceeded)", ForfeitedToken = expired.TokenNumber })
            });

            await _notifications.CreateNotificationAsync(
                expired.PatientUserId,
                "Token Forfeited (Doctor Room Timeout)",
                $"Token #{expired.TokenNumber} was forfeited because you did not enter the doctor consultation room within 2 minutes of being called.",
                NotificationType.BookingCancelled,
                expired.Id);
        }

        // Step 2: Skip / forfeit any waiting booking whose estimated service time passed without building check-in
        var candidates = session.Bookings
            .Where(b => b.Status == BookingStatus.Waiting)
            .OrderBy(b => b.TokenNumber)
            .ToList();

        Booking? nextBooking = null;
        foreach (var candidate in candidates)
        {
            // If patient hasn't checked into the building and estimated service time has passed by more than 3 minutes
            if (!candidate.IsBuildingCheckedIn && candidate.EstimatedServiceTime.HasValue && now > candidate.EstimatedServiceTime.Value.AddMinutes(3))
            {
                candidate.Status = BookingStatus.NoShow;
                candidate.CancellationReason = "Forfeited: Patient did not arrive/check into hospital building by scheduled token arrival time.";
                candidate.UpdatedAt = now;

                _db.QueueEvents.Add(new QueueEvent
                {
                    Id = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    QueueSessionId = session.Id,
                    BookingId = candidate.Id,
                    EventType = QueueEventType.NoShow,
                    Timestamp = now,
                    TriggeredByUserId = operatorUserId,
                    MetadataJson = JsonSerializer.Serialize(new { Reason = "Building arrival check-in missed", ForfeitedToken = candidate.TokenNumber })
                });

                await _notifications.CreateNotificationAsync(
                    candidate.PatientUserId,
                    "Token Forfeited (Building Arrival Missed)",
                    $"Token #{candidate.TokenNumber} was forfeited because you had not checked into the hospital building by your token time.",
                    NotificationType.BookingCancelled,
                    candidate.Id);

                continue; // Move directly to next candidate without blocking queue
            }

            nextBooking = candidate;
            break;
        }

        if (nextBooking == null)
        {
            await _db.SaveChangesAsync();
            return new QueueActionResponseDto
            {
                Success = false,
                Message = "No eligible waiting patients in queue.",
                Session = MapSessionToDto(session)
            };
        }

        nextBooking.Status = BookingStatus.Called;
        nextBooking.ActualCallTime = now;
        nextBooking.CallTimeoutTime = now.AddMinutes(2);
        nextBooking.UpdatedAt = now;

        _db.QueueEvents.Add(new QueueEvent
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            QueueSessionId = session.Id,
            BookingId = nextBooking.Id,
            EventType = QueueEventType.Called,
            Timestamp = now,
            TriggeredByUserId = operatorUserId,
            MetadataJson = JsonSerializer.Serialize(new { TokenNumber = nextBooking.TokenNumber, TimeoutSeconds = 120 })
        });

        await _db.SaveChangesAsync();

        // Broadcast Call Event via SignalR with 2-minute countdown
        await _hub.Clients.Group($"QueueSession_{session.Id}").SendAsync("TokenCalled", new
        {
            QueueSessionId = session.Id,
            BookingId = nextBooking.Id,
            TokenNumber = nextBooking.TokenNumber,
            PatientName = nextBooking.Patient.FullName,
            DoctorName = session.Doctor.FullName,
            DepartmentName = session.Department.Name,
            ActualCallTime = nextBooking.ActualCallTime,
            CallTimeoutTime = nextBooking.CallTimeoutTime,
            TimeoutSeconds = 120
        });

        // Send patient notification
        await _notifications.CreateNotificationAsync(
            nextBooking.PatientUserId,
            "Your Token Has Been Called!",
            $"Token #{nextBooking.TokenNumber} is now being called by {session.Doctor.FullName}. You have 2 minutes to enter the consultation room before this token is forfeited.",
            NotificationType.TokenCalled,
            nextBooking.Id);

        // Alert the subsequent patient that they are next in line!
        var subsequentPatient = session.Bookings
            .Where(b => b.Status == BookingStatus.Waiting && b.Id != nextBooking.Id)
            .OrderBy(b => b.TokenNumber)
            .FirstOrDefault();

        if (subsequentPatient != null)
        {
            await _notifications.CreateNotificationAsync(
                subsequentPatient.PatientUserId,
                "You're Next in Line!",
                $"Token #{nextBooking.TokenNumber} has been called. You have Token #{subsequentPatient.TokenNumber} and are next in line! Please be ready near the consultation room.",
                NotificationType.QueueUpdated,
                subsequentPatient.Id);

            await _hub.Clients.Group($"Patient_{subsequentPatient.PatientUserId}").SendAsync("YouAreNext", new
            {
                QueueSessionId = session.Id,
                BookingId = subsequentPatient.Id,
                TokenNumber = subsequentPatient.TokenNumber,
                DoctorName = session.Doctor.FullName,
                CurrentServingToken = nextBooking.TokenNumber
            });
        }

        return new QueueActionResponseDto
        {
            Success = true,
            Message = $"Token #{nextBooking.TokenNumber} called. 2-minute doctor room check-in window started.",
            Booking = MapBookingToDto(nextBooking),
            Session = MapSessionToDto(session)
        };
    }

    public async Task<QueueActionResponseDto> StartConsultationAsync(Guid hospitalId, Guid bookingId, Guid operatorUserId)
    {
        var booking = await _db.Bookings
            .Include(b => b.QueueSession)
            .Include(b => b.Patient)
            .Include(b => b.Doctor)
            .Include(b => b.Department)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.HospitalId == hospitalId);

        if (booking == null) throw new KeyNotFoundException("Booking not found.");

        if (booking.Status != BookingStatus.Called && booking.Status != BookingStatus.Waiting)
        {
            throw new InvalidOperationException($"Cannot start consultation for booking with status '{booking.Status}'.");
        }

        // 2-minute doctor room entry enforcement
        if (booking.ActualCallTime.HasValue && DateTime.UtcNow > booking.ActualCallTime.Value.AddMinutes(2))
        {
            booking.Status = BookingStatus.NoShow;
            booking.CancellationReason = "Forfeited: Did not check into doctor consultation room within 2 minutes of token call.";
            booking.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            throw new InvalidOperationException($"Token #{booking.TokenNumber} forfeited: 2-minute doctor room check-in window expired. Please call the next patient.");
        }

        // Auto-complete any prior patient who was in consultation in this session
        await AutoCompletePreviousServingBookingsAsync(hospitalId, booking.QueueSessionId, booking.Id, operatorUserId);

        booking.IsDoctorRoomCheckedIn = true;
        booking.DoctorRoomCheckInTime = DateTime.UtcNow;
        booking.Status = BookingStatus.Serving;
        booking.ActualStartTime = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        booking.QueueSession.CurrentServingBookingId = booking.Id;
        booking.QueueSession.UpdatedAt = DateTime.UtcNow;

        _db.QueueEvents.Add(new QueueEvent
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            QueueSessionId = booking.QueueSessionId,
            BookingId = booking.Id,
            EventType = QueueEventType.Started,
            Timestamp = DateTime.UtcNow,
            TriggeredByUserId = operatorUserId
        });

        await _db.SaveChangesAsync();

        // SignalR update
        await _hub.Clients.Group($"QueueSession_{booking.QueueSessionId}").SendAsync("ConsultationStarted", new
        {
            QueueSessionId = booking.QueueSessionId,
            BookingId = booking.Id,
            TokenNumber = booking.TokenNumber,
            PatientName = booking.Patient.FullName
        });

        await _notifications.CreateNotificationAsync(
            booking.PatientUserId,
            "Consultation Started",
            $"Your consultation for Token #{booking.TokenNumber} with {booking.Doctor.FullName} has started.",
            NotificationType.ConsultationStarted,
            booking.Id);

        // Alert the subsequent patient that they are now next in line!
        var nextInLine = await _db.Bookings
            .Include(b => b.Patient)
            .Where(b => b.QueueSessionId == booking.QueueSessionId && b.Status == BookingStatus.Waiting)
            .OrderBy(b => b.TokenNumber)
            .FirstOrDefaultAsync();

        if (nextInLine != null)
        {
            await _notifications.CreateNotificationAsync(
                nextInLine.PatientUserId,
                "You're Next in Line!",
                $"Token #{booking.TokenNumber} is now consulting with {booking.Doctor.FullName}. You have Token #{nextInLine.TokenNumber} and are next in line! Please wait near the consultation room.",
                NotificationType.QueueUpdated,
                nextInLine.Id);

            await _hub.Clients.Group($"Patient_{nextInLine.PatientUserId}").SendAsync("YouAreNext", new
            {
                QueueSessionId = booking.QueueSessionId,
                BookingId = nextInLine.Id,
                TokenNumber = nextInLine.TokenNumber,
                DoctorName = booking.Doctor.FullName,
                CurrentServingToken = booking.TokenNumber
            });
        }

        return new QueueActionResponseDto
        {
            Success = true,
            Message = $"Consultation for Token #{booking.TokenNumber} started.",
            Booking = MapBookingToDto(booking)
        };
    }

    public async Task<QueueActionResponseDto> CompleteConsultationAsync(Guid hospitalId, Guid bookingId, Guid operatorUserId, string? notes = null)
    {
        var booking = await _db.Bookings
            .Include(b => b.QueueSession)
            .Include(b => b.Patient)
            .Include(b => b.Doctor)
            .Include(b => b.Department)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.HospitalId == hospitalId);

        if (booking == null) throw new KeyNotFoundException("Booking not found.");

        if (booking.Status != BookingStatus.Serving)
        {
            throw new InvalidOperationException($"Cannot complete consultation. Booking status is '{booking.Status}', expected '{BookingStatus.Serving}'.");
        }

        var endTime = DateTime.UtcNow;
        var startTime = booking.ActualStartTime ?? endTime.AddMinutes(-10);

        booking.Status = BookingStatus.Completed;
        booking.ActualEndTime = endTime;
        booking.UpdatedAt = endTime;

        if (booking.QueueSession.CurrentServingBookingId == booking.Id)
        {
            booking.QueueSession.CurrentServingBookingId = null;
        }
        booking.QueueSession.UpdatedAt = endTime;

        var durationSpan = endTime - startTime;
        var durationSeconds = Math.Max(30, durationSpan.TotalSeconds);
        var durationMinutes = Math.Round(durationSeconds / 60.0, 1);

        // Store in ConsultationRecords for AI training pipeline!
        var consultationRecord = new ConsultationRecord
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            BookingId = booking.Id,
            DoctorId = booking.DoctorId,
            DepartmentId = booking.DepartmentId,
            QueueSessionId = booking.QueueSessionId,
            StartTime = startTime,
            EndTime = endTime,
            DurationSeconds = durationSeconds,
            DurationMinutes = durationMinutes,
            Notes = notes,
            IsSyntheticDemo = false, // Genuine consultation
            CreatedAt = endTime
        };

        _db.ConsultationRecords.Add(consultationRecord);

        _db.QueueEvents.Add(new QueueEvent
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            QueueSessionId = booking.QueueSessionId,
            BookingId = booking.Id,
            EventType = QueueEventType.Completed,
            Timestamp = endTime,
            TriggeredByUserId = operatorUserId,
            MetadataJson = JsonSerializer.Serialize(new { DurationMinutes = durationMinutes })
        });

        await _db.SaveChangesAsync();

        // Broadcast to SignalR
        await _hub.Clients.Group($"QueueSession_{booking.QueueSessionId}").SendAsync("ConsultationCompleted", new
        {
            QueueSessionId = booking.QueueSessionId,
            BookingId = booking.Id,
            TokenNumber = booking.TokenNumber,
            DurationMinutes = durationMinutes
        });

        await _notifications.CreateNotificationAsync(
            booking.PatientUserId,
            "Consultation Completed",
            $"Your consultation with {booking.Doctor.FullName} has ended. Thank you for using QueueLess!",
            NotificationType.ConsultationCompleted,
            booking.Id);

        return new QueueActionResponseDto
        {
            Success = true,
            Message = $"Consultation for Token #{booking.TokenNumber} completed. Actual duration: {durationMinutes} mins.",
            Booking = MapBookingToDto(booking)
        };
    }

    public async Task<QueueActionResponseDto> MarkNoShowAsync(Guid hospitalId, Guid bookingId, Guid operatorUserId)
    {
        var booking = await _db.Bookings
            .Include(b => b.QueueSession)
            .Include(b => b.Patient)
            .Include(b => b.Doctor)
            .Include(b => b.Department)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.HospitalId == hospitalId);

        if (booking == null) throw new KeyNotFoundException("Booking not found.");

        if (booking.Status == BookingStatus.Completed || booking.Status == BookingStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot mark booking as NO_SHOW with status '{booking.Status}'.");
        }

        booking.Status = BookingStatus.NoShow;
        booking.UpdatedAt = DateTime.UtcNow;

        if (booking.QueueSession.CurrentServingBookingId == booking.Id)
        {
            booking.QueueSession.CurrentServingBookingId = null;
        }

        _db.QueueEvents.Add(new QueueEvent
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            QueueSessionId = booking.QueueSessionId,
            BookingId = booking.Id,
            EventType = QueueEventType.NoShow,
            Timestamp = DateTime.UtcNow,
            TriggeredByUserId = operatorUserId
        });

        await _db.SaveChangesAsync();

        // Broadcast SignalR update
        await _hub.Clients.Group($"QueueSession_{booking.QueueSessionId}").SendAsync("QueueUpdated", new
        {
            QueueSessionId = booking.QueueSessionId,
            Action = "PATIENT_NO_SHOW",
            TokenNumber = booking.TokenNumber
        });

        return new QueueActionResponseDto
        {
            Success = true,
            Message = $"Token #{booking.TokenNumber} marked as NO_SHOW.",
            Booking = MapBookingToDto(booking)
        };
    }

    public async Task<QueueActionResponseDto> PauseQueueAsync(Guid hospitalId, Guid sessionId, Guid operatorUserId)
    {
        var session = await _db.QueueSessions
            .FirstOrDefaultAsync(qs => qs.Id == sessionId && qs.HospitalId == hospitalId);

        if (session == null) throw new KeyNotFoundException("Queue session not found.");

        session.Status = QueueSessionStatus.Paused;
        session.UpdatedAt = DateTime.UtcNow;

        _db.QueueEvents.Add(new QueueEvent
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            QueueSessionId = session.Id,
            EventType = QueueEventType.Paused,
            Timestamp = DateTime.UtcNow,
            TriggeredByUserId = operatorUserId
        });

        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"QueueSession_{session.Id}").SendAsync("QueueUpdated", new
        {
            QueueSessionId = session.Id,
            Action = "QUEUE_PAUSED",
            Status = session.Status
        });

        return new QueueActionResponseDto
        {
            Success = true,
            Message = "Queue paused successfully.",
            Session = await GetQueueSessionDetailsAsync(hospitalId, sessionId)
        };
    }

    public async Task<QueueActionResponseDto> ResumeQueueAsync(Guid hospitalId, Guid sessionId, Guid operatorUserId)
    {
        var session = await _db.QueueSessions
            .FirstOrDefaultAsync(qs => qs.Id == sessionId && qs.HospitalId == hospitalId);

        if (session == null) throw new KeyNotFoundException("Queue session not found.");

        session.Status = QueueSessionStatus.Open;
        session.UpdatedAt = DateTime.UtcNow;

        _db.QueueEvents.Add(new QueueEvent
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            QueueSessionId = session.Id,
            EventType = QueueEventType.Resumed,
            Timestamp = DateTime.UtcNow,
            TriggeredByUserId = operatorUserId
        });

        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"QueueSession_{session.Id}").SendAsync("QueueUpdated", new
        {
            QueueSessionId = session.Id,
            Action = "QUEUE_RESUMED",
            Status = session.Status
        });

        return new QueueActionResponseDto
        {
            Success = true,
            Message = "Queue resumed successfully.",
            Session = await GetQueueSessionDetailsAsync(hospitalId, sessionId)
        };
    }

    public async Task<QueueActionResponseDto> ConfirmBuildingCheckInAsync(Guid hospitalId, Guid bookingId, Guid operatorUserId)
    {
        var booking = await _db.Bookings
            .Include(b => b.Patient)
            .Include(b => b.QueueSession)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.HospitalId == hospitalId);

        if (booking == null) throw new KeyNotFoundException("Booking not found.");

        booking.IsBuildingCheckedIn = true;
        booking.BuildingCheckInTime = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        _db.QueueEvents.Add(new QueueEvent
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            QueueSessionId = booking.QueueSessionId,
            BookingId = booking.Id,
            EventType = "BUILDING_CHECK_IN",
            Timestamp = DateTime.UtcNow,
            TriggeredByUserId = operatorUserId,
            MetadataJson = JsonSerializer.Serialize(new { booking.TokenNumber, Stage = "Building Entry Verified" })
        });

        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"QueueSession_{booking.QueueSessionId}").SendAsync("QueueUpdated", new
        {
            QueueSessionId = booking.QueueSessionId,
            Action = "BUILDING_CHECK_IN",
            BookingId = booking.Id,
            TokenNumber = booking.TokenNumber
        });

        return new QueueActionResponseDto
        {
            Success = true,
            Message = $"Stage 1 Complete: Patient {booking.Patient?.FullName ?? "Patient"} (Token #{booking.TokenNumber}) confirmed at Building Entrance.",
            Booking = MapBookingToDto(booking),
            Session = await GetQueueSessionDetailsAsync(hospitalId, booking.QueueSessionId)
        };
    }

    public async Task<QueueActionResponseDto> ConfirmDoctorRoomCheckInAsync(Guid hospitalId, Guid bookingId, Guid operatorUserId)
    {
        var booking = await _db.Bookings
            .Include(b => b.Patient)
            .Include(b => b.Doctor)
            .Include(b => b.Department)
            .Include(b => b.QueueSession)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.HospitalId == hospitalId);

        if (booking == null) throw new KeyNotFoundException("Booking not found.");

        // Check if 2-minute deadline was exceeded
        if (booking.ActualCallTime.HasValue && DateTime.UtcNow > booking.ActualCallTime.Value.AddMinutes(2))
        {
            booking.Status = BookingStatus.NoShow;
            booking.CancellationReason = "Forfeited: Did not check into doctor room within 2 minutes of token call.";
            booking.UpdatedAt = DateTime.UtcNow;

            _db.QueueEvents.Add(new QueueEvent
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalId,
                QueueSessionId = booking.QueueSessionId,
                BookingId = booking.Id,
                EventType = QueueEventType.NoShow,
                Timestamp = DateTime.UtcNow,
                TriggeredByUserId = operatorUserId,
                MetadataJson = JsonSerializer.Serialize(new { Reason = "Doctor room 2-minute entry deadline missed" })
            });

            await _db.SaveChangesAsync();

            // Auto-advance to next token!
            var nextResult = await CallNextAsync(hospitalId, booking.QueueSessionId, operatorUserId);

            return new QueueActionResponseDto
            {
                Success = false,
                Message = $"Token #{booking.TokenNumber} forfeited (2-minute window expired). Auto-advanced to next patient.",
                Session = nextResult.Session
            };
        }

        // Sequential Queue Order Check: Earlier tokens must be completed or forfeited before a later token can enter
        var priorUnfinished = await _db.Bookings
            .Where(b => b.QueueSessionId == booking.QueueSessionId 
                     && b.TokenNumber < booking.TokenNumber
                     && (b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called))
            .OrderBy(b => b.TokenNumber)
            .ToListAsync();

        if (priorUnfinished.Count > 0)
        {
            var earliest = priorUnfinished.First();

            // If the earlier token was called, check if its 2-minute deadline has expired
            if (earliest.Status == BookingStatus.Called && earliest.ActualCallTime.HasValue && DateTime.UtcNow > earliest.ActualCallTime.Value.AddMinutes(2))
            {
                // Auto-forfeit the earlier expired token!
                earliest.Status = BookingStatus.NoShow;
                earliest.CancellationReason = "Forfeited: Did not check into doctor consultation room within 2 minutes of token call.";
                earliest.UpdatedAt = DateTime.UtcNow;

                _db.QueueEvents.Add(new QueueEvent
                {
                    Id = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    QueueSessionId = booking.QueueSessionId,
                    BookingId = earliest.Id,
                    EventType = QueueEventType.NoShow,
                    Timestamp = DateTime.UtcNow,
                    TriggeredByUserId = operatorUserId,
                    MetadataJson = JsonSerializer.Serialize(new { Reason = $"Token #{earliest.TokenNumber} forfeited (2-min call window expired). Admitting Token #{booking.TokenNumber}." })
                });

                await _notifications.CreateNotificationAsync(
                    earliest.PatientUserId,
                    "Token Forfeited (No-Show)",
                    $"Token #{earliest.TokenNumber} was forfeited because you did not enter the consultation room within 2 minutes of being called.",
                    NotificationType.BookingCancelled,
                    earliest.Id);
            }
            else
            {
                // Earlier token has not expired yet! Block cut-in
                if (earliest.Status == BookingStatus.Called)
                {
                    var remainingSecs = Math.Max(0, (int)(earliest.ActualCallTime!.Value.AddMinutes(2) - DateTime.UtcNow).TotalSeconds);
                    throw new InvalidOperationException($"Cannot admit Token #{booking.TokenNumber}: Token #{earliest.TokenNumber} was called first and has {remainingSecs}s remaining to enter. Please wait until Token #{earliest.TokenNumber} completes or forfeits.");
                }
                else
                {
                    throw new InvalidOperationException($"Sequential Check-In Required: Token #{earliest.TokenNumber} is ahead in queue and must be called and checked in before Token #{booking.TokenNumber}.");
                }
            }
        }

        // Auto-complete any prior patient who was in consultation in this session
        await AutoCompletePreviousServingBookingsAsync(hospitalId, booking.QueueSessionId, booking.Id, operatorUserId);

        booking.IsDoctorRoomCheckedIn = true;
        booking.DoctorRoomCheckInTime = DateTime.UtcNow;
        booking.Status = BookingStatus.Serving;
        booking.ActualStartTime = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;
        booking.QueueSession.CurrentServingBookingId = booking.Id;

        _db.QueueEvents.Add(new QueueEvent
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            QueueSessionId = booking.QueueSessionId,
            BookingId = booking.Id,
            EventType = "DOCTOR_ROOM_CHECK_IN",
            Timestamp = DateTime.UtcNow,
            TriggeredByUserId = operatorUserId,
            MetadataJson = JsonSerializer.Serialize(new { booking.TokenNumber, Stage = "Doctor Consultation Room Entry Verified" })
        });

        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"QueueSession_{booking.QueueSessionId}").SendAsync("ConsultationStarted", new
        {
            QueueSessionId = booking.QueueSessionId,
            BookingId = booking.Id,
            TokenNumber = booking.TokenNumber,
            PatientName = booking.Patient.FullName
        });

        // Alert the subsequent patient that they are now next in line!
        var nextInLine = await _db.Bookings
            .Include(b => b.Patient)
            .Where(b => b.QueueSessionId == booking.QueueSessionId && b.Status == BookingStatus.Waiting)
            .OrderBy(b => b.TokenNumber)
            .FirstOrDefaultAsync();

        if (nextInLine != null)
        {
            await _notifications.CreateNotificationAsync(
                nextInLine.PatientUserId,
                "You're Next in Line!",
                $"Token #{booking.TokenNumber} is now consulting with {booking.Doctor.FullName}. You have Token #{nextInLine.TokenNumber} and are next in line! Please wait near the consultation room.",
                NotificationType.QueueUpdated,
                nextInLine.Id);

            await _hub.Clients.Group($"Patient_{nextInLine.PatientUserId}").SendAsync("YouAreNext", new
            {
                QueueSessionId = booking.QueueSessionId,
                BookingId = nextInLine.Id,
                TokenNumber = nextInLine.TokenNumber,
                DoctorName = booking.Doctor.FullName,
                CurrentServingToken = booking.TokenNumber
            });
        }

        return new QueueActionResponseDto
        {
            Success = true,
            Message = $"Stage 2 Complete: Patient {booking.Patient?.FullName ?? "Patient"} (Token #{booking.TokenNumber}) admitted to doctor consultation room.",
            Booking = MapBookingToDto(booking),
            Session = await GetQueueSessionDetailsAsync(hospitalId, booking.QueueSessionId)
        };
    }

    public async Task<QueueActionResponseDto> CheckAndForfeitExpiredTokensAsync(Guid hospitalId, Guid sessionId, Guid operatorUserId)
    {
        var session = await _db.QueueSessions
            .Include(qs => qs.Bookings)
            .FirstOrDefaultAsync(qs => qs.Id == sessionId && qs.HospitalId == hospitalId);

        if (session == null) throw new KeyNotFoundException("Queue session not found.");

        var now = DateTime.UtcNow;
        var expiredBookings = session.Bookings
            .Where(b => b.Status == BookingStatus.Called && b.ActualCallTime.HasValue && now > b.ActualCallTime.Value.AddMinutes(2))
            .ToList();

        if (expiredBookings.Count == 0)
        {
            return new QueueActionResponseDto
            {
                Success = true,
                Message = "No tokens currently expired.",
                Session = await GetQueueSessionDetailsAsync(hospitalId, sessionId)
            };
        }

        foreach (var expired in expiredBookings)
        {
            expired.Status = BookingStatus.NoShow;
            expired.CancellationReason = "Forfeited: Did not check into doctor consultation room within 2 minutes of token call.";
            expired.UpdatedAt = now;

            _db.QueueEvents.Add(new QueueEvent
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalId,
                QueueSessionId = session.Id,
                BookingId = expired.Id,
                EventType = QueueEventType.NoShow,
                Timestamp = now,
                TriggeredByUserId = operatorUserId,
                MetadataJson = JsonSerializer.Serialize(new { Reason = "Doctor room entry deadline expired (>2 minutes)" })
            });
        }

        await _db.SaveChangesAsync();

        // Advance to next patient
        return await CallNextAsync(hospitalId, sessionId, operatorUserId);
    }

    private async Task ExpireStalePreviousDaySessionsAsync(Guid hospitalId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var staleSessions = await _db.QueueSessions
            .Include(qs => qs.Bookings)
            .Where(qs => qs.HospitalId == hospitalId 
                      && qs.SessionDate != default 
                      && qs.SessionDate < today 
                      && (qs.Status == QueueSessionStatus.Open || qs.Status == QueueSessionStatus.Paused))
            .ToListAsync();

        if (staleSessions.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var session in staleSessions)
        {
            session.Status = QueueSessionStatus.Closed;
            session.UpdatedAt = now;

            foreach (var b in session.Bookings.Where(b => b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called || b.Status == BookingStatus.Serving))
            {
                b.Status = BookingStatus.NoShow;
                b.CancellationReason = $"Expired: Clinic session date ({session.SessionDate:yyyy-MM-dd}) ended without attendance.";
                b.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task AutoCompletePreviousServingBookingsAsync(Guid hospitalId, Guid queueSessionId, Guid exceptBookingId, Guid operatorUserId)
    {
        var priorServingBookings = await _db.Bookings
            .Include(b => b.Doctor)
            .Where(b => b.QueueSessionId == queueSessionId && b.Status == BookingStatus.Serving && b.Id != exceptBookingId)
            .ToListAsync();

        if (priorServingBookings.Count == 0) return;

        var endTime = DateTime.UtcNow;

        foreach (var prevBooking in priorServingBookings)
        {
            var startTime = prevBooking.ActualStartTime ?? endTime.AddMinutes(-10);
            var durationSpan = endTime - startTime;
            var durationSeconds = Math.Max(30, durationSpan.TotalSeconds);
            var durationMinutes = Math.Round(durationSeconds / 60.0, 1);

            prevBooking.Status = BookingStatus.Completed;
            prevBooking.ActualEndTime = endTime;
            prevBooking.UpdatedAt = endTime;

            _db.ConsultationRecords.Add(new ConsultationRecord
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalId,
                BookingId = prevBooking.Id,
                DoctorId = prevBooking.DoctorId,
                DepartmentId = prevBooking.DepartmentId,
                QueueSessionId = prevBooking.QueueSessionId,
                StartTime = startTime,
                EndTime = endTime,
                DurationSeconds = durationSeconds,
                DurationMinutes = durationMinutes,
                Notes = "Auto-completed when next patient was admitted to doctor",
                IsSyntheticDemo = false,
                CreatedAt = endTime
            });

            _db.QueueEvents.Add(new QueueEvent
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalId,
                QueueSessionId = prevBooking.QueueSessionId,
                BookingId = prevBooking.Id,
                EventType = QueueEventType.Completed,
                Timestamp = endTime,
                TriggeredByUserId = operatorUserId,
                MetadataJson = JsonSerializer.Serialize(new { DurationMinutes = durationMinutes, Note = "Auto-completed on next patient admit" })
            });

            await _notifications.CreateNotificationAsync(
                prevBooking.PatientUserId,
                "Consultation Completed",
                $"Your consultation with {prevBooking.Doctor.FullName} has ended. Thank you for visiting!",
                NotificationType.ConsultationCompleted,
                prevBooking.Id);

            await _hub.Clients.Group($"QueueSession_{prevBooking.QueueSessionId}").SendAsync("ConsultationCompleted", new
            {
                QueueSessionId = prevBooking.QueueSessionId,
                BookingId = prevBooking.Id,
                TokenNumber = prevBooking.TokenNumber,
                DurationMinutes = durationMinutes
            });
        }

        await _db.SaveChangesAsync();
    }

    private static QueueSessionDto MapSessionToDto(QueueSession qs)
    {
        var servingBooking = qs.Bookings.FirstOrDefault(b => b.Status == BookingStatus.Serving);
        var waitingBookings = qs.Bookings
            .Where(b => b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called)
            .OrderBy(b => b.TokenNumber)
            .Select(MapBookingToDto)
            .ToList();

        return new QueueSessionDto
        {
            Id = qs.Id,
            HospitalId = qs.HospitalId,
            DoctorId = qs.DoctorId,
            DoctorName = qs.Doctor?.FullName ?? "Doctor",
            DepartmentId = qs.DepartmentId,
            DepartmentName = qs.Department?.Name ?? "Department",
            SessionDate = qs.SessionDate.ToString("yyyy-MM-dd"),
            StartTime = qs.StartTime.ToString(@"hh\:mm"),
            EndTime = qs.EndTime.ToString(@"hh\:mm"),
            Status = qs.Status,
            MaxCapacity = qs.MaxCapacity,
            CurrentTokenNumber = qs.CurrentTokenNumber,
            TotalBookings = qs.Bookings.Count,
            WaitingCount = qs.Bookings.Count(b => b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called),
            CompletedCount = qs.Bookings.Count(b => b.Status == BookingStatus.Completed),
            CurrentServingBooking = servingBooking != null ? MapBookingToDto(servingBooking) : null,
            WaitingBookings = waitingBookings
        };
    }

    private static BookingResponseDto MapBookingToDto(Booking b)
    {
        return new BookingResponseDto
        {
            Id = b.Id,
            HospitalId = b.HospitalId,
            QueueSessionId = b.QueueSessionId,
            DepartmentId = b.DepartmentId,
            DoctorId = b.DoctorId,
            PatientUserId = b.PatientUserId,
            PatientName = b.Patient?.FullName ?? "Patient",
            TokenNumber = b.TokenNumber,
            Status = b.Status,
            BookingTime = b.BookingTime,
            EstimatedServiceTime = b.EstimatedServiceTime,
            ActualCallTime = b.ActualCallTime,
            ActualStartTime = b.ActualStartTime,
            ActualEndTime = b.ActualEndTime,
            CancellationReason = b.CancellationReason,
            IsBuildingCheckedIn = b.IsBuildingCheckedIn,
            BuildingCheckInTime = b.BuildingCheckInTime,
            IsDoctorRoomCheckedIn = b.IsDoctorRoomCheckedIn,
            DoctorRoomCheckInTime = b.DoctorRoomCheckInTime,
            CallTimeoutTime = b.CallTimeoutTime
        };
    }
}
