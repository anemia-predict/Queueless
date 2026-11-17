using System.ComponentModel.DataAnnotations;

namespace QueueLess.Api.DTOs;

public class CreateDepartmentDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    [Range(1, 180)]
    public int DefaultConsultationMinutes { get; set; } = 10;
}

public class UpdateDepartmentDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    [Range(1, 180)]
    public int? DefaultConsultationMinutes { get; set; }

    public bool? IsActive { get; set; }
}

public class DepartmentDto
{
    public Guid Id { get; set; }
    public Guid HospitalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DefaultConsultationMinutes { get; set; }
    public bool IsActive { get; set; }
    public int DoctorCount { get; set; }
}
