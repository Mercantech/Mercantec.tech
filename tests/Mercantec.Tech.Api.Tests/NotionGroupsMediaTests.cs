using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotionGroupsMediaTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), student = Guid.NewGuid(), outsider = Guid.NewGuid();
    private Guid holdId, otherHoldId, studentId, shadowId, otherStudentId, pageId;
    private readonly MemoryStorage storage = new();
    private readonly EphemeralDataProtectionProvider protection = new();

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var hold = new Hold { Id = Guid.NewGuid(), Name = "Hold A", Forloeb = new() { Id = Guid.NewGuid(), Name = "Forløb A", CreatedBySub = teacher } };
        var other = new Hold { Id = Guid.NewGuid(), Name = "Hold B", Forloeb = new() { Id = Guid.NewGuid(), Name = "Forløb B", CreatedBySub = outsider } };
        holdId = hold.Id; otherHoldId = other.Id;
        studentId = Guid.NewGuid(); shadowId = Guid.NewGuid(); otherStudentId = Guid.NewGuid(); pageId = Guid.NewGuid();
        db.Hold.AddRange(hold, other);
        db.Memberships.AddRange(
            new() { Id = studentId, Hold = hold, UserSub = student, DisplayName = "Anna" },
            new() { Id = shadowId, Hold = hold, LectioId = "shadow", DisplayName = "Bo" },
            new() { Id = otherStudentId, Hold = other, UserSub = outsider, DisplayName = "Carl" });
        db.NotionPages.Add(new() { Id = pageId, HoldId = holdId, Title = "Kladde", CreatedBySub = teacher });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private ClaimsPrincipal User(Guid sub, bool isTeacher = false) => new(new ClaimsIdentity(
        isTeacher ? [new Claim("sub", sub.ToString()), new Claim(ClaimTypes.Role, "Underviser")] : [new Claim("sub", sub.ToString())], "test"));
    private NotionGroupsController Groups(Guid sub, bool isTeacher = false) => new(db, new(db))
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = User(sub, isTeacher) } } };
    private NotionAssetsController Media(Guid? sub, bool isTeacher = false) => new(db, new(db), storage, protection)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = sub is Guid id ? User(id, isTeacher) : new() } } };

    [Fact]
    public async Task GroupsPersistShadowMembershipsAndValidateClassroomBoundary()
    {
        var api = Groups(teacher, true);
        var group = Value<NotionGroupView>(await api.Create(holdId, new("Projekt 1", [studentId, shadowId]), default));
        Assert.Equal(2, group.Members.Length);
        var read = Assert.Single(Value<NotionGroupView[]>(await api.Groups(holdId, default)));
        Assert.Equal(group.Id, read.Id);
        Assert.Contains(read.Members, m => m.Id == shadowId);
        Assert.IsType<BadRequestObjectResult>(await api.Create(holdId, new("Forkert hold", [otherStudentId]), default));
        Assert.IsType<BadRequestObjectResult>(await api.Create(holdId, new("Dublet", [studentId, studentId]), default));
        Assert.IsType<NotFoundResult>(await Groups(outsider, true).Groups(holdId, default));
        Assert.IsType<NotFoundResult>(await Groups(student).Create(holdId, new("Elev", [studentId]), default));
    }

    [Fact]
    public async Task GroupUpdatesRejectStaleVersionsAndRosterShowsSavedGroups()
    {
        var api = Groups(teacher, true);
        var group = Value<NotionGroupView>(await api.Create(holdId, new("Projekt", [studentId]), default));
        var update = Value<NotionGroupView>(await api.Update(holdId, group.Id, new("Projekt nyt", [studentId, shadowId], group.Version), default));
        Assert.Equal(1, update.Version);
        Assert.Equal(2, update.Members.Length);
        Assert.IsType<ConflictObjectResult>(await api.Update(holdId, group.Id, new("Gammel", [studentId], group.Version), default));
        var roster = Value<NotionHoldMember[]>(await api.Members(holdId, default));
        Assert.Contains(group.Id, roster.Single(m => m.Id == shadowId).GroupIds);
        var profile = Value<NotionStudentProfile>(await api.Member(holdId, studentId, default));
        Assert.Equal(group.Id, Assert.Single(profile.Groups).Id);
        Assert.IsType<NotFoundResult>(await api.Member(holdId, otherStudentId, default));
    }

    [Fact]
    public async Task StudentsOnlySeeGroupsTheyBelongTo()
    {
        var api = Groups(teacher, true);
        var own = Value<NotionGroupView>(await api.Create(holdId, new("Fælles", [studentId, shadowId]), default));
        await api.Create(holdId, new("Anden gruppe", [shadowId]), default);
        var visible = Value<NotionGroupView[]>(await Groups(student).Groups(holdId, default));
        Assert.Equal(own.Id, Assert.Single(visible).Id);
        var profile = Value<NotionStudentProfile>(await Groups(student).Member(holdId, shadowId, default));
        Assert.Equal(own.Id, Assert.Single(profile.Groups).Id);
    }

    [Fact]
    public async Task RouletteBatchPreservesPreviousGroupsAndUsesSameGroupsAsQuests()
    {
        var api = Groups(teacher, true);
        var original = Value<NotionGroupView>(await api.Create(holdId, new("Tidligere gruppe", [studentId, shadowId]), default));
        var added = Value<NotionGroupView[]>(await api.CreateBatch(holdId,
            new([new("Runde 2 A", [studentId]), new("Runde 2 B", [shadowId])]), default));
        Assert.Equal(2, added.Length);
        var groups = Value<NotionGroupView[]>(await api.Groups(holdId, default));
        Assert.Equal(3, groups.Length);
        Assert.Contains(groups, g => g.Id == original.Id && g.Members.Length == 2);
        Assert.Equal(2, Value<NotionHoldMember[]>(await api.Members(holdId, default)).Single(m => m.Id == studentId).GroupIds.Length);
        var learning = new GroupsController(db, new(db)) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = User(teacher, true) } } };
        var submissionGroups = Value<LearningGroupView[]>(await learning.List(holdId, default));
        Assert.All(added, group => Assert.Contains(submissionGroups, g => g.Id == group.Id));
    }

    [Fact]
    public async Task InvalidRouletteBatchCreatesNothingAndEnforcesHoldAccess()
    {
        var api = Groups(teacher, true);
        Assert.IsType<BadRequestObjectResult>(await api.CreateBatch(holdId,
            new([new("Gyldig", [studentId]), new("Udenfor holdet", [otherStudentId])]), default));
        Assert.Empty(await db.LearningGroups.ToArrayAsync());
        Assert.IsType<BadRequestObjectResult>(await api.CreateBatch(holdId,
            new([new("A", [studentId]), new("B", [studentId])]), default));
        Assert.IsType<BadRequestObjectResult>(await api.CreateBatch(holdId,
            new([new("Samme", [studentId]), new(" samme ", [shadowId])]), default));
        Assert.IsType<BadRequestObjectResult>(await api.CreateBatch(holdId, new([]), default));
        Assert.IsType<NotFoundResult>(await Groups(student).CreateBatch(holdId, new([new("A", [studentId])]), default));
        Assert.IsType<NotFoundResult>(await Groups(outsider, true).CreateBatch(holdId, new([new("A", [studentId])]), default));
        Assert.Empty(await db.LearningGroups.ToArrayAsync());
    }

    private static byte[] Mp4() => [0, 0, 0, 20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 0, 0, 1, 2, 3, 4];
    private static FormFile Form(byte[] bytes, string name, string contentType = "application/octet-stream") => new(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        { Headers = new HeaderDictionary(), ContentType = contentType };
    private async Task<NotionAssetView> Upload() => Value<NotionAssetView>(await Media(teacher, true).Upload(pageId, Form(Mp4(), "lektion.mp4", "text/html"), default));

    [Fact]
    public async Task UploadsUsePrivateNamespaceVerifiedMimeAndTeacherScope()
    {
        var result = await Upload();
        Assert.Equal("video/mp4", result.ContentType);
        Assert.Equal("video", result.Kind);
        Assert.StartsWith("notion-private/", Assert.Single(storage.Objects).Key);
        Assert.IsType<NotFoundResult>(await Media(student).Upload(pageId, Form(Mp4(), "lektion.mp4"), default));
        Assert.IsType<NotFoundResult>(await Media(outsider, true).Upload(pageId, Form(Mp4(), "lektion.mp4"), default));
        Assert.IsType<BadRequestObjectResult>(await Media(teacher, true).Upload(pageId, Form(Encoding.UTF8.GetBytes("<script>bad</script>"), "bad.png"), default));
        Assert.IsType<BadRequestObjectResult>(await Media(teacher, true).Upload(pageId, Form(Mp4(), "bad.html"), default));
        Assert.Single(storage.Objects);
        var old = new FilesController(storage, db, protection) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = User(student) } } };
        Assert.IsType<NotFoundResult>(await old.Download(Assert.Single(storage.Objects).Key, default));
        Assert.IsType<NotFoundResult>(await old.Download("./" + Assert.Single(storage.Objects).Key, default));
    }

    [Fact]
    public async Task MediaTicketsRequirePublicationAndRecheckItOnEveryRead()
    {
        var asset = await Upload();
        Assert.IsType<NotFoundResult>(await Media(student).Access(asset.Id, default));
        var page = await db.NotionPages.SingleAsync(p => p.Id == pageId);
        page.PublishedAtUtc = DateTimeOffset.UtcNow;
        page.PublishedAssetIdsJson = JsonSerializer.Serialize(new[] { asset.Id });
        await db.SaveChangesAsync();
        var access = Value<NotionAssetAccess>(await Media(student).Access(asset.Id, default));
        Assert.DoesNotContain("notion-private", access.Url);
        Assert.IsType<NotFoundResult>(await Media(outsider, true).Access(asset.Id, default));
        var ticket = Uri.UnescapeDataString(access.Url.Split("ticket=")[1]);
        var content = Media(null);
        Assert.IsType<FileStreamResult>(await content.Content(asset.Id, default, ticket));
        Assert.Equal("private, no-store", content.Response.Headers.CacheControl.ToString());
        page.PublishedAtUtc = null;
        await db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await Media(null).Content(asset.Id, default, ticket));
        page.PublishedAtUtc = DateTimeOffset.UtcNow; page.PublicationVersion++;
        await db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await Media(null).Content(asset.Id, default, ticket));
        Assert.IsType<OkObjectResult>(await Media(student).Access(asset.Id, default));
        Assert.IsType<UnauthorizedResult>(await Media(null).Content(asset.Id, default, "invalid"));
    }

    [Fact]
    public async Task MediaTicketsAreBoundToCurrentMembershipAndSpecificAsset()
    {
        var asset = await Upload();
        var page = await db.NotionPages.SingleAsync(p => p.Id == pageId);
        page.PublishedAtUtc = DateTimeOffset.UtcNow; page.PublishedAssetIdsJson = JsonSerializer.Serialize(new[] { asset.Id });
        await db.SaveChangesAsync();
        var ticket = Uri.UnescapeDataString(Value<NotionAssetAccess>(await Media(student).Access(asset.Id, default)).Url.Split("ticket=")[1]);
        Assert.IsType<UnauthorizedResult>(await Media(null).Content(Guid.NewGuid(), default, ticket));
        db.Memberships.Remove(await db.Memberships.SingleAsync(m => m.Id == studentId));
        await db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await Media(null).Content(asset.Id, default, ticket));
    }

    [Fact]
    public async Task VideoSeekingStreamsOnlyRequestedBytesAndRejectsInvalidRanges()
    {
        var asset = await Upload();
        var api = Media(teacher, true);
        api.Request.Headers.Range = "bytes=4-7";
        var result = Assert.IsType<FileStreamResult>(await api.Content(asset.Id, default));
        using var reader = new StreamReader(result.FileStream);
        Assert.Equal("ftyp", await reader.ReadToEndAsync());
        Assert.Equal((4L, 7L), storage.LastRange);
        Assert.Equal(206, api.Response.StatusCode);
        Assert.Equal($"bytes 4-7/{asset.SizeBytes}", api.Response.Headers.ContentRange.ToString());
        var invalid = Media(teacher, true); invalid.Request.Headers.Range = "bytes=100-200";
        Assert.Equal(416, Assert.IsType<StatusCodeResult>(await invalid.Content(asset.Id, default)).StatusCode);
        var staleIfRange = Media(teacher, true); staleIfRange.Request.Headers.Range = "bytes=4-7";
        staleIfRange.Request.Headers["If-Range"] = "\"old-version\"";
        Assert.IsType<FileStreamResult>(await staleIfRange.Content(asset.Id, default));
        Assert.Equal(200, staleIfRange.Response.StatusCode);
        Assert.Equal(asset.SizeBytes, staleIfRange.Response.ContentLength);
        var validIfRange = Media(teacher, true); validIfRange.Request.Headers.Range = "bytes=4-7";
        validIfRange.Request.Headers["If-Range"] = $"\"notion-{asset.Id:N}\"";
        Assert.IsType<FileStreamResult>(await validIfRange.Content(asset.Id, default));
        Assert.Equal(206, validIfRange.Response.StatusCode);
        var head = Media(teacher, true); head.Request.Method = "HEAD"; head.Request.Headers.Range = "bytes=4-7";
        Assert.IsType<EmptyResult>(await head.Content(asset.Id, default));
        Assert.Equal(200, head.Response.StatusCode);
        Assert.Equal(asset.SizeBytes, head.Response.ContentLength);
        // Run the MVC file executor too: it must preserve our upstream range status and exact partial body.
        var streaming = Media(teacher, true); streaming.Request.Method = "GET"; streaming.Request.Headers.Range = "bytes=4-7";
        var streamedResult = await streaming.Content(asset.Id, default);
        var services = new ServiceCollection(); services.AddLogging(); services.AddControllers();
        await using var provider = services.BuildServiceProvider();
        streaming.HttpContext.RequestServices = provider;
        using var body = new MemoryStream(); streaming.Response.Body = body;
        await streamedResult.ExecuteResultAsync(new ActionContext(streaming.HttpContext, new RouteData(), new ActionDescriptor()));
        Assert.Equal(206, streaming.Response.StatusCode);
        Assert.Equal(4, streaming.Response.ContentLength);
        Assert.Equal("ftyp", Encoding.ASCII.GetString(body.ToArray()));
    }

    [Theory]
    [InlineData("bytes=0-", 10, true, 0, 9)]
    [InlineData("bytes=-3", 10, true, 7, 9)]
    [InlineData("bytes=7-100", 10, true, 7, 9)]
    [InlineData("bytes=0-1,4-5", 10, false, 0, 0)]
    [InlineData("bytes=-0", 10, false, 0, 0)]
    [InlineData("items=0-1", 10, false, 0, 0)]
    public void RangeParserHandlesBrowserRequests(string header, long size, bool valid, long start, long end)
    {
        Assert.Equal(valid, NotionAssetsController.TryRange(header, size, out var actualStart, out var actualEnd));
        if (valid) { Assert.Equal(start, actualStart); Assert.Equal(end, actualEnd); }
    }

    private sealed class MemoryStorage : IObjectStorage
    {
        public bool IsConfigured => true;
        public bool SupportsRanges => true;
        public string Bucket => "test";
        public Dictionary<string, byte[]> Objects { get; } = [];
        public (long, long)? LastRange { get; private set; }
        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        { using var copy = new MemoryStream(); await content.CopyToAsync(copy, cancellationToken); Objects[key] = copy.ToArray(); return new(key, Bucket, contentType, copy.Length); }
        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<ObjectGetResult?>(
            Objects.TryGetValue(key, out var bytes) ? new(new MemoryStream(bytes), "video/mp4", bytes.Length) : null);
        public Task<ObjectGetResult?> GetRangeAsync(string key, long start, long end, CancellationToken cancellationToken = default)
        { LastRange = (start, end); var bytes = Objects[key].AsSpan((int)start, (int)(end - start + 1)).ToArray(); return Task.FromResult<ObjectGetResult?>(new(new MemoryStream(bytes), "video/mp4", bytes.Length)); }
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { Objects.Remove(key); return Task.CompletedTask; }
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<Uri?>(null);
    }
}
