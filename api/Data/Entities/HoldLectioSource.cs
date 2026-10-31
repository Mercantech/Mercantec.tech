namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>Lectio-hold der er importeret ind i et Mercantec-hold (flere Lectio-hold kan samles).</summary>
public class HoldLectioSource
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public Hold Hold { get; set; } = null!;
    public string LectioHoldKey { get; set; } = "";
    public string? LectioHoldName { get; set; }
    public DateTimeOffset ImportedAtUtc { get; set; }
}
