using System.Security.Claims;
using Mercantec.Tech.Api.Arena;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/arena")]
[Authorize]
public sealed class ArenaController(AppDbContext db, ArenaRoomService rooms, IConfiguration configuration) : ControllerBase
{
    [HttpGet("rooms")]
    public ActionResult<IReadOnlyList<ArenaRoomDto>> List()
    {
        if (!Authenticated(out _)) return Unauthorized();
        return Ok(rooms.ListRooms());
    }

    [HttpPost("rooms")]
    public async Task<ActionResult<ArenaAdmission>> Create([FromBody] ArenaCreateRequest request, CancellationToken ct)
    {
        if (!Authenticated(out var subject)) return Failure(new(401, "authentication_required", "Log ind for at spille online."));
        try { return Ok(rooms.Create(await IdentityAsync(subject, request.AvatarId, request.Appearance, ct), request)); }
        catch (ArenaException ex) { return Failure(ex); }
    }

    [HttpPost("rooms/{code}/join")]
    public async Task<ActionResult<ArenaAdmission>> Join(string code, [FromBody] ArenaJoinRequest request, CancellationToken ct)
    {
        if (!Authenticated(out var subject)) return Failure(new(401, "authentication_required", "Log ind for at spille online."));
        try { return Ok(rooms.Join(code.Trim().ToUpperInvariant(), await IdentityAsync(subject, request.AvatarId, request.Appearance, ct))); }
        catch (ArenaException ex) { return Failure(ex); }
    }

    [HttpPost("rooms/{code}/ticket")]
    public ActionResult<ArenaAdmission> Ticket(string code)
    {
        if (!Authenticated(out var subject)) return Failure(new(401, "authentication_required", "Log ind for at spille online."));
        try { return Ok(rooms.Reconnect(code.Trim().ToUpperInvariant(), subject)); }
        catch (ArenaException ex) { return Failure(ex); }
    }

    [HttpDelete("rooms/{code}/players/me")]
    public IActionResult Leave(string code)
    {
        if (!Authenticated(out var subject)) return Unauthorized();
        try { rooms.Leave(code.Trim().ToUpperInvariant(), subject); return NoContent(); }
        catch (ArenaException ex) { return Failure(ex); }
    }

    [AllowAnonymous]
    [HttpGet("connect")]
    public async Task Connect([FromQuery] string? ticket, CancellationToken ct)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { code = "websocket_required", message = "Arenaen kræver en WebSocket-forbindelse." }, ct);
            return;
        }
        var origin = Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && !IsAllowedOrigin(origin))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        ArenaRoomService.TicketGrant grant;
        try { grant = rooms.ConsumeTicket(ticket); }
        catch (ArenaException ex)
        {
            Response.StatusCode = ex.Status;
            await Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message }, ct);
            return;
        }
        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await rooms.RunSocketAsync(grant, socket, ct);
    }

    private bool IsAllowedOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return false;
        if (string.Equals(uri.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase)) return true;
        return (configuration["Cors:SpaOrigins"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(allowed => string.Equals(origin.TrimEnd('/'), allowed.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ArenaIdentity> IdentityAsync(Guid subject, string? requestedAvatar, ArenaAppearance? appearance, CancellationToken ct)
    {
        var profile = await db.AvatarProfiles.AsNoTracking().Where(p => p.UserSub == subject)
            .Select(p => new { p.SelectedAvatarId }).SingleOrDefaultAsync(ct);
        var owned = (await db.AvatarOwnerships.AsNoTracking().Where(p => p.UserSub == subject).Select(p => p.AvatarId).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        owned.Add(AvatarEconomy.StarterId);
        var selected = string.IsNullOrWhiteSpace(requestedAvatar) ? profile?.SelectedAvatarId ?? AvatarEconomy.StarterId : requestedAvatar;
        if (AvatarEconomy.FindAvatar(selected) is null || !owned.Contains(selected))
            throw new ArenaException(403, "avatar_not_owned", "Lås avataren op på din profil først.");
        var name = User.FindFirstValue("name") ?? User.FindFirstValue("preferred_username") ?? User.FindFirstValue("given_name") ?? "Spiller";
        return new(subject, name, selected, owned, ArenaAppearance.Sanitize(appearance));
    }

    private bool Authenticated(out Guid subject)
    {
        var id = UserClaims.GetSub(User);
        subject = id ?? Guid.Empty;
        return User.Identity?.IsAuthenticated == true && id.HasValue;
    }
    private ObjectResult Failure(ArenaException error)
    {
        var problem = new ProblemDetails { Status = error.Status, Title = error.Message, Detail = error.Message };
        problem.Extensions["code"] = error.Code;
        return StatusCode(error.Status, problem);
    }
}
