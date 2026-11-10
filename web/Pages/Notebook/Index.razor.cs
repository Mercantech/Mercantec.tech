using System.Net;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Activity;
using Mercantec.Tech.Web.Notebooks;
using Mercantec.Tech.Web.Notion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Notebook;

public partial class Index
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [Inject] private AuthRedirect Redirect { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private NotebookClient Client { get; set; } = default!;
    [Inject] private ActionCenterService Actions { get; set; } = default!;
    [Inject] private NotionClient HoldsClient { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Parameter] public Guid? PageId { get; set; }
    [Parameter] public Guid? TemplateId { get; set; }
    [SupplyParameterFromQuery(Name = "owner")] public Guid? NotebookId { get; set; }
    [SupplyParameterFromQuery(Name = "comments")] public bool RequestedComments { get; set; }
    [SupplyParameterFromQuery(Name = "noticeKind")] public string? NoticeKind { get; set; }
    [SupplyParameterFromQuery(Name = "notice")] public string? NoticeQuery { get; set; }
    private Guid? NoticeId => Guid.TryParse(NoticeQuery, out var id) && id != Guid.Empty ? id : null;
    private readonly HashSet<(string Kind, Guid Id)> _readNotifications = [];
    private int? _readingNotificationsGeneration;
    private Guid? _loadedNotebookAccess;
    private readonly CancellationTokenSource _life = new();
    private readonly Dictionary<Guid, string> _commentDrafts = [];
    private CancellationTokenSource? _requests;
    private IJSObjectReference? _editor, _lifecycle;
    private DotNetObjectReference<Index>? _callbacks;
    private ElementReference _editorElement;
    private NotebookInfo? _mine;
    private NotebookDocumentContext? _current;
    private NotebookPageSummary[] _pages = [], _shared = [], _backlinks = [];
    private NotebookTemplateView[] _templates = [];
    private NotebookCommentView[] _comments = [];
    private NotebookShareView[] _shares = [];
    private NotebookUser[] _users = [];
    private NotionHold[] _holds = [];
    private NotebookGraph? _graph;
    private Guid? _notebookId, _newParent;
    private Guid _entryRequest = Guid.NewGuid();
    private string? _account, _route, _error, _notice;
    private string _title = "", _tags = "", _cadence = "daily";
    private string _search = "", _kind = "", _tag = "", _newTitle = "", _newCadence = "daily", _comment = "", _userSearch = "", _shareScope = "page";
    private DateOnly _period;
    private string _entryHold = "";
    private string _status = "connecting", _statusDetail = "", _ownerName = "";
    private int _total, _generation;
    private bool _busy, _loading = true, _mounted, _needsMount, _disposed, _refreshing;
    private bool _creating, _showJournal, _showComments, _showShares, _showGraph, _sidebarOpen, _usersSearched;
    private bool TemplateMode => Nav.ToBaseRelativePath(Nav.Uri).StartsWith("teacher/notebook-templates", StringComparison.Ordinal);
    private bool IsTeacher => Redirect.CurrentAppRole() == AppRole.Underviser;
    private bool CanUse => Auth.IsAuthenticated && !ViewAs.IsActive;
    private bool Own => _notebookId is not null && _notebookId == _mine?.Id;
    private bool Editing => _current?.CanEdit == true && CanUse;
    private string RouteKey => $"{PageId}:{TemplateId}:{NotebookId}:{RequestedComments}:{NoticeKind}:{NoticeQuery}:{TemplateMode}:{Nav.ToAbsoluteUri(Nav.Uri).AbsolutePath == "/journal"}";
    private NotebookTemplateView? CurrentTemplate => _templates.FirstOrDefault(t => t.Id == TemplateId);
    private string Heading => TemplateMode ? "Logbogsskabeloner" : Own ? "Min notesbog" : _ownerName.Length > 0 ? $"{_ownerName}s notesbog" : "Notesbog";
    private string StatusLabel => _status switch { "saved" => "Gemt i skyen", "saving" => "Gemmer…", "synced" => "Forbundet", "offline" => "Forbindelsen er afbrudt", "error" => "Kunne ikke gemme", _ => "Forbinder…" };
    private bool MetadataDirty => Editing && _current is not null && (_title.Trim() != _current.Title ||
        (!TemplateMode && !ParsedTags().SequenceEqual(_current.Tags.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)) ||
        TemplateMode && CurrentTemplate is { } t && _cadence != t.Cadence);

    protected override void OnInitialized()
    {
        Auth.AuthStateChanged += AccountChanged;
        ViewAs.Changed += AccountChanged;
        Actions.Changed += NotificationStateChanged;
        _ = Poll();
    }
    public override Task SetParametersAsync(ParameterView parameters)
    {
        PageId = null; TemplateId = null; NotebookId = null;
        RequestedComments = false; NoticeKind = NoticeQuery = null;
        return base.SetParametersAsync(parameters);
    }
    protected override async Task OnParametersSetAsync()
    {
        if (Auth.IsInitialized && CanUse && _account is not null && _route != RouteKey) { await Unmount(); await Load(); }
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) { await SyncAccount(); return; }
        await ReadVisibleNotifications();
        if (!_needsMount || _current is null || !CanUse || _disposed) return;
        if (!Editing && string.IsNullOrEmpty(_current.StateBase64)) return;
        _needsMount = false;
        var id = _current.DocumentId;
        var generation = _generation;
        try
        {
            _editor ??= await Js.InvokeAsync<IJSObjectReference>("import", EditorAssets.Script);
            _callbacks ??= DotNetObjectReference.Create(this);
            var token = await Auth.GetValidAccessTokenAsync();
            if (generation != _generation || id != _current?.DocumentId || !CanUse || _disposed) return;
            if (string.IsNullOrEmpty(token)) throw new NotebookException("Log ind igen for at åbne notesbogen.");
            await _editor.InvokeVoidAsync(Editing ? "mount" : "mountReadOnly", _editorElement, EditorContext(), token, _callbacks);
            _mounted = true;
            _lifecycle ??= await Js.InvokeAsync<IJSObjectReference>("import", "/js/notebook-lifecycle.js");
            await _lifecycle.InvokeVoidAsync("watch", _callbacks);
        }
        catch (Exception ex) when (Expected(ex)) { Report(ex); }
        if (!_disposed) await InvokeAsync(StateHasChanged);
    }
    private object EditorContext() => new
    {
        domain = "notebook", documentName = "notebook:" + _current!.DocumentId,
        pageId = _current.DocumentId, collectionId = _current.NotebookId, holdId = _current.HoldId?.ToString() ?? "", title = _current.Title,
        canEdit = Editing, lifecycleVersion = _current.LifecycleVersion, members = Array.Empty<object>(), polls = Array.Empty<object>(), groups = Array.Empty<object>(),
        pages = _current.Pages, publishedStateBase64 = _current.StateBase64, stateBase64 = _current.StateBase64,
        revision = _current.Revision, updatedAtUtc = _current.UpdatedAtUtc, userName = Auth.DisplayName ?? "Bruger", userSub = Auth.Subject
    };
    private void NotificationStateChanged() { if (!_disposed) _ = InvokeAsync(StateHasChanged); }
    private void AccountChanged() => _ = InvokeAsync(SyncAccount);
    private async Task SyncAccount()
    {
        if (!Auth.IsInitialized || _disposed) return;
        var account = $"{Auth.Subject}:{ViewAs.IsActive}";
        if (_account == account) return;
        _account = account;
        _generation++;
        _requests?.Cancel();
        await Unmount();
        _current = null; _mine = null; _notebookId = null; _pages = []; _shared = []; _comments = []; _shares = []; _users = []; _graph = null;
        _commentDrafts.Clear(); _comment = ""; _period = default;
        _readNotifications.Clear(); _loadedNotebookAccess = null;
        if (CanUse) await Load(); else _loading = false;
        if (!_disposed) StateHasChanged();
    }
    private async Task Load()
    {
        _requests?.Cancel(); _requests?.Dispose();
        _requests = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        var ct = _requests.Token;
        var generation = ++_generation;
        _route = RouteKey; var route = _route; _loading = true; _error = null; _notice = null; _current = null;
        _treePages = []; _draggingPage = null;
        _loadedNotebookAccess = null; _readNotifications.Clear();
        _showComments = _showShares = _showGraph = false; _comments = []; _shares = []; _backlinks = []; _graph = null;
        try
        {
            await Client.Register(ct);
            if (_period == default) _period = await Client.Today(ct);
            _templates = await Client.Templates(ct);
            if (TemplateMode)
            {
                if (!IsTeacher) throw new NotebookException("Siden er for undervisere.", HttpStatusCode.Forbidden);
                _notebookId = null;
                if (TemplateId is Guid template) Select(await Client.TemplateContext(template, ct));
            }
            else
            {
                _mine = NotebookId is null ? await Client.Mine(ct) : null;
                _shared = await Client.Shared(ct);
                _holds = await HoldsClient.Holds(ct);
                _notebookId = NotebookId ?? _mine!.Id;
                _ownerName = "";
                if (PageId is Guid page)
                {
                    var context = await Client.Context(page, ct);
                    _notebookId = context.NotebookId;
                    Select(context);
                }
                if (!Own && IsTeacher)
                    _ownerName = (await Client.Teacher("", null, ct)).FirstOrDefault(n => n.Id == _notebookId)?.OwnerName ?? "Elev";
                if (Nav.ToAbsoluteUri(Nav.Uri).AbsolutePath == "/journal") { _kind = "journal"; _showJournal = Own; }
                if (_notebookId is Guid notebook)
                {
                    var result = await Client.Pages(notebook, _search, _tag, _kind, 0, ct);
                    _pages = result.Items; _total = result.Total;
                    _loadedNotebookAccess = notebook;
                }
                if (_current is null && _pages.FirstOrDefault() is { } first) Select(await Client.Context(first.Id, ct));

                if (RequestedComments && _current?.CanComment == true)
                {
                    var commentPage = _current.DocumentId;
                    var comments = await Client.Comments(commentPage, ct);
                    ct.ThrowIfCancellationRequested();
                    if (generation == _generation && route == RouteKey && _current?.DocumentId == commentPage)
                    { _comments = comments; _showComments = true; }
                }
            }
            ct.ThrowIfCancellationRequested();
        }
        catch (Exception ex) when (Expected(ex)) { if (generation == _generation && !ct.IsCancellationRequested) Report(ex); }
        finally { if (generation == _generation && !ct.IsCancellationRequested) _loading = false; }
    }
    private bool VisibleShare(ActionCenterItem item) => item.Kind == "notebook-share" && item.TargetId is Guid target
        && (item.State == "shared-page" && _current?.CanRead == true && _current.DocumentId == target
            || item.State == "shared-notebook" && _loadedNotebookAccess == target);
    private ActionCenterReadItem[] VisibleUnreadNotifications()
    {
        if (_disposed || _loading || _busy || !CanUse || TemplateMode || _error is not null || _route != RouteKey
            || PageId is Guid page && _current?.DocumentId != page
            || NotebookId is Guid notebook && _loadedNotebookAccess != notebook) return [];
        if (!string.IsNullOrWhiteSpace(NoticeQuery))
        {
            if (NoticeId is not Guid notice) return [];
            if (NoticeKind == "notebook-comment")
            {
                if (!_showComments || _current?.CanComment != true || !_comments.Any(c => c.Id == notice)) return [];
            }
            else if (NoticeKind == "notebook-share")
            {
                if (!Actions.Items.Any(item => item.ResourceId == notice && VisibleShare(item))) return [];
            }
            else return [];
        }
        IEnumerable<ActionCenterReadItem> comments = _showComments && _current?.CanComment == true
            ? _comments.Where(c => c.Id != Guid.Empty && c.AuthorSub.ToString() != Auth.Subject).Select(c => new ActionCenterReadItem("notebook-comment", c.Id)) : [];
        var shares = Actions.Items.Where(VisibleShare).Where(item => string.IsNullOrWhiteSpace(NoticeQuery) || NoticeKind != "notebook-share" || item.ResourceId == NoticeId)
            .Select(item => new ActionCenterReadItem("notebook-share", item.ResourceId));
        return comments.Concat(shares).Where(item => !_readNotifications.Contains((item.Kind, item.ResourceId))).Distinct().ToArray();
    }
    private async Task ReadVisibleNotifications()
    {
        var generation = _generation;
        if (_disposed || _loading || _busy || !CanUse || TemplateMode || _error is not null || _readingNotificationsGeneration == generation) return;
        var route = RouteKey; var account = _account;
        _readingNotificationsGeneration = generation;
        try
        {
            if (NoticeKind == "notebook-share" && NoticeId is not null) await Actions.EnsureAsync();
            if (_disposed || generation != _generation || route != RouteKey || account != _account || !CanUse) return;
            var items = VisibleUnreadNotifications();
            if (items.Length == 0) return;
            var read = await Actions.MarkReadAsync(items);
            if (read && !_disposed && generation == _generation && route == RouteKey && account == _account && CanUse)
            {
                foreach (var item in items) _readNotifications.Add((item.Kind, item.ResourceId));
                if (VisibleUnreadNotifications().Length > 0) StateHasChanged();
            }
        }
        finally { if (_readingNotificationsGeneration == generation) _readingNotificationsGeneration = null; }
    }

    private void Select(NotebookDocumentContext context)
    {
        _current = context;
        _comment = _commentDrafts.GetValueOrDefault(context.DocumentId, "");
        if (Own) _treePages = context.Pages;
        _title = context.Title; _tags = string.Join(", ", context.Tags);
        _cadence = CurrentTemplate?.Cadence ?? "daily";
        _status = "connecting"; _statusDetail = ""; _needsMount = true; _creating = false;
    }
    private async Task Work(Func<CancellationToken, Task> action)
    {
        if (_busy || _disposed) return;
        _busy = true; _error = null; _notice = null;
        try { await action(_requests?.Token ?? _life.Token); }
        catch (Exception ex) when (Expected(ex)) { Report(ex); }
        finally { _busy = false; }
    }
    private async Task Filter(bool more = false) => await Work(async ct =>
    {
        if (_notebookId is not Guid notebook) return;
        var result = await Client.Pages(notebook, _search, _tag, _kind, more ? _pages.Length : 0, ct);
        _pages = more ? _pages.Concat(result.Items).DistinctBy(p => p.Id).ToArray() : result.Items;
        _total = result.Total;
        if (Own && _current is null && _pages.Length > 0) _treePages = await Client.Tree(notebook, ct);
    });
    private Task ShowGraph() { _showGraph = true; return Task.CompletedTask; }
    private void BeginCreate(bool child) => BeginCreateAt(child ? _current?.DocumentId : null);
    private void BeginCreateAt(Guid? parent)
    {
        if (!Own && !TemplateMode) return;
        _newTitle = ""; _newParent = parent; _creating = true;
    }
    private async Task Create()
    {
        if (string.IsNullOrWhiteSpace(_newTitle) || !await CanLeave()) return;
        await Work(async ct =>
        {
            if (TemplateMode)
            {
                var template = await Client.CreateTemplate(new(_newTitle.Trim(), _newCadence), ct);
                Nav.NavigateTo($"/teacher/notebook-templates/{template.Id}");
            }
            else if (Own && _notebookId is Guid id)
            {
                var page = await Client.Create(id, new(_newTitle.Trim(), _newParent), ct);
                Nav.NavigateTo(PageHref(page.Id));
            }
        });
    }
    private string[] ParsedTags() => _tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    private async Task SaveMetadata() => await Work(async ct =>
    {
        if (_current is null || !Editing) return;
        if (TemplateMode && TemplateId is Guid id)
        {
            var updated = await Client.UpdateTemplate(id, new(_title.Trim(), _cadence), ct);
            _templates = _templates.Select(t => t.Id == id ? updated : t).ToArray();
            _current = _current with { Title = updated.Title };
        }
        else
        {
            var summary = await Client.Update(_current.DocumentId, new(_title.Trim(), null, ParsedTags(), KeepPlacementAndHold: true), ct);
            _current = _current with { Title = summary.Title, Tags = summary.Tags, HoldId = summary.HoldId,
                Pages = _current.Pages.Select(p => p.Id == summary.Id ? p with { Title = summary.Title, ParentPageId = summary.ParentPageId } : p).ToArray() };
            _pages = _pages.Select(p => p.Id == summary.Id ? summary : p).ToArray();
            _treePages = _treePages.Select(p => p.Id == summary.Id ? p with { Title = summary.Title } : p).ToArray();
        }
        if (_mounted && _editor is not null) await _editor.InvokeVoidAsync("setContext", _editorElement, EditorContext());
        _notice = "Oplysningerne er gemt.";
    });
    private void ResetMetadata() { if (_current is null) return; _title = _current.Title; _tags = string.Join(", ", _current.Tags); _cadence = CurrentTemplate?.Cadence ?? "daily"; }
    private async Task Entry(Guid template)
    {
        if (!Own || !await CanLeave()) return;
        await Work(async ct =>
        {
            try
            {
                var page = await Client.Entry(new(template, _entryRequest, _period, Guid.TryParse(_entryHold, out var hold) ? hold : null), ct);
                _entryRequest = Guid.NewGuid(); _showJournal = false; Nav.NavigateTo(PageHref(page.Id));
            }
            catch (NotebookException ex) when (ex.ErrorCode == "journal_in_trash")
            {
                _entryRequest = Guid.NewGuid(); _error = ex.Message;
                await OpenTreeTrash();
            }
            catch (NotebookException ex) when (ex.ErrorCode == "journal_request_deleted")
            {
                _entryRequest = Guid.NewGuid();
                throw;
            }
        });
    }
    private async Task ToggleComments() => await Work(async ct =>
    {
        if (_current?.CanComment != true) return;
        _showComments = !_showComments;
        if (_showComments) _comments = await Client.Comments(_current.DocumentId, ct);
    });
    private async Task AddComment() => await Work(async ct =>
    {
        if (_current?.CanComment != true || string.IsNullOrWhiteSpace(_comment)) return;
        await Client.Comment(_current.DocumentId, _comment.Trim(), ct);
        _comment = ""; _commentDrafts.Remove(_current.DocumentId); _comments = await Client.Comments(_current.DocumentId, ct);
    });
    private async Task ToggleShares() => await Work(async ct =>
    {
        if (!Own || _notebookId is not Guid id) return;
        _showShares = !_showShares; _users = []; _userSearch = ""; _usersSearched = false;
        if (_showShares) _shares = await Client.Shares(id, ct);
    });
    private async Task SearchUsers() => await Work(async ct => { _users = await Client.Users(_userSearch.Trim(), ct); _usersSearched = true; });
    private async Task Share(Guid user) => await Work(async ct =>
    {
        if (!Own || _notebookId is not Guid id || (_shareScope == "page" && _current is null)) return;
        await Client.Share(id, new(user, _shareScope == "page" ? _current!.DocumentId : null), ct);
        _shares = await Client.Shares(id, ct); _notice = "Læseadgangen er givet.";
    });
    private async Task Revoke(Guid share) => await Work(async ct => { if (_notebookId is Guid id && Own) { await Client.Revoke(id, share, ct); _shares = await Client.Shares(id, ct); _notice = "Delingen er trukket tilbage."; } });
    private async Task Activate()
    {
        if (!TemplateMode || TemplateId is not Guid id || _current is null || !_mounted || _editor is null || !await CanLeave()) return;
        await Work(async ct =>
        {
            var publication = await _editor.InvokeAsync<PublicationData>("getPublicationData", _editorElement);
            var fresh = await Client.TemplateContext(id, ct);
            var updated = await Client.Activate(id, new(fresh.Revision, publication.ExpectedFingerprint, publication.Drawings), ct);
            _templates = _templates.Select(t => t.Id == id ? updated : t).ToArray();
            _notice = "Skabelonen er aktiv. Nye logbogsindlæg bruger denne version.";
        });
    }
    private async Task Archive() => await Work(async ct => { if (TemplateId is Guid id) { var updated = await Client.Archive(id, ct); _templates = _templates.Select(t => t.Id == id ? updated : t).ToArray(); _notice = "Skabelonen er arkiveret. Eksisterende indlæg er bevaret."; } });
    private string PageHref(Guid id) => $"/notebook/{id}" + (NotebookId is Guid nb ? $"?owner={nb}" : "");
    private async Task OpenPage(Guid id) { if (_current?.DocumentId != id && await CanLeave()) { _sidebarOpen = false; Nav.NavigateTo(PageHref(id)); } }
    private async Task BeforeNavigation(LocationChangingContext context)
    {
        if (!await CanLeave()) { context.PreventNavigation(); return; }
        var path = Nav.ToAbsoluteUri(context.TargetLocation).AbsolutePath;
        var staysInNotebook = path == "/journal" || path.StartsWith("/notebook", StringComparison.Ordinal) || path.StartsWith("/teacher/notebook-templates", StringComparison.Ordinal);
        if (CanUse && !staysInNotebook && (_comment.Length > 0 || _commentDrafts.Values.Any(d => d.Length > 0)))
        {
            _error = "Du har en usendt kommentar. Tilføj kommentaren eller kassér kommentarudkastet, før du forlader notesbogen.";
            context.PreventNavigation();
        }
    }
    private void DiscardCommentDrafts() { _comment = ""; _commentDrafts.Clear(); _error = null; }
    private async Task<bool> CanLeave(bool metadata = true)
    {
        if (metadata && MetadataDirty) { _error = "Gem eller annullér ændringerne i sidens oplysninger, før du går videre."; return false; }
        if (_mounted && _editor is not null && await _editor.InvokeAsync<bool>("hasUnsavedChanges", _editorElement))
        { _error = "Vent på, at indhold og uploads er gemt i skyen. Hold fanen åben, hvis forbindelsen er afbrudt."; return false; }
        return true;
    }
    private async Task Poll()
    {
        try { using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15)); while (await timer.WaitForNextTickAsync(_life.Token)) await InvokeAsync(RefreshReadOnly); }
        catch (OperationCanceledException) { }
    }
    [JSInvokable] public Task OnNotebookFocus() => InvokeAsync(RefreshReadOnly);
    private async Task RefreshReadOnly()
    {
        if (_disposed || _refreshing || _busy || !CanUse || _current is null || Editing) return;
        _refreshing = true;
        var generation = _generation;
        var page = _current.DocumentId;
        var ct = _requests?.Token ?? _life.Token;
        try
        {
            var previous = _current;
            var context = await Client.Context(page, ct, previous.Revision);
            if (generation != _generation || _current?.DocumentId != page || _disposed) return;
            if (!context.CanRead) { await ClearRevoked(page); return; }
            var changed = context.Revision != previous.Revision;
            if (!changed && context.StateBase64 is null) context = context with { StateBase64 = previous.StateBase64 };
            _current = context;
            if (_mounted && _editor is not null) await _editor.InvokeVoidAsync(changed ? "applyReadOnlyState" : "setContext", _editorElement, EditorContext());
            else if (!string.IsNullOrEmpty(context.StateBase64)) _needsMount = true;
            var visibleIds = context.Pages.Select(p => p.Id).ToHashSet();
            _pages = _pages.Where(p => visibleIds.Contains(p.Id)).ToArray();
            _backlinks = _backlinks.Where(p => visibleIds.Contains(p.Id)).ToArray();
            _shared = _shared.Where(p => p.NotebookId != context.NotebookId || visibleIds.Contains(p.Id)).ToArray();
            if (changed)
            {
                var backlinks = Array.Empty<NotebookPageSummary>();
                if (generation != _generation || _current?.DocumentId != page || _disposed) return;
                _backlinks = backlinks;
            }
            if (_showGraph && context.NotebookId is Guid notebook)
            {
                var graph = await Client.Graph(notebook, ct);
                if (generation != _generation || _current?.DocumentId != page || _disposed) return;
                _graph = graph;
            }
            if (_showComments && context.CanComment)
            {
                var comments = await Client.Comments(page, ct);
                if (generation != _generation || _current?.DocumentId != page || _disposed) return;
                _comments = comments;
            }
            await InvokeAsync(StateHasChanged);
        }
        catch (NotebookException ex) when (ex.Status is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        { if (generation == _generation) { await ClearRevoked(page); await InvokeAsync(StateHasChanged); } }
        catch (Exception ex) when (Expected(ex)) { if (generation == _generation && ex is not OperationCanceledException) { _error = ex.Message; await InvokeAsync(StateHasChanged); } }
        finally { _refreshing = false; }
    }
    private async Task ClearRevoked(Guid page)
    {
        await Unmount(); _current = null; _pages = []; _total = 0; _backlinks = []; _comments = []; _graph = null;
        _shared = _shared.Where(p => p.Id != page).ToArray();
        _showComments = _showShares = _showGraph = false;
        _commentDrafts.Remove(page); _comment = "";
        _error = "Adgangen til siden er trukket tilbage. Indholdet er lukket.";
    }
    [JSInvokable] public Task<string?> GetNotionToken() => CanUse ? Auth.GetValidAccessTokenAsync() : Task.FromResult<string?>(null);
    [JSInvokable] public Task OnNotionStatus(string status, string detail) => InvokeAsync(async () =>
    {
        if (_disposed) return;
        var wasSaved = _status == "saved";
        _status = status; _statusDetail = detail; StateHasChanged();
        if (status == "saved" && !wasSaved && !TemplateMode && _current is not null)
        {
            var id = _current.DocumentId; var generation = _generation;
            try
            {
                var backlinks = await Client.Backlinks(id, _requests?.Token ?? _life.Token);
                if (!_disposed && generation == _generation && id == _current?.DocumentId) { _backlinks = backlinks; StateHasChanged(); }
            }
            catch (Exception ex) when (Expected(ex)) { }
        }
    });
    private async Task Reload() { if (await CanLeave()) { await Unmount(); await Load(); } }
    private IEnumerable<(NotebookPageSummary Page, int Depth)> OrderedPages()
    {
        var visited = new HashSet<Guid>();
        var ids = _pages.Select(p => p.Id).ToHashSet();
        foreach (var item in Walk(null, 0)) yield return item;
        foreach (var page in _pages.Where(p => !visited.Contains(p.Id))) yield return (page, 0);
        IEnumerable<(NotebookPageSummary, int)> Walk(Guid? parent, int depth)
        {
            foreach (var p in _pages.Where(p => parent is null ? p.ParentPageId is null || !ids.Contains(p.ParentPageId.Value) : p.ParentPageId == parent).OrderBy(p => p.SortOrder ?? int.MaxValue).ThenByDescending(p => p.PeriodStart).ThenBy(p => p.Title).ThenBy(p => p.Id))
            { if (!visited.Add(p.Id)) continue; yield return (p, Math.Min(depth, 5)); foreach (var c in Walk(p.Id, depth + 1)) yield return c; }
        }
    }
    private static string CadenceLabel(string cadence) => cadence switch { "daily" => "Daglig", "weekly" => "Ugentlig", _ => "Fri" };
    private static bool Expected(Exception ex) => ex is NotebookException or HttpRequestException or JSException or OperationCanceledException or System.Text.Json.JsonException;
    private void Report(Exception ex) { if (!_disposed && ex is not OperationCanceledException) _error = ex is NotebookException ? ex.Message : "Handlingen kunne ikke gennemføres. Kontrollér forbindelsen og prøv igen."; }
    private async Task Unmount()
    {
        _needsMount = false;
        if (_current is not null && _comment.Length > 0) _commentDrafts[_current.DocumentId] = _comment;
        if (_mounted && _editor is not null) await _editor.InvokeVoidAsync("destroy", _editorElement);
        _mounted = false;
    }
    public async ValueTask DisposeAsync()
    {
        _disposed = true; Auth.AuthStateChanged -= AccountChanged; ViewAs.Changed -= AccountChanged; Actions.Changed -= NotificationStateChanged;
        await _life.CancelAsync(); _requests?.Cancel();
        try { if (_lifecycle is not null) { await _lifecycle.InvokeVoidAsync("unwatch"); await _lifecycle.DisposeAsync(); } await Unmount(); if (_editor is not null) await _editor.DisposeAsync(); } catch (JSDisconnectedException) { }
        _callbacks?.Dispose(); _requests?.Dispose(); _life.Dispose();
    }
    private sealed record PublicationData(string ExpectedFingerprint, Guid[] AssetIds, DrawingPublicationRequest[]? Drawings = null);
}
