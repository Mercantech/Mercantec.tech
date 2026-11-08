using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/notion/collections")]
public sealed class NotionCollectionsController(AppDbContext db, CourseAccess courses) : ControllerBase
{
    private readonly NotionAccess access = new(db, courses);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, bool publishedOnly = false)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        var manageable = await courses.AccessibleHolds(User, true).Select(h => h.Id).ToArrayAsync(ct);
        var teacher = !publishedOnly && UserClaims.IsTeacher(User);
        var pages = await access.ReadablePages(User, publishedOnly).AsNoTracking().Where(p => p.CollectionId != null)
            .Select(p => new { p.Id, p.CollectionId, p.HoldId, p.UpdatedAtUtc, p.PublishedAtUtc }).ToArrayAsync(ct);
        var collectionIds = pages.Select(p => p.CollectionId!.Value).Distinct().ToArray();
        var roots = await db.NotionPages.AsNoTracking().Where(p => collectionIds.Contains(p.Id))
            .Select(p => new { p.Id, p.HoldId, DraftTitle = !publishedOnly && (p.HoldId == null && teacher
                || p.HoldId != null && manageable.Contains(p.HoldId.Value)) ? p.Title : null,
                p.PublishedTitle, p.PublishedAtUtc, HasPublication = p.PublishedState != null }).ToDictionaryAsync(p => p.Id, ct);
        var holdIds = pages.Where(p => p.HoldId != null).Select(p => p.HoldId!.Value).Distinct().ToArray();
        var names = await db.Hold.AsNoTracking().Where(h => holdIds.Contains(h.Id)).ToDictionaryAsync(h => h.Id, h => h.Name, ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(pages.GroupBy(p => p.CollectionId!.Value).Select(group =>
        {
            var first = group.OrderBy(p => p.Id == group.Key ? 0 : 1).ThenBy(p => p.Id).First();
            var manage = !publishedOnly && (first.HoldId is Guid hold ? manageable.Contains(hold) : teacher);
            var root = roots.GetValueOrDefault(group.Key);
            var title = manage ? root?.DraftTitle : root is { PublishedAtUtc: not null, HasPublication: true } ? root.PublishedTitle : null;
            return new NotionCollectionView(group.Key, first.Id, title ?? "Samling", first.HoldId,
                first.HoldId is Guid id ? names.GetValueOrDefault(id) : null, manage,
                group.Max(p => manage ? p.UpdatedAtUtc : p.PublishedAtUtc!.Value));
        }).OrderBy(p => p.Title).ToArray());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateNotionCollection request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanManageScopeAsync(request.HoldId, User, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200 || request.Title.Any(char.IsControl))
            return BadRequest(new { error = "invalid_title" });
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var collection = new NotionCollection { Id = id, HoldId = request.HoldId, CreatedBySub = sub, CreatedAtUtc = now };
        var page = new NotionPage { Id = id, Collection = collection, HoldId = request.HoldId,
            Title = request.Title.Trim(), CreatedBySub = sub, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.NotionCollections.Add(collection); db.NotionPages.Add(page);
        await db.SaveChangesAsync(ct);
        var holdName = request.HoldId is Guid hold ? await db.Hold.Where(h => h.Id == hold).Select(h => h.Name).SingleAsync(ct) : null;
        Response.Headers.CacheControl = "no-store";
        return Ok(new NotionCollectionView(id, id, page.Title, page.HoldId, holdName, true, now));
    }
}
