using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/hold-workspace")]
public sealed class HoldWorkspaceController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet("holds/{holdId:guid}")]
    public async Task<IActionResult> ByHold(Guid holdId, [FromQuery] bool publishedOnly, CancellationToken ct)
    {
        var courseId = await access.AccessibleHolds(User).Where(h => h.Id == holdId)
            .Select(h => (Guid?)h.ForloebId).SingleOrDefaultAsync(ct);
        return courseId is Guid id ? await Get(id, publishedOnly, ct) : NotFound();
    }

    [HttpGet("{courseId:guid}")]
    public async Task<IActionResult> Get(Guid courseId, [FromQuery] bool publishedOnly, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var hold = await access.AccessibleHolds(User).AsNoTracking().Include(h => h.Forloeb)
            .Include(h => h.LectioSources).SingleOrDefaultAsync(h => h.ForloebId == courseId, ct);
        if (hold is null) return NotFound();
        var manage = !publishedOnly && await access.CanManageAsync(hold.Id, User, ct);
        var pages = await db.NotionPages.AsNoTracking()
            .Where(p => p.HoldId == hold.Id && (manage || p.PublishedAtUtc != null)).ToListAsync(ct);
        var quests = await db.LearningQuests.AsNoTracking()
            .Where(q => q.HoldId == hold.Id && (manage || q.Published)).ToListAsync(ct);
        var reviews = manage ? await db.QuestSubmissions.AsNoTracking()
            .Where(s => s.Quest.HoldId == hold.Id && s.State == "submitted")
            .Select(s => new { s.QuestId, s.Quest.Title }).ToListAsync(ct) : [];
        var now = DateTimeOffset.UtcNow;
        // Sorting in memory also keeps the read model portable to SQLite tests.
        var events = await db.HoldCalendarEvents.AsNoTracking().Where(e => e.HoldId == hold.Id).ToListAsync(ct);
        var agenda = events.Where(e => e.EndUtc >= now)
            .Select(e => new WorkspaceAgendaItem(e.Id, e.Title, e.StartUtc, "event", e.Location))
            .Concat(quests.Where(q => q.Published && q.DeadlineUtc >= now)
                .Select(q => new WorkspaceAgendaItem(q.Id, q.Title, q.DeadlineUtc!.Value, "deadline", null)))
            .OrderBy(e => e.StartUtc).Take(6).ToArray();
        var groups = db.LearningGroups.Where(g => g.HoldId == hold.Id
            && (manage || g.Members.Any(m => m.Membership.UserSub == sub)));
        return Ok(new HoldWorkspaceView(courseId, hold.Id, hold.Name, hold.Forloeb.Description, manage,
            await db.Memberships.CountAsync(m => m.HoldId == hold.Id, ct), await groups.CountAsync(ct),
            await db.CourseMaterials.CountAsync(m => m.HoldId == hold.Id && (manage || m.Published), ct),
            reviews.Count, quests.Count, quests.Count(q => !q.Published),
            pages.OrderByDescending(p => p.UpdatedAtUtc).Select(p => new WorkspacePage(p.Id,
                manage ? p.Title : p.PublishedTitle ?? p.Title, p.UpdatedAtUtc, p.PublishedAtUtc,
                manage && p.PublishedAtUtc != null && p.PublishedRevision != p.DraftRevision)).ToArray(), agenda,
            reviews.GroupBy(r => r.QuestId).Select(g => new WorkspaceReview(g.Key, g.First().Title, g.Count()))
                .OrderByDescending(r => r.Count).ToArray(),
            hold.LectioSources.Select(s => s.LectioHoldKey)
                .Concat(string.IsNullOrWhiteSpace(hold.LectioHoldKey) ? [] : new[] { hold.LectioHoldKey! })
                .Distinct().ToArray()));
    }
}
