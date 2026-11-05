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

/// <summary>
/// Skjuler Lectio/Outlook-dublethold: når 4 eller 8 aftaler er ens på nær tidspunkt
/// (samme titel/sted/varighed m.m. samme dag), fjernes de fra underviserkalenderen.
/// </summary>
public static class TeacherCalendarHoldFilter
{
    private static readonly HashSet<int> DuplicateHoldCounts = [4, 8];

    public static IReadOnlyList<TeacherCalendarEvent> HideDuplicateHolds(
        IReadOnlyList<TeacherCalendarEvent> events)
    {
        if (events.Count < 4)
            return events;

        var tz = ResolveDkTimeZone();
        var hideIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in events.GroupBy(e => Fingerprint(e, tz)))
        {
            var members = group.ToList();
            if (!DuplicateHoldCounts.Contains(members.Count))
                continue;

            foreach (var item in members)
                hideIds.Add(item.Id);
        }

        if (hideIds.Count == 0)
            return events;

        return events.Where(e => !hideIds.Contains(e.Id)).ToList();
    }

    private static (DateOnly Day, string Subject, string Location, bool IsAllDay, bool IsOnline, bool IsPrivate, int DurationMinutes)
        Fingerprint(TeacherCalendarEvent e, TimeZoneInfo tz)
    {
        var localStart = TimeZoneInfo.ConvertTime(e.Start, tz);
        var day = DateOnly.FromDateTime(localStart.DateTime);
        var duration = e.IsAllDay
            ? -1
            : Math.Max(0, (int)Math.Round((e.End - e.Start).TotalMinutes));
        return (
            day,
            Normalize(e.Subject),
            Normalize(e.Location) ?? "",
            e.IsAllDay,
            e.IsOnlineMeeting,
            e.IsPrivate,
            duration);
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? ""
            : string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant();

    private static TimeZoneInfo ResolveDkTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Copenhagen");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
