using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Avatars;

public interface IAvatarClient
{
    Task<AvatarProfileDto> GetAsync(CancellationToken ct = default, string? expectedSubject = null);
    Task<AvatarProfileDto> UnlockAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null);
    Task<AvatarProfileDto> SelectAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null);
    Task<AvatarProfileDto> ImportLegacyAsync(AvatarLegacyImportRequest request, CancellationToken ct = default, string? expectedSubject = null);
    Task<AvatarProfileDto> BuyItemAsync(string itemId, CancellationToken ct = default, string? expectedSubject = null);
}

public sealed class AvatarClient(HttpClient http) : IAvatarClient
{
    public Task<AvatarProfileDto> GetAsync(CancellationToken ct = default, string? expectedSubject = null) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, "me/avatars"), ct, expectedSubject);

    public Task<AvatarProfileDto> UnlockAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, $"me/avatars/{Uri.EscapeDataString(avatarId)}/unlock"), ct, expectedSubject);

    public Task<AvatarProfileDto> SelectAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Put, "me/avatars/selection")
        {
            Content = JsonContent.Create(new AvatarSelectionRequest(avatarId))
        }, ct, expectedSubject);

    public Task<AvatarProfileDto> ImportLegacyAsync(AvatarLegacyImportRequest request, CancellationToken ct = default, string? expectedSubject = null) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, "me/avatars/import") { Content = JsonContent.Create(request) }, ct, expectedSubject);

    public Task<AvatarProfileDto> BuyItemAsync(string itemId, CancellationToken ct = default, string? expectedSubject = null) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, $"me/avatars/items/{Uri.EscapeDataString(itemId)}/buy"), ct, expectedSubject);

    private async Task<AvatarProfileDto> SendAsync(HttpRequestMessage request, CancellationToken ct, string? expectedSubject)
    {
        try { return await SendCoreAsync(request, ct, expectedSubject); }
        catch (ObjectDisposedException)
        {
            throw new AvatarApiException("connection_closed", "Forbindelsen blev afbrudt. Hent din avatarsamling igen.");
        }
    }

    private async Task<AvatarProfileDto> SendCoreAsync(HttpRequestMessage request, CancellationToken ct, string? expectedSubject)
    {
        using (request)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            if (!string.IsNullOrWhiteSpace(expectedSubject))
                request.Headers.Add("X-LUP-Avatar-Subject", expectedSubject);
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                AvatarProblem? problem = null;
                try { problem = await response.Content.ReadFromJsonAsync<AvatarProblem>(timeout.Token); }
                catch (System.Text.Json.JsonException) { /* The API may return a non-JSON gateway error. */ }
                var fallback = response.StatusCode == HttpStatusCode.Unauthorized
                    ? "Log ind igen for at synkronisere din avatarsamling."
                    : "Din avatarsamling kunne ikke synkroniseres. Prøv igen.";
                throw new AvatarApiException(problem?.Code, string.IsNullOrWhiteSpace(problem?.Detail) ? fallback : problem.Detail);
            }

            return await response.Content.ReadFromJsonAsync<AvatarProfileDto>(timeout.Token)
                ?? throw new AvatarApiException("invalid_response", "Serveren gav et tomt svar. Prøv igen.");
        }
    }

    private sealed record AvatarProblem(string? Code, string? Detail);
}

public sealed class AvatarApiException(string? code, string message) : Exception(message)
{
    public string? Code { get; } = code;
}
