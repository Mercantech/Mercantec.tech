namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>Elevliste. Præcis ét hold pr. forløb.</summary>
public class Hold
{
    public Guid Id { get; set; }
    public Guid ForloebId { get; set; }
    public Forloeb Forloeb { get; set; } = null!;
    public string Name { get; set; } = "";
    /// <summary>Lectio-Collector hold-nøgle (lectioHoldKey), hvis oprettet fra Lectio.</summary>
    public string? LectioHoldKey { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}
