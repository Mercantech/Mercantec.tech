using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotionControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), outsider = Guid.NewGuid();
    private Guid holdId, otherHoldId, shadowId;
    private const string Secret = "test-service-secret-with-at-least-32-characters";

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

    private NotionController As(Guid sub, bool teacherRole = false)
    {
        var claims = new List<Claim> { new("sub", sub.ToString()) };
        if (teacherRole) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(db, new(db))
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
                { User = new(new ClaimsIdentity(claims, "test")) } }
        };
    }
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private async Task<NotionPageSummary> Create() => Value<NotionPageSummary>(
        await As(teacher, true).Create(new(holdId, "Introduktion"), default));
    private NotionStorageController Storage(string? configured = Secret, string? supplied = Secret, byte[]? body = null, string? secretFile = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Notion:ServiceSecret"] = configured, ["Notion:ServiceSecretFile"] = secretFile }).Build();
        var context = new DefaultHttpContext();
        if (supplied is not null) context.Request.Headers["X-Notion-Service-Key"] = supplied;
        context.Request.ContentType = "application/octet-stream";
        context.Request.Body = new MemoryStream(body ?? []);
        return new(db, config) { ControllerContext = new() { HttpContext = context } };
    }

    [Fact]
    public async Task HoldsRequireLoginAndOnlyListManageableHolds()
    {
        var controller = As(teacher, true);
        var holds = Value<List<NotionHold>>(await controller.Holds(default));
        Assert.Equal(holdId, Assert.Single(holds).Id);
        Assert.Empty(Value<List<NotionHold>>(await As(student).Holds(default)));
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
        Assert.IsType<NotFoundResult>(await unauthorized.List(holdId, default));
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
}
