using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using QueueLess.Api.DTOs;
using QueueLess.Api.Hubs;
using QueueLess.Api.Models;
using QueueLess.Api.Services;

namespace QueueLess.Tests;

public class MultiTenancyTests
{
    private readonly Mock<IHubContext<QueueHub>> _hubMock = new();
    private readonly Mock<IAIPredictionService> _aiMock = new();
    private readonly Mock<INotificationService> _notifMock = new();

    public MultiTenancyTests()
    {
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        _hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);
    }

    [Fact]
    public async Task HospitalAdmin_CannotAccessOrModify_AnotherHospitalDoctor()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var hospitalService = new HospitalService(db);

        var hospitalA = new Hospital { Id = Guid.NewGuid(), Name = "Hospital A", RegistrationNumber = "HA-001", Address = "A St", City = "City A", ContactPhone = "123", ContactEmail = "a@a.com", Status = HospitalStatus.Approved };
        var hospitalB = new Hospital { Id = Guid.NewGuid(), Name = "Hospital B", RegistrationNumber = "HB-002", Address = "B St", City = "City B", ContactPhone = "456", ContactEmail = "b@b.com", Status = HospitalStatus.Approved };
        db.Hospitals.AddRange(hospitalA, hospitalB);

        var docB = new Doctor { Id = Guid.NewGuid(), HospitalId = hospitalB.Id, FullName = "Dr. In Hospital B", Specialization = "Neurology", LicenseNumber = "DOC-B" };
        db.Doctors.Add(docB);
        await db.SaveChangesAsync();

        // Hospital A attempts to update Doctor belonging to Hospital B
        var updateDto = new UpdateDoctorDto { FullName = "Malicious Hijack" };
        var act = async () => await hospitalService.UpdateDoctorAsync(hospitalA.Id, docB.Id, updateDto);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found in this hospital*");
    }

    [Fact]
    public async Task PendingHospital_CannotAcceptBookings()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var bookingService = new BookingService(db, _hubMock.Object, _aiMock.Object, _notifMock.Object);

        var pendingHospital = new Hospital { Id = Guid.NewGuid(), Name = "Unapproved Clinic", RegistrationNumber = "UC-003", Address = "C St", City = "City C", ContactPhone = "789", ContactEmail = "c@c.com", Status = HospitalStatus.Pending };
        db.Hospitals.Add(pendingHospital);

        var patient = new User { Id = Guid.NewGuid(), Email = "p@p.com", FullName = "P User", Role = UserRoles.Patient };
        db.Users.Add(patient);
        await db.SaveChangesAsync();

        var bookingRequest = new CreateBookingRequestDto
        {
            HospitalId = pendingHospital.Id,
            DepartmentId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            QueueSessionId = Guid.NewGuid()
        };

        var act = async () => await bookingService.CreateBookingAsync(bookingRequest, patient.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not approved*");
    }

    [Fact]
    public async Task Patient_CanBookAtMultipleHospitals_WithoutBecomingMember()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var bookingService = new BookingService(db, _hubMock.Object, _aiMock.Object, _notifMock.Object);

        _aiMock.Setup(ai => ai.CalculateWaitingTimeAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
            .ReturnsAsync(new WaitingTimePredictionDto { PredictedWaitMinutes = 15, PredictionMethod = PredictionMethod.Baseline });

        var hospitalA = new Hospital { Id = Guid.NewGuid(), Name = "Hospital 1", RegistrationNumber = "H1", Address = "A", City = "City", ContactPhone = "1", ContactEmail = "h1@h.com", Status = HospitalStatus.Approved };
        var hospitalB = new Hospital { Id = Guid.NewGuid(), Name = "Hospital 2", RegistrationNumber = "H2", Address = "B", City = "City", ContactPhone = "2", ContactEmail = "h2@h.com", Status = HospitalStatus.Approved };
        db.Hospitals.AddRange(hospitalA, hospitalB);

        var patient = new User { Id = Guid.NewGuid(), Email = "versatile.patient@example.com", FullName = "Multi Hospital Patient", Role = UserRoles.Patient };
        db.Users.Add(patient);

        var deptA = new Department { Id = Guid.NewGuid(), HospitalId = hospitalA.Id, Name = "Dept A" };
        var deptB = new Department { Id = Guid.NewGuid(), HospitalId = hospitalB.Id, Name = "Dept B" };
        db.Departments.AddRange(deptA, deptB);

        var docA = new Doctor { Id = Guid.NewGuid(), HospitalId = hospitalA.Id, FullName = "Doc A", Specialization = "Gen", LicenseNumber = "L1" };
        var docB = new Doctor { Id = Guid.NewGuid(), HospitalId = hospitalB.Id, FullName = "Doc B", Specialization = "Gen", LicenseNumber = "L2" };
        db.Doctors.AddRange(docA, docB);

        var sessionA = new QueueSession { Id = Guid.NewGuid(), HospitalId = hospitalA.Id, DoctorId = docA.Id, DepartmentId = deptA.Id, Status = QueueSessionStatus.Open, MaxCapacity = 20 };
        var sessionB = new QueueSession { Id = Guid.NewGuid(), HospitalId = hospitalB.Id, DoctorId = docB.Id, DepartmentId = deptB.Id, Status = QueueSessionStatus.Open, MaxCapacity = 20 };
        db.QueueSessions.AddRange(sessionA, sessionB);

        await db.SaveChangesAsync();

        // Patient books at Hospital A
        var bookingA = await bookingService.CreateBookingAsync(new CreateBookingRequestDto
        {
            HospitalId = hospitalA.Id,
            DepartmentId = deptA.Id,
            DoctorId = docA.Id,
            QueueSessionId = sessionA.Id
        }, patient.Id);

        // Patient books at Hospital B
        var bookingB = await bookingService.CreateBookingAsync(new CreateBookingRequestDto
        {
            HospitalId = hospitalB.Id,
            DepartmentId = deptB.Id,
            DoctorId = docB.Id,
            QueueSessionId = sessionB.Id
        }, patient.Id);

        bookingA.Should().NotBeNull();
        bookingB.Should().NotBeNull();
        bookingA.HospitalId.Should().Be(hospitalA.Id);
        bookingB.HospitalId.Should().Be(hospitalB.Id);

        // Verify patient did NOT become a member of either hospital
        var memberships = db.HospitalMembers.Where(hm => hm.UserId == patient.Id).ToList();
        memberships.Should().BeEmpty();
    }
}
