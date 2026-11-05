using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Notion;

public sealed class NotionClient(HttpClient http)
{
    public Task<NotionHold[]> Holds(CancellationToken ct) => Get<NotionHold[]>("notion/holds", ct);
    public Task<NotionPageSummary[]> Pages(Guid holdId, CancellationToken ct) => Get<NotionPageSummary[]>($"notion/pages?holdId={holdId}", ct);
    public Task<NotionPageContext> Context(Guid id, CancellationToken ct) => Get<NotionPageContext>($"notion/pages/{id}/context", ct);

    public async Task<NotionPageSummary> Create(Guid holdId, string title, Guid? parentPageId, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("notion/pages", new { holdId, title, parentPageId }, ct);
        return await Read<NotionPageSummary>(response, ct);
    }

    public async Task<NotionPageSummary> Rename(Guid id, string title, CancellationToken ct)
    {
        using var response = await http.PatchAsJsonAsync($"notion/pages/{id}", new { title }, ct);
        return await Read<NotionPageSummary>(response, ct);
    }

    private async Task<T> Get<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        return await Read<T>(response, ct);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
            throw new NotionException((int)response.StatusCode switch
            {
                401 => "Log ind igen for at åbne dokumenterne.",
                403 => "Du har ikke adgang til at redigere dokumenter for dette hold.",
                404 => "Dokumentet eller holdet er ikke tilgængeligt for din konto.",
                400 => "Kontrollér titel og hold. Titlen må højst være 200 tegn.",
                503 => "Dokumenttjenesten er ikke sat op endnu. Kontakt administratoren.",
                _ => "Dokumenterne kunne ikke hentes. Prøv igen om lidt."
            });
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new NotionException("Dokumenttjenesten gav et tomt svar. Prøv igen.");
    }
}

public sealed class NotionException(string message) : Exception(message);
