using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Polls;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TeacherPolls = Mercantec.Tech.Web.Pages.Teacher.Polls;

namespace Mercantec.Tech.Web.Tests;

public sealed class LegacyPollNavigationTests
{
    [Fact]
    public async Task Editor_creation_locks_the_requested_hold_and_returns_to_its_selected_course()
    {
        using var api = new PollApi();
        using var context = await CreateContext(api);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/teacher/polls/new?workspace={api.WorkspaceId}&course={api.CourseId}&hold={api.SelectedHold}");
        var page = context.Render<TeacherPolls>();

        page.WaitForAssertion(() => Assert.Equal(api.SelectedHold, page.FindComponent<PollStudio>().Instance.FixedHoldId));
        Assert.Equal(api.SelectedHold, Assert.Single(page.FindComponent<PollStudio>().Instance.Holds).Id);
        Assert.DoesNotContain("Et andet hold", page.Find(".poll-editor").TextContent);
        Assert.Contains(api.Requests, r => r == $"GET polls?holdId={api.SelectedHold}");
        Assert.DoesNotContain("GET polls", api.Requests);
        var back = page.FindAll("a").Single(a => a.TextContent.StartsWith("Tilbage til", StringComparison.Ordinal));
        var expected = $"/courses/{api.WorkspaceId}?view=polls&course={api.CourseId}";
        Assert.Equal(expected, back.GetAttribute("href"));

        page.FindAll("button").Single(b => b.TextContent.Trim() == "Start afstemning").Click();
        page.WaitForAssertion(() => Assert.Equal(api.SelectedHold, api.Created?.HoldId));
        page.WaitForAssertion(() => Assert.EndsWith(expected, navigation.Uri));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("99999999-9999-4999-8999-999999999999")]
    public async Task Invalid_or_inaccessible_hold_never_opens_a_form_for_the_first_available_hold(string hold)
    {
        using var api = new PollApi();
        using var context = await CreateContext(api);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/teacher/polls/new?hold={hold}");
        var page = context.Render<TeacherPolls>();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindComponents<PollStudio>());
        Assert.DoesNotContain("GET polls", api.Requests);
        Assert.DoesNotContain(api.Requests, r => r.StartsWith("POST", StringComparison.Ordinal));
        Assert.Null(api.Created);
    }

    [Fact]
    public async Task Old_global_creation_without_hold_keeps_the_hold_picker()
    {
        using var api = new PollApi();
        using var context = await CreateContext(api);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/teacher/polls/new");
        var page = context.Render<TeacherPolls>();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindComponent<PollStudio>().Instance.Holds.Count));
        Assert.Null(page.FindComponent<PollStudio>().Instance.FixedHoldId);
        Assert.Contains("GET polls", api.Requests);
        Assert.Contains(page.FindAll("select option"), option => option.TextContent == "Et andet hold");
        Assert.Contains(page.FindAll("select option"), option => option.TextContent == "Det valgte hold");
    }

    [Fact]
    public async Task Teacher_preview_cannot_load_or_create_polls_from_the_legacy_route()
    {
        using var api = new PollApi();
        using var context = await LearningUiTestContext.CreateAsync(api, teacher: true, preview: true,
            configure: services => services.AddSingleton<PollClient>());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/teacher/polls/new?hold={api.SelectedHold}");
        var page = context.Render<TeacherPolls>();

        Assert.Empty(page.FindComponents<PollStudio>());
        Assert.Empty(api.Requests);
        Assert.Null(api.Created);
    }

    private static Task<LearningUiTestContext> CreateContext(PollApi api) => LearningUiTestContext.CreateAsync(api,
        teacher: true, configure: services => services.AddSingleton<PollClient>());

    private sealed class PollApi : HttpMessageHandler
    {
        public Guid SelectedHold { get; } = Guid.Parse("22222222-2222-4222-8222-222222222222");
        public Guid OtherHold { get; } = Guid.Parse("55555555-5555-4555-8555-555555555555");
        public Guid WorkspaceId { get; } = Guid.Parse("33333333-3333-4333-8333-333333333333");
        public Guid CourseId { get; } = Guid.Parse("44444444-4444-4444-8444-444444444444");
        public ConcurrentQueue<string> Requests { get; } = new();
        public CreatePoll? Created { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery["/api/".Length..];
            Requests.Enqueue($"{request.Method} {path}");
            if (path == "polls/holds") return Json(new[] { new PollHold(OtherHold, "Et andet hold", true), new PollHold(SelectedHold, "Det valgte hold", true) });
            if (path == "polls" && request.Method == HttpMethod.Get || path == $"polls?holdId={SelectedHold}") return Json(Array.Empty<PollView>());
            if (path == $"hold-workspace/holds/{SelectedHold}?publishedOnly=false") return Json(new HoldWorkspaceView(WorkspaceId, SelectedHold, "Det valgte hold", null, true,
                0, 0, 0, 0, 0, 0, [], [], [], [], Courses: [new(CourseId, "Web API", "", null, null, true)]));
            if (path == "polls" && request.Method == HttpMethod.Post)
            {
                Created = await request.Content!.ReadFromJsonAsync<CreatePoll>(cancellationToken);
                return Json(new PollView(Guid.NewGuid(), Created!.HoldId, "Det valgte hold", Created.Question, Created.Kind,
                    PollDesign.MoodNames, Created.State, Created.ResultsMode, true, 2, false, null, null, 0, new int[5], null, DateTime.UtcNow));
            }
            throw new InvalidOperationException($"Unexpected request: {request.Method} {path}");
        }
        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
