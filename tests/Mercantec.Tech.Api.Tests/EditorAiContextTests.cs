using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Ai.EditorRuns;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mercantec.Tech.Api.Tests;

public sealed class EditorAiContextTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid();
    private readonly ProjectionHandler projection = new();
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private ClaimsPrincipal User(bool isTeacher = true) => new(new ClaimsIdentity(isTeacher
        ? [new Claim("sub", teacher.ToString()), new Claim(ClaimTypes.Role, "Underviser")]
        : [new Claim("sub", teacher.ToString())], "test"));
    private EditorAiContextService Service()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Notion:ServiceSecret"] = new string('s', 40), ["Notebook:CollaborationBaseUrl"] = "http://documents" }).Build();
        var client = new NotionAiProjectionClient(new HttpFactory(projection), config, new(config));
        return new(db, new(db, client), NullLogger<EditorAiContextService>.Instance);
    }
    private static JsonElement Document(params string[] text) => JsonSerializer.SerializeToElement(text.Select((t, i) => new
    { id = "block-" + i, type = "paragraph", content = new[] { new { type = "text", text = t } }, children = Array.Empty<object>() }));
    private static NotionPage Page(string title, Guid? collection = null, Guid? hold = null) => new()
    { Id = Guid.NewGuid(), Title = title, CollectionId = collection, HoldId = hold, CreatedBySub = Guid.NewGuid(), YjsState = [1, 2], DraftRevision = 1, UpdatedAtUtc = DateTimeOffset.UtcNow };
    private async Task<Guid> Hold(Guid owner)
    {
        var id = Guid.NewGuid();
        db.Hold.Add(new() { Id = id, Name = "Hold", Forloeb = new() { Id = Guid.NewGuid(), Name = "Forløb", CreatedBySub = owner } });
        await db.SaveChangesAsync(); return id;
    }

    [Fact]
    public async Task Uses_unsaved_snapshot_and_only_authorized_relevant_saved_pages()
    {
        var collection = Guid.NewGuid();
        var active = Page("MQTT", collection); var related = Page("MQTT teori", collection);
        var privatePage = Page("MQTT private elevdata", hold: await Hold(Guid.NewGuid()));
        var unrelated = Page("Uvedkommende madopskrift");
        // A standalone collection FK is optional for this direct fixture.
        active.CollectionId = related.CollectionId = null;
        db.NotionPages.AddRange(active, related, privatePage, unrelated); await db.SaveChangesAsync();
        var result = await Service().BuildAsync(User(), active.Id, new(active.Id, "Forklar MQTT", "edit", "page", Document("Endnu ikke gemt: MQTT QoS")), default);
        Assert.Contains("Endnu ikke gemt", result.ContextText);
        Assert.Contains("MQTT broker fra den valgte kilde", result.ContextText);
        Assert.DoesNotContain("private elevdata", result.ContextText);
        Assert.DoesNotContain("madopskrift", result.ContextText);
        Assert.Equal(1, projection.Calls);
        Assert.Contains(result.Sources, s => s.SourceId == related.Id.ToString());
        Assert.Equal(1, result.Coverage.ActiveBlocks);
    }

    [Fact]
    public async Task Danish_interface_request_and_shorter_followup_use_CSharp_context_without_catalogue_noise()
    {
        var active = Page("Skrivepartner demo"); var theory = Page("C# interfaces");
        theory.YjsState = Encoding.UTF8.GetBytes("text:C# interfaces beskriver en kontrakt. ILogger definerer Log, og ConsoleLogger implementerer ILogger.");
        var arduino = Page("Arduino og sensorer"); var machine = Page("Machine learning med Python"); var core = Page("Core loop og Unity");
        db.NotionPages.AddRange(active, theory, arduino, machine, core); await db.SaveChangesAsync();
        var live = Document("C# interfaces", "Et interface er en kontrakt. public interface ILogger { void Log(string besked); } public class ConsoleLogger : ILogger { public void Log(string besked) { Console.WriteLine(besked); } }");
        var service = Service();
        var first = await service.BuildAsync(User(), active.Id, new(active.Id,
            "Erstat det eksisterende afsnit med en kort, fagligt korrekt forklaring af interfaces til H1-elever. Behold et lille C#-eksempel med ILogger og ConsoleLogger, og ændr ikke resten af siden.",
            "rewrite", "page", live), default);
        Assert.Contains(first.Sources, s => s.SourceId == theory.Id.ToString());
        Assert.DoesNotContain(first.Sources, s => s.Title.Contains("Arduino") || s.Title.Contains("Core loop") || s.Title.Contains("dashboard") || s.Title.Contains("Machine", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(first.Sources, s => s.SourceId.StartsWith("iot/") || s.SourceId.StartsWith("game-design/"));
        var followup = await service.BuildAsync(User(), active.Id, new(active.Id,
            "Gør forklaringen endnu kortere. Behold kun det vigtigste og et lille ILogger/ConsoleLogger-eksempel.", "rewrite", "page", live), default);
        Assert.Contains(followup.Sources, s => s.SourceId == theory.Id.ToString());
        Assert.DoesNotContain(followup.Sources, s => s.Title.Contains("Arduino") || s.Title.Contains("Core loop") || s.Title.Contains("dashboard") || s.Title.Contains("Machine", StringComparison.OrdinalIgnoreCase));
        Assert.True(followup.Sources.Length < 8);
    }

    [Fact]
    public async Task Generic_writing_request_can_return_zero_external_sources()
    {
        var active = Page("Skrivepartner demo"); db.NotionPages.Add(active); await db.SaveChangesAsync();
        var result = await Service().BuildAsync(User(), active.Id, new(active.Id,
            "Gør forklaringen kortere og fagligt korrekt. Brug et lille eksempel til eleverne.", "rewrite", "page", Document("Dette afsnit skal være kortere og lettere at læse.")), default);
        Assert.Empty(result.Sources);
    }

    [Fact]
    public void Domain_terms_keep_language_tokens_and_reject_instruction_only_overlap()
    {
        var words = EditorAiText.Words("Et kort C#-eksempel, F# og C++. Interfaces til H1-elever.");
        Assert.Contains("c#", words); Assert.Contains("f#", words); Assert.Contains("c++", words); Assert.Contains("interface", words);
        Assert.DoesNotContain("eksempel", words); Assert.DoesNotContain("elever", words);
        var relevance = new EditorAiRelevance("Erstat afsnittet med en kort forklaring af interfaces", "Demo", "C# interface ILogger ConsoleLogger", "", "");
        Assert.Equal(0, relevance.Score("Arduino", "Lav et kort eksempel med C++ og funktioner. Forklar input fra en sensor."));
        Assert.Equal(0, relevance.Score("Machine learning", "Brug Python til et fagligt korrekt og lille eksempel."));
        Assert.Equal(0, relevance.Score("Core loop", "Forklar regler og afprøv et kort eksempel med eleverne."));
        Assert.True(relevance.Score("C# interfaces", "Et interface definerer en kontrakt.") > 0);
    }

    [Fact]
    public async Task Excluded_source_is_not_decoded_and_access_is_rechecked_on_resume()
    {
        var hold = await Hold(teacher); var active = Page("MQTT", hold: hold); var related = Page("MQTT", hold: hold);
        db.NotionPages.AddRange(active, related); await db.SaveChangesAsync();
        var service = Service();
        var result = await service.BuildAsync(User(), active.Id, new(active.Id, "MQTT", "edit", "page", Document("MQTT"), ExcludedSourceIds: ["notion-page:" + related.Id]), default);
        Assert.DoesNotContain(result.Sources, s => s.SourceId == related.Id.ToString()); Assert.Equal(0, projection.Calls);
        var source = new EditorAiSource("notion-page:" + related.Id, "notion-page", related.Id.ToString(), related.Title, "/notion/" + related.Id);
        Assert.True(await service.CanReadSourceAsync(User(), source, default));
        await db.Forloeb.ExecuteUpdateAsync(x => x.SetProperty(p => p.CreatedBySub, Guid.NewGuid()));
        Assert.False(await service.CanReadSourceAsync(User(), source, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.BuildAsync(User(), active.Id, new(active.Id, "MQTT", "edit", "page", Document("MQTT")), default));
    }

    [Fact]
    public async Task Student_role_cannot_build_context_even_for_global_theory()
    {
        var active = Page("Teori"); db.NotionPages.Add(active); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service().BuildAsync(User(false), active.Id, new(active.Id, "Skriv", "edit", "page", Document("tekst")), default));
        Assert.Equal(0, projection.Calls);
    }

    [Fact]
    public async Task Indexing_does_not_detach_the_callers_tracked_private_run()
    {
        var active = Page("MQTT"); db.NotionPages.Add(active);
        var run = new EditorAiRun { Id = Guid.NewGuid(), PageId = active.Id, OwnerSub = teacher, ClientRequestId = Guid.NewGuid() };
        db.EditorAiRuns.Add(run); await db.SaveChangesAsync();
        await Service().BuildAsync(User(), active.Id, new(active.Id, "MQTT", "edit", "page", Document("MQTT")), default);
        Assert.NotEqual(EntityState.Detached, db.Entry(run).State);
        run.Status = "ready"; run.Progress = "Kilder er klar";
        await db.SaveChangesAsync();
        await db.Entry(run).ReloadAsync();
        Assert.Equal("ready", run.Status); Assert.Equal("Kilder er klar", run.Progress);
    }

    [Fact]
    public async Task Hold_and_emoji_metadata_are_demand_loaded_and_still_authorized()
    {
        var hold = await Hold(teacher); var active = Page("MQTT", hold: hold); db.NotionPages.Add(active);
        var emoji = new CustomEmoji { Id = Guid.NewGuid(), Name = "mqtt-glad", AltText = "En glad MQTT robot", Key = "emoji/mqtt", ContentType = "image/png" };
        db.CustomEmojis.Add(emoji); await db.SaveChangesAsync();
        var service = Service();
        var ordinary = await service.BuildAsync(User(), active.Id, new(active.Id, "Forklar MQTT", "edit", "page", Document("MQTT")), default);
        Assert.DoesNotContain(ordinary.Sources, s => s.Kind is "emoji" or "hold");
        var requested = await service.BuildAsync(User(), active.Id, new(active.Id, "Brug en MQTT emoji og tilpas til holdet", "edit", "page", Document("MQTT")), default);
        Assert.Contains(requested.Sources, s => s.Kind == "hold" && s.SourceId == hold.ToString());
        Assert.Contains(requested.Sources, s => s.Kind == "emoji" && s.SourceId == emoji.Id.ToString());
        Assert.True(requested.Sources.Length <= 8);
        Assert.True(requested.Coverage.ExternalTokens <= 24000);
        await db.CustomEmojis.Where(e => e.Id == emoji.Id).ExecuteDeleteAsync();
        Assert.False(await service.CanReadSourceAsync(User(), requested.Sources.Single(s => s.Kind == "emoji" && s.SourceId == emoji.Id.ToString()), default));
    }

    [Fact]
    public async Task Requested_character_emoji_uses_canonical_static_assets_and_respects_legacy_collisions()
    {
        var active = Page("SQL"); db.NotionPages.Add(active); await db.SaveChangesAsync();
        var service = Service();
        var result = await service.BuildAsync(User(), active.Id, new(active.Id, "Brug en Raymond emoji der griner", "generate", "page", Document("SQL")), default);
        var emoji = CharacterEmojiCatalog.All.Single(e => e.Name == "raymond_laughing");
        var source = Assert.Single(result.Sources.Where(s => s.SourceId == emoji.Id.ToString()));
        Assert.Equal(emoji.Image, source.Href); Assert.Equal("emoji", source.Kind);
        Assert.True(await service.CanReadSourceAsync(User(), source, default));
        db.CustomEmojis.Add(new() { Id = Guid.NewGuid(), Name = emoji.Name, Key = "legacy/emoji", ContentType = "image/png" });
        await db.SaveChangesAsync();
        Assert.False(await service.CanReadSourceAsync(User(), source, default));
        var collided = await service.BuildAsync(User(), active.Id, new(active.Id, "Brug en Raymond emoji der griner", "generate", "page", Document("SQL")), default);
        Assert.DoesNotContain(collided.Sources, s => s.SourceId == emoji.Id.ToString());
    }

    [Fact]
    public async Task Ready_file_text_is_cached_by_version_and_unsupported_media_has_explicit_status()
    {
        var hold = await Hold(teacher); var active = Page("MQTT", hold: hold); db.NotionPages.Add(active);
        var file = new FileAsset { Id = Guid.NewGuid(), Key = "lesson.pdf", OriginalName = "MQTT.pdf", HoldId = hold, OwnerSub = teacher, SizeBytes = 900, CreatedAtUtc = DateTimeOffset.UtcNow };
        var media = new FileAsset { Id = Guid.NewGuid(), Key = "film", OriginalName = "MQTT.mp4", HoldId = hold, OwnerSub = teacher, SizeBytes = 400 };
        var material = new CourseMaterial { Id = Guid.NewGuid(), HoldId = hold, FileAsset = file, Title = "MQTT PDF", Description = "Brokeren" };
        db.CourseMaterials.AddRange(material, new() { Id = Guid.NewGuid(), HoldId = hold, FileAsset = media, Title = "MQTT film" });
        db.EditorAiFileTexts.Add(new() { FileId = file.Id, File = file, Fingerprint = EditorAiText.FileFingerprint(file), State = "ready", Text = "MQTT PDF tekst fra side 1\fMQTT QoS fra side 2" });
        await db.SaveChangesAsync();
        var result = await Service().BuildAsync(User(), active.Id, new(active.Id, "MQTT QoS", "edit", "page", Document("MQTT")), default);
        Assert.Contains("MQTT PDF tekst fra side 1", result.ContextText); Assert.Contains("MQTT QoS fra side 2", result.ContextText);
        Assert.Contains(result.Sources, s => s.State == "unsupported" && s.Kind == "material");
        Assert.True(result.Coverage.Partial);
        Assert.Equal("ready", (await db.EditorAiFileTexts.SingleAsync(x => x.FileId == file.Id)).State);
    }

    [Fact]
    public void Long_live_document_keeps_more_than_forty_blocks_and_reports_partial_large_blocks()
    {
        var document = Document(Enumerable.Range(0, 55).Select(i => "Lang tekst " + i + new string('x', 6000)).ToArray());
        var result = EditorAiContextService.Snapshot(document, JsonSerializer.SerializeToElement(new { blockIds = new[] { "block-54" } }));
        Assert.True(result.Count > 40); Assert.True(result.Partial); Assert.Contains("block-54", result.Text);
        var oversized = EditorAiContextService.Snapshot(Document(new string('x', 400000)), null);
        Assert.True(oversized.Partial); Assert.Contains("omittedCharacters", oversized.Text); Assert.Contains("do not replace", oversized.Text);
        Assert.Empty(oversized.WritableBlockIds); Assert.Contains("block-54", result.WritableBlockIds);
        Assert.True(result.Tokens <= 96000);
    }

    [Fact]
    public void Page_windows_visit_each_complete_block_once_and_finish_with_cumulative_coverage()
    {
        var document = Document(Enumerable.Range(0, 80).Select(i => "Afsnit " + i + new string('x', 12000)).ToArray());
        string? cursor = null; var visited = new HashSet<string>(); var lastCount = 0; var windows = 0;
        do
        {
            var target = JsonSerializer.SerializeToElement(new { contextCursorBlockId = cursor });
            var snapshot = EditorAiContextService.Snapshot(document, target, pageScope: true);
            Assert.False(snapshot.Blocked); Assert.True(snapshot.Tokens <= 96000);
            Assert.True(snapshot.Count > lastCount); Assert.Equal(visited.Count, snapshot.SectionStart);
            foreach (var id in snapshot.WritableBlockIds) Assert.True(visited.Add(id), "A complete block must not repeat in the next section.");
            lastCount = snapshot.Count; cursor = snapshot.NextSectionBlockId; windows++;
            if (cursor is null) Assert.False(snapshot.Partial);
            else Assert.True(snapshot.Partial);
        } while (cursor is not null && windows < 10);
        Assert.True(windows > 1); Assert.Equal(80, visited.Count); Assert.Equal(80, lastCount); Assert.Null(cursor);
    }

    [Fact]
    public void Page_windows_never_skip_an_oversized_block_or_a_deleted_cursor()
    {
        var document = Document("Første afsnit", new string('x', 400000), "Dette afsnit må ikke springes frem til");
        var first = EditorAiContextService.Snapshot(document, null, pageScope: true);
        Assert.Equal("block-1", first.NextSectionBlockId); Assert.Equal(["block-0"], first.WritableBlockIds);
        var oversized = EditorAiContextService.Snapshot(document, JsonSerializer.SerializeToElement(new { contextCursorBlockId = first.NextSectionBlockId }), pageScope: true);
        Assert.True(oversized.Blocked); Assert.Null(oversized.NextSectionBlockId); Assert.Empty(oversized.WritableBlockIds);
        Assert.Equal(1, oversized.Count); Assert.DoesNotContain("Dette afsnit må ikke springes", oversized.Text);
        var changed = Assert.Throws<ArgumentException>(() => EditorAiContextService.Snapshot(document,
            JsonSerializer.SerializeToElement(new { contextCursorBlockId = "deleted" }), pageScope: true));
        Assert.Equal("context_cursor_changed", changed.Message);
    }

    [Fact]
    public void Selection_slice_is_counted_in_the_active_budget_and_large_targets_fail_explicitly()
    {
        var selection = JsonSerializer.SerializeToElement(new { blockIds = new[] { "block-0" }, selection = new { slice = new string('x', 120000) } });
        var result = EditorAiContextService.Snapshot(Document(new string('y', 180000)), selection);
        Assert.True(result.Tokens <= 96000);
        var tooLarge = JsonSerializer.SerializeToElement(new { selection = new { slice = new string('x', 300000) } });
        Assert.Equal("target_context_too_large_split_selection", Assert.Throws<ArgumentException>(() =>
            EditorAiContextService.Snapshot(Document("små blokke"), tooLarge)).Message);
    }

    [Fact]
    public void Chunking_keeps_long_code_and_pdf_page_locations_without_silent_tail_loss()
    {
        var text = string.Join('\n', Enumerable.Range(0, 2000).Select(i => $"var item{i} = {i};"));
        var chunks = EditorAiText.Chunks(text + "\fSIDSTE PDF-SIDE").ToArray();
        Assert.Contains(chunks, x => x.Text.Contains("item1999")); Assert.Equal("Side 2", chunks.Last().Location);
        foreach (var line in text.Split('\n')) Assert.Contains(chunks, c => c.Text.Contains(line));
    }

    [Fact]
    public async Task Extraction_limits_fail_explicitly_and_converter_does_not_try_ocr_or_video()
    {
        await using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', EditorAiText.MaxExtractedChars + 1)));
        await Assert.ThrowsAsync<InvalidDataException>(() => EditorAiFileTextWorker.ReadTextAsync(bytes, default));
        Assert.True(PreviewConverter.CanProcess("input.pdf", true)); Assert.True(PreviewConverter.CanProcess("input.docx", true));
        Assert.False(PreviewConverter.CanProcess("input.pdf", false)); Assert.False(PreviewConverter.CanProcess("input.mp4", true));
        Assert.False(PreviewConverter.CanProcess("scan.png", true));
    }

    [Fact]
    public async Task Storage_worker_persists_complete_code_and_marks_empty_pdf_text_as_unread()
    {
        var spool = Path.Combine(Path.GetTempPath(), "ai-text-test-" + Guid.NewGuid().ToString("N"));
        var code = string.Join('\n', Enumerable.Range(0, 600).Select(i => $"Console.WriteLine({i});"));
        var storage = new TextStorage(new Dictionary<string, byte[]> { ["code"] = Encoding.UTF8.GetBytes(code), ["pdf"] = [37, 80, 68, 70] });
        var codeFile = new FileAsset { Id = Guid.NewGuid(), Key = "code", OriginalName = "sample.cs", OwnerSub = teacher, SizeBytes = code.Length };
        var pdfFile = new FileAsset { Id = Guid.NewGuid(), Key = "pdf", OriginalName = "scanned.pdf", OwnerSub = teacher, SizeBytes = 4 };
        db.FileAssets.AddRange(codeFile, pdfFile);
        db.EditorAiFileTexts.Add(new() { FileId = codeFile.Id, File = codeFile, Fingerprint = EditorAiText.FileFingerprint(codeFile), State = "pending", UpdatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PreviewWorker:SpoolPath"] = spool }).Build();
        using var services = new ServiceCollection().AddDbContext<AppDbContext>(o => o.UseSqlite(connection))
            .AddSingleton<IObjectStorage>(storage).BuildServiceProvider();
        var worker = new EditorAiFileTextWorker(services.GetRequiredService<IServiceScopeFactory>(), config, NullLogger<EditorAiFileTextWorker>.Instance);
        Assert.True(await worker.ProcessNextAsync(default));
        var ready = await db.EditorAiFileTexts.AsNoTracking().SingleAsync(x => x.FileId == codeFile.Id);
        Assert.Equal("ready", ready.State); Assert.Equal(code, ready.Text);
        Assert.False(await worker.ProcessNextAsync(default));
        db.EditorAiFileTexts.Add(new() { FileId = pdfFile.Id, File = pdfFile, Fingerprint = EditorAiText.FileFingerprint(pdfFile), State = "pending", UpdatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var convert = Task.Run(async () =>
        {
            string? job;
            do
            {
                job = Directory.Exists(spool) ? Directory.EnumerateDirectories(spool).FirstOrDefault(d => File.Exists(Path.Combine(d, "ready"))) : null;
                if (job is null) await Task.Delay(20, deadline.Token);
            } while (job is null);
            Assert.True(File.Exists(Path.Combine(job, "extract-text")));
            Assert.True(File.Exists(Path.Combine(job, "input.pdf")));
            await File.WriteAllTextAsync(Path.Combine(job, "text.txt"), "\f\n", deadline.Token);
            await File.WriteAllTextAsync(Path.Combine(job, "done"), "", deadline.Token);
        }, deadline.Token);
        try
        {
            Assert.True(await worker.ProcessNextAsync(deadline.Token)); await convert;
            var unsupported = await db.EditorAiFileTexts.AsNoTracking().SingleAsync(x => x.FileId == pdfFile.Id);
            Assert.Equal("unsupported", unsupported.State); Assert.Contains("OCR", unsupported.Error);
            Assert.Empty(Directory.EnumerateDirectories(spool));
        }
        finally { if (Directory.Exists(spool) && !Directory.EnumerateFileSystemEntries(spool).Any()) Directory.Delete(spool); }
    }

    private sealed class HttpFactory(ProjectionHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, false); }
    private sealed class ProjectionHandler : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Assert.Equal("/internal/notion/project", request.RequestUri!.AbsolutePath);
            Assert.True(request.Headers.Contains("X-Notion-Service-Key"));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var state = Encoding.UTF8.GetString(Convert.FromBase64String(body.RootElement.GetProperty("stateBase64").GetString()!));
            var text = state.StartsWith("text:") ? state[5..] : "MQTT broker fra den valgte kilde";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(
                new { plainText = text, blocks = new[] { new { id = "mq", type = "paragraph", text, heading = "MQTT" } } }), Encoding.UTF8, "application/json") };
        }
    }
    private sealed class TextStorage(Dictionary<string, byte[]> data) : IObjectStorage
    {
        public bool IsConfigured => true;
        public string Bucket => "test";
        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<ObjectGetResult?>(
            data.TryGetValue(key, out var bytes) ? new(new MemoryStream(bytes), "application/octet-stream", bytes.Length) : null);
        public Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<Uri?>(null);
    }
}
