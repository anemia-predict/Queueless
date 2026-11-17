using QueueLess.Api.DTOs;

namespace QueueLess.Api.Services;

public interface IAnalyticsService
{
    Task<HospitalAnalyticsDto> GetHospitalAnalyticsAsync(Guid hospitalId);
    Task<PlatformAnalyticsDto> GetPlatformAnalyticsAsync();
}
