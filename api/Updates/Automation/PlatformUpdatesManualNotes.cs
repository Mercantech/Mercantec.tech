using System.Text.Json;

namespace Mercantec.Tech.Api.Updates.Automation;

internal sealed record PlatformUpdatesManualNote(string Id, string Title, string Description, string[] Authors);

internal static class PlatformUpdatesManualNotes
{
    private static readonly Lazy<IReadOnlyDictionary<string, PlatformUpdatesManualNote>> Notes = new(Load);
    public static IReadOnlyDictionary<string, PlatformUpdatesManualNote> All => Notes.Value;

    private static IReadOnlyDictionary<string, PlatformUpdatesManualNote> Load()
    {
        const string prefix = "platform-updates/notes/";
        var assembly = typeof(PlatformUpdatesManualNotes).Assembly;
        var result = new Dictionary<string, PlatformUpdatesManualNote>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)
                     && name.EndsWith(".json", StringComparison.Ordinal)))
        {
            var id = resource[prefix.Length..^5];
            if (!PlatformUpdatesAutomationValidation.IsId(id)) continue;
            try
            {
                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;
                var title = root.GetProperty("title").GetString();
                var description = root.GetProperty("description").GetString();
                if (!PlatformUpdatesAutomationValidation.IsText(title, 100)
                    || !PlatformUpdatesAutomationValidation.IsText(description, 500)) continue;
                var authors = root.TryGetProperty("authors", out var array) && array.ValueKind == JsonValueKind.Array
                    ? array.EnumerateArray().Select(item => item.GetString() ?? "").Where(value => value is "Kasper" or "Mathias").Distinct().ToArray()
                    : [];
                result[id] = new(id, title!, description!, authors);
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException) { }
        }
        return result;
    }
}
