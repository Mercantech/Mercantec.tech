namespace Mercantec.Tech.Api.Data.Entities;

public sealed class LearningGroupSet
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public string Name { get; set; } = "";
    public bool Archived { get; set; }
    public int Version { get; set; }
}
