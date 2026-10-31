using System.Net.Http.Headers;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Integrations.Lectio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

/// <summary>Proxy til Lectio-Collector med brugerens Bearer — elev=egen, underviser=alt (håndhæves i LC).</summary>
[ApiController]
[Route("api/lectio")]
[Authorize]
public class LectioController(LectioCollectorClient lectio) : ControllerBase
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
    public Task<IActionResult> Programmeringselever(CancellationToken ct) =>
        ProxyAsync(() => lectio.GetProgrammeringseleverAsync(RequireBearer(), ct), ct);

    [HttpGet("students/{elevId}/studieplan")]
    public Task<IActionResult> Studieplan(string elevId, CancellationToken ct) =>
        ProxyAsync(() => lectio.GetStudieplanAsync(elevId, RequireBearer(), ct), ct);

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

    public sealed record LinkBody(string LectioId, Guid? AuthSub);

    [HttpPut("me/lectio-link")]
    public async Task<IActionResult> Link([FromBody] LinkBody body, CancellationToken ct)
    {
        try
        {
            await lectio.LinkAsync(body.LectioId, RequireBearer(), body.AuthSub, ct);
            return Ok(new { linked = true, lectioId = body.LectioId, authSub = body.AuthSub });
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
