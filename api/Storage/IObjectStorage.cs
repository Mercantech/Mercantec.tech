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

    Task<Uri?> GetPresignedGetUrlAsync(
        string key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);
}

public sealed record ObjectPutResult(string Key, string Bucket, string ContentType, long SizeBytes);

public sealed record ObjectGetResult(Stream Content, string ContentType, long? ContentLength) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
