using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public class DoctorSchedule
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HospitalId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    [Range(0, 6)]
    public int DayOfWeek { get; set; } // 0=Sunday, 1=Monday...

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public int SlotDurationMinutes { get; set; } = 15;
    public int MaxPatientsPerSession { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital Hospital { get; set; } = null!;

    [ForeignKey(nameof(DoctorId))]
    public Doctor Doctor { get; set; } = null!;

    [ForeignKey(nameof(DepartmentId))]
    public Department Department { get; set; } = null!;

    public ICollection<QueueSession> QueueSessions { get; set; } = new List<QueueSession>();
}
