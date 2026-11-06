namespace Mercantec.Tech.Api.Data.Entities;

public sealed class QuestAudienceMembership
{
    public Guid QuestId { get; set; }
    public LearningQuest Quest { get; set; } = null!;
    public Guid MembershipId { get; set; }
    public Membership Membership { get; set; } = null!;
}

public sealed class QuestAudienceGroup
{
    public Guid QuestId { get; set; }
    public LearningQuest Quest { get; set; } = null!;
    public Guid GroupId { get; set; }
    public LearningGroup Group { get; set; } = null!;
}
