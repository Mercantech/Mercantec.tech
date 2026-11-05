namespace Mercantec.Tech.Contracts;

public sealed record HoldCalendarEventView(Guid Id, Guid HoldId, string Title, string Description,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc, string Location, string Kind, int Version);
public sealed record SaveHoldCalendarEvent(Guid HoldId, string Title, string Description,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc, string Location, string Kind = "teaching", int Version = 0);
