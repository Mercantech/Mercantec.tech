using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mercantec.Tech.Api.Notebooks;

public static class NotebookPeriods
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    public static DateOnly LocalDay(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Copenhagen).DateTime);
    public static DateOnly Start(string cadence, DateOnly day) => cadence == "weekly"
        ? day.AddDays(-(((int)day.DayOfWeek + 6) % 7)) : day;
    public static string? Key(string cadence, DateOnly day) => cadence == "free" ? null : $"{cadence}:{Start(cadence, day):yyyy-MM-dd}";
    public static bool ValidCadence(string? cadence) => cadence is "daily" or "weekly" or "free";
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal) == true;
}
