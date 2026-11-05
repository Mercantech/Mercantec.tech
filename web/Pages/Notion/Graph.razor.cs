using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Notion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Notion;

public partial class Graph
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private NotionClient Client { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Parameter] public Guid? CollectionId { get; set; }
    [SupplyParameterFromQuery(Name = "hold")] public Guid? HoldQuery { get; set; }
    [SupplyParameterFromQuery(Name = "published")] public bool PublishedPreview { get; set; }

    private readonly CancellationTokenSource _life = new();
    private CancellationTokenSource? _requests;
    private IJSObjectReference? _module;
    private DotNetObjectReference<Graph>? _callbacks;
    private ElementReference _stage, _host, _menuFirst, _renameInput;
    private NotionHold[] _holds = [];
    private NotionPageSummary[] _pages = [];
    private NotionGraph? _graph;
    private Guid _holdId;
    private Guid? _menuPage;
    private double _menuX, _menuY;
    private string? _account, _loadedRoute, _error;
    private string _renameTitle = "";
    private bool _loading = true, _busy, _needsMount, _mounted, _disposed, _redirecting, _scriptFailed, _renaming, _focusMenu, _focusRename;
    private bool ForceReadOnly => PublishedPreview || ViewAs.IsActive;
    private string RouteKey => $"{CollectionId}:{HoldQuery}:{ForceReadOnly}";
    private string PagesHref
    {
        get
        {
            var path = _graph is not null ? $"/notion/{_graph.CollectionId}" : _holdId == Guid.Empty ? "/notion" : $"/notion?hold={_holdId}";
            if (!ForceReadOnly) return path;
            return path.Contains('?') ? path + "&published=true" : path + "?published=true";
        }
    }

    protected override void OnInitialized() { Auth.AuthStateChanged += AccountChanged; ViewAs.Changed += AccountChanged; }
    protected override async Task OnParametersSetAsync()
    {
        if (_account is null || !Auth.IsInitialized || !Auth.IsAuthenticated || _loadedRoute == RouteKey) return;
        await Unmount();
        await Load();
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) { await SyncAccount(); return; }
        if (_focusMenu) { _focusMenu = false; try { await _menuFirst.FocusAsync(); } catch (Exception) { } }
        if (_focusRename) { _focusRename = false; try { await _renameInput.FocusAsync(); } catch (Exception) { } }
        if (!_needsMount || _graph is null || _graph.Edges.Length == 0 || _disposed) return;
        _needsMount = false;
        var collection = _graph.CollectionId;
        try
        {
            _module ??= await Js.InvokeAsync<IJSObjectReference>("import", "/js/notion-graph.js");
            _callbacks ??= DotNetObjectReference.Create(this);
            if (collection != _graph?.CollectionId || _disposed) return;
            await _module.InvokeVoidAsync("mount", _host, _callbacks, GraphPayload());
            _mounted = true;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            _scriptFailed = true;
            if (!_disposed) StateHasChanged();
        }
    }

    private object GraphPayload() => new
    {
        nodes = _graph!.Nodes.Select(n => new { id = n.Id, title = n.Title }).ToArray(),
        edges = _graph.Edges.Select(e => new { fromId = e.FromId, toId = e.ToId }).ToArray()
    };
    private void AccountChanged() => _ = InvokeAsync(SyncAccount);
    private async Task SyncAccount()
    {
        if (!Auth.IsInitialized || _disposed) return;
        var account = $"{Auth.Subject}:{ViewAs.IsActive}";
        if (_account == account) return;
        _account = account;
        await Unmount();
        _graph = null; _holds = []; _pages = []; _menuPage = null;
        if (Auth.IsAuthenticated) await Load(); else { _requests?.Cancel(); _loading = false; }
        if (!_disposed) StateHasChanged();
    }
    private async Task Load()
    {
        var old = _requests;
        _requests = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        if (old is not null) { await old.CancelAsync(); old.Dispose(); }
        var ct = _requests.Token; var account = _account;
        _loadedRoute = RouteKey; _loading = true; _error = null; _redirecting = false; _scriptFailed = false;
        _graph = null; _menuPage = null;
        try
        {
            var holds = await Client.Holds(ct);
            if (account != _account || ct.IsCancellationRequested) return;
            _holds = holds;
            if (CollectionId is Guid collection)
            {
                _graph = await Client.Graph(collection, ct, ForceReadOnly);
                if (account != _account || ct.IsCancellationRequested) return;
                _holdId = _graph.HoldId;
                _pages = await Client.Pages(_holdId, ct, ForceReadOnly);
                _needsMount = _graph.Edges.Length > 0;
            }
            else if (holds.Length > 0)
            {
                if (HoldQuery is Guid requested && holds.All(h => h.Id != requested))
                    throw new NotionException("Holdet er ikke tilgængeligt for din konto.");
                _holdId = HoldQuery ?? holds[0].Id;
                _pages = await Client.Pages(_holdId, ct, ForceReadOnly);
                var roots = _pages.Where(p => p.ParentPageId is null || _pages.All(other => other.Id != p.ParentPageId)).ToArray();
                if (roots.Length == 1)
                {
                    _redirecting = true;
                    Nav.NavigateTo(GraphPath(roots[0].Id), replace: true);
                }
            }
        }
        catch (Exception ex) when (ex is NotionException or HttpRequestException or OperationCanceledException)
        {
            if (!ct.IsCancellationRequested && ex is not OperationCanceledException)
                _error = ex is NotionException notion ? notion.Message : "Grafen kunne ikke hentes. Kontrollér forbindelsen, og prøv igen.";
        }
        finally { if (!ct.IsCancellationRequested && !_redirecting) _loading = false; }
    }
    private IEnumerable<(NotionPageSummary Page, int Depth)> OrderedPages()
    {
        var pages = _pages; var visited = new HashSet<Guid>();
        return Walk(null, 0).Concat(pages.Where(p => !visited.Contains(p.Id)).OrderBy(p => p.Title).Select(p => (p, 0)));
        IEnumerable<(NotionPageSummary, int)> Walk(Guid? parent, int depth)
        {
            foreach (var page in pages.Where(p => p.ParentPageId == parent).OrderBy(p => p.Title))
            { if (!visited.Add(page.Id)) continue; yield return (page, Math.Min(depth, 4)); foreach (var child in Walk(page.Id, depth + 1)) yield return child; }
        }
    }
    private bool IsCollection(NotionPageSummary page) =>
        page.ParentPageId is null || _pages.All(p => p.Id != page.ParentPageId) || _pages.Any(p => p.ParentPageId == page.Id);
    private IEnumerable<(NotionPageSummary Page, int Depth)> Collections()
    {
        var items = OrderedPages().Where(item => IsCollection(item.Page)).ToList();
        if (_graph is not null && items.All(item => item.Page.Id != _graph.CollectionId))
        {
            var current = _pages.FirstOrDefault(p => p.Id == _graph.CollectionId);
            if (current is not null) items.Insert(0, (current, 0));
            else items.Insert(0, (new NotionPageSummary(_graph.CollectionId, _graph.HoldId, _graph.Title, null, DateTimeOffset.UtcNow), 0));
        }
        return items;
    }
    private static string OptionLabel((NotionPageSummary Page, int Depth) item) =>
        new string('–', item.Depth) + (item.Depth > 0 ? " " : "") + item.Page.Title;
    private string Title(Guid id) => _graph?.Nodes.FirstOrDefault(n => n.Id == id)?.Title ?? "Side";
    private IEnumerable<NotionGraphNode> Isolated() => _graph is null ? [] : _graph.Nodes.Where(n => _graph.Edges.All(e => e.FromId != n.Id && e.ToId != n.Id));
    private string PagePath(Guid id) => WithPreview($"/notion/{id}");
    private string GraphPath(Guid id) => WithPreview($"/notion/graph/{id}");
    private string WithPreview(string path) => ForceReadOnly ? path + "?published=true" : path;
    private void ChangeCollection(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var id) && id != _graph?.CollectionId) Nav.NavigateTo(GraphPath(id));
    }
    private void ChangeHold(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var id) && id != _holdId)
            Nav.NavigateTo("/notion/graph?hold=" + id + (ForceReadOnly ? "&published=true" : ""));
    }
    private async Task Zoom(int direction)
    {
        if (_module is null || !_mounted) return;
        try { await _module.InvokeVoidAsync("zoom", _host, direction); } catch (JSException) { }
    }
    private async Task Retry() { await Unmount(); await Load(); }
    [JSInvokable] public Task OnGraphOpen(string id) => InvokeAsync(() => { if (!_disposed && Guid.TryParse(id, out var page)) Nav.NavigateTo(PagePath(page)); });
    [JSInvokable] public Task OnGraphMenu(string id, double x, double y) => InvokeAsync(() =>
    {
        if (_disposed) return;
        if (!Guid.TryParse(id, out var page) || _graph?.Nodes.All(n => n.Id != page) != false) { CloseMenu(); StateHasChanged(); return; }
        _menuPage = page; _menuX = x; _menuY = y; _renaming = false; _renameTitle = Title(page); _focusMenu = true;
        StateHasChanged();
    });
    private void BeginRename() { _renaming = true; _focusRename = true; }
    private void CloseMenu() { _menuPage = null; _renaming = false; }
    private void MenuKey(KeyboardEventArgs e) { if (e.Key == "Escape") CloseMenu(); }
    private async Task RenameMenuPage()
    {
        if (_menuPage is not Guid id || _graph?.CanRename != true || _busy || string.IsNullOrWhiteSpace(_renameTitle)) return;
        _busy = true; _error = null;
        try
        {
            await Client.Rename(id, _renameTitle.Trim(), _requests?.Token ?? _life.Token);
            CloseMenu();
            await Unmount();
            await Load();
        }
        catch (Exception ex) when (ex is NotionException or HttpRequestException or OperationCanceledException)
        {
            if (ex is not OperationCanceledException) _error = ex is NotionException notion ? notion.Message : "Titlen kunne ikke gemmes. Prøv igen.";
        }
        finally { _busy = false; }
    }
    private async Task Unmount()
    {
        _needsMount = false;
        if (_mounted && _module is not null)
        {
            try { await _module.InvokeVoidAsync("destroy", _host); }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException) { }
        }
        _mounted = false;
    }
    public async ValueTask DisposeAsync()
    {
        _disposed = true; Auth.AuthStateChanged -= AccountChanged; ViewAs.Changed -= AccountChanged;
        await _life.CancelAsync(); _requests?.Dispose();
        try { await Unmount(); if (_module is not null) await _module.DisposeAsync(); } catch (JSDisconnectedException) { }
        _callbacks?.Dispose(); _life.Dispose();
    }
}
