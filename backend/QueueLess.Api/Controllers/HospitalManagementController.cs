using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueLess.Api.DTOs;
using QueueLess.Api.Services;

namespace QueueLess.Api.Controllers;

[Authorize]
[Route("api/v1/hospital")]
public class HospitalManagementController : BaseApiController
{
    private readonly IHospitalService _hospitals;
    private readonly IAnalyticsService _analytics;
    private readonly IHospitalAiChatService _aiChat;

    public HospitalManagementController(IHospitalService hospitals, IAnalyticsService analytics, IHospitalAiChatService aiChat)
    {
        _hospitals = hospitals;
        _analytics = analytics;
        _aiChat = aiChat;
    }

    private async Task<Guid> ResolveHospitalIdAsync()
    {
        var hId = GetHospitalId();
        if (hId.HasValue) return hId.Value;

        var userId = GetUserId();
        var hospital = await _hospitals.GetHospitalByAdminUserIdAsync(userId);
        return hospital.Id;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyHospital()
    {
        try
        {
            var userId = GetUserId();
            var hospital = await _hospitals.GetHospitalByAdminUserIdAsync(userId);
            return Ok(hospital);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMyHospital([FromBody] HospitalUpdateDto dto)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var updated = await _hospitals.UpdateHospitalAsync(hospitalId, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var hospitalId = await ResolveHospitalIdAsync();
        var depts = await _hospitals.GetDepartmentsAsync(hospitalId);
        return Ok(depts);
    }

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentDto dto)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var created = await _hospitals.CreateDepartmentAsync(hospitalId, dto);
            return StatusCode(201, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("departments/{id}")]
    public async Task<IActionResult> UpdateDepartment(Guid id, [FromBody] UpdateDepartmentDto dto)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var updated = await _hospitals.UpdateDepartmentAsync(hospitalId, id, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("doctors")]
    public async Task<IActionResult> GetDoctors()
    {
        var hospitalId = await ResolveHospitalIdAsync();
        var docs = await _hospitals.GetDoctorsAsync(hospitalId);
        return Ok(docs);
    }

    [HttpPost("doctors")]
    public async Task<IActionResult> CreateDoctor([FromBody] CreateDoctorDto dto)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var created = await _hospitals.CreateDoctorAsync(hospitalId, dto);
            return StatusCode(201, created);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("doctors/{id}")]
    public async Task<IActionResult> UpdateDoctor(Guid id, [FromBody] UpdateDoctorDto dto)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var updated = await _hospitals.UpdateDoctorAsync(hospitalId, id, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> GetSchedules([FromQuery] Guid? doctorId)
    {
        var hospitalId = await ResolveHospitalIdAsync();
        var schedules = await _hospitals.GetSchedulesAsync(hospitalId, doctorId);
        return Ok(schedules);
    }

    [HttpPost("schedules")]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateScheduleDto dto)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var created = await _hospitals.CreateScheduleAsync(hospitalId, dto);
            return StatusCode(201, created);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics()
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var analytics = await _analytics.GetHospitalAnalyticsAsync(hospitalId);
            return Ok(analytics);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("ai-chat")]
    public async Task<IActionResult> AskAiAssistant([FromBody] HospitalAiChatRequestDto dto)
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var response = await _aiChat.ProcessQueryAsync(hospitalId, dto.Message);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
