using Mercantec.Tech.Api.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class S3ObjectStorageTests
{
    [Fact]
    public async Task MultipartSdkSignsEachStageAndCompletesOnlyServerRecordedETags()
    {
        var builder = WebApplication.CreateSlimBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        var calls = new List<string>(); byte[]? received = null; string? manifest = null;
        app.MapMethods("/{bucket}/{**key}", ["POST", "PUT", "DELETE"], async (HttpContext context) =>
        {
            Assert.StartsWith("AWS4-HMAC-SHA256", context.Request.Headers.Authorization.ToString());
            context.Response.ContentType = "application/xml";
            if (context.Request.Method == "PUT")
            {
                calls.Add("part:" + context.Request.Query["partNumber"]);
                using var copy = new MemoryStream(); await context.Request.Body.CopyToAsync(copy); received = copy.ToArray();
                if (context.Request.Headers.ContentEncoding.ToString().Contains("aws-chunked", StringComparison.Ordinal)) received = DecodeAwsChunks(received);
                context.Response.Headers.ETag = "\"recorded-etag\"";
            }
            else if (context.Request.Method == "DELETE") { calls.Add("abort"); context.Response.StatusCode = 204; }
            else if (!context.Request.Query.ContainsKey("uploadId"))
            {
                calls.Add("initiate");
                await context.Response.WriteAsync("<InitiateMultipartUploadResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\"><Bucket>qa-bucket</Bucket><Key>notion-private/page/asset</Key><UploadId>upload-123</UploadId></InitiateMultipartUploadResult>");
            }
            else
            {
                calls.Add("complete"); using var reader = new StreamReader(context.Request.Body); manifest = await reader.ReadToEndAsync();
                await context.Response.WriteAsync("<CompleteMultipartUploadResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\"><Location>http://localhost/asset</Location><Bucket>qa-bucket</Bucket><Key>notion-private/page/asset</Key><ETag>final-etag</ETag></CompleteMultipartUploadResult>");
            }
        });
        await app.StartAsync();
        try
        {
            using var storage = new S3ObjectStorage(Options.Create(new ObjectStorageOptions
            { Endpoint = app.Urls.Single(), AccessKey = "qa-access", SecretKey = "qa-local-secret", Bucket = "qa-bucket" }));
            var id = await storage.InitiateMultipartAsync("notion-private/page/asset", "video/mp4");
            Assert.Equal("upload-123", id);
            using var input = new MemoryStream([1, 2, 3, 4]);
            var etag = await storage.UploadPartAsync("notion-private/page/asset", id, 1, input);
            Assert.Equal("\"recorded-etag\"", etag); Assert.Equal(new byte[] { 1, 2, 3, 4 }, received);
            Assert.True(input.CanRead); Assert.Equal(4, input.Length);
            await storage.CompleteMultipartAsync("notion-private/page/asset", id, [new(1, etag)]);
            Assert.Contains("recorded-etag", manifest); Assert.Contains("<PartNumber>1</PartNumber>", manifest);
            await storage.AbortMultipartAsync("notion-private/page/asset", id);
            Assert.Equal(["initiate", "part:1", "complete", "abort"], calls);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task InternalHttpUploadUsesRealSdkSigningAndPreservesContent()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        byte[]? received = null;
        string? authorization = null;
        app.MapPut("/{bucket}/{**key}", async (HttpContext context) =>
        {
            using var bytes = new MemoryStream();
            await context.Request.Body.CopyToAsync(bytes);
            received = bytes.ToArray();
            if (context.Request.Headers.ContentEncoding.ToString().Contains("aws-chunked", StringComparison.Ordinal))
                received = DecodeAwsChunks(received);
            authorization = context.Request.Headers.Authorization;
            context.Response.Headers.ETag = "\"qa-etag\"";
            return Results.Ok();
        });
        await app.StartAsync();
        try
        {
            using var storage = new S3ObjectStorage(Options.Create(new ObjectStorageOptions
            {
                Endpoint = app.Urls.Single(), AccessKey = "qa-access", SecretKey = "qa-local-secret", Bucket = "qa-bucket"
            }));
            var expected = System.Text.Encoding.UTF8.GetBytes("En rapport med æøå");
            await using var input = new MemoryStream(expected);
            var result = await storage.PutAsync("reports/report.txt", input, "text/plain");
            Assert.Equal(expected, received);
            Assert.StartsWith("AWS4-HMAC-SHA256", authorization);
            Assert.Equal(expected.Length, result.SizeBytes);
        }
        finally { await app.StopAsync(); }
    }

    private static byte[] DecodeAwsChunks(byte[] encoded)
    {
        using var decoded = new MemoryStream();
        var position = 0;
        while (position < encoded.Length)
        {
            var end = position;
            while (end + 1 < encoded.Length && !(encoded[end] == '\r' && encoded[end + 1] == '\n')) end++;
            var header = System.Text.Encoding.ASCII.GetString(encoded, position, end - position);
            var length = Convert.ToInt32(header.Split(';')[0], 16);
            if (length == 0) break;
            position = end + 2;
            decoded.Write(encoded, position, length);
            position += length + 2;
        }
        return decoded.ToArray();
    }
}
