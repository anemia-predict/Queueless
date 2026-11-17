using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public class Department
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HospitalId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    public int DefaultConsultationMinutes { get; set; } = 10;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital Hospital { get; set; } = null!;

    public ICollection<DoctorDepartment> DoctorDepartments { get; set; } = new List<DoctorDepartment>();
    public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
    public ICollection<QueueSession> QueueSessions { get; set; } = new List<QueueSession>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
