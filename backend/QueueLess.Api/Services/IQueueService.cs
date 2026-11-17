using QueueLess.Api.DTOs;

namespace QueueLess.Api.Services;

public interface IQueueService
{
    Task<List<QueueSessionDto>> GetQueueSessionsAsync(Guid hospitalId, Guid? doctorId = null);
    Task<QueueSessionDto> GetQueueSessionDetailsAsync(Guid hospitalId, Guid sessionId);
    Task<QueueSessionDto> OpenQueueSessionAsync(Guid hospitalId, OpenQueueSessionDto dto);
    Task<QueueActionResponseDto> CallNextAsync(Guid hospitalId, Guid sessionId, Guid operatorUserId);
    Task<QueueActionResponseDto> StartConsultationAsync(Guid hospitalId, Guid bookingId, Guid operatorUserId);
    Task<QueueActionResponseDto> CompleteConsultationAsync(Guid hospitalId, Guid bookingId, Guid operatorUserId, string? notes = null);
    Task<QueueActionResponseDto> MarkNoShowAsync(Guid hospitalId, Guid bookingId, Guid operatorUserId);
    Task<QueueActionResponseDto> PauseQueueAsync(Guid hospitalId, Guid sessionId, Guid operatorUserId);
    Task<QueueActionResponseDto> ResumeQueueAsync(Guid hospitalId, Guid sessionId, Guid operatorUserId);
    Task<QueueActionResponseDto> ConfirmBuildingCheckInAsync(Guid hospitalId, Guid bookingId, Guid operatorUserId);
    Task<QueueActionResponseDto> ConfirmDoctorRoomCheckInAsync(Guid hospitalId, Guid bookingId, Guid operatorUserId);
    Task<QueueActionResponseDto> CheckAndForfeitExpiredTokensAsync(Guid hospitalId, Guid sessionId, Guid operatorUserId);
}
