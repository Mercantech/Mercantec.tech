namespace Mercantec.Tech.Contracts;

public sealed record TeacherCalendarView(
    string AzureUpn,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    bool Configured,
    IReadOnlyList<TeacherCalendarEvent> Events);

public sealed record TeacherCalendarEvent(
    string Id,
    string Subject,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool IsAllDay,
    string? Location,
    bool IsOnlineMeeting,
    string? OnlineMeetingUrl,
    string? WebLink,
    bool IsPrivate,
    bool IsCancelled,
    string? ShowAs);
