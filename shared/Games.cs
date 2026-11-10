namespace Mercantec.Tech.Contracts;

public sealed record UploadedGameView(Guid Id, Guid FileId, string Title, string OriginalName,
    string EntryPoint, DateTimeOffset CreatedAtUtc);
