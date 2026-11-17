using QueueLess.Api.DTOs;

namespace QueueLess.Api.Services;

public interface IHospitalService
{
    // Registration & Admin self
    Task<HospitalDto> RegisterHospitalAsync(HospitalRegistrationDto dto, Guid userId);
    Task<HospitalDto> GetHospitalByAdminUserIdAsync(Guid userId);
    Task<HospitalDto> UpdateHospitalAsync(Guid hospitalId, HospitalUpdateDto dto);

    // Public Discovery
    Task<List<HospitalDto>> SearchHospitalsAsync(string? name, string? city, string? departmentName);
    Task<HospitalDto> GetHospitalByIdAsync(Guid hospitalId);
    Task<List<DepartmentDto>> GetHospitalDepartmentsAsync(Guid hospitalId);
    Task<List<DoctorDto>> GetHospitalDoctorsAsync(Guid hospitalId, Guid? departmentId = null);

    // Department Management
    Task<List<DepartmentDto>> GetDepartmentsAsync(Guid hospitalId);
    Task<DepartmentDto> CreateDepartmentAsync(Guid hospitalId, CreateDepartmentDto dto);
    Task<DepartmentDto> UpdateDepartmentAsync(Guid hospitalId, Guid departmentId, UpdateDepartmentDto dto);

    // Doctor Management
    Task<List<DoctorDto>> GetDoctorsAsync(Guid hospitalId);
    Task<DoctorDto> CreateDoctorAsync(Guid hospitalId, CreateDoctorDto dto);
    Task<DoctorDto> UpdateDoctorAsync(Guid hospitalId, Guid doctorId, UpdateDoctorDto dto);

    // Schedule Management
    Task<List<DoctorScheduleDto>> GetSchedulesAsync(Guid hospitalId, Guid? doctorId = null);
    Task<DoctorScheduleDto> CreateScheduleAsync(Guid hospitalId, CreateScheduleDto dto);

    // Platform Admin Operations
    Task<List<HospitalDto>> GetAllHospitalsForPlatformAsync(string? status = null);
    Task<HospitalDto> ApproveHospitalAsync(Guid hospitalId);
    Task<HospitalDto> RejectHospitalAsync(Guid hospitalId, string? reason);
    Task<HospitalDto> SuspendHospitalAsync(Guid hospitalId, string? reason);
}
