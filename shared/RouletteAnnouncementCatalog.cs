using System.Text.Json;

namespace Mercantec.Tech.Contracts;

/// <summary>Generic prerecorded announcements; pupils' names never enter the voice bank.</summary>
public static class RouletteAnnouncementCatalog
{
    public const string AssetRoot = "/audio/roulette/cues-v1";
    public const string ManifestPath = AssetRoot + "/manifest.json";
    public const string ManifestUrl = ManifestPath + "?v=" + RouletteNameCatalog.RuntimeVersion;
    public static IReadOnlyList<RouletteAnnouncementCue> All { get; } = ReadCues();

    public static string? GetClipUrl(string? id) =>
        All.FirstOrDefault(cue => string.Equals(cue.Id, id, StringComparison.Ordinal)) is { } cue
            ? AssetRoot + "/" + cue.File : null;

    private static IReadOnlyList<RouletteAnnouncementCue> ReadCues()
    {
        using var stream = typeof(RouletteAnnouncementCatalog).Assembly.GetManifestResourceStream("Mercantec.Tech.Contracts.RouletteAnnouncements")
            ?? throw new InvalidOperationException("Roulette announcement catalog is missing.");
        using var json = JsonDocument.Parse(stream);
        return Array.AsReadOnly(json.RootElement.GetProperty("cues").EnumerateArray()
            .Select(cue => new RouletteAnnouncementCue(cue.GetProperty("id").GetString()!,
                cue.GetProperty("text").GetString()!, cue.GetProperty("id").GetString()! + ".wav")).ToArray());
    }
}

public sealed record RouletteAnnouncementCue(string Id, string Text, string File);
