namespace QueueLess.Api.DTOs;

public class WaitingTimePredictionDto
{
    public Guid BookingId { get; set; }
    public Guid HospitalId { get; set; }
    public double PredictedWaitMinutes { get; set; }
    public double BaselineWaitMinutes { get; set; }
    public string PredictionMethod { get; set; } = string.Empty; // BASELINE, SHARED_MODEL, HOSPITAL_MODEL
    public string MethodExplanation { get; set; } = string.Empty;
    public string? ModelVersion { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> FeatureSnapshot { get; set; } = new();
}

public class AIModelMetricsDto
{
    public Guid? Id { get; set; }
    public string ModelType { get; set; } = string.Empty;
    public Guid? HospitalId { get; set; }
    public string VersionTag { get; set; } = string.Empty;
    public int TrainingRecordCount { get; set; }
    public double MeanAbsoluteError { get; set; }
    public double BaselineMAE { get; set; }
    public bool IsActive { get; set; }
    public DateTime TrainedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TrainModelRequestDto
{
    public Guid? HospitalId { get; set; } // Null for shared model
    public bool IncludeSynthetic { get; set; } = true;
}

public class SeedSyntheticDataRequestDto
{
    public Guid? HospitalId { get; set; }
    public int RecordCount { get; set; } = 60;
}
