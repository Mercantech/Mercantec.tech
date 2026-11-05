namespace Mercantec.Tech.Api.Data.Entities;

public sealed class LearningGroup
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public ICollection<LearningGroupMember> Members { get; set; } = new List<LearningGroupMember>();
}

public sealed class LearningGroupMember
{
    public Guid GroupId { get; set; }
    public LearningGroup Group { get; set; } = null!;
    public Guid MembershipId { get; set; }
    public Membership Membership { get; set; } = null!;
}

public sealed class NotionAsset
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public NotionPage Page { get; set; } = null!;
    public string Key { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public string Kind { get; set; } = "download";
    public long SizeBytes { get; set; }
    public Guid UploadedBySub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
