namespace Mercantec.Tech.Api.Storage;

public interface IObjectStorage
{
    bool IsConfigured { get; }
    string Bucket { get; }

    Task<ObjectPutResult> PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    bool SupportsRanges => false;

    bool SupportsMultipart => false;

    Task<string> InitiateMultipartAsync(string key, string contentType, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Multipart uploads are unavailable.");

    Task<string> UploadPartAsync(string key, string uploadId, int partNumber, Stream content,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("Multipart uploads are unavailable.");

    Task CompleteMultipartAsync(string key, string uploadId, IReadOnlyList<ObjectUploadPart> parts,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("Multipart uploads are unavailable.");

    Task AbortMultipartAsync(string key, string uploadId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Multipart uploads are unavailable.");

    Task<ObjectGetResult?> GetRangeAsync(string key, long start, long end,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("Byte ranges are unavailable.");

    Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<Uri?> GetPresignedGetUrlAsync(
        string key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);
}

public sealed record ObjectPutResult(string Key, string Bucket, string ContentType, long SizeBytes);

public sealed record ObjectUploadPart(int PartNumber, string ETag);

public sealed record ObjectGetResult(Stream Content, string ContentType, long? ContentLength, IDisposable? Owner = null) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        try { await Content.DisposeAsync(); }
        finally { Owner?.Dispose(); }
    }
}
