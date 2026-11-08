using Mercantec.Tech.Api.Auth;
using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Drawings;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

/// <summary>Hold-scoped authoring and immutable published reading. Only managers join draft collaboration.</summary>
[ApiController, Authorize, Route("api/notion")]
public sealed class NotionController(AppDbContext db, CourseAccess access, DrawingService? drawings = null) : ControllerBase
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

    [HttpGet("holds/{holdId:guid}/navigation")]
    public async Task<IActionResult> Navigation(Guid holdId, CancellationToken ct, bool publishedOnly = false)
    {
        var hold = await access.AccessibleHolds(User).AsNoTracking().Where(h => h.Id == holdId)
            .Select(h => new { h.Id, h.ForloebId }).SingleOrDefaultAsync(ct);
        if (hold is null) return NotFound();
        var manage = !publishedOnly && await access.CanManageAsync(holdId, User, ct);
        var courses = await db.TeachingCourses.AsNoTracking().Where(c => c.HoldId == holdId
                && !c.Archived && (manage || c.Published))
            .Select(c => c.Id).ToArrayAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new NotionNavigationContext(hold.ForloebId, hold.Id, courses));
    }

    /// <summary>Pages in one sidebar collection and the internal page links between them.</summary>
    [HttpGet("graph/{collectionId:guid}")]
    public async Task<IActionResult> Graph(Guid collectionId, CancellationToken ct, bool publishedOnly = false)
    {
        var root = await db.NotionPages.AsNoTracking().Where(p => p.Id == collectionId)
            .Select(p => new { p.Id, p.HoldId, p.Title, p.PublishedTitle, p.PublishedAtUtc, HasPublication = p.PublishedState != null })
            .SingleOrDefaultAsync(ct);
        if (root is null || !await access.CanAccessAsync(root.HoldId, User, ct)) return NotFound();
        var manage = !publishedOnly && await access.CanManageAsync(root.HoldId, User, ct);
        if (!manage && (root.PublishedAtUtc is null || root.PublishedTitle is null || !root.HasPublication)) return NotFound();

        var meta = await db.NotionPages.AsNoTracking().Where(p => p.HoldId == root.HoldId)
            .Select(p => new PageMeta(p.Id, p.ParentPageId, p.Title, p.PublishedTitle, p.PublishedAtUtc, p.PublishedState != null))
            .ToListAsync(ct);
        var byParent = meta.ToLookup(p => p.ParentPageId);
        var subtree = new List<Guid>();
        var pending = new Stack<Guid>();
        var seen = new HashSet<Guid>();
        pending.Push(root.Id);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!seen.Add(id)) continue;
            subtree.Add(id);
            foreach (var child in byParent[id]) pending.Push(child.Id);
        }

        var ordered = subtree.Select(id => meta.First(p => p.Id == id)).Where(p => PageVisible(p, manage))
            .OrderBy(p => manage ? p.Title : p.PublishedTitle, StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.Id)
            .ToList();
        if (ordered.Count > 400)
        {
            var rootNode = ordered.First(p => p.Id == root.Id);
            ordered = [rootNode, .. ordered.Where(p => p.Id != root.Id).Take(399)];
        }
        var nodes = ordered.ToArray();
        Response.Headers.CacheControl = "no-store";
        return Ok(new NotionGraph(root.HoldId, root.Id, manage ? root.Title : root.PublishedTitle!, manage,
            ToNodes(nodes, manage), await ReadEdgesAsync(manage, nodes.Select(p => p.Id).ToHashSet(), ct)));
    }

    /// <summary>Every visible page on one hold, including every root, and the internal page links between them.</summary>
    [HttpGet("holds/{holdId:guid}/graph")]
    public async Task<IActionResult> HoldGraph(Guid holdId, CancellationToken ct, bool publishedOnly = false)
    {
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var hold = await db.Hold.AsNoTracking().Where(h => h.Id == holdId)
            .Select(h => new { h.Id, h.Name }).SingleOrDefaultAsync(ct);
        if (hold is null) return NotFound();
        var manage = !publishedOnly && await access.CanManageAsync(holdId, User, ct);
        var meta = await db.NotionPages.AsNoTracking().Where(p => p.HoldId == holdId)
            .Select(p => new PageMeta(p.Id, p.ParentPageId, p.Title, p.PublishedTitle, p.PublishedAtUtc, p.PublishedState != null))
            .ToListAsync(ct);
        var visible = meta.Where(p => PageVisible(p, manage)).ToList();
        var visibleIds = visible.Select(p => p.Id).ToHashSet();
        // Same cap as one collection. Roots stay ahead of descendants so a large hold still shows every collection.
        var nodes = visible
            .OrderBy(p => p.ParentPageId is Guid parent && visibleIds.Contains(parent) ? 1 : 0)
            .ThenBy(p => manage ? p.Title : p.PublishedTitle, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Id)
            .Take(400)
            .ToArray();
        Response.Headers.CacheControl = "no-store";
        return Ok(new NotionGraph(hold.Id, Guid.Empty, hold.Name, manage, ToNodes(nodes, manage),
            await ReadEdgesAsync(manage, nodes.Select(p => p.Id).ToHashSet(), ct), WholeHold: true));
    }

    private async Task<NotionGraphEdge[]> ReadEdgesAsync(bool manage, HashSet<Guid> nodeIds, CancellationToken ct)
    {
        if (nodeIds.Count == 0) return [];
        var stateQuery = db.NotionPages.AsNoTracking().Where(p => nodeIds.Contains(p.Id));
        var states = manage
            ? (await stateQuery.Select(p => new { p.Id, p.YjsState }).ToListAsync(ct)).Select(p => (p.Id, Bytes: p.YjsState))
            : (await stateQuery.Select(p => new { p.Id, p.PublishedState }).ToListAsync(ct)).Select(p => (p.Id, Bytes: p.PublishedState));
        var edges = new List<NotionGraphEdge>();
        var seenEdges = new HashSet<(Guid From, Guid To)>();
        foreach (var state in states)
        {
            if (state.Bytes is not { Length: > 0 and <= MaxGraphStateBytes } bytes) continue;
            var targets = new HashSet<Guid>();
            if (!YjsPageLinks.TryRead(bytes, targets)) continue;
            foreach (var target in targets)
            {
                if (target == state.Id || !nodeIds.Contains(target)) continue;
                if (seenEdges.Add((state.Id, target))) edges.Add(new NotionGraphEdge(state.Id, target));
            }
        }
        return edges.OrderBy(e => e.FromId).ThenBy(e => e.ToId).ToArray();
    }

    private static bool PageVisible(PageMeta page, bool manage) =>
        manage || page.PublishedAtUtc is not null && page.PublishedTitle is not null && page.HasPublication;

    private static NotionGraphNode[] ToNodes(IReadOnlyList<PageMeta> nodes, bool manage) =>
        nodes.Select(p => new NotionGraphNode(p.Id, manage ? p.Title : p.PublishedTitle!)).ToArray();

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
    public Task<IActionResult> Context(Guid id, CancellationToken ct, bool includeResources = true) => ReadContext(id, false, includeResources, ct);

    [HttpGet("pages/{id:guid}/published-context")]
    public Task<IActionResult> PublishedContext(Guid id, CancellationToken ct, bool includeResources = true) => ReadContext(id, true, includeResources, ct);

    [HttpGet("holds/{holdId:guid}/editor-resources")]
    public async Task<IActionResult> EditorResources(Guid holdId, CancellationToken ct, bool publishedOnly = false)
    {
        if (!await access.CanAccessAsync(holdId, User, ct)) return NotFound();
        var edit = !publishedOnly && await access.CanManageAsync(holdId, User, ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(await ReadEditorResources(holdId, edit, ct));
    }

    private async Task<NotionEditorResources> ReadEditorResources(Guid holdId, bool edit, CancellationToken ct)
    {
        var members = await db.Memberships.AsNoTracking().Where(m => m.HoldId == holdId)
            .OrderBy(m => m.DisplayName).Select(m => new NotionMember(m.Id, m.DisplayName ?? "Elev"))
            .ToArrayAsync(ct);
        var polls = await db.PollRounds.AsNoTracking().Where(p => p.HoldId == holdId && (edit || p.State != "draft"))
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new NotionPollReference(p.Id, p.Question, p.State)).Take(100).ToArrayAsync(ct);
        return new(members, polls);
    }

    private async Task<IActionResult> ReadContext(Guid id, bool publishedOnly, bool includeResources, CancellationToken ct)
    {
        // Establish access before fetching the selected document. Draft editing does not need
        // the potentially large publication snapshot or drawing manifest at all.
        var holdId = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => (Guid?)p.HoldId).SingleOrDefaultAsync(ct);
        if (holdId is not Guid hold || !await access.CanAccessAsync(hold, User, ct)) return NotFound();
        var edit = !publishedOnly && await access.CanManageAsync(hold, User, ct);
        var page = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.Id, p.HoldId, p.Title, p.UpdatedAtUtc, p.PublishedAtUtc, p.PublishedTitle,
                p.DraftRevision, p.PublishedRevision, PublishedState = edit ? null : p.PublishedState,
                PublishedDrawingsJson = edit ? null : p.PublishedDrawingsJson }).SingleOrDefaultAsync(ct);
        if (page is null) return NotFound();
        if (!edit && (page.PublishedAtUtc is null || page.PublishedTitle is null || page.PublishedState is not { Length: > 0 })) return NotFound();
        var resources = includeResources ? await ReadEditorResources(page.HoldId, edit, ct) : new NotionEditorResources([], []);
        NotionPageLink[] pages = [];
        if (includeResources)
        {
            var links = await db.NotionPages.AsNoTracking().Where(p => p.HoldId == page.HoldId
                && (edit || p.PublishedAtUtc != null && p.PublishedState != null && p.PublishedTitle != null))
                .Select(p => new { p.Id, p.Title, p.PublishedTitle, p.ParentPageId }).ToListAsync(ct);
            var visible = links.Select(p => p.Id).ToHashSet();
            pages = links.Select(p => new NotionPageLink(p.Id, edit ? p.Title : p.PublishedTitle!,
                edit || p.ParentPageId is not Guid parent || visible.Contains(parent) ? p.ParentPageId : null)).ToArray();
        }
        // Published metadata and bytes come from the same row read; a concurrent re-publication
        // cannot combine an old title with a new document body.
        var state = edit ? null : page.PublishedState;
        Response.Headers.CacheControl = "no-store";
        return Ok(new NotionPageContext(page.Id, page.HoldId, edit ? page.Title : page.PublishedTitle!, edit, resources.Members, resources.Polls,
            edit ? page.UpdatedAtUtc : page.PublishedAtUtc!.Value, pages, page.PublishedAtUtc,
            edit ? page.DraftRevision : page.PublishedRevision ?? 0, page.PublishedRevision,
            state is null ? null : Convert.ToBase64String(state), page.PublishedTitle,
            edit ? [] : DrawingAccess.Bindings(page.PublishedDrawingsJson)));
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
        var drawingIds = JsonSerializer.Deserialize<Guid[]>(page.DrawingIdsJson) ?? [];
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
        DrawingPublicationBinding[] bindings = [];
        if (drawingIds.Length > 0 || (request.Drawings?.Length ?? 0) > 0)
        {
            if (drawings is null) return StatusCode(503, new { error = "drawing_service_unavailable" });
            // A checkpoint may persist a new parent revision: it must happen before any row lock.
            await drawings.CheckpointAsync(drawingIds, ct);
            transaction = await db.Database.BeginTransactionAsync(ct);
            var locked = await db.NotionPages.Where(p => p.Id == id && p.DraftRevision == request.ExpectedRevision
                && p.DraftFingerprint == request.ExpectedFingerprint && p.PublicationVersion == page.PublicationVersion)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdatedAtUtc, p => p.UpdatedAtUtc), ct);
            if (locked != 1) return Conflict(new { error = "draft_changed" });
            bindings = await drawings.PreparePublicationAsync("notion", id, drawingIds, request.Drawings, ct);
        }
        var drawingManifest = JsonSerializer.Serialize(bindings, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        // The snapshot and its file grants change in one conditional SQL update. New draft saves,
        // title changes or a concurrent withdrawal invalidate this publication attempt.
        var written = await db.NotionPages.Where(p => p.Id == id && p.DraftRevision == request.ExpectedRevision
            && p.DraftFingerprint == request.ExpectedFingerprint && p.PublicationVersion == page.PublicationVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedState, page.YjsState)
                .SetProperty(p => p.PublishedTitle, page.Title).SetProperty(p => p.PublishedAtUtc, now)
                .SetProperty(p => p.PublishedBySub, sub).SetProperty(p => p.PublishedRevision, request.ExpectedRevision)
                .SetProperty(p => p.PublishedAssetIdsJson, assets).SetProperty(p => p.PublishedDrawingsJson, drawingManifest)
                .SetProperty(p => p.PublicationVersion, p => p.PublicationVersion + 1), ct);
        if (written == 0) return Conflict(new { error = "draft_changed" });
        if (transaction is not null) await transaction.CommitAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(View(await db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == id, ct)));
        }
        catch (DrawingException ex) { return StatusCode(ex.Status, new { error = ex.Code, message = ex.Message }); }
        catch (HttpRequestException) { return StatusCode(503, new { error = "drawing_service_unavailable" }); }
        finally { if (transaction is not null) await transaction.DisposeAsync(); }
    }

    [HttpPost("pages/{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        var page = await FindEditable(id, ct);
        if (page is null) return NotFound();
        await db.NotionPages.Where(p => p.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.PublishedState, (byte[]?)null).SetProperty(p => p.PublishedTitle, (string?)null)
            .SetProperty(p => p.PublishedAtUtc, (DateTimeOffset?)null).SetProperty(p => p.PublishedBySub, (Guid?)null)
            .SetProperty(p => p.PublishedRevision, (long?)null).SetProperty(p => p.PublishedAssetIdsJson, "[]").SetProperty(p => p.PublishedDrawingsJson, "[]")
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
        // Only notion:<guid> rooms are accepted. Hex casing may vary from JS/Blazor.
        return name is not null && name.Length == 43 && name.StartsWith("notion:", StringComparison.Ordinal)
            && Guid.TryParseExact(name.AsSpan(7), "D", out id) && id != Guid.Empty;
    }

    private async Task<NotionPage?> FindEditable(Guid id, CancellationToken ct)
    {
        var page = await db.NotionPages.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        return page is not null && await access.CanManageAsync(page.HoldId, User, ct) ? page : null;
    }

    private const int MaxGraphStateBytes = 1_500_000;
    private sealed record PageMeta(Guid Id, Guid? ParentPageId, string Title, string? PublishedTitle,
        DateTimeOffset? PublishedAtUtc, bool HasPublication);

    private static bool ValidTitle(string? title) => !string.IsNullOrWhiteSpace(title)
        && title.Length <= 200 && !title.Any(char.IsControl);
    private static NotionPageSummary View(NotionPage p) => new(p.Id, p.HoldId, p.Title, p.ParentPageId, p.UpdatedAtUtc,
        p.PublishedAtUtc, p.PublishedAtUtc is not null && (p.DraftRevision != p.PublishedRevision || p.Title != p.PublishedTitle));
}
