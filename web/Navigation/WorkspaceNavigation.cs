using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Navigation;

/// <summary>Only fixed local routes and typed IDs; query context never grants access.</summary>
public static class WorkspaceNavigation
{
    /// <summary>Personal notebooks remain available independently of hold or course selection.</summary>
    public static IReadOnlyList<NotebookNavigationLink> NotebookLinks(bool teacher) => teacher
        ? [new("Elevnotesbøger", "/teacher/notebooks"), new("Logbogsskabeloner", "/teacher/notebook-templates")]
        : [new("Min notesbog", "/notebook"), new("Min logbog", "/journal")];

    private static readonly HashSet<string> Views = ["overview", "content", "quests", "submissions", "calendar", "groups", "materials", "settings", "polls"];
    private static readonly HashSet<string> Statuses = ["missing", "overdue", "submitted", "approved", "changes-requested"];
    public static Guid? NonEmpty(Guid? id) => id is Guid value && value != Guid.Empty ? value : null;
    public static string NormalizeView(string? view) => view is not null && Views.Contains(view) ? view : "overview";
    public static string PrimarySection(string? view) => NormalizeView(view) switch
    {
        "submissions" => "quests", "materials" or "polls" => "content", "settings" => "groups", var known => known
    };

    public static string View(Guid workspaceId, string view = "overview", Guid? courseId = null, bool create = false,
        Guid? questId = null, string? status = null, Guid? eventId = null) => workspaceId == Guid.Empty ? "/courses"
        : WorkspacePath($"/courses/{workspaceId:D}", view, courseId, create, questId, status, eventId);
    public static string Hold(Guid holdId, string view = "overview", Guid? courseId = null, bool create = false,
        Guid? questId = null, string? status = null, Guid? eventId = null) => holdId == Guid.Empty ? "/courses"
        : WorkspacePath($"/holds/{holdId:D}", view, courseId, create, questId, status, eventId);
    private static string WorkspacePath(string path, string view, Guid? courseId, bool create, Guid? questId, string? status, Guid? eventId)
    {
        var query = new List<string> { "view=" + NormalizeView(view) };
        AddId(query, "course", courseId);
        if (create) query.Add("create=true");
        AddId(query, "quest", questId);
        if (status is not null && Statuses.Contains(status)) query.Add("status=" + status);
        AddId(query, "event", eventId);
        return path + "?" + string.Join("&", query);
    }

    public static string Page(Guid pageId, Guid? workspaceId = null, Guid? courseId = null, bool publishedOnly = false, Guid? holdId = null) =>
        ContextPath($"/notion/{pageId:D}", workspaceId, courseId, holdId, publishedOnly);
    public static string Editor(Guid holdId, Guid? workspaceId = null, Guid? courseId = null, bool publishedOnly = false) =>
        ContextPath("/notion", workspaceId, courseId, holdId, publishedOnly);
    public static string Graph(Guid pageId, Guid? workspaceId = null, Guid? courseId = null, bool publishedOnly = false, Guid? holdId = null) =>
        ContextPath($"/notion/graph/{pageId:D}", workspaceId, courseId, holdId, publishedOnly);
    public static string HoldGraph(Guid holdId, Guid? workspaceId = null, Guid? courseId = null, bool publishedOnly = false) =>
        ContextPath($"/notion/holds/{holdId:D}/graph", workspaceId, courseId, holdId, publishedOnly);
    public static string Student(Guid holdId, Guid memberId, Guid? workspaceId = null, Guid? courseId = null) =>
        ContextPath($"/notion/holds/{holdId:D}/students/{memberId:D}", workspaceId, courseId, holdId);
    public static string Groups(Guid holdId, Guid? workspaceId = null, Guid? courseId = null, IEnumerable<Guid>? members = null)
    {
        var query = ContextQuery(workspaceId, courseId, holdId);
        var selected = (members ?? []).Where(id => id != Guid.Empty).Distinct().Take(100).ToArray();
        if (selected.Length > 0) query.Add("members=" + string.Join(",", selected.Select(id => id.ToString("D"))));
        return Join($"/notion/holds/{holdId:D}/groups", query);
    }
    public static string Roulette(Guid holdId, Guid? workspaceId = null, Guid? courseId = null, Guid? setId = null)
    {
        var query = ContextQuery(workspaceId, courseId, holdId);
        AddId(query, "set", setId);
        return Join($"/holds/{holdId:D}/roulette", query);
    }
    public static string GraphIndex(Guid holdId, bool publishedOnly = false) =>
        ContextPath("/notion/graph", null, null, holdId, publishedOnly);
    public static string Quest(Guid questId, Guid? workspaceId = null, Guid? courseId = null, Guid? holdId = null, Guid? submissionId = null)
    {
        var query = ContextQuery(workspaceId, courseId, holdId);
        AddId(query, "submission", submissionId);
        return Join($"/quests/{questId:D}", query);
    }
    public static string Poll(Guid pollId, Guid? workspaceId = null, Guid? courseId = null, Guid? holdId = null, bool present = false) =>
        ContextPath(present ? $"/teacher/polls/{pollId:D}/present" : $"/polls/{pollId:D}", workspaceId, courseId, holdId);
    public static string PollIndex(Guid? workspaceId = null, Guid? courseId = null, Guid? holdId = null, bool create = false, string? kind = null)
    {
        var query = ContextQuery(workspaceId, courseId, holdId);
        if (kind is "mood" or "choice" or "scale" or "text") query.Add("type=" + kind);
        return Join(create ? "/teacher/polls/new" : "/teacher/polls", query);
    }
    private static string ContextPath(string path, Guid? workspaceId, Guid? courseId, Guid? holdId, bool publishedOnly = false)
    {
        var query = ContextQuery(workspaceId, courseId, holdId);
        if (publishedOnly) query.Add("published=true");
        return Join(path, query);
    }
    private static List<string> ContextQuery(Guid? workspaceId, Guid? courseId, Guid? holdId)
    {
        var query = new List<string>();
        AddId(query, "workspace", workspaceId);
        // Course context is meaningful only alongside a particular hold/workspace.
        if (NonEmpty(workspaceId) is not null || NonEmpty(holdId) is not null) AddId(query, "course", courseId);
        AddId(query, "hold", holdId);
        return query;
    }
    private static void AddId(List<string> query, string name, Guid? id) { if (NonEmpty(id) is Guid value) query.Add(name + "=" + value.ToString("D")); }
    private static string Join(string path, List<string> query) => query.Count == 0 ? path : path + "?" + string.Join("&", query);

    public static WorkspaceRouteContext? Validate(HoldWorkspaceView workspace, Guid holdId, Guid? requestedWorkspace, Guid? requestedCourse)
    {
        if (holdId == Guid.Empty || workspace.HoldId != holdId || workspace.CourseId == Guid.Empty) return null;
        var matches = NonEmpty(requestedWorkspace) is not Guid expected || expected == workspace.CourseId;
        var course = matches && workspace.Courses?.Any(c => c.Id == requestedCourse) == true ? NonEmpty(requestedCourse) : null;
        return new(workspace.CourseId, workspace.HoldId, course);
    }
    public static async Task<WorkspaceRouteContext?> Resolve(HttpClient http, Guid holdId, Guid? workspaceId, Guid? courseId, bool publishedOnly, CancellationToken ct)
    {
        if (holdId == Guid.Empty) return null;
        try
        {
            using var response = await http.GetAsync($"hold-workspace/holds/{holdId:D}?publishedOnly={publishedOnly.ToString().ToLowerInvariant()}", ct);
            if (!response.IsSuccessStatusCode) return null;
            var workspace = await response.Content.ReadFromJsonAsync<HoldWorkspaceView>(ct);
            return workspace is null ? null : Validate(workspace, holdId, workspaceId, courseId);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException) { return null; }
    }
}

public sealed record NotebookNavigationLink(string Label, string Href);

public sealed record WorkspaceRouteContext(Guid WorkspaceId, Guid HoldId, Guid? CourseId)
{
    public string View(string view = "content") => WorkspaceNavigation.View(WorkspaceId, view, CourseId);
}
