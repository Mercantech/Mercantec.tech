using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Navigation;

namespace Mercantec.Tech.Web.Tests;

public sealed class WorkspaceNavigationTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid HoldId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid CourseId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ResourceId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public void Student_notebook_entries_are_permanent_and_do_not_require_workspace_context()
    {
        Assert.Equal(new[] { new NotebookNavigationLink("Min notesbog", "/notebook"), new NotebookNavigationLink("Min logbog", "/journal") }, WorkspaceNavigation.NotebookLinks(false));
        Assert.All(WorkspaceNavigation.NotebookLinks(false), link => Assert.DoesNotContain('?', link.Href));
    }

    [Fact]
    public void Teacher_notebook_entries_open_the_real_overview_and_template_library()
    {
        Assert.Equal(new[] { new NotebookNavigationLink("Elevnotesbøger", "/teacher/notebooks"), new NotebookNavigationLink("Logbogsskabeloner", "/teacher/notebook-templates") }, WorkspaceNavigation.NotebookLinks(true));
    }

    [Theory]
    [InlineData("overview", "overview")]
    [InlineData("content", "content")]
    [InlineData("polls", "content")]
    [InlineData("materials", "content")]
    [InlineData("submissions", "quests")]
    [InlineData("settings", "groups")]
    public void Child_views_keep_their_route_and_select_the_primary_section(string view, string section)
    {
        Assert.Equal(view, WorkspaceNavigation.NormalizeView(view));
        Assert.Equal(section, WorkspaceNavigation.PrimarySection(view));
        Assert.Equal($"/courses/{WorkspaceId}?view={view}&course={CourseId}", WorkspaceNavigation.View(WorkspaceId, view, CourseId));
    }

    [Fact]
    public void Explicit_all_courses_removes_the_selection_and_unknown_views_or_statuses_cannot_enter_links()
    {
        Assert.Equal($"/courses/{WorkspaceId}?view=content", WorkspaceNavigation.View(WorkspaceId, "content"));
        Assert.Equal($"/courses/{WorkspaceId}?view=overview", WorkspaceNavigation.View(WorkspaceId, "../settings&create=true", Guid.Empty, status: "missing&event=evil"));
        Assert.Equal("/courses", WorkspaceNavigation.Hold(Guid.Empty, "content", CourseId));
    }

    [Fact]
    public void Document_graph_quest_and_poll_routes_preserve_only_typed_navigation_context()
    {
        var query = $"?workspace={WorkspaceId}&course={CourseId}&hold={HoldId}";
        Assert.Equal($"/notion/{ResourceId}{query}&published=true", WorkspaceNavigation.Page(ResourceId, WorkspaceId, CourseId, true, HoldId));
        Assert.Equal($"/notion{query}&published=true", WorkspaceNavigation.Editor(HoldId, WorkspaceId, CourseId, true));
        Assert.Equal($"/notion/graph/{ResourceId}{query}", WorkspaceNavigation.Graph(ResourceId, WorkspaceId, CourseId, holdId: HoldId));
        Assert.Equal($"/notion/holds/{HoldId}/graph{query}", WorkspaceNavigation.HoldGraph(HoldId, WorkspaceId, CourseId));
        Assert.Equal($"/quests/{ResourceId}{query}&submission={ResourceId}", WorkspaceNavigation.Quest(ResourceId, WorkspaceId, CourseId, HoldId, ResourceId));
        Assert.Equal($"/teacher/polls/{ResourceId}/present{query}", WorkspaceNavigation.Poll(ResourceId, WorkspaceId, CourseId, HoldId, true));
        Assert.Equal($"/holds/{HoldId}/roulette{query}&set={ResourceId}", WorkspaceNavigation.Roulette(HoldId, WorkspaceId, CourseId, ResourceId));
        Assert.Equal($"/quests/{ResourceId}", WorkspaceNavigation.Quest(ResourceId, Guid.Empty, CourseId));
    }

    [Fact]
    public void Material_and_group_deep_links_preserve_typed_targets_through_workspace_routes()
    {
        Assert.Equal($"/holds/{HoldId}?view=materials&material={ResourceId}", WorkspaceNavigation.Hold(HoldId, "materials", materialId: ResourceId));
        Assert.Equal($"/courses/{WorkspaceId}?view=materials&material={ResourceId}", WorkspaceNavigation.View(WorkspaceId, "materials", materialId: ResourceId));
        Assert.Equal($"/courses/{WorkspaceId}?view=groups&group={ResourceId}", WorkspaceNavigation.View(WorkspaceId, "groups", groupId: ResourceId));
        Assert.Equal($"/holds/{HoldId}?view=groups", WorkspaceNavigation.Hold(HoldId, "groups", groupId: Guid.Empty));
    }

    [Fact]
    public void Return_context_uses_the_resources_actual_hold_and_rejects_a_foreign_or_deleted_selection()
    {
        var workspace = Workspace();
        Assert.Equal(new WorkspaceRouteContext(WorkspaceId, HoldId, CourseId), WorkspaceNavigation.Validate(workspace, HoldId, WorkspaceId, CourseId));
        Assert.Null(WorkspaceNavigation.Validate(workspace, Guid.NewGuid(), WorkspaceId, CourseId));
        Assert.Null(WorkspaceNavigation.Validate(workspace with { CourseId = Guid.Empty }, HoldId, WorkspaceId, CourseId));
        Assert.Equal(new WorkspaceRouteContext(WorkspaceId, HoldId, null), WorkspaceNavigation.Validate(workspace, HoldId, Guid.NewGuid(), CourseId));
        Assert.Equal(new WorkspaceRouteContext(WorkspaceId, HoldId, null), WorkspaceNavigation.Validate(workspace, HoldId, WorkspaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Resolution_uses_the_authorized_preview_endpoint_and_never_trusts_a_query_as_access()
    {
        using var handler = new WorkspaceHandler(Workspace());
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://lup.example/api/") };
        Assert.Equal(new WorkspaceRouteContext(WorkspaceId, HoldId, CourseId),
            await WorkspaceNavigation.Resolve(http, HoldId, WorkspaceId, CourseId, true, CancellationToken.None));
        Assert.Equal($"/api/hold-workspace/holds/{HoldId}?publishedOnly=true", handler.RequestedPath);
        handler.Forbidden = true;
        Assert.Null(await WorkspaceNavigation.Resolve(http, HoldId, WorkspaceId, CourseId, false, CancellationToken.None));
    }

    private static HoldWorkspaceView Workspace() => new(WorkspaceId, HoldId, "H3", null, true,
        0, 0, 0, 0, 0, 0, [], [], [], [], Courses: [new(CourseId, "Web API", "", null, null, true)]);

    private sealed class WorkspaceHandler(HoldWorkspaceView workspace) : HttpMessageHandler
    {
        public string? RequestedPath { get; private set; }
        public bool Forbidden { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedPath = request.RequestUri!.PathAndQuery;
            return Task.FromResult(Forbidden ? new HttpResponseMessage(HttpStatusCode.Forbidden)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(workspace) });
        }
    }
}
