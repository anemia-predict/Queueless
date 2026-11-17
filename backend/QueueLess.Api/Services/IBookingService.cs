using QueueLess.Api.DTOs;

namespace QueueLess.Api.Services;

public interface IBookingService
{
    Task<BookingResponseDto> CreateBookingAsync(CreateBookingRequestDto dto, Guid patientUserId);
    Task<BookingResponseDto> CancelBookingAsync(Guid bookingId, Guid patientUserId, string? reason);
    Task<List<BookingResponseDto>> GetPatientBookingsAsync(Guid patientUserId);
    Task<BookingResponseDto> GetBookingByIdAsync(Guid bookingId, Guid userId);
    Task<QueueStatusResponseDto> GetQueueStatusAsync(Guid bookingId);
}
