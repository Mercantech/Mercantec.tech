using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/holds")]
[Authorize]
public class HoldsController(AppDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
    {
        var hold = await db.Hold
            .AsNoTracking()
            .Include(h => h.Forloeb)
            .Include(h => h.Memberships)
            .FirstOrDefaultAsync(h => h.Id == id, ct);

        if (hold is null)
            return NotFound();

        return Ok(new
        {
            hold.Id,
            hold.Name,
            hold.ForloebId,
            forloebName = hold.Forloeb.Name,
            hold.LectioHoldKey,
            hold.CreatedAtUtc,
            memberCount = hold.Memberships.Count,
            shadowCount = hold.Memberships.Count(m => m.UserSub == null)
        });
    }

    [HttpGet("{id:guid}/memberships")]
    public async Task<ActionResult<IEnumerable<object>>> ListMemberships(Guid id, CancellationToken ct)
    {
        var exists = await db.Hold.AnyAsync(h => h.Id == id, ct);
        if (!exists)
            return NotFound();

        var items = await db.Memberships
            .AsNoTracking()
            .Where(m => m.HoldId == id)
            .OrderBy(m => m.DisplayName ?? m.LectioId ?? "")
            .Select(m => new
            {
                m.Id,
                m.HoldId,
                m.UserSub,
                m.LectioId,
                m.DisplayName,
                m.Source,
                isShadow = m.UserSub == null,
                m.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    public sealed record AddMembershipRequest(Guid? UserSub, string? LectioId, string? DisplayName, string? Source);

    /// <summary>Manuel tilføjelse af elev (claimed eller shadow). Underviser/Admin.</summary>
    [HttpPost("{id:guid}/memberships")]
    public async Task<ActionResult<object>> AddMembership(Guid id, [FromBody] AddMembershipRequest body, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var hold = await db.Hold.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (hold is null)
            return NotFound();

        var source = string.IsNullOrWhiteSpace(body.Source) ? "manual" : body.Source.Trim().ToLowerInvariant();
        if (source is not ("manual" or "lectio"))
            return BadRequest(new { error = "invalid_source" });

        var userSub = body.UserSub is Guid g && g != Guid.Empty ? g : (Guid?)null;
        var lectioId = string.IsNullOrWhiteSpace(body.LectioId) ? null : body.LectioId.Trim();
        if (userSub is null && lectioId is null)
            return BadRequest(new { error = "need_user_sub_or_lectio_id" });

        if (userSub is not null)
        {
            var exists = await db.Memberships.AnyAsync(m => m.HoldId == id && m.UserSub == userSub, ct);
            if (exists)
                return Conflict(new { error = "already_member" });
        }

        if (lectioId is not null)
        {
            var exists = await db.Memberships.AnyAsync(m => m.HoldId == id && m.LectioId == lectioId, ct);
            if (exists)
                return Conflict(new { error = "already_member_lectio" });
        }

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            HoldId = id,
            UserSub = userSub,
            LectioId = lectioId,
            DisplayName = string.IsNullOrWhiteSpace(body.DisplayName) ? null : body.DisplayName.Trim(),
            Source = source,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Memberships.Add(membership);
        await db.SaveChangesAsync(ct);

        return Created($"/api/holds/{id}/memberships", new
        {
            membership.Id,
            membership.HoldId,
            membership.UserSub,
            membership.LectioId,
            membership.DisplayName,
            membership.Source,
            isShadow = membership.IsShadow,
            membership.CreatedAtUtc
        });
    }
}
