using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public static class NotificationType
{
    public const string BookingConfirmed = "BOOKING_CONFIRMED";
    public const string TokenCalled = "TOKEN_CALLED";
    public const string QueueUpdated = "QUEUE_UPDATED";
    public const string ConsultationStarted = "CONSULTATION_STARTED";
    public const string ConsultationCompleted = "CONSULTATION_COMPLETED";
    public const string BookingCancelled = "BOOKING_CANCELLED";
    public const string QueuePaused = "QUEUE_PAUSED";
}

public class Notification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    public Guid? RelatedEntityId { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
