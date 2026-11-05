namespace Mercantec.Tech.Api.Data.Entities;

public sealed class PollRound
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public Hold Hold { get; set; } = null!;
    public Guid CreatedBySub { get; set; }
    public string Question { get; set; } = "";
    public string Kind { get; set; } = "choice";
    public string OptionsJson { get; set; } = "[]";
    public string State { get; set; } = "draft";
    public string ResultsMode { get; set; } = "after-vote";
    public long ResponseRevision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<PollResponse> Responses { get; set; } = [];
}
