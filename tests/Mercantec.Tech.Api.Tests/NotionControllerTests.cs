using System.Security.Claims;
using System.Data.Common;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotionControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), outsider = Guid.NewGuid();
    private Guid holdId, otherHoldId, shadowId;
    private const string Secret = "test-service-secret-with-at-least-32-characters";
    private static readonly string Fingerprint = new('a', 64);

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = teacher };
        var hold = new Hold { Id = Guid.NewGuid(), Forloeb = course, Name = "H3 spil" };
        var otherCourse = new Forloeb { Id = Guid.NewGuid(), Name = "Andet", CreatedBySub = outsider };
        var otherHold = new Hold { Id = Guid.NewGuid(), Forloeb = otherCourse, Name = "Andet hold" };
        holdId = hold.Id; otherHoldId = otherHold.Id; shadowId = Guid.NewGuid();
        db.Hold.AddRange(hold, otherHold);
        db.Memberships.AddRange(
            new() { Id = Guid.NewGuid(), Hold = hold, UserSub = student, DisplayName = "Anna" },
            new() { Id = shadowId, Hold = hold, UserSub = null, LectioId = "shadow", DisplayName = "Bo" },
            new() { Id = Guid.NewGuid(), Hold = otherHold, UserSub = null, DisplayName = "Privat elev" });
        db.PollRounds.AddRange(
            new() { Id = Guid.NewGuid(), Hold = hold, Question = "Klar?", State = "open" },
            new() { Id = Guid.NewGuid(), Hold = otherHold, Question = "Privat afstemning", State = "draft" });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    private NotionController As(Guid sub, bool teacherRole = false, AppDbContext? context = null)
    {
        var claims = new List<Claim> { new("sub", sub.ToString()) };
        if (teacherRole) claims.Add(new(ClaimTypes.Role, "Underviser"));
        var database = context ?? db;
        return new(database, new(database))
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
                { User = new(new ClaimsIdentity(claims, "test")) } }
        };
    }
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private async Task<NotionPageSummary> Create() => Value<NotionPageSummary>(
        await As(teacher, true).Create(new(holdId, "Introduktion"), default));
    private NotionStorageController Storage(string? configured = Secret, string? supplied = Secret, byte[]? body = null, string? secretFile = null,
        string? fingerprint = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Notion:ServiceSecret"] = configured, ["Notion:ServiceSecretFile"] = secretFile }).Build();
        var context = new DefaultHttpContext();
        if (supplied is not null) context.Request.Headers["X-Notion-Service-Key"] = supplied;
        context.Request.ContentType = "application/octet-stream";
        context.Request.Headers["X-Notion-State-Fingerprint"] = fingerprint ?? Fingerprint;
        context.Request.Body = new MemoryStream(body ?? []);
        return new(db, config) { ControllerContext = new() { HttpContext = context } };
    }

    [Fact]
    public async Task TeacherPublishedPageListKeepsPublishedTitleWhileDraftChanges()
    {
        var page = await Create();
        Assert.IsType<OkObjectResult>(await Storage(body: [1, 2, 3]).WriteState(page.Id, default));
        var context = Value<NotionPageContext>(await As(teacher, true).Context(page.Id, default));
        Assert.IsType<OkObjectResult>(await As(teacher, true).Publish(page.Id,
            new(context.DraftRevision, Fingerprint, []), default));
        await As(teacher, true).Rename(page.Id, new("Ny privat kladdetitel"), default);
        var draftList = Value<List<NotionPageSummary>>(await As(teacher, true).List(holdId, default));
        Assert.Equal("Ny privat kladdetitel", Assert.Single(draftList).Title);
        var publishedList = Value<List<NotionPageSummary>>(await As(teacher, true).List(holdId, default, publishedOnly: true));
        Assert.Equal("Introduktion", Assert.Single(publishedList).Title);
        Assert.False(publishedList[0].HasUnpublishedChanges);
    }

    [Fact]
    public async Task HoldsRequireLoginAndListAccessibleHoldsWithManagementFlag()
    {
        var controller = As(teacher, true);
        var holds = Value<List<NotionHold>>(await controller.Holds(default));
        Assert.Equal(holdId, Assert.Single(holds).Id);
        var studentHold = Assert.Single(Value<List<NotionHold>>(await As(student).Holds(default)));
        Assert.Equal(holdId, studentHold.Id);
        Assert.False(studentHold.CanManage);
        controller.HttpContext.User = new();
        Assert.IsType<UnauthorizedResult>(await controller.Holds(default));
        Assert.NotEmpty(typeof(NotionController).GetCustomAttributes(typeof(AuthorizeAttribute), false));
    }

    [Fact]
    public async Task TeacherCreatesRenamesAndReadsScopedContextIncludingShadowMembers()
    {
        var page = await Create();
        var renamed = Value<NotionPageSummary>(await As(teacher, true).Rename(page.Id, new("  Teori  "), default));
        Assert.Equal("Teori", renamed.Title);
        var child = Value<NotionPageSummary>(await As(teacher, true).Create(new(holdId, "Øvelse", page.Id), default));
        Assert.Equal(page.Id, child.ParentPageId);
        Assert.Equal(2, Value<List<NotionPageSummary>>(await As(teacher, true).List(holdId, default)).Count);
        var context = Value<NotionPageContext>(await As(teacher, true).Context(page.Id, default));
        Assert.True(context.CanEdit);
        Assert.Equal(2, context.Members.Length);
        Assert.Contains(context.Members, m => m.Id == shadowId && m.DisplayName == "Bo");
        Assert.Equal("Klar?", Assert.Single(context.Polls).Title);
        Assert.Equal(page.Id, Value<NotionCollaborationAccess>(await As(teacher, true)
            .AuthorizeDocument(new("notion:" + page.Id), default)).DocumentId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StudentAndUnrelatedTeacherCannotReadRosterOrChangeOrJoinDocument(bool unrelatedTeacher)
    {
        var page = await Create();
        var unauthorized = As(unrelatedTeacher ? outsider : student, unrelatedTeacher);
        if (unrelatedTeacher) Assert.IsType<NotFoundResult>(await unauthorized.List(holdId, default));
        else Assert.Empty(Value<List<NotionPageSummary>>(await unauthorized.List(holdId, default)));
        Assert.IsType<NotFoundResult>(await unauthorized.Create(new(holdId, "Ulovlig"), default));
        Assert.IsType<NotFoundResult>(await unauthorized.Rename(page.Id, new("Ulovlig"), default));
        Assert.IsType<NotFoundResult>(await unauthorized.Context(page.Id, default));
        Assert.IsType<NotFoundResult>(await unauthorized.AuthorizeDocument(new("notion:" + page.Id), default));
        Assert.Equal("Introduktion", (await db.NotionPages.FindAsync(page.Id))!.Title);
    }

    [Fact]
    public async Task ParentMustExistInSameHoldAndTitlesAndRoomNamesAreValidated()
    {
        var other = Value<NotionPageSummary>(await As(outsider, true).Create(new(otherHoldId, "Andet"), default));
        var controller = As(teacher, true);
        Assert.IsType<BadRequestObjectResult>(await controller.Create(new(holdId, "Barn", other.Id), default));
        Assert.IsType<BadRequestObjectResult>(await controller.Create(new(holdId, "Barn", Guid.NewGuid()), default));
        Assert.IsType<BadRequestObjectResult>(await controller.Create(new(holdId, "  "), default));
        Assert.IsType<BadRequestObjectResult>(await controller.Create(new(holdId, new string('x', 201)), default));
        Assert.IsType<BadRequestObjectResult>(await controller.AuthorizeDocument(new("../../private"), default));
        Assert.IsType<BadRequestObjectResult>(await controller.AuthorizeDocument(new("notion:" + Guid.Empty), default));
        Assert.IsType<NotFoundResult>(await controller.AuthorizeDocument(new("notion:" + Guid.NewGuid()), default));
        var page = await Create();
        Assert.True(NotionController.TryDocumentId("notion:" + page.Id.ToString("D").ToUpperInvariant(), out var parsed));
        Assert.Equal(page.Id, parsed);
        Assert.Equal(page.Id, Value<NotionCollaborationAccess>(await As(teacher, true)
            .AuthorizeDocument(new("notion:" + page.Id.ToString("D").ToUpperInvariant()), default)).DocumentId);
    }

    [Fact]
    public async Task StorageFailsClosedWithoutStrongSecretAndRejectsMissingOrWrongKey()
    {
        var page = await Create();
        Assert.Equal(503, Assert.IsType<ObjectResult>(await Storage(configured: null).ReadState(page.Id, default)).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await Storage(configured: "weak").ReadState(page.Id, default)).StatusCode);
        Assert.IsType<UnauthorizedResult>(await Storage(supplied: null).ReadState(page.Id, default));
        Assert.IsType<UnauthorizedResult>(await Storage(supplied: "incorrect").ReadState(page.Id, default));
        Assert.IsType<UnauthorizedResult>(await Storage(supplied: "incorrect", body: [1]).WriteState(page.Id, default));
        Assert.Null((await db.NotionPages.FindAsync(page.Id))!.YjsState);
    }

    [Fact]
    public async Task ConfiguredSecretFileWorksAndMissingOrInvalidFileFailsClosed()
    {
        var page = await Create();
        var path = Path.Combine(Path.GetTempPath(), "notion-key-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(503, Assert.IsType<ObjectResult>(await Storage(configured: null, secretFile: path).ReadState(page.Id, default)).StatusCode);
            await System.IO.File.WriteAllTextAsync(path, Secret + "\n");
            Assert.IsType<NoContentResult>(await Storage(configured: " ", secretFile: path).ReadState(page.Id, default));
            Assert.IsType<UnauthorizedResult>(await Storage(configured: null, supplied: "incorrect", secretFile: path).ReadState(page.Id, default));
            await System.IO.File.WriteAllTextAsync(path, "weak");
            Assert.Equal(503, Assert.IsType<ObjectResult>(await Storage(configured: null, secretFile: path).ReadState(page.Id, default)).StatusCode);
            Assert.IsType<NoContentResult>(await Storage(secretFile: path).ReadState(page.Id, default));
            await System.IO.File.WriteAllTextAsync(path, new string('x', 1025));
            Assert.Equal(503, Assert.IsType<ObjectResult>(await Storage(configured: null, secretFile: path).ReadState(page.Id, default)).StatusCode);
            Assert.Equal(503, Assert.IsType<ObjectResult>(await Storage(configured: null, secretFile: "relative-key").ReadState(page.Id, default)).StatusCode);
        }
        finally { System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task BinaryStateRoundTripsAndDoesNotOverwriteRenamedTitle()
    {
        var page = await Create();
        Assert.IsType<NoContentResult>(await Storage().ReadState(page.Id, default));
        await As(teacher, true).Rename(page.Id, new("Ny titel"), default);
        var bytes = new byte[] { 0, 255, 128, 3, 0, 4 };
        Assert.IsType<OkObjectResult>(await Storage(body: bytes).WriteState(page.Id, default));
        var read = Assert.IsType<FileContentResult>(await Storage().ReadState(page.Id, default));
        Assert.Equal("application/octet-stream", read.ContentType);
        Assert.Equal(bytes, read.FileContents);
        Assert.Equal("Ny titel", await db.NotionPages.Where(p => p.Id == page.Id).Select(p => p.Title).SingleAsync());
        Assert.IsType<NotFoundResult>(await Storage().ReadState(Guid.NewGuid(), default));
        Assert.IsType<NotFoundResult>(await Storage(body: bytes).WriteState(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task StorageRejectsEmptyWrongTypeAndOversizedRequestsIncludingChunkedBodies()
    {
        var page = await Create();
        Assert.IsType<BadRequestObjectResult>(await Storage().WriteState(page.Id, default));
        var wrongType = Storage(body: [1]); wrongType.Request.ContentType = "application/json";
        Assert.Equal(415, Assert.IsType<StatusCodeResult>(await wrongType.WriteState(page.Id, default)).StatusCode);
        var oversized = Storage(body: [1]); oversized.Request.ContentLength = NotionStorageController.MaxStateBytes + 1;
        Assert.Equal(413, Assert.IsType<StatusCodeResult>(await oversized.WriteState(page.Id, default)).StatusCode);
        var chunked = Storage(body: new byte[NotionStorageController.MaxStateBytes + 1]);
        Assert.Equal(413, Assert.IsType<StatusCodeResult>(await chunked.WriteState(page.Id, default)).StatusCode);
        Assert.Null((await db.NotionPages.FindAsync(page.Id))!.YjsState);
    }

    private async Task<NotionPageContext> SaveDraft(Guid id, byte[] bytes, string? fingerprint = null)
    {
        Assert.IsType<OkObjectResult>(await Storage(body: bytes, fingerprint: fingerprint).WriteState(id, default));
        return Value<NotionPageContext>(await As(teacher, true).Context(id, default));
    }

    private async Task PublishCurrent(Guid id, Guid[]? assets = null)
    {
        var context = Value<NotionPageContext>(await As(teacher, true).Context(id, default));
        var hash = await db.NotionPages.AsNoTracking().Where(p => p.Id == id).Select(p => p.DraftFingerprint).SingleAsync();
        Value<NotionPageSummary>(await As(teacher, true).Publish(id, new(context.DraftRevision, hash!, assets ?? []), default));
    }

    [Fact]
    public async Task PublishedSnapshotAndTitleStayStableWhileTeacherEditsDraft()
    {
        var page = await Create();
        var original = new byte[] { 1, 2, 3 };
        await SaveDraft(page.Id, original);
        await PublishCurrent(page.Id);
        var reader = Value<NotionPageContext>(await As(student).Context(page.Id, default));
        Assert.False(reader.CanEdit);
        Assert.Equal("Introduktion", reader.Title);
        Assert.Equal(original, Convert.FromBase64String(reader.PublishedStateBase64!));

        await As(teacher, true).Rename(page.Id, new("Privat kladdeoverskrift"), default);
        await SaveDraft(page.Id, [9, 8, 7], new string('b', 64));
        var nextReader = Value<NotionPageContext>(await As(student).Context(page.Id, default));
        Assert.Equal(reader.Title, nextReader.Title);
        Assert.Equal(reader.PublishedStateBase64, nextReader.PublishedStateBase64);
        Assert.Equal(reader.DraftRevision, nextReader.DraftRevision);
        Assert.DoesNotContain(nextReader.Pages!, p => p.Title.Contains("Privat"));
        var studentList = Assert.Single(Value<List<NotionPageSummary>>(await As(student).List(holdId, default)));
        Assert.Equal("Introduktion", studentList.Title);
        Assert.False(studentList.HasUnpublishedChanges);
        Assert.True(Assert.Single(Value<List<NotionPageSummary>>(await As(teacher, true).List(holdId, default))).HasUnpublishedChanges);
        var preview = Value<NotionPageContext>(await As(teacher, true).PublishedContext(page.Id, default));
        Assert.False(preview.CanEdit);
        Assert.Equal(reader.PublishedStateBase64, preview.PublishedStateBase64);
    }

    [Fact]
    public async Task StudentsCannotReadDraftsOrPrivateTitlesOrMutateAndOtherHoldsCannotReadPublication()
    {
        var page = await Create();
        var draft = Value<NotionPageSummary>(await As(teacher, true).Create(new(holdId, "Hemmelig underside", page.Id), default));
        db.PollRounds.Add(new() { Id = Guid.NewGuid(), HoldId = holdId, Question = "Hemmelig kladdeafstemning", State = "draft" });
        await db.SaveChangesAsync();
        await SaveDraft(page.Id, [1, 2]);
        await PublishCurrent(page.Id);
        var context = Value<NotionPageContext>(await As(student).Context(page.Id, default));
        Assert.DoesNotContain(context.Pages!, p => p.Id == draft.Id);
        Assert.DoesNotContain(context.Polls, p => p.State == "draft");
        Assert.IsType<NotFoundResult>(await As(student).Context(draft.Id, default));
        Assert.IsType<NotFoundResult>(await As(outsider, true).Context(page.Id, default));
        Assert.IsType<NotFoundResult>(await As(student).Publish(page.Id, new(context.DraftRevision, Fingerprint, []), default));
        Assert.IsType<NotFoundResult>(await As(student).Unpublish(page.Id, default));
        Assert.IsType<NotFoundResult>(await As(student).AuthorizeDocument(new("notion:" + page.Id), default));
    }

    [Fact]
    public async Task PublishedChildIsVisibleWithoutLeakingItsUnpublishedParentAndWithdrawalRevokesReads()
    {
        var parent = await Create();
        var child = Value<NotionPageSummary>(await As(teacher, true).Create(new(holdId, "Offentlig øvelse", parent.Id), default));
        await SaveDraft(child.Id, [3, 4]);
        await PublishCurrent(child.Id);
        var listed = Assert.Single(Value<List<NotionPageSummary>>(await As(student).List(holdId, default)));
        Assert.Equal(child.Id, listed.Id);
        Assert.Null(listed.ParentPageId);
        var links = Value<NotionPageContext>(await As(student).Context(child.Id, default)).Pages;
        Assert.Null(Assert.Single(links!).ParentPageId);
        Value<NotionPageSummary>(await As(teacher, true).Unpublish(child.Id, default));
        Assert.IsType<NotFoundResult>(await As(student).Context(child.Id, default));
        Assert.IsType<NotFoundResult>(await As(teacher, true).PublishedContext(child.Id, default));
        Assert.Empty(Value<List<NotionPageSummary>>(await As(student).List(holdId, default)));
        Assert.True(Value<NotionPageContext>(await As(teacher, true).Context(child.Id, default)).CanEdit);
    }

    [Fact]
    public async Task PublicationRejectsStaleRevisionFingerprintAndForeignAssetsWithoutReplacingSnapshot()
    {
        var page = await Create();
        var context = await SaveDraft(page.Id, [1, 2]);
        Assert.IsType<ConflictObjectResult>(await As(teacher, true).Publish(page.Id, new(context.DraftRevision - 1, Fingerprint, []), default));
        Assert.IsType<ConflictObjectResult>(await As(teacher, true).Publish(page.Id, new(context.DraftRevision, new string('b', 64), []), default));
        Assert.IsType<BadRequestObjectResult>(await As(teacher, true).Publish(page.Id, new(context.DraftRevision, Fingerprint, [Guid.NewGuid()]), default));
        await PublishCurrent(page.Id);
        var before = Value<NotionPageContext>(await As(student).Context(page.Id, default));
        await SaveDraft(page.Id, [5, 6], new string('c', 64));
        Assert.IsType<ConflictObjectResult>(await As(teacher, true).Publish(page.Id, new(context.DraftRevision, Fingerprint, []), default));
        Assert.Equal(before.PublishedStateBase64, Value<NotionPageContext>(await As(student).Context(page.Id, default)).PublishedStateBase64);
    }

    [Fact]
    public async Task PublicationGrantsOnlyReferencedFilesOwnedByThatPageAndWithdrawalClearsGrants()
    {
        var page = await Create();
        var otherPage = Value<NotionPageSummary>(await As(teacher, true).Create(new(holdId, "Anden side"), default));
        NotionAsset Asset(Guid owner) => new()
        {
            Id = Guid.NewGuid(), PageId = owner, Key = Guid.NewGuid().ToString("N"), OriginalName = "lesson.mp4",
            ContentType = "video/mp4", Kind = "video", SizeBytes = 12, UploadedBySub = teacher,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var first = Asset(page.Id); var draftOnly = Asset(page.Id); var foreign = Asset(otherPage.Id);
        db.NotionAssets.AddRange(first, draftOnly, foreign);
        await db.SaveChangesAsync();
        var context = await SaveDraft(page.Id, [1, 2]);
        await PublishCurrent(page.Id, [first.Id]);
        var granted = await db.NotionPages.AsNoTracking().Where(p => p.Id == page.Id).Select(p => p.PublishedAssetIdsJson).SingleAsync();
        Assert.Contains(first.Id.ToString(), granted);
        Assert.DoesNotContain(draftOnly.Id.ToString(), granted);
        Assert.IsType<BadRequestObjectResult>(await As(teacher, true).Publish(page.Id,
            new(context.DraftRevision, Fingerprint, [foreign.Id]), default));
        Assert.Equal(granted, await db.NotionPages.AsNoTracking().Where(p => p.Id == page.Id).Select(p => p.PublishedAssetIdsJson).SingleAsync());
        await As(teacher, true).Unpublish(page.Id, default);
        Assert.Equal("[]", await db.NotionPages.AsNoTracking().Where(p => p.Id == page.Id).Select(p => p.PublishedAssetIdsJson).SingleAsync());
    }

    [Fact]
    public async Task StorageTracksSemanticRevisionAndRejectsInvalidFingerprints()
    {
        var page = await Create();
        var first = await SaveDraft(page.Id, [1, 2]);
        var identical = await SaveDraft(page.Id, [1, 2]);
        Assert.Equal(first.DraftRevision, identical.DraftRevision);
        var changed = await SaveDraft(page.Id, [1, 2, 3], new string('b', 64));
        Assert.Equal(first.DraftRevision + 1, changed.DraftRevision);
        var invalid = Storage(body: [4]); invalid.Request.Headers["X-Notion-State-Fingerprint"] = "";
        Assert.IsType<BadRequestObjectResult>(await invalid.WriteState(page.Id, default));
        Assert.IsType<BadRequestObjectResult>(await Storage(body: [4], fingerprint: "not-a-hash").WriteState(page.Id, default));
        Assert.Equal(changed.DraftRevision, Value<NotionPageContext>(await As(teacher, true).Context(page.Id, default)).DraftRevision);
    }

    [Fact]
    public async Task AuthenticatedLegacyShutdownFlushIsPreservedButCannotPublishUntilV2StampsIt()
    {
        var page = await Create();
        var first = await SaveDraft(page.Id, [1, 2]);
        await PublishCurrent(page.Id);
        var legacy = Storage(body: [5, 6]);
        legacy.Request.Headers.Remove("X-Notion-State-Fingerprint");
        Assert.IsType<OkObjectResult>(await legacy.WriteState(page.Id, default));
        var stored = await db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == page.Id);
        Assert.Equal(new byte[] { 5, 6 }, stored.YjsState);
        Assert.Null(stored.DraftFingerprint);
        Assert.Equal(first.DraftRevision + 1, stored.DraftRevision);
        Assert.IsType<ConflictObjectResult>(await As(teacher, true).Publish(page.Id,
            new(stored.DraftRevision, new string('b', 64), []), default));
        Assert.Equal(new byte[] { 1, 2 }, Convert.FromBase64String(Value<NotionPageContext>(
            await As(student).Context(page.Id, default)).PublishedStateBase64!));
        var stamped = await SaveDraft(page.Id, [5, 6], new string('b', 64));
        Value<NotionPageSummary>(await As(teacher, true).Publish(page.Id,
            new(stamped.DraftRevision, new string('b', 64), []), default));
        Assert.Equal(new byte[] { 5, 6 }, Convert.FromBase64String(Value<NotionPageContext>(
            await As(student).Context(page.Id, default)).PublishedStateBase64!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentDraftChangeOrWithdrawalMakesInFlightPublicationFailAtomically(bool withdraw)
    {
        var page = await Create();
        var context = await SaveDraft(page.Id, [1, 2]);
        await PublishCurrent(page.Id);
        var interceptor = new BeforePublicationUpdate(async command =>
        {
            await using var mutation = command.Connection!.CreateCommand();
            mutation.Transaction = command.Transaction;
            mutation.CommandText = withdraw
                ? "UPDATE notion_pages SET PublishedAtUtc = NULL, PublishedState = NULL, PublicationVersion = PublicationVersion + 1"
                : "UPDATE notion_pages SET DraftRevision = DraftRevision + 1, Title = 'Changed during publication'";
            await mutation.ExecuteNonQueryAsync();
        });
        await using var racing = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(interceptor).Options);
        Assert.IsType<ConflictObjectResult>(await As(teacher, true, racing).Publish(page.Id,
            new(context.DraftRevision, Fingerprint, []), default));
        Assert.True(interceptor.Intercepted);
        if (withdraw) Assert.IsType<NotFoundResult>(await As(student).Context(page.Id, default));
        else Assert.Equal("Introduktion", Value<NotionPageContext>(await As(student).Context(page.Id, default)).Title);
    }

    private sealed class BeforePublicationUpdate(Func<DbCommand, Task> before) : DbCommandInterceptor
    {
        public bool Intercepted { get; private set; }
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Intercepted && command.CommandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("PublishedState", StringComparison.Ordinal))
            {
                Intercepted = true;
                await before(command);
            }
            return result;
        }
    }
}
