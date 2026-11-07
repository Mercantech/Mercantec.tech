using System.Globalization;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Navigation;

namespace Mercantec.Tech.Web.Activity;

/// <summary>Shared destinations and copy for the full overview and header preview.</summary>
public static class ActionCenterPresentation
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    public static IEnumerable<ActionCenterItem> Ordered(IEnumerable<ActionCenterItem> items) => items
        .OrderBy(i => i.Severity == "important" ? 0 : 1).ThenBy(i => i.DeadlineUtc ?? DateTimeOffset.MaxValue)
        .ThenBy(i => i.HoldName).ThenBy(i => i.Title);
    public static string Href(ActionCenterItem item) => item.Kind switch
    {
        "poll" => WorkspaceNavigation.Poll(item.ResourceId, item.WorkspaceId, item.CourseId, item.HoldId),
        "review" => WorkspaceNavigation.View(item.WorkspaceId, "submissions", item.CourseId, questId: item.ResourceId, status: "submitted"),
        _ => WorkspaceNavigation.Quest(item.ResourceId, item.WorkspaceId, item.CourseId, item.HoldId, item.SubmissionId)
    };
    public static string KindLabel(ActionCenterItem item) => item.Kind switch
    {
        "poll" => "Afstemning",
        "review" => item.Count == 1 ? "1 aflevering til vurdering" : $"{item.Count} afleveringer til vurdering",
        _ => item.State == "changes-requested" ? "Ret din aflevering" : item.State == "overdue" ? "Afleveringsfristen er overskredet" : "Du mangler at aflevere"
    };
    public static string Icon(ActionCenterItem item) => item.Kind switch { "poll" => "chart", "review" => "check", _ => "target" };
    public static string DateLabel(DateTimeOffset deadline) => TimeZoneInfo.ConvertTime(deadline, Copenhagen).ToString("d. MMM yyyy 'kl.' HH:mm", Danish);
}
