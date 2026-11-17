using Microsoft.ML.Data;

namespace QueueLess.Api.AI;

public class QueueWaitTimeTrainingData
{
    [LoadColumn(0)]
    public float PeopleAhead { get; set; }

    [LoadColumn(1)]
    public float DayOfWeek { get; set; }

    [LoadColumn(2)]
    public float HourOfDay { get; set; }

    [LoadColumn(3)]
    public float ConfiguredDurationMinutes { get; set; }

    [LoadColumn(4)]
    public float ActiveServingRemainingMinutes { get; set; }

    [LoadColumn(5)]
    public float TotalQueueWorkload { get; set; }

    [LoadColumn(6)]
    public string DepartmentCategory { get; set; } = string.Empty;

    [LoadColumn(7)]
    public float ActualWaitMinutes { get; set; } // Label
}

public class QueueWaitTimePrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
}
