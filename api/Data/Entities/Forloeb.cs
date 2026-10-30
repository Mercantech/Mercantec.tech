namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>Faglig ramme (fx H3, Game Design). 1:1 med <see cref="Hold"/>.</summary>
public class Forloeb
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Auth sub for opretter (typisk underviser).</summary>
    public Guid CreatedBySub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public Hold? Hold { get; set; }
}
