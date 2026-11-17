using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueLess.Api.DTOs;
using QueueLess.Api.Services;

namespace QueueLess.Api.Controllers;

[Authorize]
[Route("api/v1/bookings")]
public class BookingsController : BaseApiController
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings)
    {
        _bookings = bookings;
    }

    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequestDto dto)
    {
        try
        {
            var patientUserId = GetUserId();
            var booking = await _bookings.CreateBookingAsync(dto, patientUserId);
            return StatusCode(201, booking);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyBookings()
    {
        var patientUserId = GetUserId();
        var bookings = await _bookings.GetPatientBookingsAsync(patientUserId);
        return Ok(bookings);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBookingById(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var booking = await _bookings.GetBookingByIdAsync(id, userId);
            return Ok(booking);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(Guid id, [FromBody] CancelBookingRequestDto? dto)
    {
        try
        {
            var userId = GetUserId();
            var result = await _bookings.CancelBookingAsync(id, userId, dto?.Reason);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/queue-status")]
    public async Task<IActionResult> GetQueueStatus(Guid id)
    {
        try
        {
            var status = await _bookings.GetQueueStatusAsync(id);
            return Ok(status);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
