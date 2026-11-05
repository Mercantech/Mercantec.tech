namespace Mercantec.Tech.Contracts;

public sealed record NotionAssetView(Guid Id, string OriginalName, string ContentType, string Kind, long SizeBytes);
public sealed record NotionAssetAccess(string Url, string DownloadUrl, DateTimeOffset ExpiresAtUtc,
    string OriginalName, string ContentType, string Kind, long SizeBytes);
