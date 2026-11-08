using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Pages.Learning;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class AssignmentsStudentTests
{
    [Fact]
    public async Task Direct_quest_route_opens_its_hold_and_existing_editable_draft()
    {
        using var api = new AssignmentApi();
        await using var context = await LearningUiTestContext.CreateAsync(api);

        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));

        page.WaitForAssertion(() => Assert.Equal("Tidligere kladde", DraftText(page)));
        Assert.Equal(api.Quest.Title, page.Find("h1").TextContent.Trim());
        Assert.Contains(page.FindAll("h2"), h => h.TextContent.Trim() == "Din aflevering");
        Assert.DoesNotContain("En anden quest", page.Markup);
        Assert.DoesNotContain(page.FindAll("h2"), h => h.TextContent.Trim() is "Materialer" or "Quests");
        Assert.Contains(api.Requests, r => r.Path == $"groups?holdId={api.HoldId}");
        Assert.DoesNotContain(api.Requests, r => r.Path.Contains(api.OtherHoldId.ToString()));
        Assert.DoesNotContain(api.Requests, r => r.Path.StartsWith("quests/members"));
        Assert.Empty(api.Mutations);
    }

    [Fact]
    public async Task Submit_saves_the_text_typed_in_the_editor_before_submitting_the_new_version()
    {
        using var api = new AssignmentApi();
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));
        page.WaitForAssertion(() => Assert.Equal("Tidligere kladde", DraftText(page)));

        page.Find("textarea").Input("Min færdige besvarelse");
        Button(page, "Aflevér").Click();

        page.WaitForAssertion(() => Assert.Contains("Afleveringen er indsendt", page.Markup));
        Assert.Equal(new[] { "draft", "submit" }, api.Mutations.ToArray());
        Assert.Equal("Min færdige besvarelse", api.SavedDraft?.Text);
        Assert.Equal(3, api.SavedDraft?.Version);
        Assert.Equal(4, api.Submitted?.Version);
        Assert.Empty(page.FindAll("textarea"));
        Assert.Contains("Min færdige besvarelse", page.Markup);
        Assert.Empty(api.UnexpectedRequests);
    }

    [Fact]
    public async Task Failed_draft_save_keeps_the_typed_text_and_does_not_submit()
    {
        using var api = new AssignmentApi { FailDraftSave = true };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));
        page.WaitForAssertion(() => Assert.Equal("Tidligere kladde", DraftText(page)));

        page.Find("textarea").Input("Må ikke gå tabt");
        Button(page, "Aflevér").Click();

        page.WaitForAssertion(() => Assert.Contains("Indholdet er ændret", page.Find("[role='alert']").TextContent));
        Assert.Equal("Må ikke gå tabt", DraftText(page));
        Assert.Equal(new[] { "draft" }, api.Mutations.ToArray());
        Assert.Null(api.Submitted);
    }

    [Fact]
    public async Task Empty_draft_explains_what_is_needed_and_cannot_be_submitted()
    {
        using var api = new AssignmentApi();
        api.Submission = api.Submission with { DraftText = "" };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));

        page.WaitForAssertion(() => Assert.True(Button(page, "Aflevér").HasAttribute("disabled")));
        Assert.Contains("Skriv en tekst, eller tilføj mindst én fil.", page.Markup);
        Assert.Empty(api.Mutations);
    }

    [Fact]
    public async Task Group_only_quest_without_an_eligible_group_shows_help_and_disables_start()
    {
        using var api = new AssignmentApi { HasSubmission = false };
        api.Quest = api.Quest with { SubmissionMode = "group", GroupSetId = Guid.NewGuid() };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));

        page.WaitForAssertion(() => Assert.Contains("Du mangler en gruppe til denne quest.", page.Markup));
        Assert.True(Button(page, "Start aflevering").HasAttribute("disabled"));
        Assert.Contains(page.FindAll("a"), a => a.TextContent.Trim() == "Se mine grupper");
        Assert.DoesNotContain(api.Requests, r => r.Path.StartsWith("quests/members"));
        Assert.Empty(api.Mutations);
    }

    [Fact]
    public async Task Approved_group_submission_remains_a_receipt_when_the_group_is_no_longer_available()
    {
        using var api = new AssignmentApi();
        api.Quest = api.Quest with { SubmissionMode = "group", AllowedGroupIds = [] };
        var revision = new SubmissionRevisionView(Guid.NewGuid(), 1, "Gruppens løsning", Guid.NewGuid(),
            DateTimeOffset.UtcNow, [], [], []);
        api.Submission = api.Submission with { GroupId = Guid.NewGuid(), State = "approved", CanEdit = false, Revisions = [revision] };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));

        page.WaitForAssertion(() => Assert.Contains("Gruppens løsning", page.Markup));
        Assert.Contains("Godkendt", page.Markup);
        Assert.DoesNotContain("Du mangler en gruppe", page.Markup);
        Assert.Empty(page.FindAll("textarea"));
        Assert.Empty(api.Mutations);
    }

    [Fact]
    public async Task Teacher_preview_shows_published_quest_but_never_loads_personal_or_teacher_submission_data()
    {
        using var api = new AssignmentApi { Teacher = true };
        api.Quest = api.Quest with { CanManage = true };
        await using var context = await LearningUiTestContext.CreateAsync(api, teacher: true, preview: true);
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));

        page.WaitForAssertion(() => Assert.Contains(api.Quest.Description, page.Markup));
        Assert.Equal(api.Quest.Title, page.Find("h1").TextContent.Trim());
        Assert.Contains("Aflevering og personlig feedback kræver elevens egen konto.", page.Markup);
        Assert.Empty(page.FindAll("textarea, input[type=file]"));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() is "Start aflevering" or "Gem kladde" or "Aflevér");
        Assert.DoesNotContain(api.Requests, r => r.Path.StartsWith("submissions") || r.Path.StartsWith("groups") || r.Path.StartsWith("quests/members"));
        Assert.Empty(api.Mutations);
    }

    [Fact]
    public async Task Account_refresh_for_the_same_identity_preserves_unsaved_editor_text()
    {
        using var api = new AssignmentApi();
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));
        page.WaitForAssertion(() => Assert.Equal("Tidligere kladde", DraftText(page)));
        page.Find("textarea").Input("Stadig i gang");
        var requestCount = api.Requests.Count(r => !(r.Method == "GET" && r.Path.StartsWith("me/actions", StringComparison.Ordinal)));

        await page.InvokeAsync(context.Auth.InitializeAsync);

        page.WaitForAssertion(() => Assert.Equal("Stadig i gang", DraftText(page)));
        Assert.Equal(requestCount, api.Requests.Count(r => !(r.Method == "GET" && r.Path.StartsWith("me/actions", StringComparison.Ordinal))));
        Assert.Empty(api.Mutations);
    }

    [Fact]
    public async Task Leaving_a_dirty_draft_requires_confirmation_and_cancel_preserves_the_editor()
    {
        using var api = new AssignmentApi();
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));
        page.WaitForAssertion(() => Assert.Equal("Tidligere kladde", DraftText(page)));
        page.Find("textarea").Input("Arbejde jeg vil beholde");
        context.Javascript.ConfirmResult = false;
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();

        await page.InvokeAsync(() => navigation.NavigateTo("/courses"));

        page.WaitForAssertion(() => Assert.Equal(NavigationState.Prevented, navigation.History.First().State));
        Assert.Contains("ugemte ændringer", Assert.Single(context.Javascript.Confirmations));
        Assert.Equal("Arbejde jeg vil beholde", DraftText(page));
        Assert.Empty(api.Mutations);

        context.Javascript.ConfirmResult = true;
        await page.InvokeAsync(() => navigation.NavigateTo("/courses"));
        page.WaitForAssertion(() => Assert.Equal(NavigationState.Succeeded, navigation.History.First().State));
    }

    private static string DraftText(IRenderedComponent<Assignments> page)
        => page.Find("textarea").GetAttribute("value") ?? page.Find("textarea").TextContent;

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<Assignments> page, string text)
        => page.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private sealed class AssignmentApi : HttpMessageHandler
    {
        public Guid HoldId { get; } = Guid.NewGuid();
        public Guid OtherHoldId { get; } = Guid.NewGuid();
        public QuestView Quest { get; set; }
        public SubmissionView Submission { get; set; }
        public bool Teacher { get; init; }
        public bool HasSubmission { get; init; } = true;
        public bool FailDraftSave { get; init; }
        public SaveSubmissionDraft? SavedDraft { get; private set; }
        public SubmitSubmission? Submitted { get; private set; }
        public ConcurrentQueue<(string Method, string Path)> Requests { get; } = [];
        public ConcurrentQueue<string> Mutations { get; } = [];
        public ConcurrentQueue<string> UnexpectedRequests { get; } = [];

        public AssignmentApi()
        {
            Quest = new(Guid.NewGuid(), HoldId, "Byg din første API", "Lav et endpoint og forklar dit valg.",
                "individual", true, null, false);
            Submission = new(Guid.NewGuid(), Quest.Id, null, "draft", 3, "Tidligere kladde", [], [], true, false);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery["/api/".Length..];
            Requests.Enqueue((request.Method.Method, path));
            if (request.Method == HttpMethod.Get)
            {
                if (path == "quests/holds") return Json(new LearningHold[] { new(OtherHoldId, "Et andet hold", Teacher), new(HoldId, "H2 Web", Teacher) });
                if (path == $"quests/{Quest.Id}") return Json(Quest);
                if (path == $"quests?holdId={HoldId}") return Json(new[] { Quest, Quest with { Id = Guid.NewGuid(), Title = "En anden quest" } });
                if (path == $"materials?holdId={HoldId}") return Json(Array.Empty<CourseMaterialView>());
                if (path == $"groups?holdId={HoldId}") return Json(Array.Empty<LearningGroupView>());
                if (path == $"quests/members?holdId={HoldId}") return Json(Array.Empty<LearningMember>());
                if (path == $"submissions?questId={Quest.Id}") return Json(HasSubmission ? new[] { Submission } : []);
                if (path == $"submissions/{Submission.Id}") return Json(Submission);
                if (path == "emojis") return Json(Array.Empty<object>());
                if (path.StartsWith("me/actions", StringComparison.Ordinal)) return Json(new ActionCenterSnapshot(DateTimeOffset.UtcNow, []));
            }
            if (request.Method == HttpMethod.Put && path == $"submissions/{Submission.Id}/draft")
            {
                Mutations.Enqueue("draft");
                SavedDraft = await request.Content!.ReadFromJsonAsync<SaveSubmissionDraft>(cancellationToken);
                if (FailDraftSave) return new(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { error = "version_conflict" }) };
                Submission = Submission with { DraftText = SavedDraft!.Text, Version = Submission.Version + 1 };
                return Json(Submission);
            }
            if (request.Method == HttpMethod.Post && path == $"submissions/{Submission.Id}/submit")
            {
                Mutations.Enqueue("submit");
                Submitted = await request.Content!.ReadFromJsonAsync<SubmitSubmission>(cancellationToken);
                var revision = new SubmissionRevisionView(Guid.NewGuid(), 1, Submission.DraftText,
                    LearningUiTestContext.AccountId, DateTimeOffset.UtcNow, [],
                    [new(Guid.NewGuid(), LearningUiTestContext.AccountId, "Testelev")], []);
                Submission = Submission with { State = "submitted", Version = Submission.Version + 1, CanEdit = false, Revisions = [revision] };
                return Json(Submission);
            }
            UnexpectedRequests.Enqueue($"{request.Method} {path}");
            return new(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { error = "test_endpoint_missing" }) };
        }

        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}
