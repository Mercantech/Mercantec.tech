using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mercantec.Tech.Web.Extensions;

public sealed record ExtensionAccessKey(Guid Id, string Label, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? LastUsedAtUtc, DateTimeOffset? RevokedAtUtc);
public sealed record ExtensionAccessState(bool Enabled, IReadOnlyList<ExtensionAccessKey> Keys);
public sealed record NewExtensionAccess(Guid Id, string Key, string Label, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);

/// <summary>Uses the application's Mercantec-authenticated client. Credentials are never persisted here.</summary>
public sealed class ExtensionAccessClient(HttpClient http)
{
    public async Task<ExtensionAccessState> Get(CancellationToken ct = default)
    {
        using var response = await Send(() => http.GetAsync("extension/access", ct), ct);
        return await Read<ExtensionAccessState>(response, ct);
    }

    public async Task<NewExtensionAccess> Create(string label, CancellationToken ct = default)
    {
        using var response = await Send(() => http.PostAsJsonAsync("extension/access", new { label }, ct), ct);
        var value = await Read<NewExtensionAccess>(response, ct);
        if (value.Id == Guid.Empty || value.Key is not { Length: 47 } || !value.Key.StartsWith("lup_", StringComparison.Ordinal))
            throw new ExtensionAccessException("Nøglen kunne ikke læses. Genindlæs listen, og tilbagekald den nye nøgle, før du prøver igen.");
        return value;
    }

    public async Task Revoke(Guid id, CancellationToken ct = default)
    {
        using var response = await Send(() => http.DeleteAsync($"extension/access/{id:D}", ct), ct);
    }

    private static async Task<HttpResponseMessage> Send(Func<Task<HttpResponseMessage>> action, CancellationToken ct)
    {
        HttpResponseMessage response;
        try { response = await action(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ExtensionAccessException("LUP svarede ikke i tide. Prøv igen."); }
        catch (HttpRequestException) { throw new ExtensionAccessException("Forbindelsen til LUP kunne ikke oprettes."); }
        if (response.IsSuccessStatusCode) return response;
        var status = response.StatusCode;
        response.Dispose();
        // Do not surface response bodies: a proxy or backend error must never echo a credential.
        throw new ExtensionAccessException(status switch
        {
            HttpStatusCode.Unauthorized => "Log ind igen for at administrere udvidelsens adgang.",
            HttpStatusCode.Forbidden => "Kun de godkendte konti kan oprette adgang til udvidelsen.",
            HttpStatusCode.Conflict => "Du har allerede fem aktive nøgler. Tilbagekald en nøgle, før du opretter en ny.",
            HttpStatusCode.BadRequest => "Giv forbindelsen et navn på højst 80 tegn.",
            HttpStatusCode.NotFound => "Adgangen findes ikke længere. Genindlæs listen.",
            _ => "Adgangen kunne ikke opdateres. Prøv igen."
        });
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new JsonException(); }
        catch (Exception error) when (error is JsonException or IOException or HttpRequestException)
        { throw new ExtensionAccessException("LUP returnerede et ugyldigt svar. Genindlæs siden, og prøv igen."); }
    }
}

public sealed class ExtensionAccessException(string message) : Exception(message);
