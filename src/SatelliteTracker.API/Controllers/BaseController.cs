using Microsoft.AspNetCore.Mvc;

namespace SatelliteTracker.API.Controllers;



[ApiController]
public abstract class BaseController : ControllerBase
{
    // Helper method to convert error messages into appropriate HTTP responses. If the error message contains "not found",
    // it returns a 404 Not Found response; otherwise, it returns a 500 Internal Server Error response.
    protected IActionResult ToError(string error)
    {
        if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { error });

        if (error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { error });

        return StatusCode(500, new { error });
    }
}