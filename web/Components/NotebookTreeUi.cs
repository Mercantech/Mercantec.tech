using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Components;

public sealed record NotebookTreeItem(Guid Id, string Title, Guid? ParentId, Guid? ScopeId = null);

public static class NotebookTreeUi
{
    public static HashSet<Guid> Branch(IEnumerable<NotebookTreeItem> items, Guid root)
    {
        var children = items.ToLookup(p => p.ParentId);
        var branch = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(root);
        while (pending.TryPop(out var id))
            if (branch.Add(id))
                foreach (var child in children[id]) pending.Push(child.Id);
        return branch;
    }

    public static MoveNotebookPage? Placement(NotebookTreeItem[] items, Guid page, Guid? target, string position)
    {
        if (position == "root") return new(null);
        var source = items.FirstOrDefault(p => p.Id == page);
        var destination = items.FirstOrDefault(p => p.Id == target);
        if (source is null || destination is null || source.ScopeId != destination.ScopeId || Branch(items, page).Contains(destination.Id))
            return null;
        return position switch
        {
            "before" => new(destination.ParentId, BeforePageId: destination.Id),
            "after" => new(destination.ParentId, AfterPageId: destination.Id),
            _ => new(destination.Id)
        };
    }
}
