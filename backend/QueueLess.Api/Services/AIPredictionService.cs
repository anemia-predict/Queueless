using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using QueueLess.Api.AI;
using QueueLess.Api.Data;
using QueueLess.Api.DTOs;
using QueueLess.Api.Models;

namespace QueueLess.Api.Services;

public class AIPredictionService : IAIPredictionService
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AIPredictionService> _logger;
    private static readonly MLContext _mlContext = new(seed: 42);

    private const int MinimumRecordsForHospitalModel = 20;
    private const int MinimumRecordsForSharedModel = 40;

    public AIPredictionService(ApplicationDbContext db, IWebHostEnvironment env, ILogger<AIPredictionService> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    public async Task<WaitingTimePredictionDto> CalculateWaitingTimeAsync(Guid bookingId, bool savePrediction = true)
    {
        var booking = await _db.Bookings
            .Include(b => b.Hospital)
            .Include(b => b.Department)
            .Include(b => b.Doctor)
            .Include(b => b.QueueSession)
                .ThenInclude(qs => qs.Bookings)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            throw new KeyNotFoundException("Booking not found.");
        }

        var session = booking.QueueSession;
        var hospital = booking.Hospital;
        var dept = booking.Department;

        // Count eligible bookings waiting ahead of this token
        var bookingsAhead = session.Bookings
            .Where(b => b.TokenNumber < booking.TokenNumber &&
                        (b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Called))
            .ToList();

        var peopleAhead = bookingsAhead.Count;

        // Check if someone is currently serving
        var activeServingBooking = session.Bookings
            .FirstOrDefault(b => b.Status == BookingStatus.Serving);

        var configuredDuration = dept.DefaultConsultationMinutes > 0
            ? dept.DefaultConsultationMinutes
            : (hospital.InitialConsultationMinutes > 0 ? hospital.InitialConsultationMinutes : 10);

        double activeRemainingMinutes = 0;
        if (activeServingBooking != null && activeServingBooking.ActualStartTime.HasValue)
        {
            var elapsedMinutes = (DateTime.UtcNow - activeServingBooking.ActualStartTime.Value).TotalMinutes;
            activeRemainingMinutes = Math.Max(1.0, configuredDuration - elapsedMinutes);
        }

        // Mode 1: Baseline Calculation
        // Baseline = Remaining serving time + (people ahead * configured consultation duration)
        var baselineWaitMinutes = Math.Round(activeRemainingMinutes + (peopleAhead * configuredDuration), 1);
        if (booking.Status != BookingStatus.Waiting && booking.Status != BookingStatus.Called)
        {
            baselineWaitMinutes = 0;
        }

        double predictedWaitMinutes = baselineWaitMinutes;
        string method = PredictionMethod.Baseline;
        string explanation = "Standard estimate based on queue position and configured consultation duration.";
        string? modelVersionTag = null;
        Guid? modelVersionId = null;

        var featureSnapshot = new Dictionary<string, object>
        {
            ["peopleAhead"] = peopleAhead,
            ["configuredDuration"] = configuredDuration,
            ["activeRemainingMinutes"] = activeRemainingMinutes,
            ["dayOfWeek"] = (int)DateTime.UtcNow.DayOfWeek,
            ["hourOfDay"] = DateTime.UtcNow.Hour,
            ["department"] = dept.Name,
            ["hospitalId"] = hospital.Id.ToString()
        };

        // If booking is waiting or called, attempt ML prediction with graceful fallback
        if (booking.Status == BookingStatus.Waiting || booking.Status == BookingStatus.Called)
        {
            try
            {
                // First check for active hospital-specific model (Mode 3)
                var hospitalModel = await _db.MLModelVersions
                    .Where(m => m.HospitalId == hospital.Id && m.ModelType == MLModelType.HospitalSpecific && m.IsActive)
                    .OrderByDescending(m => m.TrainedAt)
                    .FirstOrDefaultAsync();

                // Then check for active shared model (Mode 2)
                var sharedModel = hospitalModel == null
                    ? await _db.MLModelVersions
                        .Where(m => m.ModelType == MLModelType.Shared && m.IsActive)
                        .OrderByDescending(m => m.TrainedAt)
                        .FirstOrDefaultAsync()
                    : null;

                var chosenModel = hospitalModel ?? sharedModel;

                if (chosenModel != null && File.Exists(chosenModel.FilePath))
                {
                    ITransformer trainedModel;
                    using (var stream = File.OpenRead(chosenModel.FilePath))
                    {
                        trainedModel = _mlContext.Model.Load(stream, out var _);
                    }

                    var predEngine = _mlContext.Model.CreatePredictionEngine<QueueWaitTimeTrainingData, QueueWaitTimePrediction>(trainedModel);
                    var inputData = new QueueWaitTimeTrainingData
                    {
                        PeopleAhead = peopleAhead,
                        DayOfWeek = (float)DateTime.UtcNow.DayOfWeek,
                        HourOfDay = DateTime.UtcNow.Hour,
                        ConfiguredDurationMinutes = configuredDuration,
                        ActiveServingRemainingMinutes = (float)activeRemainingMinutes,
                        TotalQueueWorkload = session.Bookings.Count(b => b.Status == BookingStatus.Waiting || b.Status == BookingStatus.Serving),
                        DepartmentCategory = dept.Name
                    };

                    var prediction = predEngine.Predict(inputData);
                    var score = Math.Round((double)prediction.Score, 1);

                    // Validate output: must be non-negative and plausible
                    if (score >= 0 && score <= 600)
                    {
                        predictedWaitMinutes = score;
                        method = chosenModel.ModelType == MLModelType.HospitalSpecific
                            ? PredictionMethod.HospitalModel
                            : PredictionMethod.SharedModel;

                        modelVersionTag = chosenModel.VersionTag;
                        modelVersionId = chosenModel.Id;
                        explanation = method == PredictionMethod.HospitalModel
                            ? "ML estimate trained on this hospital's validated historical consultation records."
                            : "ML estimate based on shared multi-hospital consultation patterns.";
                    }
                    else
                    {
                        _logger.LogWarning("ML prediction produced invalid score: {Score}. Falling back to baseline.", score);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing ML model prediction. Falling back to baseline.");
                // Fallback remains Baseline
                predictedWaitMinutes = baselineWaitMinutes;
                method = PredictionMethod.Baseline;
                explanation = "Standard estimate (AI model unavailable or evaluating; falling back to baseline).";
            }
        }

        var result = new WaitingTimePredictionDto
        {
            BookingId = booking.Id,
            HospitalId = hospital.Id,
            PredictedWaitMinutes = predictedWaitMinutes,
            BaselineWaitMinutes = baselineWaitMinutes,
            PredictionMethod = method,
            MethodExplanation = explanation,
            ModelVersion = modelVersionTag,
            Timestamp = DateTime.UtcNow,
            FeatureSnapshot = featureSnapshot
        };

        if (savePrediction)
        {
            var mlPred = new MLPrediction
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                HospitalId = hospital.Id,
                PredictionMethod = method,
                ModelVersionId = modelVersionId,
                PredictedWaitMinutes = predictedWaitMinutes,
                BaselineWaitMinutes = baselineWaitMinutes,
                FeaturesJson = JsonSerializer.Serialize(featureSnapshot),
                GeneratedAt = DateTime.UtcNow
            };

            _db.MLPredictions.Add(mlPred);
            await _db.SaveChangesAsync();
        }

        return result;
    }

    public async Task<WaitingTimePredictionDto> EstimateQueueWaitAsync(Guid hospitalId, Guid departmentId, int peopleAhead, bool hasActiveServing, double remainingServingMinutes)
    {
        var hospital = await _db.Hospitals.FindAsync(hospitalId);
        var dept = await _db.Departments.FindAsync(departmentId);

        var configuredDuration = dept?.DefaultConsultationMinutes ?? (hospital?.InitialConsultationMinutes ?? 10);
        var activeRem = hasActiveServing ? Math.Max(1.0, remainingServingMinutes) : 0;
        var baseline = Math.Round(activeRem + (peopleAhead * configuredDuration), 1);

        return new WaitingTimePredictionDto
        {
            HospitalId = hospitalId,
            PredictedWaitMinutes = baseline,
            BaselineWaitMinutes = baseline,
            PredictionMethod = PredictionMethod.Baseline,
            MethodExplanation = "Standard estimate based on queue position and configured consultation duration.",
            Timestamp = DateTime.UtcNow,
            FeatureSnapshot = new Dictionary<string, object>
            {
                ["peopleAhead"] = peopleAhead,
                ["configuredDuration"] = configuredDuration,
                ["hasActiveServing"] = hasActiveServing
            }
        };
    }

    public async Task<AIModelMetricsDto> TrainModelAsync(Guid? hospitalId, bool includeSynthetic = true)
    {
        var modelType = hospitalId.HasValue ? MLModelType.HospitalSpecific : MLModelType.Shared;
        var minRecords = hospitalId.HasValue ? MinimumRecordsForHospitalModel : MinimumRecordsForSharedModel;

        var query = _db.ConsultationRecords
            .Include(c => c.Department)
            .Include(c => c.Booking)
            .Where(c => c.DurationMinutes > 0 && c.DurationMinutes <= 180)
            .AsQueryable();

        if (hospitalId.HasValue)
        {
            query = query.Where(c => c.HospitalId == hospitalId.Value);
        }

        if (!includeSynthetic)
        {
            query = query.Where(c => !c.IsSyntheticDemo);
        }

        // Chronological order for robust split
        var records = await query.OrderBy(c => c.StartTime).ToListAsync();

        if (records.Count < minRecords)
        {
            throw new InvalidOperationException(
                $"Insufficient completed consultation data to train {modelType} model. " +
                $"Found {records.Count} records, but at least {minRecords} are required.");
        }

        // Prepare training dataset
        var dataList = new List<QueueWaitTimeTrainingData>();
        foreach (var r in records)
        {
            var deptDuration = r.Department?.DefaultConsultationMinutes ?? 10;
            // Features represent information known at prediction time
            var simulatedPeopleAhead = Math.Max(0, (r.Booking?.TokenNumber ?? 1) - 1);
            var item = new QueueWaitTimeTrainingData
            {
                PeopleAhead = simulatedPeopleAhead,
                DayOfWeek = (float)r.StartTime.DayOfWeek,
                HourOfDay = r.StartTime.Hour,
                ConfiguredDurationMinutes = deptDuration,
                ActiveServingRemainingMinutes = deptDuration / 2f,
                TotalQueueWorkload = simulatedPeopleAhead + 1,
                DepartmentCategory = r.Department?.Name ?? "General",
                ActualWaitMinutes = (float)(simulatedPeopleAhead * r.DurationMinutes)
            };
            dataList.Add(item);
        }

        var trainCount = (int)(dataList.Count * 0.8);
        var trainDataList = dataList.Take(trainCount).ToList();
        var testDataList = dataList.Skip(trainCount).ToList();

        var trainDataView = _mlContext.Data.LoadFromEnumerable(trainDataList);
        var testDataView = _mlContext.Data.LoadFromEnumerable(testDataList);

        // Build ML.NET pipeline
        var pipeline = _mlContext.Transforms.Categorical.OneHotEncoding("DepartmentCategoryEncoded", nameof(QueueWaitTimeTrainingData.DepartmentCategory))
            .Append(_mlContext.Transforms.Concatenate("Features",
                "DepartmentCategoryEncoded",
                nameof(QueueWaitTimeTrainingData.PeopleAhead),
                nameof(QueueWaitTimeTrainingData.DayOfWeek),
                nameof(QueueWaitTimeTrainingData.HourOfDay),
                nameof(QueueWaitTimeTrainingData.ConfiguredDurationMinutes),
                nameof(QueueWaitTimeTrainingData.ActiveServingRemainingMinutes),
                nameof(QueueWaitTimeTrainingData.TotalQueueWorkload)))
            .Append(_mlContext.Regression.Trainers.FastTree(
                labelColumnName: nameof(QueueWaitTimeTrainingData.ActualWaitMinutes),
                featureColumnName: "Features",
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 2));

        var model = pipeline.Fit(trainDataView);
        var predictions = model.Transform(testDataView);
        var metrics = _mlContext.Regression.Evaluate(predictions, labelColumnName: nameof(QueueWaitTimeTrainingData.ActualWaitMinutes));

        var modelMAE = Math.Round(metrics.MeanAbsoluteError, 2);

        // Calculate Baseline MAE on test dataset
        var baselineErrors = testDataList.Select(d =>
            Math.Abs(d.ActualWaitMinutes - (d.ActiveServingRemainingMinutes + (d.PeopleAhead * d.ConfiguredDurationMinutes)))
        ).ToList();
        var baselineMAE = Math.Round(baselineErrors.Count > 0 ? baselineErrors.Average() : 10.0, 2);

        // Save model file
        var modelsDir = Path.Combine(_env.ContentRootPath, "MLModels", "generated");
        Directory.CreateDirectory(modelsDir);

        var versionTag = $"v{DateTime.UtcNow:yyyyMMddHHmmss}_{modelType.ToLower()}";
        var fileName = $"{versionTag}.zip";
        var filePath = Path.Combine(modelsDir, fileName);

        using (var fs = File.Create(filePath))
        {
            _mlContext.Model.Save(model, trainDataView.Schema, fs);
        }

        // Acceptance criteria: Model is activated if MAE is competitive with or better than baseline
        var isActive = modelMAE <= (baselineMAE * 1.15) && modelMAE < 60;

        // Deactivate previous active models for this scope if new model is active
        if (isActive)
        {
            var oldModels = await _db.MLModelVersions
                .Where(m => m.ModelType == modelType && m.HospitalId == hospitalId && m.IsActive)
                .ToListAsync();

            foreach (var om in oldModels) om.IsActive = false;
        }

        var modelVersion = new MLModelVersion
        {
            Id = Guid.NewGuid(),
            ModelType = modelType,
            HospitalId = hospitalId,
            VersionTag = versionTag,
            FilePath = filePath,
            TrainingRecordCount = records.Count,
            MeanAbsoluteError = modelMAE,
            BaselineMAE = baselineMAE,
            IsActive = isActive,
            TrainedAt = DateTime.UtcNow,
            MetricsJson = JsonSerializer.Serialize(new
            {
                R2 = metrics.RSquared,
                RMSE = metrics.RootMeanSquaredError,
                Loss = metrics.LossFunction,
                BaselineMAE = baselineMAE,
                TrainedRecords = trainCount,
                TestRecords = testDataList.Count
            })
        };

        _db.MLModelVersions.Add(modelVersion);
        await _db.SaveChangesAsync();

        return new AIModelMetricsDto
        {
            Id = modelVersion.Id,
            ModelType = modelVersion.ModelType,
            HospitalId = modelVersion.HospitalId,
            VersionTag = modelVersion.VersionTag,
            TrainingRecordCount = modelVersion.TrainingRecordCount,
            MeanAbsoluteError = modelVersion.MeanAbsoluteError,
            BaselineMAE = modelVersion.BaselineMAE,
            IsActive = modelVersion.IsActive,
            TrainedAt = modelVersion.TrainedAt,
            Status = isActive ? "ACTIVATED" : "TRAINED_INACTIVE_EXCEEDS_BASELINE"
        };
    }

    public async Task<int> SeedSyntheticConsultationDataAsync(Guid hospitalId, int recordCount = 60)
    {
        var hospital = await _db.Hospitals
            .Include(h => h.Departments)
            .Include(h => h.Doctors)
            .FirstOrDefaultAsync(h => h.Id == hospitalId);

        if (hospital == null) throw new KeyNotFoundException("Hospital not found.");

        if (!hospital.Departments.Any() || !hospital.Doctors.Any())
        {
            throw new InvalidOperationException("Hospital must have at least one department and doctor to seed consultation records.");
        }

        var depts = hospital.Departments.ToList();
        var doctors = hospital.Doctors.ToList();

        // Get or create dummy patient user
        var demoPatient = await _db.Users.FirstOrDefaultAsync(u => u.Email == "demopatient@queueless.local");
        if (demoPatient == null)
        {
            demoPatient = new User
            {
                Id = Guid.NewGuid(),
                Email = "demopatient@queueless.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo@123"),
                FullName = "Demo Patient",
                Role = UserRoles.Patient,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(demoPatient);
            await _db.SaveChangesAsync();
        }

        // Create or get queue session
        var session = await _db.QueueSessions
            .FirstOrDefaultAsync(qs => qs.HospitalId == hospitalId && qs.DoctorId == doctors[0].Id);

        if (session == null)
        {
            session = new QueueSession
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = doctors[0].Id,
                DepartmentId = depts[0].Id,
                SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                Status = QueueSessionStatus.Open,
                MaxCapacity = 50,
                CreatedAt = DateTime.UtcNow
            };
            _db.QueueSessions.Add(session);
            await _db.SaveChangesAsync();
        }

        var random = new Random(42); // deterministic seed for reproducibility
        var addedRecords = 0;

        for (int i = 0; i < recordCount; i++)
        {
            var dept = depts[i % depts.Count];
            var doc = doctors[i % doctors.Count];
            var daysAgo = (recordCount - i) / 5;
            var startHour = 9 + (i % 7);
            var startTime = DateTime.UtcNow.AddDays(-daysAgo).Date.AddHours(startHour).AddMinutes(random.Next(0, 45));

            // Realistic duration with small natural variance around department default
            var baseDuration = dept.DefaultConsultationMinutes > 0 ? dept.DefaultConsultationMinutes : 12;
            var variance = random.Next(-3, 6);
            var durationMinutes = Math.Max(5, baseDuration + variance);
            var endTime = startTime.AddMinutes(durationMinutes);

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalId,
                QueueSessionId = session.Id,
                PatientUserId = demoPatient.Id,
                DoctorId = doc.Id,
                DepartmentId = dept.Id,
                TokenNumber = 1000 + i,
                Status = BookingStatus.Completed,
                BookingTime = startTime.AddMinutes(-30),
                ActualStartTime = startTime,
                ActualEndTime = endTime,
                CreatedAt = startTime.AddMinutes(-30),
                UpdatedAt = endTime
            };
            _db.Bookings.Add(booking);

            var cr = new ConsultationRecord
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalId,
                BookingId = booking.Id,
                DoctorId = doc.Id,
                DepartmentId = dept.Id,
                QueueSessionId = session.Id,
                StartTime = startTime,
                EndTime = endTime,
                DurationSeconds = durationMinutes * 60,
                DurationMinutes = durationMinutes,
                Notes = "Synthetic consultation record for local ML model training and evaluation.",
                IsSyntheticDemo = true,
                CreatedAt = endTime
            };
            _db.ConsultationRecords.Add(cr);
            addedRecords++;
        }

        await _db.SaveChangesAsync();
        return addedRecords;
    }

    public async Task<AIModelMetricsDto?> GetActiveModelMetricsAsync(Guid? hospitalId)
    {
        var model = await _db.MLModelVersions
            .Where(m => (hospitalId.HasValue ? m.HospitalId == hospitalId.Value : m.ModelType == MLModelType.Shared) && m.IsActive)
            .OrderByDescending(m => m.TrainedAt)
            .FirstOrDefaultAsync();

        if (model == null) return null;

        return new AIModelMetricsDto
        {
            Id = model.Id,
            ModelType = model.ModelType,
            HospitalId = model.HospitalId,
            VersionTag = model.VersionTag,
            TrainingRecordCount = model.TrainingRecordCount,
            MeanAbsoluteError = model.MeanAbsoluteError,
            BaselineMAE = model.BaselineMAE,
            IsActive = model.IsActive,
            TrainedAt = model.TrainedAt,
            Status = "ACTIVE"
        };
    }

    public async Task<List<AIModelMetricsDto>> GetAllModelsAsync()
    {
        var models = await _db.MLModelVersions
            .OrderByDescending(m => m.TrainedAt)
            .ToListAsync();

        return models.Select(m => new AIModelMetricsDto
        {
            Id = m.Id,
            ModelType = m.ModelType,
            HospitalId = m.HospitalId,
            VersionTag = m.VersionTag,
            TrainingRecordCount = m.TrainingRecordCount,
            MeanAbsoluteError = m.MeanAbsoluteError,
            BaselineMAE = m.BaselineMAE,
            IsActive = m.IsActive,
            TrainedAt = m.TrainedAt,
            Status = m.IsActive ? "ACTIVE" : "INACTIVE"
        }).ToList();
    }
}
