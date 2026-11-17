using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public static class BookingStatus
{
    public const string Waiting = "WAITING";
    public const string Called = "CALLED";
    public const string Serving = "SERVING";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
    public const string NoShow = "NO_SHOW";
}

public class Booking
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HospitalId { get; set; }

    [Required]
    public Guid QueueSessionId { get; set; }

    [Required]
    public Guid PatientUserId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public int TokenNumber { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = BookingStatus.Waiting;

    public DateTime BookingTime { get; set; } = DateTime.UtcNow;
    public DateTime? EstimatedServiceTime { get; set; }
    public DateTime? ActualCallTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }

    // Two-stage check-in process
    public bool IsBuildingCheckedIn { get; set; } = false;
    public DateTime? BuildingCheckInTime { get; set; }
    public bool IsDoctorRoomCheckedIn { get; set; } = false;
    public DateTime? DoctorRoomCheckInTime { get; set; }
    public DateTime? CallTimeoutTime { get; set; }

    [MaxLength(300)]
    public string? CancellationReason { get; set; }

    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital Hospital { get; set; } = null!;

    [ForeignKey(nameof(QueueSessionId))]
    public QueueSession QueueSession { get; set; } = null!;

    [ForeignKey(nameof(PatientUserId))]
    public User Patient { get; set; } = null!;

    [ForeignKey(nameof(DoctorId))]
    public Doctor Doctor { get; set; } = null!;

    [ForeignKey(nameof(DepartmentId))]
    public Department Department { get; set; } = null!;

    public ConsultationRecord? ConsultationRecord { get; set; }
    public ICollection<QueueEvent> QueueEvents { get; set; } = new List<QueueEvent>();
    public ICollection<MLPrediction> MLPredictions { get; set; } = new List<MLPrediction>();
}
