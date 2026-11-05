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
    public bool SupportsMultipart => true;

    public async Task<string> InitiateMultipartAsync(string key, string contentType, CancellationToken cancellationToken = default)
    {
        var result = await _client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = _options.Bucket, Key = key, ContentType = contentType
        }, cancellationToken);
        return result.UploadId;
    }

    public async Task<string> UploadPartAsync(string key, string uploadId, int partNumber, Stream content,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.UploadPartAsync(new UploadPartRequest
        {
            BucketName = _options.Bucket, Key = key, UploadId = uploadId, PartNumber = partNumber,
            InputStream = content, PartSize = content.Length,
            DisablePayloadSigning = _options.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        }, cancellationToken);
        return result.ETag;
    }

    public async Task CompleteMultipartAsync(string key, string uploadId, IReadOnlyList<ObjectUploadPart> parts,
        CancellationToken cancellationToken = default) =>
        await _client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
        {
            BucketName = _options.Bucket, Key = key, UploadId = uploadId,
            PartETags = parts.Select(p => new PartETag(p.PartNumber, p.ETag)).ToList()
        }, cancellationToken);

    public async Task AbortMultipartAsync(string key, string uploadId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = _options.Bucket, Key = key, UploadId = uploadId
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }
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
            DisablePayloadSigning = _options.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        };

        await _client.PutObjectAsync(request, cancellationToken);

        var size = content.CanSeek ? content.Length : 0;
        return new ObjectPutResult(key, _options.Bucket, contentType, size);
    }

    public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        ReadAsync(new GetObjectRequest { BucketName = _options.Bucket, Key = key }, cancellationToken);

    public Task<ObjectGetResult?> GetRangeAsync(string key, long start, long end,
        CancellationToken cancellationToken = default) =>
        ReadAsync(new GetObjectRequest { BucketName = _options.Bucket, Key = key, ByteRange = new ByteRange(start, end) },
            cancellationToken);

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        await _client.DeleteObjectAsync(_options.Bucket, key, cancellationToken);

    private async Task<ObjectGetResult?> ReadAsync(GetObjectRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetObjectAsync(request, cancellationToken);
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

        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            return Task.FromResult<Uri?>(new Uri(_client.GetPreSignedURL(request)));

        // The host is part of the signature. Sign against the browser endpoint directly.
        var publicEndpoint = new Uri(_options.PublicBaseUrl, UriKind.Absolute);
        request.Protocol = publicEndpoint.Scheme == "https" ? Protocol.HTTPS : Protocol.HTTP;
        using var signer = new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = publicEndpoint.ToString().TrimEnd('/'),
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1"
            });
        return Task.FromResult<Uri?>(new Uri(signer.GetPreSignedURL(request)));
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
