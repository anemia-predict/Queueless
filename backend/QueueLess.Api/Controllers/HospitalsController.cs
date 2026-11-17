using Microsoft.AspNetCore.Mvc;
using QueueLess.Api.Services;

namespace QueueLess.Api.Controllers;

[Route("api/v1/hospitals")]
public class HospitalsController : BaseApiController
{
    private readonly IHospitalService _hospitals;
    private readonly IQueueService _queues;

    public HospitalsController(IHospitalService hospitals, IQueueService queues)
    {
        _hospitals = hospitals;
        _queues = queues;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? name, [FromQuery] string? city, [FromQuery] string? department)
    {
        var results = await _hospitals.SearchHospitalsAsync(name, city, department);
        return Ok(results);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var hospital = await _hospitals.GetHospitalByIdAsync(id);
            return Ok(hospital);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/departments")]
    public async Task<IActionResult> GetDepartments(Guid id)
    {
        var departments = await _hospitals.GetHospitalDepartmentsAsync(id);
        return Ok(departments);
    }

    [HttpGet("{id}/doctors")]
    public async Task<IActionResult> GetDoctors(Guid id, [FromQuery] Guid? departmentId)
    {
        var doctors = await _hospitals.GetHospitalDoctorsAsync(id, departmentId);
        return Ok(doctors);
    }

    [HttpGet("{id}/queue-sessions")]
    public async Task<IActionResult> GetQueueSessions(Guid id, [FromQuery] Guid? doctorId)
    {
        var sessions = await _queues.GetQueueSessionsAsync(id, doctorId);
        return Ok(sessions);
    }
}
