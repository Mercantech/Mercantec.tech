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
        Assert.Equal("https://platform.test" + RouletteNameCatalog.ManifestPath + "?v=announcer-v1-2", request.Uri.ToString());
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

    private static JsonObject Manifest() => JsonSerializer.SerializeToNode(new
    {
        version = 1, complete = true, provider = "Røst", style = "Arenaspeaker", sampleRate = 24000,
        clips = RouletteNameCatalog.Starts.Select((text, index) => new { id = $"prefix-{index:00}", text, file = $"prefix-{index:00}.wav" })
            .Concat(RouletteNameCatalog.Ends.Select((text, index) => new { id = $"suffix-{index:00}", text, file = $"suffix-{index:00}.wav" })).ToArray()
    })!.AsObject();

    private sealed class BankHandler(JsonObject? manifest, string? rawBody = null) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!));
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
