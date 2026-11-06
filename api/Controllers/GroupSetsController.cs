using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/holds/{holdId:guid}/group-sets")]
public sealed class GroupSetsController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid holdId, bool includeArchived, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var manage = await access.CanManageAsync(holdId, User, ct);
        var sets = await db.LearningGroupSets.AsNoTracking().Where(s => s.HoldId == holdId
            && (includeArchived || !s.Archived)
            && (manage || db.LearningGroups.Any(g => g.GroupSetId == s.Id && g.Members.Any(m => m.Membership.UserSub == sub))))
            .OrderBy(s => s.Name).ToArrayAsync(ct);
        var groups = await db.LearningGroups.AsNoTracking().Where(g => g.HoldId == holdId
            && (manage || g.Members.Any(m => m.Membership.UserSub == sub)))
            .Include(g => g.Members).ToArrayAsync(ct);
        return Ok(sets.Select(s => View(s, groups)).ToArray());
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid holdId, SaveGroupSet request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        if (!Valid(request)) return BadRequest(new { error = "invalid_group_set" });
        var set = new LearningGroupSet { Id = Guid.NewGuid(), HoldId = holdId, Name = request.Name.Trim(), Archived = request.Archived };
        db.LearningGroupSets.Add(set);
        await db.SaveChangesAsync(ct);
        return Ok(View(set, []));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid holdId, Guid id, SaveGroupSet request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        var set = await db.LearningGroupSets.SingleOrDefaultAsync(s => s.Id == id && s.HoldId == holdId, ct);
        if (set is null) return NotFound();
        if (!Valid(request)) return BadRequest(new { error = "invalid_group_set" });
        if (request.Version != set.Version) return Conflict(new { error = "group_set_changed" });
        set.Name = request.Name.Trim(); set.Archived = request.Archived; set.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "group_set_changed" }); }
        var groups = await db.LearningGroups.AsNoTracking().Where(g => g.GroupSetId == id).Include(g => g.Members).ToArrayAsync(ct);
        return Ok(View(set, groups));
    }

    private static bool Valid(SaveGroupSet request) => !string.IsNullOrWhiteSpace(request.Name)
        && request.Name.Length <= 120 && !request.Name.Any(char.IsControl);
    private static GroupSetView View(LearningGroupSet set, LearningGroup[] groups)
    {
        var items = groups.Where(g => g.GroupSetId == set.Id).ToArray();
        return new(set.Id, set.HoldId, set.Name, set.Archived, set.Version, items.Length,
            items.SelectMany(g => g.Members.Select(m => m.MembershipId)).Distinct().Count());
    }
}
