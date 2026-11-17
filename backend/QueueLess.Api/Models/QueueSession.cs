using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public static class QueueSessionStatus
{
    public const string Scheduled = "SCHEDULED";
    public const string Open = "OPEN";
    public const string Paused = "PAUSED";
    public const string Closed = "CLOSED";
}

public class QueueSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HospitalId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public Guid? ScheduleId { get; set; }

    public DateOnly SessionDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = QueueSessionStatus.Open;

    public int MaxCapacity { get; set; } = 30;
    public int CurrentTokenNumber { get; set; } = 0;
    public Guid? CurrentServingBookingId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital Hospital { get; set; } = null!;

    [ForeignKey(nameof(DoctorId))]
    public Doctor Doctor { get; set; } = null!;

    [ForeignKey(nameof(DepartmentId))]
    public Department Department { get; set; } = null!;

    [ForeignKey(nameof(ScheduleId))]
    public DoctorSchedule? Schedule { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<QueueEvent> QueueEvents { get; set; } = new List<QueueEvent>();
}
