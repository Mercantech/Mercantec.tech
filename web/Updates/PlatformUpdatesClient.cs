using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Updates;

public sealed class PlatformUpdatesClient(HttpClient http)
{
    public async Task<PlatformUpdate[]> Get(CancellationToken ct)
    {
        using var response = await http.GetAsync("platform-updates?includeText=true", ct);
        Check(response);
        try
        {
            return await response.Content.ReadFromJsonAsync<PlatformUpdate[]>(ct)
                ?? throw new PlatformUpdatesException("Opdateringerne kunne ikke læses. Prøv igen.");
        }
        catch (System.Text.Json.JsonException)
        {
            throw new PlatformUpdatesException("Opdateringerne kunne ikke læses. Prøv igen.");
        }
    }

    public async Task<string> Image(string id, CancellationToken ct)
    {
        using var response = await http.GetAsync($"platform-updates/images/{Uri.EscapeDataString(id)}", ct);
        Check(response);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not ("image/webp" or "image/png"))
            throw new PlatformUpdatesException("Billedet kunne ikke hentes.");
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return $"data:{contentType};base64," + Convert.ToBase64String(bytes);
    }

    private static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new PlatformUpdatesException((int)response.StatusCode switch
        {
            401 => "Log ind igen for at se opdateringerne.",
            403 => "Opdateringerne er kun tilgængelige for undervisere.",
            _ => "Opdateringerne kunne ikke hentes. Prøv igen."
        });
    }
}

public sealed class PlatformUpdatesException(string message) : Exception(message);
