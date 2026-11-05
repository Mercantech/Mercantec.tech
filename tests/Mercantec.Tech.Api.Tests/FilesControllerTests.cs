using System.Security.Claims;
using System.Text;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Tests;

public sealed class FilesControllerTests
{
    [Fact]
    public async Task StatusReflectsStorageConfiguration()
    {
        var disabled = As(Guid.NewGuid(), teacher: true, new DisabledObjectStorage());
        var status = Assert.IsType<FilesController.FileStorageStatus>(
            Assert.IsType<OkObjectResult>(disabled.Status()).Value);
        Assert.False(status.Enabled);

        var enabled = As(Guid.NewGuid(), teacher: true, new FakeObjectStorage());
        status = Assert.IsType<FilesController.FileStorageStatus>(
            Assert.IsType<OkObjectResult>(enabled.Status()).Value);
        Assert.True(status.Enabled);
        Assert.Equal("test-bucket", status.Bucket);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task StudentsCannotUploadAndMissingStorageReturns503()
    {
        var student = As(Guid.NewGuid(), teacher: false, new FakeObjectStorage());
        Assert.IsType<ForbidResult>(await student.Upload(FormFile("a.txt", "hi"), default));

        var teacher = As(Guid.NewGuid(), teacher: true, new DisabledObjectStorage());
        var result = Assert.IsType<ObjectResult>(await teacher.Upload(FormFile("a.txt", "hi"), default));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    [Fact]
    public async Task TeacherCanUploadAndDownload()
    {
        var storage = new FakeObjectStorage();
        var teacher = As(Guid.NewGuid(), teacher: true, storage);
        var uploaded = Assert.IsType<FilesController.FileUploadResult>(
            Assert.IsType<OkObjectResult>(await teacher.Upload(FormFile("rapport.pdf", "%PDF"), default)).Value);
        Assert.Contains("rapport.pdf", uploaded.Key, StringComparison.Ordinal);
        Assert.Equal("application/pdf", uploaded.ContentType);

        var download = Assert.IsType<FileStreamResult>(await teacher.Download(uploaded.Key, default));
        Assert.Equal("application/pdf", download.ContentType);
        using var reader = new StreamReader(download.FileStream);
        Assert.Equal("%PDF", await reader.ReadToEndAsync());
    }

    private static FilesController As(Guid? sub, bool teacher, IObjectStorage storage)
    {
        var claims = new List<Claim>();
        if (sub is Guid id) claims.Add(new("sub", id.ToString()));
        if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(storage)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, sub is null ? null : "test"))
                }
            }
        };
    }

    private static FormFile FormFile(string name, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf"
                : "text/plain"
        };
    }

    private sealed class FakeObjectStorage : IObjectStorage
    {
        private readonly Dictionary<string, (byte[] Bytes, string ContentType)> _objects = new(StringComparer.Ordinal);

        public bool IsConfigured => true;
        public string Bucket => "test-bucket";

        public async Task<ObjectPutResult> PutAsync(
            string key,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            _objects[key] = (ms.ToArray(), contentType);
            return new ObjectPutResult(key, Bucket, contentType, ms.Length);
        }

        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!_objects.TryGetValue(key, out var obj))
                return Task.FromResult<ObjectGetResult?>(null);
            return Task.FromResult<ObjectGetResult?>(
                new ObjectGetResult(new MemoryStream(obj.Bytes), obj.ContentType, obj.Bytes.Length));
        }

        public Task<Uri?> GetPresignedGetUrlAsync(
            string key,
            TimeSpan expiry,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Uri?>(new Uri($"https://example.test/{key}?exp={expiry.TotalSeconds}"));
    }
}
