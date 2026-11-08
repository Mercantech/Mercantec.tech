using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Notion;

public sealed class NotionMcpClient(HttpClient http)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web) { MaxDepth = 512 };
    public async Task<NotionMcpChangeSummary[]> List(Guid pageId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"notion/pages/{pageId}/codex-changes", ct);
        return await Read<NotionMcpChangeSummary[]>(response, ct);
    }

    public async Task<NotionMcpChangeView> Get(Guid pageId, Guid operationId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"notion/pages/{pageId}/codex-changes/{operationId}", ct);
        return await Read<NotionMcpChangeView>(response, ct);
    }

    public async Task<NotionMcpChangeView> Undo(Guid pageId, Guid operationId, Guid reversalId, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"notion/pages/{pageId}/codex-changes/{operationId}/undo", new UndoNotionMcpChange(reversalId), ct);
        return await Read<NotionMcpChangeView>(response, ct);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            string? code = null;
            try { code = (await response.Content.ReadFromJsonAsync<ErrorResponse>(ct))?.Error; }
            catch (System.Text.Json.JsonException) { }
            throw new NotionException(response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Log ind igen for at åbne historikken.",
                System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound => "Du har ikke længere adgang til ændringen på denne side.",
                System.Net.HttpStatusCode.Conflict => "Teksten er ændret siden Codex-redigeringen. Den kan ikke fortrydes automatisk. Opdatér historikken for at se den aktuelle status.",
                System.Net.HttpStatusCode.ServiceUnavailable => "Lagringen kan ikke bekræftes lige nu. Opdatér historikken, før du prøver igen.",
                _ => "Historikken kunne ikke hentes. Prøv at opdatere den."
            }, code);
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new NotionException("Historikken gav et tomt svar.");
    }

    private sealed record ErrorResponse(string? Error);
}
