using System.Net.Http.Json;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Drawings;

public sealed class DrawingEngineClient(IHttpClientFactory clients, IConfiguration configuration, NotebookServiceKey key)
{
    private async Task<HttpResponseMessage> Post(string path, object body, CancellationToken ct)
    {
        var secret = key.Read() ?? throw new HttpRequestException("Document service is not configured.");
        var origin = configuration["Notebook:CollaborationBaseUrl"] ?? "http://mercantec-tech-collaboration:1234";
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(origin), path));
        request.Headers.Add("X-Notion-Service-Key", secret); request.Content = JsonContent.Create(body);
        using var client = clients.CreateClient(); client.Timeout = TimeSpan.FromSeconds(30);
        var result = await client.SendAsync(request, ct);
        if (!result.IsSuccessStatusCode) { result.Dispose(); throw new HttpRequestException("Drawing service is unavailable."); }
        return result;
    }

    public async Task<StoreDrawingDocument> CloneAsync(string? stateBase64, string? sceneJson, Dictionary<string, string> assetMap, CancellationToken ct)
    {
        using var response = await Post("/internal/drawing/clone", new { stateBase64, sceneJson, assetMap }, ct);
        var result = await response.Content.ReadFromJsonAsync<StoreDrawingDocument>(ct) ?? throw new HttpRequestException("Invalid drawing response.");
        DrawingValidation.Validate(result);
        return result;
    }
    public async Task CheckpointAsync(IEnumerable<Guid> drawingIds, CancellationToken ct)
    {
        var ids = drawingIds.Distinct().ToArray(); if (ids.Length == 0) return;
        using var response = await Post("/internal/drawing/checkpoint", new { drawingIds = ids }, ct);
    }
    public async Task InvalidateAsync(Guid drawingId, CancellationToken ct)
    {
        using var response = await Post("/internal/drawing/invalidate", new { drawingId }, ct);
    }
}
