using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Updates;
using Mercantec.Tech.Api.Updates.Automation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/platform-updates")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformUpdatesController(PlatformUpdatesFeed feed, PlatformUpdatesAutomationStore? automated = null) : ControllerBase
{
    [HttpGet]
    public IActionResult List([FromQuery] bool includeText = false)
    {
        NoStore();
        if (User.Identity?.IsAuthenticated != true || UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var automatic = automated?.Entries ?? [];
        var automaticIds = automatic.Select(update => update.Id).ToHashSet(StringComparer.Ordinal);
        var originals = feed.Entries.ToDictionary(update => update.Id, StringComparer.Ordinal);
        var entries = automatic.Select(update => originals.TryGetValue(update.Id, out var original)
                ? update with { Authors = original.Authors.Concat(update.Authors).Distinct(StringComparer.Ordinal).ToArray() }
                : update)
            .Concat(feed.Entries.Where(update => !automaticIds.Contains(update.Id)))
            .Where(update => includeText || update.After is not null)
            .OrderByDescending(update => update.Date).ToArray();
        return Ok(entries);
    }

    [HttpGet("images/{id}")]
    public IActionResult Image(string id)
    {
        NoStore();
        if (User.Identity?.IsAuthenticated != true || UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var generated = automated?.OpenImage(id);
        if (generated is not null)
        {
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(generated.Content, generated.ContentType);
        }
        var content = feed.OpenImage(id);
        if (content is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content, "image/webp");
    }

    private void NoStore() => Response.Headers.CacheControl = "no-store";
}
