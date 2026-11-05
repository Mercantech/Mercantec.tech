using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FilePreview = Mercantec.Tech.Contracts.FilePreview;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class FilesControllerTests
{
    [Fact]
    public async Task OwnerCanUploadReadAndInspectCodeButAnotherMemberCannotRead()
    {
        await using var env = await Environment.Create();
        var alice = env.As(env.Student);
        var uploaded = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(
            await alice.Upload(FormFile("sketch.ino", "void setup() {}"), default, env.Hold)).Value);
        Assert.Equal("text", uploaded.PreviewKind);
        Assert.Null(uploaded.DownloadUrl);
        Assert.IsType<FileStreamResult>(await alice.Download(uploaded.Key, default));
        Assert.IsType<NotFoundResult>(await env.As(env.OtherStudent).Download(uploaded.Key, default));
        var text = Assert.IsType<FileTextPreview>(Assert.IsType<OkObjectResult>(await alice.Text(uploaded.Id, default)).Value);
        Assert.Equal("void setup() {}", text.Text);
    }

    [Fact]
    public async Task StudentNeedsCourseMembershipAndLegacyKeysFailClosed()
    {
        await using var env = await Environment.Create();
        Assert.IsType<ForbidResult>(await env.As(env.Student).Upload(FormFile("a.txt", "hi"), default));
        Assert.IsType<NotFoundResult>(await env.As(Guid.NewGuid()).Upload(FormFile("a.txt", "hi"), default, env.Hold));
        await env.Storage.PutAsync("old/secret.pdf", new MemoryStream("%PDF"u8.ToArray()), "application/pdf");
        Assert.IsType<NotFoundResult>(await env.As(env.Teacher, true).Download("old/secret.pdf", default));
    }

    [Theory]
    [InlineData("game.html", "text/plain; charset=utf-8", "text")]
    [InlineData("drawing.svg", "text/plain; charset=utf-8", "text")]
    [InlineData("report.docx", "application/octet-stream", "download")]
    public async Task BrowserSuppliedMimeCannotActivateContent(string name, string expectedType, string expectedKind)
    {
        await using var env = await Environment.Create();
        var upload = FormFile(name, "<script>alert(1)</script>");
        upload.ContentType = "text/html";
        var controller = env.As(env.Student);
        var result = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(
            await controller.Upload(upload, default, env.Hold)).Value);
        Assert.Equal(expectedType, result.ContentType);
        Assert.Equal(expectedKind, result.PreviewKind);
        var download = Assert.IsType<FileStreamResult>(await controller.Download(result.Key, default));
        Assert.Equal(expectedType, download.ContentType);
        Assert.StartsWith("attachment;", controller.Response.Headers.ContentDisposition.ToString());
        Assert.Equal("nosniff", controller.Response.Headers.XContentTypeOptions.ToString());
    }

    [Fact]
    public async Task UnknownExecutableIsRejectedAndMissingStorageReturns503()
    {
        await using var env = await Environment.Create();
        Assert.IsType<BadRequestObjectResult>(await env.As(env.Student).Upload(FormFile("a.exe", "MZ"), default, env.Hold));
        var controller = env.As(env.Teacher, true, new DisabledObjectStorage());
        Assert.Equal(503, Assert.IsType<ObjectResult>(await controller.Upload(FormFile("a.txt", "hi"), default)).StatusCode);
    }

    [Theory]
    [InlineData("bytes=2-4", "234", "bytes 2-4/10")]
    [InlineData("bytes=7-", "789", "bytes 7-9/10")]
    [InlineData("bytes=-3", "789", "bytes 7-9/10")]
    [InlineData("bytes=8-100", "89", "bytes 8-9/10")]
    public async Task ByteRangesAreRequestedFromStorageWithoutSeekingWholeObject(string range, string expected, string contentRange)
    {
        await using var env = await Environment.Create();
        var controller = env.As(env.Student);
        var file = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(
            await controller.Upload(FormFile("clip.mp4", "0123456789"), default, env.Hold)).Value);
        controller.Request.Headers.Range = range;
        var body = Assert.IsType<FileStreamResult>(await controller.Download(file.Key, default));
        Assert.Equal(206, controller.Response.StatusCode);
        Assert.Equal(contentRange, controller.Response.Headers.ContentRange.ToString());
        Assert.Equal(expected.Length, controller.Response.ContentLength);
        using var reader = new StreamReader(body.FileStream);
        Assert.Equal(expected, await reader.ReadToEndAsync());
        Assert.Equal(1, env.Storage.RangeReads);
        Assert.Equal(0, env.Storage.FullReads);
    }

    [Theory]
    [InlineData("bytes=10-")]
    [InlineData("bytes=4-2")]
    [InlineData("bytes=-0")]
    [InlineData("bytes=0-1,4-5")]
    [InlineData("nonsense")]
    public async Task UnsatisfiableOrMultipleRangesReturn416(string range)
    {
        await using var env = await Environment.Create();
        var controller = env.As(env.Student);
        var file = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(
            await controller.Upload(FormFile("clip.mp4", "0123456789"), default, env.Hold)).Value);
        controller.Request.Headers.Range = range;
        Assert.Equal(416, Assert.IsType<StatusCodeResult>(await controller.Download(file.Key, default)).StatusCode);
        Assert.Equal("bytes */10", controller.Response.Headers.ContentRange.ToString());
        Assert.Equal(0, env.Storage.RangeReads);
    }

    [Fact]
    public async Task ViewerTicketIsFileScopedAndRechecksMembership()
    {
        await using var env = await Environment.Create();
        var owner = env.As(env.Student);
        var file = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(
            await owner.Upload(FormFile("report.pdf", "%PDF"), default, env.Hold)).Value);
        var preview = Assert.IsType<FilePreview>(Assert.IsType<OkObjectResult>(await owner.Preview(file.Id, default)).Value);
        var ticket = Uri.UnescapeDataString(preview.ContentUrl.Split("ticket=")[1]);
        var anonymous = env.Anonymous();
        Assert.IsType<FileStreamResult>(await anonymous.Content(file.Id, default, ticket));
        Assert.IsType<UnauthorizedResult>(await anonymous.Content(Guid.NewGuid(), default, ticket));
        Assert.IsType<UnauthorizedResult>(await anonymous.Content(file.Id, default, ticket + "broken"));
        env.Db.Memberships.Remove(await env.Db.Memberships.SingleAsync(m => m.UserSub == env.Student));
        await env.Db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await env.Anonymous().Content(file.Id, default, ticket));
    }

    [Fact]
    public async Task ZipPreviewListsFilesWithoutExecutingOrExtractingThem()
    {
        await using var env = await Environment.Create();
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("../index.html").Open());
            await writer.WriteAsync("<script>danger()</script>");
        }
        var controller = env.As(env.Student);
        var input = new FormFile(new MemoryStream(zip.ToArray()), 0, zip.Length, "file", "game.zip")
            { Headers = new HeaderDictionary(), ContentType = "application/zip" };
        var file = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(await controller.Upload(input, default, env.Hold)).Value);
        var result = Assert.IsType<FileArchivePreview>(Assert.IsType<OkObjectResult>(await controller.Archive(file.Id, default)).Value);
        Assert.Equal("../index.html", Assert.Single(result.Entries).Path);
    }

    [Fact]
    public async Task ArduinoSketchSourceCanBeReadInsideZipWithoutExtraction()
    {
        await using var env = await Environment.Create();
        var controller = env.As(env.Student);
        var uploaded = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(await controller.Upload(
            ZipFile(("sketch/sketch.ino", "void setup() {}"), ("sketch/data.bin", "binary")),
            default, env.Hold)).Value);
        var text = Assert.IsType<FileTextPreview>(Assert.IsType<OkObjectResult>(
            await controller.ArchiveText(uploaded.Id, "sketch/sketch.ino", default)).Value);
        Assert.Equal("void setup() {}", text.Text);
        Assert.IsType<BadRequestObjectResult>(await controller.ArchiveText(uploaded.Id, "sketch/data.bin", default));
        Assert.IsType<NotFoundResult>(await env.As(env.OtherStudent).ArchiveText(uploaded.Id, "sketch/sketch.ino", default));
    }

    [Fact]
    public async Task TruncatedUploadsAreRejectedBeforeObjectIsStored()
    {
        await using var env = await Environment.Create();
        var controller = env.As(env.Student);
        Assert.IsType<BadRequestObjectResult>(await controller.Upload(new MisreportedFile(), default, env.Hold));
        Assert.Empty(env.Storage.Objects);
        Assert.Empty(await env.Db.FileAssets.ToListAsync());
    }

    [Fact]
    public async Task WebBuildIsPreparedAndOnlyServedFromIsolatedOrigin()
    {
        await using var env = await Environment.Create();
        var controller = env.As(env.Student);
        var file = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(
            await controller.Upload(FormFile("game.html", "<h1>My game</h1>"), default, env.Hold)).Value);
        var prepared = Assert.IsType<FileWebPreview>(Assert.IsType<OkObjectResult>(
            await controller.PrepareWebPreview(file.Id, default)).Value);
        Assert.StartsWith("https://games.example.test/api/web-files/", prepared.LaunchUrl);
        var segments = new Uri(prepared.LaunchUrl).AbsolutePath.Split('/');
        var ticket = Uri.UnescapeDataString(segments[^2]);
        var game = env.Web("https", "games.example.test");
        var response = Assert.IsType<FileStreamResult>(await game.Get(file.Id, ticket, "index.html", default));
        Assert.Equal("text/html; charset=utf-8", response.ContentType);
        Assert.Contains("sandbox allow-scripts allow-same-origin", game.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.IsType<NotFoundResult>(await env.Web("https", "app.example.test").Get(file.Id, ticket, "index.html", default));
        Assert.IsType<NotFoundResult>(await env.Web("http", "games.example.test").Get(file.Id, ticket, "index.html", default));
        Assert.IsType<NotFoundResult>(await env.Web("https", "games.example.test").Get(file.Id, ticket, "../index.html", default));
        env.Db.Memberships.Remove(await env.Db.Memberships.SingleAsync(m => m.UserSub == env.Student));
        await env.Db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await env.Web("https", "games.example.test").Get(file.Id, ticket, "index.html", default));
    }

    [Fact]
    public async Task WebArchivePreservesBuildPathsAndRejectsTraversalBeforePublishing()
    {
        await using var env = await Environment.Create();
        var controller = env.As(env.Student);
        var file = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(await controller.Upload(
            ZipFile(("game/index.html", "<script src='Build/game.js'></script>"), ("game/Build/game.js", "console.log(1)"),
                ("game/Build/game.wasm.br", "compressed")), default, env.Hold)).Value);
        var prepared = Assert.IsType<FileWebPreview>(Assert.IsType<OkObjectResult>(
            await controller.PrepareWebPreview(file.Id, default)).Value);
        Assert.Equal("game/index.html", prepared.EntryPoint);
        var manifest = await new WebBuildService(env.Storage).GetManifestAsync(file.Id, default);
        var wasm = Assert.Single(manifest!.Entries, e => e.Path.EndsWith(".br"));
        Assert.Equal("application/wasm", wasm.ContentType);
        Assert.Equal("br", wasm.ContentEncoding);

        var unsafeFile = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(await controller.Upload(
            ZipFile(("index.html", "game"), ("../secret.txt", "escape")), default, env.Hold)).Value);
        Assert.IsType<BadRequestObjectResult>(await controller.PrepareWebPreview(unsafeFile.Id, default));
        Assert.Null(await new WebBuildService(env.Storage).GetManifestAsync(unsafeFile.Id, default));
    }

    [Fact]
    public async Task WebPreviewRequiresDistinctHttpsHostname()
    {
        await using var env = await Environment.Create();
        var controller = env.As(env.Student);
        var file = Assert.IsType<UploadedFile>(Assert.IsType<OkObjectResult>(
            await controller.Upload(FormFile("game.html", "<h1>Game</h1>"), default, env.Hold)).Value);
        env.Configuration["WebPreview:Origin"] = "https://app.example.test";
        var response = Assert.IsType<ObjectResult>(await controller.PrepareWebPreview(file.Id, default));
        Assert.Equal(503, response.StatusCode);
    }

    private static FormFile ZipFile(params (string Path, string Text)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            foreach (var entry in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(entry.Path).Open());
                writer.Write(entry.Text);
            }
        return new FormFile(new MemoryStream(buffer.ToArray()), 0, buffer.Length, "file", "game.zip")
            { Headers = new HeaderDictionary(), ContentType = "application/zip" };
    }

    private static FormFile FormFile(string name, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
            { Headers = new HeaderDictionary(), ContentType = "application/octet-stream" };
    }

    private sealed class MisreportedFile : IFormFile
    {
        public string ContentType => "text/plain";
        public string ContentDisposition => "";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => 10;
        public string Name => "file";
        public string FileName => "truncated.txt";
        public Stream OpenReadStream() => new MemoryStream("short"u8.ToArray());
        public void CopyTo(Stream target) => OpenReadStream().CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            OpenReadStream().CopyToAsync(target, cancellationToken);
    }

    private sealed class Environment : IAsyncDisposable
    {
        public Guid Teacher { get; } = Guid.NewGuid();
        public Guid Student { get; } = Guid.NewGuid();
        public Guid OtherStudent { get; } = Guid.NewGuid();
        public Guid Hold { get; } = Guid.NewGuid();
        public AppDbContext Db { get; }
        public FakeStorage Storage { get; } = new();
        public IConfiguration Configuration { get; } = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["WebPreview:Origin"] = "https://games.example.test",
                ["WebPreview:AppOrigin"] = "https://app.example.test"
            }).Build();
        private readonly SqliteConnection connection;
        private readonly IDataProtectionProvider protection = new EphemeralDataProtectionProvider();

        private Environment(SqliteConnection connection)
        {
            this.connection = connection;
            Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        }

        public static async Task<Environment> Create()
        {
            SQLitePCL.Batteries_V2.Init();
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var env = new Environment(connection);
            await env.Db.Database.EnsureCreatedAsync();
            var course = new Forloeb { Id = Guid.NewGuid(), Name = "Course", CreatedBySub = env.Teacher };
            env.Db.Forloeb.Add(course);
            env.Db.Hold.Add(new Hold { Id = env.Hold, ForloebId = course.Id, Name = "Class" });
            env.Db.Memberships.AddRange(
                new Membership { Id = Guid.NewGuid(), HoldId = env.Hold, UserSub = env.Student },
                new Membership { Id = Guid.NewGuid(), HoldId = env.Hold, UserSub = env.OtherStudent });
            await env.Db.SaveChangesAsync();
            return env;
        }

        public FilesController As(Guid sub, bool teacher = false, IObjectStorage? storage = null)
        {
            var claims = new List<Claim> { new("sub", sub.ToString()) };
            if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
            return Controller(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), storage);
        }

        public WebFilesController Web(string scheme, string host) =>
            new(Storage, Db, protection, Configuration)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { Request = { Scheme = scheme, Host = new HostString(host) } }
                }
            };

        public FilesController Anonymous() => Controller(new ClaimsPrincipal(new ClaimsIdentity()), null);

        private FilesController Controller(ClaimsPrincipal principal, IObjectStorage? storage) =>
            new(storage ?? Storage, Db, protection)
            {
                ControllerContext = new ControllerContext
                    { HttpContext = new DefaultHttpContext { User = principal, RequestServices = new ServiceCollection()
                        .AddSingleton(Configuration).BuildServiceProvider() } }
            };

        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class FakeStorage : IObjectStorage
    {
        public Dictionary<string, (byte[] Bytes, string Mime)> Objects { get; } = new();
        public int RangeReads { get; private set; }
        public int FullReads { get; private set; }
        public bool IsConfigured => true;
        public string Bucket => "test";
        public bool SupportsRanges => true;

        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Objects[key] = (buffer.ToArray(), contentType);
            return new(key, Bucket, contentType, buffer.Length);
        }

        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            FullReads++;
            return Task.FromResult<ObjectGetResult?>(Objects.TryGetValue(key, out var obj)
                ? new(new NonSeekableStream(obj.Bytes), obj.Mime, obj.Bytes.Length) : null);
        }

        public Task<ObjectGetResult?> GetRangeAsync(string key, long start, long end, CancellationToken cancellationToken = default)
        {
            RangeReads++;
            return Task.FromResult<ObjectGetResult?>(Objects.TryGetValue(key, out var obj)
                ? new(new NonSeekableStream(obj.Bytes[(int)start..((int)end + 1)]), obj.Mime, end - start + 1) : null);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        { Objects.Remove(key); return Task.CompletedTask; }

        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Private files must not expose storage URLs.");
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}
