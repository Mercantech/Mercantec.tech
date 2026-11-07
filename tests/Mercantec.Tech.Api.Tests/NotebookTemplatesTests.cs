using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Drawings;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotebookTemplatesTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private AppDbContext db = null!;
    private readonly Guid pupil = Guid.NewGuid(), teacher = Guid.NewGuid();
    private Guid notebookId;
    private static readonly IConfiguration Config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["Notion:ServiceSecret"] = "notebook-test-service-key-at-least-32-characters", ["Notebook:CollaborationBaseUrl"] = "http://test-engine" }).Build();
    private readonly CloneHandler handler = new();

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await NotebookDefaults.EnsureAsync(db);
        notebookId = Guid.NewGuid();
        db.Notebooks.Add(new() { Id = notebookId, OwnerSub = pupil, CreatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private NotebookTemplatesController As(bool isTeacher = false)
    {
        db.ChangeTracker.Clear();
        var claims = new List<Claim> { new("sub", (isTeacher ? teacher : pupil).ToString()) };
        if (isTeacher) claims.Add(new("role", "Underviser"));
        var http = new HttpClient(handler);
        var drawings = new DrawingService(db, new(db, new(db), new(db)), null!, new Factory(handler), Config, new(Config), new EphemeralDataProtectionProvider());
        return new(db, new(db), new(http, Config, new(Config)), drawings)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } } };
    }
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    [Fact]
    public async Task AllPupilsSeeActiveVersionsAndNeverUnpublishedTemplateMetadata()
    {
        var defaults = Value<NotebookTemplateView[]>(await As().List(default));
        Assert.Single(defaults); Assert.Equal("Dagens refleksion", defaults[0].Title);
        var draft = Value<NotebookTemplateView>(await As(true).Create(new("Hemmeligt udkast", "weekly"), default));
        Assert.Single(Value<NotebookTemplateView[]>(await As().List(default)));
        Assert.IsType<ForbidResult>(await As().Context(draft.Id, default));
        var context = Value<NotebookDocumentContext>(await As(true).Context(draft.Id, default));
        await As(true).Activate(draft.Id, new(context.Revision, context.Fingerprint!), default);
        await As(true).Update(draft.Id, new("Ny kladdetitel", "free"), default);
        var visible = Value<NotebookTemplateView[]>(await As().List(default)).Single(t => t.Id == draft.Id);
        Assert.Equal("Hemmeligt udkast", visible.Title); Assert.Equal("weekly", visible.Cadence);
    }

    [Fact]
    public async Task PeriodEntriesReuseExistingPageEvenAfterTemplateUpdatesAndDifferentRequests()
    {
        var day = new DateOnly(2026, 12, 31);
        var first = Value<NotebookPageSummary>(await As().EnsureEntry(new(NotebookDefaults.TemplateId, Guid.NewGuid(), day), default));
        var again = Value<NotebookPageSummary>(await As().EnsureEntry(new(NotebookDefaults.TemplateId, Guid.NewGuid(), day), default));
        Assert.Equal(first.Id, again.Id); Assert.Equal(1, handler.Calls);
        var original = await db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == first.Id);
        var originalVersion = original.TemplateVersionId;
        await As(true).Update(NotebookDefaults.TemplateId, new("Opdateret refleksion", "daily"), default);
        var context = Value<NotebookDocumentContext>(await As(true).Context(NotebookDefaults.TemplateId, default));
        Value<NotebookTemplateView>(await As(true).Activate(NotebookDefaults.TemplateId, new(context.Revision, context.Fingerprint!), default));
        var samePeriod = Value<NotebookPageSummary>(await As().EnsureEntry(new(NotebookDefaults.TemplateId, Guid.NewGuid(), day), default));
        Assert.Equal(first.Id, samePeriod.Id);
        var next = Value<NotebookPageSummary>(await As().EnsureEntry(new(NotebookDefaults.TemplateId, Guid.NewGuid(), day.AddDays(1)), default));
        Assert.StartsWith("Opdateret refleksion", next.Title);
        var unchanged = await db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == first.Id);
        Assert.Equal(originalVersion, unchanged.TemplateVersionId); Assert.Equal(original.Title, unchanged.Title);
        Assert.Equal(original.YjsState, unchanged.YjsState);
    }

    [Fact]
    public async Task FreeEntryRetryIsIdempotentAndArchivePreservesExistingEntries()
    {
        await As(true).Update(NotebookDefaults.TemplateId, new("Projektrefleksion", "free"), default);
        var context = Value<NotebookDocumentContext>(await As(true).Context(NotebookDefaults.TemplateId, default));
        await As(true).Activate(NotebookDefaults.TemplateId, new(context.Revision, context.Fingerprint!), default);
        var request = new EnsureJournalEntry(NotebookDefaults.TemplateId, Guid.NewGuid());
        var one = Value<NotebookPageSummary>(await As().EnsureEntry(request, default));
        var two = Value<NotebookPageSummary>(await As().EnsureEntry(request with { RequestId = Guid.NewGuid() }, default));
        Assert.NotEqual(one.Id, two.Id);
        await As(true).Archive(NotebookDefaults.TemplateId, default);
        Assert.Empty(Value<NotebookTemplateView[]>(await As().List(default)));
        var retry = Value<NotebookPageSummary>(await As().EnsureEntry(request, default));
        Assert.Equal(one.Id, retry.Id);
        Assert.IsType<NotFoundResult>(await As().EnsureEntry(request with { RequestId = Guid.NewGuid() }, default));
        Assert.Equal(2, await db.NotebookDocuments.CountAsync(d => d.NotebookId == notebookId));
    }

    [Fact]
    public async Task ActivationRejectsStaleRevisionOrFingerprintAndPupilCannotManageTemplates()
    {
        Assert.IsType<ForbidResult>(await As().Create(new("Elevskabelon", "daily"), default));
        Assert.IsType<ForbidResult>(await As().Archive(NotebookDefaults.TemplateId, default));
        var current = Value<NotebookDocumentContext>(await As(true).Context(NotebookDefaults.TemplateId, default));
        Assert.IsType<ConflictObjectResult>(await As(true).Activate(NotebookDefaults.TemplateId,
            new(current.Revision + 1, current.Fingerprint!), default));
        Assert.IsType<ConflictObjectResult>(await As(true).Activate(NotebookDefaults.TemplateId,
            new(current.Revision, new string('b', 64)), default));
        Assert.Equal(1, await db.NotebookTemplateVersions.CountAsync());
    }

    [Fact]
    public async Task ConcurrentRequestIdReuseForAnotherTemplateReturnsConflictInsteadOfWrongDocument()
    {
        var other = Value<NotebookTemplateView>(await As(true).Create(new("Anden skabelon", "free"), default));
        var context = Value<NotebookDocumentContext>(await As(true).Context(other.Id, default));
        await As(true).Activate(other.Id, new(context.Revision, context.Fingerprint!), default);
        var requestId = Guid.NewGuid();
        // Simulate another request committing after the initial idempotency lookup, during cloning.
        handler.BeforeReturn = async () =>
        {
            db.NotebookDocuments.Add(new NotebookDocument { Id = Guid.NewGuid(), NotebookId = notebookId,
                Kind = "journal", Title = "Den anden samtidige forespørgsel", TemplateId = NotebookDefaults.TemplateId,
                TemplateVersionId = NotebookDefaults.VersionId, RequestId = requestId,
                YjsState = [0, 0], Fingerprint = new string('a', 64), Revision = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        };
        var result = await As().EnsureEntry(new(other.Id, requestId), default);
        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(await db.NotebookDocuments.Where(d => d.NotebookId == notebookId).ToArrayAsync());
    }

    [Fact]
    public async Task TemplateMediaGetsIndependentAssetGrantsForEachPupilPage()
    {
        var oldId = Guid.NewGuid();
        db.NotebookAssets.Add(new() { Id = oldId, DocumentId = NotebookDefaults.TemplateId,
            Key = "notebook-private/template/image", OriginalName = "note.png", ContentType = "image/png", Kind = "image",
            SizeBytes = 123, UploadedBySub = teacher, CreatedAtUtc = DateTimeOffset.UtcNow });
        var version = await db.NotebookTemplateVersions.SingleAsync(v => v.Id == NotebookDefaults.VersionId);
        version.AssetIdsJson = JsonSerializer.Serialize(new[] { oldId }); await db.SaveChangesAsync();
        var page = Value<NotebookPageSummary>(await As().EnsureEntry(new(NotebookDefaults.TemplateId, Guid.NewGuid()), default));
        var copy = await db.NotebookAssets.SingleAsync(a => a.DocumentId == page.Id);
        Assert.NotEqual(oldId, copy.Id); Assert.Equal("notebook-private/template/image", copy.Key);
        Assert.Equal(page.Id.ToString(), handler.LastRequest!.PageId.ToString());
        Assert.Equal(copy.Id.ToString(), handler.LastRequest.AssetMap[oldId.ToString()]);
        var stored = await db.NotebookDocuments.SingleAsync(d => d.Id == page.Id);
        Assert.Contains(copy.Id.ToString(), stored.AssetIdsJson);
    }

    [Fact]
    public async Task TemplateDrawingActivationCheckpointsBeforeLockAndRejectsStaleDrawingConsent()
    {
        var drawing = await AddTemplateDrawing();
        handler.BeforeCheckpoint = () => Assert.Null(db.Database.CurrentTransaction);
        var context = Value<NotebookDocumentContext>(await As(true).Context(NotebookDefaults.TemplateId, default));
        var bad = await As(true).Activate(NotebookDefaults.TemplateId, new(context.Revision, context.Fingerprint!,
            [new(drawing.Id, "snapshot", drawing.Revision + 1, drawing.Fingerprint!)]), default);
        Assert.Equal(409, Assert.IsType<ObjectResult>(bad).StatusCode);
        Assert.Empty(await db.DrawingSnapshots.ToArrayAsync());
        var good = await As(true).Activate(NotebookDefaults.TemplateId, new(context.Revision, context.Fingerprint!,
            [new(drawing.Id, "snapshot", drawing.Revision, drawing.Fingerprint!)]), default);
        Value<NotebookTemplateView>(good);
        Assert.True(handler.CheckpointCalls >= 2);
        var template = await db.NotebookTemplates.AsNoTracking().SingleAsync(t => t.Id == NotebookDefaults.TemplateId);
        var version = await db.NotebookTemplateVersions.AsNoTracking().SingleAsync(v => v.Id == template.ActiveVersionId);
        var binding = Assert.Single(JsonSerializer.Deserialize<DrawingPublicationBinding[]>(version.DrawingsJson)!);
        Assert.Equal(drawing.Id, binding.DrawingId); Assert.Equal("snapshot", binding.Mode); Assert.NotNull(binding.SnapshotId);
        Assert.Single(await db.DrawingSnapshots.ToArrayAsync());
    }

    [Fact]
    public async Task JournalDrawingCopiesUseImmutableSnapshotAndIndependentDrawingAssetAndBlockBindings()
    {
        var source = await AddTemplateDrawing();
        var context = Value<NotebookDocumentContext>(await As(true).Context(NotebookDefaults.TemplateId, default));
        Value<NotebookTemplateView>(await As(true).Activate(NotebookDefaults.TemplateId,
            new(context.Revision, context.Fingerprint!, [new(source.Id, "snapshot", source.Revision, source.Fingerprint!)]), default));
        var head = await db.Drawings.SingleAsync(d => d.Id == source.Id);
        head.SceneJson = "{\"elements\":[{\"id\":\"new-head\",\"type\":\"text\",\"text\":\"Senere kladde\"}],\"files\":{},\"appState\":{\"viewBackgroundColor\":\"#ffffff\"}}";
        head.Fingerprint = new string('b', 64); head.Revision++;
        await db.SaveChangesAsync();
        var first = Value<NotebookPageSummary>(await As().EnsureEntry(new(NotebookDefaults.TemplateId, Guid.NewGuid(), new DateOnly(2026, 11, 1)), default));
        var firstRequest = handler.LastRequest!;
        var second = Value<NotebookPageSummary>(await As().EnsureEntry(new(NotebookDefaults.TemplateId, Guid.NewGuid(), new DateOnly(2026, 11, 2)), default));
        var copies = await db.Drawings.AsNoTracking().Where(d => d.NotebookDocumentId == first.Id || d.NotebookDocumentId == second.Id).ToArrayAsync();
        Assert.Equal(2, copies.Length); Assert.NotEqual(copies[0].Id, copies[1].Id);
        Assert.All(copies, copy =>
        {
            Assert.NotEqual(source.Id, copy.Id); Assert.StartsWith("cloned-", copy.BlockId);
            Assert.DoesNotContain("Senere kladde", copy.SceneJson); Assert.Contains("Original", copy.SceneJson);
            Assert.Equal(1, copy.Revision); Assert.Equal(copy.Fingerprint, copy.PreviewFingerprint);
        });
        var firstCopy = copies.Single(d => d.NotebookDocumentId == first.Id);
        var secondDrawingId = copies.Single(d => d.NotebookDocumentId == second.Id).Id;
        Assert.Equal(firstCopy.Id.ToString(), firstRequest.DrawingMap![source.Id.ToString()]);
        var firstAssets = await db.DrawingAssets.Where(a => a.DrawingId == firstCopy.Id).Select(a => a.Id).ToArrayAsync();
        var secondAssets = await db.DrawingAssets.Where(a => a.DrawingId == secondDrawingId).Select(a => a.Id).ToArrayAsync();
        Assert.Equal(3, firstAssets.Length); Assert.Empty(firstAssets.Intersect(secondAssets));
        Assert.Empty(await db.DrawingGrants.Where(g => g.DrawingId == firstCopy.Id).ToArrayAsync());
        var firstDoc = await db.NotebookDocuments.AsNoTracking().SingleAsync(d => d.Id == first.Id);
        Assert.Equal(new[] { firstCopy.Id }, JsonSerializer.Deserialize<Guid[]>(firstDoc.DrawingIdsJson));
        var again = Value<NotebookPageSummary>(await As().EnsureEntry(new(NotebookDefaults.TemplateId, Guid.NewGuid(), new DateOnly(2026, 11, 1)), default));
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(3, await db.Drawings.CountAsync());
    }

    [Fact]
    public async Task FailedDrawingBlockBindingNeverPersistsPartialJournalOrDrawingCopies()
    {
        var source = await AddTemplateDrawing();
        var context = Value<NotebookDocumentContext>(await As(true).Context(NotebookDefaults.TemplateId, default));
        Value<NotebookTemplateView>(await As(true).Activate(NotebookDefaults.TemplateId,
            new(context.Revision, context.Fingerprint!, [new(source.Id, "snapshot", source.Revision, source.Fingerprint!)]), default));
        handler.MissingDrawingBinding = true;
        var result = await As().EnsureEntry(new(NotebookDefaults.TemplateId, Guid.NewGuid()), default);
        Assert.Equal(400, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(1, await db.Drawings.CountAsync());
        Assert.Equal(3, await db.DrawingAssets.CountAsync());
        Assert.Empty(await db.NotebookDocuments.Where(d => d.NotebookId == notebookId).ToArrayAsync());
    }

    private async Task<Drawing> AddTemplateDrawing()
    {
        var id = Guid.NewGuid(); var image = Guid.NewGuid(); var light = Guid.NewGuid(); var dark = Guid.NewGuid();
        var drawing = new Drawing { Id = id, NotebookDocumentId = NotebookDefaults.TemplateId, BlockId = "template-drawing-block",
            YjsState = [0, 0], Fingerprint = new string('a', 64), Revision = 1,
            SceneJson = JsonSerializer.Serialize(new { elements = new object[] { new { id = "image", type = "image", fileId = "hash", text = "Original" } },
                files = new Dictionary<string, object> { ["hash"] = new { assetId = image } }, appState = new { viewBackgroundColor = "#ffffff" } }),
            AssetIdsJson = JsonSerializer.Serialize(new[] { image }), PreviewLightAssetId = light, PreviewDarkAssetId = dark,
            PreviewFingerprint = new string('a', 64), PreviewRevision = 1, CreatedBySub = teacher,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow };
        db.Drawings.Add(drawing);
        foreach (var (asset, kind) in new[] { (image, "image"), (light, "preview-light"), (dark, "preview-dark") })
            db.DrawingAssets.Add(new() { Id = asset, DrawingId = id, Key = "drawing-private/" + asset, OriginalName = "drawing.png", Kind = kind,
                ContentType = "image/png", SizeBytes = 100, UploadedBySub = teacher, CreatedAtUtc = DateTimeOffset.UtcNow });
        var doc = await db.NotebookDocuments.SingleAsync(d => d.Id == NotebookDefaults.TemplateId);
        doc.DrawingIdsJson = JsonSerializer.Serialize(new[] { id });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        return drawing;
    }

    [Theory]
    [InlineData("2026-03-28T23:30:00Z", "2026-03-29")]
    [InlineData("2026-10-24T22:30:00Z", "2026-10-25")]
    [InlineData("2026-12-31T23:30:00Z", "2027-01-01")]
    public void LocalDayUsesCopenhagenAcrossDstAndNewYear(string instant, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), NotebookPeriods.LocalDay(DateTimeOffset.Parse(instant)));

    [Theory]
    [InlineData("2027-01-03", "2026-12-28")]
    [InlineData("2027-01-04", "2027-01-04")]
    public void WeeklyPeriodUsesMondayDateAcrossWeekYears(string day, string monday) =>
        Assert.Equal("weekly:" + monday, NotebookPeriods.Key("weekly", DateOnly.Parse(day)));

    private sealed class CloneHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public CloneNotebookDocument? LastRequest { get; private set; }
        public Func<Task>? BeforeReturn { get; set; }
        public Action? BeforeCheckpoint { get; set; }
        public int CheckpointCalls { get; private set; }
        public bool MissingDrawingBinding { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/internal/drawing/checkpoint")
            {
                CheckpointCalls++; BeforeCheckpoint?.Invoke();
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { drawings = Array.Empty<object>() }) };
            }
            if (request.RequestUri.AbsolutePath == "/internal/drawing/clone")
            {
                var body = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
                var scene = System.Text.Json.Nodes.JsonNode.Parse(body.GetProperty("sceneJson").GetString()!)!;
                var assets = new List<Guid>();
                foreach (var file in scene["files"]!.AsObject())
                {
                    var old = file.Value!["assetId"]!.GetValue<string>();
                    var mapped = body.GetProperty("assetMap").GetProperty(old).GetString()!;
                    file.Value["assetId"] = mapped; assets.Add(Guid.Parse(mapped));
                }
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new StoreDrawingDocument(Convert.ToBase64String([0, 0]), new string('a', 64), scene.ToJsonString(), assets.ToArray())) };
            }
            Calls++; Assert.Equal("/internal/notebook/clone", request.RequestUri!.AbsolutePath);
            Assert.True(request.Headers.Contains("X-Notion-Service-Key"));
            LastRequest = await request.Content!.ReadFromJsonAsync<CloneNotebookDocument>(ct);
            if (BeforeReturn is { } action) { BeforeReturn = null; await action(); }
            // Transport fake; actual live-tree cloning, fresh block IDs and media rebinding are covered in Node tests.
            var projection = new StoreNotebookDocument(LastRequest!.StateBase64 ?? Convert.ToBase64String([0, 0]),
                new string('a', 64), "Refleksion", [], LastRequest.AssetMap.Values.Select(Guid.Parse).ToArray(),
                LastRequest.DrawingMap?.Values.Select(Guid.Parse).ToArray() ?? [],
                MissingDrawingBinding ? [] : LastRequest.DrawingMap?.Values.ToDictionary(value => value, value => "cloned-" + Guid.Parse(value).ToString("N")));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(projection) };
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, disposeHandler: false); }
}
