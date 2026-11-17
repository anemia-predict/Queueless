using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueueLess.Api.Models;

public static class MLModelType
{
    public const string Shared = "SHARED";
    public const string HospitalSpecific = "HOSPITAL_SPECIFIC";
}

public class MLModelVersion
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string ModelType { get; set; } = MLModelType.Shared;

    public Guid? HospitalId { get; set; }

    [Required]
    [MaxLength(50)]
    public string VersionTag { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string FilePath { get; set; } = string.Empty;

    public int TrainingRecordCount { get; set; }
    public double MeanAbsoluteError { get; set; }
    public double BaselineMAE { get; set; }

    public bool IsActive { get; set; } = false;

    public DateTime TrainedAt { get; set; } = DateTime.UtcNow;

    public string? MetricsJson { get; set; }

    // Navigation
    [ForeignKey(nameof(HospitalId))]
    public Hospital? Hospital { get; set; }

    public ICollection<MLPrediction> Predictions { get; set; } = new List<MLPrediction>();
}
