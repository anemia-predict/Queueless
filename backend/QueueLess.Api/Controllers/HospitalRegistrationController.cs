using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueLess.Api.DTOs;
using QueueLess.Api.Services;

namespace QueueLess.Api.Controllers;

[Route("api/v1/hospital-registration")]
public class HospitalRegistrationController : BaseApiController
{
    private readonly IHospitalService _hospitals;

    public HospitalRegistrationController(IHospitalService hospitals)
    {
        _hospitals = hospitals;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> RegisterHospital([FromBody] HospitalRegistrationDto dto)
    {
        try
        {
            var userId = GetUserId();
            var hospital = await _hospitals.RegisterHospitalAsync(dto, userId);
            return StatusCode(201, hospital);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
