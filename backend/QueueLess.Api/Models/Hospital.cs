using System.ComponentModel.DataAnnotations;

namespace QueueLess.Api.Models;

public static class HospitalStatus
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Suspended = "SUSPENDED";
}

public class Hospital
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RegistrationNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Area { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [Required]
    [MaxLength(25)]
    public string ContactPhone { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string ContactEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = HospitalStatus.Pending;

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public int InitialConsultationMinutes { get; set; } = 10;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<HospitalMember> Members { get; set; } = new List<HospitalMember>();
    public ICollection<Department> Departments { get; set; } = new List<Department>();
    public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
    public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
    public ICollection<QueueSession> QueueSessions { get; set; } = new List<QueueSession>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<ConsultationRecord> ConsultationRecords { get; set; } = new List<ConsultationRecord>();
    public ICollection<MLModelVersion> MLModelVersions { get; set; } = new List<MLModelVersion>();
}
