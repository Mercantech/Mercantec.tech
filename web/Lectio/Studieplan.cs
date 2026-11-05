using System.Text.Json;

namespace Mercantec.Tech.Web.Lectio;

/// <summary>
/// Parser og tidslinje for Lectio-studieplan + VUF fra Collector.
/// VUF-reglen kommer fra Lectio (<c>vuf.ok</c>) — ingen lokale tærskler.
/// </summary>
public static class Studieplan
{
    public static StudentPlan ParsePlan(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var className = GetString(root, "className");
        var periods = new List<PeriodRow>();

        if (TryGetArray(root, "schoolPeriods", out var arr)
            || TryGetArray(root, "SchoolPeriods", out arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                var name = GetString(item, "name") ?? GetString(item, "Name") ?? "—";
                var start = ParseDateOnly(item, "startDate") ?? ParseDateOnly(item, "StartDate");
                var end = ParseDateOnly(item, "endDate") ?? ParseDateOnly(item, "EndDate");
                periods.Add(new PeriodRow(
                    name,
                    start?.ToString("dd.MM.yyyy"),
                    end?.ToString("dd.MM.yyyy"),
                    start,
                    end));
            }
        }

        return new StudentPlan(className, periods);
    }

    public static TimelineView? BuildTimeline(IReadOnlyList<PeriodRow> periods)
    {
        var dated = periods
            .Where(p => p.StartDate is not null && p.EndDate is not null && p.EndDate >= p.StartDate)
            .OrderBy(p => p.StartDate)
            .ToList();
        if (dated.Count == 0)
            return null;

        var rangeStart = dated.Min(p => p.StartDate!.Value);
        var rangeEnd = dated.Max(p => p.EndDate!.Value);
        rangeStart = rangeStart.AddMonths(-1);
        rangeEnd = rangeEnd.AddMonths(1);
        var totalDays = Math.Max(1, rangeEnd.DayNumber - rangeStart.DayNumber);
        var today = DateOnly.FromDateTime(DateTime.Today);

        double Pct(DateOnly d) =>
            100.0 * (d.DayNumber - rangeStart.DayNumber) / totalDays;

        double? todayLeft = today >= rangeStart && today <= rangeEnd ? Pct(today) : null;

        var bars = dated.Select(p =>
        {
            var left = Pct(p.StartDate!.Value);
            var right = Pct(p.EndDate!.Value);
            var width = Math.Max(0.8, right - left);
            var isPast = p.EndDate < today;
            var isFuture = p.StartDate > today;
            var isCurrent = !isPast && !isFuture;
            return new TimelineBar(
                p.Name,
                left,
                width,
                isPast,
                isCurrent,
                isFuture,
                $"{p.Name}: {p.StartLabel} – {p.EndLabel}");
        }).ToList();

        var ticks = new List<TimelineTick>();
        var year = rangeStart.Year;
        while (year <= rangeEnd.Year)
        {
            var jan1 = new DateOnly(year, 1, 1);
            if (jan1 >= rangeStart && jan1 <= rangeEnd)
                ticks.Add(new TimelineTick(Pct(jan1), year.ToString()));
            year++;
        }

        if (ticks.Count == 0)
        {
            ticks.Add(new TimelineTick(0, rangeStart.ToString("MMM yyyy")));
            ticks.Add(new TimelineTick(100, rangeEnd.ToString("MMM yyyy")));
        }

        return new TimelineView(bars, ticks, todayLeft);
    }

    public static StudentSnapshot? ParseStudentSnapshot(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = UnwrapStudent(doc.RootElement);
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        string? workplaceName = null;
        if (root.TryGetProperty("workplace", out var wp) && wp.ValueKind == JsonValueKind.Object)
            workplaceName = GetString(wp, "name");

        string? forloebName = null;
        string? forloebDates = null;
        DateOnly? forloebStart = null;
        DateOnly? forloebEnd = null;
        if (root.TryGetProperty("h6", out var h6) && h6.ValueKind == JsonValueKind.Object)
        {
            forloebName = GetString(h6, "name");
            forloebStart = ParseDateOnly(h6, "startDate");
            forloebEnd = ParseDateOnly(h6, "endDate");
            var start = forloebStart?.ToString("dd.MM.yyyy");
            var end = forloebEnd?.ToString("dd.MM.yyyy");
            if (!string.IsNullOrWhiteSpace(start) || !string.IsNullOrWhiteSpace(end))
                forloebDates = $"{start ?? "?"} – {end ?? "?"}";
            var level = GetString(h6, "level") ?? (h6.TryGetProperty("level", out var lv) && lv.ValueKind == JsonValueKind.Number
                ? lv.ToString()
                : null);
            if (!string.IsNullOrWhiteSpace(level) && !string.IsNullOrWhiteSpace(forloebName))
                forloebName = $"{level}: {forloebName}";
            else if (!string.IsNullOrWhiteSpace(level))
                forloebName = level;
        }

        int? vufEarned = null, vufRequired = null;
        double? vufPercent = null;
        bool? vufOk = null;
        if (root.TryGetProperty("vuf", out var vuf) && vuf.ValueKind == JsonValueKind.Object)
        {
            vufEarned = GetInt(vuf, "earned");
            vufRequired = GetInt(vuf, "required");
            vufPercent = GetDouble(vuf, "percent");
            if (vuf.TryGetProperty("ok", out var ok) && (ok.ValueKind is JsonValueKind.True or JsonValueKind.False))
                vufOk = ok.GetBoolean();
        }

        return new StudentSnapshot(
            LectioId: GetString(root, "lectioId"),
            Name: GetString(root, "name"),
            ClassName: GetString(root, "className"),
            WorkplaceName: workplaceName,
            ForloebName: forloebName,
            ForloebDates: forloebDates,
            ForloebStart: forloebStart,
            ForloebEnd: forloebEnd,
            Vuf: new VufStatus(vufEarned, vufRequired, vufPercent, vufOk));
    }

    public static string? ExtractLectioId(string meJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(meJson);
            if (!doc.RootElement.TryGetProperty("lectio", out var lectio)
                || lectio.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                || lectio.ValueKind != JsonValueKind.Object)
                return null;
            if (!lectio.TryGetProperty("lectioId", out var id) || id.ValueKind != JsonValueKind.String)
                return null;
            var value = id.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    public static double? VufWidth(VufStatus vuf)
    {
        if (vuf.Percent is double p)
            return Math.Clamp(p, 0, 100);
        if (vuf.Earned is int earned && vuf.Required is int required && required > 0)
            return Math.Clamp(100.0 * earned / required, 0, 100);
        return null;
    }

    private static JsonElement UnwrapStudent(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return root;
        if (root.TryGetProperty("student", out var student) && student.ValueKind == JsonValueKind.Object)
            return student;
        if (root.TryGetProperty("students", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
                return item;
        }
        return root;
    }

    private static bool TryGetArray(JsonElement el, string name, out JsonElement arr)
    {
        if (el.TryGetProperty(name, out arr) && arr.ValueKind == JsonValueKind.Array)
            return true;
        arr = default;
        return false;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int? GetInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null) return null;
        return p.TryGetInt32(out var i) ? i : null;
    }

    private static double? GetDouble(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null) return null;
        return p.TryGetDouble(out var d) ? d : null;
    }

    private static DateOnly? ParseDateOnly(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null) return null;
        if (p.ValueKind == JsonValueKind.String)
        {
            var raw = p.GetString();
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (DateOnly.TryParse(raw, out var d)) return d;
            if (DateTimeOffset.TryParse(raw, out var dto)) return DateOnly.FromDateTime(dto.Date);
            return null;
        }

        if (p.TryGetDateTimeOffset(out var dt))
            return DateOnly.FromDateTime(dt.Date);
        return null;
    }
}

public sealed record PeriodRow(
    string Name,
    string? StartLabel,
    string? EndLabel,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record StudentPlan(string? ClassName, IReadOnlyList<PeriodRow> Periods);

public sealed record TimelineBar(
    string Name,
    double Left,
    double Width,
    bool IsPast,
    bool IsCurrent,
    bool IsFuture,
    string Tooltip);

public sealed record TimelineTick(double Left, string Label);

public sealed record TimelineView(
    IReadOnlyList<TimelineBar> Bars,
    IReadOnlyList<TimelineTick> Ticks,
    double? TodayLeft);

public sealed record VufStatus(
    int? Earned,
    int? Required,
    double? Percent,
    bool? Ok)
{
    public bool IsMissing =>
        Earned is null && Required is null && Ok is null && Percent is null;
}

public sealed record StudentSnapshot(
    string? LectioId,
    string? Name,
    string? ClassName,
    string? WorkplaceName,
    string? ForloebName,
    string? ForloebDates,
    DateOnly? ForloebStart,
    DateOnly? ForloebEnd,
    VufStatus Vuf);
