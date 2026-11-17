using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public class DoctorDepartment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HospitalId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital Hospital { get; set; } = null!;

    [ForeignKey(nameof(DoctorId))]
    public Doctor Doctor { get; set; } = null!;

    [ForeignKey(nameof(DepartmentId))]
    public Department Department { get; set; } = null!;
}
