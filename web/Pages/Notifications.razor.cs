using System.Globalization;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Activity;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Navigation;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages;

public partial class Notifications : IDisposable
{
    [Inject] private AuthService Auth { get; set; } = null!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = null!;
    [Inject] private ActionCenterService Actions { get; set; } = null!;
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private IEnumerable<ActionCenterItem> Items(string severity) => Actions.Items.Where(i => i.Severity == severity)
        .OrderBy(i => i.DeadlineUtc ?? DateTimeOffset.MaxValue).ThenBy(i => i.HoldName).ThenBy(i => i.Title);
    private static string Href(ActionCenterItem item) => item.Kind switch
    {
        "poll" => WorkspaceNavigation.Poll(item.ResourceId, item.WorkspaceId, item.CourseId, item.HoldId),
        "review" => WorkspaceNavigation.View(item.WorkspaceId, "submissions", item.CourseId, questId: item.ResourceId, status: "submitted"),
        _ => WorkspaceNavigation.Quest(item.ResourceId, item.WorkspaceId, item.CourseId, item.HoldId, item.SubmissionId)
    };
    private static string KindLabel(ActionCenterItem item) => item.Kind switch
    {
        "poll" => "Afstemning",
        "review" => item.Count == 1 ? "1 aflevering til vurdering" : $"{item.Count} afleveringer til vurdering",
        _ => item.State == "changes-requested" ? "Ret din aflevering" : item.State == "overdue" ? "Afleveringsfristen er overskredet" : "Du mangler at aflevere"
    };
    private static string Icon(ActionCenterItem item) => item.Kind switch { "poll" => "chart", "review" => "check", _ => "target" };
    private static string DateLabel(DateTimeOffset deadline) => TimeZoneInfo.ConvertTime(deadline, Copenhagen).ToString("d. MMM yyyy 'kl.' HH:mm", Danish);
    protected override void OnInitialized() { Actions.Changed += StatusChanged; }
    protected override Task OnAfterRenderAsync(bool firstRender) => firstRender ? Actions.EnsureAsync() : Task.CompletedTask;
    private Task Refresh() => Actions.RefreshAsync(force: true);
    private void StatusChanged() => _ = InvokeAsync(StateHasChanged);
    public void Dispose() { Actions.Changed -= StatusChanged; }
}
