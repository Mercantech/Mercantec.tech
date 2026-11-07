using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Notebooks;

/// <summary>Clones live Yjs content in the existing Node service; ASP.NET owns the resulting transaction.</summary>
public sealed class NotebookEngineClient(HttpClient http, IConfiguration configuration, NotebookServiceKey key)
{
    public async Task<StoreNotebookDocument> CloneAsync(CloneNotebookDocument clone, CancellationToken ct)
    {
        var secret = key.Read() ?? throw new HttpRequestException("Document service is not configured.");
        var origin = configuration["Notebook:CollaborationBaseUrl"] ?? "http://mercantec-tech-collaboration:1234";
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(origin), "/internal/notebook/clone"));
        request.Headers.Add("X-Notion-Service-Key", secret);
        request.Content = JsonContent.Create(clone);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StoreNotebookDocument>(ct)
            ?? throw new HttpRequestException("Document service returned an invalid document.");
        if (!NotebookDocumentValidation.ValidProjection(result, out _))
            throw new HttpRequestException("Document service returned an invalid document.");
        return result;
    }
}
