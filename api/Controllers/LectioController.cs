using System.Net.Http.Headers;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Integrations.Lectio;
using Mercantec.Tech.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

/// <summary>
/// Proxy til Lectio-Collector. Udgående kald bruger den Bearer, JWT-middleware allerede har valideret.
/// Rolle og sub læses kun fra den principal — aldrig fra body eller query.
/// </summary>
[ApiController]
[Route("api/lectio")]
[Authorize]
public class LectioController(LectioCollectorClient lectio, MembershipClaimService claims) : ControllerBase
{
    [HttpGet("me")]
    public Task<IActionResult> Me(CancellationToken ct) =>
        ProxyAsync(() => lectio.GetMeAsync(RequireBearer(), ct), ct);

    [HttpGet("me/student")]
    public Task<IActionResult> MyStudent(CancellationToken ct)
    {
        if (UserClaims.IsTeacher(User))
            return Task.FromResult<IActionResult>(BadRequest(new
            {
                error = "teacher_use_list",
                message = "Brug GET /api/lectio/programmeringselever."
            }));

        return ProxyAsync(() => lectio.GetMyStudentAsync(RequireBearer(), ct), ct);
    }

    [HttpGet("me/lectio-candidates")]
    public Task<IActionResult> LectioCandidates(CancellationToken ct) =>
        ProxyAsync(() => lectio.GetLectioCandidatesAsync(RequireBearer(), ct), ct);

    [HttpGet("programmeringselever")]
    public Task<IActionResult> Programmeringselever(CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Task.FromResult<IActionResult>(Forbid());
        return ProxyAsync(() => lectio.GetProgrammeringseleverAsync(RequireBearer(), ct), ct);
    }

    [HttpGet("students/{elevId}/studieplan")]
    public async Task<IActionResult> Studieplan(string elevId, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
        {
            string? ownId;
            try
            {
                ownId = await ResolveOwnLectioIdAsync(ct);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { error = "missing_bearer" });
            }
            catch (LectioCollectorException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }

            if (string.IsNullOrWhiteSpace(ownId)
                || !string.Equals(ownId, elevId, StringComparison.OrdinalIgnoreCase))
                return Forbid();
        }

        return await ProxyAsync(() => lectio.GetStudieplanAsync(elevId, RequireBearer(), ct), ct);
    }

    [HttpGet("holds/tracked")]
    public Task<IActionResult> TrackedHolds(CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Task.FromResult<IActionResult>(Forbid());
        return ProxyAsync(() => lectio.GetTrackedHoldsAsync(RequireBearer(), ct), ct);
    }

    [HttpGet("holds/saved")]
    public Task<IActionResult> SavedHolds(CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Task.FromResult<IActionResult>(Forbid());
        return ProxyAsync(() => lectio.GetSavedHoldsAsync(RequireBearer(), ct), ct);
    }

    [HttpGet("holds/by-lectio/{lectioHoldKey}")]
    public Task<IActionResult> HoldByLectio(string lectioHoldKey, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Task.FromResult<IActionResult>(Forbid());
        return ProxyAsync(() => lectio.GetHoldByLectioKeyAsync(lectioHoldKey, RequireBearer(), ct), ct);
    }

    public sealed record LinkBody(string LectioId, Guid? AuthSub);

    [HttpPut("me/lectio-link")]
    public async Task<IActionResult> Link([FromBody] LinkBody body, CancellationToken ct)
    {
        try
        {
            var isTeacher = UserClaims.IsTeacher(User);
            Guid? authSub = isTeacher && body.AuthSub is Guid overrideSub && overrideSub != Guid.Empty
                ? overrideSub
                : null;
            await lectio.LinkAsync(body.LectioId, RequireBearer(), authSub, ct);

            var sub = authSub ?? UserClaims.GetSub(User);
            var claimed = 0;
            if (sub is Guid s)
                claimed = await claims.ClaimByLectioIdAsync(s, body.LectioId, ct);

            return Ok(new
            {
                linked = true,
                lectioId = body.LectioId,
                authSub = sub,
                claimedMemberships = claimed
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "missing_bearer" });
        }
        catch (LectioCollectorException ex)
        {
            return StatusCode(ex.StatusCode, ex.Body);
        }
    }

    [HttpPost("sync/memberships/{holdId:guid}")]
    public async Task<IActionResult> SyncMemberships(
        Guid holdId,
        [FromQuery] string lectioHoldKey,
        [FromServices] ILectioMembershipSync sync,
        CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var result = await sync.SyncHoldMembershipsAsync(holdId, lectioHoldKey, RequireBearer(), ct);
        return Ok(result);
    }

    private async Task<IActionResult> ProxyAsync(
        Func<Task<System.Text.Json.JsonElement>> call,
        CancellationToken ct)
    {
        try
        {
            var json = await call();
            return Content(json.GetRawText(), "application/json");
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "missing_bearer" });
        }
        catch (LectioCollectorException ex)
        {
            return StatusCode(ex.StatusCode, ex.Body);
        }
    }

    private async Task<string?> ResolveOwnLectioIdAsync(CancellationToken ct)
    {
        var me = await lectio.GetMeAsync(RequireBearer(), ct);
        if (!me.TryGetProperty("lectio", out var lectioEl)
            || lectioEl.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (lectioEl.ValueKind != JsonValueKind.Object
            || !lectioEl.TryGetProperty("lectioId", out var id)
            || id.ValueKind != JsonValueKind.String)
            return null;
        var value = id.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private string RequireBearer()
    {
        var header = Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(header, out var value)
            && value.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(value.Parameter))
        {
            return value.Parameter;
        }

        throw new UnauthorizedAccessException("missing_bearer");
    }
}
