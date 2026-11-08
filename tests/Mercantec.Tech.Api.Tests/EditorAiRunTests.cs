using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Ai.EditorRuns;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Mercantec.Tech.Api.Notebooks;

namespace Mercantec.Tech.Api.Tests;

public sealed class EditorAiRunTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly Guid owner = Guid.NewGuid(), page = Guid.NewGuid();
    private readonly FakeContext context = new();
    private readonly ProviderHandler handler = new();
    private ServiceProvider services = null!;
    private readonly OpenAiOptions settings = new() { ApiKey = "unit-test-not-a-secret", EditorRunTokens = 65536, EditorWorkerConcurrency = 1 };
    public async Task InitializeAsync()
    {
        SQLitePCL.Batteries_V2.Init(); await connection.OpenAsync();
        var collection = new ServiceCollection();
        collection.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        collection.AddScoped<CourseAccess>(); collection.AddScoped<EditorAiRunStore>();
        collection.AddSingleton<IEditorAiContextService>(context);
        collection.AddSingleton<IOptions<OpenAiOptions>>(Options.Create(settings));
        collection.AddSingleton<IHttpClientFactory>(new Factory(handler)); collection.AddSingleton<EditorAiResponseStream>();
        services = collection.BuildServiceProvider();
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        db.NotionPages.Add(new() { Id = page, Title = "Teori uden hold", CreatedBySub = owner, YjsState = [1, 2, 3] });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await services.DisposeAsync(); await connection.DisposeAsync(); }
    private ClaimsPrincipal User(Guid? sub = null, bool teacher = true) => new(new ClaimsIdentity(teacher
        ? [new Claim("sub", (sub ?? owner).ToString()), new Claim("role", "Teacher")]
        : [new Claim("sub", (sub ?? owner).ToString())], "test"));
    private static JsonElement Snapshot() => EditorAiRunStore.Parse("""[{"id":"b1","type":"paragraph","content":[{"type":"text","text":"Original","styles":{"bold":true}}],"children":[]},{"id":"b2","type":"paragraph","content":"Anden blok","children":[]}]""");
    private EditorAiRunRequest Request(Guid? key = null) => new(page, "Skriv en grundig dansk forklaring", "generate", "page", Snapshot(), ClientRequestId: key);
    private EditorAiRunWorker Worker() => new(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(settings), NullLogger<EditorAiRunWorker>.Instance);
    private EditorAiRunsController Controller(IServiceProvider sp, ClaimsPrincipal? user = null) => new(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<CourseAccess>(), sp.GetRequiredService<EditorAiRunStore>(), sp.GetRequiredService<EditorAiResponseStream>(), context)
    { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = user ?? User() } } };
    private static EditorAiModelChange Change(string anchor = "b1", string operation = "insert_after", int count = 45) => new(operation, [anchor],
        EditorAiRunStore.Serialize(Enumerable.Range(0, count).Select(i => new { type = "paragraph", content = $"Langt afsnit {i}: " + new string('æ', 300) })), "Udbygger forklaringen", []);

    [Fact]
    public void Incremental_changes_handle_every_character_boundary_and_escaped_json()
    {
        var expected = new[] { Change(count: 1), Change("b2", count: 1) };
        var json = EditorAiRunStore.Serialize(new { changes = expected, hasMore = false, assistantNote = "Klar \"nu\" } [" });
        var reader = new CompletedChangeReader(); var actual = new List<EditorAiModelChange>();
        for (var i = 1; i <= json.Length; i++) actual.AddRange(reader.Read(json[..i]));
        Assert.Equal(EditorAiRunStore.Serialize(expected), EditorAiRunStore.Serialize(actual)); Assert.Empty(reader.Read(json));
    }

    [Fact]
    public async Task Durable_generation_preserves_document_and_emits_large_valid_hunk_with_private_cursor()
    {
        handler.Responses.Enqueue(Response([Change()], false));
        Guid id;
        using (var scope = services.CreateScope()) id = (await scope.ServiceProvider.GetRequiredService<EditorAiRunStore>().CreateAsync(Request(), User(), default)).Id;
        Assert.True(await Worker().ProcessNextAsync(default));
        using var read = services.CreateScope(); var db = read.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await db.EditorAiRuns.Include(r => r.Hunks).SingleAsync();
        Assert.Equal("ready", run.Status); Assert.Null(run.LeaseId);
        var hunk = Assert.Single(run.Hunks); Assert.Equal(45, EditorAiRunStore.Parse(hunk.AfterJson).GetArrayLength());
        Assert.Equal("ready", hunk.Status); Assert.Equal(new byte[] { 1, 2, 3 }, (await db.NotionPages.SingleAsync()).YjsState);
        var events = await db.EditorAiRunEvents.OrderBy(e => e.Id).ToListAsync();
        Assert.All(events, e => Assert.Equal(e.Id, EditorAiRunStore.Deserialize<EditorAiRunView?>(e.DataJson, null)!.EventCursor));
        Assert.Contains(events, e => EditorAiRunStore.Deserialize<EditorAiRunView?>(e.DataJson, null)!.Hunks.Length == 1);
        using var payload = JsonDocument.Parse(Assert.Single(handler.Payloads));
        Assert.Equal("gpt-6.1-sol", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal("high", payload.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.False(payload.RootElement.GetProperty("store").GetBoolean());
        Assert.Equal(32768, payload.RootElement.GetProperty("max_output_tokens").GetInt32());
        Assert.Contains("\"mode\":\"generate\"", payload.RootElement.GetProperty("input")[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Real_context_indexing_keeps_worker_entities_tracked_and_finishes_generation()
    {
        handler.Responses.Enqueue(Response([Change(count: 1)], false));
        using (var create = services.CreateScope()) await create.ServiceProvider.GetRequiredService<EditorAiRunStore>().CreateAsync(Request(), User(), default);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var collection = new ServiceCollection();
        collection.AddDbContext<AppDbContext>(o => o.UseSqlite(connection)); collection.AddScoped<CourseAccess>();
        collection.AddSingleton<IOptions<OpenAiOptions>>(Options.Create(settings));
        collection.AddSingleton<IHttpClientFactory>(new Factory(handler));
        collection.AddSingleton<IConfiguration>(configuration); collection.AddSingleton(new NotebookServiceKey(configuration));
        collection.AddLogging(); collection.AddScoped<NotionAiProjectionClient>(); collection.AddScoped<EditorAiContextIndexer>();
        collection.AddScoped<IEditorAiContextService, EditorAiContextService>(); collection.AddScoped<EditorAiRunStore>(); collection.AddSingleton<EditorAiResponseStream>();
        using var real = collection.BuildServiceProvider();
        var worker = new EditorAiRunWorker(real.GetRequiredService<IServiceScopeFactory>(), Options.Create(settings), NullLogger<EditorAiRunWorker>.Instance);
        Assert.True(await worker.ProcessNextAsync(default));
        using var read = real.CreateScope();
        var run = await read.ServiceProvider.GetRequiredService<AppDbContext>().EditorAiRuns.Include(r => r.Hunks).SingleAsync();
        Assert.Equal("ready", run.Status); Assert.Single(run.Hunks);
        Assert.Contains("ActiveBlocks", run.CoverageJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stale_worker_cannot_resurrect_cancelled_run_or_overwrite_accepted_review()
    {
        Guid id;
        using (var create = services.CreateScope()) id = (await create.ServiceProvider.GetRequiredService<EditorAiRunStore>().CreateAsync(Request(), User(), default)).Id;
        using var staleScope = services.CreateScope(); var stale = staleScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staleRun = await stale.EditorAiRuns.SingleAsync();
        using (var cancel = services.CreateScope()) Assert.IsType<OkObjectResult>(await Controller(cancel.ServiceProvider).Cancel(id, default));
        staleRun.Status = "ready";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }

    [Fact]
    public async Task Automatic_continuation_allows_sibling_insertions_and_pauses_at_task_budget()
    {
        settings.EditorMaxBatches = 2;
        handler.Responses.Enqueue(Response([Change(count: 1)], true));
        handler.Responses.Enqueue(Response([Change(count: 2)], true));
        using (var scope = services.CreateScope()) await scope.ServiceProvider.GetRequiredService<EditorAiRunStore>().CreateAsync(Request(), User(), default);
        await Worker().ProcessNextAsync(default);
        using var read = services.CreateScope(); var db = read.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await db.EditorAiRuns.Include(r => r.Hunks).SingleAsync();
        Assert.Equal("needs_continuation", run.Status); Assert.Equal(2, run.Hunks.Count); Assert.Equal(2, run.AttemptBatchCount);
        Assert.Equal(new[] { 0, 1 }, run.Hunks.OrderBy(h => h.Order).Select(h => h.Order));
        Assert.Contains("Fortsæt", run.Progress);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Large_pages_advance_durable_server_context_windows_and_resume_after_budget(bool pause)
    {
        settings.EditorMaxBatches = pause ? 1 : 128;
        context.BuildOverride = request =>
        {
            var last = request.Target is { } target && target.TryGetProperty("contextCursorBlockId", out var cursor) && cursor.GetString() == "b2";
            return new("Window " + (last ? "b2" : "b1"), [], new(last ? 2 : 1, 2, 100, 0, !last), [last ? "b2" : "b1"], last ? null : "b2");
        };
        handler.Responses.Enqueue(Response([Change(count: 1)], false));
        handler.Responses.Enqueue(Response([Change("b2", count: 1)], false));
        Guid id;
        using (var scope = services.CreateScope())
            id = (await scope.ServiceProvider.GetRequiredService<EditorAiRunStore>().CreateAsync(Request() with
                { Target = EditorAiRunStore.Parse("""{"contextCursorBlockId":"b2"}""") }, User(), default)).Id;
        await Worker().ProcessNextAsync(default);
        if (pause)
        {
            using var scope = services.CreateScope(); var sp = scope.ServiceProvider;
            var paused = await sp.GetRequiredService<AppDbContext>().EditorAiRuns.SingleAsync();
            Assert.Equal("needs_continuation", paused.Status); Assert.Equal("b2", paused.ContextCursorBlockId);
            Assert.IsType<OkObjectResult>(await Controller(sp).Continue(id, new(), default));
            await Worker().ProcessNextAsync(default);
        }
        using var read = services.CreateScope(); var run = await read.ServiceProvider.GetRequiredService<AppDbContext>().EditorAiRuns.Include(r => r.Hunks).SingleAsync();
        Assert.Equal("ready", run.Status); Assert.Equal("b2", run.ContextCursorBlockId); Assert.Equal(2, run.Hunks.Count);
        Assert.False(context.Requests[0].Target!.Value.TryGetProperty("contextCursorBlockId", out _));
        Assert.Contains("Window b2", handler.Payloads.Last());
    }

    [Fact]
    public async Task Oversized_single_block_pauses_before_spending_model_tokens()
    {
        context.BuildOverride = _ => new("Only an excerpt was read", [], new(0, 1, 90000, 0, true), [], ActiveSectionBlocked: true);
        using (var create = services.CreateScope()) await create.ServiceProvider.GetRequiredService<EditorAiRunStore>().CreateAsync(Request(), User(), default);
        await Worker().ProcessNextAsync(default);
        using var read = services.CreateScope(); var run = await read.ServiceProvider.GetRequiredService<AppDbContext>().EditorAiRuns.SingleAsync();
        Assert.Equal("needs_continuation", run.Status); Assert.Equal("active_section_too_large", run.Error);
        Assert.Equal(0, run.OutputTokens); Assert.Empty(handler.Payloads); Assert.Contains("opdel blokken", run.Progress);
    }

    [Fact]
    public async Task Interrupted_stream_preserves_completed_hunk_and_continue_does_not_duplicate_it()
    {
        var change = Change(count: 1);
        handler.Responses.Enqueue(Response([change], false, omitTerminal: true));
        Guid id;
        using (var scope = services.CreateScope()) id = (await scope.ServiceProvider.GetRequiredService<EditorAiRunStore>().CreateAsync(Request(), User(), default)).Id;
        await Worker().ProcessNextAsync(default);
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var paused = await db.EditorAiRuns.Include(r => r.Hunks).SingleAsync();
            Assert.Equal("needs_continuation", paused.Status); Assert.Single(paused.Hunks);
            Assert.IsType<OkObjectResult>(await Controller(scope.ServiceProvider).Continue(id, new(), default));
        }
        handler.Responses.Enqueue(Response([change], false));
        await Worker().ProcessNextAsync(default);
        using var read = services.CreateScope();
        var run = await read.ServiceProvider.GetRequiredService<AppDbContext>().EditorAiRuns.Include(r => r.Hunks).SingleAsync();
        Assert.Equal("ready", run.Status); Assert.Single(run.Hunks);
    }

    [Fact]
    public async Task Adjustment_preserves_stable_hunk_identity_and_rejects_splitting_existing_target()
    {
        Guid id, hunkId;
        var original = new EditorAiModelChange("replace", ["b1", "b2"], Change(count: 1).AfterJson, "Første forslag", []);
        using (var scope = services.CreateScope())
        {
            var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>(); var store = sp.GetRequiredService<EditorAiRunStore>();
            var run = await store.CreateAsync(Request(), User(), default); id = run.Id; run.Status = "ready";
            var hunk = EditorAiChangeValidation.Validate(original, run, EditorAiChangeValidation.Blocks(Snapshot()), []); hunkId = hunk.Id;
            run.Hunks.Add(hunk); db.EditorAiRunHunks.Add(hunk); await store.PublishAsync(run, default);
            Assert.IsType<OkObjectResult>(await Controller(sp).Adjust(id, new("Gør forslaget bedre", HunkIds: [hunkId]), default));
        }
        handler.Responses.Enqueue(Response([original with { TargetBlockIds = ["b1"], HunkId = hunkId }], false));
        await Worker().ProcessNextAsync(default);
        using (var scope = services.CreateScope())
        {
            var sp = scope.ServiceProvider; var run = await sp.GetRequiredService<AppDbContext>().EditorAiRuns.Include(r => r.Hunks).SingleAsync();
            Assert.Equal("needs_continuation", run.Status); Assert.Equal(1, Assert.Single(run.Hunks).Version);
            Assert.IsType<OkObjectResult>(await Controller(sp).Adjust(id, new("Udbyg forslaget", HunkIds: [hunkId]), default));
        }
        handler.Responses.Enqueue(Response([original with { AfterJson = Change(count: 3).AfterJson, HunkId = hunkId }], false));
        await Worker().ProcessNextAsync(default);
        using var read = services.CreateScope(); var complete = await read.ServiceProvider.GetRequiredService<AppDbContext>().EditorAiRuns.Include(r => r.Hunks).SingleAsync();
        Assert.Equal("ready", complete.Status); var revised = Assert.Single(complete.Hunks);
        Assert.Equal(hunkId, revised.Id); Assert.Equal(2, revised.Version); Assert.Equal(3, EditorAiRunStore.Parse(revised.AfterJson).GetArrayLength());
    }

    [Fact]
    public async Task Idempotency_owner_role_source_access_and_review_versions_are_enforced()
    {
        using var scope = services.CreateScope(); var sp = scope.ServiceProvider; var store = sp.GetRequiredService<EditorAiRunStore>();
        var key = Guid.NewGuid(); var run = await store.CreateAsync(Request(key), User(), default);
        Assert.Equal(run.Id, (await store.CreateAsync(Request(key), User(), default)).Id);
        Assert.Null(await store.OwnedAsync(run.Id, User(Guid.NewGuid()), default));
        Assert.Null(await store.OwnedAsync(run.Id, User(teacher: false), default));
        var db = sp.GetRequiredService<AppDbContext>(); run.Status = "ready";
        run.SourcesJson = EditorAiRunStore.Serialize(new[] { new EditorAiSource("lesson:x", "lesson", "x", "Privat kilde", "/x") }); await db.SaveChangesAsync();
        context.Readable = false;
        await Assert.ThrowsAsync<EditorAiAccessChangedException>(() => store.OwnedAsync(run.Id, User(), default));
        await Assert.ThrowsAsync<EditorAiAccessChangedException>(() => store.CreateAsync(Request(key), User(), default));
        Assert.IsType<ConflictObjectResult>(await Controller(sp).Create(Request(key), default));
        Assert.IsType<ConflictObjectResult>(await Controller(sp).Get(run.Id, default));
        var stopped = Assert.IsType<EditorAiRunView>(Assert.IsType<OkObjectResult>(await Controller(sp).Cancel(run.Id, default)).Value);
        Assert.Equal("cancelled", stopped.Status); Assert.Empty(stopped.Sources); Assert.Empty(stopped.Hunks);
        context.Readable = true;
        var hunk = EditorAiChangeValidation.Validate(Change(count: 1), run, EditorAiChangeValidation.Blocks(Snapshot()), []); run.Hunks.Add(hunk); db.EditorAiRunHunks.Add(hunk); await store.PublishAsync(run, default);
        Assert.IsType<ConflictObjectResult>(await Controller(sp).Review(run.Id, hunk.Id, new("accepted", 2), default));
        Assert.IsType<OkObjectResult>(await Controller(sp).Review(run.Id, hunk.Id, new("accepted", 1), default));
        Assert.IsType<OkObjectResult>(await Controller(sp).Review(run.Id, hunk.Id, new("accepted", 1), default));
        Assert.IsType<ConflictObjectResult>(await Controller(sp).Review(run.Id, hunk.Id, new("rejected", 1), default));
        Assert.Equal(new byte[] { 1, 2, 3 }, (await db.NotionPages.SingleAsync()).YjsState);
    }

    [Fact]
    public async Task Expired_leases_recover_completed_hunks_and_private_tasks_expire()
    {
        using (var scope = services.CreateScope())
        {
            var sp = scope.ServiceProvider; var run = await sp.GetRequiredService<EditorAiRunStore>().CreateAsync(Request(), User(), default);
            run.Status = "generating"; run.LeaseId = Guid.NewGuid(); run.LeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            var hunk = EditorAiChangeValidation.Validate(Change(count: 1), run, EditorAiChangeValidation.Blocks(Snapshot()), []);
            run.Hunks.Add(hunk); sp.GetRequiredService<AppDbContext>().EditorAiRunHunks.Add(hunk);
            await sp.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }
        await Worker().RecoverAsync(default);
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var run = await db.EditorAiRuns.Include(r => r.Hunks).SingleAsync();
            Assert.Equal("needs_continuation", run.Status); Assert.Single(run.Hunks); Assert.Null(run.LeaseId);
            run.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        }
        await Worker().RecoverAsync(default);
        using var read = services.CreateScope(); var cleaned = read.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await cleaned.EditorAiRuns.ToListAsync()); Assert.Empty(await cleaned.EditorAiRunHunks.ToListAsync()); Assert.Empty(await cleaned.EditorAiRunEvents.ToListAsync());
    }

    [Fact]
    public async Task Sse_resumes_after_cursor_and_never_replays_revoked_historical_sources()
    {
        using var scope = services.CreateScope(); var sp = scope.ServiceProvider; var store = sp.GetRequiredService<EditorAiRunStore>();
        var run = await store.CreateAsync(Request(), User(), default);
        var cursor = (await store.ViewAsync(run, default)).EventCursor;
        run.Status = "ready"; await store.PublishAsync(run, default);
        var controller = Controller(sp); using var body = new MemoryStream(); controller.Response.Body = body;
        await controller.Events(run.Id, cursor, default);
        var stream = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("event: run", stream); Assert.DoesNotContain($"id: {cursor}\n", stream);
        Assert.Equal("no", controller.Response.Headers["X-Accel-Buffering"]);
        run.SourcesJson = EditorAiRunStore.Serialize(new[] { new EditorAiSource("lesson:old", "lesson", "old", "REVOKED_SOURCE_TEXT", "/old") });
        await store.PublishAsync(run, default);
        run.SourcesJson = "[]"; await store.PublishAsync(run, default); context.Readable = false;
        var resume = Controller(sp); using var protectedBody = new MemoryStream(); resume.Response.Body = protectedBody;
        await resume.Events(run.Id, 0, default);
        Assert.DoesNotContain("REVOKED_SOURCE_TEXT", Encoding.UTF8.GetString(protectedBody.ToArray()));
    }

    [Fact]
    public void Selection_and_native_content_cannot_be_silently_replaced_or_deleted()
    {
        var doc = Snapshot(); var run = new EditorAiRun { Scope = "selection", TargetJson = """{"blockIds":["b1","b2"],"selection":{"text":"partial edges"}}""" };
        var blocks = EditorAiChangeValidation.Blocks(doc);
        Assert.Throws<ArgumentException>(() => EditorAiChangeValidation.Validate(Change(operation: "replace", count: 1), run, blocks, []));
        Assert.Throws<ArgumentException>(() => EditorAiChangeValidation.Validate(Change(operation: "replace_selection", count: 1), run, blocks, []));
        var native = EditorAiRunStore.Parse("""[{"id":"b1","type":"paragraph","content":"keep","children":[{"id":"native","type":"drawing","props":{"drawingId":"abc"},"children":[]}]}]""");
        run = new() { Scope = "page" };
        Assert.Throws<ArgumentException>(() => EditorAiChangeValidation.Validate(Change(operation: "replace", count: 1), run, EditorAiChangeValidation.Blocks(native), []));
        Assert.Throws<ArgumentException>(() => EditorAiChangeValidation.Validate(new("delete", ["b1"], "[]", "", []), run, EditorAiChangeValidation.Blocks(native), []));
    }

    private static string Response(EditorAiModelChange[] changes, bool more, bool omitTerminal = false)
    {
        var json = EditorAiRunStore.Serialize(new { changes, hasMore = more, assistantNote = "Forslag klar" }); var stream = new StringBuilder();
        for (var i = 0; i < json.Length; i += 43)
            stream.Append("data: ").Append(EditorAiRunStore.Serialize(new { type = "response.output_text.delta", delta = json.Substring(i, Math.Min(43, json.Length - i)) })).Append("\n\n");
        if (!omitTerminal) stream.Append("data: ").Append(EditorAiRunStore.Serialize(new { type = "response.completed", response = new { usage = new { output_tokens = 100 } } })).Append("\n\n");
        return stream.ToString();
    }
    private sealed class FakeContext : IEditorAiContextService
    {
        public bool Readable = true;
        public Func<EditorAiRunRequest, EditorAiContextBundle>? BuildOverride;
        public List<EditorAiRunRequest> Requests = [];
        public Task<EditorAiContextBundle> BuildAsync(ClaimsPrincipal u, Guid page, EditorAiRunRequest r, CancellationToken ct)
        { Requests.Add(r); return Task.FromResult(BuildOverride?.Invoke(r) ?? new EditorAiContextBundle(r.Document.GetRawText(), [], new(2, 2, 100, 0, false))); }
        public Task<bool> CanReadSourceAsync(ClaimsPrincipal user, EditorAiSource source, CancellationToken ct) => Task.FromResult(Readable);
    }
    private sealed class Factory(ProviderHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, false) { BaseAddress = new("https://mock.openai.test/v1/") }; }
    private sealed class ProviderHandler : HttpMessageHandler
    {
        public readonly Queue<string> Responses = new(); public readonly List<string> Payloads = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Payloads.Add(await request.Content!.ReadAsStringAsync(ct));
            return new(HttpStatusCode.OK) { Content = new StringContent(Responses.Dequeue(), Encoding.UTF8, "text/event-stream") };
        }
    }
}
