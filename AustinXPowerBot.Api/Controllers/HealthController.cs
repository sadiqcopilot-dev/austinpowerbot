using Microsoft.AspNetCore.Mvc;

namespace AustinXPowerBot.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { ok = true, service = "AustinXPowerBot.Api" });
}
