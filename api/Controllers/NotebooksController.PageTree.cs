using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class NotebooksController
{
    [HttpGet("{notebookId:guid}/tree")]
    public async Task<IActionResult> Tree(Guid notebookId, CancellationToken ct)
    {
        if (!await CanOpenNotebook(notebookId, ct)) return NotFound();
        NoCache();
        return Ok(await ReadTreeLinks(notebookId, ct));
    }

    [HttpPatch("pages/{id:guid}/placement")]
    public async Task<IActionResult> Move(Guid id, MoveNotebookPage request, CancellationToken ct)
    {
        if (await FindOwnedNotebook(id, ct) is not Guid notebook) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await NotebookPageTree.LockPersonalAsync(db, notebook, ct);
        var pages = await LoadTree(notebook, ct);
        if (!pages.Any(p => p.Id == id)) return NotFound();
        var error = NotebookPageTree.Place(pages, id, request, out var order);
        if (error is not null) return BadRequest(new { error });
        for (var index = 0; index < order.Length; index++)
        {
            var row = order[index];
            var parent = request.ParentPageId;
            if (row.Order == index && row.ParentId == parent) continue;
            await db.NotebookDocuments.Where(p => p.Id == row.Id).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ParentPageId, parent).SetProperty(p => p.SortOrder, index), ct);
        }
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpGet("pages/{id:guid}/delete-preview")]
    public async Task<IActionResult> DeletePreview(Guid id, CancellationToken ct)
    {
        if (await FindOwnedNotebook(id, ct) is not Guid notebook) return NotFound();
        NoCache();
        return Ok(new NotebookPageDeletionPreview(NotebookPageTree.Branch(await LoadTree(notebook, ct), id).Count - 1));
    }

    [HttpDelete("pages/{id:guid}")]
    public async Task<IActionResult> TrashPage(Guid id, CancellationToken ct, int? expectedDescendantCount = null)
    {
        if (await FindOwnedNotebook(id, ct) is not Guid notebook) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await NotebookPageTree.LockPersonalAsync(db, notebook, ct);
        var pages = await LoadTree(notebook, ct);
        if (!pages.Any(p => p.Id == id)) return NotFound();
        var branch = NotebookPageTree.Branch(pages, id);
        if (expectedDescendantCount is int expected && expected != branch.Count - 1)
            return Conflict(new { error = "page_tree_changed" });
        await db.NotebookDocuments.Where(p => branch.Contains(p.Id)).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.DeletedAtUtc, DateTimeOffset.UtcNow).SetProperty(p => p.TrashRootId, id)
            .SetProperty(p => p.LifecycleVersion, p => p.LifecycleVersion + 1), ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpGet("{notebookId:guid}/trash")]
    public async Task<IActionResult> Trash(Guid notebookId, CancellationToken ct)
    {
        if (!await access.CanShareNotebookAsync(notebookId, User, ct)) return NotFound();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-NotebookPageTree.RetentionDays);
        var trashed = TreeDocuments(notebookId, deleted: true).Where(p => p.TrashRootId != null && p.DeletedAtUtc != null);
        if (db.Database.IsNpgsql()) trashed = trashed.Where(p => p.DeletedAtUtc > cutoff);
        var rows = await trashed.Select(p => new { p.Id, p.Title, p.TrashRootId, p.DeletedAtUtc }).ToListAsync(ct);
        var groups = rows.Where(p => p.DeletedAtUtc > cutoff).ToLookup(p => p.TrashRootId);
        NoCache();
        return Ok(groups.Select(g => (Root: g.FirstOrDefault(p => p.Id == g.Key), Count: g.Count() - 1))
            .Where(g => g.Root is not null).OrderByDescending(g => g.Root!.DeletedAtUtc)
            .Select(g => new TrashedNotebookPage(g.Root!.Id, g.Root.Title, g.Root.DeletedAtUtc!.Value, g.Count)).ToArray());
    }

    [HttpPost("pages/{id:guid}/restore")]
    public async Task<IActionResult> RestorePage(Guid id, CancellationToken ct)
    {
        if (await FindOwnedNotebook(id, ct, deleted: true) is not Guid notebook) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await NotebookPageTree.LockPersonalAsync(db, notebook, ct);
        var trashed = TreeDocuments(notebook, deleted: true).Where(p => p.TrashRootId == id && p.DeletedAtUtc != null);
        var root = await trashed.Where(p => p.Id == id).Select(p => new { p.ParentPageId, p.DeletedAtUtc }).SingleOrDefaultAsync(ct);
        if (root is null || root.DeletedAtUtc <= DateTimeOffset.UtcNow.AddDays(-NotebookPageTree.RetentionDays)) return NotFound();
        if (root.ParentPageId is Guid original && !await TreeDocuments(notebook).AnyAsync(p => p.Id == original, ct))
        {
            await trashed.Where(p => p.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ParentPageId, (Guid?)null).SetProperty(p => p.SortOrder, (int?)null), ct);
        }
        await trashed.ExecuteUpdateAsync(s => s
            .SetProperty(p => p.DeletedAtUtc, (DateTimeOffset?)null).SetProperty(p => p.TrashRootId, (Guid?)null), ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private async Task<Guid?> FindOwnedNotebook(Guid id, CancellationToken ct, bool deleted = false)
    {
        var query = db.NotebookDocuments.AsNoTracking();
        if (deleted) query = query.IgnoreQueryFilters().Where(p => p.TrashRootId == id && p.DeletedAtUtc != null);
        var notebook = await query.Where(p => p.Id == id).Select(p => p.NotebookId).SingleOrDefaultAsync(ct);
        return notebook is Guid book && await access.CanShareNotebookAsync(book, User, ct) ? notebook : null;
    }

    private IQueryable<NotebookDocument> TreeDocuments(Guid notebook, bool deleted = false)
    {
        var pages = deleted ? db.NotebookDocuments.IgnoreQueryFilters() : db.NotebookDocuments;
        return pages.Where(p => p.NotebookId == notebook);
    }

    private Task<List<NotebookPageTree.Row>> LoadTree(Guid notebook, CancellationToken ct) => TreeDocuments(notebook)
        .Select(p => new NotebookPageTree.Row(p.Id, p.ParentPageId, p.SortOrder, p.Title, p.PeriodStart)).ToListAsync(ct);

    private async Task<NotionPageLink[]> ReadTreeLinks(Guid notebook, CancellationToken ct)
    {
        var pages = await access.ReadableDocuments(User).AsNoTracking().Where(d => d.NotebookId == notebook)
            .Select(d => new NotionPageLink(d.Id, d.Title, d.ParentPageId, d.SortOrder)).ToArrayAsync(ct);
        var visible = pages.Select(p => p.Id).ToHashSet();
        return pages.Select(p => p.ParentPageId is Guid parent && !visible.Contains(parent)
            ? p with { ParentPageId = null } : p).OrderBy(p => p.Title).ToArray();
    }
}
