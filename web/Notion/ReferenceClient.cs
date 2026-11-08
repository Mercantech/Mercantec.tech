using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Notion;

public sealed class ReferenceClient(HttpClient http)
{
    public async Task<ReferenceSearchResult> Search(string query, CancellationToken ct, string? sourceKind = null, string? sourceId = null, bool publishedOnly = false, bool contentOnly = false) =>
        await http.GetFromJsonAsync<ReferenceSearchResult>($"references/search?q={Uri.EscapeDataString(query)}&sourceKind={Uri.EscapeDataString(sourceKind ?? "")}&sourceId={Uri.EscapeDataString(sourceId ?? "")}&publishedOnly={publishedOnly.ToString().ToLowerInvariant()}&contentOnly={contentOnly.ToString().ToLowerInvariant()}&limit=30", ct) ?? new([], 0);
    public async Task<EntityGraph> Graph(ReferenceGraphRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("references/graph", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EntityGraph>(ct) ?? new([], [], 0, false);
    }
    public async Task<ResolvedReference[]> Backlinks(string kind, string id, CancellationToken ct, bool publishedOnly = false) =>
        await http.GetFromJsonAsync<ResolvedReference[]>($"references/backlinks?kind={Uri.EscapeDataString(kind)}&id={Uri.EscapeDataString(id)}&publishedOnly={publishedOnly.ToString().ToLowerInvariant()}", ct) ?? [];
}
