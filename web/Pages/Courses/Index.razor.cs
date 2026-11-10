using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Learning;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages.Courses;

public partial class Index
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly StringComparer HoldNames = StringComparer.Create(Danish, ignoreCase: true);

    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private AuthRedirect Redirect { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "import")] public string? Import { get; set; }

    private CancellationTokenSource _request = new();
    private MyHoldCard[] _holds = [];
    private IReadOnlyDictionary<string, Guid> ImportedLectioHolds { get; set; } =
        new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> _savingFavorites = [];
    private readonly Dictionary<Guid, CardMessage> _cardMessages = [];
    private string _search = "", _source = "", _renameValue = "", _newName = "", _newDescription = "";
    private string? _loadError, _createError;
    private Guid? _renameId;
    private ElementReference _renameInput;
    private bool _loading = true, _showCreate, _creating, _renaming, _focusRename, _favoritesOnly, _disposed;

    private bool CanTeach => Redirect.ShowsTeacherChrome;
    private bool OpenLectio => string.Equals(Import, "lectio", StringComparison.OrdinalIgnoreCase);
    private string EmptyMessage => CanTeach
        ? "Opret et hold fra Lectio eller manuelt."
        : ViewAs.IsActive && ViewAs.AuthSub is null
            ? "Denne elevprofil har endnu ingen tilknyttet LUP-konto."
            : "Når en underviser tilføjer dig til et hold, finder du det her.";

    protected override async Task OnInitializedAsync()
    {
        Auth.AuthStateChanged += OnAuth;
        ViewAs.Changed += OnAuth;
        await LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusRename || _renameId is null) return;
        _focusRename = false;
        await _renameInput.FocusAsync();
    }

    private void OnAuth() => _ = InvokeAsync(async () =>
    {
        if (_disposed) return;
        var load = LoadAsync(reset: true);
        StateHasChanged();
        await load;
        if (!_disposed) StateHasChanged();
    });

    private Task RetryLoad() => LoadAsync();
    private Task ReloadAfterLectioImport() => LoadAsync();

    private async Task LoadAsync(bool reset = false)
    {
        _request.Cancel();
        _request.Dispose();
        _request = new();
        var ct = _request.Token;
        _loading = true;
        _loadError = null;
        _creating = _renaming = false;
        _savingFavorites.Clear();
        if (reset)
        {
            _holds = [];
            ImportedLectioHolds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            ClearFilters();
            CancelRename();
            _showCreate = false;
            _newName = _newDescription = "";
            _createError = null;
            _cardMessages.Clear();
        }

        try
        {
            if (!Auth.IsInitialized || !Auth.IsAuthenticated || ViewAs.IsActive && ViewAs.AuthSub is null) return;

            var path = ViewAs.IsActive && ViewAs.AuthSub is Guid pupil
                ? $"holds/mine?asUser={pupil:D}"
                : "holds/mine";
            var holds = await Http.GetFromJsonAsync<MyHoldCard[]>(path, ct) ?? [];
            if (!IsCurrent(ct)) return;

            _holds = holds;
            ImportedLectioHolds = holds
                .SelectMany(hold => hold.LectioSources.Select(source => (source.Key, hold.ForloebId)))
                .GroupBy(source => source.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().ForloebId, StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (IsCurrent(ct)) _loadError = "Kontrollér forbindelsen, og prøv igen.";
        }
        finally
        {
            if (IsCurrent(ct)) _loading = false;
        }
    }

    private MyHoldCard[] VisibleHolds()
    {
        var term = _search.Trim();
        return _holds
            .Where(hold => !_favoritesOnly || hold.IsFavorite)
            .Where(hold => _source.Length == 0 || HasLectio(hold) == (_source == "lectio"))
            .Where(hold => term.Length == 0
                || Matches(hold.HoldName, term)
                || Matches(hold.ForloebName, term)
                || Matches(hold.Description, term)
                || hold.LectioSources.Any(source => Matches(source.Name, term) || Matches(source.Key, term)))
            .OrderByDescending(hold => hold.IsFavorite)
            .ThenBy(hold => hold.HoldName, HoldNames)
            .ThenBy(hold => hold.HoldId)
            .ToArray();
    }

    private static bool Matches(string? value, string term) =>
        value is not null && Danish.CompareInfo.IndexOf(value, term, CompareOptions.IgnoreCase) >= 0;
    private static bool HasLectio(MyHoldCard hold) => hold.LectioSources.Length > 0;
    private static string HoldDate(DateTimeOffset value) => HoldTime.Local(value).ToString("d. MMM yyyy", Danish);
    private static string? SourceNames(MyHoldCard hold)
    {
        var names = hold.LectioSources.Select(source => source.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name)
                && !HoldNames.Equals(name, hold.HoldName)
                && !HoldNames.Equals(name, hold.ForloebName))
            .Distinct(HoldNames).ToArray();
        return names.Length > 0 ? string.Join(", ", names) : null;
    }

    private bool CanEdit(MyHoldCard hold) => CanTeach && hold.CanManage;
    private bool IsCurrent(CancellationToken ct) => !_disposed && !ct.IsCancellationRequested;

    private void ClearFilters()
    {
        _search = _source = "";
        _favoritesOnly = false;
    }

    private void ToggleCreate()
    {
        _showCreate = !_showCreate;
        _createError = null;
    }

    private async Task CreateAsync()
    {
        if (!CanTeach || _creating || string.IsNullOrWhiteSpace(_newName)) return;
        var ct = _request.Token;
        _creating = true;
        _createError = null;
        try
        {
            using var response = await Http.PostAsJsonAsync("forloeb",
                new { name = _newName.Trim(), description = _newDescription.Trim() }, ct);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<CreatedHold>(ct);
            if (!IsCurrent(ct)) return;
            if (created is null || created.Id == Guid.Empty) throw new JsonException("Missing hold ID.");
            Nav.NavigateTo($"/courses/{created.Id:D}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (IsCurrent(ct)) _createError = "Holdet kunne ikke oprettes. Kontrollér navnet og prøv igen.";
        }
        finally { if (IsCurrent(ct)) _creating = false; }
    }

    private void BeginRename(MyHoldCard hold)
    {
        if (!CanEdit(hold) || _renaming) return;
        _renameId = hold.HoldId;
        _renameValue = hold.HoldName;
        _focusRename = true;
        _cardMessages.Remove(hold.HoldId);
    }

    private void CancelRename()
    {
        _renameId = null;
        _renameValue = "";
        _focusRename = false;
    }

    private async Task RenameAsync(MyHoldCard hold)
    {
        if (!CanEdit(hold) || _renameId != hold.HoldId || _renaming) return;
        var name = _renameValue.Trim();
        if (name.Length is 0 or > 200) return;
        var ct = _request.Token;
        _renaming = true;
        _cardMessages.Remove(hold.HoldId);
        try
        {
            using var response = await Http.PatchAsJsonAsync($"holds/{hold.HoldId:D}/name", new RenameHold(name), ct);
            response.EnsureSuccessStatusCode();
            if (!IsCurrent(ct)) return;
            _holds = _holds.Select(item => item.HoldId == hold.HoldId ? item with { HoldName = name } : item).ToArray();
            _cardMessages[hold.HoldId] = new("Holdets navn er gemt.");
            CancelRename();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            if (IsCurrent(ct)) _cardMessages[hold.HoldId] = new("Navnet kunne ikke gemmes. Prøv igen.", IsError: true);
        }
        finally { if (IsCurrent(ct)) _renaming = false; }
    }

    private async Task ToggleFavorite(MyHoldCard hold)
    {
        if (ViewAs.IsActive || !_savingFavorites.Add(hold.HoldId)) return;
        var ct = _request.Token;
        var favorite = !hold.IsFavorite;
        _cardMessages.Remove(hold.HoldId);
        try
        {
            using var request = new HttpRequestMessage(favorite ? HttpMethod.Put : HttpMethod.Delete, $"holds/{hold.HoldId:D}/favorite");
            using var response = await Http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            if (IsCurrent(ct))
                _holds = _holds.Select(item => item.HoldId == hold.HoldId ? item with { IsFavorite = favorite } : item).ToArray();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            if (IsCurrent(ct)) _cardMessages[hold.HoldId] = new("Favoritten kunne ikke gemmes. Prøv igen.", IsError: true);
        }
        finally { if (IsCurrent(ct)) _savingFavorites.Remove(hold.HoldId); }
    }

    public void Dispose()
    {
        _disposed = true;
        Auth.AuthStateChanged -= OnAuth;
        ViewAs.Changed -= OnAuth;
        _request.Cancel();
        _request.Dispose();
    }

    private sealed record CreatedHold(Guid Id);
    private sealed record CardMessage(string Text, bool IsError = false);
}
