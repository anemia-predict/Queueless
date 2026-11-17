using System.ComponentModel.DataAnnotations;

namespace QueueLess.Api.DTOs;

public class CreateBookingRequestDto
{
    [Required]
    public Guid HospitalId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    public Guid? QueueSessionId { get; set; }

    public string? IdempotencyKey { get; set; }
}

public class CancelBookingRequestDto
{
    [MaxLength(300)]
    public string? Reason { get; set; }
}

public class BookingResponseDto
{
    public Guid Id { get; set; }
    public Guid HospitalId { get; set; }
    public string HospitalName { get; set; } = string.Empty;
    public Guid QueueSessionId { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int TokenNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime BookingTime { get; set; }
    public DateTime? EstimatedServiceTime { get; set; }
    public DateTime? ActualCallTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    public int PositionInQueue { get; set; }
    public double EstimatedWaitMinutes { get; set; }
    public string PredictionMethod { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }

    // Two-stage check-in fields
    public bool IsBuildingCheckedIn { get; set; }
    public DateTime? BuildingCheckInTime { get; set; }
    public bool IsDoctorRoomCheckedIn { get; set; }
    public DateTime? DoctorRoomCheckInTime { get; set; }
    public DateTime? CallTimeoutTime { get; set; }
}

public class QueueStatusResponseDto
{
    public Guid BookingId { get; set; }
    public int TokenNumber { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
    public int PositionInQueue { get; set; }
    public int CurrentlyServingToken { get; set; }
    public int TotalWaiting { get; set; }
    public double EstimatedWaitMinutes { get; set; }
    public string PredictionMethod { get; set; } = string.Empty;
    public string MethodExplanation { get; set; } = string.Empty;
    public string SessionStatus { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
