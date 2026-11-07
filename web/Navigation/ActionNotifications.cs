using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Navigation;

public static class ActionNotifications
{
    public static IReadOnlyList<HoldWorkspaceView> PendingReviews(IEnumerable<HoldWorkspaceView> workspaces) =>
        workspaces.Where(w => w.CanManage && w.PendingReviews > 0).OrderBy(w => w.Name).ToArray();

    public static IReadOnlyList<PollView> Polls(IEnumerable<PollView> polls, IEnumerable<HoldWorkspaceView> workspaces, bool teacher)
    {
        var allowed = workspaces.Where(w => !teacher || w.CanManage).Select(w => w.HoldId).ToHashSet();
        return polls.Where(p => allowed.Contains(p.HoldId) && p.State == "open"
            && (teacher ? p.CanManage : !p.HasResponded && !p.CanManage))
            .OrderByDescending(p => p.CreatedAtUtc).ToArray();
    }
}
