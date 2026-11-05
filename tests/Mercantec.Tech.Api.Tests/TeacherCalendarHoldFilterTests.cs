using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class TeacherCalendarHoldFilterTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    public void HidesGroupsOfFourOrEightIdenticalExceptTime(int count)
    {
        var day = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        var events = Enumerable.Range(0, count)
            .Select(i => Event($"e{i}", "dt1H2601/pro", day.AddHours(8 + i), minutes: 45))
            .ToList();

        var filtered = TeacherCalendarHoldFilter.HideDuplicateHolds(events);

        Assert.Empty(filtered);
    }

    [Fact]
    public void KeepsGroupsWithOtherCounts()
    {
        var day = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        var events = Enumerable.Range(0, 3)
            .Select(i => Event($"e{i}", "dt1H2601/pro", day.AddHours(8 + i), minutes: 45))
            .Concat([Event("other", "Møde", day.AddHours(14), minutes: 30)])
            .ToList();

        var filtered = TeacherCalendarHoldFilter.HideDuplicateHolds(events);

        Assert.Equal(4, filtered.Count);
    }

    [Fact]
    public void DifferentSubjectsAreNotGrouped()
    {
        var day = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        var events = Enumerable.Range(0, 4)
            .Select(i => Event($"e{i}", $"hold-{i}", day.AddHours(8 + i), minutes: 45))
            .ToList();

        var filtered = TeacherCalendarHoldFilter.HideDuplicateHolds(events);

        Assert.Equal(4, filtered.Count);
    }

    [Fact]
    public void SameSubjectOnDifferentDaysIsKept()
    {
        var monday = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);
        var events = Enumerable.Range(0, 4)
            .Select(i => Event($"e{i}", "dt1H2601/pro", monday.AddDays(i), minutes: 45))
            .ToList();

        var filtered = TeacherCalendarHoldFilter.HideDuplicateHolds(events);

        Assert.Equal(4, filtered.Count);
    }

    [Fact]
    public void DifferentDurationIsNotIdentical()
    {
        var day = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        var events = new[]
        {
            Event("a", "dt1H2601/pro", day.AddHours(8), 45),
            Event("b", "dt1H2601/pro", day.AddHours(9), 45),
            Event("c", "dt1H2601/pro", day.AddHours(10), 45),
            Event("d", "dt1H2601/pro", day.AddHours(11), 90)
        };

        var filtered = TeacherCalendarHoldFilter.HideDuplicateHolds(events);

        Assert.Equal(4, filtered.Count);
    }

    [Fact]
    public void NormalizesSubjectWhitespaceAndCase()
    {
        var day = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        var events = new[]
        {
            Event("a", "dt1H2601/pro", day.AddHours(8), 45),
            Event("b", "DT1H2601/PRO", day.AddHours(9), 45),
            Event("c", "  dt1H2601/pro  ", day.AddHours(10), 45),
            Event("d", "dt1H2601/pro", day.AddHours(11), 45)
        };

        var filtered = TeacherCalendarHoldFilter.HideDuplicateHolds(events);

        Assert.Empty(filtered);
    }

    private static TeacherCalendarEvent Event(
        string id, string subject, DateTimeOffset start, int minutes) =>
        new(
            id,
            subject,
            start,
            start.AddMinutes(minutes),
            IsAllDay: false,
            Location: "A1.12",
            IsOnlineMeeting: false,
            OnlineMeetingUrl: null,
            WebLink: null,
            IsPrivate: false,
            IsCancelled: false,
            ShowAs: "busy");
}
