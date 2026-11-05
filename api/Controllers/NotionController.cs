using Mercantec.Tech.Api.Auth;
using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

/// <summary>Hold-scoped authoring and immutable published reading. Only managers join draft collaboration.</summary>
[ApiController, Authorize, Route("api/notion")]
public sealed class NotionController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet("holds")]
    public async Task<IActionResult> Holds(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid) return Unauthorized();
        var manageable = await access.AccessibleHolds(User, true).Select(h => h.Id).ToListAsync(ct);
        return Ok(await access.AccessibleHolds(User).AsNoTracking().OrderBy(h => h.Name)
            .Select(h => new NotionHold(h.Id, h.Name, manageable.Contains(h.Id))).ToListAsync(ct));
    }

    [HttpGet("pages")]
    public async Task<IActionResult> List(Guid holdId, CancellationToken ct, bool publishedOnly = false)
    {
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var manage = !publishedOnly && await access.CanManageAsync(holdId, User, ct);
        // Metadata queries deliberately never materialize the potentially large Yjs binary.
        var pages = await db.NotionPages.AsNoTracking().Where(p => p.HoldId == holdId
                && (manage || p.PublishedAtUtc != null && p.PublishedState != null && p.PublishedTitle != null))
            .Select(p => new { p.Id, p.HoldId, p.Title, p.ParentPageId, p.UpdatedAtUtc, p.PublishedTitle,
                p.PublishedAtUtc, p.DraftRevision, p.PublishedRevision }).ToListAsync(ct);
        var visible = pages.Select(p => p.Id).ToHashSet();
        Response.Headers.CacheControl = "no-store";
        return Ok(pages.Select(p => new NotionPageSummary(p.Id, p.HoldId, manage ? p.Title : p.PublishedTitle!,
            manage || p.ParentPageId is not Guid parent || visible.Contains(parent) ? p.ParentPageId : null,
            manage ? p.UpdatedAtUtc : p.PublishedAtUtc!.Value, p.PublishedAtUtc,
            manage && p.PublishedAtUtc is not null && (p.DraftRevision != p.PublishedRevision || p.Title != p.PublishedTitle)))
            .OrderBy(p => p.Title).ToList());
    }

    [HttpPost("pages")]
    public async Task<IActionResult> Create(CreateNotionPage request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await access.CanManageAsync(request.HoldId, User, ct)) return NotFound();
        if (!ValidTitle(request.Title)) return BadRequest(new { error = "invalid_title" });
        if (request.ParentPageId is Guid parent && !await db.NotionPages
            .AnyAsync(p => p.Id == parent && p.HoldId == request.HoldId, ct))
            return BadRequest(new { error = "invalid_parent" });
        var page = new NotionPage
        {
            Id = Guid.NewGuid(), HoldId = request.HoldId, ParentPageId = request.ParentPageId,
            Title = request.Title.Trim(), CreatedBySub = sub,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        db.NotionPages.Add(page);
        await db.SaveChangesAsync(ct);
        return Ok(View(page));
    }

    [HttpPatch("pages/{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameNotionPage request, CancellationToken ct)
    {
        var page = await FindEditable(id, ct);
        if (page is null) return NotFound();
        if (!ValidTitle(request.Title)) return BadRequest(new { error = "invalid_title" });
        var title = request.Title.Trim();
        await db.NotionPages.Where(p => p.Id == id && p.Title != title).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Title, title).SetProperty(p => p.UpdatedAtUtc, DateTimeOffset.UtcNow)
            .SetProperty(p => p.DraftRevision, p => p.DraftRevision + 1), ct);
        return Ok(View(await db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == id, ct)));
    }

    [HttpGet("pages/{id:guid}/context")]
    public Task<IActionResult> Context(Guid id, CancellationToken ct) => ReadContext(id, false, ct);

    [HttpGet("pages/{id:guid}/published-context")]
    public Task<IActionResult> PublishedContext(Guid id, CancellationToken ct) => ReadContext(id, true, ct);

    private async Task<IActionResult> ReadContext(Guid id, bool publishedOnly, CancellationToken ct)
    {
        var page = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.Id, p.HoldId, p.Title, p.UpdatedAtUtc, p.PublishedAtUtc, p.PublishedTitle,
                p.DraftRevision, p.PublishedRevision, p.PublishedState }).SingleOrDefaultAsync(ct);
        if (page is null || !await access.CanAccessAsync(page.HoldId, User, ct)) return NotFound();
        var edit = !publishedOnly && await access.CanManageAsync(page.HoldId, User, ct);
        if (!edit && (page.PublishedAtUtc is null || page.PublishedTitle is null || page.PublishedState is not { Length: > 0 })) return NotFound();
        var members = await db.Memberships.AsNoTracking().Where(m => m.HoldId == page.HoldId)
            .OrderBy(m => m.DisplayName).Select(m => new NotionMember(m.Id, m.DisplayName ?? "Elev"))
            .ToArrayAsync(ct);
        var polls = await db.PollRounds.AsNoTracking().Where(p => p.HoldId == page.HoldId && (edit || p.State != "draft"))
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new NotionPollReference(p.Id, p.Question, p.State)).Take(100).ToArrayAsync(ct);
        var links = await db.NotionPages.AsNoTracking().Where(p => p.HoldId == page.HoldId
            && (edit || p.PublishedAtUtc != null && p.PublishedState != null && p.PublishedTitle != null))
            .Select(p => new { p.Id, p.Title, p.PublishedTitle, p.ParentPageId }).ToListAsync(ct);
        var visible = links.Select(p => p.Id).ToHashSet();
        var pages = links.Select(p => new NotionPageLink(p.Id, edit ? p.Title : p.PublishedTitle!,
            edit || p.ParentPageId is not Guid parent || visible.Contains(parent) ? p.ParentPageId : null)).ToArray();
        // Published metadata and bytes come from the same row read; a concurrent re-publication
        // cannot combine an old title with a new document body.
        var state = edit ? null : page.PublishedState;
        Response.Headers.CacheControl = "no-store";
        return Ok(new NotionPageContext(page.Id, page.HoldId, edit ? page.Title : page.PublishedTitle!, edit, members, polls,
            edit ? page.UpdatedAtUtc : page.PublishedAtUtc!.Value, pages, page.PublishedAtUtc,
            edit ? page.DraftRevision : page.PublishedRevision ?? 0, page.PublishedRevision,
            state is null ? null : Convert.ToBase64String(state), page.PublishedTitle));
    }

    [HttpPost("pages/{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, PublishNotionPage request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var page = await FindEditable(id, ct);
        if (page is null) return NotFound();
        if (request.ExpectedRevision < 0 || !NotionStorageController.IsFingerprint(request.ExpectedFingerprint)
            || request.AssetIds is null || request.AssetIds.Length > 1000 || request.AssetIds.Any(id => id == Guid.Empty)
            || request.AssetIds.Distinct().Count() != request.AssetIds.Length)
            return BadRequest(new { error = "invalid_publication" });
        if (page.YjsState is not { Length: > 0 } || page.DraftFingerprint is null)
            return Conflict(new { error = "document_not_saved" });
        if (page.DraftRevision != request.ExpectedRevision || page.DraftFingerprint != request.ExpectedFingerprint)
            return Conflict(new { error = "draft_changed" });
        if (request.AssetIds.Length > 0 && await db.NotionAssets.CountAsync(a => a.PageId == id
            && request.AssetIds.Contains(a.Id), ct) != request.AssetIds.Length)
            return BadRequest(new { error = "invalid_assets" });
        var now = DateTimeOffset.UtcNow;
        var assets = JsonSerializer.Serialize(request.AssetIds);
        // The snapshot and its file grants change in one conditional SQL update. New draft saves,
        // title changes or a concurrent withdrawal invalidate this publication attempt.
        var written = await db.NotionPages.Where(p => p.Id == id && p.DraftRevision == request.ExpectedRevision
            && p.DraftFingerprint == request.ExpectedFingerprint && p.PublicationVersion == page.PublicationVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedState, page.YjsState)
                .SetProperty(p => p.PublishedTitle, page.Title).SetProperty(p => p.PublishedAtUtc, now)
                .SetProperty(p => p.PublishedBySub, sub).SetProperty(p => p.PublishedRevision, request.ExpectedRevision)
                .SetProperty(p => p.PublishedAssetIdsJson, assets).SetProperty(p => p.PublicationVersion, p => p.PublicationVersion + 1), ct);
        if (written == 0) return Conflict(new { error = "draft_changed" });
        Response.Headers.CacheControl = "no-store";
        return Ok(View(await db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == id, ct)));
    }

    [HttpPost("pages/{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        var page = await FindEditable(id, ct);
        if (page is null) return NotFound();
        await db.NotionPages.Where(p => p.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.PublishedState, (byte[]?)null).SetProperty(p => p.PublishedTitle, (string?)null)
            .SetProperty(p => p.PublishedAtUtc, (DateTimeOffset?)null).SetProperty(p => p.PublishedBySub, (Guid?)null)
            .SetProperty(p => p.PublishedRevision, (long?)null).SetProperty(p => p.PublishedAssetIdsJson, "[]")
            .SetProperty(p => p.PublicationVersion, p => p.PublicationVersion + 1), ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(View(await db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == id, ct)));
    }

    [HttpPost("collaboration/authorize")]
    public async Task<IActionResult> AuthorizeDocument(AuthorizeNotionCollaboration request, CancellationToken ct)
    {
        if (!TryDocumentId(request.DocumentName, out var id))
            return BadRequest(new { error = "invalid_document" });
        var hold = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.HoldId }).SingleOrDefaultAsync(ct);
        if (hold is null || !await access.CanManageAsync(hold.HoldId, User, ct)) return NotFound();
        return Ok(new NotionCollaborationAccess(id, true));
    }

    public static bool TryDocumentId(string? name, out Guid id)
    {
        id = Guid.Empty;
        // Only canonical document names become WebSocket rooms or storage keys.
        return name is not null && name.Length == 43 && name.StartsWith("notion:", StringComparison.Ordinal)
            && Guid.TryParseExact(name.AsSpan(7), "D", out id) && id != Guid.Empty
            && name.AsSpan(7).SequenceEqual(id.ToString("D").AsSpan());
    }

    private async Task<NotionPage?> FindEditable(Guid id, CancellationToken ct)
    {
        var page = await db.NotionPages.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        return page is not null && await access.CanManageAsync(page.HoldId, User, ct) ? page : null;
    }

    private static bool ValidTitle(string? title) => !string.IsNullOrWhiteSpace(title)
        && title.Length <= 200 && !title.Any(char.IsControl);
    private static NotionPageSummary View(NotionPage p) => new(p.Id, p.HoldId, p.Title, p.ParentPageId, p.UpdatedAtUtc,
        p.PublishedAtUtc, p.PublishedAtUtc is not null && (p.DraftRevision != p.PublishedRevision || p.Title != p.PublishedTitle));
}
