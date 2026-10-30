using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name");
        var email = User.FindFirstValue("email");
        var loginMethod = User.FindFirstValue("login_method");
        var roles = User.Claims
            .Where(c =>
                c.Type is "role" or "roles"
                || c.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r)
            .ToArray();

        var teacherRoleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Admin", "Underviser", "Teacher", "Instructor"
        };
        var appRole = roles.Any(r => teacherRoleNames.Contains(r)) ? "underviser" : "elev";

        return Ok(new
        {
            sub,
            name,
            email,
            loginMethod,
            roles,
            appRole,
            claims = User.Claims
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => g.Select(c => c.Value).Distinct().ToArray())
        });
    }
}
