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
            // Undgå DefaultInboundClaimTypeMap ("role" → lang ClaimTypes.Role-URI)
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            var jwt = handler.ReadJwtToken(accessToken);

            Subject = FirstClaim(jwt, "sub");
            Email = FirstClaim(jwt, "email");
            LoginMethod = FirstClaim(jwt, "login_method");
            DisplayName = FirstClaim(jwt, "name") ?? Email ?? Subject;
            Roles = ReadRoles(jwt);
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

    private static string? FirstClaim(System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt, string type) =>
        jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value
        ?? jwt.Payload.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    private static string[] ReadRoles(System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt)
    {
        // Gentagne "role"-claims og evt. array i payload
        var fromClaims = jwt.Claims
            .Where(IsRoleClaim)
            .Select(c => c.Value)
            .ToList();

        if (jwt.Payload.TryGetValue("role", out var roleValue))
            fromClaims.AddRange(FlattenPayloadRoles(roleValue));

        if (jwt.Payload.TryGetValue("roles", out var rolesValue))
            fromClaims.AddRange(FlattenPayloadRoles(rolesValue));

        return fromClaims
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r)
            .ToArray();
    }

    private static bool IsRoleClaim(System.Security.Claims.Claim c) =>
        c.Type is "role" or "roles"
        || c.Type.Equals(System.Security.Claims.ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
        || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> FlattenPayloadRoles(object? value) => value switch
    {
        string s => [s],
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } el => [el.GetString()!],
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } arr =>
            arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0),
        System.Collections.IEnumerable list when value is not string =>
            list.Cast<object?>().Select(o => o?.ToString() ?? "").Where(x => x.Length > 0),
        _ => []
    };

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
