using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Updates;
using Mercantec.Tech.Api.Updates.Automation;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class PlatformUpdatesAutomationTests
{
    private const string Commit = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Previous = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Revision = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public async Task MissingOrBadServiceKeyCannotReadStateAnalyzeOrPublish()
    {
        using var fixture = new Fixture();
        foreach (var supplied in new string?[] { null, "", "incorrect-key" })
        {
            var controller = fixture.Controller(supplied);
            Assert.IsType<UnauthorizedResult>(controller.State());
            Assert.IsType<UnauthorizedResult>(await controller.Analyze(AnalysisRequest(), default));
            Assert.IsType<UnauthorizedResult>(await controller.Publish(Publication(), default));
        }
        File.Delete(fixture.KeyFile);
        Assert.IsType<UnauthorizedResult>(fixture.Controller(Fixture.Secret).State());
        Assert.Equal(0, fixture.Handler.Calls);
    }

    [Fact]
    public void PublicDeploymentExposesOnlyRevisionAndReadinessWithoutAuthentication()
    {
        using var fixture = new Fixture();
        var controller = fixture.Controller(null);
        var response = Assert.IsType<OkObjectResult>(controller.Deployment());
        var json = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal(Revision, json.GetProperty("revision").GetString());
        Assert.False(json.GetProperty("automationReady").GetBoolean());
        Assert.Equal(4, json.EnumerateObject().Count());
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task MissingOrDifferentDeployedRevisionPreventsPublishing()
    {
        using var fixture = new Fixture();
        fixture.Deployment.Revision = null;
        Assert.Equal(503, Assert.IsType<ObjectResult>(await fixture.Controller().Publish(Publication(), default)).StatusCode);
        fixture.Deployment.Revision = new string('d', 64);
        Assert.IsType<ConflictObjectResult>(await fixture.Controller().Publish(Publication(), default));
        Assert.Empty(fixture.Store.Entries);
    }

    [Fact]
    public void APreviousBaselineCannotReportANewDeploymentAsReady()
    {
        using var fixture = new Fixture();
        fixture.Store.RecordState(new PlatformUpdatesAutomationState(Commit, Revision,
            DateTimeOffset.UtcNow, "ready", true, Commit, Revision, 1, 1));
        var ready = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(fixture.Controller(null).Deployment()).Value);
        Assert.True(ready.GetProperty("automationReady").GetBoolean());
        fixture.Deployment.Revision = new string('d', 64);
        var pending = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(fixture.Controller(null).Deployment()).Value);
        Assert.False(pending.GetProperty("automationReady").GetBoolean());
    }

    [Fact]
    public void BaselineRequiresARealCaptureAndPersistsAcrossStoreInstances()
    {
        using var fixture = new Fixture();
        var invalid = ReadyState() with { CaptureSuccessCount = 0 };
        Assert.Equal(400, Assert.IsType<ObjectResult>(fixture.Controller().State(invalid)).StatusCode);
        Assert.IsType<OkObjectResult>(fixture.Controller().State(ReadyState()));
        var restarted = fixture.NewStore();
        Assert.True(restarted.ReadState().BaselineRecorded);
        Assert.Equal(Commit, restarted.ReadState().LastProcessedCommit);
        Assert.Equal(2, restarted.ReadState().CaptureSuccessCount);
        var stale = ReadyState() with { CapturedAtUtc = ReadyState().CapturedAtUtc!.Value.AddMinutes(-1) };
        Assert.Throws<PlatformUpdatesAutomationException>(() => restarted.RecordState(stale));
        Assert.True(JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(fixture.Controller(null).Deployment()).Value)
            .GetProperty("automationReady").GetBoolean());
    }

    [Fact]
    public async Task PublicationIsAtomicDurableAndIdempotentAfterRestart()
    {
        using var fixture = new Fixture();
        var publication = Publication();
        var first = await fixture.Store.PublishAsync(publication, default);
        Assert.False(first.AlreadyPublished);
        var restarted = fixture.NewStore();
        var repeated = await restarted.PublishAsync(publication, default);
        Assert.True(repeated.AlreadyPublished);
        var entry = Assert.Single(restarted.Entries);
        Assert.Equal(new DateOnly(2026, 11, 10), entry.Date); // 23:30 UTC is the next Danish day.
        Assert.Equal(new[] { "Kasper" }, entry.Authors);
        var image = Assert.IsType<UpdateImage>(entry.After);
        var stored = Assert.IsType<PlatformUpdatesStoredImage>(restarted.OpenImage(image.Id));
        using (stored.Content)
        {
            Assert.Equal("image/png", stored.ContentType);
            Assert.Equal(Png(), ReadAll(stored.Content));
        }
        Assert.Single(Directory.GetDirectories(Path.Combine(fixture.Directory, "data", "releases")));
        var changed = publication with { Entries = [publication.Entries[0] with { Description = "Et andet indhold." }] };
        var error = await Assert.ThrowsAsync<PlatformUpdatesAutomationException>(() => restarted.PublishAsync(changed, default));
        Assert.Equal(409, error.Status);
    }

    [Fact]
    public async Task AllCommitsAreRetainedWhileTheNewestEntryWinsByStableId()
    {
        using var fixture = new Fixture();
        await fixture.Store.PublishAsync(Publication(), default);
        var later = Publication() with { Commit = new string('e', 40), CapturedAtUtc = Publication().CapturedAtUtc.AddDays(1),
            Entries = [Publication().Entries[0] with { Description = "Nyeste forbedring.", Authors = ["Mathias"] }] };
        await fixture.Store.PublishAsync(later, default);
        Assert.Equal("Nyeste forbedring.", Assert.Single(fixture.NewStore().Entries).Description);
        Assert.Equal(new[] { "Kasper", "Mathias" }, Assert.Single(fixture.NewStore().Entries).Authors);
        Assert.Equal(2, Directory.GetDirectories(Path.Combine(fixture.Directory, "data", "releases")).Length);
    }

    [Theory]
    [InlineData("../appsettings")]
    [InlineData("../../secrets")]
    [InlineData("/etc/passwd")]
    [InlineData("auto-../../service-key")]
    public async Task PathsCannotBecomeEntryIdsOrReadPrivateFiles(string id)
    {
        using var fixture = new Fixture();
        var request = Publication() with { Entries = [Publication().Entries[0] with { Id = id }] };
        Assert.Equal(400, (await Assert.ThrowsAsync<PlatformUpdatesAutomationException>(() => fixture.Store.PublishAsync(request, default))).Status);
        Assert.Null(fixture.Store.OpenImage(id));
        Assert.Empty(fixture.Store.Entries);
    }

    [Fact]
    public async Task NonPngOversizedDimensionsAndBeforeWithoutAfterAreRejected()
    {
        using var fixture = new Fixture();
        var entry = Publication().Entries[0];
        var nonPng = entry with { After = new("Test", Convert.ToBase64String(Encoding.UTF8.GetBytes("not a PNG"))) };
        var large = Png();
        BinaryPrimitives.WriteUInt32BigEndian(large.AsSpan(16, 4), 100_000);
        var badDimensions = entry with { After = new("Test", Convert.ToBase64String(large)) };
        var beforeOnly = entry with { Before = entry.After, After = null };
        foreach (var invalid in new[] { nonPng, badDimensions, beforeOnly })
            await Assert.ThrowsAsync<PlatformUpdatesAutomationException>(() => fixture.Store.PublishAsync(Publication() with { Entries = [invalid] }, default));
        Assert.Empty(fixture.Store.Entries);
    }

    [Fact]
    public async Task TextOnlyEntriesAreSupportedAndAnExistingManualFeatureCanBeUpdated()
    {
        using var fixture = new Fixture();
        var manual = new PlatformUpdatesFeed(NullLogger<PlatformUpdatesFeed>.Instance).Entries
            .Single(entry => entry.Id == "nyheder-med-udgivelser");
        var entry = new PlatformUpdatesPublishEntry(manual.Id, "Omskrevet titel", "Omskrevet beskrivelse", ["Kasper"]);
        await fixture.Store.PublishAsync(Publication() with { Entries = [entry] }, default);
        var stored = Assert.Single(fixture.Store.Entries);
        Assert.Equal("Omskrevet titel", stored.Title);
        Assert.Equal("Omskrevet beskrivelse", stored.Description);
        Assert.Null(stored.After);
        Assert.Null(stored.Before);
        Assert.Contains("Kasper", stored.Authors);
    }

    [Fact]
    public async Task OnlyManualNotesChangedInThisReleaseKeepTheirExactAuthoredCopy()
    {
        using var fixture = new Fixture();
        var manual = new PlatformUpdatesFeed(NullLogger<PlatformUpdatesFeed>.Instance).Entries
            .Single(entry => entry.Id == "nyheder-med-udgivelser");
        fixture.Handler.Id = manual.Id;
        fixture.Handler.Title = "Et nyt forslag";
        fixture.Handler.Description = "En ny beskrivelse af en senere forbedring.";
        var request = AnalysisRequest() with { ChangedFiles = ["api/Updates/Notes/nyheder-med-udgivelser.json"] };
        var changed = Assert.Single((await fixture.Analyzer().AnalyzeAsync(request, default)).Entries);
        Assert.Equal(manual.Title, changed.Title);
        Assert.Equal(manual.Description, changed.Description);
        var later = Assert.Single((await fixture.Analyzer().AnalyzeAsync(AnalysisRequest(), default)).Entries);
        Assert.Equal("Et nyt forslag", later.Title);
        Assert.Equal("En ny beskrivelse af en senere forbedring.", later.Description);
    }

    [Fact]
    public async Task AnalysisUsesStructuredResponsesAndItsPersistentCacheAvoidsRepeatCalls()
    {
        using var fixture = new Fixture();
        await fixture.Store.PublishAsync(Publication(), default);
        var first = await fixture.Analyzer().AnalyzeAsync(AnalysisRequest(), default);
        var restarted = fixture.Analyzer(fixture.NewStore());
        var repeated = await restarted.AnalyzeAsync(AnalysisRequest(), default);
        Assert.Equal(first.Entries, repeated.Entries);
        Assert.Equal(1, fixture.Handler.Calls);
        using var body = JsonDocument.Parse(fixture.Handler.LastBody!);
        Assert.False(body.RootElement.GetProperty("store").GetBoolean());
        Assert.Equal("existing-test-model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("none", body.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
        var format = body.RootElement.GetProperty("text").GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        Assert.Contains("DATA", body.RootElement.GetProperty("input")[0].GetProperty("content").GetString());
        using var context = JsonDocument.Parse(body.RootElement.GetProperty("input")[1].GetProperty("content").GetString()!);
        Assert.Contains(context.RootElement.GetProperty("existingEntries").EnumerateArray(),
            entry => entry.GetProperty("id").GetString() == "notesbog-soegning");
    }

    [Fact]
    public async Task UnknownScenariosAndUnconfiguredAiNeverProduceFakePosts()
    {
        using var fixture = new Fixture();
        fixture.Handler.ScenarioId = "not-a-provided-scenario";
        var failure = await Assert.ThrowsAsync<PlatformUpdatesAutomationException>(() => fixture.Analyzer().AnalyzeAsync(AnalysisRequest(), default));
        Assert.Equal(502, failure.Status);
        Assert.Empty(fixture.Store.Entries);
        var unconfigured = fixture.Analyzer(configured: false);
        Assert.Equal(503, (await Assert.ThrowsAsync<PlatformUpdatesAutomationException>(() => unconfigured.AnalyzeAsync(AnalysisRequest(), default))).Status);
        Assert.Equal(1, fixture.Handler.Calls);
    }

    private static PlatformUpdatesAnalyzeRequest AnalysisRequest() => new(Previous, Commit,
        "diff --git a/web/Pages/Test.razor b/web/Pages/Test.razor\n+<button>Find noter</button>",
        ["web/Pages/Test.razor"], [new("notesbog", "Notesbogens søgning")], ["Kasper"]);
    private static PlatformUpdatesAutomationState ReadyState() => new(Commit, Revision,
        new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero), "ready", true, Commit, Revision, 2, 3);
    private static PlatformUpdatesPublishRequest Publication() => new(Previous, Commit, Revision,
        new DateTimeOffset(2026, 11, 9, 23, 30, 0, TimeSpan.Zero),
        [new("notesbog-soegning", "Find noter hurtigere", "Søg efter titler og tekst i dine noter.", ["Kasper"], new("Notesbogens søgning med testindhold.", Convert.ToBase64String(Png())))]);
    private static byte[] Png()
    {
        using var stream = typeof(PlatformUpdatesCatalog).Assembly.GetManifestResourceStream("standard-emojis/1f389.png")!;
        return ReadAll(stream);
    }
    private static byte[] ReadAll(Stream stream) { using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray(); }

    private sealed class RevisionStub : IDeploymentRevision { public string? Revision { get; set; } = PlatformUpdatesAutomationTests.Revision; public string? Read() => Revision; }
    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class ResponseHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastBody { get; private set; }
        public string ScenarioId { get; set; } = "notesbog";
        public string Id { get; set; } = "notesbog-soegning";
        public string Title { get; set; } = "Find noter hurtigere";
        public string Description { get; set; } = "Søg i dine noter.";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            LastBody = await request.Content!.ReadAsStringAsync(ct);
            var answer = JsonSerializer.Serialize(new { entries = new[] { new { id = Id, title = Title, description = Description, scenarioId = ScenarioId } } });
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = answer } } } } }) };
        }
    }
    private sealed class Fixture : IDisposable
    {
        public const string Secret = "test-only-updates-service-key-with-48-characters";
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "lup-updates-tests-" + Guid.NewGuid().ToString("N"));
        public string KeyFile => Path.Combine(Directory, "service-key");
        public RevisionStub Deployment { get; } = new();
        public ResponseHandler Handler { get; } = new();
        private readonly HttpClient _client;
        private readonly IOptions<PlatformUpdatesAutomationOptions> _options;
        public PlatformUpdatesAutomationStore Store { get; }
        public Fixture()
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(KeyFile, Secret);
            _options = Options.Create(new PlatformUpdatesAutomationOptions { StoragePath = Path.Combine(Directory, "data"), ServiceKeyFile = KeyFile });
            Store = NewStore();
            _client = new HttpClient(Handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        }
        public PlatformUpdatesAutomationStore NewStore() => new(_options, NullLogger<PlatformUpdatesAutomationStore>.Instance);
        public PlatformUpdatesAutomationAnalyzer Analyzer(PlatformUpdatesAutomationStore? store = null, bool configured = true) => new(
            new ClientFactory(_client), Options.Create(new OpenAiOptions { ApiKey = configured ? "unit-test-credential" : "", Model = "existing-test-model" }),
            store ?? Store, new PlatformUpdatesFeed(NullLogger<PlatformUpdatesFeed>.Instance));
        public PlatformUpdatesAutomationController Controller(string? supplied = Secret)
        {
            var controller = new PlatformUpdatesAutomationController(new(_options), Store, Analyzer(), Deployment)
                { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
            if (supplied is not null) controller.Request.Headers["X-LUP-Updates-Key"] = supplied;
            return controller;
        }
        public void Dispose()
        {
            _client.Dispose();
            var target = Path.GetFullPath(Directory);
            var allowed = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (target.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(target).StartsWith("lup-updates-tests-", StringComparison.Ordinal))
                System.IO.Directory.Delete(target, true);
        }
    }
}
