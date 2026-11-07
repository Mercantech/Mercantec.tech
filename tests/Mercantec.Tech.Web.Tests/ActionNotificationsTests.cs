using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Navigation;

namespace Mercantec.Tech.Web.Tests;

public sealed class ActionNotificationsTests
{
    [Fact]
    public void StudentSeesOnlyUnansweredOpenPollsFromAccessibleWorkspaces()
    {
        var own = Guid.NewGuid(); var other = Guid.NewGuid();
        var pending = Poll(own); var answered = Poll(own, responded: true); var closed = Poll(own, state: "closed");
        var draft = Poll(own, state: "draft"); var unrelated = Poll(other); var teacher = Poll(own, manage: true);
        var result = ActionNotifications.Polls([pending, answered, closed, draft, unrelated, teacher], [Workspace(own)], false);
        Assert.Equal(pending.Id, Assert.Single(result).Id);
    }
    [Fact]
    public void TeacherReviewsRequireManagePermissionAndActualPendingCount()
    {
        var manageable = Workspace(Guid.NewGuid(), manage: true, pending: 3);
        var notManageable = Workspace(Guid.NewGuid(), pending: 9);
        var clear = Workspace(Guid.NewGuid(), manage: true);
        Assert.Equal(manageable.CourseId, Assert.Single(ActionNotifications.PendingReviews([manageable, notManageable, clear])).CourseId);
    }
    [Fact]
    public void TeacherPollsMustBelongToAnAuthorizedManageableWorkspace()
    {
        var managed = Guid.NewGuid(); var readable = Guid.NewGuid(); var unrelated = Guid.NewGuid();
        var included = Poll(managed, manage: true);
        var result = ActionNotifications.Polls([included, Poll(readable, manage: true), Poll(unrelated, manage: true), Poll(managed)], [Workspace(managed, manage: true), Workspace(readable)], true);
        Assert.Equal(included.Id, Assert.Single(result).Id);
    }
    [Fact]
    public void NoWorkspaceMeansNoCrossAccountPollNotifications() => Assert.Empty(ActionNotifications.Polls([Poll(Guid.NewGuid())], [], false));

    private static HoldWorkspaceView Workspace(Guid hold, bool manage = false, int pending = 0) =>
        new(Guid.NewGuid(), hold, "Testhold", null, manage, 0, 0, 0, pending, 0, 0, [], [], [], []);
    private static PollView Poll(Guid hold, bool responded = false, bool manage = false, string state = "open") =>
        new(Guid.NewGuid(), hold, "Testhold", "Et spørgsmål", "choice", ["A", "B"], state, "after", manage, 1, responded, null, null, null, null, null, DateTime.UtcNow);
}
