using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace QueueLess.Api.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected Guid GetUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("userId")?.Value;
        if (Guid.TryParse(idStr, out var id))
        {
            return id;
        }
        throw new UnauthorizedAccessException("User is not authenticated or has an invalid token.");
    }

    protected Guid? GetHospitalId()
    {
        var hStr = User.FindFirst("hospitalId")?.Value;
        if (Guid.TryParse(hStr, out var id))
        {
            return id;
        }
        return null;
    }
}
