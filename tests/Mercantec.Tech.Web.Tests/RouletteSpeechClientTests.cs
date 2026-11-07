using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Learning;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class RouletteSpeechClientTests
{
    [Fact]
    public async Task A_complete_bank_prepares_known_group_names_locally_without_per_spin_requests()
    {
        using var handler = new BankHandler(Manifest());
        using var http = new HttpClient(handler) { BaseAddress = new("https://different-api.test/api/") };
        var speech = new RouletteSpeechClient(http, new PlatformNavigation());

        Assert.True((await speech.Status(CancellationToken.None)).Available);
        var neon = Assert.IsType<PreparedRouletteSpeech>(speech.Prepare("NeonOpfinderne"));
        Assert.Equal(new[] { RouletteNameCatalog.AssetRoot + "/prefix-16.wav", RouletteNameCatalog.AssetRoot + "/suffix-04.wav" }, neon.ClipUrls);
        Assert.NotNull(speech.Prepare("TurboPingvinerne"));
        Assert.NotNull(speech.Prepare("NeonOpfinderne"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://platform.test" + RouletteNameCatalog.ManifestPath + "?v=announcer-v2-1", request.Uri.ToString());
    }

    [Fact]
    public void Unknown_or_custom_names_use_the_browser_instead_of_requesting_new_audio()
    {
        using var handler = new BankHandler(Manifest());
        using var http = new HttpClient(handler) { BaseAddress = new("https://platform.test/api/") };
        var speech = new RouletteSpeechClient(http, new PlatformNavigation());

        Assert.Null(speech.Prepare("Stjernehold abcde"));
        Assert.Null(speech.Prepare("Elevens eget holdnavn"));
        Assert.Null(speech.Prepare("Neonhttps://other.test/voice.wav"));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("external_url")]
    [InlineData("traversal")]
    [InlineData("wrong_word")]
    [InlineData("wrong_version")]
    [InlineData("wrong_style")]
    [InlineData("wrong_rate")]
    public async Task An_incomplete_or_incompatible_manifest_cannot_enable_the_recorded_voice(string defect)
    {
        var manifest = Manifest();
        var clips = manifest["clips"]!.AsArray();
        switch (defect)
        {
            case "incomplete": manifest["complete"] = false; break;
            case "missing": clips.RemoveAt(0); break;
            case "duplicate": clips[1] = clips[0]!.DeepClone(); break;
            case "external_url": clips[0]!["file"] = "https://external.test/clip.wav"; break;
            case "traversal": clips[0]!["file"] = "../../clip.wav"; break;
            case "wrong_word": clips[0]!["text"] = "Forkert ord"; break;
            case "wrong_version": manifest["version"] = 2; break;
            case "wrong_style": manifest["style"] = "Neutral"; break;
            case "wrong_rate": manifest["sampleRate"] = 8000; break;
        }
        using var handler = new BankHandler(manifest);
        using var http = new HttpClient(handler) { BaseAddress = new("https://platform.test/api/") };

        var result = await new RouletteSpeechClient(http, new PlatformNavigation()).Status(CancellationToken.None);

        Assert.False(result.Available);
        Assert.Contains("browserstemmen", result.Message);
        Assert.Equal(HttpMethod.Get, Assert.Single(handler.Requests).Method);
    }

    [Fact]
    public async Task A_missing_bank_keeps_browser_speech_available_without_model_health_checks()
    {
        using var handler = new BankHandler(null);
        using var http = new HttpClient(handler) { BaseAddress = new("https://platform.test/api/") };

        var result = await new RouletteSpeechClient(http, new PlatformNavigation()).Status(CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal(RouletteNameCatalog.ManifestPath, Assert.Single(handler.Requests).Uri.AbsolutePath);
    }

    [Fact]
    public async Task A_host_returning_its_HTML_shell_for_the_manifest_still_leaves_the_browser_voice_usable()
    {
        using var handler = new BankHandler(null, "<!doctype html><html><body>App shell</body></html>");
        using var http = new HttpClient(handler) { BaseAddress = new("https://platform.test/api/") };

        var result = await new RouletteSpeechClient(http, new PlatformNavigation()).Status(CancellationToken.None);

        Assert.False(result.Available);
        Assert.Contains("browserstemmen", result.Message);
    }

    [Fact]
    public async Task The_complete_announcement_bank_is_fetched_from_the_frontend_with_the_runtime_version()
    {
        using var handler = new BankHandler(Manifest(), announcements: AnnouncementManifest());
        using var http = new HttpClient(handler) { BaseAddress = new("https://different-api.test/api/") };

        Assert.True(await new RouletteSpeechClient(http, new PlatformNavigation()).AnnouncementsAvailable(CancellationToken.None));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://platform.test/audio/roulette/cues-v1/manifest.json?v=announcer-v2-1", request.Uri.ToString());
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("external_url")]
    [InlineData("wrong_word")]
    public async Task Invalid_announcement_entries_disable_extra_cues_while_the_word_bank_remains_usable(string defect)
    {
        var announcements = AnnouncementManifest();
        var clips = announcements["clips"]!.AsArray();
        switch (defect)
        {
            case "incomplete": announcements["complete"] = false; break;
            case "missing": clips.RemoveAt(0); break;
            case "duplicate": clips[1] = clips[0]!.DeepClone(); break;
            case "external_url": clips[0]!["file"] = "https://external.test/cue.wav"; break;
            case "wrong_word": clips[0]!["text"] = "Forkert opråb"; break;
        }
        using var handler = new BankHandler(Manifest(), announcements: announcements);
        using var http = new HttpClient(handler) { BaseAddress = new("https://platform.test/api/") };
        var speech = new RouletteSpeechClient(http, new PlatformNavigation());

        Assert.True((await speech.Status(CancellationToken.None)).Available);
        Assert.False(await speech.AnnouncementsAvailable(CancellationToken.None));
        Assert.NotNull(speech.Prepare("NeonOpfinderne"));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task An_unavailable_extra_bank_does_not_disable_the_recorded_group_names(HttpStatusCode cueStatus)
    {
        using var handler = new BankHandler(Manifest(), announcementStatus: cueStatus);
        using var http = new HttpClient(handler) { BaseAddress = new("https://platform.test/api/") };
        var speech = new RouletteSpeechClient(http, new PlatformNavigation());

        Assert.True((await speech.Status(CancellationToken.None)).Available);
        Assert.False(await speech.AnnouncementsAvailable(CancellationToken.None));
        Assert.NotNull(speech.Prepare("NeonOpfinderne"));
        Assert.Equal(2, handler.Requests.Count);
    }

    private static JsonObject Manifest() => JsonSerializer.SerializeToNode(new
    {
        version = 1, complete = true, provider = "Røst", style = "Arenaspeaker", sampleRate = 24000,
        clips = RouletteNameCatalog.Starts.Select((text, index) => new { id = $"prefix-{index:00}", text, file = $"prefix-{index:00}.wav" })
            .Concat(RouletteNameCatalog.Ends.Select((text, index) => new { id = $"suffix-{index:00}", text, file = $"suffix-{index:00}.wav" })).ToArray()
    })!.AsObject();

    private static JsonObject AnnouncementManifest() => JsonSerializer.SerializeToNode(new
    {
        version = 1, complete = true, provider = "Røst", style = "Arenaspeaker", sampleRate = 24000,
        clips = RouletteAnnouncementCatalog.All.Select(cue => new { id = cue.Id, text = cue.Text, file = cue.File }).ToArray()
    })!.AsObject();

    private sealed class BankHandler(JsonObject? manifest, string? rawBody = null, JsonObject? announcements = null,
        HttpStatusCode? announcementStatus = null) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!));
            if (request.RequestUri!.AbsolutePath == RouletteAnnouncementCatalog.ManifestPath)
                return Task.FromResult(announcementStatus is { } status ? new HttpResponseMessage(status)
                    : announcements is null ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new(HttpStatusCode.OK) { Content = JsonContent.Create(announcements) });
            if (rawBody is not null)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(rawBody) });
            return Task.FromResult(manifest is null ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new(HttpStatusCode.OK) { Content = JsonContent.Create(manifest) });
        }
    }

    private sealed class PlatformNavigation : NavigationManager
    {
        public PlatformNavigation() => Initialize("https://platform.test/", "https://platform.test/teacher");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }
}
