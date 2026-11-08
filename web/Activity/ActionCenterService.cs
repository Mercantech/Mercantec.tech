using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;

namespace Mercantec.Tech.Web.Activity;

public enum ActionCenterStatus { Unknown, Loading, Important, Optional, Clear }

/// <summary>One personal snapshot shared by the header and action page; never treats failed reads as clear.</summary>
public sealed class ActionCenterService : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private readonly AuthRedirect _redirect;
    private readonly ViewAsStudentContext _viewAs;
    private readonly ActionCenterChanges _changes;
    private readonly CancellationTokenSource _life = new();
    private CancellationTokenSource? _request;
    private Task? _polling;
    private string? _identity;
    private DateTimeOffset _lastRead;
    private bool _refreshAgain, _disposed;
    public ActionCenterSnapshot? Snapshot { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }
    public event Action? Changed;
    public IReadOnlyList<ActionCenterItem> Items => Snapshot?.Items ?? [];
    public int ImportantCount => Items.Where(i => i.Severity == "important").Sum(i => Math.Max(1, i.Count));
    public int OptionalCount => Items.Where(i => i.Severity == "optional").Sum(i => Math.Max(1, i.Count));
    public ActionCenterStatus Status => IsLoading ? ActionCenterStatus.Loading
        : Error is not null || Snapshot is null ? ActionCenterStatus.Unknown
        : ImportantCount > 0 ? ActionCenterStatus.Important
        : OptionalCount > 0 ? ActionCenterStatus.Optional : ActionCenterStatus.Clear;
    public string Label => Status switch
    {
        ActionCenterStatus.Important => $"{ImportantCount} vigtig{(ImportantCount == 1 ? " mangel" : "e mangler")}" + (OptionalCount > 0 ? $" og {OptionalCount} {(OptionalCount == 1 ? "anden handling" : "andre handlinger")}" : ""),
        ActionCenterStatus.Optional => $"{OptionalCount} handling{(OptionalCount == 1 ? " venter" : "er venter")}",
        ActionCenterStatus.Clear => "Intet mangler",
        ActionCenterStatus.Loading => "Henter status…",
        _ => Error is not null ? "Status er ikke tilgængelig" : "Status er endnu ikke hentet"
    };
    private string Identity => $"{_auth.Subject}:{_redirect.CurrentAppRole()}:{_viewAs.IsActive}:{_viewAs.AuthSub}";

    public ActionCenterService(HttpClient http, AuthService auth, AuthRedirect redirect, ViewAsStudentContext viewAs, ActionCenterChanges changes)
    {
        _http = http; _auth = auth; _redirect = redirect; _viewAs = viewAs; _changes = changes;
        _auth.AuthStateChanged += AccountChanged; _viewAs.Changed += AccountChanged; _changes.Changed += Invalidate;
    }
    public Task EnsureAsync()
    {
        _polling ??= PollAsync();
        return RefreshAsync();
    }
    private void AccountChanged()
    {
        if (_identity == Identity && _auth.IsAuthenticated) return;
        _request?.Cancel();
        Snapshot = null; Error = null; _lastRead = default;
        Changed?.Invoke();
        _ = RefreshAsync(force: true);
    }
    private void Invalidate()
    {
        _lastRead = default;
        if (IsLoading) { _refreshAgain = true; return; }
        _ = RefreshAsync(force: true);
    }
    public async Task RefreshAsync(bool force = false)
    {
        if (_disposed || !_auth.IsInitialized) return;
        var identity = Identity;
        if (_identity != identity)
        {
            _request?.Cancel(); _identity = identity;
            Snapshot = null; Error = null; _lastRead = default; IsLoading = false;
        }
        if (!_auth.IsAuthenticated) { Snapshot = null; Error = null; IsLoading = false; Changed?.Invoke(); return; }
        if (_viewAs.IsActive && (_redirect.CurrentAppRole() != AppRole.Underviser || _viewAs.AuthSub is null))
        {
            Snapshot = null; Error = "Personlig status kræver en tilknyttet elevkonto."; IsLoading = false; Changed?.Invoke(); return;
        }
        if (IsLoading || !force && DateTimeOffset.UtcNow - _lastRead < TimeSpan.FromSeconds(15)) return;
        _request?.Cancel(); _request?.Dispose();
        var request = _request = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        request.CancelAfter(TimeSpan.FromSeconds(20));
        var ct = request.Token;
        IsLoading = true; Error = null; Changed?.Invoke();
        try
        {
            var path = _viewAs.IsActive ? $"me/actions?version=2&asUser={_viewAs.AuthSub!.Value:D}" : "me/actions?version=2";
            var result = await _http.GetFromJsonAsync<ActionCenterSnapshot>(path, ct);
            ct.ThrowIfCancellationRequested();
            if (result?.Items is null || result.Items.Any(i => !ValidItem(i)))
                throw new JsonException("Invalid action status.");
            if (!_disposed && ReferenceEquals(_request, request) && identity == Identity)
            {
                Snapshot = result; _lastRead = DateTimeOffset.UtcNow; Error = null;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (!_disposed && ReferenceEquals(_request, request) && identity == Identity && !_life.IsCancellationRequested)
                Error = "Kontrollér forbindelsen, og prøv igen.";
        }
        finally
        {
            if (!_disposed && ReferenceEquals(_request, request) && identity == Identity)
            {
                IsLoading = false; Changed?.Invoke();
                if (_refreshAgain) { _refreshAgain = false; await RefreshAsync(force: true); }
            }
        }
    }
    public Task<bool> MarkReadAsync(string kind, Guid resourceId) => MarkReadAsync([new ActionCenterReadItem(kind, resourceId)]);
    public async Task<bool> MarkReadAsync(IEnumerable<ActionCenterReadItem> requested)
    {
        if (_disposed || !_auth.IsInitialized || !_auth.IsAuthenticated || _viewAs.IsActive) return false;
        var items = requested.Where(i => i.ResourceId != Guid.Empty && i.Kind is "feedback" or "notebook-comment" or "notebook-share").Distinct().ToArray();
        if (items.Length == 0) return true;
        var identity = Identity;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            foreach (var batch in items.Chunk(200))
            {
                using var response = await _http.PostAsJsonAsync("me/actions/read", new ActionCenterReadRequest(batch), timeout.Token);
                if (!response.IsSuccessStatusCode || _disposed || identity != Identity) return false;
            }
            _lastRead = default;
            if (IsLoading) _refreshAgain = true;
            else await RefreshAsync(force: true);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException) { return false; }
    }
    private static bool ValidItem(ActionCenterItem? item)
    {
        if (item is null || item.Severity is not ("important" or "optional") || item.Count < 1 || item.ResourceId == Guid.Empty) return false;
        if (item.Kind is "gift" or "notebook-comment" or "notebook-share")
        {
            if (item.Severity != "optional" || item.HoldId is not null || item.WorkspaceId is not null || item.CourseId is not null || item.SubmissionId is not null) return false;
            return item.Kind == "gift" ? item.State == "unopened" && item.TargetId is null : item.TargetId is Guid target && target != Guid.Empty;
        }
        if (item.Kind == "feedback" && (item.TargetId is not Guid quest || quest == Guid.Empty || item.SubmissionId is not Guid submission || submission == Guid.Empty)) return false;
        return item.Kind is "poll" or "submission" or "review" or "quest" or "feedback"
            && item.HoldId is Guid hold && hold != Guid.Empty && item.WorkspaceId is Guid workspace && workspace != Guid.Empty;
    }
    private async Task PollAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(45));
            while (await timer.WaitForNextTickAsync(_life.Token)) await RefreshAsync();
        }
        catch (OperationCanceledException) { }
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true; _auth.AuthStateChanged -= AccountChanged; _viewAs.Changed -= AccountChanged; _changes.Changed -= Invalidate;
        _request?.Cancel(); await _life.CancelAsync();
        if (_polling is not null) await _polling;
        _request?.Dispose(); _life.Dispose();
    }
}
