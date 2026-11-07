using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Learning;

public sealed class RouletteSpeechClient(HttpClient http, NavigationManager navigation)
{
    public async Task<RouletteSpeechStatus> Status(CancellationToken ct)
    {
        var manifestUri = new Uri(new Uri(navigation.BaseUri), RouletteNameCatalog.ManifestUrl);
        using var response = await http.GetAsync(manifestUri, ct);
        if (!response.IsSuccessStatusCode)
            return Unavailable();
        SpeechManifest? manifest;
        try { manifest = await response.Content.ReadFromJsonAsync<SpeechManifest>(ct); }
        catch (JsonException) { return Unavailable(); }
        var expected = RouletteNameCatalog.Starts.Select((text, index) => new SpeechClip($"prefix-{index:00}", text, $"prefix-{index:00}.wav"))
            .Concat(RouletteNameCatalog.Ends.Select((text, index) => new SpeechClip($"suffix-{index:00}", text, $"suffix-{index:00}.wav"))).ToArray();
        if (manifest is not { Version: 1, Complete: true, Provider: "Røst", Style: "Arenaspeaker", SampleRate: 24000 }
            || manifest.Clips is null || manifest.Clips.Length != expected.Length
            || !expected.All(clip => manifest.Clips.Count(candidate => candidate == clip) == 1))
            return Unavailable();
        return new(true, "Røst", "Arenaspeakeren er klar med forudindtalte navneled.");
    }

    public PreparedRouletteSpeech? Prepare(string groupName) => RouletteNameCatalog.GetClipUrls(groupName) is { Length: 2 } clips
        ? new(clips) : null;

    private static RouletteSpeechStatus Unavailable() => new(false, "Røst",
        "De forudindtalte navneled er ikke klar. Du kan bruge browserstemmen.");

    private sealed record SpeechClip(string Id, string Text, string File);
    private sealed record SpeechManifest(int Version, bool Complete, string Provider, string Style, int SampleRate, SpeechClip[]? Clips);
}

public sealed record PreparedRouletteSpeech(string[] ClipUrls);
