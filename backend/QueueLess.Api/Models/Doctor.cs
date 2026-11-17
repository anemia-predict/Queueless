using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public class Doctor
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HospitalId { get; set; }

    public Guid? UserId { get; set; }

    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Specialization { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LicenseNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital Hospital { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ICollection<DoctorDepartment> DoctorDepartments { get; set; } = new List<DoctorDepartment>();
    public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
    public ICollection<QueueSession> QueueSessions { get; set; } = new List<QueueSession>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<ConsultationRecord> ConsultationRecords { get; set; } = new List<ConsultationRecord>();
}
