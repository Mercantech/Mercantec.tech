using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Notebooks;

internal static class NotebookPageTree
{
    internal const int RetentionDays = 30;

    internal sealed record Row(Guid Id, Guid? ParentId, int? Order, string Title, DateOnly? PeriodStart = null);

    internal static Task LockAsync(AppDbContext db, string scope, Guid id, CancellationToken ct) =>
        db.Database.IsNpgsql()
            ? db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({scope + ":" + id}))", ct)
            : Task.CompletedTask;

    internal static Task LockPersonalAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        db.Database.IsNpgsql()
            ? db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM notebooks WHERE \"Id\" = {id} FOR UPDATE", ct)
            : Task.CompletedTask;

    internal static Task LockTeachingAsync(AppDbContext db, Guid? collectionId, Guid? holdId, CancellationToken ct) =>
        LockAsync(db, collectionId is null ? "notion-hold-tree" : "notion-collection-tree", collectionId ?? holdId ?? Guid.Empty, ct);

    internal static HashSet<Guid> Branch(IReadOnlyList<Row> pages, Guid root)
    {
        var children = pages.ToLookup(p => p.ParentId);
        var ids = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(root);
        while (pending.TryPop(out var id))
            if (ids.Add(id))
                foreach (var child in children[id]) pending.Push(child.Id);
        return ids;
    }

    internal static string? Place(IReadOnlyList<Row> pages, Guid id, MoveNotebookPage request, out Row[] order)
    {
        order = [];
        if (request.BeforePageId is not null && request.AfterPageId is not null) return "invalid_page_position";
        if (request.ParentPageId is Guid parent)
        {
            var parents = pages.ToDictionary(p => p.Id, p => p.ParentId);
            if (!parents.ContainsKey(parent)) return "invalid_parent";
            var visited = new HashSet<Guid> { id };
            for (Guid? cursor = parent; cursor is Guid current; cursor = parents.GetValueOrDefault(current))
                if (!visited.Add(current)) return "parent_cycle";
        }
        var siblings = pages.Where(p => p.Id != id && p.ParentId == request.ParentPageId)
            .OrderBy(p => p.Order ?? int.MaxValue).ThenByDescending(p => p.PeriodStart)
            .ThenBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.Id).ToList();
        var anchor = request.BeforePageId ?? request.AfterPageId;
        var index = anchor is Guid sibling ? siblings.FindIndex(p => p.Id == sibling) : siblings.Count;
        if (index < 0) return "invalid_page_position";
        if (request.AfterPageId is not null) index++;
        siblings.Insert(index, pages.Single(p => p.Id == id));
        order = siblings.ToArray();
        return null;
    }
}
