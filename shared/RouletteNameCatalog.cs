using System.Text.Json;

namespace Mercantec.Tech.Contracts;

/// <summary>The same fixed vocabulary drives group names and the prerecorded announcer bank.</summary>
public static class RouletteNameCatalog
{
    public const string AssetRoot = "/audio/roulette/announcer-v1";
    public const string ManifestPath = AssetRoot + "/manifest.json";
    private static readonly (IReadOnlyList<string> Starts, IReadOnlyList<string> Ends) Words = ReadWords();
    public static IReadOnlyList<string> Starts => Words.Starts;
    public static IReadOnlyList<string> Ends => Words.Ends;

    public static string[]? GetClipUrls(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        for (var first = 0; first < Starts.Count; first++)
        {
            if (!name.StartsWith(Starts[first], StringComparison.Ordinal)) continue;
            for (var last = 0; last < Ends.Count; last++)
                if (name.AsSpan(Starts[first].Length).SequenceEqual(Ends[last]))
                    return [$"{AssetRoot}/prefix-{first:D2}.wav", $"{AssetRoot}/suffix-{last:D2}.wav"];
        }
        return null;
    }

    private static (IReadOnlyList<string>, IReadOnlyList<string>) ReadWords()
    {
        using var stream = typeof(RouletteNameCatalog).Assembly.GetManifestResourceStream("Mercantec.Tech.Contracts.RouletteWords")
            ?? throw new InvalidOperationException("Roulette word catalog is missing.");
        using var json = JsonDocument.Parse(stream);
        static IReadOnlyList<string> Read(JsonElement root, string property) =>
            Array.AsReadOnly(root.GetProperty(property).EnumerateArray().Select(word => word.GetString()!).ToArray());
        return (Read(json.RootElement, "starts"), Read(json.RootElement, "ends"));
    }
}
