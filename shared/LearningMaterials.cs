namespace Mercantec.Tech.Contracts;

public sealed record CourseMaterialView(Guid Id, Guid HoldId, Guid FileAssetId, string Title,
    string? Description, bool Published, DateTimeOffset CreatedAtUtc);
public sealed record CreateCourseMaterial(Guid HoldId, Guid FileAssetId, string Title,
    string? Description, bool Published);
public sealed record UpdateCourseMaterial(string Title, string? Description, bool Published);
