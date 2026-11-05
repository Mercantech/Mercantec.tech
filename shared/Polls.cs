namespace Mercantec.Tech.Contracts;

public sealed record PollHold(Guid Id, string Name, bool CanManage);
public sealed record PollView(Guid Id, Guid HoldId, string HoldName, string Question, string Kind,
    string[] Options, string State, string ResultsMode, bool CanManage, int ExpectedResponses,
    bool HasResponded, int? YourChoice, string? YourText, int? TotalResponses, int[]? Counts,
    string[]? Texts, DateTime CreatedAtUtc);
public sealed record CreatePoll(Guid HoldId, string Question, string Kind, string[] Options, string ResultsMode, string State);
public sealed record PollAnswer(int? Choice, string? Text);
public sealed record PollState(string State);
