using System.Globalization;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages.Courses;

public partial class Detail
{
    [Inject] private AuthService Auth { get; set; } = null!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = null!;
    [Inject] private HttpClient Http { get; set; } = null!;
    [Parameter] public Guid Id { get; set; }
    [SupplyParameterFromQuery(Name = "view")] public string? View { get; set; }
    [SupplyParameterFromQuery(Name = "create")] public bool Create { get; set; }
    private HoldWorkspaceView? _workspace;
    private CancellationTokenSource _requests = new();
    private string? _identity, _error, _notice;
    private Guid _loadedId;
    private bool _loading = true, _disposed, _adding;
    private string _memberName = "", _memberSub = "", _memberLectio = "";
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private bool CanManage => _workspace?.CanManage == true && !ViewAs.IsActive;
    private string ActiveView => Tabs.Any(t => t.Key == View) ? View! : "overview";
    private string EditorHref => $"/notion?hold={_workspace!.HoldId}" + (CanManage ? "" : "&published=true");
    private string AssignmentsHref => $"/afleveringer?hold={_workspace!.HoldId}";
    private static string QuestHref(Guid questId) => $"/quests/{questId}";
    private string PageHref(Guid pageId) => $"/notion/{pageId}" + (CanManage ? "" : "?published=true");
    private string ViewHref(string view, bool create = false) => $"/courses/{Id}?view={view}" + (create ? "&create=true" : "");
    private static DateTimeOffset LocalTime(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Copenhagen);
    private static string LocalDate(DateTimeOffset value) => LocalTime(value).ToString("d. MMM yyyy", Danish);
    private static string PageState(WorkspacePage page) => page.PublishedAtUtc is null ? "Kladde" : page.HasUnpublishedChanges ? "Ny kladde" : "Udgivet";
    private IEnumerable<(string Key, string Label, string Icon, int? Count)> Tabs
    {
        get
        {
            yield return ("overview", "Overblik", "screen", null);
            yield return ("content", "Forløb", "book", _workspace?.Pages.Length);
            yield return ("quests", "Quests", "target", _workspace?.QuestCount);
            yield return ("calendar", "Kalender", "calendar", null);
            yield return ("groups", "Elever og grupper", "people", _workspace?.GroupCount);
            yield return ("materials", "Materialer", "folder", _workspace?.MaterialCount);
            if (CanManage) yield return ("settings", "Holdindstillinger", "sliders", null);
        }
    }
    protected override void OnInitialized() { Auth.AuthStateChanged += AccountChanged; ViewAs.Changed += AccountChanged; }
    protected override async Task OnParametersSetAsync()
    {
        if (!Auth.IsInitialized) return;
        if (_loadedId != Id || _identity != $"{Auth.Subject}:{ViewAs.IsActive}") await Reload();
    }
    private void AccountChanged() => _ = InvokeAsync(async () =>
    {
        if (_identity != $"{Auth.Subject}:{ViewAs.IsActive}") await Reload();
        StateHasChanged();
    });
    private async Task Reload()
    {
        _workspace = null; _memberName = _memberSub = _memberLectio = ""; _notice = null;
        await RefreshSummary();
    }
    private async Task RefreshSummary()
    {
        if (_disposed || !Auth.IsInitialized) return;
        _requests.Cancel(); _requests.Dispose(); _requests = new();
        var ct = _requests.Token;
        _identity = $"{Auth.Subject}:{ViewAs.IsActive}"; _loadedId = Id;
        _loading = _workspace is null; _error = null;
        if (!Auth.IsAuthenticated) { _loading = false; return; }
        try
        {
            using var response = await Http.GetAsync($"hold-workspace/{Id}?publishedOnly={ViewAs.IsActive.ToString().ToLowerInvariant()}", ct);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode == System.Net.HttpStatusCode.NotFound
                ? "Holdet findes ikke, eller din konto har ikke adgang til det." : "Holdet kunne ikke hentes. Kontrollér forbindelsen, og prøv igen.");
            var result = await response.Content.ReadFromJsonAsync<HoldWorkspaceView>(ct);
            ct.ThrowIfCancellationRequested();
            _workspace = result ?? throw new HttpRequestException("Serveren returnerede et tomt overblik.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        { if (!ct.IsCancellationRequested) _error = ex is HttpRequestException ? ex.Message : "Holdets overblik kunne ikke læses. Prøv igen."; }
        finally { if (!ct.IsCancellationRequested) _loading = false; }
    }
    private async Task AddMember()
    {
        if (_adding || !CanManage || _workspace is null) return;
        _error = _notice = null; Guid? sub = null;
        if (!string.IsNullOrWhiteSpace(_memberSub))
        {
            if (!Guid.TryParse(_memberSub, out var id) || id == Guid.Empty) { _error = "Kontoens Auth-id skal være en gyldig GUID."; return; }
            sub = id;
        }
        if (sub is null && string.IsNullOrWhiteSpace(_memberLectio)) { _error = "Angiv et Lectio-id eller kontoens Auth-id."; return; }
        _adding = true;
        try
        {
            using var response = await Http.PostAsJsonAsync($"holds/{_workspace.HoldId}/memberships", new
            { userSub = sub, lectioId = string.IsNullOrWhiteSpace(_memberLectio) ? null : _memberLectio.Trim(), displayName = _memberName.Trim(), source = "manual" }, _requests.Token);
            if (!response.IsSuccessStatusCode) { _error = response.StatusCode == System.Net.HttpStatusCode.Conflict ? "Eleven findes allerede på holdet." : "Eleven kunne ikke tilføjes. Kontrollér oplysningerne, og prøv igen."; return; }
            _memberName = _memberSub = _memberLectio = "";
            await RefreshSummary(); _notice = "Eleven er tilføjet til holdet.";
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { _error = "Eleven kunne ikke tilføjes. Kontrollér forbindelsen, og prøv igen."; }
        finally { _adding = false; }
    }
    public void Dispose()
    {
        _disposed = true; Auth.AuthStateChanged -= AccountChanged; ViewAs.Changed -= AccountChanged;
        _requests.Cancel(); _requests.Dispose();
    }
}
