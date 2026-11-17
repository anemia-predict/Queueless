using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueueLess.Api.Data;
using QueueLess.Api.DTOs;
using QueueLess.Api.Models;
using QueueLess.Api.Services;

namespace QueueLess.Api.Controllers;

public class CompleteConsultationRequestDto
{
    public string? Notes { get; set; }
}

[Authorize]
[Route("api/v1/hospital")]
public class QueueManagementController : BaseApiController
{
    private readonly IQueueService _queues;
    private readonly IHospitalService _hospitals;
    private readonly ApplicationDbContext _db;

    public QueueManagementController(IQueueService queues, IHospitalService hospitals, ApplicationDbContext db)
    {
        _queues = queues;
        _hospitals = hospitals;
        _db = db;
    }

    private async Task<Guid> ResolveHospitalIdAsync()
    {
        var hId = GetHospitalId();
        if (hId.HasValue) return hId.Value;

        var userId = GetUserId();

        // 1. Check if user is a Doctor
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
        if (doctor != null) return doctor.HospitalId;

        // 2. Check if user is a member of any hospital
        var member = await _db.HospitalMembers.FirstOrDefaultAsync(m => m.UserId == userId);
        if (member != null) return member.HospitalId;

        // 3. Fallback to hospital where admin
        try
        {
            var hospital = await _hospitals.GetHospitalByAdminUserIdAsync(userId);
            if (hospital != null) return hospital.Id;
        }
        catch { }

        // 4. Default to first active hospital in database
        var firstHospital = await _db.Hospitals.FirstOrDefaultAsync(h => h.Status == "APPROVED");
        if (firstHospital != null) return firstHospital.Id;

        throw new UnauthorizedAccessException("Hospital could not be resolved for current user.");
    }

    [HttpGet("queues")]
    public async Task<IActionResult> GetQueues([FromQuery] Guid? doctorId)
    {
        var hospitalId = await ResolveHospitalIdAsync();
        var sessions = await _queues.GetQueueSessionsAsync(hospitalId, doctorId);
        return Ok(sessions);
    }

    [HttpGet("queues/{id}")]
    public async Task<IActionResult> GetQueueDetails(Guid id)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var session = await _queues.GetQueueSessionDetailsAsync(hospitalId, id);
            return Ok(session);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("queues")]
    public async Task<IActionResult> OpenQueueSession([FromBody] OpenQueueSessionDto dto)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var session = await _queues.OpenQueueSessionAsync(hospitalId, dto);
            return StatusCode(201, session);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("queues/{id}/call-next")]
    public async Task<IActionResult> CallNext(Guid id)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var userId = GetUserId();
            var result = await _queues.CallNextAsync(hospitalId, id, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bookings/{id}/start")]
    public async Task<IActionResult> StartConsultation(Guid id)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var userId = GetUserId();
            var result = await _queues.StartConsultationAsync(hospitalId, id, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bookings/{id}/complete")]
    public async Task<IActionResult> CompleteConsultation(Guid id, [FromBody] CompleteConsultationRequestDto? dto)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var userId = GetUserId();
            var result = await _queues.CompleteConsultationAsync(hospitalId, id, userId, dto?.Notes);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bookings/{id}/no-show")]
    public async Task<IActionResult> MarkNoShow(Guid id)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var userId = GetUserId();
            var result = await _queues.MarkNoShowAsync(hospitalId, id, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("queues/{id}/pause")]
    public async Task<IActionResult> PauseQueue(Guid id)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var userId = GetUserId();
            var result = await _queues.PauseQueueAsync(hospitalId, id, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("queues/{id}/resume")]
    public async Task<IActionResult> ResumeQueue(Guid id)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var userId = GetUserId();
            var result = await _queues.ResumeQueueAsync(hospitalId, id, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("bookings/verify")]
    public async Task<IActionResult> VerifyBooking([FromQuery] string query)
    {
        try
        {
            Guid hospitalId = Guid.Empty;
            try
            {
                hospitalId = await ResolveHospitalIdAsync();
            }
            catch
            {
                // Optional: hospital fallback for global verification
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Scan data or token number is required." });
            }

            Guid bookingId = Guid.Empty;
            int tokenNumber = -1;

            // Try parsing as JSON first (from QR code)
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(query);
                if (doc.RootElement.TryGetProperty("bookingId", out var bIdProp))
                {
                    Guid.TryParse(bIdProp.GetString(), out bookingId);
                }
                if (doc.RootElement.TryGetProperty("tokenNumber", out var tNumProp))
                {
                    tokenNumber = tNumProp.GetInt32();
                }
            }
            catch
            {
                // Not JSON, try GUID or integer
                var trimmed = query.Trim().TrimStart('#');
                if (Guid.TryParse(trimmed, out var parsedGuid))
                {
                    bookingId = parsedGuid;
                }
                else if (int.TryParse(trimmed, out var parsedToken))
                {
                    tokenNumber = parsedToken;
                }
            }

            var bookingQuery = _db.Bookings
                .Include(b => b.Patient)
                .Include(b => b.Doctor)
                .Include(b => b.Department)
                .Include(b => b.QueueSession)
                .AsQueryable();

            if (hospitalId != Guid.Empty)
            {
                bookingQuery = bookingQuery.Where(b => b.HospitalId == hospitalId);
            }

            Booking? booking = null;
            if (bookingId != Guid.Empty)
            {
                booking = await bookingQuery.FirstOrDefaultAsync(b => b.Id == bookingId);
            }
            else if (tokenNumber > 0)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                booking = await bookingQuery
                    .OrderByDescending(b => b.BookingTime)
                    .FirstOrDefaultAsync(b => b.TokenNumber == tokenNumber && b.QueueSession.SessionDate == today);
                
                if (booking == null)
                {
                    // Fallback to latest with this token number
                    booking = await bookingQuery
                        .OrderByDescending(b => b.BookingTime)
                        .FirstOrDefaultAsync(b => b.TokenNumber == tokenNumber);
                }
            }

            if (booking == null)
            {
                return NotFound(new { message = "Booking token not found for this hospital." });
            }

            return Ok(new
            {
                booking.Id,
                booking.TokenNumber,
                booking.Status,
                PatientName = booking.Patient.FullName,
                PatientEmail = booking.Patient.Email,
                DoctorName = booking.Doctor.FullName,
                DepartmentName = booking.Department.Name,
                booking.BookingTime,
                booking.EstimatedServiceTime,
                booking.ActualCallTime,
                booking.CallTimeoutTime,
                booking.IsBuildingCheckedIn,
                booking.BuildingCheckInTime,
                booking.IsDoctorRoomCheckedIn,
                booking.DoctorRoomCheckInTime,
                booking.QueueSessionId,
                IsVerified = true
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bookings/{id}/building-check-in")]
    public async Task<IActionResult> BuildingCheckIn(Guid id)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var userId = GetUserId();
            var result = await _queues.ConfirmBuildingCheckInAsync(hospitalId, id, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bookings/{id}/room-check-in")]
    public async Task<IActionResult> RoomCheckIn(Guid id)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var userId = GetUserId();
            var result = await _queues.ConfirmDoctorRoomCheckInAsync(hospitalId, id, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("queues/{id}/forfeit-expired")]
    public async Task<IActionResult> ForfeitExpired(Guid id)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var userId = GetUserId();
            var result = await _queues.CheckAndForfeitExpiredTokensAsync(hospitalId, id, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bookings/{id}/check-in")]
    public async Task<IActionResult> CheckInBooking(Guid id)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var userId = GetUserId();
            var result = await _queues.ConfirmBuildingCheckInAsync(hospitalId, id, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
