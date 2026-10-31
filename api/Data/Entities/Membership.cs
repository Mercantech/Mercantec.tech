namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>
/// Elev på hold. Samme bruger kan være på flere hold.
/// Shadow: <see cref="UserSub"/> er null indtil Auth↔Lectio er claimed; så bruges <see cref="LectioId"/>.
/// </summary>
public class Membership
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public Hold Hold { get; set; } = null!;

    /// <summary>Auth sub når claimed; null = shadow-medlem fra Lectio.</summary>
    public Guid? UserSub { get; set; }

    /// <summary>Lectio elev-id (primær nøgle for shadow / sync).</summary>
    public string? LectioId { get; set; }

    /// <summary>Visningsnavn fra Lectio (shadow) eller kopi ved import.</summary>
    public string? DisplayName { get; set; }

    /// <summary>manual | lectio</summary>
    public string Source { get; set; } = "manual";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool IsShadow => UserSub is null;
}
