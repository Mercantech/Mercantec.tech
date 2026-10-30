using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Auth;

public sealed class TokenStore(IJSRuntime js)
{
    public async Task SetTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt) =>
        await js.InvokeVoidAsync("mercantecAuth.setTokens", accessToken, refreshToken, expiresAt.ToUnixTimeSeconds());

    public async Task<(string? Access, string? Refresh, long ExpiresAt)> GetTokensAsync()
    {
        var result = await js.InvokeAsync<TokenBag>("mercantecAuth.getTokens");
        return (result?.Access, result?.Refresh, result?.ExpiresAt ?? 0);
    }

    public async Task ClearTokensAsync() =>
        await js.InvokeVoidAsync("mercantecAuth.clearTokens");

    public async Task SetPkceAsync(string verifier, string state) =>
        await js.InvokeVoidAsync("mercantecAuth.setPkce", verifier, state);

    public async Task<(string? Verifier, string? State)> GetPkceAsync()
    {
        var result = await js.InvokeAsync<PkceBag>("mercantecAuth.getPkce");
        return (result?.Verifier, result?.State);
    }

    public async Task ClearPkceAsync() =>
        await js.InvokeVoidAsync("mercantecAuth.clearPkce");

    private sealed class TokenBag
    {
        [JsonPropertyName("access")]
        public string? Access { get; set; }

        [JsonPropertyName("refresh")]
        public string? Refresh { get; set; }

        [JsonPropertyName("expiresAt")]
        public long ExpiresAt { get; set; }
    }

    private sealed class PkceBag
    {
        [JsonPropertyName("verifier")]
        public string? Verifier { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }
    }
}
