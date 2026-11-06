using System.Text.RegularExpressions;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Speech;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/holds/{holdId:guid}/roulette/speech")]
public sealed class RouletteSpeechController(AppDbContext db, CourseAccess access, RouletteSpeechService speech) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Status(Guid holdId, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Ok(await speech.StatusAsync(ct));
    }

    [HttpPost, RequestSizeLimit(8192)]
    public async Task<IActionResult> Create(Guid holdId, CreateRouletteSpeech request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.GroupName) || request.GroupName.Length > 120 || request.GroupName.Any(char.IsControl)
            || request.MembershipIds is null || request.MembershipIds.Length > 12
            || request.MembershipIds.Distinct().Count() != request.MembershipIds.Length
            || request.IncludeMembers && request.MembershipIds.Length == 0)
            return BadRequest(new { error = "invalid_roulette_speech" });
        var members = await db.Memberships.AsNoTracking().Where(m => m.HoldId == holdId && request.MembershipIds.Contains(m.Id))
            .Select(m => new { m.Id, m.DisplayName }).ToDictionaryAsync(m => m.Id, ct);
        if (members.Count != request.MembershipIds.Length) return BadRequest(new { error = "invalid_roulette_speech" });
        var label = Regex.Replace(request.GroupName.Trim(), @"(?<=[\p{Ll}\p{Nd}])(?=\p{Lu})", " ");
        var text = label + ".";
        if (request.IncludeMembers)
            text += " " + string.Join(", ", request.MembershipIds.Select(id => members[id].DisplayName?.Trim() ?? "Elev")) + ".";
        if (text.Length > 1600 || text.Any(char.IsControl)) return BadRequest(new { error = "invalid_roulette_speech" });
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await speech.GenerateAsync(sub, holdId, text, ct)); }
        catch (RouletteSpeechUnavailableException)
        { return StatusCode(503, new { error = "local_speech_unavailable", message = "Røst kunne ikke gøre oplæsningen klar. Du kan stadig spinne uden oplæsning." }); }
    }
}
