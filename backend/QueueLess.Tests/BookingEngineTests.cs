using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using QueueLess.Api.DTOs;
using QueueLess.Api.Hubs;
using QueueLess.Api.Models;
using QueueLess.Api.Services;

namespace QueueLess.Tests;

public class BookingEngineTests
{
    private readonly Mock<IHubContext<QueueHub>> _hubMock = new();
    private readonly Mock<IAIPredictionService> _aiMock = new();
    private readonly Mock<INotificationService> _notifMock = new();

    public BookingEngineTests()
    {
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        _hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        _aiMock.Setup(ai => ai.CalculateWaitingTimeAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
            .ReturnsAsync(new WaitingTimePredictionDto { PredictedWaitMinutes = 10, PredictionMethod = PredictionMethod.Baseline });
    }

    [Fact]
    public async Task CreateBooking_IncrementsTokenScopedToSession()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var bookingService = new BookingService(db, _hubMock.Object, _aiMock.Object, _notifMock.Object);

        var hospital = new Hospital { Id = Guid.NewGuid(), Name = "H", RegistrationNumber = "R", Address = "A", City = "C", ContactPhone = "1", ContactEmail = "e@h.com", Status = HospitalStatus.Approved };
        var dept = new Department { Id = Guid.NewGuid(), HospitalId = hospital.Id, Name = "D" };
        var doc = new Doctor { Id = Guid.NewGuid(), HospitalId = hospital.Id, FullName = "Dr", Specialization = "S", LicenseNumber = "L" };
        var session = new QueueSession { Id = Guid.NewGuid(), HospitalId = hospital.Id, DoctorId = doc.Id, DepartmentId = dept.Id, Status = QueueSessionStatus.Open, MaxCapacity = 50, CurrentTokenNumber = 0 };
        var patient1 = new User { Id = Guid.NewGuid(), Email = "p1@p.com", FullName = "P1" };
        var patient2 = new User { Id = Guid.NewGuid(), Email = "p2@p.com", FullName = "P2" };

        db.Hospitals.Add(hospital);
        db.Departments.Add(dept);
        db.Doctors.Add(doc);
        db.QueueSessions.Add(session);
        db.Users.AddRange(patient1, patient2);
        await db.SaveChangesAsync();

        var b1 = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { HospitalId = hospital.Id, DepartmentId = dept.Id, DoctorId = doc.Id, QueueSessionId = session.Id }, patient1.Id);
        var b2 = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { HospitalId = hospital.Id, DepartmentId = dept.Id, DoctorId = doc.Id, QueueSessionId = session.Id }, patient2.Id);

        b1.TokenNumber.Should().Be(1);
        b2.TokenNumber.Should().Be(2);
        session.CurrentTokenNumber.Should().Be(2);
    }

    [Fact]
    public async Task CreateBooking_DuplicateIdempotencyKey_ReturnsSameBooking()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var bookingService = new BookingService(db, _hubMock.Object, _aiMock.Object, _notifMock.Object);

        var hospital = new Hospital { Id = Guid.NewGuid(), Name = "H", RegistrationNumber = "R", Address = "A", City = "C", ContactPhone = "1", ContactEmail = "e@h.com", Status = HospitalStatus.Approved };
        var dept = new Department { Id = Guid.NewGuid(), HospitalId = hospital.Id, Name = "D" };
        var doc = new Doctor { Id = Guid.NewGuid(), HospitalId = hospital.Id, FullName = "Dr", Specialization = "S", LicenseNumber = "L" };
        var session = new QueueSession { Id = Guid.NewGuid(), HospitalId = hospital.Id, DoctorId = doc.Id, DepartmentId = dept.Id, Status = QueueSessionStatus.Open, MaxCapacity = 50 };
        var patient = new User { Id = Guid.NewGuid(), Email = "idem@p.com", FullName = "Idem Patient" };

        db.Hospitals.Add(hospital);
        db.Departments.Add(dept);
        db.Doctors.Add(doc);
        db.QueueSessions.Add(session);
        db.Users.Add(patient);
        await db.SaveChangesAsync();

        var key = "UNIQUE-IDEMPOTENCY-KEY-12345";
        var req = new CreateBookingRequestDto
        {
            HospitalId = hospital.Id,
            DepartmentId = dept.Id,
            DoctorId = doc.Id,
            QueueSessionId = session.Id,
            IdempotencyKey = key
        };

        var firstCall = await bookingService.CreateBookingAsync(req, patient.Id);
        var secondCall = await bookingService.CreateBookingAsync(req, patient.Id);

        firstCall.Id.Should().Be(secondCall.Id);
        firstCall.TokenNumber.Should().Be(secondCall.TokenNumber);
        db.Bookings.Count().Should().Be(1);
    }

    [Fact]
    public async Task CreateBooking_SessionCapacityReached_ThrowsInvalidOperationException()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var bookingService = new BookingService(db, _hubMock.Object, _aiMock.Object, _notifMock.Object);

        var hospital = new Hospital { Id = Guid.NewGuid(), Name = "H", RegistrationNumber = "R", Address = "A", City = "C", ContactPhone = "1", ContactEmail = "e@h.com", Status = HospitalStatus.Approved };
        var dept = new Department { Id = Guid.NewGuid(), HospitalId = hospital.Id, Name = "D" };
        var doc = new Doctor { Id = Guid.NewGuid(), HospitalId = hospital.Id, FullName = "Dr", Specialization = "S", LicenseNumber = "L" };
        var session = new QueueSession { Id = Guid.NewGuid(), HospitalId = hospital.Id, DoctorId = doc.Id, DepartmentId = dept.Id, Status = QueueSessionStatus.Open, MaxCapacity = 1 };
        var patient1 = new User { Id = Guid.NewGuid(), Email = "p1@p.com", FullName = "P1" };
        var patient2 = new User { Id = Guid.NewGuid(), Email = "p2@p.com", FullName = "P2" };

        db.Hospitals.Add(hospital);
        db.Departments.Add(dept);
        db.Doctors.Add(doc);
        db.QueueSessions.Add(session);
        db.Users.AddRange(patient1, patient2);
        await db.SaveChangesAsync();

        // 1st booking fills capacity
        await bookingService.CreateBookingAsync(new CreateBookingRequestDto { HospitalId = hospital.Id, DepartmentId = dept.Id, DoctorId = doc.Id, QueueSessionId = session.Id }, patient1.Id);

        // 2nd booking must be rejected
        var act = async () => await bookingService.CreateBookingAsync(new CreateBookingRequestDto { HospitalId = hospital.Id, DepartmentId = dept.Id, DoctorId = doc.Id, QueueSessionId = session.Id }, patient2.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reached maximum capacity*");
    }

    [Fact]
    public async Task CancelBooking_CompletedBooking_ThrowsInvalidOperationException()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var bookingService = new BookingService(db, _hubMock.Object, _aiMock.Object, _notifMock.Object);

        var hospital = new Hospital { Id = Guid.NewGuid(), Name = "H", RegistrationNumber = "R", Address = "A", City = "C", ContactPhone = "1", ContactEmail = "e@h.com", Status = HospitalStatus.Approved };
        var dept = new Department { Id = Guid.NewGuid(), HospitalId = hospital.Id, Name = "D" };
        var doc = new Doctor { Id = Guid.NewGuid(), HospitalId = hospital.Id, FullName = "Dr", Specialization = "S", LicenseNumber = "L" };
        var session = new QueueSession { Id = Guid.NewGuid(), HospitalId = hospital.Id, DoctorId = doc.Id, DepartmentId = dept.Id, Status = QueueSessionStatus.Open };
        var patient = new User { Id = Guid.NewGuid(), Email = "p@p.com", FullName = "P" };
        var completedBooking = new Booking
        {
            Id = Guid.NewGuid(),
            HospitalId = hospital.Id,
            QueueSessionId = session.Id,
            PatientUserId = patient.Id,
            DoctorId = doc.Id,
            DepartmentId = dept.Id,
            TokenNumber = 1,
            Status = BookingStatus.Completed
        };

        db.Hospitals.Add(hospital);
        db.Departments.Add(dept);
        db.Doctors.Add(doc);
        db.QueueSessions.Add(session);
        db.Users.Add(patient);
        db.Bookings.Add(completedBooking);
        await db.SaveChangesAsync();

        var act = async () => await bookingService.CancelBookingAsync(completedBooking.Id, patient.Id, "Attempt cancel completed");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only waiting bookings can be cancelled*");
    }
}
