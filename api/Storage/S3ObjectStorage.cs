using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Storage;

public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly ObjectStorageOptions _options;
    private readonly IAmazonS3 _client;

    public S3ObjectStorage(IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;
        if (!_options.IsConfigured)
            throw new InvalidOperationException("ObjectStorage er ikke konfigureret.");

        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint.TrimEnd('/'),
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        };

        _client = new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            config);
    }

    public bool IsConfigured => true;
    public string Bucket => _options.Bucket;
    public bool SupportsRanges => true;

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        _client.DeleteObjectAsync(_options.Bucket, key, cancellationToken);

    public async Task<ObjectGetResult?> GetRangeAsync(string key, long start, long end, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _options.Bucket, Key = key, ByteRange = new ByteRange(start, end)
            }, cancellationToken);
            return new ObjectGetResult(response.ResponseStream, response.Headers.ContentType ?? "application/octet-stream",
                response.ContentLength >= 0 ? response.ContentLength : null, response);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
    }

    public async Task<ObjectPutResult> PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true
        };

        await _client.PutObjectAsync(request, cancellationToken);

        var size = content.CanSeek ? content.Length : 0;
        return new ObjectPutResult(key, _options.Bucket, contentType, size);
    }

    public async Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_options.Bucket, key, cancellationToken);
            return new ObjectGetResult(
                response.ResponseStream,
                string.IsNullOrWhiteSpace(response.Headers.ContentType)
                    ? "application/octet-stream"
                    : response.Headers.ContentType,
                response.ContentLength >= 0 ? response.ContentLength : null, response);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<Uri?> GetPresignedGetUrlAsync(
        string key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = _options.Endpoint.StartsWith("https", StringComparison.OrdinalIgnoreCase)
                ? Protocol.HTTPS
                : Protocol.HTTP
        };

        var url = _client.GetPreSignedURL(request);

        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            && Uri.TryCreate(url, UriKind.Absolute, out var signed)
            && Uri.TryCreate(_options.PublicBaseUrl.TrimEnd('/') + signed.PathAndQuery, UriKind.Absolute, out var rewritten))
        {
            return Task.FromResult<Uri?>(rewritten);
        }

        return Task.FromResult<Uri?>(new Uri(url));
    }

    public void Dispose() => _client.Dispose();
}

public sealed class DisabledObjectStorage : IObjectStorage
{
    public bool IsConfigured => false;
    public string Bucket => "";

    public Task<ObjectPutResult> PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ObjectStorage er ikke konfigureret.");

    public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<ObjectGetResult?>(null);

    public Task<Uri?> GetPresignedGetUrlAsync(
        string key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Uri?>(null);
}
