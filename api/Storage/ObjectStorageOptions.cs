namespace Mercantec.Tech.Api.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>S3 API endpoint, fx http://mercantec-tech-minio:9000</summary>
    public string Endpoint { get; set; } = "";

    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "mercantec-tech";

    /// <summary>Valgfri browser-URL til MinIO (fx http://localhost:9000). Bruges i presigned URL'er.</summary>
    public string? PublicBaseUrl { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && !string.IsNullOrWhiteSpace(Bucket);
}
