using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/groups")]
public sealed class GroupsController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid holdId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var manage = await access.CanManageAsync(holdId, User, ct);
        var groups = await Query().Where(g => g.HoldId == holdId
            && (manage || g.Members.Any(m => m.Membership.UserSub == sub))).ToListAsync(ct);
        return Ok(groups.OrderBy(g => g.Name).Select(View).ToArray());
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveLearningGroup request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(request.HoldId, User, ct)) return NotFound();
        var members = await ValidMembers(request, ct);
        if (members is null) return BadRequest(new { error = "invalid_group" });
        var group = new LearningGroup { Id = Guid.NewGuid(), HoldId = request.HoldId, Name = request.Name.Trim() };
        foreach (var member in members)
            group.Members.Add(new() { MembershipId = member.Id, Membership = member });
        db.LearningGroups.Add(group);
        await db.SaveChangesAsync(ct);
        return Ok(View(group));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveLearningGroup request, CancellationToken ct)
    {
        var group = await Query().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group is null || !await access.CanManageAsync(group.HoldId, User, ct)) return NotFound();
        if (request.HoldId != group.HoldId) return BadRequest(new { error = "invalid_group" });
        var members = await ValidMembers(request, ct);
        if (members is null) return BadRequest(new { error = "invalid_group" });
        if (group.Version != request.Version) return Conflict(new { error = "group_changed" });
        group.Name = request.Name.Trim();
        group.Version++;
        var wanted = members.Select(m => m.Id).ToHashSet();
        foreach (var old in group.Members.Where(m => !wanted.Contains(m.MembershipId)).ToArray()) db.LearningGroupMembers.Remove(old);
        foreach (var member in members.Where(m => !group.Members.Any(old => old.MembershipId == m.Id)))
            group.Members.Add(new() { MembershipId = member.Id, Membership = member });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "group_changed" }); }
        return Ok(View(await Query().AsNoTracking().SingleAsync(g => g.Id == id, ct)));
    }

    private async Task<List<Membership>?> ValidMembers(SaveLearningGroup r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 120 || r.MembershipIds is null
            || r.MembershipIds.Length is < 1 or > 100 || r.MembershipIds.Distinct().Count() != r.MembershipIds.Length) return null;
        var members = await db.Memberships.Where(m => m.HoldId == r.HoldId && r.MembershipIds.Contains(m.Id)).ToListAsync(ct);
        return members.Count == r.MembershipIds.Length ? members : null;
    }

    private IQueryable<LearningGroup> Query() => db.LearningGroups.Include(g => g.Members).ThenInclude(m => m.Membership);
    private static LearningGroupView View(LearningGroup g) => new(g.Id, g.HoldId, g.Name, g.Version,
        g.Members.Where(m => m.Membership is not null).Select(m => new LearningMember(m.MembershipId,
            m.Membership.UserSub, m.Membership.DisplayName ?? "Elev")).ToArray());
}
