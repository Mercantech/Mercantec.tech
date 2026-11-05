using System.IO.Compression;
using System.Text;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Storage;

namespace Mercantec.Tech.Api.Tests;

public sealed class WebBuildServiceTests
{
    [Fact]
    public async Task StandaloneHtmlCanBePreparedFromNonSeekableObjectStorageRead()
    {
        var html = Encoding.UTF8.GetBytes("<!doctype html><button>Spil</button>");
        var asset = new FileAsset { Id = Guid.NewGuid(), Key = "original.html", OriginalName = "spil.html", SizeBytes = html.Length };
        var storage = new NonSeekableStorage(asset.Key, html);

        var manifest = await new WebBuildService(storage).PrepareAsync(asset, null, default);

        Assert.Equal("index.html", manifest.EntryPoint);
        Assert.Equal(html, storage.Uploads[WebBuildService.Key(asset.Id, "index.html")]);
        Assert.Contains($"web-builds/{asset.Id:N}/manifest.json", storage.Uploads.Keys);
    }

    [Fact]
    public async Task StandaloneHtmlWithIncorrectStoredLengthDoesNotPublishBuild()
    {
        var html = Encoding.UTF8.GetBytes("<h1>Spil</h1>");
        var asset = new FileAsset { Id = Guid.NewGuid(), Key = "original.html", OriginalName = "spil.html", SizeBytes = html.Length + 1 };
        var storage = new NonSeekableStorage(asset.Key, html);

        await Assert.ThrowsAsync<InvalidDataException>(() => new WebBuildService(storage).PrepareAsync(asset, null, default));

        Assert.Empty(storage.Uploads);
    }

    private sealed class NonSeekableStorage(string originalKey, byte[] original) : IObjectStorage
    {
        public Dictionary<string, byte[]> Uploads { get; } = new();
        public bool IsConfigured => true;
        public string Bucket => "test";

        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            // MinIO's SDK needs a seekable source for this upload path.
            Assert.True(content.CanSeek);
            using var output = new MemoryStream();
            await content.CopyToAsync(output, ct);
            Uploads[key] = output.ToArray();
            return new(key, Bucket, contentType, output.Length);
        }

        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken ct = default)
        {
            if (key != originalKey) return Task.FromResult<ObjectGetResult?>(null);
            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true)) gzip.Write(original);
            Stream source = new GZipStream(new MemoryStream(compressed.ToArray()), CompressionMode.Decompress);
            Assert.False(source.CanSeek);
            return Task.FromResult<ObjectGetResult?>(new(source, "text/html", original.Length));
        }

        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default) =>
            Task.FromResult<Uri?>(null);
    }
}
