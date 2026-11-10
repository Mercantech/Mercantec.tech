using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/holds/{holdId:guid}/teaching-courses")]
public sealed class TeachingCoursesController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid holdId, [FromQuery] bool publishedOnly, CancellationToken ct)
    {
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var manage = !publishedOnly && await access.CanManageAsync(holdId, User, ct);
        var courses = await Query(holdId).Where(c => manage || c.Published && !c.Archived).ToListAsync(ct);
        var catalog = await Resources(holdId, manage, ct);
        return Ok(new TeachingCourseWorkspace(holdId, manage, courses.OrderBy(c => c.Archived)
            .ThenBy(c => c.StartsOn ?? DateOnly.MaxValue).ThenBy(c => c.Title)
            .Select(c => View(c, catalog)).ToArray()));
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(Guid holdId, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        return Ok(await Resources(holdId, true, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid holdId, SaveTeachingCourse request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        if (!Valid(request)) return BadRequest(new { error = "invalid_teaching_course" });
        var item = new TeachingCourse { Id = Guid.NewGuid(), HoldId = holdId };
        Apply(item, request);
        db.TeachingCourses.Add(item);
        await db.SaveChangesAsync(ct);
        return Ok(View(item, []));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid holdId, Guid id, SaveTeachingCourse request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        var item = await Query(holdId).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (item is null) return NotFound();
        if (!Valid(request)) return BadRequest(new { error = "invalid_teaching_course" });
        if (request.Version != item.Version) return Conflict(new { error = "teaching_course_changed" });
        Apply(item, request); item.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "teaching_course_changed" }); }
        return Ok(View(item, await Resources(holdId, true, ct)));
    }

    [HttpPut("{id:guid}/resources")]
    public async Task<IActionResult> SaveResources(Guid holdId, Guid id, SaveTeachingCourseResources request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        var item = await Query(holdId).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (item is null) return NotFound();
        if (item.Archived) return Conflict(new { error = "teaching_course_archived" });
        if (request.Version != item.Version) return Conflict(new { error = "teaching_course_changed" });
        var catalog = await Resources(holdId, true, ct);
        if (request.Resources is null || request.Resources.Length > 500
            || request.Resources.Any(r => r is null || r.ResourceId == Guid.Empty || r.Kind is not ("page" or "quest" or "material" or "event"))
            || request.Resources.Distinct().Count() != request.Resources.Length
            || request.Resources.Any(r => !catalog.Any(c => c.Kind == r.Kind && c.ResourceId == r.ResourceId)))
            return BadRequest(new { error = "invalid_course_resources" });
        if (request.Resources.Any(r => catalog.Any(c => c.Kind == r.Kind && c.ResourceId == r.ResourceId
            && c.LinkedCourseId is Guid owner && owner != id)))
            return Conflict(new { error = "resource_already_assigned" });
        var wanted = request.Resources.ToHashSet();
        foreach (var old in item.Resources.Where(r => !wanted.Contains(new(r.Kind, r.ResourceId))).ToArray())
        { db.TeachingCourseResources.Remove(old); item.Resources.Remove(old); }
        foreach (var next in wanted.Where(r => !item.Resources.Any(old => old.Kind == r.Kind && old.ResourceId == r.ResourceId)))
            item.Resources.Add(new() { Kind = next.Kind, ResourceId = next.ResourceId });
        item.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "teaching_course_changed" }); }
        catch (DbUpdateException) { return Conflict(new { error = "resource_already_assigned" }); }
        return Ok(View(item, catalog));
    }

    [HttpPost("{id:guid}/pages")]
    public async Task<IActionResult> CreatePage(Guid holdId, Guid id, CreateTeachingCoursePage request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct) || UserClaims.GetSub(User) is not Guid sub) return NotFound();
        var item = await Query(holdId).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (item is null) return NotFound();
        if (item.Archived) return Conflict(new { error = "teaching_course_archived" });
        if (request.Version != item.Version) return Conflict(new { error = "teaching_course_changed" });
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 || request.Title.Any(char.IsControl)
            || request.ParentPageId is Guid parent && !item.Resources.Any(r => r.Kind == "page" && r.ResourceId == parent))
            return BadRequest(new { error = "invalid_course_page" });
        var parentScope = request.ParentPageId is Guid target
            ? await db.NotionPages.Where(p => p.Id == target && p.HoldId == holdId)
                .Select(p => new { p.CollectionId }).SingleOrDefaultAsync(ct)
            : null;
        if (request.ParentPageId is not null && parentScope is null) return BadRequest(new { error = "invalid_course_page" });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Mercantec.Tech.Api.Notebooks.NotebookPageTree.LockTeachingAsync(db, parentScope?.CollectionId, holdId, ct);
        if (request.ParentPageId is Guid targetId && !await db.NotionPages.AnyAsync(p => p.Id == targetId, ct))
            return BadRequest(new { error = "invalid_course_page" });
        var now = DateTimeOffset.UtcNow;
        var page = new NotionPage { Id = Guid.NewGuid(), HoldId = holdId, ParentPageId = request.ParentPageId,
            CollectionId = parentScope?.CollectionId, Title = request.Title.Trim(), CreatedBySub = sub, CreatedAtUtc = now,
            UpdatedAtUtc = now, LastEditedByName = UserClaims.EditorName(User) };
        db.NotionPages.Add(page);
        item.Resources.Add(new() { Kind = "page", ResourceId = page.Id }); item.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "teaching_course_changed" }); }
        await transaction.CommitAsync(ct);
        return Ok(new TeachingCoursePageCreated(page.Id, View(item, await Resources(holdId, true, ct))));
    }

    private IQueryable<TeachingCourse> Query(Guid holdId) => db.TeachingCourses.Include(c => c.Resources).Where(c => c.HoldId == holdId);
    private static bool Valid(SaveTeachingCourse r) => !string.IsNullOrWhiteSpace(r.Title) && r.Title.Length <= 200
        && !r.Title.Any(char.IsControl) && r.Description is not null && r.Description.Length <= 4000
        && r.LearningObjectives is not null && r.LearningObjectives.Length <= 8000
        && !(r.StartsOn is DateOnly start && r.EndsOn is DateOnly end && end < start);
    private static void Apply(TeachingCourse item, SaveTeachingCourse r)
    { item.Title = r.Title.Trim(); item.Description = r.Description.Trim(); item.LearningObjectives = r.LearningObjectives.Trim(); item.StartsOn = r.StartsOn; item.EndsOn = r.EndsOn; item.Published = r.Published; item.Archived = r.Archived; }
    private static TeachingCourseView View(TeachingCourse c, TeachingCourseResourceView[] catalog)
    {
        var keys = c.Resources.Select(r => (r.Kind, r.ResourceId)).ToHashSet();
        return new(c.Id, c.HoldId, c.Title, c.Description, c.LearningObjectives, c.StartsOn, c.EndsOn,
            c.Published, c.Archived, c.Version, catalog.Where(r => keys.Contains((r.Kind, r.ResourceId))).ToArray());
    }
    private async Task<TeachingCourseResourceView[]> Resources(Guid holdId, bool manage, CancellationToken ct)
    {
        var links = await db.TeachingCourseResources.AsNoTracking().Where(r => r.Course.HoldId == holdId).ToListAsync(ct);
        Guid? Owner(string kind, Guid id) => links.FirstOrDefault(r => r.Kind == kind && r.ResourceId == id)?.CourseId;
        var pages = await db.NotionPages.AsNoTracking().Where(p => p.HoldId == holdId && (manage || p.PublishedAtUtc != null)).ToListAsync(ct);
        var quests = await db.LearningQuests.AsNoTracking().Where(q => q.HoldId == holdId && (manage || q.Published)).ToListAsync(ct);
        if (!manage && UserClaims.GetSub(User) is Guid sub && !await access.CanManageAsync(holdId, User, ct))
        {
            var readableIds = await new QuestAudienceAccess(db).Readable(db.LearningQuests.Where(q => q.HoldId == holdId), sub).Select(q => q.Id).ToArrayAsync(ct);
            quests = quests.Where(q => readableIds.Contains(q.Id)).ToList();
        }
        var materials = await db.CourseMaterials.AsNoTracking().Where(m => m.HoldId == holdId && (manage || m.Published)).ToListAsync(ct);
        var events = await db.HoldCalendarEvents.AsNoTracking().Where(e => e.HoldId == holdId).ToListAsync(ct);
        return pages.Select(p => new TeachingCourseResourceView(p.Id, "page", manage ? p.Title : p.PublishedTitle ?? p.Title,
                p.PublishedAtUtc != null, p.ParentPageId, LinkedCourseId: Owner("page", p.Id)))
            .Concat(quests.Select(q => new TeachingCourseResourceView(q.Id, "quest", q.Title, q.Published, LinkedCourseId: Owner("quest", q.Id))))
            .Concat(materials.Select(m => new TeachingCourseResourceView(m.Id, "material", m.Title, m.Published, FileAssetId: m.FileAssetId, LinkedCourseId: Owner("material", m.Id))))
            .Concat(events.Select(e => new TeachingCourseResourceView(e.Id, "event", e.Title, true, StartsAtUtc: e.StartUtc, EndsAtUtc: e.EndUtc, LinkedCourseId: Owner("event", e.Id))))
            .OrderBy(r => r.Kind).ThenBy(r => r.Title).ToArray();
    }
}
