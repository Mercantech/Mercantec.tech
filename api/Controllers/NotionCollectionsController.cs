using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Emojis;
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

    [HttpPatch("{id:guid}/icon")]
    public async Task<IActionResult> SetIcon(Guid id, SetNotebookIcon input, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        var collection = await db.NotionCollections.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (collection is null || !await access.CanManageScopeAsync(collection.HoldId, User, ct)) return NotFound();
        if (!await NotebookIcon.IsAvailableAsync(db, input.IconEmojiId, ct))
            return BadRequest(new { error = "invalid_icon" });
        var updated = await db.NotionCollections.Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IconEmojiId, input.IconEmojiId), ct);
        if (updated == 0) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, bool publishedOnly = false)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await ReadCollections(null, publishedOnly, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct, bool publishedOnly = false)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        var collection = (await ReadCollections(id, publishedOnly, ct)).SingleOrDefault();
        Response.Headers.CacheControl = "private, no-store";
        return collection is null ? NotFound() : Ok(collection);
    }

    private async Task<NotionCollectionView[]> ReadCollections(Guid? id, bool publishedOnly, CancellationToken ct)
    {
        var manageable = await courses.AccessibleHolds(User, true).Select(h => h.Id).ToArrayAsync(ct);
        var teacher = !publishedOnly && UserClaims.IsTeacher(User);
        var pageQuery = access.ReadablePages(User, publishedOnly).AsNoTracking()
            .Where(p => p.CollectionId != null && (id == null || p.CollectionId == id));
        var collections = await db.NotionCollections.AsNoTracking().Where(c => (id == null || c.Id == id)
                && (pageQuery.Any(p => p.CollectionId == c.Id)
                    || !publishedOnly && (c.HoldId == null ? teacher : manageable.Contains(c.HoldId.Value))))
            .Select(c => new { c.Id, c.HoldId, c.CreatedAtUtc, c.Title, c.PublishedTitle, c.IconEmojiId,
                HasActiveRoot = db.NotionPages.Any(p => p.Id == c.Id),
                HoldName = db.Hold.Where(h => h.Id == c.HoldId).Select(h => h.Name).FirstOrDefault() }).ToArrayAsync(ct);
        var pages = await pageQuery.Select(p => new { p.Id, p.CollectionId, p.ParentPageId, p.SortOrder, p.Title, p.PublishedTitle,
            p.UpdatedAtUtc, p.PublishedAtUtc, p.LastEditedByName, p.PublishedEditedAtUtc, p.PublishedEditorName }).ToArrayAsync(ct);
        var byCollection = pages.ToLookup(p => p.CollectionId);
        return collections.Select(c =>
        {
            var group = byCollection[c.Id];
            var manage = !publishedOnly && (c.HoldId is Guid hold ? manageable.Contains(hold) : teacher);
            var root = group.FirstOrDefault(p => p.Id == c.Id);
            var first = root ?? group.OrderBy(p => p.ParentPageId is not null).ThenBy(p => p.SortOrder ?? int.MaxValue)
                .ThenBy(p => manage ? p.Title : p.PublishedTitle).ThenBy(p => p.Id).FirstOrDefault();
            var title = manage ? root?.Title ?? c.Title : root?.PublishedTitle ?? (c.HasActiveRoot ? null : c.PublishedTitle);
            var latest = group.OrderByDescending(p => manage ? p.UpdatedAtUtc : p.PublishedEditedAtUtc ?? p.PublishedAtUtc)
                .ThenBy(p => p.Id).FirstOrDefault();
            var updated = manage ? latest?.UpdatedAtUtc : latest?.PublishedEditedAtUtc ?? latest?.PublishedAtUtc;
            return new NotionCollectionView(c.Id, first?.Id, title ?? "Samling", c.HoldId,
                c.HoldName, manage, updated ?? c.CreatedAtUtc, c.IconEmojiId,
                manage ? latest?.LastEditedByName : latest?.PublishedEditorName);
        }).Where(p => p.CanManage || p.RootPageId is not null).OrderBy(p => p.Title).ToArray();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateNotionCollection request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanManageScopeAsync(request.HoldId, User, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200 || request.Title.Any(char.IsControl))
            return BadRequest(new { error = "invalid_title" });
        if (!await NotebookIcon.IsAvailableAsync(db, request.IconEmojiId, ct)) return BadRequest(new { error = "invalid_icon" });
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var title = request.Title.Trim();
        var collection = new NotionCollection { Id = id, HoldId = request.HoldId, CreatedBySub = sub, CreatedAtUtc = now,
            Title = title, IconEmojiId = request.IconEmojiId };
        var page = new NotionPage { Id = id, Collection = collection, HoldId = request.HoldId,
            Title = title, CreatedBySub = sub, CreatedAtUtc = now, UpdatedAtUtc = now, LastEditedByName = UserClaims.EditorName(User) };
        db.NotionCollections.Add(collection); db.NotionPages.Add(page);
        await db.SaveChangesAsync(ct);
        var holdName = request.HoldId is Guid hold ? await db.Hold.Where(h => h.Id == hold).Select(h => h.Name).SingleAsync(ct) : null;
        Response.Headers.CacheControl = "no-store";
        return Ok(new NotionCollectionView(id, id, page.Title, page.HoldId, holdName, true, now,
            collection.IconEmojiId, page.LastEditedByName));
    }
}
