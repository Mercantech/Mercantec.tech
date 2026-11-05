using System.Text.Json;

namespace Mercantec.Tech.Web.Lectio;

/// <summary>
/// Kobler Auth-sub (platform-konti) til Lectio-elever via
/// <c>programmeringselever</c> + <c>auth-links/resolve</c>.
/// </summary>
public static class AuthPlatformLinks
{
    public sealed record Link(string LectioId, string? Name, string? ClassName);

    /// <summary>
    /// Bygger map Auth-sub → Lectio-elev for dem, der er koblet (på platformen).
    /// </summary>
    public static IReadOnlyDictionary<Guid, Link> MapByAuthSub(
        string programmeringseleverJson,
        string authLinksResolveJson)
    {
        var students = IndexStudents(programmeringseleverJson);
        var result = new Dictionary<Guid, Link>();

        foreach (var (lectioId, authSub) in ParseAuthLinks(authLinksResolveJson))
        {
            if (authSub == Guid.Empty || string.IsNullOrWhiteSpace(lectioId))
                continue;

            students.TryGetValue(lectioId, out var student);
            result[authSub] = new Link(
                lectioId,
                student?.Name,
                student?.ClassName);
        }

        return result;
    }

    /// <summary>
    /// Lectio-id'er fra <c>students[]</c> i programmeringselever-svaret.
    /// </summary>
    public static IReadOnlyList<string> ExtractLectioIds(string programmeringseleverJson)
    {
        return IndexStudents(programmeringseleverJson).Keys.ToList();
    }

    private static Dictionary<string, Link> IndexStudents(string listJson)
    {
        var map = new Dictionary<string, Link>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(listJson))
            return map;

        using var doc = JsonDocument.Parse(listJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("students", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return map;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = GetString(item, "lectioId");
            if (string.IsNullOrWhiteSpace(id))
                continue;
            map[id] = new Link(id, GetString(item, "name"), GetString(item, "className"));
        }

        return map;
    }

    private static IEnumerable<(string LectioId, Guid AuthSub)> ParseAuthLinks(string resolveJson)
    {
        if (string.IsNullOrWhiteSpace(resolveJson))
            yield break;

        using var doc = JsonDocument.Parse(resolveJson);
        var root = doc.RootElement;
        IEnumerable<JsonElement> items;
        if (root.ValueKind == JsonValueKind.Array)
            items = root.EnumerateArray();
        else if (root.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
            items = links.EnumerateArray();
        else
            yield break;

        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = GetString(item, "lectioId");
            if (string.IsNullOrWhiteSpace(id) || !item.TryGetProperty("authSub", out var subEl))
                continue;
            var raw = subEl.ValueKind == JsonValueKind.String ? subEl.GetString() : subEl.ToString();
            if (Guid.TryParse(raw, out var sub) && sub != Guid.Empty)
                yield return (id, sub);
        }
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
