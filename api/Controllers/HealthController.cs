using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() =>
        Ok(new
        {
            status = "ok",
            service = "mercantec-tech-api",
            utc = DateTime.UtcNow
        });
}
