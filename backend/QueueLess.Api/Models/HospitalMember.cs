using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public static class HospitalMemberRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Doctor = "Doctor";
}

public class HospitalMember
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HospitalId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string RoleInHospital { get; set; } = HospitalMemberRoles.Admin;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital Hospital { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
