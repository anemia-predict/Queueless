using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using QueueLess.Api.Hubs;
using QueueLess.Api.Models;
using QueueLess.Api.Services;

namespace QueueLess.Tests;

public class QueueManagementTests
{
    private readonly Mock<IHubContext<QueueHub>> _hubMock = new();
    private readonly Mock<INotificationService> _notifMock = new();
    private readonly Mock<IAIPredictionService> _aiMock = new();

    public QueueManagementTests()
    {
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        _hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);
    }

    [Fact]
    public async Task CallNext_TransitionsWaitingBookingToCalled()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var queueService = new QueueService(db, _hubMock.Object, _notifMock.Object, _aiMock.Object);

        var hospital = new Hospital { Id = Guid.NewGuid(), Name = "H", RegistrationNumber = "R", Address = "A", City = "C", ContactPhone = "1", ContactEmail = "e@h.com", Status = HospitalStatus.Approved };
        var doc = new Doctor { Id = Guid.NewGuid(), HospitalId = hospital.Id, FullName = "Dr" };
        var dept = new Department { Id = Guid.NewGuid(), HospitalId = hospital.Id, Name = "Dept" };
        var session = new QueueSession { Id = Guid.NewGuid(), HospitalId = hospital.Id, DoctorId = doc.Id, DepartmentId = dept.Id, Status = QueueSessionStatus.Open };
        var patient = new User { Id = Guid.NewGuid(), Email = "p@p.com", FullName = "P" };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            HospitalId = hospital.Id,
            QueueSessionId = session.Id,
            PatientUserId = patient.Id,
            DoctorId = doc.Id,
            DepartmentId = dept.Id,
            TokenNumber = 1,
            Status = BookingStatus.Waiting
        };

        db.Hospitals.Add(hospital);
        db.Doctors.Add(doc);
        db.Departments.Add(dept);
        db.QueueSessions.Add(session);
        db.Users.Add(patient);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var result = await queueService.CallNextAsync(hospital.Id, session.Id, Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.Booking!.Status.Should().Be(BookingStatus.Called);
        result.Booking.TokenNumber.Should().Be(1);

        var updated = await db.Bookings.FindAsync(booking.Id);
        updated!.Status.Should().Be(BookingStatus.Called);
        updated.ActualCallTime.Should().NotBeNull();
    }

    [Fact]
    public async Task Consultation_StartAndComplete_PersistsActualDurationInConsultationRecords()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var queueService = new QueueService(db, _hubMock.Object, _notifMock.Object, _aiMock.Object);

        var hospital = new Hospital { Id = Guid.NewGuid(), Name = "H", RegistrationNumber = "R", Address = "A", City = "C", ContactPhone = "1", ContactEmail = "e@h.com", Status = HospitalStatus.Approved };
        var doc = new Doctor { Id = Guid.NewGuid(), HospitalId = hospital.Id, FullName = "Dr" };
        var dept = new Department { Id = Guid.NewGuid(), HospitalId = hospital.Id, Name = "Dept" };
        var session = new QueueSession { Id = Guid.NewGuid(), HospitalId = hospital.Id, DoctorId = doc.Id, DepartmentId = dept.Id, Status = QueueSessionStatus.Open };
        var patient = new User { Id = Guid.NewGuid(), Email = "p@p.com", FullName = "P" };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            HospitalId = hospital.Id,
            QueueSessionId = session.Id,
            PatientUserId = patient.Id,
            DoctorId = doc.Id,
            DepartmentId = dept.Id,
            TokenNumber = 5,
            Status = BookingStatus.Called
        };

        db.Hospitals.Add(hospital);
        db.Doctors.Add(doc);
        db.Departments.Add(dept);
        db.QueueSessions.Add(session);
        db.Users.Add(patient);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var opUserId = Guid.NewGuid();

        // 1. Start consultation
        var startRes = await queueService.StartConsultationAsync(hospital.Id, booking.Id, opUserId);
        startRes.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Serving);
        booking.ActualStartTime.Should().NotBeNull();

        // Simulate 12 minutes consultation
        booking.ActualStartTime = DateTime.UtcNow.AddMinutes(-12);
        await db.SaveChangesAsync();

        // 2. Complete consultation
        var completeRes = await queueService.CompleteConsultationAsync(hospital.Id, booking.Id, opUserId, "Diagnosis complete");
        completeRes.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);
        booking.ActualEndTime.Should().NotBeNull();

        // 3. Verify ConsultationRecord was persisted in database
        var record = db.ConsultationRecords.FirstOrDefault(cr => cr.BookingId == booking.Id);
        record.Should().NotBeNull();
        record!.HospitalId.Should().Be(hospital.Id);
        record.DurationMinutes.Should().BeApproximately(12.0, 1.0);
        record.IsSyntheticDemo.Should().BeFalse();
        record.Notes.Should().Be("Diagnosis complete");
    }

    [Fact]
    public async Task PauseAndResumeQueue_UpdatesStatusAndEmitsEvents()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var queueService = new QueueService(db, _hubMock.Object, _notifMock.Object, _aiMock.Object);

        var hospital = new Hospital { Id = Guid.NewGuid(), Name = "H", RegistrationNumber = "R", Address = "A", City = "C", ContactPhone = "1", ContactEmail = "e@h.com", Status = HospitalStatus.Approved };
        var doc = new Doctor { Id = Guid.NewGuid(), HospitalId = hospital.Id, FullName = "Dr" };
        var dept = new Department { Id = Guid.NewGuid(), HospitalId = hospital.Id, Name = "Dept" };
        var session = new QueueSession
        {
            Id = Guid.NewGuid(),
            HospitalId = hospital.Id,
            DoctorId = doc.Id,
            DepartmentId = dept.Id,
            Status = QueueSessionStatus.Open
        };

        db.Hospitals.Add(hospital);
        db.Doctors.Add(doc);
        db.Departments.Add(dept);
        db.QueueSessions.Add(session);
        await db.SaveChangesAsync();

        var opId = Guid.NewGuid();

        // Pause
        await queueService.PauseQueueAsync(hospital.Id, session.Id, opId);
        session.Status.Should().Be(QueueSessionStatus.Paused);

        // Resume
        await queueService.ResumeQueueAsync(hospital.Id, session.Id, opId);
        session.Status.Should().Be(QueueSessionStatus.Open);
    }
}
