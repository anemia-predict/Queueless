using Microsoft.EntityFrameworkCore;
using QueueLess.Api.Models;
using QueueLess.Api.Services;

namespace QueueLess.Api.Data;

public class DataSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly IAuthService _auth;

    public DataSeeder(ApplicationDbContext db, IAuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task SeedAsync()
    {
        // 1. Seed Platform Admin if not present
        var adminEmail = "platformadmin@queueless.local";
        var admin = await _db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
        if (admin == null)
        {
            admin = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                PasswordHash = _auth.HashPassword("Admin@123456"),
                FullName = "Platform Administrator",
                PhoneNumber = "+919876543210",
                Role = UserRoles.PlatformAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Users.Add(admin);
            await _db.SaveChangesAsync();
        }

        // 2. Seed Patient Arun
        var patientEmail = "patient.arun@queueless.local";
        var patient = await _db.Users.FirstOrDefaultAsync(u => u.Email == patientEmail);
        if (patient == null)
        {
            patient = new User
            {
                Id = Guid.NewGuid(),
                Email = patientEmail,
                PasswordHash = _auth.HashPassword("Patient@123"),
                FullName = "Arun Kumar",
                PhoneNumber = "+919876543220",
                Role = UserRoles.Patient,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Users.Add(patient);
            await _db.SaveChangesAsync();
        }

        // 2b. Seed Patient Priya (Friend 2)
        var priyaEmail = "patient.priya@queueless.local";
        var priya = await _db.Users.FirstOrDefaultAsync(u => u.Email == priyaEmail);
        if (priya == null)
        {
            priya = new User
            {
                Id = Guid.NewGuid(),
                Email = priyaEmail,
                PasswordHash = _auth.HashPassword("Patient@123"),
                FullName = "Priya Sharma",
                PhoneNumber = "+919876543221",
                Role = UserRoles.Patient,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Users.Add(priya);
            await _db.SaveChangesAsync();
        }

        // 2c. Seed Patient Rahul (Friend 3)
        var rahulEmail = "patient.rahul@queueless.local";
        var rahul = await _db.Users.FirstOrDefaultAsync(u => u.Email == rahulEmail);
        if (rahul == null)
        {
            rahul = new User
            {
                Id = Guid.NewGuid(),
                Email = rahulEmail,
                PasswordHash = _auth.HashPassword("Patient@123"),
                FullName = "Rahul Verma",
                PhoneNumber = "+919876543222",
                Role = UserRoles.Patient,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Users.Add(rahul);
            await _db.SaveChangesAsync();
        }

        // 3. Seed Hospital A: Apollo City Hospital (APPROVED)
        var hospitalAReg = "HOSP-BLR-001";
        var hospitalA = await _db.Hospitals.FirstOrDefaultAsync(h => h.RegistrationNumber == hospitalAReg);
        if (hospitalA == null)
        {
            hospitalA = new Hospital
            {
                Id = Guid.NewGuid(),
                Name = "Apollo City Hospital",
                RegistrationNumber = hospitalAReg,
                Address = "15 Bannerghatta Main Road",
                City = "Bengaluru",
                Area = "Jayanagar",
                State = "Karnataka",
                ContactPhone = "+918023456789",
                ContactEmail = "apollo.admin@queueless.local",
                Status = HospitalStatus.Approved,
                InitialConsultationMinutes = 12,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Hospitals.Add(hospitalA);

            var adminAUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "apollo.admin@queueless.local",
                PasswordHash = _auth.HashPassword("Hospital@123"),
                FullName = "Dr. Suresh Reddy (Admin)",
                PhoneNumber = "+918023456780",
                Role = UserRoles.HospitalAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(adminAUser);

            _db.HospitalMembers.Add(new HospitalMember
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalA.Id,
                UserId = adminAUser.Id,
                RoleInHospital = HospitalMemberRoles.Admin,
                CreatedAt = DateTime.UtcNow
            });

            // Departments
            var deptCardio = new Department
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalA.Id,
                Name = "Cardiology",
                Description = "Specialized cardiovascular care and diagnostics.",
                DefaultConsultationMinutes = 15,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var deptGenMed = new Department
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalA.Id,
                Name = "General Medicine",
                Description = "Primary comprehensive healthcare and medical diagnoses.",
                DefaultConsultationMinutes = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var deptPedia = new Department
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalA.Id,
                Name = "Pediatrics",
                Description = "Child healthcare, immunization and pediatric treatment.",
                DefaultConsultationMinutes = 12,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Departments.AddRange(deptCardio, deptGenMed, deptPedia);

            // Doctors
            var docRajeshUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "doctor.rajesh@queueless.local",
                PasswordHash = _auth.HashPassword("Doctor@123"),
                FullName = "Dr. Rajesh Sharma",
                Role = UserRoles.Doctor,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(docRajeshUser);

            _db.HospitalMembers.Add(new HospitalMember
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalA.Id,
                UserId = docRajeshUser.Id,
                RoleInHospital = HospitalMemberRoles.Doctor,
                CreatedAt = DateTime.UtcNow
            });

            var docRajesh = new Doctor
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalA.Id,
                UserId = docRajeshUser.Id,
                FullName = "Dr. Rajesh Sharma",
                Specialization = "Senior Cardiologist",
                LicenseNumber = "MCI-CARD-9921",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var docPriya = new Doctor
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalA.Id,
                FullName = "Dr. Priya Nair",
                Specialization = "Chief Pediatrician",
                LicenseNumber = "MCI-PED-8812",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var docAmit = new Doctor
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalA.Id,
                FullName = "Dr. Amit Verma",
                Specialization = "General Physician",
                LicenseNumber = "MCI-GEN-7733",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Doctors.AddRange(docRajesh, docPriya, docAmit);

            // Doctor Departments
            _db.DoctorDepartments.Add(new DoctorDepartment { Id = Guid.NewGuid(), HospitalId = hospitalA.Id, DoctorId = docRajesh.Id, DepartmentId = deptCardio.Id });
            _db.DoctorDepartments.Add(new DoctorDepartment { Id = Guid.NewGuid(), HospitalId = hospitalA.Id, DoctorId = docPriya.Id, DepartmentId = deptPedia.Id });
            _db.DoctorDepartments.Add(new DoctorDepartment { Id = Guid.NewGuid(), HospitalId = hospitalA.Id, DoctorId = docAmit.Id, DepartmentId = deptGenMed.Id });

            // Doctor Schedules
            var schedRajesh = new DoctorSchedule
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalA.Id,
                DoctorId = docRajesh.Id,
                DepartmentId = deptCardio.Id,
                DayOfWeek = (int)DateTime.UtcNow.DayOfWeek,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                SlotDurationMinutes = 15,
                MaxPatientsPerSession = 25,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.DoctorSchedules.Add(schedRajesh);

            // Active Queue Session for Dr. Rajesh
            var sessionRajesh = new QueueSession
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalA.Id,
                DoctorId = docRajesh.Id,
                DepartmentId = deptCardio.Id,
                ScheduleId = schedRajesh.Id,
                SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                Status = QueueSessionStatus.Open,
                MaxCapacity = 25,
                CurrentTokenNumber = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.QueueSessions.Add(sessionRajesh);

            await _db.SaveChangesAsync();
        }

        // 4. Seed Hospital B: St. Jude Medical Center (APPROVED)
        var hospitalBReg = "HOSP-MUM-002";
        var hospitalB = await _db.Hospitals.FirstOrDefaultAsync(h => h.RegistrationNumber == hospitalBReg);
        if (hospitalB == null)
        {
            hospitalB = new Hospital
            {
                Id = Guid.NewGuid(),
                Name = "St. Jude Medical Center",
                RegistrationNumber = hospitalBReg,
                Address = "44 Linking Road, Bandra West",
                City = "Mumbai",
                Area = "Bandra",
                State = "Maharashtra",
                ContactPhone = "+912226401122",
                ContactEmail = "stjude.admin@queueless.local",
                Status = HospitalStatus.Approved,
                InitialConsultationMinutes = 10,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Hospitals.Add(hospitalB);

            var adminBUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "stjude.admin@queueless.local",
                PasswordHash = _auth.HashPassword("Hospital@123"),
                FullName = "Sr. Maria Fernandes (Admin)",
                PhoneNumber = "+912226401120",
                Role = UserRoles.HospitalAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(adminBUser);

            _db.HospitalMembers.Add(new HospitalMember
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalB.Id,
                UserId = adminBUser.Id,
                RoleInHospital = HospitalMemberRoles.Admin,
                CreatedAt = DateTime.UtcNow
            });

            var deptOrtho = new Department
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalB.Id,
                Name = "Orthopedics",
                Description = "Bone, joint and musculoskeletal diagnostics and treatment.",
                DefaultConsultationMinutes = 15,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var deptDerma = new Department
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalB.Id,
                Name = "Dermatology",
                Description = "Skin, hair and dermatological consultations.",
                DefaultConsultationMinutes = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Departments.AddRange(deptOrtho, deptDerma);

            var docVikram = new Doctor
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalB.Id,
                FullName = "Dr. Vikram Patel",
                Specialization = "Orthopedic Surgeon",
                LicenseNumber = "MMC-ORT-4411",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var docAnanya = new Doctor
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalB.Id,
                FullName = "Dr. Ananya Sen",
                Specialization = "Consultant Dermatologist",
                LicenseNumber = "MMC-DER-5522",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Doctors.AddRange(docVikram, docAnanya);

            _db.DoctorDepartments.Add(new DoctorDepartment { Id = Guid.NewGuid(), HospitalId = hospitalB.Id, DoctorId = docVikram.Id, DepartmentId = deptOrtho.Id });
            _db.DoctorDepartments.Add(new DoctorDepartment { Id = Guid.NewGuid(), HospitalId = hospitalB.Id, DoctorId = docAnanya.Id, DepartmentId = deptDerma.Id });

            var schedVikram = new DoctorSchedule
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalB.Id,
                DoctorId = docVikram.Id,
                DepartmentId = deptOrtho.Id,
                DayOfWeek = (int)DateTime.UtcNow.DayOfWeek,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(18, 0, 0),
                SlotDurationMinutes = 15,
                MaxPatientsPerSession = 20,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.DoctorSchedules.Add(schedVikram);

            var sessionVikram = new QueueSession
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalB.Id,
                DoctorId = docVikram.Id,
                DepartmentId = deptOrtho.Id,
                ScheduleId = schedVikram.Id,
                SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(18, 0, 0),
                Status = QueueSessionStatus.Open,
                MaxCapacity = 20,
                CurrentTokenNumber = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.QueueSessions.Add(sessionVikram);

            await _db.SaveChangesAsync();
        }

        // 5. Seed Hospital C: New Metro Clinic (PENDING approval)
        var hospitalCReg = "HOSP-DEL-003";
        var hospitalC = await _db.Hospitals.FirstOrDefaultAsync(h => h.RegistrationNumber == hospitalCReg);
        if (hospitalC == null)
        {
            hospitalC = new Hospital
            {
                Id = Guid.NewGuid(),
                Name = "New Metro Clinic",
                RegistrationNumber = hospitalCReg,
                Address = "12 Connaught Place",
                City = "New Delhi",
                Area = "Central",
                State = "Delhi",
                ContactPhone = "+911145678900",
                ContactEmail = "metro.admin@queueless.local",
                Status = HospitalStatus.Pending,
                InitialConsultationMinutes = 10,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Hospitals.Add(hospitalC);

            var adminCUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "metro.admin@queueless.local",
                PasswordHash = _auth.HashPassword("Hospital@123"),
                FullName = "Dr. Rakesh Mehra (Applicant)",
                PhoneNumber = "+911145678901",
                Role = UserRoles.HospitalAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(adminCUser);

            _db.HospitalMembers.Add(new HospitalMember
            {
                Id = Guid.NewGuid(),
                HospitalId = hospitalC.Id,
                UserId = adminCUser.Id,
                RoleInHospital = HospitalMemberRoles.Admin,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
    }
}
