using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueLess.Api.DTOs;
using QueueLess.Api.Models;
using QueueLess.Api.Services;

namespace QueueLess.Api.Controllers;

[Authorize(Roles = UserRoles.PlatformAdmin)]
[Route("api/v1/platform")]
public class PlatformAdminController : BaseApiController
{
    private readonly IHospitalService _hospitals;
    private readonly IAnalyticsService _analytics;

    public PlatformAdminController(IHospitalService hospitals, IAnalyticsService analytics)
    {
        _hospitals = hospitals;
        _analytics = analytics;
    }

    [HttpGet("hospitals")]
    public async Task<IActionResult> GetAllHospitals([FromQuery] string? status)
    {
        var hospitals = await _hospitals.GetAllHospitalsForPlatformAsync(status);
        return Ok(hospitals);
    }

    [HttpGet("hospitals/pending")]
    public async Task<IActionResult> GetPendingHospitals()
    {
        var hospitals = await _hospitals.GetAllHospitalsForPlatformAsync(HospitalStatus.Pending);
        return Ok(hospitals);
    }

    [HttpPost("hospitals/{id}/approve")]
    public async Task<IActionResult> ApproveHospital(Guid id)
    {
        try
        {
            var approved = await _hospitals.ApproveHospitalAsync(id);
            return Ok(approved);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("hospitals/{id}/reject")]
    public async Task<IActionResult> RejectHospital(Guid id, [FromBody] HospitalStatusDecisionDto? dto)
    {
        try
        {
            var rejected = await _hospitals.RejectHospitalAsync(id, dto?.Reason);
            return Ok(rejected);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("hospitals/{id}/suspend")]
    public async Task<IActionResult> SuspendHospital(Guid id, [FromBody] HospitalStatusDecisionDto? dto)
    {
        try
        {
            var suspended = await _hospitals.SuspendHospitalAsync(id, dto?.Reason);
            return Ok(suspended);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> GetPlatformAnalytics()
    {
        var analytics = await _analytics.GetPlatformAnalyticsAsync();
        return Ok(analytics);
    }
}
