using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/notion/holds/{holdId:guid}")]
public sealed class NotionGroupsController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet("members")]
    public async Task<IActionResult> Members(Guid holdId, CancellationToken ct)
    {
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var groups = await AccessibleGroups(holdId, ct);
        var members = await db.Memberships.AsNoTracking().Where(m => m.HoldId == holdId)
            .OrderBy(m => m.DisplayName).Select(m => new { m.Id, m.DisplayName }).ToArrayAsync(ct);
        return Ok(members.Select(m => new NotionHoldMember(m.Id, m.DisplayName ?? "Elev",
            groups.Where(g => g.Members.Any(x => x.Id == m.Id)).Select(g => g.Id).ToArray())).ToArray());
    }

    [HttpGet("members/{membershipId:guid}")]
    public async Task<IActionResult> Member(Guid holdId, Guid membershipId, CancellationToken ct)
    {
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var member = await db.Memberships.AsNoTracking().Where(m => m.Id == membershipId && m.HoldId == holdId)
            .Select(m => new { m.Id, m.DisplayName }).SingleOrDefaultAsync(ct);
        if (member is null) return NotFound();
        var groups = await AccessibleGroups(holdId, ct);
        return Ok(new NotionStudentProfile(member.Id, holdId, member.DisplayName ?? "Elev",
            groups.Where(g => g.Members.Any(m => m.Id == member.Id)).ToArray()));
    }

    [HttpGet("groups")]
    public async Task<IActionResult> Groups(Guid holdId, CancellationToken ct)
    {
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        return Ok(await AccessibleGroups(holdId, ct));
    }

    [HttpPost("groups")]
    public async Task<IActionResult> Create(Guid holdId, SaveNotionGroup request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        var members = await ValidMembers(holdId, request, ct);
        if (members is null) return BadRequest(new { error = "invalid_group" });
        var group = new LearningGroup { Id = Guid.NewGuid(), HoldId = holdId, Name = request.Name.Trim() };
        foreach (var member in members) group.Members.Add(new() { Membership = member, MembershipId = member.Id });
        db.LearningGroups.Add(group);
        await db.SaveChangesAsync(ct);
        return Ok(View(group));
    }

    [HttpPut("groups/{groupId:guid}")]
    public async Task<IActionResult> Update(Guid holdId, Guid groupId, SaveNotionGroup request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        var group = await Query(holdId).SingleOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return NotFound();
        var members = await ValidMembers(holdId, request, ct);
        if (members is null) return BadRequest(new { error = "invalid_group" });
        if (group.Version != request.Version) return Conflict(new { error = "group_changed" });
        group.Name = request.Name.Trim(); group.Version++;
        var wanted = members.Select(m => m.Id).ToHashSet();
        foreach (var old in group.Members.Where(m => !wanted.Contains(m.MembershipId)).ToArray()) db.LearningGroupMembers.Remove(old);
        foreach (var member in members.Where(m => !group.Members.Any(x => x.MembershipId == m.Id)))
            group.Members.Add(new() { Membership = member, MembershipId = member.Id });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "group_changed" }); }
        return Ok(View(await Query(holdId).AsNoTracking().SingleAsync(g => g.Id == groupId, ct)));
    }

    private async Task<NotionGroupView[]> AccessibleGroups(Guid holdId, CancellationToken ct)
    {
        var sub = UserClaims.GetSub(User);
        var manage = await access.CanManageAsync(holdId, User, ct);
        var groups = await Query(holdId).AsNoTracking()
            .Where(g => manage || g.Members.Any(m => m.Membership.UserSub == sub)).ToListAsync(ct);
        return groups.OrderBy(g => g.Name).Select(View).ToArray();
    }

    private IQueryable<LearningGroup> Query(Guid holdId) => db.LearningGroups.Where(g => g.HoldId == holdId)
        .Include(g => g.Members).ThenInclude(m => m.Membership);
    private async Task<List<Membership>?> ValidMembers(Guid holdId, SaveNotionGroup request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 || request.Name.Any(char.IsControl)
            || request.MembershipIds is null || request.MembershipIds.Length is < 1 or > 250
            || request.MembershipIds.Distinct().Count() != request.MembershipIds.Length) return null;
        var members = await db.Memberships.Where(m => m.HoldId == holdId && request.MembershipIds.Contains(m.Id)).ToListAsync(ct);
        return members.Count == request.MembershipIds.Length ? members : null;
    }
    private static NotionGroupView View(LearningGroup group) => new(group.Id, group.HoldId, group.Name, group.Version,
        group.Members.Where(m => m.Membership is not null).OrderBy(m => m.Membership.DisplayName)
            .Select(m => new NotionMember(m.MembershipId, m.Membership.DisplayName ?? "Elev")).ToArray());
}
