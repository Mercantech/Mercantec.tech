using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mercantec.Tech.Api.Integrations.Lectio;

public class LectioCollectorClient(HttpClient http, ILogger<LectioCollectorClient> log) : ILectioMembershipSync
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<JsonElement> GetMeAsync(string bearerToken, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Get, "me", bearerToken);
        using var res = await http.SendAsync(req, ct);
        await EnsureSuccess(res, ct);
        return await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
    }

    public async Task<JsonElement> GetMyStudentAsync(string bearerToken, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Get, "me/student", bearerToken);
        using var res = await http.SendAsync(req, ct);
        await EnsureSuccess(res, ct);
        return await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
    }

    public async Task<JsonElement> GetLectioCandidatesAsync(string bearerToken, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Get, "me/lectio-candidates", bearerToken);
        using var res = await http.SendAsync(req, ct);
        await EnsureSuccess(res, ct);
        return await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
    }

    public async Task<JsonElement> GetProgrammeringseleverAsync(string bearerToken, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Get, "programmeringselever", bearerToken);
        using var res = await http.SendAsync(req, ct);
        await EnsureSuccess(res, ct);
        return await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
    }

    public async Task<JsonElement> GetStudieplanAsync(string elevId, string bearerToken, CancellationToken ct = default)
    {
        using var req = CreateRequest(
            HttpMethod.Get,
            $"people/students/{Uri.EscapeDataString(elevId)}/studieplan",
            bearerToken);
        using var res = await http.SendAsync(req, ct);
        await EnsureSuccess(res, ct);
        return await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
    }

    public async Task<JsonElement> GetTrackedHoldsAsync(string bearerToken, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Get, "holds/tracked", bearerToken);
        using var res = await http.SendAsync(req, ct);
        await EnsureSuccess(res, ct);
        return await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
    }

    public async Task<JsonElement> GetSavedHoldsAsync(string bearerToken, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Get, "holds/saved", bearerToken);
        using var res = await http.SendAsync(req, ct);
        await EnsureSuccess(res, ct);
        return await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
    }

    public async Task<JsonElement> GetHoldByLectioKeyAsync(string lectioHoldKey, string bearerToken, CancellationToken ct = default)
    {
        using var req = CreateRequest(
            HttpMethod.Get,
            $"holds/by-lectio/{Uri.EscapeDataString(lectioHoldKey)}",
            bearerToken);
        using var res = await http.SendAsync(req, ct);
        await EnsureSuccess(res, ct);
        return await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
    }

    public async Task<IReadOnlyDictionary<string, Guid>> ResolveAuthLinksAsync(
        IEnumerable<string> lectioIds,
        string bearerToken,
        CancellationToken ct = default)
    {
        var ids = lectioIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        using var req = CreateRequest(HttpMethod.Post, "auth-links/resolve", bearerToken);
        req.Content = JsonContent.Create(new { lectioIds = ids });
        using var res = await http.SendAsync(req, ct);
        // Ældre Collector uden endpoint → alle shadows
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        await EnsureSuccess(res, ct);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        if (json.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in json.EnumerateArray())
                TryAddLink(map, item);
        }
        else if (json.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in links.EnumerateArray())
                TryAddLink(map, item);
        }

        return map;
    }

    private static void TryAddLink(Dictionary<string, Guid> map, JsonElement item)
    {
        if (!item.TryGetProperty("lectioId", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            return;
        var lectioId = idEl.GetString();
        if (string.IsNullOrWhiteSpace(lectioId))
            return;
        if (!item.TryGetProperty("authSub", out var subEl))
            return;
        var raw = subEl.ValueKind == JsonValueKind.String ? subEl.GetString() : subEl.ToString();
        if (Guid.TryParse(raw, out var sub))
            map[lectioId] = sub;
    }

    public async Task LinkAsync(string lectioId, string bearerToken, Guid? authSub = null, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Put, "me/lectio-link", bearerToken);
        req.Content = JsonContent.Create(new { lectioId, authSub });
        using var res = await http.SendAsync(req, ct);
        await EnsureSuccess(res, ct);
    }

    public async Task<string?> ResolveLectioIdAsync(Guid authSub, string bearerToken, CancellationToken ct = default)
    {
        // Elev: /me. Underviser der spørger for sig selv får typisk null-link.
        _ = authSub;
        var me = await GetMeAsync(bearerToken, ct);
        if (me.TryGetProperty("lectio", out var lectio)
            && lectio.ValueKind == JsonValueKind.Object
            && lectio.TryGetProperty("lectioId", out var id)
            && id.ValueKind == JsonValueKind.String)
        {
            return id.GetString();
        }

        return null;
    }

    public Task<LectioSyncResult> SyncHoldMembershipsAsync(
        Guid holdId,
        string lectioHoldKey,
        string bearerToken,
        CancellationToken ct = default)
    {
        log.LogInformation(
            "Lectio membership sync stub — hold={HoldId} lectioHold={Key} (ikke implementeret endnu)",
            holdId, lectioHoldKey);
        return Task.FromResult(new LectioSyncResult(
            Implemented: false,
            Upserted: 0,
            SkippedManual: 0,
            Message: "Stub: fuld Lectio→Membership sync kommer senere. Manuelle memberships røres ikke."));
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relative, string bearerToken)
    {
        var req = new HttpRequestMessage(method, relative);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return req;
    }

    private static async Task EnsureSuccess(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var body = await res.Content.ReadAsStringAsync(ct);
        throw new LectioCollectorException((int)res.StatusCode, body);
    }
}

public sealed class LectioCollectorException(int statusCode, string body) : Exception($"Lectio-Collector HTTP {statusCode}: {body}")
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}
