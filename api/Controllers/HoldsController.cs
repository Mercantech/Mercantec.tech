using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/holds")]
[Authorize]
public class HoldsController(AppDbContext db) : ControllerBase
{
    private readonly CourseAccess access = new(db);

    [HttpGet("mine"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Mine([FromQuery] Guid? asUser, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty)
            return Unauthorized();

        var teacher = UserClaims.IsTeacher(User);
        if (asUser.HasValue && !teacher)
            return Forbid();
        if (asUser == Guid.Empty)
            return BadRequest(new { error = "invalid_user" });

        var canManage = teacher && !asUser.HasValue;
        var holds = access.AccessibleHolds(User, manageOnly: teacher).AsNoTracking();
        if (asUser is Guid pupil)
            holds = holds.Where(h => h.Memberships.Any(member => member.UserSub == pupil));

        var cards = await holds.OrderBy(h => h.Name).ThenBy(h => h.Id)
            .Select(h => new
            {
                h.Id,
                h.ForloebId,
                HoldName = h.Name,
                ForloebName = h.Forloeb.Name,
                h.Forloeb.Description,
                h.CreatedAtUtc,
                MemberCount = canManage ? h.Memberships.Count : 0,
                UnlinkedMemberCount = canManage ? h.Memberships.Count(member => member.UserSub == null) : 0,
                PrimaryLectioKey = h.LectioHoldKey,
                Sources = h.LectioSources.OrderBy(source => source.LectioHoldKey)
                    .Select(source => new HoldLectioBadge(source.LectioHoldKey, source.LectioHoldName)).ToArray(),
                IsFavorite = !asUser.HasValue && db.HoldFavorites.Any(f => f.UserSub == subject && f.HoldId == h.Id)
            })
            .ToArrayAsync(ct);

        return Ok(cards.Select(card =>
        {
            var sources = card.Sources;
            if (card.PrimaryLectioKey is { Length: > 0 } primary &&
                !sources.Any(source => string.Equals(source.Key, primary, StringComparison.OrdinalIgnoreCase)))
                sources = [new HoldLectioBadge(primary, null), .. sources];

            return new MyHoldCard(card.Id, card.ForloebId, card.HoldName, card.ForloebName,
                card.Description, card.CreatedAtUtc, card.MemberCount, card.UnlinkedMemberCount,
                sources, canManage, card.IsFavorite);
        }).ToArray());
    }

    [HttpPatch("{id:guid}/name")]
    public async Task<IActionResult> Rename(Guid id, RenameHold input, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty)
            return Unauthorized();

        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.Any(char.IsControl))
            return BadRequest(new { error = "invalid_name" });

        var updated = await access.AccessibleHolds(User, manageOnly: true).Where(h => h.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(h => h.Name, name), ct);
        return updated == 0 ? NotFound() : NoContent();
    }

    [HttpPut("{id:guid}/favorite")]
    public async Task<IActionResult> AddFavorite(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty)
            return Unauthorized();
        if (!await access.CanAccessAsync(id, User, ct))
            return NotFound();

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO hold_favorites (\"UserSub\", \"HoldId\") VALUES ({subject}, {id}) ON CONFLICT (\"UserSub\", \"HoldId\") DO NOTHING", ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/favorite")]
    public async Task<IActionResult> RemoveFavorite(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty)
            return Unauthorized();
        if (!await access.CanAccessAsync(id, User, ct))
            return NotFound();

        await db.HoldFavorites.Where(f => f.UserSub == subject && f.HoldId == id).ExecuteDeleteAsync(ct);
        return NoContent();
    }

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
