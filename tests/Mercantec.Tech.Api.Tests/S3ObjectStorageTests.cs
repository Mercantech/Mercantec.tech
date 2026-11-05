using Mercantec.Tech.Api.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class S3ObjectStorageTests
{
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
