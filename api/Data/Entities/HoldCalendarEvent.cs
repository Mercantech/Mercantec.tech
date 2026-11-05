namespace Mercantec.Tech.Api.Data.Entities;

public sealed class HoldCalendarEvent
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public string Location { get; set; } = "";
    public string Kind { get; set; } = "teaching";
    public int Version { get; set; }
}
