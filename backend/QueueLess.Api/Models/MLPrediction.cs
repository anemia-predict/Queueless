using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public static class PredictionMethod
{
    public const string Baseline = "BASELINE";
    public const string SharedModel = "SHARED_MODEL";
    public const string HospitalModel = "HOSPITAL_MODEL";
}

public class MLPrediction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid BookingId { get; set; }

    [Required]
    public Guid HospitalId { get; set; }

    [Required]
    [MaxLength(50)]
    public string PredictionMethod { get; set; } = Models.PredictionMethod.Baseline;

    public Guid? ModelVersionId { get; set; }

    public double PredictedWaitMinutes { get; set; }
    public double BaselineWaitMinutes { get; set; }

    [Required]
    public string FeaturesJson { get; set; } = "{}";

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(BookingId))]
    public Booking Booking { get; set; } = null!;

    [ForeignKey(nameof(HospitalId))]
    public Hospital Hospital { get; set; } = null!;

    [ForeignKey(nameof(ModelVersionId))]
    public MLModelVersion? ModelVersion { get; set; }
}
