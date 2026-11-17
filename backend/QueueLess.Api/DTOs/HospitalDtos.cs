using System.ComponentModel.DataAnnotations;

namespace QueueLess.Api.DTOs;

public class HospitalRegistrationDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string RegistrationNumber { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    public string? Area { get; set; }
    public string? State { get; set; }

    [Required, MaxLength(25)]
    public string ContactPhone { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(150)]
    public string ContactEmail { get; set; } = string.Empty;

    public int InitialConsultationMinutes { get; set; } = 10;
}

public class HospitalDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? State { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public int InitialConsultationMinutes { get; set; }
    public int DepartmentCount { get; set; }
    public int DoctorCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class HospitalUpdateDto
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public string? Area { get; set; }
    public string? State { get; set; }

    [MaxLength(25)]
    public string? ContactPhone { get; set; }

    [EmailAddress, MaxLength(150)]
    public string? ContactEmail { get; set; }

    public int? InitialConsultationMinutes { get; set; }
}

public class HospitalStatusDecisionDto
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}
