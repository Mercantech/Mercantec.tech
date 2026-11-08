using System.Security.Claims;
using System.Data.Common;
using System.Text.Json;
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

    [Fact]
    public async Task LightweightDraftContextReadsOnlySelectedPageWithoutPublicationOrHoldResources()
    {
        var page = await Create();
        await AddPage(WeekId, "Anden side", page.Id);
        await SetDocument(page.Id, [1, 2], new byte[1_000_000], "Udgivet titel");
        var queries = new ReadQueries();
        await using var lean = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(queries).Options);

        var controller = As(teacher, true, lean);
        var context = Value<NotionPageContext>(await controller.Context(page.Id, default, includeResources: false));

        Assert.True(context.CanEdit);
        Assert.Equal(page.Id, context.PageId);
        Assert.Equal("Introduktion", context.Title);
        Assert.Null(context.PublishedStateBase64);
        Assert.Empty(context.Members);
        Assert.Empty(context.Polls);
        Assert.Empty(context.Pages!);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal(4, queries.Commands.Count);
        Assert.All(queries.Commands, sql =>
        {
            Assert.DoesNotContain(".\"YjsState\"", sql, StringComparison.Ordinal);
            Assert.DoesNotContain(".\"PublishedState\"", sql, StringComparison.Ordinal);
            Assert.DoesNotContain(".\"PublishedDrawingsJson\"", sql, StringComparison.Ordinal);
        });
        Assert.Equal(2, queries.Commands.Count(sql => sql.Contains("FROM \"notion_pages\"", StringComparison.Ordinal)));
        Assert.All(queries.Commands.Where(sql => sql.Contains("FROM \"notion_pages\"", StringComparison.Ordinal)),
            sql => Assert.Contains("WHERE \"n\".\"Id\" =", sql, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LightweightPublishedContextKeepsSelectedSnapshotAndDoesNotReadOtherPages()
    {
        var page = await Create();
        await AddPage(WeekId, "Anden side", page.Id);
        await SetDocument(page.Id, [9, 9], [1, 2, 3], "Udgivet titel");
        var queries = new ReadQueries();
        await using var lean = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(queries).Options);

        var context = Value<NotionPageContext>(await As(teacher, true, lean)
            .PublishedContext(page.Id, default, includeResources: false));

        Assert.False(context.CanEdit);
        Assert.Equal("Udgivet titel", context.Title);
        Assert.Equal(new byte[] { 1, 2, 3 }, Convert.FromBase64String(context.PublishedStateBase64!));
        Assert.Empty(context.Members);
        Assert.Empty(context.Polls);
        Assert.Empty(context.Pages!);
        Assert.Equal(3, queries.Commands.Count);
        Assert.All(queries.Commands.Where(sql => sql.Contains("FROM \"notion_pages\"", StringComparison.Ordinal)),
            sql => Assert.Contains("WHERE \"n\".\"Id\" =", sql, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeferredEditorResourcesAreScopedAndPublishedPreviewExcludesDraftPolls()
    {
        db.PollRounds.Add(new() { Id = Guid.NewGuid(), HoldId = holdId, Question = "Privat afstemning", State = "draft" });
        await db.SaveChangesAsync();

        var controller = As(teacher, true);
        var resources = Value<NotionEditorResources>(await controller.EditorResources(holdId, default));
        Assert.Equal(2, resources.Members.Length);
        Assert.Contains(resources.Members, m => m.Id == shadowId);
        Assert.Equal(2, resources.Polls.Length);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("Klar?", Assert.Single(Value<NotionEditorResources>(await As(teacher, true)
            .EditorResources(holdId, default, publishedOnly: true)).Polls).Title);
        Assert.Equal("Klar?", Assert.Single(Value<NotionEditorResources>(await As(student)
            .EditorResources(holdId, default)).Polls).Title);
        Assert.IsType<NotFoundResult>(await As(outsider, true).EditorResources(holdId, default));
        Assert.IsType<NotFoundResult>(await As(teacher, true).EditorResources(otherHoldId, default));
    }

    [Fact]
    public async Task NavigationReadsOnlyAccessibleHoldAndVisibleCourseIds()
    {
        TeachingCourse Course(Guid hold, bool published = false, bool archived = false) => new()
            { Id = Guid.NewGuid(), HoldId = hold, Title = "Forløb", Published = published, Archived = archived };
        var published = Course(holdId, true);
        var draft = Course(holdId);
        db.TeachingCourses.AddRange(published, draft, Course(holdId, true, true), Course(otherHoldId, true));
        await db.SaveChangesAsync();
        var queries = new ReadQueries();
        await using var lean = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(queries).Options);
        var controller = As(teacher, true, lean);

        var navigation = Value<NotionNavigationContext>(await controller.Navigation(holdId, default));

        Assert.Equal(holdId, navigation.HoldId);
        Assert.Equal(await db.Hold.Where(h => h.Id == holdId).Select(h => h.ForloebId).SingleAsync(), navigation.WorkspaceId);
        Assert.Equal(new HashSet<Guid> { published.Id, draft.Id }, navigation.CourseIds.ToHashSet());
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal(3, queries.Commands.Count);
        Assert.DoesNotContain(queries.Commands, sql => sql.Contains("notion_pages", StringComparison.Ordinal));
        Assert.Equal(published.Id, Assert.Single(Value<NotionNavigationContext>(await As(teacher, true)
            .Navigation(holdId, default, publishedOnly: true)).CourseIds));
        Assert.Equal(published.Id, Assert.Single(Value<NotionNavigationContext>(await As(student)
            .Navigation(holdId, default)).CourseIds));
        Assert.IsType<NotFoundResult>(await As(outsider, true).Navigation(holdId, default));
        Assert.IsType<NotFoundResult>(await As(teacher, true).Navigation(otherHoldId, default));
    }

    [Fact]
    public async Task WorkspacePageOverviewNeverMaterializesDocumentBinaries()
    {
        var page = await Create();
        await SetDocument(page.Id, new byte[1_000_000], new byte[1_000_000], "Udgivet titel");
        var workspaceId = await db.Hold.Where(h => h.Id == holdId).Select(h => h.ForloebId).SingleAsync();
        var queries = new ReadQueries();
        await using var lean = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(queries).Options);
        var controller = new HoldWorkspaceController(lean, new(lean)) { ControllerContext = As(teacher, true).ControllerContext };

        var workspace = Value<HoldWorkspaceView>(await controller.Get(workspaceId, false, default));

        Assert.Equal(page.Id, Assert.Single(workspace.Pages).Id);
        var sql = Assert.Single(queries.Commands, sql => sql.Contains("FROM \"notion_pages\"", StringComparison.Ordinal));
        Assert.DoesNotContain("YjsState", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishedState", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishedDrawingsJson", sql, StringComparison.Ordinal);
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
    public async Task StorageSavesAuthoritativeDrawingProjectionAndRejectsForeignOrMismatchedBindings()
    {
        var page = await Create();
        var other = await Create();
        var drawing = new Drawing { Id = Guid.NewGuid(), NotionPageId = page.Id, BlockId = "drawing-block", CreatedBySub = teacher };
        var foreign = new Drawing { Id = Guid.NewGuid(), NotionPageId = other.Id, BlockId = "foreign-block", CreatedBySub = teacher };
        db.Drawings.AddRange(drawing, foreign);
        await db.SaveChangesAsync();

        async Task<IActionResult> Write(Guid[] ids, Dictionary<string, string>? blocks)
        {
            var controller = Storage(body: JsonSerializer.SerializeToUtf8Bytes(new StoreNotionDocument(
                Convert.ToBase64String([7, 8, 9]), Fingerprint, ids, blocks)));
            controller.Request.ContentType = "application/json";
            return await controller.WriteState(page.Id, default);
        }

        Assert.IsType<BadRequestObjectResult>(await Write([foreign.Id], new() { [foreign.Id.ToString()] = foreign.BlockId }));
        Assert.IsType<BadRequestObjectResult>(await Write([drawing.Id, drawing.Id], new() { [drawing.Id.ToString()] = drawing.BlockId }));
        Assert.IsType<BadRequestObjectResult>(await Write([drawing.Id], null));
        Assert.IsType<BadRequestObjectResult>(await Write([drawing.Id], new() { [drawing.Id.ToString()] = "different-block" }));
        Assert.Null(await db.NotionPages.Where(p => p.Id == page.Id).Select(p => p.YjsState).SingleAsync());
        Assert.IsType<OkObjectResult>(await Write([drawing.Id], new() { [drawing.Id.ToString()] = drawing.BlockId }));
        var saved = await db.NotionPages.AsNoTracking().SingleAsync(p => p.Id == page.Id);
        Assert.Equal(new byte[] { 7, 8, 9 }, saved.YjsState);
        Assert.Equal(Fingerprint, saved.DraftFingerprint);
        Assert.Equal(new[] { drawing.Id }, JsonSerializer.Deserialize<Guid[]>(saved.DrawingIdsJson));

        // An upgraded collaboration server can also remove the final drawing reference.
        Assert.IsType<OkObjectResult>(await Write([], new()));
        Assert.Equal("[]", await db.NotionPages.Where(p => p.Id == page.Id).Select(p => p.DrawingIdsJson).SingleAsync());
    }

    [Fact]
    public async Task StorageRejectsEmptyWrongTypeAndOversizedRequestsIncludingChunkedBodies()
    {
        var page = await Create();
        Assert.IsType<BadRequestObjectResult>(await Storage().WriteState(page.Id, default));
        var wrongType = Storage(body: [1]); wrongType.Request.ContentType = "text/plain";
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

    [Fact]
    public async Task GraphUsesLivePageLinksInsideTheCollection()
    {
        var root = await Create();
        await AddPage(WeekId, "Uge 1", root.Id);
        await AddPage(TaskId, "Opgave", WeekId);
        await AddPage(OutsideId, "Andet forløb", null);
        var foreign = new NotionPage
        {
            Id = Guid.NewGuid(), HoldId = otherHoldId, Title = "Privat", CreatedBySub = outsider,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
            PublishedTitle = "Privat", PublishedAtUtc = DateTimeOffset.UtcNow, PublishedState = GraphFixtures.Get("empty")
        };
        db.NotionPages.Add(foreign);
        await db.SaveChangesAsync();
        await SetDocument(root.Id, GraphFixtures.Get("basic"), GraphFixtures.Get("empty"), "Udgivet H3");

        var teacher = As(this.teacher, true);
        var graph = Value<NotionGraph>(await teacher.Graph(root.Id, default));
        Assert.Equal("no-store", teacher.Response.Headers.CacheControl.ToString());
        Assert.Equal(root.Id, graph.CollectionId);
        Assert.False(graph.WholeHold);
        Assert.Equal(holdId, graph.HoldId);
        Assert.True(graph.CanRename);
        Assert.Equal("Introduktion", graph.Title);
        Assert.Equal(new HashSet<Guid> { root.Id, WeekId, TaskId }, graph.Nodes.Select(n => n.Id).ToHashSet());
        Assert.DoesNotContain(graph.Nodes, n => n.Id == OutsideId);
        Assert.Equal([(root.Id, WeekId), (root.Id, TaskId)], Edges(graph));

        var student = Value<NotionGraph>(await As(this.student).Graph(root.Id, default));
        Assert.False(student.CanRename);
        Assert.Equal("Udgivet H3", student.Title);
        Assert.Equal(root.Id, Assert.Single(student.Nodes).Id);
        Assert.Empty(student.Edges);
        var preview = Value<NotionGraph>(await As(this.teacher, true).Graph(root.Id, default, publishedOnly: true));
        Assert.False(preview.CanRename);
        Assert.Empty(preview.Edges);
        Assert.Equal("Udgivet H3", preview.Title);

        Assert.IsType<NotFoundResult>(await As(outsider, true).Graph(root.Id, default));
        Assert.IsType<NotFoundResult>(await As(this.student).Graph(WeekId, default));
        Assert.IsType<NotFoundResult>(await As(this.student).Graph(foreign.Id, default));

        var week = Value<NotionGraph>(await As(this.teacher, true).Graph(WeekId, default));
        Assert.Equal(new HashSet<Guid> { WeekId, TaskId }, week.Nodes.Select(n => n.Id).ToHashSet());
        Assert.Empty(week.Edges);

        await SetDocument(WeekId, GraphFixtures.Get("basic"));
        Assert.Equal([(WeekId, TaskId)], Edges(Value<NotionGraph>(await As(this.teacher, true).Graph(WeekId, default))));
        Assert.Contains((WeekId, TaskId), Edges(Value<NotionGraph>(await As(this.teacher, true).Graph(root.Id, default))));
        await SetDocument(WeekId, null);

        await SetDocument(root.Id, GraphFixtures.Get("deleted"));
        Assert.Equal([(root.Id, TaskId)], Edges(Value<NotionGraph>(await As(this.teacher, true).Graph(root.Id, default))));
        await SetDocument(root.Id, GraphFixtures.Get("overwrite"));
        Assert.Equal([(root.Id, TaskId)], Edges(Value<NotionGraph>(await As(this.teacher, true).Graph(root.Id, default))));
        await SetDocument(root.Id, GraphFixtures.Get("merged"));
        Assert.Equal([(root.Id, WeekId), (root.Id, TaskId)], Edges(Value<NotionGraph>(await As(this.teacher, true).Graph(root.Id, default))));
        await SetDocument(root.Id, GraphFixtures.Get("nested"));
        Assert.Equal([(root.Id, WeekId)], Edges(Value<NotionGraph>(await As(this.teacher, true).Graph(root.Id, default))));
        await SetDocument(root.Id, [1, 2, 3, 4, 5]);
        var corrupt = Value<NotionGraph>(await As(this.teacher, true).Graph(root.Id, default));
        Assert.Contains(corrupt.Nodes, n => n.Id == root.Id);
        Assert.Empty(corrupt.Edges);

        await SetDocument(WeekId, GraphFixtures.Get("empty"), GraphFixtures.Get("basic"), "Uge 1");
        await SetDocument(TaskId, GraphFixtures.Get("empty"), GraphFixtures.Get("empty"), "Opgave");
        Assert.Empty(Edges(Value<NotionGraph>(await As(this.teacher, true).Graph(WeekId, default))));
        var publishedWeek = Value<NotionGraph>(await As(this.student).Graph(WeekId, default));
        Assert.False(publishedWeek.CanRename);
        Assert.Equal("Uge 1", publishedWeek.Nodes.Single(n => n.Id == WeekId).Title);
        Assert.Equal([(WeekId, TaskId)], Edges(publishedWeek));
    }

    [Fact]
    public async Task HoldGraphIncludesEveryRootAndLinksAcrossThem()
    {
        var root = await Create();
        var noteId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
        var draftRoot = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var publishedChild = Guid.Parse("22222222-2222-4222-8222-222222222222");
        await AddPage(WeekId, "Uge 1", null);
        await AddPage(TaskId, "Opgave", WeekId);
        await AddPage(OutsideId, "Andet forløb", null);
        await AddPage(noteId, "Noter", OutsideId);
        await AddPage(draftRoot, "Kladde-rod", null);
        await AddPage(publishedChild, "Udgivet barn", draftRoot);
        var foreign = new NotionPage
        {
            Id = Guid.NewGuid(), HoldId = otherHoldId, Title = "Privat", CreatedBySub = outsider,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow,
            PublishedTitle = "Privat", PublishedAtUtc = DateTimeOffset.UtcNow, PublishedState = GraphFixtures.Get("empty")
        };
        db.NotionPages.Add(foreign);
        await db.SaveChangesAsync();
        await SetDocument(root.Id, GraphFixtures.Get("basic"), GraphFixtures.Get("empty"), "Udgivet H3");
        await SetDocument(publishedChild, GraphFixtures.Get("empty"), GraphFixtures.Get("empty"), "Udgivet barn");

        var teacher = As(this.teacher, true);
        var hold = Value<NotionGraph>(await teacher.HoldGraph(holdId, default));
        Assert.Equal("no-store", teacher.Response.Headers.CacheControl.ToString());
        Assert.True(hold.WholeHold);
        Assert.Equal(Guid.Empty, hold.CollectionId);
        Assert.Equal(holdId, hold.HoldId);
        Assert.Equal("H3 spil", hold.Title);
        Assert.True(hold.CanRename);
        Assert.Equal("Introduktion", hold.Nodes.Single(n => n.Id == root.Id).Title);
        Assert.Equal(
            new HashSet<Guid> { root.Id, WeekId, TaskId, OutsideId, noteId, draftRoot, publishedChild },
            hold.Nodes.Select(n => n.Id).ToHashSet());
        Assert.DoesNotContain(hold.Nodes, n => n.Id == foreign.Id);
        Assert.Equal(new HashSet<(Guid, Guid)> { (root.Id, WeekId), (root.Id, TaskId) }, Edges(hold).ToHashSet());

        var collection = Value<NotionGraph>(await teacher.Graph(root.Id, default));
        Assert.False(collection.WholeHold);
        Assert.Equal(root.Id, Assert.Single(collection.Nodes).Id);
        Assert.Empty(collection.Edges);
        var week = Value<NotionGraph>(await teacher.Graph(WeekId, default));
        Assert.Equal(new HashSet<Guid> { WeekId, TaskId }, week.Nodes.Select(n => n.Id).ToHashSet());
        Assert.Empty(week.Edges);
        Assert.DoesNotContain(week.Nodes, n => n.Id == root.Id);

        var student = Value<NotionGraph>(await As(this.student).HoldGraph(holdId, default));
        Assert.False(student.CanRename);
        Assert.True(student.WholeHold);
        Assert.Equal("H3 spil", student.Title);
        Assert.Equal(new HashSet<Guid> { root.Id, publishedChild }, student.Nodes.Select(n => n.Id).ToHashSet());
        Assert.Equal("Udgivet H3", student.Nodes.Single(n => n.Id == root.Id).Title);
        Assert.Equal("Udgivet barn", student.Nodes.Single(n => n.Id == publishedChild).Title);
        Assert.Empty(student.Edges);

        await SetDocument(root.Id, GraphFixtures.Get("basic"), GraphFixtures.Get("basic"), "Udgivet H3");
        await SetDocument(WeekId, GraphFixtures.Get("empty"), GraphFixtures.Get("empty"), "Uge 1");
        await SetDocument(TaskId, GraphFixtures.Get("empty"), GraphFixtures.Get("empty"), "Opgave");
        var published = Value<NotionGraph>(await As(this.student).HoldGraph(holdId, default));
        Assert.Equal(
            new HashSet<Guid> { root.Id, WeekId, TaskId, publishedChild },
            published.Nodes.Select(n => n.Id).ToHashSet());
        Assert.DoesNotContain(published.Nodes, n => n.Id == OutsideId || n.Id == noteId || n.Id == draftRoot);
        Assert.Equal(new HashSet<(Guid, Guid)> { (root.Id, WeekId), (root.Id, TaskId) }, Edges(published).ToHashSet());
        var preview = Value<NotionGraph>(await As(this.teacher, true).HoldGraph(holdId, default, publishedOnly: true));
        Assert.False(preview.CanRename);
        Assert.Equal(
            published.Nodes.OrderBy(n => n.Id).Select(n => (n.Id, n.Title)),
            preview.Nodes.OrderBy(n => n.Id).Select(n => (n.Id, n.Title)));
        Assert.Equal(Edges(published), Edges(preview));

        Assert.IsType<NotFoundResult>(await As(outsider, true).HoldGraph(holdId, default));
        Assert.IsType<NotFoundResult>(await As(this.student).HoldGraph(otherHoldId, default));
        Assert.IsType<NotFoundResult>(await As(this.teacher, true).HoldGraph(Guid.NewGuid(), default));
    }

    private static readonly Guid WeekId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid TaskId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid OutsideId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");

    private async Task AddPage(Guid id, string title, Guid? parent)
    {
        db.NotionPages.Add(new NotionPage
        {
            Id = id, HoldId = holdId, ParentPageId = parent, Title = title, CreatedBySub = teacher,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task SetDocument(Guid id, byte[]? draft, byte[]? published = null, string? publishedTitle = null)
    {
        var page = await db.NotionPages.SingleAsync(p => p.Id == id);
        page.YjsState = draft;
        if (published is not null)
        {
            page.PublishedState = published;
            page.PublishedTitle = publishedTitle ?? page.Title;
            page.PublishedAtUtc = DateTimeOffset.UtcNow;
            page.PublishedRevision = 1;
        }
        await db.SaveChangesAsync();
    }

    private static (Guid From, Guid To)[] Edges(NotionGraph graph) => graph.Edges.Select(e => (e.FromId, e.ToId)).ToArray();

    private sealed class ReadQueries : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
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
