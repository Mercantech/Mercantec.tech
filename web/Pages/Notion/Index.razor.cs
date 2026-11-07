using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Emojis;
using Mercantec.Tech.Web.Notion;
using Mercantec.Tech.Web.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Notion;

public partial class Index
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private NotionClient Client { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Parameter] public Guid? PageId { get; set; }
    [SupplyParameterFromQuery(Name = "hold")] public Guid? HoldQuery { get; set; }
    [SupplyParameterFromQuery(Name = "published")] public bool PublishedPreview { get; set; }
    [SupplyParameterFromQuery(Name = "workspace")] public Guid? WorkspaceQuery { get; set; }
    [SupplyParameterFromQuery(Name = "course")] public Guid? CourseQuery { get; set; }
    private WorkspaceRouteContext? _returnContext;
    private readonly CancellationTokenSource _life = new();
    private CancellationTokenSource? _requests;
    private IJSObjectReference? _module;
    private DotNetObjectReference<Index>? _callbacks;
    private ElementReference _editorElement;
    private NotionHold[] _holds = [];
    private NotionPageSummary[] _pages = [];
    private NotionGroupView[] _groups = [];
    private NotionPageContext? _current;
    private Guid _holdId;
    private Guid? _newParent;
    private Guid[] _groupSelection = [];
    private int _groupPanelGeneration;
    private string? _account, _loadedRoute, _error, _notice;
    private string _title = "", _newTitle = "", _status = "connecting", _statusDetail = "";
    private bool _loading = true, _busy, _creating, _needsMount, _mounted, _disposed, _showGroups;
    private bool CanUse => Auth.IsAuthenticated;
    private bool ForceReadOnly => PublishedPreview || ViewAs.IsActive;
    private bool CanManageHold => !ViewAs.IsActive && _holds.Any(h => h.Id == _holdId && h.CanManage);
    private bool IsEditing => _current?.CanEdit == true && !ForceReadOnly;
    private bool Authoring => CanManageHold && !ForceReadOnly;
    private string RouteKey => $"{PageId}:{HoldQuery}:{WorkspaceQuery}:{CourseQuery}:{ForceReadOnly}";
    private string StatusLabel => _status switch { "saved" => "Gemt i skyen", "saving" => "Gemmer…", "synced" => "Forbundet", "offline" => "Forbindelsen er afbrudt", "error" => "Forbindelsen kræver opmærksomhed", _ => "Forbinder…" };
    private IEnumerable<NotionPageSummary> VisiblePages => ForceReadOnly ? _pages.Where(p => p.PublishedAtUtc is not null) : _pages;
    private string PageHref(Guid id, bool? publishedOnly = null) => WorkspaceNavigation.Page(id, _returnContext?.WorkspaceId, _returnContext?.CourseId, publishedOnly ?? ForceReadOnly, _holdId);
    private string GraphHref(Guid id) => WorkspaceNavigation.Graph(id, _returnContext?.WorkspaceId, _returnContext?.CourseId, ForceReadOnly, _holdId);
    private string HoldGraphHref => WorkspaceNavigation.HoldGraph(_holdId, _returnContext?.WorkspaceId, _returnContext?.CourseId, ForceReadOnly);
    private string WorkspaceHref(string view = "content") => _returnContext?.View(view) ?? WorkspaceNavigation.Hold(_holdId, view);
    private bool IsCollection(NotionPageSummary page) =>
        page.ParentPageId is null || !VisiblePages.Any(p => p.Id == page.ParentPageId) || VisiblePages.Any(p => p.ParentPageId == page.Id);
    private Guid CollectionRoot(Guid id)
    {
        var pages = VisiblePages.ToDictionary(p => p.Id);
        for (var guard = 0; guard < 32 && pages.TryGetValue(id, out var page) && page.ParentPageId is Guid parent && pages.ContainsKey(parent); guard++)
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
        await Unmount();
        await Load();
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await SyncAccount();
        if (!_needsMount || _current is null || !CanUse || _disposed) return;
        _needsMount = false;
        var account = _account; var page = _current.PageId;
        try
        {
            _module ??= await Js.InvokeAsync<IJSObjectReference>("import", "/notion-editor/editor.js");
            _callbacks ??= DotNetObjectReference.Create(this);
            var token = await Auth.GetValidAccessTokenAsync();
            if (account != _account || page != _current?.PageId || !CanUse || _disposed) return;
            if (string.IsNullOrEmpty(token)) throw new NotionException("Log ind igen for at åbne dokumentet.");
            await _module.InvokeVoidAsync(IsEditing ? "mount" : "mountReadOnly", _editorElement, EditorContext(), token, _callbacks);
            _mounted = true;
        }
        catch (Exception ex) when (ex is JSException or NotionException or HttpRequestException)
        { _status = "error"; _error = ex is NotionException ? ex.Message : "Dokumentet kunne ikke åbnes. Prøv igen, eller kontakt administratoren."; }
        if (!_disposed) await InvokeAsync(StateHasChanged);
    }
    private object EditorContext() => new
    {
        pageId = _current!.PageId, holdId = _current.HoldId, title = _current.Title,
        workspaceId = _returnContext?.WorkspaceId, courseId = _returnContext?.CourseId,
        canEdit = IsEditing, members = _current.Members, polls = _current.Polls,
        pages = _current.Pages ?? [], groups = _groups, publishedStateBase64 = _current.PublishedStateBase64,
        publishedAtUtc = _current.PublishedAtUtc, updatedAtUtc = _current.UpdatedAtUtc,
        userName = Auth.DisplayName ?? "Bruger"
    };
    private void AccountChanged() => _ = InvokeAsync(SyncAccount);
    private async Task SyncAccount()
    {
        if (!Auth.IsInitialized || _disposed) return;
        var account = $"{Auth.Subject}:{ViewAs.IsActive}";
        if (_account == account) return;
        _account = account;
        await Unmount();
        _current = null; _holds = []; _pages = []; _groups = []; _showGroups = false;
        if (CanUse) await Load(); else { _requests?.Cancel(); _loading = false; }
        if (!_disposed) StateHasChanged();
    }
    private async Task Load()
    {
        var old = _requests;
        _requests = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        if (old is not null) { await old.CancelAsync(); old.Dispose(); }
        var ct = _requests.Token; var account = _account;
        _loadedRoute = RouteKey; _loading = true; _error = null; _notice = null; _current = null; _returnContext = null;
        var route = RouteKey;
        try
        {
            var holds = await Client.Holds(ct);
            if (account != _account) return;
            _holds = holds;
            if (PageId is Guid id)
            {
                var context = await FetchContext(id, ct);
                _holdId = context.HoldId;
                _pages = await Client.Pages(_holdId, ct, ForceReadOnly);
                _groups = await Client.Groups(_holdId, ct);
                ct.ThrowIfCancellationRequested();
                SelectContext(context);
            }
            else if (holds.Length > 0)
            {
                if (HoldQuery is Guid requested && !holds.Any(h => h.Id == requested)) throw new NotionException("Holdet er ikke tilgængeligt for din konto.");
                _holdId = HoldQuery ?? holds[0].Id;
                _pages = await Client.Pages(_holdId, ct);
                _groups = await Client.Groups(_holdId, ct);
                if (VisiblePages.FirstOrDefault() is { } first) SelectContext(await FetchContext(first.Id, ct));
            }
            if (_holdId != Guid.Empty && (HoldQuery is null || HoldQuery == _holdId)
                && (WorkspaceNavigation.NonEmpty(WorkspaceQuery) is not null || WorkspaceNavigation.NonEmpty(CourseQuery) is not null))
            {
                var context = await WorkspaceNavigation.Resolve(Http, _holdId, WorkspaceQuery, CourseQuery, ForceReadOnly, ct);
                ct.ThrowIfCancellationRequested();
                if (route == RouteKey && account == _account) _returnContext = context;
            }
        }
        catch (Exception ex) when (IsExpected(ex)) { if (!ct.IsCancellationRequested) Report(ex); }
        finally { if (!ct.IsCancellationRequested) _loading = false; }
    }
    private Task<NotionPageContext> FetchContext(Guid id, CancellationToken ct) => ForceReadOnly ? Client.PublishedContext(id, ct) : Client.Context(id, ct);
    private void SelectContext(NotionPageContext context) { _current = context; _title = context.Title; _status = "connecting"; _statusDetail = ""; _needsMount = true; _creating = false; }
    private async Task<bool> CanLeave()
    {
        if (!_mounted || _module is null || !IsEditing) return true;
        if (!await _module.InvokeAsync<bool>("hasUnsavedChanges", _editorElement)) return true;
        _error = "Vent på, at ændringer og uploads er gemt i skyen. Hold siden åben, hvis forbindelsen er afbrudt.";
        return false;
    }
    private async Task BeforeNavigation(LocationChangingContext context) { if (!await CanLeave()) context.PreventNavigation(); }
    private async Task ChangeHold(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var id) && id != _holdId && await CanLeave()) Nav.NavigateTo(WorkspaceNavigation.Editor(id, publishedOnly: ForceReadOnly));
    }
    private async Task OpenPage(Guid id) { if (_current?.PageId != id && await CanLeave()) Nav.NavigateTo(PageHref(id)); }
    private void BeginCreate(bool child) { if (!Authoring) return; _newParent = child ? _current?.PageId : null; _newTitle = ""; _creating = true; }
    private async Task CreatePage()
    {
        if (!Authoring || string.IsNullOrWhiteSpace(_newTitle) || !await CanLeave()) return;
        await Work(async ct => { var page = await Client.Create(_holdId, _newTitle.Trim(), _newParent, ct); _creating = false; Nav.NavigateTo(PageHref(page.Id)); });
    }
    private void InsertTitleEmoji(CustomEmojiItem emoji) => _title = EmojiInsert.Append(_title, emoji.Name, 200);
    private void InsertNewTitleEmoji(CustomEmojiItem emoji) => _newTitle = EmojiInsert.Append(_newTitle, emoji.Name, 200);
    private async Task RenamePage()
    {
        if (!IsEditing || _current is null || string.IsNullOrWhiteSpace(_title)) return;
        await Work(async ct => { await Client.Rename(_current.PageId, _title.Trim(), ct); await UpdateContext(ct); _notice = "Titlen er gemt i kladden."; });
    }
    private async Task UpdateContext(CancellationToken ct)
    {
        if (_current is null) return;
        var id = _current.PageId;
        var originalTitle = _current.Title;
        var localTitle = _title;
        var context = await FetchContext(id, ct);
        var pages = await Client.Pages(context.HoldId, ct, ForceReadOnly);
        var groups = await Client.Groups(context.HoldId, ct);
        ct.ThrowIfCancellationRequested();
        if (_current?.PageId != id) return;
        _current = context;
        _title = localTitle.Trim() == originalTitle || localTitle.Trim() == context.Title ? context.Title : localTitle;
        _pages = pages; _groups = groups;
        if (_mounted && _module is not null) await _module.InvokeVoidAsync("setContext", _editorElement, EditorContext());
    }
    private async Task RefreshContext() => await Work(UpdateContext);
    private async Task PublishPage()
    {
        if (!IsEditing || _current is null || _module is null || !_mounted || !await CanLeave()) return;
        if (_title.Trim() != _current.Title) { _error = "Gem sidens titel, før du udgiver."; return; }
        await Work(async ct =>
        {
            var id = _current.PageId;
            var data = await _module.InvokeAsync<PublicationData>("getPublicationData", _editorElement);
            var context = await Client.Context(id, ct);
            await Client.Publish(id, context.DraftRevision, data.ExpectedFingerprint, data.AssetIds, ct);
            await UpdateContext(ct);
            _notice = "Siden er udgivet til eleverne på holdet. Du kan arbejde videre i kladden.";
        });
    }
    private async Task UnpublishPage()
    {
        if (!IsEditing || _current is null) return;
        await Work(async ct => { await Client.Unpublish(_current.PageId, ct); await UpdateContext(ct); _notice = "Udgivelsen er trukket tilbage. Eleverne kan ikke længere åbne siden og dens vedhæftninger."; });
    }
    private async Task CopyLink()
    {
        if (_current is null) return;
        try { await Js.InvokeVoidAsync("navigator.clipboard.writeText", Nav.ToAbsoluteUri(PageHref(_current.PageId)).ToString()); _notice = "Linket er kopieret. Modtageren skal have adgang til holdet."; }
        catch (JSException) { _error = "Linket kunne ikke kopieres. Brug adressen i browserens adresselinje."; }
    }
    private async Task Retry() { if (!await CanLeave()) return; await Unmount(); await Load(); }
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
        if (!IsEditing || _disposed) return;
        var ids = JsonSerializer.Deserialize<Guid[]>(membershipIdsJson) ?? [];
        _groupSelection = ids.Where(id => _current!.Members.Any(m => m.Id == id)).Distinct().ToArray();
        _groupPanelGeneration++; _showGroups = true; StateHasChanged();
        try { await Js.InvokeVoidAsync("window.scrollTo", 0, 0); } catch (JSException) { }
    });
    private IEnumerable<(NotionPageSummary Page, int Depth)> OrderedPages()
    {
        var pages = VisiblePages.ToArray(); var visited = new HashSet<Guid>();
        return Walk(null, 0).Concat(pages.Where(p => !visited.Contains(p.Id)).Select(p => (p, 0)));
        IEnumerable<(NotionPageSummary, int)> Walk(Guid? parent, int depth)
        {
            foreach (var page in pages.Where(p => p.ParentPageId == parent).OrderBy(p => p.Title))
            { if (!visited.Add(page.Id)) continue; yield return (page, Math.Min(depth, 4)); foreach (var child in Walk(page.Id, depth + 1)) yield return child; }
        }
    }
    private static bool IsExpected(Exception ex) => ex is NotionException or HttpRequestException or OperationCanceledException or JSException or JsonException;
    private void Report(Exception ex) { if (!_disposed && ex is not OperationCanceledException) _error = ex is NotionException ? ex.Message : ex is JSException ? "Handlingen kunne ikke gennemføres. Vent på, at kladden er gemt, og prøv igen." : "Handlingen kunne ikke gennemføres. Kontrollér forbindelsen, og prøv igen."; }
    private async Task Unmount() { _needsMount = false; if (_mounted && _module is not null) await _module.InvokeVoidAsync("destroy", _editorElement); _mounted = false; }
    public async ValueTask DisposeAsync()
    {
        _disposed = true; Auth.AuthStateChanged -= AccountChanged; ViewAs.Changed -= AccountChanged;
        await _life.CancelAsync(); _requests?.Dispose();
        try { await Unmount(); if (_module is not null) await _module.DisposeAsync(); } catch (JSDisconnectedException) { }
        _callbacks?.Dispose(); _life.Dispose();
    }
    private sealed record PublicationData(string ExpectedFingerprint, Guid[] AssetIds);
}
