using System.Text.Json;
using System.Globalization;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Emojis;
using Mercantec.Tech.Web.Notion;
using Mercantec.Tech.Web.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Notion;

public partial class Index
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private NotionClient Client { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Parameter] public Guid? PageId { get; set; }
    [SupplyParameterFromQuery(Name = "notice")] public string? MentionNotice { get; set; }
    [SupplyParameterFromQuery(Name = "hold")] public Guid? HoldQuery { get; set; }
    [SupplyParameterFromQuery(Name = "published")] public bool PublishedPreview { get; set; }
    [SupplyParameterFromQuery(Name = "workspace")] public Guid? WorkspaceQuery { get; set; }
    [SupplyParameterFromQuery(Name = "course")] public Guid? CourseQuery { get; set; }
    private WorkspaceRouteContext? _returnContext;
    private readonly CancellationTokenSource _life = new();
    private CancellationTokenSource? _requests;
    private CancellationTokenSource? _navigationRequests;
    private IJSObjectReference? _module;
    private Task<IJSObjectReference>? _moduleTask;
    private IJSObjectReference? _sidebarModule;
    private Task<IJSObjectReference>? _sidebarModuleTask;
    private DotNetObjectReference<Index>? _callbacks;
    private ElementReference _editorElement;
    private ElementReference _mountedElement;
    private ElementReference _workspaceElement;
    private ElementReference _sidebarMountedElement;
    private NotionHold[] _holds = [];
    private NotionPageSummary[] _pages = [];
    private NotionGroupView[] _groups = [];
    private NotionPageContext? _current;
    private Guid _holdId;
    private Guid? _newParent;
    private Guid[] _groupSelection = [];
    private int _groupPanelGeneration;
    private int _generation;
    private int _navigationGeneration = -1;
    private Guid? _pagesHold;
    private bool _pagesPublishedOnly, _holdsLoaded, _navigationLoading, _resourcesLoaded;
    private Task? _resourcesTask;
    private string? _navigationError;
    private string? _resourcesError;
    private List<PageRow> _pageRows = [];
    private List<PageRow> _virtualRows = [];
    private PageRow? _activeRow;
    private HashSet<Guid> _collections = [];
    private Dictionary<Guid, Guid?> _parents = [];
    private Dictionary<Guid, NotionPageSummary> _pagesById = [];
    private HashSet<Guid> _pagesWithChildren = [];
    private readonly HashSet<Guid> _expandedPages = [];
    private Guid? _sidebarScope, _revealedPage;
    private bool _revealedWithMetadata, _sidebarPublishedOnly;
    private string _pageSearch = "";
    private int _totalPages, _listVersion, _sidebarInteractions;
    private bool _sidebarOpen = true, _sidebarMobile, _sidebarReady, _sidebarStarting, _sidebarNeedsSync, _workspaceFocused;
    private string? _account, _loadedRoute, _error, _notice;
    private string _title = "", _newTitle = "", _status = "connecting", _statusDetail = "";
    private bool _loading = true, _busy, _creating, _needsMount, _mounted, _disposed, _showGroups, _scrollCenterToTop;
    private bool _savingTitle;
    private Guid? _codexHistoryPageId;
    private Task<bool>? _titleSaveTask;
    private bool CanUse => Auth.IsAuthenticated;
    private bool ForceReadOnly => PublishedPreview || ViewAs.IsActive;
    private bool CanManageHold => !ViewAs.IsActive && (_current?.CanEdit == true || _holds.Any(h => h.Id == _holdId && h.CanManage));
    private bool IsEditing => _current?.CanEdit == true && !ForceReadOnly;
    private bool Authoring => CanManageHold && !ForceReadOnly;
    private string RouteKey => $"{PageId}:{HoldQuery}:{WorkspaceQuery}:{CourseQuery}:{ForceReadOnly}";
    private string StatusLabel => _savingTitle ? "Gemmer titel…" : _status switch { "saved" => "Gemt i skyen", "saving" => "Gemmer…", "synced" => "Forbundet", "offline" => "Forbindelsen er afbrudt", "error" => "Forbindelsen kræver opmærksomhed", _ => "Forbinder…" };
    private IEnumerable<NotionPageSummary> VisiblePages => ForceReadOnly ? _pages.Where(p => p.PublishedAtUtc is not null) : _pages;
    private string PageHref(Guid id, bool? publishedOnly = null) => WorkspaceNavigation.Page(id, _returnContext?.WorkspaceId, _returnContext?.CourseId, publishedOnly ?? ForceReadOnly, _holdId);
    private string GraphHref(Guid id) => WorkspaceNavigation.Graph(id, _returnContext?.WorkspaceId, _returnContext?.CourseId, ForceReadOnly, _holdId);
    private string HoldGraphHref => WorkspaceNavigation.HoldGraph(_holdId, _returnContext?.WorkspaceId, _returnContext?.CourseId, ForceReadOnly);
    private string WorkspaceHref(string view = "content") => _holdId == Guid.Empty ? "/notion" : _returnContext?.View(view) ?? WorkspaceNavigation.Hold(_holdId, view);
    private bool IsCollection(NotionPageSummary page) => _collections.Contains(page.Id);
    private Guid CollectionRoot(Guid id)
    {
        var visited = new HashSet<Guid>();
        while (visited.Add(id) && _parents.TryGetValue(id, out var parentId) && parentId is Guid parent && _parents.ContainsKey(parent))
            id = parent;
        return id;
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        PageId = HoldQuery = WorkspaceQuery = CourseQuery = null;
        PublishedPreview = false;
        return base.SetParametersAsync(parameters);
    }

    protected override void OnInitialized() { Auth.AuthStateChanged += AccountChanged; ViewAs.Changed += AccountChanged; }
    protected override async Task OnParametersSetAsync()
    {
        if (_account is null || !Auth.IsInitialized || !CanUse || _loadedRoute == RouteKey) return;
        _codexHistoryPageId = null;
        await Load();
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await SyncAccount();
        if (_disposed) return;
        if (!CanUse || (!_loading && _holdsLoaded && _holds.Length == 0 && _current is null && _holdId == Guid.Empty))
        { await UnmountSidebar(); return; }
        var sidebarSetup = InitializeSidebar();
        if (_needsMount && _current is not null && CanUse && !_disposed) await MountEditor();
        await sidebarSetup;
        if (_sidebarNeedsSync && _sidebarModule is not null && !_disposed)
        {
            _sidebarNeedsSync = false;
            try { await _sidebarModule.InvokeVoidAsync("setOpen", _sidebarMountedElement, _sidebarOpen); }
            catch (JSException) { /* The visible toggle still works without browser enhancements. */ }
        }
        if (_scrollCenterToTop && _sidebarModule is not null && !_disposed)
        {
            _scrollCenterToTop = false;
            try { await _sidebarModule.InvokeVoidAsync("scrollCenterTop", _sidebarMountedElement); }
            catch (JSException) { /* The center remains scrollable without the enhanced shortcut. */ }
        }
    }
    private async Task MountEditor()
    {
        if (!_needsMount || _current is null || !CanUse || _disposed) return;
        _needsMount = false;
        var generation = _generation;
        var account = _account; var page = _current.PageId;
        var element = _editorElement;
        try
        {
            _moduleTask ??= Js.InvokeAsync<IJSObjectReference>("import", EditorAssets.Script).AsTask();
            var module = await _moduleTask;
            if (generation != _generation || _disposed) return;
            _module = module;
            var token = await Auth.GetValidAccessTokenAsync();
            if (generation != _generation || account != _account || page != _current?.PageId || !CanUse || _disposed) return;
            if (string.IsNullOrEmpty(token)) throw new NotionException("Log ind igen for at åbne dokumentet.");
            _callbacks ??= DotNetObjectReference.Create(this);
            _mountedElement = element; _mounted = true;
            await module.InvokeVoidAsync(IsEditing ? "mount" : "mountReadOnly", element, EditorContext(), token, _callbacks);
            if (generation != _generation || _disposed)
            { await module.InvokeVoidAsync("destroy", element); return; }
        }
        catch (Exception ex) when (ex is JSException or NotionException or HttpRequestException)
        { if (generation == _generation) { if (_moduleTask?.IsFaulted == true) _moduleTask = null; _status = "error"; _error = ex is NotionException ? ex.Message : "Dokumentet kunne ikke åbnes. Prøv igen, eller kontakt administratoren."; } }
        // Navigation is secondary: start it only after the active editor has mounted.
        if (generation == _generation && _mounted && _navigationGeneration != generation)
        {
            _navigationGeneration = generation;
            _ = LoadNavigation(generation, _requests!.Token);
        }
        if (!_disposed) await InvokeAsync(StateHasChanged);
    }
    private async Task InitializeSidebar()
    {
        if (_sidebarStarting || string.IsNullOrEmpty(_workspaceElement.Id) || _sidebarReady && _sidebarMountedElement.Id == _workspaceElement.Id) return;
        _sidebarStarting = true;
        var interactions = _sidebarInteractions;
        var element = _workspaceElement;
        try
        {
            _sidebarModuleTask ??= Js.InvokeAsync<IJSObjectReference>("import", "/js/notion-sidebar.js?v=ide-edge-20261108").AsTask();
            var module = await _sidebarModuleTask;
            if (_disposed) return;
            if (_sidebarModule is not null && !string.IsNullOrEmpty(_sidebarMountedElement.Id))
                await _sidebarModule.InvokeVoidAsync("destroy", _sidebarMountedElement);
            _sidebarModule = module;
            _sidebarMountedElement = element;
            _callbacks ??= DotNetObjectReference.Create(this);
            var state = await module.InvokeAsync<SidebarPreferences?>("mount", element, _callbacks);
            if (_disposed) return;
            if (state is not null)
            {
                _sidebarMobile = state.Mobile;
                if (interactions == _sidebarInteractions) _sidebarOpen = !state.Mobile && state.DesktopOpen;
            }
            _sidebarNeedsSync = true;
        }
        catch (JSException) { /* Storage restrictions must not prevent using the menu. */ }
        finally
        {
            if (!_disposed) { _sidebarReady = true; _sidebarStarting = false; await InvokeAsync(StateHasChanged); }
        }
    }
    private void SetSidebarOpen(bool open)
    {
        ++_sidebarInteractions;
        if (open && _workspaceFocused) { _workspaceFocused = false; _ = SyncWorkspaceFocus(); }
        if (_sidebarOpen == open) return;
        _sidebarOpen = open; _sidebarNeedsSync = true;
    }
    private void ToggleSidebar() => SetSidebarOpen(_workspaceFocused || !_sidebarOpen);
    private async Task ToggleWorkspaceFocus()
    {
        _workspaceFocused = !_workspaceFocused;
        await SyncWorkspaceFocus();
    }
    private async Task SyncWorkspaceFocus()
    {
        if (_sidebarModule is null || _disposed) return;
        try { await _sidebarModule.InvokeVoidAsync("setFocus", _sidebarMountedElement, _workspaceFocused); }
        catch (JSException) { /* The document remains available without enhanced layout controls. */ }
    }
    private async Task UnmountSidebar()
    {
        if (_sidebarModule is not null && !string.IsNullOrEmpty(_sidebarMountedElement.Id))
        {
            try { await _sidebarModule.InvokeVoidAsync("destroy", _sidebarMountedElement); }
            catch (JSException) { }
        }
        _sidebarMountedElement = default; _sidebarReady = false;
    }
    [JSInvokable] public Task CloseNotionSidebar() => InvokeAsync(() => { if (!_disposed) { SetSidebarOpen(false); StateHasChanged(); } });
    [JSInvokable] public Task OnNotionFocusMode(bool focused) => InvokeAsync(() =>
    {
        if (_disposed || _workspaceFocused == focused) return;
        _workspaceFocused = focused; StateHasChanged();
    });
    [JSInvokable] public Task OnNotionSidebarViewport(bool mobile, bool desktopOpen) => InvokeAsync(() =>
    {
        if (_disposed) return;
        _sidebarMobile = mobile; ++_sidebarInteractions;
        _sidebarOpen = !mobile && desktopOpen; _sidebarNeedsSync = true;
        StateHasChanged();
    });
    private void SearchPages(ChangeEventArgs args) { _pageSearch = args.Value?.ToString() ?? ""; ++_listVersion; RebuildPageRows(); }
    private void ClearPageSearch() { _pageSearch = ""; ++_listVersion; RebuildPageRows(); }
    private void SearchKeyDown(KeyboardEventArgs args)
    {
        if (args.Key != "Escape") return;
        if (_pageSearch.Length > 0) ClearPageSearch(); else SetSidebarOpen(false);
    }
    private void ToggleBranch(Guid id)
    {
        if (!_expandedPages.Remove(id)) _expandedPages.Add(id);
        RebuildPageRows();
    }
    private string? ParentTitle(NotionPageSummary page) => page.ParentPageId is Guid parent && _pagesById.TryGetValue(parent, out var value) ? value.Title : null;
    private object EditorContext() => new
    {
        pageId = _current!.PageId, holdId = _current.HoldId, collectionId = _current.CollectionId, title = _current.Title,
        workspaceId = _returnContext?.WorkspaceId, courseId = _returnContext?.CourseId,
        canEdit = IsEditing, members = _current.Members, polls = _current.Polls,
        pages = _current.Pages ?? [], groups = _groups, resourcesLoaded = _resourcesLoaded, publishedStateBase64 = _current.PublishedStateBase64,
        publishedAtUtc = _current.PublishedAtUtc, updatedAtUtc = _current.UpdatedAtUtc,
        userName = Auth.DisplayName ?? "Bruger", userSub = Auth.Subject, drawings = _current.Drawings
    };
    private void AccountChanged() => _ = InvokeAsync(SyncAccount);
    private async Task SyncAccount()
    {
        if (!Auth.IsInitialized || _disposed) return;
        var account = $"{Auth.Subject}:{ViewAs.IsActive}";
        if (_account == account) return;
        _account = account;
        _expandedPages.Clear(); _pageSearch = ""; _sidebarScope = null; _revealedPage = null;
        _holds = []; _pages = []; _groups = []; _showGroups = false; _holdsLoaded = false; _pagesHold = null;
        if (CanUse) await Load();
        else { ++_generation; _requests?.Cancel(); await Unmount(); _current = null; _loading = false; }
        if (!_disposed) StateHasChanged();
    }
    private async Task Load()
    {
        var generation = ++_generation;
        var old = _requests;
        _requests = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        var ct = _requests.Token; var account = _account;
        if (old is not null) { old.Cancel(); old.Dispose(); }
        await Unmount();
        if (!IsCurrent(generation, ct)) return;
        _loadedRoute = RouteKey; _loading = true; _error = null; _notice = null; _current = null; _returnContext = null;
        _groups = []; _resourcesLoaded = false; _resourcesTask = null; _navigationLoading = false; _navigationError = null; _resourcesError = null; _showGroups = false; _scrollCenterToTop = false;
        try
        {
            if (PageId is Guid id)
            {
                var context = await FetchContext(id, ct);
                if (!IsCurrent(generation, ct) || account != _account) return;
                _holdId = context.HoldId ?? Guid.Empty;
                ResetSidebarScope(context.CollectionId);
                if (_pagesHold != (context.CollectionId ?? _holdId) || _pagesPublishedOnly != ForceReadOnly) { _pages = []; _pagesHold = null; }
                SelectContext(context);
            }
            else
            {
                // Without a page ID, metadata is necessary to choose the first page.
                if (HoldQuery is Guid requested) _holdId = requested;
                else
                {
                    var holds = await Client.Holds(ct);
                    if (!IsCurrent(generation, ct)) return;
                    _holds = holds; _holdsLoaded = true; _holdId = holds.FirstOrDefault()?.Id ?? Guid.Empty;
                }
                if (_holdId != Guid.Empty)
                {
                    ResetSidebarScope();
                    var pages = await Client.Pages(_holdId, ct, ForceReadOnly);
                    if (!IsCurrent(generation, ct)) return;
                    SetPages(pages);
                    if (VisiblePages.FirstOrDefault() is { } first)
                    {
                        var context = await FetchContext(first.Id, ct);
                        if (!IsCurrent(generation, ct)) return;
                        SelectContext(context);
                    }
                }
            }
        }
        catch (Exception ex) when (IsExpected(ex)) { if (IsCurrent(generation, ct)) Report(ex); }
        finally
        {
            if (IsCurrent(generation, ct))
            {
                _loading = false;
                if (_current is null && _error is null && _holdId != Guid.Empty)
                { _navigationGeneration = generation; _ = LoadNavigation(generation, ct); }
            }
        }
    }
    private bool IsCurrent(int generation, CancellationToken ct) => !_disposed && !ct.IsCancellationRequested && generation == _generation;
    private void ResetSidebarScope(Guid? collectionId = null)
    {
        var scopeId = collectionId ?? _holdId;
        if (_sidebarScope == scopeId && _sidebarPublishedOnly == ForceReadOnly) return;
        _sidebarScope = scopeId; _sidebarPublishedOnly = ForceReadOnly; _expandedPages.Clear(); _pageSearch = ""; _revealedPage = null; _revealedWithMetadata = false;
    }
    private Task<NotionPageContext> FetchContext(Guid id, CancellationToken ct) => ForceReadOnly ? Client.PublishedContext(id, ct, false) : Client.Context(id, ct, false);
    private void SelectContext(NotionPageContext context)
    {
        _current = context;
        _pages = ReconcileActivePage(_pages);
        _current = context with { Pages = _pages.Select(p => new NotionPageLink(p.Id, p.Title, p.ParentPageId)).ToArray() };
        _title = context.Title; _status = "connecting"; _statusDetail = ""; _needsMount = true; _creating = false;
        RebuildPageRows();
    }

    private async Task LoadNavigation(int generation, CancellationToken ct, bool refresh = false)
    {
        if (!IsCurrent(generation, ct)) return;
        var previous = _navigationRequests;
        _navigationRequests = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ct = _navigationRequests.Token;
        if (previous is not null) { previous.Cancel(); previous.Dispose(); }
        _navigationLoading = true; _navigationError = null;
        var holdId = _holdId; var collectionId = _current?.CollectionId; var scopeId = collectionId ?? holdId;
        // Each independent metadata request handles its own failure. A slow hold
        // selector must not prevent the page list or the document from appearing.
        async Task LoadPart(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) when (IsExpected(ex))
            { if (IsCurrent(generation, ct)) _navigationError = "Sideoversigten kunne ikke hentes helt. Du kan fortsætte på den åbne side."; }
            finally { if (IsCurrent(generation, ct)) await InvokeAsync(StateHasChanged); }
        }
        var parts = new List<Task>();
        if (holdId != Guid.Empty && (!_holdsLoaded || refresh)) parts.Add(LoadPart(async () =>
        {
            var holds = await Client.Holds(ct);
            if (IsCurrent(generation, ct)) { _holds = holds; _holdsLoaded = true; }
        }));
        if (_pagesHold != scopeId || _pagesPublishedOnly != ForceReadOnly || refresh) parts.Add(LoadPart(async () =>
        {
            var pages = collectionId is Guid collection ? await Client.CollectionPages(collection, ct, ForceReadOnly) : await Client.Pages(holdId, ct, ForceReadOnly);
            if (!IsCurrent(generation, ct)) return;
            SetPages(pages);
            await PushEditorContext(generation, ct);
        }));
        if (holdId != Guid.Empty && (HoldQuery is null || HoldQuery == holdId) &&
            (WorkspaceNavigation.NonEmpty(WorkspaceQuery) is not null || WorkspaceNavigation.NonEmpty(CourseQuery) is not null))
            parts.Add(LoadPart(async () =>
            {
                var navigation = await Client.Navigation(holdId, ct, ForceReadOnly);
                if (!IsCurrent(generation, ct) || navigation.HoldId != holdId) return;
                var matches = WorkspaceNavigation.NonEmpty(WorkspaceQuery) is not Guid requested || requested == navigation.WorkspaceId;
                _returnContext = new(navigation.WorkspaceId, holdId, matches && navigation.CourseIds.Contains(CourseQuery ?? Guid.Empty) ? CourseQuery : null);
                await PushEditorContext(generation, ct);
            }));
        await Task.WhenAll(parts);
        if (IsCurrent(generation, ct)) { _navigationLoading = false; await InvokeAsync(StateHasChanged); }
    }
    private void SetPages(NotionPageSummary[] pages)
    {
        pages = ReconcileActivePage(pages);
        _pages = pages; _pagesHold = _current?.CollectionId ?? _holdId; _pagesPublishedOnly = ForceReadOnly;
        if (_current is not null) _current = _current with { Pages = pages.Select(p => new NotionPageLink(p.Id, p.Title, p.ParentPageId)).ToArray() };
        RebuildPageRows();
    }
    private NotionPageSummary[] ReconcileActivePage(NotionPageSummary[] pages) => _current is not { } current ? pages
        : pages.Select(p => p.Id == current.PageId ? p with
        {
            Title = current.Title, UpdatedAtUtc = current.UpdatedAtUtc, PublishedAtUtc = current.PublishedAtUtc,
            HasUnpublishedChanges = current.CanEdit && current.PublishedAtUtc is not null && (current.DraftRevision != current.PublishedRevision || current.Title != current.PublishedTitle)
        } : p).ToArray();
    private async Task PushEditorContext(int generation, CancellationToken ct)
    {
        if (IsCurrent(generation, ct) && _mounted && _module is not null && _current is not null)
            await _module.InvokeVoidAsync("setContext", _editorElement, EditorContext());
    }
    private Task RetryNavigation() => LoadNavigation(_generation, _requests?.Token ?? _life.Token, true);

    [JSInvokable] public async Task<bool> EnsureNotionResources()
    {
        await InvokeAsync(async () =>
        {
            if (_disposed || _current is null || !CanUse || _resourcesLoaded) return;
            if (_resourcesTask is { IsCompleted: false } pending) { await pending; return; }
            _resourcesTask = LoadResources(_generation, _requests!.Token);
            await _resourcesTask;
        });
        return _resourcesLoaded && !_disposed;
    }
    private async Task LoadResources(int generation, CancellationToken ct)
    {
        _resourcesError = null;
        if (_holdId == Guid.Empty) { _resourcesLoaded = true; await PushEditorContext(generation, ct); return; }
        var resourcesTask = Client.EditorResources(_holdId, ct, ForceReadOnly);
        var groupsTask = Client.Groups(_holdId, ct);
        try
        {
            await Task.WhenAll(resourcesTask, groupsTask);
            if (!IsCurrent(generation, ct) || _current is null) return;
            var resources = await resourcesTask;
            _current = _current with { Members = resources.Members, Polls = resources.Polls };
            _groups = await groupsTask; _resourcesLoaded = true;
            await PushEditorContext(generation, ct);
        }
        catch (Exception ex) when (IsExpected(ex)) { if (IsCurrent(generation, ct)) _resourcesError = "Holdets elever, grupper og afstemninger kunne ikke hentes. Prøv igen."; }
        finally { if (IsCurrent(generation, ct)) await InvokeAsync(StateHasChanged); }
    }
    private async Task<bool> CanLeave()
    {
        if (!await SaveTitle()) return false;
        if (!_mounted || _module is null) return true;
        if (!await _module.InvokeAsync<bool>("hasUnsavedChanges", _editorElement)) return true;
        _error = "Vent på, at ændringer og uploads er gemt i skyen. Hold siden åben, hvis forbindelsen er afbrudt.";
        return false;
    }
    private async Task BeforeNavigation(LocationChangingContext context) { if (!await CanLeave()) context.PreventNavigation(); }
    private async Task ChangeHold(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var id) && id != _holdId && await CanLeave())
        {
            if (_sidebarMobile) SetSidebarOpen(false);
            Nav.NavigateTo(WorkspaceNavigation.Editor(id, publishedOnly: ForceReadOnly));
        }
    }
    private async Task OpenPage(Guid id)
    {
        if (_current?.PageId == id) { if (_sidebarMobile) SetSidebarOpen(false); return; }
        if (!await CanLeave()) return;
        var href = PageHref(id);
        if (_sidebarMobile) SetSidebarOpen(false);
        Nav.NavigateTo(href);
    }
    private void BeginCreate(bool child) { if (!Authoring) return; SetSidebarOpen(true); _newParent = child ? _current?.PageId : null; _newTitle = ""; _creating = true; }
    private async Task CreatePage()
    {
        if (!Authoring || string.IsNullOrWhiteSpace(_newTitle) || !await CanLeave()) return;
        await Work(async ct =>
        {
            var holdId = _current is null ? WorkspaceNavigation.NonEmpty(_holdId) : _current.HoldId;
            var page = await Client.Create(holdId, _newTitle.Trim(), _newParent, ct, _current?.CollectionId);
            _creating = false; _pagesHold = null;
            if (_sidebarMobile) SetSidebarOpen(false);
            Nav.NavigateTo(PageHref(page.Id));
        });
    }
    private void InsertNewTitleEmoji(CustomEmojiItem emoji) => _newTitle = EmojiInsert.Append(_newTitle, emoji.Name, 200);
    private async Task RenamePage() => await SaveTitle();
    private async Task<bool> SaveTitle()
    {
        while (!_disposed && IsEditing && _current is not null)
        {
            if (_titleSaveTask is { IsCompleted: false } pending)
            {
                if (!await pending) return false;
                continue;
            }
            var title = _title.Trim();
            if (title == _current.Title) return true;
            if (title.Length == 0) { _error = "Skriv en titel."; return false; }
            _titleSaveTask = SaveTitleCore(_current.PageId, _account, title);
            if (!await _titleSaveTask) return false;
        }
        return !_disposed;
    }
    private async Task<bool> SaveTitleCore(Guid id, string? account, string title)
    {
        _savingTitle = true; _error = null;
        try
        {
            var ct = _requests?.Token ?? _life.Token;
            await Client.Rename(id, title, ct);
            if (_disposed || _current?.PageId != id || _account != account) return false;
            await UpdateContext(ct);
            return !_disposed && _current?.PageId == id && _account == account;
        }
        catch (Exception ex) when (IsExpected(ex)) { Report(ex); return false; }
        finally { _savingTitle = false; }
    }
    private async Task UpdateContext(CancellationToken ct)
    {
        if (_current is null) return;
        var generation = _generation;
        var id = _current.PageId;
        var originalTitle = _current.Title;
        var context = await FetchContext(id, ct);
        if (!IsCurrent(generation, ct) || _current?.PageId != id) return;
        _current = context with { Members = _current.Members, Polls = _current.Polls, Pages = _current.Pages };
        _title = _title.Trim() == originalTitle || _title.Trim() == context.Title ? context.Title : _title;
        SetPages(_pages.Select(p => p.Id == id ? p with
        {
            Title = context.Title, UpdatedAtUtc = context.UpdatedAtUtc, PublishedAtUtc = context.PublishedAtUtc,
            HasUnpublishedChanges = context.PublishedAtUtc is not null && (context.DraftRevision != context.PublishedRevision || context.Title != context.PublishedTitle)
        } : p).ToArray());
        await PushEditorContext(generation, ct);
    }
    private async Task RefreshContext() => await Work(async ct =>
    {
        var generation = _generation;
        await UpdateContext(ct);
        if (!IsCurrent(generation, ct)) return;
        await LoadNavigation(generation, ct, true);
        if (_resourcesLoaded) { _resourcesLoaded = false; await LoadResources(generation, ct); }
    });
    private async Task PublishPage()
    {
        if (!IsEditing || _current is null || _module is null || !_mounted || !await CanLeave()) return;
        await Work(async ct =>
        {
            var id = _current.PageId;
            var data = await _module.InvokeAsync<PublicationData>("getPublicationData", _editorElement);
            var context = await Client.Context(id, ct, false);
            await Client.Publish(id, context.DraftRevision, data.ExpectedFingerprint, data.AssetIds, ct, data.Drawings);
            await UpdateContext(ct);
            _notice = "Siden er udgivet.";
        });
    }
    private async Task UnpublishPage()
    {
        if (!IsEditing || _current is null) return;
        await Work(async ct => { await Client.Unpublish(_current.PageId, ct); await UpdateContext(ct); _notice = "Udgivelsen er trukket tilbage."; });
    }
    private async Task PresentPage()
    {
        if (!_mounted || _module is null || !await SaveTitle()) return;
        try { await _module.InvokeVoidAsync("setPresentation", _editorElement, true); }
        catch (JSException ex) { Report(ex); }
    }
    private async Task CopyLink()
    {
        if (_current is null) return;
        try { await Js.InvokeVoidAsync("navigator.clipboard.writeText", Nav.ToAbsoluteUri(PageHref(_current.PageId)).ToString()); _notice = "Linket er kopieret."; }
        catch (JSException) { _error = "Linket kunne ikke kopieres. Brug adressen i browserens adresselinje."; }
    }
    private async Task Retry() { if (!await CanLeave()) return; _pagesHold = null; _holdsLoaded = false; await Load(); }
    private async Task Work(Func<CancellationToken, Task> action)
    {
        if (_busy || _disposed) return;
        _busy = true; _error = null; _notice = null;
        try { await action(_requests?.Token ?? _life.Token); }
        catch (Exception ex) when (IsExpected(ex)) { Report(ex); }
        finally { _busy = false; }
    }
    [JSInvokable] public Task<string?> GetNotionToken() => Auth.IsAuthenticated ? Auth.GetValidAccessTokenAsync() : Task.FromResult<string?>(null);
    [JSInvokable] public Task OnNotionStatus(string status, string detail) => InvokeAsync(() => { if (_disposed) return; _status = status; _statusDetail = detail; StateHasChanged(); });
    [JSInvokable] public Task OnNotionCreateGroup(string membershipIdsJson) => InvokeAsync(async () =>
    {
        if (!IsEditing || _disposed || _holdId == Guid.Empty) return;
        var generation = _generation;
        if (!await EnsureNotionResources() || generation != _generation) return;
        var ids = JsonSerializer.Deserialize<Guid[]>(membershipIdsJson) ?? [];
        _groupSelection = ids.Where(id => _current!.Members.Any(m => m.Id == id)).Distinct().ToArray();
        _groupPanelGeneration++; _showGroups = true; _scrollCenterToTop = true; StateHasChanged();
    });
    private void RebuildPageRows()
    {
        var pages = VisiblePages.OrderBy(p => p.Title).ToArray();
        if (_current is not null && !pages.Any(p => p.Id == _current.PageId))
            pages = [new(_current.PageId, _current.HoldId, _current.Title, null, _current.UpdatedAtUtc, _current.PublishedAtUtc, CollectionId: _current.CollectionId), .. pages];
        _parents = pages.ToDictionary(p => p.Id, p => p.ParentPageId);
        _pagesById = pages.ToDictionary(p => p.Id);
        var children = pages.ToLookup(p => p.ParentPageId ?? Guid.Empty);
        _pagesWithChildren = pages.Where(p => children.Contains(p.Id)).Select(p => p.Id).ToHashSet();
        _collections = pages.Where(p => p.ParentPageId is null || !_parents.ContainsKey(p.ParentPageId.Value) || children.Contains(p.Id)).Select(p => p.Id).ToHashSet();
        if (_current is not null && (_revealedPage != _current.PageId || !_revealedWithMetadata))
        {
            var seen = new HashSet<Guid>();
            var child = _current.PageId;
            while (seen.Add(child) && _parents.TryGetValue(child, out var parent) && parent is Guid id)
            { _expandedPages.Add(id); child = id; }
            _revealedPage = _current.PageId; _revealedWithMetadata = _pages.Any(p => p.Id == _current.PageId);
        }
        var visited = new HashSet<Guid>();
        var rows = new List<PageRow>(pages.Length);
        var matches = new List<PageRow>();
        var query = _pageSearch.Trim();
        // Iterative traversal also handles deep trees, orphaned pages and cycles.
        void Walk(NotionPageSummary root)
        {
            var pending = new Stack<(NotionPageSummary Page, int Depth, bool Visible)>();
            pending.Push((root, 0, true));
            while (pending.TryPop(out var entry))
            {
                if (!visited.Add(entry.Page.Id)) continue;
                if (entry.Visible) rows.Add(new(entry.Page, Math.Min(entry.Depth, 4)));
                if (query.Length > 0 && CultureInfo.GetCultureInfo("da-DK").CompareInfo.IndexOf(entry.Page.Title, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                    matches.Add(new(entry.Page, 0));
                foreach (var child in children[entry.Page.Id].Reverse()) pending.Push((child, entry.Depth + 1, entry.Visible && _expandedPages.Contains(entry.Page.Id)));
            }
        }
        foreach (var root in pages.Where(p => p.ParentPageId is null || !_parents.ContainsKey(p.ParentPageId.Value))) Walk(root);
        foreach (var orphan in pages) if (!visited.Contains(orphan.Id)) Walk(orphan);
        _totalPages = pages.Length;
        _pageRows = query.Length > 0 ? matches : rows;
        _activeRow = _pageRows.FirstOrDefault(p => p.Page.Id == _current?.PageId);
        _virtualRows = _pageRows.Where(p => p.Page.Id != _current?.PageId).ToList();
    }
    private sealed record PageRow(NotionPageSummary Page, int Depth);
    private static bool IsExpected(Exception ex) => ex is NotionException or HttpRequestException or OperationCanceledException or JSException or JsonException;
    private void Report(Exception ex) { if (!_disposed && ex is not OperationCanceledException) _error = ex is NotionException ? ex.Message : ex is JSException ? "Handlingen kunne ikke gennemføres. Vent på, at kladden er gemt, og prøv igen." : "Handlingen kunne ikke gennemføres. Kontrollér forbindelsen, og prøv igen."; }
    private async Task Unmount()
    {
        _needsMount = false;
        var mounted = _mounted; _mounted = false;
        if (mounted && _module is not null) await _module.InvokeVoidAsync("destroy", _mountedElement);
    }
    public async ValueTask DisposeAsync()
    {
        _disposed = true; Auth.AuthStateChanged -= AccountChanged; ViewAs.Changed -= AccountChanged;
        await _life.CancelAsync(); _navigationRequests?.Dispose(); _requests?.Dispose();
        try
        {
            await Unmount();
            if (_moduleTask is not null) await (await _moduleTask).DisposeAsync();
            else if (_module is not null) await _module.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException) { }
        try
        {
            await UnmountSidebar();
            if (_sidebarModuleTask is not null) await (await _sidebarModuleTask).DisposeAsync();
        }
        catch (JSException) { }
        _callbacks?.Dispose(); _life.Dispose();
    }
    private sealed record PublicationData(string ExpectedFingerprint, Guid[] AssetIds, DrawingPublicationRequest[]? Drawings = null);
    private sealed record SidebarPreferences(bool Mobile, bool DesktopOpen);
}
