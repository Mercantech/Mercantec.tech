using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Notebook;

public partial class Index
{
    private NotionPageLink[] _treePages = [];
    private NotebookTreeDialogs? _treeDialogs;
    private Guid? _draggingPage;
    private bool CanReorderTree => string.IsNullOrWhiteSpace(_search) && string.IsNullOrWhiteSpace(_tag) && _kind.Length == 0;
    private NotebookTreeItem[] TreeItems => _treePages.Select(p => new NotebookTreeItem(p.Id, p.Title, p.ParentPageId, _notebookId)).ToArray();
    private CancellationToken TreeToken => _requests?.Token ?? _life.Token;

    private Task OpenTreeActions(Guid id) => _treeDialogs?.Show(id) ?? Task.CompletedTask;
    private Task OpenTreeTrash() => _treeDialogs?.ShowTrash() ?? Task.CompletedTask;
    private Task CreateTreeChild(Guid id) { _sidebarOpen = true; BeginCreateAt(id); return Task.CompletedTask; }
    private void StartTreeDrag(Guid id) { if (Own && !_busy) _draggingPage = id; }
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
        if (!Own) return false;
        await Client.MovePage(id, move, TreeToken);
        await RefreshTree();
        _notice = "Siden er flyttet.";
        await InvokeAsync(StateHasChanged);
        return true;
    }
    private Task<NotebookPageDeletionPreview> PreviewTreeDelete(Guid id) => Client.DeletePreview(id, TreeToken);
    private async Task<bool> DeleteTreePage(Guid id, int descendants)
    {
        if (!Own || !await CanLeave()) return false;
        var branch = NotebookTreeUi.Branch(TreeItems, id);
        await Client.DeletePage(id, descendants, TreeToken);
        if (_current is { } page && branch.Contains(page.DocumentId))
        {
            await Unmount();
            _current = null;
            if (PageId is not null) Nav.NavigateTo(_kind == "journal" ? "/journal" : "/notebook");
            else await Load();
        }
        else await RefreshTree();
        _notice = "Siden er flyttet til papirkurven.";
        await InvokeAsync(StateHasChanged);
        return true;
    }
    private Task<TrashedNotebookPage[]> ListTreeTrash() => _notebookId is Guid id ? Client.Trash(id, TreeToken) : Task.FromResult<TrashedNotebookPage[]>([]);
    private async Task RestoreTreePage(Guid id)
    {
        if (!Own) return;
        await Client.RestorePage(id, TreeToken);
        await RefreshTree();
        _notice = "Siden er gendannet.";
        await InvokeAsync(StateHasChanged);
    }
    private async Task RefreshTree()
    {
        if (_notebookId is not Guid id) return;
        var ct = TreeToken;
        var generation = _generation;
        var pagesTask = Client.Pages(id, _search, _tag, _kind, 0, ct);
        var treeTask = Client.Tree(id, ct);
        await Task.WhenAll(pagesTask, treeTask);
        if (generation != _generation || ct.IsCancellationRequested) return;
        var result = await pagesTask;
        _treePages = await treeTask;
        _pages = result.Items; _total = result.Total;
        if (_current is not null) _current = _current with { Pages = _treePages };
        if (_mounted && _editor is not null && _current is not null)
            await _editor.InvokeVoidAsync("setContext", _editorElement, EditorContext());
    }
}
