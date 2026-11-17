using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueLess.Api.DTOs;
using QueueLess.Api.Models;
using QueueLess.Api.Services;

namespace QueueLess.Api.Controllers;

[Authorize]
public class AIController : BaseApiController
{
    private readonly IAIPredictionService _ai;
    private readonly IHospitalService _hospitals;

    public AIController(IAIPredictionService ai, IHospitalService hospitals)
    {
        _ai = ai;
        _hospitals = hospitals;
    }

    private async Task<Guid> ResolveHospitalIdAsync()
    {
        var hId = GetHospitalId();
        if (hId.HasValue) return hId.Value;

        var userId = GetUserId();
        var hospital = await _hospitals.GetHospitalByAdminUserIdAsync(userId);
        return hospital.Id;
    }

    [HttpGet("api/v1/ai/waiting-time/{bookingId}")]
    public async Task<IActionResult> GetWaitingTime(Guid bookingId)
    {
        try
        {
            var result = await _ai.CalculateWaitingTimeAsync(bookingId, savePrediction: true);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("api/v1/hospital/ai/metrics")]
    public async Task<IActionResult> GetHospitalAIMetrics()
    {
        var hospitalId = await ResolveHospitalIdAsync();
        var metrics = await _ai.GetActiveModelMetricsAsync(hospitalId);
        return Ok(metrics ?? new AIModelMetricsDto
        {
            ModelType = MLModelType.HospitalSpecific,
            HospitalId = hospitalId,
            Status = "NO_ACTIVE_MODEL_BASELINE_ACTIVE",
            IsActive = false
        });
    }

    [HttpPost("api/v1/hospital/ai/seed-data")]
    public async Task<IActionResult> SeedSyntheticData([FromBody] SeedSyntheticDataRequestDto? dto)
    {
        try
        {
            var hospitalId = (dto?.HospitalId.HasValue == true && dto.HospitalId.Value != Guid.Empty)
                ? dto.HospitalId.Value
                : await ResolveHospitalIdAsync();
            var count = (dto?.RecordCount > 0) ? dto.RecordCount : 60;
            var added = await _ai.SeedSyntheticConsultationDataAsync(hospitalId, count);
            return Ok(new
            {
                Success = true,
                Message = $"Seeded {added} synthetic consultation records marked 'IsSyntheticDemo = true'.",
                RecordCount = added
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("api/v1/hospital/ai/train")]
    public async Task<IActionResult> TrainHospitalModel()
    {
        try
        {
            var hospitalId = await ResolveHospitalIdAsync();
            var result = await _ai.TrainModelAsync(hospitalId, includeSynthetic: true);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = UserRoles.PlatformAdmin)]
    [HttpGet("api/v1/platform/ai/models")]
    public async Task<IActionResult> GetAllModels()
    {
        var models = await _ai.GetAllModelsAsync();
        return Ok(models);
    }

    [Authorize(Roles = UserRoles.PlatformAdmin)]
    [HttpPost("api/v1/platform/ai/train-shared")]
    public async Task<IActionResult> TrainSharedModel()
    {
        try
        {
            var result = await _ai.TrainModelAsync(hospitalId: null, includeSynthetic: true);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
