using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public class ConsultationRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HospitalId { get; set; }

    [Required]
    public Guid BookingId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public Guid QueueSessionId { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public double DurationSeconds { get; set; }
    public double DurationMinutes { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsSyntheticDemo { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital Hospital { get; set; } = null!;

    [ForeignKey(nameof(BookingId))]
    public Booking Booking { get; set; } = null!;

    [ForeignKey(nameof(DoctorId))]
    public Doctor Doctor { get; set; } = null!;

    [ForeignKey(nameof(DepartmentId))]
    public Department Department { get; set; } = null!;

    [ForeignKey(nameof(QueueSessionId))]
    public QueueSession QueueSession { get; set; } = null!;
}
