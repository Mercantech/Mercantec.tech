using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Notion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Notion;

public partial class Index
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private AuthRedirect Redirect { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [Inject] private NotionClient Client { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Parameter] public Guid? PageId { get; set; }

    private readonly CancellationTokenSource _life = new();
    private IJSObjectReference? _module;
    private CancellationTokenSource? _accountRequests;
    private Guid? _routePage;
    private DotNetObjectReference<Index>? _callbacks;
    private ElementReference _editorElement;
    private NotionHold[] _holds = [];
    private NotionPageSummary[] _pages = [];
    private NotionPageContext? _current;
    private Guid _holdId;
    private Guid? _newParent;
    private string? _account;
    private string? _error;
    private string _title = "", _newTitle = "", _status = "connecting", _statusDetail = "";
    private bool _loading = true, _busy, _creating, _needsMount, _mounted, _disposed;
    private bool CanUse => Auth.IsAuthenticated && Redirect.CurrentAppRole() == AppRole.Underviser && !ViewAs.IsActive;
    private string StatusLabel => _status switch
    {
        "saved" => "Gemt i skyen", "saving" => "Gemmer…", "synced" => "Forbundet", "offline" => "Forbindelsen er afbrudt",
        "error" => "Forbindelsen kræver opmærksomhed", _ => "Forbinder…"
    };

    protected override void OnInitialized()
    {
        Auth.AuthStateChanged += AccountChanged;
        ViewAs.Changed += AccountChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await SyncAccount();
        if (!_needsMount || _current is null || !CanUse || _disposed) return;
        _needsMount = false;
        try
        {
            _module ??= await Js.InvokeAsync<IJSObjectReference>("import", "/notion-editor/editor.js");
            _callbacks ??= DotNetObjectReference.Create(this);
            var token = await Auth.GetValidAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) throw new NotionException("Log ind igen for at redigere dokumentet.");
            await _module.InvokeVoidAsync("mount", _editorElement, EditorContext(), token, _callbacks);
            _mounted = true;
        }
        catch (Exception ex) when (ex is JSException or NotionException or HttpRequestException)
        {
            _status = "error";
            _error = ex is NotionException ? ex.Message : "Editoren kunne ikke startes. Prøv igen, eller kontakt administratoren.";
        }
        if (!_disposed) await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_loading || !CanUse || _account is null || PageId == _routePage) return;
        _routePage = PageId;
        if (PageId is Guid id) await OpenPage(id);
    }

    private object EditorContext() => new
    {
        pageId = _current!.PageId, holdId = _current.HoldId, title = _current.Title,
        canEdit = _current.CanEdit, members = _current.Members, polls = _current.Polls,
        updatedAtUtc = _current.UpdatedAtUtc, userName = Auth.DisplayName ?? "Underviser"
    };

    private void AccountChanged() => _ = InvokeAsync(SyncAccount);

    private async Task SyncAccount()
    {
        if (!Auth.IsInitialized || _disposed) return;
        var account = $"{Auth.Subject}:{CanUse}";
        if (_account == account) return;
        _account = account;
        var previousRequests = _accountRequests;
        _accountRequests = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        if (previousRequests is not null) { await previousRequests.CancelAsync(); previousRequests.Dispose(); }
        await Unmount();
        _current = null;
        _holds = [];
        _pages = [];
        if (CanUse) await Load();
        else _loading = false;
        if (!_disposed) StateHasChanged();
    }

    private async Task Load()
    {
        _loading = true;
        _error = null;
        var account = _account;
        var ct = _accountRequests?.Token ?? _life.Token;
        _routePage = PageId;
        try
        {
            var holds = await Client.Holds(ct);
            if (account != _account) return;
            _holds = holds;
            if (holds.Length == 0) return;
            if (PageId is Guid id)
            {
                var context = await Client.Context(id, ct);
                if (account != _account) return;
                _holdId = context.HoldId;
                _pages = await Client.Pages(_holdId, ct);
                if (account != _account) return;
                SelectContext(context);
            }
            else
            {
                _holdId = holds[0].Id;
                _pages = await Client.Pages(_holdId, ct);
                if (account != _account) return;
                if (_pages.Length > 0) SelectContext(await Client.Context(_pages[0].Id, ct));
            }
        }
        catch (Exception ex) when (IsExpected(ex)) { Report(ex); }
        finally { _loading = false; }
    }

    private void SelectContext(NotionPageContext context)
    {
        _current = context;
        _title = context.Title;
        _status = "connecting";
        _statusDetail = "";
        _needsMount = true;
    }

    private async Task<bool> CanLeave()
    {
        if (!_mounted || _module is null) return true;
        if (!await _module.InvokeAsync<bool>("hasUnsavedChanges", _editorElement)) return true;
        _error = "Vent på, at ændringerne er gemt i skyen, før du forlader dokumentet. Hold siden åben, hvis forbindelsen er afbrudt.";
        return false;
    }

    private async Task BeforeNavigation(LocationChangingContext context)
    {
        if (!await CanLeave()) context.PreventNavigation();
    }

    private async Task ChangeHold(ChangeEventArgs args)
    {
        if (!Guid.TryParse(args.Value?.ToString(), out var id) || id == _holdId || !await CanLeave()) return;
        await Work(async ct =>
        {
            var pages = await Client.Pages(id, ct);
            var context = pages.Length > 0 ? await Client.Context(pages[0].Id, ct) : null;
            await Unmount();
            ct.ThrowIfCancellationRequested();
            _holdId = id;
            _pages = pages;
            _current = null;
            _creating = false;
            if (context is not null) SelectContext(context);
        });
    }

    private async Task OpenPage(Guid id)
    {
        if (_current?.PageId == id || !await CanLeave()) return;
        await Work(async ct =>
        {
            var context = await Client.Context(id, ct);
            var pages = context.HoldId == _holdId ? _pages : await Client.Pages(context.HoldId, ct);
            await Unmount();
            ct.ThrowIfCancellationRequested();
            _holdId = context.HoldId;
            _pages = pages;
            SelectContext(context);
        });
    }

    private void BeginCreate(bool child)
    {
        _newParent = child ? _current?.PageId : null;
        _newTitle = "";
        _creating = true;
    }

    private async Task CreatePage()
    {
        if (string.IsNullOrWhiteSpace(_newTitle) || !await CanLeave()) return;
        await Work(async ct =>
        {
            var page = await Client.Create(_holdId, _newTitle.Trim(), _newParent, ct);
            _pages = await Client.Pages(_holdId, ct);
            var context = await Client.Context(page.Id, ct);
            await Unmount();
            ct.ThrowIfCancellationRequested();
            _creating = false;
            SelectContext(context);
        });
    }

    private async Task RenamePage()
    {
        if (_current is null || string.IsNullOrWhiteSpace(_title)) return;
        await Work(async ct =>
        {
            await Client.Rename(_current.PageId, _title.Trim(), ct);
            _current = await Client.Context(_current.PageId, ct);
            _pages = await Client.Pages(_holdId, ct);
            _title = _current.Title;
            if (_mounted && _module is not null) await _module.InvokeVoidAsync("setContext", _editorElement, EditorContext());
        });
    }

    private async Task RefreshContext()
    {
        if (_current is null) return;
        await Work(async ct =>
        {
            _current = await Client.Context(_current.PageId, ct);
            if (_mounted && _module is not null) await _module.InvokeVoidAsync("setContext", _editorElement, EditorContext());
        });
    }

    private async Task Retry()
    {
        if (!await CanLeave()) return;
        await Unmount();
        if (_current is not null) { _error = null; _needsMount = true; }
        else await Load();
    }

    private async Task Work(Func<CancellationToken, Task> action)
    {
        if (_busy || _disposed) return;
        _busy = true;
        _error = null;
        try { await action(_accountRequests?.Token ?? _life.Token); }
        catch (Exception ex) when (IsExpected(ex)) { Report(ex); }
        finally { _busy = false; }
    }

    [JSInvokable]
    public Task<string?> GetNotionToken() => CanUse ? Auth.GetValidAccessTokenAsync() : Task.FromResult<string?>(null);

    [JSInvokable]
    public Task OnNotionStatus(string status, string detail) => InvokeAsync(() =>
    {
        if (_disposed) return;
        _status = status;
        _statusDetail = detail;
        StateHasChanged();
    });

    private IEnumerable<(NotionPageSummary Page, int Depth)> OrderedPages()
    {
        var visited = new HashSet<Guid>();
        return Walk(null, 0).Concat(_pages.Where(p => !visited.Contains(p.Id)).Select(p => (p, 0)));
        IEnumerable<(NotionPageSummary, int)> Walk(Guid? parent, int depth)
        {
            foreach (var page in _pages.Where(p => p.ParentPageId == parent).OrderBy(p => p.Title))
            {
                if (!visited.Add(page.Id)) continue;
                yield return (page, Math.Min(depth, 4));
                foreach (var child in Walk(page.Id, depth + 1)) yield return child;
            }
        }
    }

    private static bool IsExpected(Exception ex) => ex is NotionException or HttpRequestException or OperationCanceledException or JSException or System.Text.Json.JsonException;
    private void Report(Exception ex) { if (!_disposed) _error = ex is NotionException ? ex.Message : "Handlingen kunne ikke gennemføres. Kontrollér forbindelsen, og prøv igen."; }
    private async Task Unmount()
    {
        _needsMount = false;
        if (_mounted && _module is not null) await _module.InvokeVoidAsync("destroy", _editorElement);
        _mounted = false;
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        Auth.AuthStateChanged -= AccountChanged;
        ViewAs.Changed -= AccountChanged;
        await _life.CancelAsync();
        _accountRequests?.Dispose();
        try { await Unmount(); if (_module is not null) await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
        _callbacks?.Dispose();
        _life.Dispose();
    }
}
