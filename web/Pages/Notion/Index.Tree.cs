using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Navigation;

namespace Mercantec.Tech.Web.Pages.Notion;

public partial class Index
{
    private NotionCollectionView? _collection;
    private NotebookTreeDialogs? _treeDialogs;
    private Guid? _draggingPage;
    private Guid? ActiveCollectionId => _current?.CollectionId ?? _collection?.Id;
    private bool CanReorderTree => string.IsNullOrWhiteSpace(_pageSearch);
    private bool CanOpenTrash => ActiveCollectionId is not null || _holdId != Guid.Empty;
    private NotebookTreeItem[] TreeItems => _pagesById.Values.Select(p => new NotebookTreeItem(p.Id, p.Title, p.ParentPageId, p.CollectionId)).ToArray();
    private CancellationToken TreeToken => _requests?.Token ?? _life.Token;

    private Task OpenTreeActions(Guid id) => _treeDialogs?.Show(id) ?? Task.CompletedTask;
    private Task OpenTreeTrash() => _treeDialogs?.ShowTrash() ?? Task.CompletedTask;
    private Task CreateTreeChild(Guid id) { _expandedPages.Add(id); BeginCreateAt(id); return Task.CompletedTask; }
    private void StartTreeDrag(Guid id) { if (Authoring && !_busy) _draggingPage = id; }
    private void EndTreeDrag() => _draggingPage = null;
    private async Task DropTreePage((Guid? Target, string Position) drop)
    {
        var source = _draggingPage;
        EndTreeDrag();
        if (source is Guid id && NotebookTreeUi.Placement(TreeItems, id, drop.Target, drop.Position) is { } move)
            await Work(async _ => await MoveTreePage(id, move));
    }
    private async Task<bool> MoveTreePage(Guid id, MoveNotebookPage move)
    {
        if (!Authoring) return false;
        await Client.MovePage(id, move, TreeToken);
        if (move.ParentPageId is Guid parent) _expandedPages.Add(parent);
        _revealedWithMetadata = false;
        await RetryNavigation();
        _notice = "Siden er flyttet.";
        await InvokeAsync(StateHasChanged);
        return true;
    }
    private Task<NotebookPageDeletionPreview> PreviewTreeDelete(Guid id) => Client.DeletePreview(id, TreeToken);
    private async Task<bool> DeleteTreePage(Guid id, int descendants)
    {
        if (!Authoring || !await CanLeave()) return false;
        var branch = NotebookTreeUi.Branch(TreeItems, id);
        await Client.DeletePage(id, descendants, TreeToken);
        if (_current is { } page && branch.Contains(page.PageId))
        {
            var collection = ActiveCollectionId;
            await Unmount();
            _current = null;
            if (collection is Guid collectionId && CollectionId == collectionId) await Load();
            else Nav.NavigateTo(collection is Guid scope ? $"/notion/collections/{scope}" : WorkspaceNavigation.Editor(_holdId));
        }
        else await RetryNavigation();
        _notice = "Siden er flyttet til papirkurven.";
        await InvokeAsync(StateHasChanged);
        return true;
    }
    private Task<TrashedNotebookPage[]> ListTreeTrash() =>
        Client.Trash(ActiveCollectionId ?? _holdId, ActiveCollectionId is not null, TreeToken);
    private async Task RestoreTreePage(Guid id)
    {
        if (!Authoring) return;
        await Client.RestorePage(id, TreeToken);
        await RetryNavigation();
        _notice = "Siden er gendannet.";
        await InvokeAsync(StateHasChanged);
    }
}
