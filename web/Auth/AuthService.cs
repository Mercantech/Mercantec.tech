using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Web.Auth;

public sealed class AuthService(
    IOptions<AuthOptions> options,
    TokenStore tokens,
    NavigationManager navigation)
{
    private readonly AuthOptions _options = options.Value;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static readonly HttpClient TokenHttp = new();

    public bool IsInitialized { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public string? AccessToken { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Subject { get; private set; }
    public string? Email { get; private set; }
    public string? LoginMethod { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];

    public event Action? AuthStateChanged;

    public async Task InitializeAsync()
    {
        var (access, refresh, expiresAt) = await tokens.GetTokensAsync();
        if (string.IsNullOrEmpty(access))
        {
            ClearLocalState();
            IsInitialized = true;
            Notify();
            return;
        }

        if (expiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiresAt - 30)
        {
            if (!string.IsNullOrEmpty(refresh))
            {
                var ok = await RefreshAsync(refresh);
                if (!ok)
                {
                    await LogoutLocalAsync();
                    IsInitialized = true;
                    Notify();
                    return;
                }
            }
            else
            {
                await LogoutLocalAsync();
                IsInitialized = true;
                Notify();
                return;
            }
        }
        else
        {
            ApplyAccessToken(access);
        }

        IsInitialized = true;
        Notify();
    }

    public async Task BeginLoginAsync()
    {
        var verifier = Pkce.CreateVerifier();
        var challenge = Pkce.CreateChallenge(verifier);
        var state = Pkce.CreateState();
        await tokens.SetPkceAsync(verifier, state);

        var redirectUri = GetRedirectUri();
        var url =
            $"{_options.AuthorizeUrl}" +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(_options.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString(_options.Scope)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            $"&code_challenge_method=S256";

        // Fuld browser-navigation — ikke fetch
        navigation.NavigateTo(url, forceLoad: true);
    }

    public async Task<bool> CompleteLoginAsync(string? code, string? state, string? error)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            return false;

        var (verifier, expectedState) = await tokens.GetPkceAsync();
        await tokens.ClearPkceAsync();

        if (string.IsNullOrEmpty(verifier) || string.IsNullOrEmpty(expectedState) || expectedState != state)
            return false;

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = GetRedirectUri(),
            ["client_id"] = _options.ClientId,
            ["code_verifier"] = verifier
        };

        return await ExchangeTokenAsync(body);
    }

    public async Task<string?> GetValidAccessTokenAsync()
    {
        var (access, refresh, expiresAt) = await tokens.GetTokensAsync();
        if (string.IsNullOrEmpty(access))
            return null;

        if (expiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiresAt - 30)
        {
            if (string.IsNullOrEmpty(refresh))
                return null;

            var ok = await RefreshAsync(refresh);
            return ok ? AccessToken : null;
        }

        if (!IsAuthenticated)
            ApplyAccessToken(access);

        return access;
    }

    public async Task<bool> TryRefreshOnUnauthorizedAsync()
    {
        var (_, refresh, _) = await tokens.GetTokensAsync();
        if (string.IsNullOrEmpty(refresh))
            return false;

        return await RefreshAsync(refresh);
    }

    public async Task LogoutLocalAsync()
    {
        await tokens.ClearTokensAsync();
        ClearLocalState();
        IsInitialized = true;
        Notify();
    }

    private async Task<bool> RefreshAsync(string refreshToken)
    {
        await _refreshLock.WaitAsync();
        try
        {
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _options.ClientId
            };
            return await ExchangeTokenAsync(body);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<bool> ExchangeTokenAsync(Dictionary<string, string> body)
    {
        using var content = new FormUrlEncodedContent(body);
        using var response = await TokenHttp.PostAsync(_options.TokenUrl, content);
        if (!response.IsSuccessStatusCode)
            return false;

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (payload?.AccessToken is null)
            return false;

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn > 0 ? payload.ExpiresIn : 900);
        var refresh = payload.RefreshToken
            ?? (await tokens.GetTokensAsync()).Refresh
            ?? string.Empty;

        await tokens.SetTokensAsync(payload.AccessToken, refresh, expiresAt);
        ApplyAccessToken(payload.AccessToken);
        Notify();
        return true;
    }

    private void ApplyAccessToken(string accessToken)
    {
        AccessToken = accessToken;
        IsAuthenticated = true;
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(accessToken);
            Subject = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            Email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            LoginMethod = jwt.Claims.FirstOrDefault(c => c.Type == "login_method")?.Value;
            DisplayName = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value
                ?? Email
                ?? Subject;
            Roles = jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).Distinct().ToArray();
        }
        catch
        {
            DisplayName = null;
            Subject = null;
            Email = null;
            LoginMethod = null;
            Roles = [];
        }
    }

    private void ClearLocalState()
    {
        AccessToken = null;
        IsAuthenticated = false;
        DisplayName = null;
        Subject = null;
        Email = null;
        LoginMethod = null;
        Roles = [];
    }

    private string GetRedirectUri()
    {
        var baseUri = navigation.BaseUri.TrimEnd('/');
        var path = _options.RedirectPath.StartsWith('/') ? _options.RedirectPath : "/" + _options.RedirectPath;
        return baseUri + path;
    }

    private void Notify() => AuthStateChanged?.Invoke();

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }
}
