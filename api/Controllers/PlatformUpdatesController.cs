using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Updates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/platform-updates")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformUpdatesController : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        NoStore();
        if (User.Identity?.IsAuthenticated != true || UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        return Ok(PlatformUpdatesCatalog.Entries);
    }

    [HttpGet("images/{id}")]
    public IActionResult Image(string id)
    {
        NoStore();
        if (User.Identity?.IsAuthenticated != true || UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var content = PlatformUpdatesCatalog.OpenImage(id);
        if (content is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content, "image/webp");
    }

    private void NoStore() => Response.Headers.CacheControl = "no-store";
}
