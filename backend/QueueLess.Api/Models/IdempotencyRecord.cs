using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public class IdempotencyRecord
{
    [Key]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string RequestHash { get; set; } = string.Empty;

    public int ResponseStatusCode { get; set; }

    [Required]
    public string ResponseBody { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    // Navigation
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
