using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public static class QueueEventType
{
    public const string Created = "CREATED";
    public const string Called = "CALLED";
    public const string Started = "STARTED";
    public const string Completed = "COMPLETED";
    public const string NoShow = "NO_SHOW";
    public const string Paused = "PAUSED";
    public const string Resumed = "RESUMED";
    public const string Cancelled = "CANCELLED";
}

public class QueueEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HospitalId { get; set; }

    [Required]
    public Guid QueueSessionId { get; set; }

    public Guid? BookingId { get; set; }

    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid? TriggeredByUserId { get; set; }

    public string? MetadataJson { get; set; }

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital Hospital { get; set; } = null!;

    [ForeignKey(nameof(QueueSessionId))]
    public QueueSession QueueSession { get; set; } = null!;

    [ForeignKey(nameof(BookingId))]
    public Booking? Booking { get; set; }

    [ForeignKey(nameof(TriggeredByUserId))]
    public User? TriggeredByUser { get; set; }
}
