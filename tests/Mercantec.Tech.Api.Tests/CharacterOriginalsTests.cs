using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Api.Storage.CharacterOriginals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mercantec.Tech.Api.Tests;

public sealed class CharacterOriginalsTests
{
    private const string RelativePath = "whale/scenes/kommandobroen.png";
    private static readonly string Sha = new('a', 64);

    [Fact]
    public async Task MatchesOnlyListedPathAndStreamsFromTheContentAddressedKey()
    {
        var storage = new TestStorage([1, 2, 3]);
        var controller = Controller(storage);

        foreach (var path in new[] { "../" + RelativePath, "/images/characters/" + RelativePath,
            "other/kommandobroen.png", RelativePath.ToUpperInvariant(), "private/assets.png" })
        {
            Assert.IsType<NotFoundResult>(await controller.Get(path, default));
        }
        Assert.Null(storage.LastKey);

        var result = Assert.IsType<FileStreamResult>(await controller.Get(RelativePath, default));
        Assert.Equal($"public/character-originals/sha256/{Sha}.png", storage.LastKey);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal($"\"sha256-{Sha}\"", result.EntityTag?.Tag.Value);
        Assert.Equal("public, max-age=86400, no-transform", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("nosniff", controller.Response.Headers.XContentTypeOptions.ToString());
        Assert.Equal(3, controller.Response.ContentLength);
        await using var content = result.FileStream;
        Assert.Equal(1, content.ReadByte());
    }

    [Fact]
    public async Task UnavailableOrInvalidObjectsAreNotCachedAsSuccess()
    {
        var missing = Controller(new TestStorage(null));
        Assert.IsType<NotFoundResult>(await missing.Get(RelativePath, default));
        Assert.Equal("no-store", missing.Response.Headers.CacheControl.ToString());

        var mismatch = Controller(new TestStorage([1, 2]));
        var badGateway = Assert.IsType<StatusCodeResult>(await mismatch.Get(RelativePath, default));
        Assert.Equal(502, badGateway.StatusCode);
        Assert.Equal("no-store", mismatch.Response.Headers.CacheControl.ToString());

        var disabled = Controller(new DisabledObjectStorage());
        var unavailable = Assert.IsType<StatusCodeResult>(await disabled.Get(RelativePath, default));
        Assert.Equal(503, unavailable.StatusCode);
    }

    [Fact]
    public void RejectsDuplicateOrUnsupportedManifestPaths()
    {
        var valid = new CharacterOriginalCatalog.Original(RelativePath, Sha, 3);
        Assert.Throws<InvalidDataException>(() => MakeCatalog(valid, valid));
        Assert.Throws<InvalidDataException>(() => MakeCatalog(valid with { Path = "private/../name.png" }));
        Assert.Throws<InvalidDataException>(() => MakeCatalog(valid with { Sha256 = new string('x', 64) }));
        Assert.Throws<InvalidDataException>(() => MakeCatalog(valid with { Bytes = 0 }));
    }

    [Fact]
    public void PublishedManifestResolvesExistingPngLinksWithAnEmbeddedCatalog()
    {
        var original = CharacterOriginalCatalog.Load().Find(RelativePath);
        Assert.NotNull(original);
        Assert.StartsWith("public/character-originals/sha256/", original.Key);
    }

    [Fact]
    public async Task GetsAndHeadsANonSeekableStorageStreamAndRevalidatesETag()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(CharacterOriginalsController).Assembly);
        builder.Services.AddSingleton(MakeCatalog(new CharacterOriginalCatalog.Original(RelativePath, Sha, 3)));
        builder.Services.AddSingleton<IObjectStorage>(new TestStorage([1, 2, 3]));
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();

        using var client = new HttpClient
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())
        };
        const string route = "/api/internal/character-originals/" + RelativePath;

        using var original = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        Assert.Equal("image/png", original.Content.Headers.ContentType?.MediaType);
        Assert.Equal(3, original.Content.Headers.ContentLength);
        Assert.Equal(new byte[] { 1, 2, 3 }, await original.Content.ReadAsByteArrayAsync());

        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, route));
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal(3, head.Content.Headers.ContentLength);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());

        using var check = new HttpRequestMessage(HttpMethod.Get, route);
        check.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"sha256-{Sha}\""));
        using var unchanged = await client.SendAsync(check);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
    }

    private static CharacterOriginalsController Controller(IObjectStorage storage) =>
        new(storage, MakeCatalog(new CharacterOriginalCatalog.Original(RelativePath, Sha, 3)))
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        };

    private static CharacterOriginalCatalog MakeCatalog(params CharacterOriginalCatalog.Original[] files)
    {
        var json = JsonSerializer.Serialize(new { schemaVersion = 1, files }, JsonSerializerOptions.Web);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return CharacterOriginalCatalog.Load(stream);
    }

    private sealed class TestStorage(byte[]? data) : IObjectStorage
    {
        public bool IsConfigured => true;
        public string Bucket => "test";
        public string? LastKey { get; private set; }

        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            LastKey = key;
            return Task.FromResult(data is null ? null : new ObjectGetResult(
                new NonSeekableStorageStream(data), "application/octet-stream", data.Length));
        }

        public Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NonSeekableStorageStream(byte[] data) : MemoryStream(data, writable: false)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
    }
}
