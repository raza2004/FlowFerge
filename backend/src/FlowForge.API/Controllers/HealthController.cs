using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new {
        status = "healthy",
        service = "FlowForge.API",
        timestamp = DateTime.UtcNow
    });
}
