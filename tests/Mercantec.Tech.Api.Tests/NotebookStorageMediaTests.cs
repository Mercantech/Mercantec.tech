using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotebookStorageMediaTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid pupil = Guid.NewGuid(), peer = Guid.NewGuid(), teacher = Guid.NewGuid();
    private Guid notebookId, pageId, otherPageId;
    private readonly MemoryStorage storage = new();
    private readonly EphemeralDataProtectionProvider protection = new();
    private const string Secret = "test-notebook-service-key-at-least-32-characters";
    private static readonly IConfiguration Configuration = new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Notion:ServiceSecret"] = Secret }).Build();

    public async Task InitializeAsync()
    {
        await connection.OpenAsync(); db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(); notebookId = Guid.NewGuid(); pageId = Guid.NewGuid(); otherPageId = Guid.NewGuid();
        db.Notebooks.Add(new() { Id = notebookId, OwnerSub = pupil, CreatedAtUtc = DateTimeOffset.UtcNow });
        db.NotebookDocuments.AddRange(new NotebookDocument { Id = pageId, NotebookId = notebookId, Title = "Mine noter" },
            new NotebookDocument { Id = otherPageId, NotebookId = notebookId, Title = "Anden side" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private ClaimsPrincipal User(Guid sub, bool isTeacher = false) => new(new ClaimsIdentity(isTeacher
        ? [new Claim("sub", sub.ToString()), new Claim("role", "Underviser")]
        : [new Claim("sub", sub.ToString())], "test"));
    private NotebookAssetsController Media(Guid? sub, bool isTeacher = false) => new(db, new(db), storage, protection)
    { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = sub is Guid id ? User(id, isTeacher) : new() } } };
    private NotebookStorageController Store(string? key = Secret) => new(db, new(Configuration))
    { ControllerContext = new() { HttpContext = Context(key) } };
    private static DefaultHttpContext Context(string? key)
    { var context = new DefaultHttpContext(); if (key is not null) context.Request.Headers["X-Notion-Service-Key"] = key; return context; }
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private static StoreNotebookDocument Projection(char fingerprint = 'a', Guid[]? links = null, Guid[]? assets = null) =>
        new(Convert.ToBase64String([0, 0]), new string(fingerprint, 64), "Async og await", links ?? [], assets ?? []);
    private async Task<NotionAssetView> Upload()
    {
        var bytes = Encoding.ASCII.GetBytes("0000ftypisom00000000");
        return Value<NotionAssetView>(await Media(pupil).Upload(pageId,
            new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "demo.mp4"), default));
    }

    [Fact]
    public async Task StorageRequiresServiceKeyAndCommitsBodySearchLinksAssetsAsOneRevision()
    {
        Assert.IsType<UnauthorizedResult>(await Store(null).WriteState(pageId, Projection(), default));
        Assert.IsType<UnauthorizedResult>(await Store("wrong").ReadState(pageId, default));
        Assert.IsType<BadRequestObjectResult>(await Store().WriteState(pageId, Projection() with { StateBase64 = "broken" }, default));
        var asset = await Upload();
        Assert.IsType<OkObjectResult>(await Store().WriteState(pageId, Projection(links: [otherPageId, otherPageId], assets: [asset.Id]), default));
        db.ChangeTracker.Clear();
        var saved = await db.NotebookDocuments.SingleAsync(d => d.Id == pageId);
        Assert.Equal(1, saved.Revision); Assert.Equal("Async og await", saved.SearchText); Assert.Contains(asset.Id.ToString(), saved.AssetIdsJson);
        Assert.Equal(otherPageId, (await db.NotebookLinks.SingleAsync()).ToId);
        await Store().WriteState(pageId, Projection(links: [otherPageId], assets: [asset.Id]), default);
        db.ChangeTracker.Clear(); Assert.Equal(1, (await db.NotebookDocuments.SingleAsync(d => d.Id == pageId)).Revision);
        await Store().WriteState(pageId, Projection('b') with { PlainText = "Efter sletning" }, default);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.NotebookLinks.ToArrayAsync());
        saved = await db.NotebookDocuments.SingleAsync(d => d.Id == pageId);
        Assert.Equal(2, saved.Revision); Assert.Equal("[]", saved.AssetIdsJson); Assert.Equal("Efter sletning", saved.SearchText);
        Assert.IsType<FileContentResult>(await Store().ReadState(pageId, default));
    }

    [Fact]
    public async Task ForeignAssetsCannotBeSmuggledIntoAnotherPageAndFailureKeepsExistingRevision()
    {
        var asset = await Upload();
        await Store().WriteState(otherPageId, Projection(), default);
        Assert.IsType<BadRequestObjectResult>(await Store().WriteState(otherPageId, Projection('b', assets: [asset.Id]), default));
        db.ChangeTracker.Clear();
        var other = await db.NotebookDocuments.SingleAsync(d => d.Id == otherPageId);
        Assert.Equal(1, other.Revision); Assert.Equal(new string('a', 64), other.Fingerprint);
    }

    [Fact]
    public async Task OnlyOwnerUploadsWhileTeachersAndExplicitReadersReadReferencedMedia()
    {
        var file = new FormFile(new MemoryStream([1, 2]), 0, 2, "file", "file.txt");
        Assert.IsType<NotFoundResult>(await Media(peer).Upload(pageId, file, default));
        Assert.IsType<NotFoundResult>(await Media(teacher, true).Upload(pageId, file, default));
        var asset = await Upload();
        Assert.IsType<OkObjectResult>(await Media(pupil).Access(asset.Id, default));
        Assert.IsType<NotFoundResult>(await Media(teacher, true).Access(asset.Id, default));
        await Store().WriteState(pageId, Projection(assets: [asset.Id]), default);
        Assert.IsType<OkObjectResult>(await Media(teacher, true).Access(asset.Id, default));
        Assert.IsType<NotFoundResult>(await Media(peer).Access(asset.Id, default));
        db.NotebookShares.Add(new() { Id = Guid.NewGuid(), NotebookId = notebookId, PageId = pageId,
            RecipientSub = peer, CreatedAtUtc = DateTimeOffset.UtcNow }); await db.SaveChangesAsync();
        var access = Value<NotionAssetAccess>(await Media(peer).Access(asset.Id, default));
        Assert.DoesNotContain("notebook-private", access.Url);
        var ticket = Uri.UnescapeDataString(access.Url.Split("ticket=")[1]);
        Assert.IsType<FileStreamResult>(await Media(null).Content(asset.Id, default, ticket));
        Assert.IsType<UnauthorizedResult>(await Media(null).Content(Guid.NewGuid(), default, ticket));
        await db.NotebookShares.ExecuteDeleteAsync();
        Assert.IsType<NotFoundResult>(await Media(null).Content(asset.Id, default, ticket));
    }

    [Fact]
    public async Task DeletedLiveReferenceRevokesOldMediaTicketsAndRangesRemainBounded()
    {
        var asset = await Upload(); await Store().WriteState(pageId, Projection(assets: [asset.Id]), default);
        var access = Value<NotionAssetAccess>(await Media(teacher, true).Access(asset.Id, default));
        var ticket = Uri.UnescapeDataString(access.Url.Split("ticket=")[1]);
        var stream = Media(null); stream.Request.Headers.Range = "bytes=4-7";
        var result = Assert.IsType<FileStreamResult>(await stream.Content(asset.Id, default, ticket));
        using var reader = new StreamReader(result.FileStream);
        Assert.Equal("ftyp", await reader.ReadToEndAsync()); Assert.Equal(206, stream.Response.StatusCode);
        Assert.Equal(4, stream.Response.ContentLength);
        await Store().WriteState(pageId, Projection('b'), default);
        Assert.IsType<NotFoundResult>(await Media(null).Content(asset.Id, default, ticket));
    }

    [Theory]
    [InlineData("notebook-private/abc")]
    [InlineData("/NOTEBOOK-PRIVATE/abc")]
    [InlineData("prefix\\notebook-private\\abc")]
    public void LegacyFileRouteRecognizesNotebookPrivateNamespace(string key) => Assert.True(NotionMediaPolicy.IsPrivateKey(key));

    private sealed class MemoryStorage : IObjectStorage
    {
        public bool IsConfigured => true;
        public bool SupportsRanges => true;
        public string Bucket => "test";
        private readonly Dictionary<string, byte[]> objects = [];
        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string type, CancellationToken cancellationToken = default)
        { using var copy = new MemoryStream(); await content.CopyToAsync(copy, cancellationToken); objects[key] = copy.ToArray(); return new(key, Bucket, type, copy.Length); }
        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<ObjectGetResult?>(objects.TryGetValue(key, out var bytes) ? new(new MemoryStream(bytes), "video/mp4", bytes.Length) : null);
        public Task<ObjectGetResult?> GetRangeAsync(string key, long start, long end, CancellationToken cancellationToken = default)
        { var bytes = objects[key].AsSpan((int)start, (int)(end - start + 1)).ToArray(); return Task.FromResult<ObjectGetResult?>(new(new MemoryStream(bytes), "video/mp4", bytes.Length)); }
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<Uri?>(null);
    }
}
