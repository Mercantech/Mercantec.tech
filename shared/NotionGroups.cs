namespace Mercantec.Tech.Contracts;

public sealed record NotionHoldMember(Guid Id, string DisplayName, Guid[] GroupIds);
public sealed record NotionGroupView(Guid Id, Guid HoldId, string Name, int Version, NotionMember[] Members);
public sealed record SaveNotionGroup(string Name, Guid[] MembershipIds, int Version = 0);
public sealed record NotionStudentProfile(Guid Id, Guid HoldId, string DisplayName, NotionGroupView[] Groups);
