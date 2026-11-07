using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Navigation;
using Mercantec.Tech.Web.Polls;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages;

public partial class Notifications : IDisposable
{
    [Inject] private AuthService Auth { get; set; } = null!;
    [Inject] private AuthRedirect Redirect { get; set; } = null!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = null!;
    [Inject] private PollClient PollClient { get; set; } = null!;
    [Inject] private HttpClient Http { get; set; } = null!;
    private CancellationTokenSource _requests = new();
    private string? _identity, _holdError, _pollError;
    private bool _loading = true, _disposed, _previewWithoutAccount;
    private List<HoldWorkspaceView> _workspaces = [];
    private List<PollView> _polls = [];
    private bool IsTeacher => Redirect.ShowsTeacherChrome;
    private string Identity => $"{Auth.Subject}:{Redirect.CurrentAppRole()}:{ViewAs.IsActive}:{ViewAs.AuthSub}:{ViewAs.LectioId}";
    private IReadOnlyList<HoldWorkspaceView> ReviewWorkspaces => ActionNotifications.PendingReviews(_workspaces);
    private IReadOnlyList<PollView> VisiblePolls => ViewAs.IsActive ? [] : ActionNotifications.Polls(_polls, _workspaces, IsTeacher);
    private string PollHref(PollView poll)
    {
        var workspace = _workspaces.First(w => w.HoldId == poll.HoldId);
        return WorkspaceNavigation.Poll(poll.Id, workspace.CourseId, holdId: poll.HoldId);
    }
    protected override void OnInitialized() { Auth.AuthStateChanged += AccountChanged; ViewAs.Changed += AccountChanged; }
    protected override async Task OnParametersSetAsync() { if (Auth.IsInitialized && _identity != Identity) await Refresh(); }
    private void AccountChanged() => _ = InvokeAsync(async () => { if (_identity != Identity) await Refresh(); StateHasChanged(); });
    private async Task Refresh()
    {
        if (_disposed || !Auth.IsInitialized) return;
        _requests.Cancel(); _requests.Dispose();
        var requestSource = _requests = new CancellationTokenSource();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestSource.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        var ct = timeout.Token;
        var identity = Identity;
        _identity = identity; _holdError = null; _pollError = null; _loading = true;
        _workspaces = []; _polls = []; _previewWithoutAccount = false;
        if (!Auth.IsAuthenticated) { _loading = false; return; }
        if (ViewAs.IsActive && (ViewAs.AuthSub is not Guid previewSub || previewSub == Guid.Empty))
        {
            _previewWithoutAccount = true; _loading = false; return;
        }
        try
        {
            var holdsTask = LoadWorkspaces(ct);
            var pollsTask = LoadPolls(ct);
            await Task.WhenAll(holdsTask, pollsTask);
            if (_disposed || !ReferenceEquals(_requests, requestSource) || requestSource.IsCancellationRequested || identity != Identity) return;
            _workspaces = holdsTask.Result.Workspaces;
            _holdError = holdsTask.Result.Error;
            _polls = pollsTask.Result.Polls;
            _pollError = pollsTask.Result.Error;
        }
        finally
        {
            if (!_disposed && ReferenceEquals(_requests, requestSource) && !requestSource.IsCancellationRequested && identity == Identity) { _loading = false; await InvokeAsync(StateHasChanged); }
        }
    }
    private async Task<(List<HoldWorkspaceView> Workspaces, string? Error)> LoadWorkspaces(CancellationToken ct)
    {
        try
        {
            CourseReference[] references;
            if (IsTeacher)
            {
                var courses = await Http.GetFromJsonAsync<CourseRow[]>("forloeb", ct) ?? [];
                references = courses.Where(c => c.Id != Guid.Empty && c.HoldId is Guid id && id != Guid.Empty).Select(c => new CourseReference(c.Id, c.HoldId!.Value)).ToArray();
            }
            else
            {
                if (ViewAs.IsActive && Redirect.CurrentAppRole() != AppRole.Underviser)
                    return ([], "Elevvisningen kunne ikke godkendes for din konto.");
                var path = ViewAs.IsActive ? $"me/memberships?asUser={ViewAs.AuthSub!.Value:D}" : "me/memberships";
                var memberships = await Http.GetFromJsonAsync<MembershipRow[]>(path, ct) ?? [];
                references = memberships.Where(m => m.ForloebId != Guid.Empty && m.HoldId != Guid.Empty).Select(m => new CourseReference(m.ForloebId, m.HoldId)).ToArray();
            }
            using var parallel = new SemaphoreSlim(4);
            var failed = false;
            async Task<HoldWorkspaceView?> Load(CourseReference reference)
            {
                await parallel.WaitAsync(ct);
                try
                {
                    using var response = await Http.GetAsync($"hold-workspace/{reference.CourseId:D}?publishedOnly={(!IsTeacher).ToString().ToLowerInvariant()}", ct);
                    if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return null;
                    response.EnsureSuccessStatusCode();
                    var workspace = await response.Content.ReadFromJsonAsync<HoldWorkspaceView>(ct);
                    if (workspace is null || workspace.CourseId != reference.CourseId || workspace.HoldId != reference.HoldId) { failed = true; return null; }
                    return workspace;
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException) { failed = true; return null; }
                finally { parallel.Release(); }
            }
            var loaded = await Task.WhenAll(references.Distinct().Select(Load));
            return (loaded.OfType<HoldWorkspaceView>().ToList(), failed ? "Nogle af dine hold kunne ikke hentes. Prøv at opdatere oversigten." : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            return ([], "Dine hold kunne ikke hentes. Prøv at opdatere oversigten.");
        }
    }
    private async Task<(List<PollView> Polls, string? Error)> LoadPolls(CancellationToken ct)
    {
        if (ViewAs.IsActive) return ([], null);
        try { return (await PollClient.List(ct), null); }
        catch (Exception ex) when (ex is PollException or HttpRequestException or OperationCanceledException)
        { return ([], "Afstemningerne kunne ikke hentes. Prøv at opdatere oversigten."); }
    }
    public void Dispose() { _disposed = true; Auth.AuthStateChanged -= AccountChanged; ViewAs.Changed -= AccountChanged; _requests.Cancel(); _requests.Dispose(); }
    private sealed record CourseRow(Guid Id, Guid? HoldId);
    private sealed record MembershipRow(Guid ForloebId, Guid HoldId);
    private sealed record CourseReference(Guid CourseId, Guid HoldId);
}
