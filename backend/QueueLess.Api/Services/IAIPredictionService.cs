using QueueLess.Api.DTOs;

namespace QueueLess.Api.Services;

public interface IAIPredictionService
{
    Task<WaitingTimePredictionDto> CalculateWaitingTimeAsync(Guid bookingId, bool savePrediction = true);
    Task<WaitingTimePredictionDto> EstimateQueueWaitAsync(Guid hospitalId, Guid departmentId, int peopleAhead, bool hasActiveServing, double remainingServingMinutes);
    Task<AIModelMetricsDto> TrainModelAsync(Guid? hospitalId, bool includeSynthetic = true);
    Task<int> SeedSyntheticConsultationDataAsync(Guid hospitalId, int recordCount = 60);
    Task<AIModelMetricsDto?> GetActiveModelMetricsAsync(Guid? hospitalId);
    Task<List<AIModelMetricsDto>> GetAllModelsAsync();
}
