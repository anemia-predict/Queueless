namespace QueueLess.Api.DTOs;

public class HospitalAnalyticsDto
{
    public Guid HospitalId { get; set; }
    public string HospitalName { get; set; } = string.Empty;
    public int TotalBookingsToday { get; set; }
    public int WaitingPatients { get; set; }
    public int CompletedConsultations { get; set; }
    public int CancelledBookings { get; set; }
    public int NoShowCount { get; set; }
    public double AverageActualConsultationMinutes { get; set; }
    public double AverageActualWaitingMinutes { get; set; }
    public double EstimatedVsActualVarianceMinutes { get; set; }
    public List<DepartmentQueueStatDto> DepartmentStats { get; set; } = new();
    public List<HourlyWaitTimeStatDto> HourlyStats { get; set; } = new();
}

public class DepartmentQueueStatDto
{
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalBookings { get; set; }
    public int WaitingCount { get; set; }
    public int CompletedCount { get; set; }
    public double AverageConsultationDuration { get; set; }
}

public class HourlyWaitTimeStatDto
{
    public int Hour { get; set; }
    public double AvgPredictedWait { get; set; }
    public double AvgActualWait { get; set; }
}

public class PlatformAnalyticsDto
{
    public int TotalRegisteredHospitals { get; set; }
    public int PendingApprovals { get; set; }
    public int ApprovedHospitals { get; set; }
    public int SuspendedHospitals { get; set; }
    public int TotalPatients { get; set; }
    public int TotalBookings { get; set; }
    public int TotalConsultationsCompleted { get; set; }
    public List<AIModelMetricsDto> ActiveModels { get; set; } = new();
}

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
