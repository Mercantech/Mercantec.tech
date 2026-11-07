using System.Globalization;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Notion;
using Mercantec.Tech.Web.Navigation;
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
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Parameter] public Guid? CollectionId { get; set; }
    [Parameter] public Guid? HoldId { get; set; }
    [SupplyParameterFromQuery(Name = "hold")] public Guid? HoldQuery { get; set; }
    [SupplyParameterFromQuery(Name = "published")] public bool PublishedPreview { get; set; }
    [SupplyParameterFromQuery(Name = "workspace")] public Guid? WorkspaceQuery { get; set; }
    [SupplyParameterFromQuery(Name = "course")] public Guid? CourseQuery { get; set; }
    private WorkspaceRouteContext? _returnContext;

    private readonly CancellationTokenSource _life = new();
    private CancellationTokenSource? _requests;
    private IJSObjectReference? _module;
    private DotNetObjectReference<Graph>? _callbacks;
    private ElementReference _stage, _host, _menuFirst, _renameInput;
    private NotionHold[] _holds = [];
    private NotionPageSummary[] _pages = [];
    private (NotionPageSummary Page, int Depth)[]? _collections;
    private NotionGraph? _graph;
    private readonly Dictionary<Guid, string> _titles = [];
    private readonly Dictionary<Guid, List<Guid>> _neighbors = [];
    private readonly Dictionary<Guid, int> _incoming = [];
    private NotionGraphNode[] _visibleNodes = [];
    private NotionGraphEdge[] _visibleEdges = [];
    private Guid? _localNode, _focusNode;
    private int _depth = 1;
    private string _search = "";
    private bool _showOrphans = true, _showLabels = true, _showArrows, _showLinks;
    private double _nodeSize = 1, _linkThickness = 1, _labelThreshold = .8;
    private double _centerForce = 1, _repelForce = 1, _linkForce = 1, _linkDistance = 1;
    private bool _graphDirty, _optionsDirty, _fitGraph;
    private int _canvasGeneration;
    private Guid _holdId;
    private Guid? _menuPage;
    private double _menuX, _menuY;
    private string? _account, _loadedRoute, _error;
    private string _renameTitle = "";
    private bool _loading = true, _busy, _mounted, _disposed, _redirecting, _scriptFailed, _renaming, _focusMenu, _focusRename, _focusStage;
    private bool ForceReadOnly => PublishedPreview || ViewAs.IsActive;
    private bool RouteIsHoldGraph
    {
        get
        {
            var path = Nav.ToBaseRelativePath(Nav.Uri);
            var query = path.IndexOf('?');
            if (query >= 0) path = path[..query];
            return path.StartsWith("notion/holds/", StringComparison.OrdinalIgnoreCase)
                && path.EndsWith("/graph", StringComparison.OrdinalIgnoreCase);
        }
    }
    private bool ViewingHold => RouteIsHoldGraph || _graph?.WholeHold == true;
    private string RouteKey => $"{CollectionId}:{HoldId}:{HoldQuery}:{WorkspaceQuery}:{CourseQuery}:{ForceReadOnly}:{RouteIsHoldGraph}";
    private string GraphHostKey => _graph is null ? "" : _graph.WholeHold ? $"hold:{_graph.HoldId}" : $"page:{_graph.CollectionId}";
    private string ScopeValue => _graph?.WholeHold == true ? "hold" : _graph?.CollectionId.ToString() ?? "";
    private string WorkspaceHref => _returnContext?.View() ?? WorkspaceNavigation.Hold(_holdId, "content");
    private string PagesHref => _graph is { WholeHold: false }
        ? PagePath(_graph.CollectionId)
        : WorkspaceNavigation.Editor(_holdId, _returnContext?.WorkspaceId, _returnContext?.CourseId, ForceReadOnly);

    public override Task SetParametersAsync(ParameterView parameters)
    {
        CollectionId = HoldId = HoldQuery = WorkspaceQuery = CourseQuery = null;
        PublishedPreview = false;
        return base.SetParametersAsync(parameters);
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
        if (_focusStage) { _focusStage = false; try { await _stage.FocusAsync(true); } catch (JSException) { } }
        if (_loading || _graph is not { Nodes.Length: > 0 } || _scriptFailed || _disposed) return;
        if (!_graphDirty && !_optionsDirty && !_fitGraph && _focusNode is null) return;
        var generation = _canvasGeneration;
        try
        {
            _module ??= await Js.InvokeAsync<IJSObjectReference>("import", "/js/notion-graph.js");
            if (generation != _canvasGeneration || _disposed) return;
            _callbacks ??= DotNetObjectReference.Create(this);
            if (_graphDirty)
            {
                _graphDirty = _optionsDirty = false;
                var host = _host;
                if (_mounted)
                {
                    await _module.InvokeVoidAsync("update", host, GraphPayload());
                }
                else
                {
                    await _module.InvokeVoidAsync("mount", host, _callbacks, GraphPayload());
                    if (generation != _canvasGeneration || _disposed)
                    {
                        await _module.InvokeVoidAsync("destroy", host);
                        return;
                    }
                    _mounted = true;
                }
            }
            else if (_optionsDirty)
            {
                _optionsDirty = false;
                await _module.InvokeVoidAsync("configure", _host, GraphOptions());
            }
            if (generation != _canvasGeneration || _disposed) return;
            if (_fitGraph)
            {
                _fitGraph = false;
                await Zoom(0);
            }
            if (_focusNode is Guid id)
            {
                _focusNode = null;
                await _module.InvokeVoidAsync("focus", _host, id);
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            if (generation != _canvasGeneration || _disposed) return;
            _scriptFailed = true;
            _showLinks = true;
            StateHasChanged();
        }
    }

    private object GraphPayload() => new
    {
        nodes = _visibleNodes.Select(node => new { node.Id, node.Title, inbound = _incoming.GetValueOrDefault(node.Id) }).ToArray(),
        edges = _visibleEdges,
        options = GraphOptions()
    };

    private object GraphOptions() => new
    {
        activeId = _localNode,
        showLabels = _showLabels,
        showArrows = _showArrows,
        labelThreshold = _labelThreshold,
        nodeSize = _nodeSize,
        linkThickness = _linkThickness,
        centerForce = _centerForce,
        repelForce = _repelForce,
        linkForce = _linkForce,
        linkDistance = _linkDistance
    };

    private int VisibleNodes => _visibleNodes.Length;
    private int VisibleEdges => _visibleEdges.Length;
    private IEnumerable<NotionGraphNode> SearchResults => string.IsNullOrWhiteSpace(_search) ? [] : _visibleNodes.Take(6);

    private void IndexGraph(NotionGraph graph)
    {
        _graph = graph;
        _titles.Clear();
        _neighbors.Clear();
        _incoming.Clear();
        foreach (var node in graph.Nodes) _titles.Add(node.Id, node.Title);
        foreach (var edge in graph.Edges)
        {
            AddNeighbor(edge.FromId, edge.ToId);
            AddNeighbor(edge.ToId, edge.FromId);
            _incoming[edge.ToId] = _incoming.GetValueOrDefault(edge.ToId) + 1;
        }
        RefreshGraph();
    }

    private void AddNeighbor(Guid node, Guid neighbor)
    {
        if (!_neighbors.TryGetValue(node, out var list)) _neighbors[node] = list = [];
        list.Add(neighbor);
    }

    private HashSet<Guid> Neighborhood(Guid root)
    {
        var nearby = new HashSet<Guid> { root };
        var pending = new Queue<(Guid Id, int Level)>();
        pending.Enqueue((root, 0));
        while (pending.TryDequeue(out var item))
        {
            if (item.Level >= _depth || !_neighbors.TryGetValue(item.Id, out var neighbors)) continue;
            foreach (var neighbor in neighbors)
                if (nearby.Add(neighbor)) pending.Enqueue((neighbor, item.Level + 1));
        }
        return nearby;
    }

    private void RefreshGraph()
    {
        if (_graph is null) return;
        var local = _localNode is Guid id ? Neighborhood(id) : null;
        var query = _search.Trim();
        _visibleNodes = _graph.Nodes.Where(node =>
            (local is null || local.Contains(node.Id))
            && (_showOrphans || _localNode == node.Id || _neighbors.ContainsKey(node.Id))
            && (query.Length == 0 || CultureInfo.CurrentCulture.CompareInfo.IndexOf(node.Title, query, CompareOptions.IgnoreCase) >= 0))
            .ToArray();
        var ids = _visibleNodes.Select(node => node.Id).ToHashSet();
        _visibleEdges = _graph.Edges.Where(edge => ids.Contains(edge.FromId) && ids.Contains(edge.ToId)).ToArray();
        _graphDirty = true;
    }

    private void ChangeSearch(ChangeEventArgs args) { _search = args.Value?.ToString() ?? ""; RefreshGraph(); }
    private void ClearSearch() { _search = ""; RefreshGraph(); }
    private void FocusNode(Guid id) => _focusNode = id;
    private void ChangeOrphans(ChangeEventArgs args) { _showOrphans = args.Value is true; RefreshGraph(); }
    private void ChangeLabels(ChangeEventArgs args) { _showLabels = args.Value is true; _optionsDirty = true; }
    private void ChangeArrows(ChangeEventArgs args) { _showArrows = args.Value is true; _optionsDirty = true; }
    private void ToggleLinks() => _showLinks = !_showLinks;

    private void ShowLocal(Guid id)
    {
        CloseMenu();
        _localNode = id;
        _search = "";
        _fitGraph = true;
        RefreshGraph();
    }

    private void ClearLocal()
    {
        _localNode = null;
        _fitGraph = true;
        RefreshGraph();
    }

    private void ChangeDepth(ChangeEventArgs args)
    {
        if (!int.TryParse(args.Value?.ToString(), out var value) || value is < 1 or > 5) return;
        _depth = value;
        _fitGraph = true;
        RefreshGraph();
    }

    private void ChangeSetting(string name, ChangeEventArgs args)
    {
        if (!double.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) return;
        value = Math.Clamp(value, name == nameof(_labelThreshold) ? .2 : .4, 2.5);
        switch (name)
        {
            case nameof(_nodeSize): _nodeSize = value; break;
            case nameof(_linkThickness): _linkThickness = value; break;
            case nameof(_labelThreshold): _labelThreshold = value; break;
            case nameof(_centerForce): _centerForce = value; break;
            case nameof(_repelForce): _repelForce = value; break;
            case nameof(_linkForce): _linkForce = value; break;
            case nameof(_linkDistance): _linkDistance = value; break;
        }
        _optionsDirty = true;
    }

    private void ResetGraphSettings()
    {
        _showOrphans = _showLabels = true;
        _showArrows = false;
        _nodeSize = _linkThickness = _centerForce = _repelForce = _linkForce = _linkDistance = 1;
        _labelThreshold = .8;
        _depth = 1;
        RefreshGraph();
    }
    private void AccountChanged() => _ = InvokeAsync(SyncAccount);
    private async Task SyncAccount()
    {
        if (!Auth.IsInitialized || _disposed) return;
        var account = $"{Auth.Subject}:{ViewAs.IsActive}";
        if (_account == account) return;
        _account = account;
        await Unmount();
        _graph = null; _holds = []; _pages = []; _menuPage = null;
        _collections = null;
        _titles.Clear(); _neighbors.Clear(); _incoming.Clear(); _visibleNodes = []; _visibleEdges = [];
        if (Auth.IsAuthenticated) await Load(); else { _requests?.Cancel(); _loading = false; }
        if (!_disposed) StateHasChanged();
    }
    private async Task Load()
    {
        var old = _requests;
        using var requests = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        _requests = requests;
        var ct = requests.Token; var account = _account;
        var published = ForceReadOnly;
        _loadedRoute = RouteKey; _loading = true; _error = null; _redirecting = false; _scriptFailed = false;
        _graph = null; _menuPage = null; _returnContext = null;
        _pages = []; _collections = null;
        _search = ""; _localNode = null; _showLinks = false;
        try
        {
            if (old is not null) await old.CancelAsync();
            ct.ThrowIfCancellationRequested();
            var holds = await Client.Holds(ct);
            if (account != _account || ct.IsCancellationRequested) return;
            _holds = holds;
            NotionGraph? graph = null;
            if (RouteIsHoldGraph && HoldId is Guid hold)
                graph = await Client.HoldGraph(hold, ct, published);
            else if (CollectionId is Guid collection)
                graph = await Client.Graph(collection, ct, published);
            if (account != _account || ct.IsCancellationRequested) return;

            if (graph is not null)
            {
                var pages = await Client.Pages(graph.HoldId, ct, published);
                if (account != _account || ct.IsCancellationRequested) return;
                _holdId = graph.HoldId;
                await ResolveReturnContext(ct);
                if (account != _account || ct.IsCancellationRequested) return;
                _pages = pages;
                IndexGraph(graph);
            }
            else if (holds.Length > 0)
            {
                if (HoldQuery is Guid requested && holds.All(h => h.Id != requested))
                    throw new NotionException("Holdet er ikke tilgængeligt for din konto.");
                _holdId = HoldQuery ?? holds[0].Id;
                var pages = await Client.Pages(_holdId, ct, published);
                if (account != _account || ct.IsCancellationRequested) return;
                _pages = pages;
                await ResolveReturnContext(ct);
                if (account != _account || ct.IsCancellationRequested) return;
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
        finally
        {
            if (ReferenceEquals(_requests, requests)) _requests = null;
            if (!ct.IsCancellationRequested && !_redirecting) _loading = false;
        }
    }
    private async Task ResolveReturnContext(CancellationToken ct)
    {
        if (_holdId == Guid.Empty || HoldQuery is Guid requested && requested != _holdId
            || WorkspaceNavigation.NonEmpty(WorkspaceQuery) is null && WorkspaceNavigation.NonEmpty(CourseQuery) is null) return;
        var route = RouteKey;
        var context = await WorkspaceNavigation.Resolve(Http, _holdId, WorkspaceQuery, CourseQuery, ForceReadOnly, ct);
        ct.ThrowIfCancellationRequested();
        if (route == RouteKey) _returnContext = context;
    }

    private IEnumerable<(NotionPageSummary Page, int Depth)> OrderedPages()
    {
        var pages = _pages.OrderBy(page => page.Title).ToArray();
        var children = pages.ToLookup(page => page.ParentPageId);
        var visited = new HashSet<Guid>();
        return Walk(null, 0).Concat(pages.Where(p => !visited.Contains(p.Id)).Select(p => (p, 0)));
        IEnumerable<(NotionPageSummary, int)> Walk(Guid? parent, int depth)
        {
            foreach (var page in children[parent])
            { if (!visited.Add(page.Id)) continue; yield return (page, Math.Min(depth, 4)); foreach (var child in Walk(page.Id, depth + 1)) yield return child; }
        }
    }
    private (NotionPageSummary Page, int Depth)[] Collections() => _collections ??= BuildCollections();

    private (NotionPageSummary Page, int Depth)[] BuildCollections()
    {
        var ids = _pages.Select(page => page.Id).ToHashSet();
        var parents = _pages.Select(page => page.ParentPageId).ToHashSet();
        var items = OrderedPages().Where(item =>
            item.Page.ParentPageId is not Guid parent || !ids.Contains(parent) || parents.Contains(item.Page.Id)).ToList();
        if (_graph is { WholeHold: false } && items.All(item => item.Page.Id != _graph.CollectionId))
        {
            var current = _pages.FirstOrDefault(p => p.Id == _graph.CollectionId);
            if (current is not null) items.Insert(0, (current, 0));
            else items.Insert(0, (new NotionPageSummary(_graph.CollectionId, _graph.HoldId, _graph.Title, null, DateTimeOffset.UtcNow), 0));
        }
        return items.ToArray();
    }
    private static string OptionLabel((NotionPageSummary Page, int Depth) item) =>
        new string('–', item.Depth) + (item.Depth > 0 ? " " : "") + item.Page.Title;
    private string Title(Guid id) => _titles.GetValueOrDefault(id, "Side");
    private IEnumerable<NotionGraphNode> Isolated() => _graph is null ? [] : _graph.Nodes.Where(node => !_neighbors.ContainsKey(node.Id));
    private string PagePath(Guid id) => WorkspaceNavigation.Page(id, _returnContext?.WorkspaceId, _returnContext?.CourseId, ForceReadOnly, _holdId);
    private string GraphPath(Guid id) => WorkspaceNavigation.Graph(id, _returnContext?.WorkspaceId, _returnContext?.CourseId, ForceReadOnly, _holdId);
    private string HoldGraphPath(Guid id) => WorkspaceNavigation.HoldGraph(id, _returnContext?.WorkspaceId, _returnContext?.CourseId, ForceReadOnly);
    private void ChangeScope(ChangeEventArgs args)
    {
        var value = args.Value?.ToString();
        if (value == "hold")
        {
            if (_graph?.WholeHold != true && _holdId != Guid.Empty) Nav.NavigateTo(HoldGraphPath(_holdId));
            return;
        }
        if (Guid.TryParse(value, out var id) && (_graph?.WholeHold == true || id != _graph?.CollectionId))
            Nav.NavigateTo(GraphPath(id));
    }
    private void ChangeHold(ChangeEventArgs args)
    {
        if (!Guid.TryParse(args.Value?.ToString(), out var id) || id == _holdId) return;
        Nav.NavigateTo(ViewingHold
            ? WorkspaceNavigation.HoldGraph(id, publishedOnly: ForceReadOnly)
            : WorkspaceNavigation.GraphIndex(id, ForceReadOnly));
    }
    private async Task Zoom(int direction)
    {
        if (_module is null || !_mounted) return;
        try { await _module.InvokeVoidAsync("zoom", _host, direction); } catch (JSException) { }
    }
    private async Task Retry() { await Unmount(); await Load(); }
    [JSInvokable] public Task OnGraphOpen(string id) => InvokeAsync(() =>
    {
        if (!_disposed && _graph is not null && Guid.TryParse(id, out var page) && _titles.ContainsKey(page))
            Nav.NavigateTo(PagePath(page));
    });
    [JSInvokable] public Task OnGraphMenu(string id, double x, double y) => InvokeAsync(() =>
    {
        if (_disposed) return;
        if (_graph is null || !Guid.TryParse(id, out var page) || !_titles.ContainsKey(page)) { CloseMenu(); StateHasChanged(); return; }
        _menuPage = page; _menuX = x; _menuY = y; _renaming = false; _renameTitle = Title(page); _focusMenu = true;
        StateHasChanged();
    });
    private void BeginRename() { _renaming = true; _focusRename = true; }
    private void CloseMenu()
    {
        if (_menuPage.HasValue) _focusStage = true;
        _menuPage = null;
        _renaming = false;
    }
    private void MenuKey(KeyboardEventArgs e) { if (e.Key == "Escape") CloseMenu(); }
    private async Task RenameMenuPage()
    {
        if (_menuPage is not Guid id || _graph is not { CanRename: true } graph || _busy || string.IsNullOrWhiteSpace(_renameTitle)) return;
        _busy = true; _error = null;
        try
        {
            var page = await Client.Rename(id, _renameTitle.Trim(), _life.Token);
            if (_disposed || !ReferenceEquals(_graph, graph)) return;
            CloseMenu();
            _pages = _pages.Select(existing => existing.Id == id ? page : existing).ToArray();
            _collections = null;
            _graph = graph with
            {
                Title = graph.CollectionId == id ? page.Title : graph.Title,
                Nodes = graph.Nodes.Select(node => node.Id == id ? node with { Title = page.Title } : node).ToArray()
            };
            _titles[id] = page.Title;
            RefreshGraph();
        }
        catch (Exception ex) when (ex is NotionException or HttpRequestException or OperationCanceledException)
        {
            if (ex is not OperationCanceledException && !_disposed && ReferenceEquals(_graph, graph))
                _error = ex is NotionException notion ? notion.Message : "Titlen kunne ikke gemmes. Prøv igen.";
        }
        finally { _busy = false; }
    }
    private async Task Unmount()
    {
        _canvasGeneration++;
        _graphDirty = _optionsDirty = _fitGraph = false;
        _focusNode = null;
        if (_mounted && _module is not null)
        {
            _mounted = false;
            try { await _module.InvokeVoidAsync("destroy", _host); }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException) { }
        }
    }
    public async ValueTask DisposeAsync()
    {
        _disposed = true; Auth.AuthStateChanged -= AccountChanged; ViewAs.Changed -= AccountChanged;
        await _life.CancelAsync();
        try { await Unmount(); if (_module is not null) await _module.DisposeAsync(); } catch (JSDisconnectedException) { }
        _callbacks?.Dispose(); _life.Dispose();
    }
}
