namespace Mercantec.Tech.Api.Data.Entities;

public sealed class PollResponse
{
    public Guid PollId { get; set; }
    public PollRound Poll { get; set; } = null!;
    public Guid UserSub { get; set; }
    public int? Choice { get; set; }
    public string? Text { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
