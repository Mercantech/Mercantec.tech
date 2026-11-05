using System.Globalization;

namespace Mercantec.Tech.Web.Learning;

public static class HoldTime
{
    private static TimeZoneInfo Copenhagen => TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    public static DateTime Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Copenhagen).DateTime;
    public static string Input(DateTimeOffset value) => Local(value).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    public static string Display(DateTimeOffset value) => Local(value).ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("da-DK"));
    public static bool TryUtc(string input, out DateTimeOffset utc)
    {
        utc = default;
        if (!DateTime.TryParseExact(input, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        if (Copenhagen.IsInvalidTime(parsed) || Copenhagen.IsAmbiguousTime(parsed)) return false;
        utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(parsed, Copenhagen));
        return true;
    }
    public static string QuestKind(string kind) => kind switch { "daily" => "Daily quest", "side" => "Side quest", _ => "Main quest" };
}
