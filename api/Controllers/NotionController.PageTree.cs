using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class NotionController
{
    [HttpPatch("pages/{id:guid}/placement")]
    public async Task<IActionResult> Move(Guid id, MoveNotebookPage request, CancellationToken ct)
    {
        var scope = await FindTreeScope(id, ct);
        if (scope is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await NotebookPageTree.LockTeachingAsync(db, scope.CollectionId, scope.HoldId, ct);
        var pages = await LoadTree(scope, ct);
        if (!pages.Any(p => p.Id == id)) return NotFound();
        var error = NotebookPageTree.Place(pages, id, request, out var order);
        if (error is not null) return BadRequest(new { error });
        for (var index = 0; index < order.Length; index++)
        {
            var row = order[index];
            var parent = request.ParentPageId;
            if (row.Order == index && row.ParentId == parent) continue;
            await db.NotionPages.Where(p => p.Id == row.Id).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ParentPageId, parent).SetProperty(p => p.SortOrder, index), ct);
        }
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpGet("pages/{id:guid}/delete-preview")]
    public async Task<IActionResult> DeletePreview(Guid id, CancellationToken ct)
    {
        var scope = await FindTreeScope(id, ct);
        if (scope is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new NotebookPageDeletionPreview(NotebookPageTree.Branch(await LoadTree(scope, ct), id).Count - 1));
    }

    [HttpDelete("pages/{id:guid}")]
    public async Task<IActionResult> TrashPage(Guid id, CancellationToken ct, int? expectedDescendantCount = null)
    {
        var scope = await FindTreeScope(id, ct);
        if (scope is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await NotebookPageTree.LockTeachingAsync(db, scope.CollectionId, scope.HoldId, ct);
        var pages = await LoadTree(scope, ct);
        if (!pages.Any(p => p.Id == id)) return NotFound();
        var branch = NotebookPageTree.Branch(pages, id);
        if (expectedDescendantCount is int expected && expected != branch.Count - 1)
            return Conflict(new { error = "page_tree_changed" });
        var now = DateTimeOffset.UtcNow;
        await db.NotionPages.Where(p => branch.Contains(p.Id)).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.DeletedAtUtc, now).SetProperty(p => p.TrashRootId, id)
            .SetProperty(p => p.LifecycleVersion, p => p.LifecycleVersion + 1)
            .SetProperty(p => p.PublicationVersion, p => p.PublicationVersion + 1), ct);
        if (scope.CollectionId is Guid collection && branch.Contains(collection))
        {
            var root = await db.NotionPages.IgnoreQueryFilters().Where(p => p.Id == collection && p.TrashRootId == id)
                .Select(p => new { p.Title, p.PublishedTitle }).SingleAsync(ct);
            await db.NotionCollections.Where(c => c.Id == collection).ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Title, root.Title).SetProperty(c => c.PublishedTitle, root.PublishedTitle), ct);
        }
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpGet("collections/{id:guid}/trash")]
    public async Task<IActionResult> CollectionTrash(Guid id, CancellationToken ct)
    {
        var collection = await db.NotionCollections.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (collection is null || !await pagesAccess.CanManageScopeAsync(collection.HoldId, User, ct)) return NotFound();
        return await ReadTrash(new(collection.HoldId, collection.Id), ct);
    }

    [HttpGet("holds/{id:guid}/trash")]
    public async Task<IActionResult> HoldTrash(Guid id, CancellationToken ct)
    {
        if (!await pagesAccess.CanManageScopeAsync(id, User, ct)) return NotFound();
        return await ReadTrash(new(id, null), ct);
    }

    [HttpPost("pages/{id:guid}/restore")]
    public async Task<IActionResult> RestorePage(Guid id, CancellationToken ct)
    {
        var scope = await FindTreeScope(id, ct, deleted: true);
        if (scope is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await NotebookPageTree.LockTeachingAsync(db, scope.CollectionId, scope.HoldId, ct);
        var root = await TreePages(scope, deleted: true).Where(p => p.Id == id && p.TrashRootId == id && p.DeletedAtUtc != null)
            .Select(p => new { p.ParentPageId, p.DeletedAtUtc }).SingleOrDefaultAsync(ct);
        if (root is null || root.DeletedAtUtc <= DateTimeOffset.UtcNow.AddDays(-NotebookPageTree.RetentionDays)) return NotFound();
        if (root.ParentPageId is Guid original && !await TreePages(scope).AnyAsync(p => p.Id == original, ct))
        {
            await TreePages(scope, deleted: true).Where(p => p.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ParentPageId, (Guid?)null).SetProperty(p => p.SortOrder, (int?)null), ct);
        }
        await TreePages(scope, deleted: true).Where(p => p.TrashRootId == id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.DeletedAtUtc, (DateTimeOffset?)null).SetProperty(p => p.TrashRootId, (Guid?)null), ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private sealed record TreeScope(Guid? HoldId, Guid? CollectionId);

    private async Task<TreeScope?> FindTreeScope(Guid id, CancellationToken ct, bool deleted = false)
    {
        var query = db.NotionPages.AsNoTracking();
        if (deleted) query = query.IgnoreQueryFilters().Where(p => p.TrashRootId == id && p.DeletedAtUtc != null);
        var scope = await query.Where(p => p.Id == id).Select(p => new TreeScope(p.HoldId, p.CollectionId)).SingleOrDefaultAsync(ct);
        return scope is not null && await pagesAccess.CanManageScopeAsync(scope.HoldId, User, ct) ? scope : null;
    }

    private IQueryable<NotionPage> TreePages(TreeScope scope, bool deleted = false)
    {
        var pages = deleted ? db.NotionPages.IgnoreQueryFilters() : db.NotionPages;
        return pages.Where(p => p.CollectionId == scope.CollectionId && p.HoldId == scope.HoldId);
    }

    private Task<List<NotebookPageTree.Row>> LoadTree(TreeScope scope, CancellationToken ct) => TreePages(scope)
        .Select(p => new NotebookPageTree.Row(p.Id, p.ParentPageId, p.SortOrder, p.Title, null)).ToListAsync(ct);

    private async Task<IActionResult> ReadTrash(TreeScope scope, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-NotebookPageTree.RetentionDays);
        var trashed = TreePages(scope, deleted: true).Where(p => p.TrashRootId != null && p.DeletedAtUtc != null);
        if (db.Database.IsNpgsql()) trashed = trashed.Where(p => p.DeletedAtUtc > cutoff);
        var rows = await trashed.Select(p => new { p.Id, p.Title, p.TrashRootId, p.DeletedAtUtc }).ToListAsync(ct);
        var groups = rows.Where(p => p.DeletedAtUtc > cutoff).ToLookup(p => p.TrashRootId);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(groups.Select(g => (Root: g.FirstOrDefault(p => p.Id == g.Key), Count: g.Count() - 1))
            .Where(g => g.Root is not null).OrderByDescending(g => g.Root!.DeletedAtUtc)
            .Select(g => new TrashedNotebookPage(g.Root!.Id, g.Root.Title, g.Root.DeletedAtUtc!.Value, g.Count)).ToArray());
    }
}
