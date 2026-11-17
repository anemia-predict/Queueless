using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QueueLess.Api.Data;
using QueueLess.Api.DTOs;
using QueueLess.Api.Hubs;
using QueueLess.Api.Models;

namespace QueueLess.Api.Services;

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<QueueHub> _hub;
    private readonly IAIPredictionService _ai;
    private readonly INotificationService _notifications;

    public BookingService(
        ApplicationDbContext db,
        IHubContext<QueueHub> hub,
        IAIPredictionService ai,
        INotificationService notifications)
    {
        _db = db;
        _hub = hub;
        _ai = ai;
        _notifications = notifications;
    }

    public async Task<BookingResponseDto> CreateBookingAsync(CreateBookingRequestDto dto, Guid patientUserId)
    {
        // 1. Idempotency Check
        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            var existingRecord = await _db.IdempotencyRecords
                .FirstOrDefaultAsync(r => r.Key == dto.IdempotencyKey && r.UserId == patientUserId);

            if (existingRecord != null)
            {
                var existingBooking = JsonSerializer.Deserialize<BookingResponseDto>(existingRecord.ResponseBody);
                if (existingBooking != null)
                {
                    return existingBooking;
                }
            }
        }

        // 2. Begin Atomic Transaction
        using var tx = await _db.Database.BeginTransactionAsync();

        // 3. Hospital Approval Check
        var hospital = await _db.Hospitals.FindAsync(dto.HospitalId);
        if (hospital == null || hospital.Status != HospitalStatus.Approved)
        {
            throw new InvalidOperationException("Hospital is not approved to accept queue bookings.");
        }

        // 4. Queue Session Check with lock/capacity check
        QueueSession? session = null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (dto.QueueSessionId.HasValue && dto.QueueSessionId.Value != Guid.Empty)
        {
            session = await _db.QueueSessions
                .Include(qs => qs.Bookings)
                .Include(qs => qs.Doctor)
                .Include(qs => qs.Department)
                .FirstOrDefaultAsync(qs => qs.Id == dto.QueueSessionId.Value && qs.HospitalId == dto.HospitalId);

            // If the passed session is from a previous day, auto-close it and do NOT use it for today's tokens
            if (session != null && session.SessionDate != default && session.SessionDate < today)
            {
                if (session.Status == QueueSessionStatus.Open || session.Status == QueueSessionStatus.Paused)
                {
                    session.Status = QueueSessionStatus.Closed;
                    session.UpdatedAt = DateTime.UtcNow;
                    foreach (var oldB in session.Bookings.Where(b => b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called || b.Status == BookingStatus.Serving))
                    {
                        oldB.Status = BookingStatus.NoShow;
                        oldB.CancellationReason = $"Expired: Clinic session date ({session.SessionDate:yyyy-MM-dd}) ended without attendance.";
                        oldB.UpdatedAt = DateTime.UtcNow;
                    }
                }
                session = null; // Forces creation / lookup of a fresh queue session for TODAY starting from Token #1
            }
        }

        if (session == null)
        {
            // Auto-resolve open session for this doctor and department today
            session = await _db.QueueSessions
                .Include(qs => qs.Bookings)
                .Include(qs => qs.Doctor)
                .Include(qs => qs.Department)
                .FirstOrDefaultAsync(qs => qs.HospitalId == dto.HospitalId && qs.DoctorId == dto.DoctorId && qs.SessionDate == today && qs.Status == QueueSessionStatus.Open);
        }

        if (session == null)
        {
            // Auto-create an open queue session for today for this doctor
            session = new QueueSession
            {
                Id = Guid.NewGuid(),
                HospitalId = dto.HospitalId,
                DoctorId = dto.DoctorId,
                DepartmentId = dto.DepartmentId,
                SessionDate = today,
                StartTime = TimeSpan.FromHours(9),
                EndTime = TimeSpan.FromHours(18),
                MaxCapacity = 50,
                CurrentTokenNumber = 0,
                Status = QueueSessionStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.QueueSessions.Add(session);
            await _db.SaveChangesAsync();

            // Reload with relations
            session = await _db.QueueSessions
                .Include(qs => qs.Bookings)
                .Include(qs => qs.Doctor)
                .Include(qs => qs.Department)
                .FirstAsync(qs => qs.Id == session.Id);
        }

        if (session.Status != QueueSessionStatus.Open)
        {
            throw new InvalidOperationException($"Queue session is currently {session.Status} and cannot accept new bookings.");
        }

        var activeBookingsCount = session.Bookings.Count(b => b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called || b.Status == BookingStatus.Serving);
        if (activeBookingsCount >= session.MaxCapacity)
        {
            throw new InvalidOperationException($"Queue session has reached maximum capacity of {session.MaxCapacity} patients.");
        }

        // 5. Generate Atomic Next Token Number scoped to this session
        var nextToken = session.CurrentTokenNumber + 1;
        session.CurrentTokenNumber = nextToken;
        session.UpdatedAt = DateTime.UtcNow;

        var patient = await _db.Users.FindAsync(patientUserId);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            HospitalId = dto.HospitalId,
            QueueSessionId = session.Id,
            PatientUserId = patientUserId,
            DoctorId = dto.DoctorId,
            DepartmentId = dto.DepartmentId,
            TokenNumber = nextToken,
            Status = BookingStatus.Waiting,
            BookingTime = DateTime.UtcNow,
            IdempotencyKey = dto.IdempotencyKey,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Bookings.Add(booking);

        // Queue Event
        var queueEvent = new QueueEvent
        {
            Id = Guid.NewGuid(),
            HospitalId = dto.HospitalId,
            QueueSessionId = session.Id,
            BookingId = booking.Id,
            EventType = QueueEventType.Created,
            Timestamp = DateTime.UtcNow,
            TriggeredByUserId = patientUserId,
            MetadataJson = JsonSerializer.Serialize(new { TokenNumber = nextToken })
        };
        _db.QueueEvents.Add(queueEvent);

        await _db.SaveChangesAsync();

        // 6. Calculate AI / Baseline Waiting Time
        var waitPrediction = await _ai.CalculateWaitingTimeAsync(booking.Id, savePrediction: true);

        booking.EstimatedServiceTime = DateTime.UtcNow.AddMinutes(waitPrediction.PredictedWaitMinutes);
        await _db.SaveChangesAsync();

        var response = new BookingResponseDto
        {
            Id = booking.Id,
            HospitalId = hospital.Id,
            HospitalName = hospital.Name,
            QueueSessionId = session.Id,
            DepartmentId = session.DepartmentId,
            DepartmentName = session.Department.Name,
            DoctorId = session.DoctorId,
            DoctorName = session.Doctor.FullName,
            PatientUserId = patientUserId,
            PatientName = patient?.FullName ?? "Patient",
            TokenNumber = booking.TokenNumber,
            Status = booking.Status,
            BookingTime = booking.BookingTime,
            EstimatedServiceTime = booking.EstimatedServiceTime,
            PositionInQueue = session.Bookings.Count(b => (b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called) && b.TokenNumber < booking.TokenNumber),
            EstimatedWaitMinutes = waitPrediction.PredictedWaitMinutes,
            PredictionMethod = waitPrediction.PredictionMethod
        };

        // 7. Store Idempotency Record if key provided
        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            var rawKey = $"{dto.HospitalId}_{dto.QueueSessionId}_{nextToken}";
            var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawKey));
            var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

            _db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = dto.IdempotencyKey,
                UserId = patientUserId,
                RequestHash = hashHex,
                ResponseStatusCode = 201,
                ResponseBody = JsonSerializer.Serialize(response),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            await _db.SaveChangesAsync();
        }

        await tx.CommitAsync();

        // 8. Broadcast to Queue Group via SignalR
        await _hub.Clients.Group($"QueueSession_{session.Id}")
            .SendAsync("QueueUpdated", new
            {
                QueueSessionId = session.Id,
                Action = "BOOKING_CREATED",
                TokenNumber = booking.TokenNumber,
                TotalWaiting = session.Bookings.Count(b => b.Status == BookingStatus.Waiting)
            });

        // 9. Send In-App Notification to Patient
        await _notifications.CreateNotificationAsync(
            patientUserId,
            "Booking Confirmed",
            $"Token #{booking.TokenNumber} booked at {hospital.Name} for {session.Doctor.FullName}. Estimated wait: {waitPrediction.PredictedWaitMinutes:F0} mins.",
            NotificationType.BookingConfirmed,
            booking.Id);

        return response;
    }

    public async Task<BookingResponseDto> CancelBookingAsync(Guid bookingId, Guid patientUserId, string? reason)
    {
        var booking = await _db.Bookings
            .Include(b => b.Hospital)
            .Include(b => b.Doctor)
            .Include(b => b.Department)
            .Include(b => b.Patient)
            .Include(b => b.QueueSession)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            throw new KeyNotFoundException("Booking not found.");
        }

        // Authorization check: patient who booked or hospital admin
        if (booking.PatientUserId != patientUserId)
        {
            var isHospitalAdmin = await _db.HospitalMembers
                .AnyAsync(hm => hm.HospitalId == booking.HospitalId && hm.UserId == patientUserId);

            if (!isHospitalAdmin)
            {
                throw new UnauthorizedAccessException("You are not authorized to cancel this booking.");
            }
        }

        if (booking.Status != BookingStatus.Waiting)
        {
            throw new InvalidOperationException($"Cannot cancel booking with status '{booking.Status}'. Only waiting bookings can be cancelled.");
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancellationReason = reason ?? "Cancelled by patient";
        booking.UpdatedAt = DateTime.UtcNow;

        _db.QueueEvents.Add(new QueueEvent
        {
            Id = Guid.NewGuid(),
            HospitalId = booking.HospitalId,
            QueueSessionId = booking.QueueSessionId,
            BookingId = booking.Id,
            EventType = QueueEventType.Cancelled,
            Timestamp = DateTime.UtcNow,
            TriggeredByUserId = patientUserId,
            MetadataJson = JsonSerializer.Serialize(new { Reason = reason })
        });

        await _db.SaveChangesAsync();

        // Broadcast cancellation via SignalR
        await _hub.Clients.Group($"QueueSession_{booking.QueueSessionId}")
            .SendAsync("QueueUpdated", new
            {
                QueueSessionId = booking.QueueSessionId,
                Action = "BOOKING_CANCELLED",
                TokenNumber = booking.TokenNumber
            });

        // Patient notification
        await _notifications.CreateNotificationAsync(
            booking.PatientUserId,
            "Booking Cancelled",
            $"Your booking for Token #{booking.TokenNumber} at {booking.Hospital.Name} has been cancelled.",
            NotificationType.BookingCancelled,
            booking.Id);

        return new BookingResponseDto
        {
            Id = booking.Id,
            HospitalId = booking.HospitalId,
            HospitalName = booking.Hospital.Name,
            QueueSessionId = booking.QueueSessionId,
            DepartmentId = booking.DepartmentId,
            DepartmentName = booking.Department.Name,
            DoctorId = booking.DoctorId,
            DoctorName = booking.Doctor.FullName,
            PatientUserId = booking.PatientUserId,
            PatientName = booking.Patient.FullName,
            TokenNumber = booking.TokenNumber,
            Status = booking.Status,
            BookingTime = booking.BookingTime,
            CancellationReason = booking.CancellationReason,
            IsBuildingCheckedIn = booking.IsBuildingCheckedIn,
            BuildingCheckInTime = booking.BuildingCheckInTime,
            IsDoctorRoomCheckedIn = booking.IsDoctorRoomCheckedIn,
            DoctorRoomCheckInTime = booking.DoctorRoomCheckInTime,
            CallTimeoutTime = booking.CallTimeoutTime
        };
    }

    public async Task<List<BookingResponseDto>> GetPatientBookingsAsync(Guid patientUserId)
    {
        var bookings = await _db.Bookings
            .Where(b => b.PatientUserId == patientUserId)
            .Include(b => b.Hospital)
            .Include(b => b.Doctor)
            .Include(b => b.Department)
            .Include(b => b.QueueSession)
                .ThenInclude(qs => qs.Bookings)
            .OrderByDescending(b => b.BookingTime)
            .ToListAsync();

        var result = new List<BookingResponseDto>();

        foreach (var b in bookings)
        {
            var position = b.QueueSession.Bookings
                .Count(ob => (ob.Status == BookingStatus.Waiting || ob.Status == BookingStatus.Called) && ob.TokenNumber < b.TokenNumber);

            double waitMinutes = 0;
            string method = "BASELINE";
            if (b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called)
            {
                var wait = await _ai.CalculateWaitingTimeAsync(b.Id, savePrediction: false);
                waitMinutes = wait.PredictedWaitMinutes;
                method = wait.PredictionMethod;
            }

            var dto = new BookingResponseDto
            {
                Id = b.Id,
                HospitalId = b.HospitalId,
                HospitalName = b.Hospital.Name,
                QueueSessionId = b.QueueSessionId,
                DepartmentId = b.DepartmentId,
                DepartmentName = b.Department.Name,
                DoctorId = b.DoctorId,
                DoctorName = b.Doctor.FullName,
                PatientUserId = b.PatientUserId,
                PatientName = b.Patient?.FullName ?? "Patient",
                TokenNumber = b.TokenNumber,
                Status = b.Status,
                BookingTime = b.BookingTime,
                EstimatedServiceTime = b.EstimatedServiceTime,
                ActualCallTime = b.ActualCallTime,
                ActualStartTime = b.ActualStartTime,
                ActualEndTime = b.ActualEndTime,
                PositionInQueue = position,
                EstimatedWaitMinutes = waitMinutes,
                PredictionMethod = method,
                CancellationReason = b.CancellationReason,
                IsBuildingCheckedIn = b.IsBuildingCheckedIn,
                BuildingCheckInTime = b.BuildingCheckInTime,
                IsDoctorRoomCheckedIn = b.IsDoctorRoomCheckedIn,
                DoctorRoomCheckInTime = b.DoctorRoomCheckInTime,
                CallTimeoutTime = b.CallTimeoutTime
            };

            result.Add(dto);
        }

        return result;
    }

    public async Task<BookingResponseDto> GetBookingByIdAsync(Guid bookingId, Guid userId)
    {
        var b = await _db.Bookings
            .Include(b => b.Hospital)
            .Include(b => b.Doctor)
            .Include(b => b.Department)
            .Include(b => b.Patient)
            .Include(b => b.QueueSession)
                .ThenInclude(qs => qs.Bookings)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (b == null) throw new KeyNotFoundException("Booking not found.");

        var position = b.QueueSession.Bookings
            .Count(ob => (ob.Status == BookingStatus.Waiting || ob.Status == BookingStatus.Called) && ob.TokenNumber < b.TokenNumber);

        var wait = await _ai.CalculateWaitingTimeAsync(b.Id, savePrediction: false);

        return new BookingResponseDto
        {
            Id = b.Id,
            HospitalId = b.HospitalId,
            HospitalName = b.Hospital.Name,
            QueueSessionId = b.QueueSessionId,
            DepartmentId = b.DepartmentId,
            DepartmentName = b.Department.Name,
            DoctorId = b.DoctorId,
            DoctorName = b.Doctor.FullName,
            PatientUserId = b.PatientUserId,
            PatientName = b.Patient.FullName,
            TokenNumber = b.TokenNumber,
            Status = b.Status,
            BookingTime = b.BookingTime,
            EstimatedServiceTime = b.EstimatedServiceTime,
            ActualCallTime = b.ActualCallTime,
            ActualStartTime = b.ActualStartTime,
            ActualEndTime = b.ActualEndTime,
            PositionInQueue = position,
            EstimatedWaitMinutes = wait.PredictedWaitMinutes,
            PredictionMethod = wait.PredictionMethod,
            CancellationReason = b.CancellationReason,
            IsBuildingCheckedIn = b.IsBuildingCheckedIn,
            BuildingCheckInTime = b.BuildingCheckInTime,
            IsDoctorRoomCheckedIn = b.IsDoctorRoomCheckedIn,
            DoctorRoomCheckInTime = b.DoctorRoomCheckInTime,
            CallTimeoutTime = b.CallTimeoutTime
        };
    }

    public async Task<QueueStatusResponseDto> GetQueueStatusAsync(Guid bookingId)
    {
        var booking = await _db.Bookings
            .Include(b => b.QueueSession)
                .ThenInclude(qs => qs.Bookings)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) throw new KeyNotFoundException("Booking not found.");

        var session = booking.QueueSession;

        var position = session.Bookings
            .Count(b => b.Status == BookingStatus.Waiting && b.TokenNumber <= booking.TokenNumber);

        var servingBooking = session.Bookings.FirstOrDefault(b => b.Status == BookingStatus.Serving);
        var totalWaiting = session.Bookings.Count(b => b.Status == BookingStatus.Waiting);

        var wait = await _ai.CalculateWaitingTimeAsync(booking.Id, savePrediction: false);

        return new QueueStatusResponseDto
        {
            BookingId = booking.Id,
            TokenNumber = booking.TokenNumber,
            BookingStatus = booking.Status,
            PositionInQueue = position,
            CurrentlyServingToken = servingBooking?.TokenNumber ?? 0,
            TotalWaiting = totalWaiting,
            EstimatedWaitMinutes = wait.PredictedWaitMinutes,
            PredictionMethod = wait.PredictionMethod,
            MethodExplanation = wait.MethodExplanation,
            SessionStatus = session.Status,
            LastUpdated = DateTime.UtcNow
        };
    }
}
