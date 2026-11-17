using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using QueueLess.Api.Models;
using QueueLess.Api.Services;

namespace QueueLess.Tests;

public class AIPredictionTests
{
    private readonly Mock<IWebHostEnvironment> _envMock = new();
    private readonly Mock<ILogger<AIPredictionService>> _loggerMock = new();

    public AIPredictionTests()
    {
        _envMock.Setup(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);
    }

    [Fact]
    public async Task BaselineCalculation_FormulaCorrectlyAccountsForPeopleAheadAndActiveServing()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var aiService = new AIPredictionService(db, _envMock.Object, _loggerMock.Object);

        var hospital = new Hospital { Id = Guid.NewGuid(), Name = "H", RegistrationNumber = "R", Address = "A", City = "C", ContactPhone = "1", ContactEmail = "e@h.com", Status = HospitalStatus.Approved, InitialConsultationMinutes = 10 };
        var dept = new Department { Id = Guid.NewGuid(), HospitalId = hospital.Id, Name = "General", DefaultConsultationMinutes = 15 };
        var doc = new Doctor { Id = Guid.NewGuid(), HospitalId = hospital.Id, FullName = "Dr" };
        var session = new QueueSession { Id = Guid.NewGuid(), HospitalId = hospital.Id, DoctorId = doc.Id, DepartmentId = dept.Id, Status = QueueSessionStatus.Open };
        var patient = new User { Id = Guid.NewGuid(), Email = "p@p.com", FullName = "P" };

        db.Hospitals.Add(hospital);
        db.Departments.Add(dept);
        db.Doctors.Add(doc);
        db.QueueSessions.Add(session);
        db.Users.Add(patient);

        // Add 2 waiting patients ahead (tokens 1 and 2)
        var b1 = new Booking { Id = Guid.NewGuid(), HospitalId = hospital.Id, QueueSessionId = session.Id, PatientUserId = patient.Id, DoctorId = doc.Id, DepartmentId = dept.Id, TokenNumber = 1, Status = BookingStatus.Waiting };
        var b2 = new Booking { Id = Guid.NewGuid(), HospitalId = hospital.Id, QueueSessionId = session.Id, PatientUserId = patient.Id, DoctorId = doc.Id, DepartmentId = dept.Id, TokenNumber = 2, Status = BookingStatus.Waiting };

        // Our target booking is token 3 (2 people ahead)
        var targetBooking = new Booking { Id = Guid.NewGuid(), HospitalId = hospital.Id, QueueSessionId = session.Id, PatientUserId = patient.Id, DoctorId = doc.Id, DepartmentId = dept.Id, TokenNumber = 3, Status = BookingStatus.Waiting };

        db.Bookings.AddRange(b1, b2, targetBooking);
        await db.SaveChangesAsync();

        var prediction = await aiService.CalculateWaitingTimeAsync(targetBooking.Id, savePrediction: true);

        // Expected: 2 people ahead * 15 min duration = 30 min
        prediction.Should().NotBeNull();
        prediction.PredictionMethod.Should().Be(PredictionMethod.Baseline);
        prediction.PredictedWaitMinutes.Should().Be(30.0);
        prediction.BaselineWaitMinutes.Should().Be(30.0);
        prediction.MethodExplanation.Should().Contain("Standard estimate based on queue position");
    }

    [Fact]
    public async Task Prediction_FallsBackToBaseline_WhenNoTrainedModelExists()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var aiService = new AIPredictionService(db, _envMock.Object, _loggerMock.Object);

        var hospital = new Hospital { Id = Guid.NewGuid(), Name = "H", RegistrationNumber = "R", Address = "A", City = "C", ContactPhone = "1", ContactEmail = "e@h.com", Status = HospitalStatus.Approved, InitialConsultationMinutes = 10 };
        var dept = new Department { Id = Guid.NewGuid(), HospitalId = hospital.Id, Name = "General", DefaultConsultationMinutes = 10 };
        var doc = new Doctor { Id = Guid.NewGuid(), HospitalId = hospital.Id, FullName = "Dr" };
        var session = new QueueSession { Id = Guid.NewGuid(), HospitalId = hospital.Id, DoctorId = doc.Id, DepartmentId = dept.Id, Status = QueueSessionStatus.Open };
        var patient = new User { Id = Guid.NewGuid(), Email = "p@p.com", FullName = "P" };
        var target = new Booking { Id = Guid.NewGuid(), HospitalId = hospital.Id, QueueSessionId = session.Id, PatientUserId = patient.Id, DoctorId = doc.Id, DepartmentId = dept.Id, TokenNumber = 1, Status = BookingStatus.Waiting };

        db.Hospitals.Add(hospital);
        db.Departments.Add(dept);
        db.Doctors.Add(doc);
        db.QueueSessions.Add(session);
        db.Users.Add(patient);
        db.Bookings.Add(target);
        await db.SaveChangesAsync();

        // No model trained -> Should seamlessly fallback to Baseline without throwing exception
        var pred = await aiService.CalculateWaitingTimeAsync(target.Id, savePrediction: false);
        pred.PredictionMethod.Should().Be(PredictionMethod.Baseline);
        pred.PredictedWaitMinutes.Should().Be(0.0); // 0 people ahead
    }

    [Fact]
    public async Task SeedSyntheticData_CreatesClearlyMarkedRecords()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var aiService = new AIPredictionService(db, _envMock.Object, _loggerMock.Object);

        var hospital = new Hospital { Id = Guid.NewGuid(), Name = "H", RegistrationNumber = "R", Address = "A", City = "C", ContactPhone = "1", ContactEmail = "e@h.com", Status = HospitalStatus.Approved };
        var dept = new Department { Id = Guid.NewGuid(), HospitalId = hospital.Id, Name = "General", DefaultConsultationMinutes = 12 };
        var doc = new Doctor { Id = Guid.NewGuid(), HospitalId = hospital.Id, FullName = "Dr Demo" };

        db.Hospitals.Add(hospital);
        db.Departments.Add(dept);
        db.Doctors.Add(doc);
        await db.SaveChangesAsync();

        var seededCount = await aiService.SeedSyntheticConsultationDataAsync(hospital.Id, recordCount: 30);

        seededCount.Should().Be(30);
        var records = db.ConsultationRecords.Where(c => c.HospitalId == hospital.Id).ToList();
        records.Count.Should().Be(30);
        records.All(r => r.IsSyntheticDemo).Should().BeTrue();
    }
}
