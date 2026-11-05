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

    Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default);

    bool SupportsRanges => false;

    Task<ObjectGetResult?> GetRangeAsync(string key, long start, long end,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("Byte ranges are unavailable.");

    Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task<Uri?> GetPresignedGetUrlAsync(
        string key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);
}

public sealed record ObjectPutResult(string Key, string Bucket, string ContentType, long SizeBytes);

public sealed record ObjectGetResult(Stream Content, string ContentType, long? ContentLength, IDisposable? Owner = null) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        try { await Content.DisposeAsync(); }
        finally { Owner?.Dispose(); }
    }
}
