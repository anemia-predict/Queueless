using Microsoft.EntityFrameworkCore;
using QueueLess.Api.Data;
using QueueLess.Api.DTOs;
using QueueLess.Api.Models;

namespace QueueLess.Api.Services;

public class HospitalService : IHospitalService
{
    private readonly ApplicationDbContext _db;

    public HospitalService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<HospitalDto> RegisterHospitalAsync(HospitalRegistrationDto dto, Guid userId)
    {
        var existingReg = await _db.Hospitals.AnyAsync(h => h.RegistrationNumber.ToLower() == dto.RegistrationNumber.Trim().ToLower());
        if (existingReg)
        {
            throw new InvalidOperationException($"Hospital with registration number '{dto.RegistrationNumber}' already exists.");
        }

        var hospital = new Hospital
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            RegistrationNumber = dto.RegistrationNumber.Trim(),
            Address = dto.Address.Trim(),
            City = dto.City.Trim(),
            Area = dto.Area?.Trim(),
            State = dto.State?.Trim(),
            ContactPhone = dto.ContactPhone.Trim(),
            ContactEmail = dto.ContactEmail.Trim().ToLowerInvariant(),
            Status = HospitalStatus.Pending,
            InitialConsultationMinutes = dto.InitialConsultationMinutes > 0 ? dto.InitialConsultationMinutes : 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Hospitals.Add(hospital);

        // Associate user as Hospital Admin
        var member = new HospitalMember
        {
            Id = Guid.NewGuid(),
            HospitalId = hospital.Id,
            UserId = userId,
            RoleInHospital = HospitalMemberRoles.Admin,
            CreatedAt = DateTime.UtcNow
        };
        _db.HospitalMembers.Add(member);

        // Update user role if not already admin
        var user = await _db.Users.FindAsync(userId);
        if (user != null && user.Role != UserRoles.PlatformAdmin)
        {
            user.Role = UserRoles.HospitalAdmin;
        }

        await _db.SaveChangesAsync();

        return MapToDto(hospital, 0, 0);
    }

    public async Task<HospitalDto> GetHospitalByAdminUserIdAsync(Guid userId)
    {
        var member = await _db.HospitalMembers
            .Include(hm => hm.Hospital)
                .ThenInclude(h => h.Departments)
            .Include(hm => hm.Hospital)
                .ThenInclude(h => h.Doctors)
            .FirstOrDefaultAsync(hm => hm.UserId == userId);

        if (member?.Hospital == null)
        {
            throw new KeyNotFoundException("No hospital associated with this administrator.");
        }

        return MapToDto(member.Hospital, member.Hospital.Departments.Count, member.Hospital.Doctors.Count);
    }

    public async Task<HospitalDto> UpdateHospitalAsync(Guid hospitalId, HospitalUpdateDto dto)
    {
        var hospital = await _db.Hospitals
            .Include(h => h.Departments)
            .Include(h => h.Doctors)
            .FirstOrDefaultAsync(h => h.Id == hospitalId);

        if (hospital == null)
        {
            throw new KeyNotFoundException("Hospital not found.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Name)) hospital.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Address)) hospital.Address = dto.Address.Trim();
        if (!string.IsNullOrWhiteSpace(dto.City)) hospital.City = dto.City.Trim();
        if (dto.Area != null) hospital.Area = dto.Area.Trim();
        if (dto.State != null) hospital.State = dto.State.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ContactPhone)) hospital.ContactPhone = dto.ContactPhone.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ContactEmail)) hospital.ContactEmail = dto.ContactEmail.Trim().ToLowerInvariant();
        if (dto.InitialConsultationMinutes.HasValue && dto.InitialConsultationMinutes.Value > 0)
        {
            hospital.InitialConsultationMinutes = dto.InitialConsultationMinutes.Value;
        }

        hospital.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapToDto(hospital, hospital.Departments.Count, hospital.Doctors.Count);
    }

    public async Task<List<HospitalDto>> SearchHospitalsAsync(string? name, string? city, string? departmentName)
    {
        var query = _db.Hospitals
            .Where(h => h.Status == HospitalStatus.Approved)
            .Include(h => h.Departments.Where(d => d.IsActive))
            .Include(h => h.Doctors.Where(d => d.IsActive))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            var searchName = name.Trim().ToLower();
            query = query.Where(h => h.Name.ToLower().Contains(searchName));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var searchCity = city.Trim().ToLower();
            query = query.Where(h => h.City.ToLower().Contains(searchCity));
        }

        if (!string.IsNullOrWhiteSpace(departmentName))
        {
            var searchDept = departmentName.Trim().ToLower();
            query = query.Where(h => h.Departments.Any(d => d.IsActive && d.Name.ToLower().Contains(searchDept)));
        }

        var hospitals = await query.ToListAsync();

        return hospitals.Select(h => MapToDto(h, h.Departments.Count, h.Doctors.Count)).ToList();
    }

    public async Task<HospitalDto> GetHospitalByIdAsync(Guid hospitalId)
    {
        var hospital = await _db.Hospitals
            .Include(h => h.Departments.Where(d => d.IsActive))
            .Include(h => h.Doctors.Where(d => d.IsActive))
            .FirstOrDefaultAsync(h => h.Id == hospitalId);

        if (hospital == null)
        {
            throw new KeyNotFoundException("Hospital not found.");
        }

        return MapToDto(hospital, hospital.Departments.Count, hospital.Doctors.Count);
    }

    public async Task<List<DepartmentDto>> GetHospitalDepartmentsAsync(Guid hospitalId)
    {
        var departments = await _db.Departments
            .Where(d => d.HospitalId == hospitalId && d.IsActive)
            .Include(d => d.DoctorDepartments)
            .ToListAsync();

        return departments.Select(d => new DepartmentDto
        {
            Id = d.Id,
            HospitalId = d.HospitalId,
            Name = d.Name,
            Description = d.Description,
            DefaultConsultationMinutes = d.DefaultConsultationMinutes,
            IsActive = d.IsActive,
            DoctorCount = d.DoctorDepartments.Count
        }).ToList();
    }

    public async Task<List<DoctorDto>> GetHospitalDoctorsAsync(Guid hospitalId, Guid? departmentId = null)
    {
        var query = _db.Doctors
            .Where(d => d.HospitalId == hospitalId && d.IsActive)
            .Include(d => d.DoctorDepartments)
                .ThenInclude(dd => dd.Department)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(d => d.DoctorDepartments.Any(dd => dd.DepartmentId == departmentId.Value));
        }

        var doctors = await query.ToListAsync();

        return doctors.Select(d => new DoctorDto
        {
            Id = d.Id,
            HospitalId = d.HospitalId,
            UserId = d.UserId,
            FullName = d.FullName,
            Specialization = d.Specialization,
            LicenseNumber = d.LicenseNumber,
            IsActive = d.IsActive,
            Departments = d.DoctorDepartments.Select(dd => new DepartmentDto
            {
                Id = dd.Department.Id,
                HospitalId = dd.Department.HospitalId,
                Name = dd.Department.Name,
                Description = dd.Department.Description,
                DefaultConsultationMinutes = dd.Department.DefaultConsultationMinutes,
                IsActive = dd.Department.IsActive
            }).ToList()
        }).ToList();
    }

    public async Task<List<DepartmentDto>> GetDepartmentsAsync(Guid hospitalId)
    {
        var departments = await _db.Departments
            .Where(d => d.HospitalId == hospitalId)
            .Include(d => d.DoctorDepartments)
            .ToListAsync();

        return departments.Select(d => new DepartmentDto
        {
            Id = d.Id,
            HospitalId = d.HospitalId,
            Name = d.Name,
            Description = d.Description,
            DefaultConsultationMinutes = d.DefaultConsultationMinutes,
            IsActive = d.IsActive,
            DoctorCount = d.DoctorDepartments.Count
        }).ToList();
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(Guid hospitalId, CreateDepartmentDto dto)
    {
        var existing = await _db.Departments
            .AnyAsync(d => d.HospitalId == hospitalId && d.Name.ToLower() == dto.Name.Trim().ToLower());

        if (existing)
        {
            throw new InvalidOperationException($"Department '{dto.Name}' already exists in this hospital.");
        }

        var dept = new Department
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            DefaultConsultationMinutes = dto.DefaultConsultationMinutes > 0 ? dto.DefaultConsultationMinutes : 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();

        return new DepartmentDto
        {
            Id = dept.Id,
            HospitalId = dept.HospitalId,
            Name = dept.Name,
            Description = dept.Description,
            DefaultConsultationMinutes = dept.DefaultConsultationMinutes,
            IsActive = dept.IsActive,
            DoctorCount = 0
        };
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid hospitalId, Guid departmentId, UpdateDepartmentDto dto)
    {
        var dept = await _db.Departments
            .Include(d => d.DoctorDepartments)
            .FirstOrDefaultAsync(d => d.Id == departmentId && d.HospitalId == hospitalId);

        if (dept == null)
        {
            throw new KeyNotFoundException("Department not found in this hospital.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Name)) dept.Name = dto.Name.Trim();
        if (dto.Description != null) dept.Description = dto.Description.Trim();
        if (dto.DefaultConsultationMinutes.HasValue && dto.DefaultConsultationMinutes.Value > 0)
        {
            dept.DefaultConsultationMinutes = dto.DefaultConsultationMinutes.Value;
        }
        if (dto.IsActive.HasValue) dept.IsActive = dto.IsActive.Value;

        await _db.SaveChangesAsync();

        return new DepartmentDto
        {
            Id = dept.Id,
            HospitalId = dept.HospitalId,
            Name = dept.Name,
            Description = dept.Description,
            DefaultConsultationMinutes = dept.DefaultConsultationMinutes,
            IsActive = dept.IsActive,
            DoctorCount = dept.DoctorDepartments.Count
        };
    }

    public async Task<List<DoctorDto>> GetDoctorsAsync(Guid hospitalId)
    {
        return await GetHospitalDoctorsAsync(hospitalId);
    }

    public async Task<DoctorDto> CreateDoctorAsync(Guid hospitalId, CreateDoctorDto dto)
    {
        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            FullName = dto.FullName.Trim(),
            Specialization = dto.Specialization.Trim(),
            LicenseNumber = dto.LicenseNumber.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // If email and password provided, create login for the doctor
        if (!string.IsNullOrWhiteSpace(dto.DoctorEmail) && !string.IsNullOrWhiteSpace(dto.DoctorPassword))
        {
            var email = dto.DoctorEmail.Trim().ToLowerInvariant();
            var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (existingUser != null)
            {
                doctor.UserId = existingUser.Id;
            }
            else
            {
                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.DoctorPassword),
                    FullName = dto.FullName.Trim(),
                    Role = UserRoles.Doctor,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Users.Add(newUser);
                doctor.UserId = newUser.Id;

                // Also add hospital membership
                _db.HospitalMembers.Add(new HospitalMember
                {
                    Id = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    UserId = newUser.Id,
                    RoleInHospital = HospitalMemberRoles.Doctor,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _db.Doctors.Add(doctor);

        // Assign departments (validating they belong to THIS hospital)
        if (dto.DepartmentIds != null && dto.DepartmentIds.Count > 0)
        {
            var validDepts = await _db.Departments
                .Where(d => d.HospitalId == hospitalId && dto.DepartmentIds.Contains(d.Id))
                .ToListAsync();

            foreach (var d in validDepts)
            {
                _db.DoctorDepartments.Add(new DoctorDepartment
                {
                    Id = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    DoctorId = doctor.Id,
                    DepartmentId = d.Id
                });
            }
        }

        await _db.SaveChangesAsync();

        return (await GetHospitalDoctorsAsync(hospitalId)).First(d => d.Id == doctor.Id);
    }

    public async Task<DoctorDto> UpdateDoctorAsync(Guid hospitalId, Guid doctorId, UpdateDoctorDto dto)
    {
        var doctor = await _db.Doctors
            .Include(d => d.DoctorDepartments)
            .FirstOrDefaultAsync(d => d.Id == doctorId && d.HospitalId == hospitalId);

        if (doctor == null)
        {
            throw new KeyNotFoundException("Doctor not found in this hospital.");
        }

        if (!string.IsNullOrWhiteSpace(dto.FullName)) doctor.FullName = dto.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Specialization)) doctor.Specialization = dto.Specialization.Trim();
        if (!string.IsNullOrWhiteSpace(dto.LicenseNumber)) doctor.LicenseNumber = dto.LicenseNumber.Trim();
        if (dto.IsActive.HasValue) doctor.IsActive = dto.IsActive.Value;

        if (dto.DepartmentIds != null)
        {
            _db.DoctorDepartments.RemoveRange(doctor.DoctorDepartments);

            var validDepts = await _db.Departments
                .Where(d => d.HospitalId == hospitalId && dto.DepartmentIds.Contains(d.Id))
                .ToListAsync();

            foreach (var d in validDepts)
            {
                _db.DoctorDepartments.Add(new DoctorDepartment
                {
                    Id = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    DoctorId = doctor.Id,
                    DepartmentId = d.Id
                });
            }
        }

        await _db.SaveChangesAsync();

        return (await GetHospitalDoctorsAsync(hospitalId)).First(d => d.Id == doctor.Id);
    }

    public async Task<List<DoctorScheduleDto>> GetSchedulesAsync(Guid hospitalId, Guid? doctorId = null)
    {
        var query = _db.DoctorSchedules
            .Where(s => s.HospitalId == hospitalId)
            .Include(s => s.Doctor)
            .Include(s => s.Department)
            .AsQueryable();

        if (doctorId.HasValue)
        {
            query = query.Where(s => s.DoctorId == doctorId.Value);
        }

        var schedules = await query.ToListAsync();

        return schedules.Select(s => new DoctorScheduleDto
        {
            Id = s.Id,
            HospitalId = s.HospitalId,
            DoctorId = s.DoctorId,
            DoctorName = s.Doctor.FullName,
            DepartmentId = s.DepartmentId,
            DepartmentName = s.Department.Name,
            DayOfWeek = s.DayOfWeek,
            DayName = Enum.GetName(typeof(DayOfWeek), s.DayOfWeek) ?? s.DayOfWeek.ToString(),
            StartTime = s.StartTime.ToString(@"hh\:mm"),
            EndTime = s.EndTime.ToString(@"hh\:mm"),
            SlotDurationMinutes = s.SlotDurationMinutes,
            MaxPatientsPerSession = s.MaxPatientsPerSession,
            IsActive = s.IsActive
        }).ToList();
    }

    public async Task<DoctorScheduleDto> CreateScheduleAsync(Guid hospitalId, CreateScheduleDto dto)
    {
        // Enforce tenant boundary: doctor & department must belong to hospital
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == dto.DoctorId && d.HospitalId == hospitalId);
        if (doctor == null) throw new KeyNotFoundException("Doctor does not belong to this hospital.");

        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == dto.DepartmentId && d.HospitalId == hospitalId);
        if (dept == null) throw new KeyNotFoundException("Department does not belong to this hospital.");

        if (!TimeSpan.TryParse(dto.StartTime, out var startTime)) startTime = new TimeSpan(9, 0, 0);
        if (!TimeSpan.TryParse(dto.EndTime, out var endTime)) endTime = new TimeSpan(17, 0, 0);

        var schedule = new DoctorSchedule
        {
            Id = Guid.NewGuid(),
            HospitalId = hospitalId,
            DoctorId = dto.DoctorId,
            DepartmentId = dto.DepartmentId,
            DayOfWeek = dto.DayOfWeek,
            StartTime = startTime,
            EndTime = endTime,
            SlotDurationMinutes = dto.SlotDurationMinutes > 0 ? dto.SlotDurationMinutes : 15,
            MaxPatientsPerSession = dto.MaxPatientsPerSession > 0 ? dto.MaxPatientsPerSession : 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.DoctorSchedules.Add(schedule);
        await _db.SaveChangesAsync();

        return new DoctorScheduleDto
        {
            Id = schedule.Id,
            HospitalId = schedule.HospitalId,
            DoctorId = schedule.DoctorId,
            DoctorName = doctor.FullName,
            DepartmentId = schedule.DepartmentId,
            DepartmentName = dept.Name,
            DayOfWeek = schedule.DayOfWeek,
            DayName = Enum.GetName(typeof(DayOfWeek), schedule.DayOfWeek) ?? schedule.DayOfWeek.ToString(),
            StartTime = schedule.StartTime.ToString(@"hh\:mm"),
            EndTime = schedule.EndTime.ToString(@"hh\:mm"),
            SlotDurationMinutes = schedule.SlotDurationMinutes,
            MaxPatientsPerSession = schedule.MaxPatientsPerSession,
            IsActive = schedule.IsActive
        };
    }

    public async Task<List<HospitalDto>> GetAllHospitalsForPlatformAsync(string? status = null)
    {
        var query = _db.Hospitals
            .Include(h => h.Departments)
            .Include(h => h.Doctors)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToUpper();
            query = query.Where(h => h.Status == s);
        }

        var hospitals = await query.OrderByDescending(h => h.CreatedAt).ToListAsync();

        return hospitals.Select(h => MapToDto(h, h.Departments.Count, h.Doctors.Count)).ToList();
    }

    public async Task<HospitalDto> ApproveHospitalAsync(Guid hospitalId)
    {
        var hospital = await _db.Hospitals
            .Include(h => h.Departments)
            .Include(h => h.Doctors)
            .FirstOrDefaultAsync(h => h.Id == hospitalId);

        if (hospital == null) throw new KeyNotFoundException("Hospital not found.");

        hospital.Status = HospitalStatus.Approved;
        hospital.RejectionReason = null;
        hospital.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToDto(hospital, hospital.Departments.Count, hospital.Doctors.Count);
    }

    public async Task<HospitalDto> RejectHospitalAsync(Guid hospitalId, string? reason)
    {
        var hospital = await _db.Hospitals
            .Include(h => h.Departments)
            .Include(h => h.Doctors)
            .FirstOrDefaultAsync(h => h.Id == hospitalId);

        if (hospital == null) throw new KeyNotFoundException("Hospital not found.");

        hospital.Status = HospitalStatus.Rejected;
        hospital.RejectionReason = reason;
        hospital.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToDto(hospital, hospital.Departments.Count, hospital.Doctors.Count);
    }

    public async Task<HospitalDto> SuspendHospitalAsync(Guid hospitalId, string? reason)
    {
        var hospital = await _db.Hospitals
            .Include(h => h.Departments)
            .Include(h => h.Doctors)
            .FirstOrDefaultAsync(h => h.Id == hospitalId);

        if (hospital == null) throw new KeyNotFoundException("Hospital not found.");

        hospital.Status = HospitalStatus.Suspended;
        hospital.RejectionReason = reason;
        hospital.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToDto(hospital, hospital.Departments.Count, hospital.Doctors.Count);
    }

    private static HospitalDto MapToDto(Hospital h, int deptCount, int docCount)
    {
        return new HospitalDto
        {
            Id = h.Id,
            Name = h.Name,
            RegistrationNumber = h.RegistrationNumber,
            Address = h.Address,
            City = h.City,
            Area = h.Area,
            State = h.State,
            ContactPhone = h.ContactPhone,
            ContactEmail = h.ContactEmail,
            Status = h.Status,
            RejectionReason = h.RejectionReason,
            InitialConsultationMinutes = h.InitialConsultationMinutes,
            DepartmentCount = deptCount,
            DoctorCount = docCount,
            CreatedAt = h.CreatedAt
        };
    }
}
