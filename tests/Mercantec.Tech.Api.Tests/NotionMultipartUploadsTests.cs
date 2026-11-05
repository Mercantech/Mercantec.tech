using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotionMultipartUploadsTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), colleague = Guid.NewGuid(), student = Guid.NewGuid(), outsider = Guid.NewGuid();
    private readonly Guid holdId = Guid.NewGuid(), pageId = Guid.NewGuid(), otherPageId = Guid.NewGuid();
    private readonly MultipartStorage storage = new();

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = NewDb(); await db.Database.EnsureCreatedAsync();
        db.Hold.Add(new() { Id = holdId, Name = "Hold", Forloeb = new() { Id = Guid.NewGuid(), Name = "Forløb", CreatedBySub = teacher } });
        db.Memberships.AddRange(new Membership { Id = Guid.NewGuid(), HoldId = holdId, UserSub = colleague },
            new Membership { Id = Guid.NewGuid(), HoldId = holdId, UserSub = student });
        db.NotionPages.AddRange(new NotionPage { Id = pageId, HoldId = holdId, Title = "A", CreatedBySub = teacher },
            new NotionPage { Id = otherPageId, HoldId = holdId, Title = "B", CreatedBySub = teacher });
        await db.SaveChangesAsync();
    }
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private NotionAssetUploadsController Api(Guid? sub = null, bool teacherRole = true, AppDbContext? context = null) => new(context ?? db, new(context ?? db), storage)
    {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(
            teacherRole ? [new Claim("sub", (sub ?? teacher).ToString()), new Claim(ClaimTypes.Role, "Underviser")]
                : [new Claim("sub", (sub ?? teacher).ToString())], "test")) } }
    };
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
    private async Task<NotionAssetUploadsController.UploadCreated> Create(long size = 20, string name = "lektion.mp4") =>
        Value<NotionAssetUploadsController.UploadCreated>(await Api().Create(pageId, new(name, size, "text/html"), default));
    private async Task<IActionResult> Part(Guid id, int number, long length, byte fill = 0, bool validHeader = true)
    {
        var api = Api(); api.Request.ContentLength = length;
        await using var input = new GeneratedFile(length, number == 1 && validHeader, fill);
        api.Request.Body = input;
        return await api.Part(pageId, id, number, default);
    }

    [Fact]
    public async Task UploadAboveOldLimitStreamsPartsAndCompletesIdempotently()
    {
        const long bytes = 111L * 1024 * 1024 + 3;
        var upload = await Create(bytes);
        var count = (int)((bytes + upload.ChunkSizeBytes - 1) / upload.ChunkSizeBytes);
        for (var number = 1; number <= count; number++)
            Assert.IsType<OkObjectResult>(await Part(upload.Id, number, Math.Min(upload.ChunkSizeBytes, bytes - (long)(number - 1) * upload.ChunkSizeBytes)));
        var asset = Value<NotionAssetView>(await Api().Complete(pageId, upload.Id, default));
        Assert.Equal(bytes, asset.SizeBytes); Assert.Equal("video/mp4", asset.ContentType);
        Assert.Equal(bytes, storage.Objects.Values.Single());
        Assert.Equal(count, storage.PartCalls); Assert.True(storage.MaxPartBytes <= 8 * 1024 * 1024);
        Assert.Equal(asset, Value<NotionAssetView>(await Api().Complete(pageId, upload.Id, default)));
        Assert.Equal(1, storage.CompleteCalls); Assert.Single(await db.NotionAssets.ToArrayAsync());
        Assert.StartsWith("notion-private/", (await db.NotionAssets.SingleAsync()).Key);
    }

    [Fact]
    public async Task EveryStageChecksHoldPageAndOriginalUploader()
    {
        var upload = await Create();
        Assert.IsType<NotFoundResult>(await Api(student, false).Create(pageId, new("a.mp4", 20), default));
        Assert.IsType<NotFoundResult>(await Api(outsider).Create(pageId, new("a.mp4", 20), default));
        foreach (var api in new[] { Api(student, false), Api(colleague), Api(outsider) })
        {
            Assert.IsType<NotFoundResult>(await api.Part(pageId, upload.Id, 1, default));
            Assert.IsType<NotFoundResult>(await api.Complete(pageId, upload.Id, default));
            Assert.IsType<NotFoundResult>(await api.Abort(pageId, upload.Id, default));
        }
        Assert.IsType<NotFoundResult>(await Api().Complete(otherPageId, upload.Id, default));
        Assert.IsType<NotFoundResult>(await Api().Abort(otherPageId, upload.Id, default));
        Assert.Equal(0, storage.PartCalls); Assert.Equal(0, storage.AbortCalls);
    }

    [Fact]
    public async Task PartRetriesAreContentBoundAndMalformedRequestsCannotFinish()
    {
        var upload = await Create();
        Assert.IsType<BadRequestObjectResult>(await Part(upload.Id, 1, 20, validHeader: false));
        Assert.IsType<BadRequestObjectResult>(await Part(upload.Id, 2, 20));
        Assert.IsType<BadRequestObjectResult>(await Part(upload.Id, 1, 19));
        Assert.IsType<BadRequestObjectResult>(await Api().Complete(pageId, upload.Id, default));
        Assert.IsType<OkObjectResult>(await Part(upload.Id, 1, 20));
        Assert.IsType<OkObjectResult>(await Part(upload.Id, 1, 20));
        Assert.IsType<ConflictObjectResult>(await Part(upload.Id, 1, 20, 1));
        Assert.Equal(1, storage.PartCalls);
        Assert.Empty(await db.NotionAssets.ToArrayAsync());
    }

    [Fact]
    public async Task ActualBodyLengthIsCheckedEvenWithoutContentLength()
    {
        var upload = await Create();
        foreach (var bytes in new[] { 19, 21 })
        {
            var api = Api(); api.Request.Body = new GeneratedFile(bytes, true);
            Assert.IsType<BadRequestObjectResult>(await api.Part(pageId, upload.Id, 1, default));
        }
        Assert.Equal(0, storage.PartCalls);
    }

    [Fact]
    public async Task CompleteRecoversS3SuccessWithLostResponse()
    {
        var upload = await Create(); await Part(upload.Id, 1, 20);
        storage.ThrowAfterComplete = true;
        var asset = Value<NotionAssetView>(await Api().Complete(pageId, upload.Id, default));
        Assert.Equal(20, asset.SizeBytes); Assert.Single(await db.NotionAssets.ToArrayAsync());
    }

    [Fact]
    public async Task FreshApiRecoversCrashAfterS3CompleteBeforeAssetRegistration()
    {
        var upload = await Create(); await Part(upload.Id, 1, 20);
        var row = await db.NotionAssetUploads.SingleAsync(x => x.Id == upload.Id);
        await storage.CompleteMultipartAsync(row.Key, row.StorageUploadId,
            NotionUploadSessions.Parts(row).Select(p => new ObjectUploadPart(p.PartNumber, p.ETag)).ToArray());
        await using var restartedDb = NewDb();
        var asset = Value<NotionAssetView>(await Api(context: restartedDb).Complete(pageId, upload.Id, default));
        Assert.Equal(20, asset.SizeBytes); Assert.Single(await restartedDb.NotionAssets.ToArrayAsync());
    }

    [Fact]
    public async Task RevokedHoldAccessIsRecheckedAfterUploadCreation()
    {
        var upload = Value<NotionAssetUploadsController.UploadCreated>(await Api(colleague).Create(pageId, new("video.mp4", 20), default));
        db.Memberships.Remove(await db.Memberships.SingleAsync(m => m.UserSub == colleague)); await db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await Api(colleague).Part(pageId, upload.Id, 1, default));
        Assert.IsType<NotFoundResult>(await Api(colleague).Complete(pageId, upload.Id, default));
        Assert.IsType<NotFoundResult>(await Api(colleague).Abort(pageId, upload.Id, default));
        Assert.Equal(0, storage.PartCalls);
    }

    [Fact]
    public async Task ExpiryAbortsPartsAndUnregisteredCompletedObjectsButKeepsRegisteredAssets()
    {
        var abandoned = await Create(); await Part(abandoned.Id, 1, 20);
        var completed = await Create(); await Part(completed.Id, 1, 20); await Api().Complete(pageId, completed.Id, default);
        var unknownComplete = await Create(); await Part(unknownComplete.Id, 1, 20);
        var pending = await db.NotionAssetUploads.SingleAsync(x => x.Id == unknownComplete.Id);
        storage.Objects[pending.Key] = 20; // API crashed after S3 complete but before DB asset registration.
        foreach (var row in await db.NotionAssetUploads.ToArrayAsync()) row.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();
        await NotionUploadCleanup.Sweep(db, storage, default);
        Assert.Empty(await db.NotionAssetUploads.ToArrayAsync());
        Assert.Single(storage.Objects); Assert.Single(await db.NotionAssets.ToArrayAsync());
        Assert.Contains((await db.NotionAssets.SingleAsync()).Key, storage.Objects.Keys);
        Assert.Equal(2, storage.AbortCalls);
    }

    [Fact]
    public async Task RevisionLeasePreventsTwoReplicasFromChangingManifest()
    {
        var created = await Create();
        await using var first = NewDb(); await using var second = NewDb();
        var a = await first.NotionAssetUploads.SingleAsync(x => x.Id == created.Id);
        var b = await second.NotionAssetUploads.SingleAsync(x => x.Id == created.Id);
        Assert.True(await NotionUploadSessions.TryLease(first, a, default));
        Assert.False(await NotionUploadSessions.TryLease(second, b, default));
        await NotionUploadSessions.Release(first, a);
        await second.Entry(b).ReloadAsync();
        Assert.True(await NotionUploadSessions.TryLease(second, b, default));
    }

    [Fact]
    public async Task CancelDuringLeasedOperationPersistsIntentAndCleanupHonorsIt()
    {
        var created = await Create();
        await using var activeDb = NewDb();
        var active = await activeDb.NotionAssetUploads.SingleAsync(x => x.Id == created.Id);
        Assert.True(await NotionUploadSessions.TryLease(activeDb, active, default));
        await using var cancelDb = NewDb();
        Assert.IsType<NoContentResult>(await Api(context: cancelDb).Abort(pageId, created.Id, default));
        Assert.True(await cancelDb.NotionAssetUploads.AsNoTracking().Select(x => x.AbortRequested).SingleAsync());
        await NotionUploadSessions.Release(activeDb, active);
        db.ChangeTracker.Clear();
        await NotionUploadCleanup.Sweep(db, storage, default);
        Assert.Empty(await db.NotionAssetUploads.ToArrayAsync()); Assert.Equal(1, storage.AbortCalls);
    }

    [Fact]
    public async Task AbortIsIdempotentAndDoesNotDeleteACompletedAsset()
    {
        var created = await Create(); await Part(created.Id, 1, 20);
        Assert.IsType<NoContentResult>(await Api().Abort(pageId, created.Id, default));
        Assert.IsType<NoContentResult>(await Api().Abort(pageId, created.Id, default));
        Assert.IsType<ConflictObjectResult>(await Api().Complete(pageId, created.Id, default));
        var second = await Create(); await Part(second.Id, 1, 20); await Api().Complete(pageId, second.Id, default);
        Assert.IsType<NoContentResult>(await Api().Abort(pageId, second.Id, default));
        Assert.Single(storage.Objects); Assert.Single(await db.NotionAssets.ToArrayAsync());
    }

    [Fact]
    public void AdaptiveChunksFollowStoragePartCountWithoutOldProductLimit()
    {
        const long size = 100L * 1024 * 1024 * 1024;
        var chunk = NotionUploadSessions.ChunkSize(size);
        Assert.InRange(chunk, NotionUploadSessions.DefaultChunkBytes, NotionUploadSessions.MaxChunkBytes);
        Assert.InRange(1 + (size - 1) / chunk, 1, NotionUploadSessions.MaxParts);
    }

    private sealed class GeneratedFile(long length, bool mp4, byte fill = 0) : Stream
    {
        private long position;
        private static readonly byte[] Header = [0, 0, 0, 20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m'];
        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, length - position); buffer.AsSpan(offset, n).Fill(fill);
            if (mp4 && position < Header.Length) Header.AsSpan((int)position, (int)Math.Min(n, Header.Length - position)).CopyTo(buffer.AsSpan(offset));
            position += n; return n;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var n = (int)Math.Min(buffer.Length, length - position); buffer.Span[..n].Fill(fill);
            if (mp4 && position < Header.Length) Header.AsSpan((int)position, (int)Math.Min(n, Header.Length - position)).CopyTo(buffer.Span);
            position += n; return ValueTask.FromResult(n);
        }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => length; public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class MultipartStorage : IObjectStorage
    {
        public bool IsConfigured => true; public bool SupportsMultipart => true; public string Bucket => "test";
        public Dictionary<string, long> Objects { get; } = [];
        private readonly Dictionary<string, Dictionary<int, (string ETag, long Size)>> uploads = [];
        public int PartCalls, CompleteCalls, AbortCalls; public long MaxPartBytes; public bool ThrowAfterComplete;
        public Task<string> InitiateMultipartAsync(string key, string contentType, CancellationToken cancellationToken = default)
        { var id = Guid.NewGuid().ToString(); uploads[id] = []; return Task.FromResult(id); }
        public async Task<string> UploadPartAsync(string key, string uploadId, int number, Stream content, CancellationToken cancellationToken = default)
        {
            PartCalls++; await content.CopyToAsync(Stream.Null, cancellationToken); var etag = Guid.NewGuid().ToString();
            uploads[uploadId][number] = (etag, content.Length); MaxPartBytes = Math.Max(MaxPartBytes, content.Length); return etag;
        }
        public Task CompleteMultipartAsync(string key, string uploadId, IReadOnlyList<ObjectUploadPart> parts, CancellationToken cancellationToken = default)
        {
            CompleteCalls++; var uploaded = uploads[uploadId];
            foreach (var p in parts) Assert.Equal(uploaded[p.PartNumber].ETag, p.ETag);
            Objects[key] = parts.Sum(p => uploaded[p.PartNumber].Size); uploads.Remove(uploadId);
            if (ThrowAfterComplete) throw new IOException("Connection lost after S3 accepted complete");
            return Task.CompletedTask;
        }
        public Task AbortMultipartAsync(string key, string uploadId, CancellationToken cancellationToken = default)
        { AbortCalls++; uploads.Remove(uploadId); return Task.CompletedTask; }
        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<ObjectGetResult?>(
            Objects.TryGetValue(key, out var size) ? new(Stream.Null, "video/mp4", size) : null);
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { Objects.Remove(key); return Task.CompletedTask; }
        public Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<Uri?>(null);
    }
}
