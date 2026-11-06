namespace Mercantec.Tech.Contracts;

public sealed record GroupSetView(Guid Id, Guid HoldId, string Name, bool Archived, int Version,
    int GroupCount, int MemberCount);
public sealed record SaveGroupSet(string Name, bool Archived = false, int Version = 0);
