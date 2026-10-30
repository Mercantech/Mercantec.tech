namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>Elev på hold. Samme bruger kan være på flere hold. Nøgle = Auth sub.</summary>
public class Membership
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public Hold Hold { get; set; } = null!;
    public Guid UserSub { get; set; }
    /// <summary>manual | lectio (lectio sync kommer senere).</summary>
    public string Source { get; set; } = "manual";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
