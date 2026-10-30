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
            hold.CreatedAtUtc,
            memberCount = hold.Memberships.Count
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
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new
            {
                m.Id,
                m.HoldId,
                m.UserSub,
                m.Source,
                m.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    public sealed record AddMembershipRequest(Guid UserSub, string? Source);

    /// <summary>Manuel tilføjelse af elev (stub til #7). Underviser/Admin.</summary>
    [HttpPost("{id:guid}/memberships")]
    public async Task<ActionResult<object>> AddMembership(Guid id, [FromBody] AddMembershipRequest body, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var hold = await db.Hold.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (hold is null)
            return NotFound();

        if (body.UserSub == Guid.Empty)
            return BadRequest(new { error = "invalid_user_sub" });

        var source = string.IsNullOrWhiteSpace(body.Source) ? "manual" : body.Source.Trim().ToLowerInvariant();
        if (source is not ("manual" or "lectio"))
            return BadRequest(new { error = "invalid_source" });

        var exists = await db.Memberships.AnyAsync(m => m.HoldId == id && m.UserSub == body.UserSub, ct);
        if (exists)
            return Conflict(new { error = "already_member" });

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            HoldId = id,
            UserSub = body.UserSub,
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
            membership.Source,
            membership.CreatedAtUtc
        });
    }
}
