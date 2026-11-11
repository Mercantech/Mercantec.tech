using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;

namespace Mercantec.Tech.Web.ProfileNames;

/// <summary>The account's confirmed LUP display name, shared by its profile and navigation.</summary>
public sealed class ProfileNameState : IDisposable
{
    private const string ProfilePath = "me/profile";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private readonly ViewAsStudentContext _viewAs;
    private AccountProfile? _account;
    private bool _disposed;

    public ProfileNameState(HttpClient http, AuthService auth, ViewAsStudentContext viewAs)
    {
        _http = http;
        _auth = auth;
        _viewAs = viewAs;
        _auth.AuthStateChanged += AccountChanged;
        _viewAs.Changed += AccountChanged;
    }

    public event Action? Changed;
    public string? DisplayName => CurrentAccount?.DisplayName;
    public bool CanEdit => OwnSubject.HasValue;
    public bool IsLoaded => CurrentAccount?.IsLoaded == true;
    public bool IsLoading => CurrentAccount?.IsLoading == true;
    public bool IsSaving => CurrentAccount?.IsSaving == true;
    public string? LoadError => CurrentAccount?.LoadError;
    public string? SaveError => CurrentAccount?.SaveError;

    private Guid? OwnSubject => !_disposed && _auth.IsInitialized && _auth.IsAuthenticated && !_viewAs.IsActive
        && Guid.TryParse(_auth.Subject, out var subject) && subject != Guid.Empty ? subject : null;
    private AccountProfile? CurrentAccount => _account is { } account && OwnSubject == account.Subject ? account : null;

    public Task EnsureAsync() => LoadAsync(reload: false);
    public Task ReloadAsync() => LoadAsync(reload: true);

    private Task LoadAsync(bool reload)
    {
        var account = SyncAccount();
        if (account is null || account.IsSaving) return Task.CompletedTask;
        if (account.LoadTask is { IsCompleted: false } pending) return pending;
        if (!reload && (account.IsLoaded || account.LoadError is not null)) return Task.CompletedTask;

        var load = account.LoadTask = ReadAsync(account);
        if (account.IsLoading && IsCurrent(account)) Changed?.Invoke();
        return load;
    }

    private async Task ReadAsync(AccountProfile account)
    {
        account.IsLoading = true;
        account.LoadError = null;
        using var timeout = RequestLifetime(account);
        try
        {
            using var request = CreateRequest(HttpMethod.Get, account);
            using var response = await _http.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            var profile = await response.Content.ReadFromJsonAsync<ProfileNameDto>(timeout.Token)
                ?? throw new JsonException("Profilen mangler i svaret.");
            timeout.Token.ThrowIfCancellationRequested();
            if (!IsCurrent(account)) return;
            account.DisplayName = profile.DisplayName;
            account.IsLoaded = true;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (IsCurrent(account)) account.LoadError = "Profilnavnet kunne ikke hentes. Prøv igen.";
        }
        finally
        {
            account.IsLoading = false;
            if (IsCurrent(account)) Changed?.Invoke();
        }
    }

    public async Task<bool> SaveAsync(string? displayName)
    {
        if (CurrentAccount is not { IsLoaded: true, IsLoading: false, IsSaving: false } account) return false;

        using var timeout = RequestLifetime(account);
        account.IsSaving = true;
        account.SaveError = null;
        Changed?.Invoke();

        var saved = false;
        try
        {
            using var request = CreateRequest(HttpMethod.Put, account);
            request.Content = JsonContent.Create(new UpdateProfileNameRequest(displayName));
            using var response = await _http.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            var profile = await response.Content.ReadFromJsonAsync<ProfileNameDto>(timeout.Token)
                ?? throw new JsonException("Profilen mangler i svaret.");
            timeout.Token.ThrowIfCancellationRequested();
            if (!IsCurrent(account)) return false;
            account.DisplayName = profile.DisplayName;
            saved = true;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (IsCurrent(account))
                account.SaveError = error is HttpRequestException { StatusCode: HttpStatusCode.BadRequest }
                    ? $"Navnet kunne ikke bruges. Kontrollér, at det har højst {ProfileNameRules.MaxLength} tegn."
                    : "Profilnavnet kunne ikke gemmes. Prøv igen.";
        }
        finally
        {
            account.IsSaving = false;
            if (IsCurrent(account)) Changed?.Invoke();
        }

        return saved && IsCurrent(account);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, AccountProfile account)
    {
        var request = new HttpRequestMessage(method, ProfilePath);
        request.Headers.Add(ProfileNameRules.SubjectHeader, account.Subject.ToString("D"));
        return request;
    }

    private static CancellationTokenSource RequestLifetime(AccountProfile account)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(account.Lifetime.Token);
        timeout.CancelAfter(RequestTimeout);
        return timeout;
    }

    private bool IsCurrent(AccountProfile account) => ReferenceEquals(account, CurrentAccount);

    private AccountProfile? SyncAccount()
    {
        var subject = OwnSubject;
        if (_account?.Subject == subject) return _account;

        var previous = _account;
        _account = subject is Guid id ? new AccountProfile(id) : null;
        previous?.Dispose();
        Changed?.Invoke();
        return CurrentAccount;
    }

    private void AccountChanged()
    {
        var previous = _account;
        var current = SyncAccount();
        if (current is not null && !ReferenceEquals(previous, current)) _ = EnsureAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _auth.AuthStateChanged -= AccountChanged;
        _viewAs.Changed -= AccountChanged;
        _account?.Dispose();
        _account = null;
    }

    private sealed class AccountProfile(Guid subject) : IDisposable
    {
        public Guid Subject { get; } = subject;
        public CancellationTokenSource Lifetime { get; } = new();
        public Task? LoadTask { get; set; }
        public string? DisplayName { get; set; }
        public bool IsLoaded { get; set; }
        public bool IsLoading { get; set; }
        public bool IsSaving { get; set; }
        public string? LoadError { get; set; }
        public string? SaveError { get; set; }

        public void Dispose()
        {
            Lifetime.Cancel();
            Lifetime.Dispose();
        }
    }
}
