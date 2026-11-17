using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public class AuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? HospitalId { get; set; }
    public Guid? UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? EntityId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? DetailsJson { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital? Hospital { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}
