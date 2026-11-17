using System.ComponentModel.DataAnnotations;

namespace QueueLess.Api.DTOs;

public class CreateDoctorDto
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Specialization { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LicenseNumber { get; set; } = string.Empty;

    public List<Guid> DepartmentIds { get; set; } = new();

    public string? DoctorEmail { get; set; }
    public string? DoctorPassword { get; set; }
}

public class UpdateDoctorDto
{
    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(100)]
    public string? Specialization { get; set; }

    [MaxLength(100)]
    public string? LicenseNumber { get; set; }

    public bool? IsActive { get; set; }

    public List<Guid>? DepartmentIds { get; set; }
}

public class DoctorDto
{
    public Guid Id { get; set; }
    public Guid HospitalId { get; set; }
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<DepartmentDto> Departments { get; set; } = new();
}

public class CreateScheduleDto
{
    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    [Range(0, 6)]
    public int DayOfWeek { get; set; } // 0=Sunday, 1=Monday...

    [Required]
    public string StartTime { get; set; } = "09:00:00"; // HH:mm:ss

    [Required]
    public string EndTime { get; set; } = "17:00:00"; // HH:mm:ss

    [Range(5, 120)]
    public int SlotDurationMinutes { get; set; } = 15;

    [Range(1, 200)]
    public int MaxPatientsPerSession { get; set; } = 30;
}

public class DoctorScheduleDto
{
    public Guid Id { get; set; }
    public Guid HospitalId { get; set; }
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int SlotDurationMinutes { get; set; }
    public int MaxPatientsPerSession { get; set; }
    public bool IsActive { get; set; }
}
