using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Activity;
using Mercantec.Tech.Web.Notebooks;
using Mercantec.Tech.Web.Notion;
using Mercantec.Tech.Web.Pages.Learning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NotebookPage = Mercantec.Tech.Web.Pages.Notebook.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class ReadNotificationUiTests
{
    private static async Task<LearningUiTestContext> Context(Api api, bool preview = false) =>
        await LearningUiTestContext.CreateAsync(api, teacher: preview, preview: preview, configure: services =>
        {
            var original = (IJSRuntime)services.Last(s => s.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            services.AddSingleton<IJSRuntime>(new EditorRuntime(original));
            services.AddSingleton<NotebookClient>(); services.AddSingleton<NotionClient>();
        });

    private static void Navigate(LearningUiTestContext context, string path) => context.Services.GetRequiredService<NavigationManager>().NavigateTo(path);

    [Fact]
    public async Task Feedback_is_acknowledged_only_in_open_revision_panels_including_normal_entry()
    {
        using var api = new Api(); await using var context = await Context(api);
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));
        page.WaitForAssertion(() => Assert.Contains(api.LatestFeedback.Id, api.Read.Select(x => x.ResourceId)));
        Assert.DoesNotContain(api.OldFeedback.Id, api.Read.Select(x => x.ResourceId));
        var oldPanel = page.FindAll(".assignment-revision").Single(panel => panel.QuerySelector("summary")!.TextContent.StartsWith("Version 1", StringComparison.Ordinal));
        Assert.False(oldPanel.HasAttribute("open"));

        oldPanel.QuerySelector("summary")!.Click();

        page.WaitForAssertion(() => Assert.Contains(api.OldFeedback.Id, api.Read.Select(x => x.ResourceId)));
        Assert.False(api.ReadBeforeContent);
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task A_feedback_notice_opens_its_historical_revision_without_reading_hidden_newer_feedback()
    {
        using var api = new Api(); await using var context = await Context(api);
        Navigate(context, $"/quests/{api.Quest.Id}?submission={api.Submission.Id}&notice={api.OldFeedback.Id}");
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));
        page.WaitForAssertion(() => Assert.Contains(api.OldFeedback.Id, api.Read.Select(x => x.ResourceId)));
        Assert.DoesNotContain(api.LatestFeedback.Id, api.Read.Select(x => x.ResourceId));
        var open = Assert.Single(page.FindAll(".assignment-revision[open]"));
        Assert.StartsWith("Version 1", open.QuerySelector("summary")!.TextContent);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("99999999-9999-4999-8999-999999999999")]
    public async Task Unknown_feedback_notices_cannot_create_read_receipts(string notice)
    {
        using var api = new Api(); await using var context = await Context(api);
        Navigate(context, $"/quests/{api.Quest.Id}?notice={Uri.EscapeDataString(notice)}");
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".assignment-revision").Count));
        await page.InvokeAsync(() => Task.CompletedTask);
        Assert.Empty(api.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_submission_reads_and_teacher_preview_never_acknowledge_feedback(bool preview)
    {
        using var api = new Api { FailSubmission = !preview }; await using var context = await Context(api, preview);
        Navigate(context, $"/quests/{api.Quest.Id}?notice={api.LatestFeedback.Id}");
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));
        if (preview) page.WaitForAssertion(() => Assert.Single(page.FindAll(".assignment-preview")));
        else page.WaitForAssertion(() => Assert.Single(page.FindAll("[role='alert']")));
        await page.InvokeAsync(() => Task.CompletedTask);
        Assert.Empty(api.Attempts);
    }

    [Fact]
    public async Task A_failed_acknowledgement_can_be_retried_after_refresh_instead_of_being_cached_as_read()
    {
        using var api = new Api { FailReceipt = true }; await using var context = await Context(api);
        var page = context.Render<Assignments>(p => p.Add(x => x.QuestId, api.Quest.Id));
        page.WaitForAssertion(() => Assert.Single(api.Attempts));
        Assert.Empty(api.Read);
        api.FailReceipt = false;
        page.FindAll("button").Single(button => button.TextContent.Trim() == "Opdatér").Click();
        page.WaitForAssertion(() => Assert.Contains(api.LatestFeedback.Id, api.Read.Select(x => x.ResourceId)));
        Assert.Equal(2, api.Attempts.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Notebook_comments_are_read_only_when_the_comments_panel_is_actually_shown(bool query)
    {
        using var api = new Api(); await using var context = await Context(api);
        Navigate(context, $"/notebook/{api.Page.Id}" + (query ? "?comments=true" : ""));
        var page = context.Render<NotebookPage>(p => p.Add(x => x.PageId, api.Page.Id));
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".nb-document-bar")));
        if (!query)
        {
            Assert.Empty(api.Attempts);
            Assert.Equal(0, api.CommentReads);
            page.FindAll("button").Single(button => button.TextContent.Trim() == "Kommentarer").Click();
        }
        page.WaitForAssertion(() => Assert.Contains(api.Comment.Id, api.Read.Select(x => x.ResourceId)));
        Assert.Single(page.FindAll(".nb-comments"));
        Assert.DoesNotContain(api.OwnComment.Id, api.Read.Select(x => x.ResourceId));
        Assert.False(api.ReadBeforeContent);
        Assert.Empty(api.Unexpected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_student_readers_and_failed_comment_reads_do_not_acknowledge_hidden_comments(bool failure)
    {
        using var api = new Api { CanComment = failure, FailComments = failure }; await using var context = await Context(api);
        Navigate(context, $"/notebook/{api.Page.Id}?comments=true&noticeKind=notebook-comment&notice={api.Comment.Id}");
        var page = context.Render<NotebookPage>(p => p.Add(x => x.PageId, api.Page.Id));
        if (failure) page.WaitForAssertion(() => Assert.Single(page.FindAll("[role='alert']")));
        else
        {
            page.WaitForAssertion(() => Assert.Single(page.FindAll(".nb-document-bar")));
            Assert.Equal(0, api.CommentReads);
            Assert.DoesNotContain(page.FindAll("button"), button => button.TextContent.Trim() is "Kommentarer" or "Del");
        }
        await page.InvokeAsync(() => Task.CompletedTask);
        Assert.Empty(api.Attempts);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("99999999-9999-4999-8999-999999999999")]
    public async Task Unknown_comment_notices_do_not_acknowledge_a_different_comment_on_the_page(string notice)
    {
        using var api = new Api(); await using var context = await Context(api);
        Navigate(context, $"/notebook/{api.Page.Id}?comments=true&noticeKind=notebook-comment&notice={Uri.EscapeDataString(notice)}");
        var page = context.Render<NotebookPage>(p => p.Add(x => x.PageId, api.Page.Id));
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".nb-comments")));
        await page.InvokeAsync(() => Task.CompletedTask);
        Assert.Empty(api.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Share_notices_require_a_successful_read_of_the_correct_page_or_notebook(bool wholeNotebook)
    {
        using var api = new Api { EmptyNotebook = wholeNotebook };
        var share = Guid.NewGuid();
        api.Actions = [new("notebook-share", "optional", null, null, null, share, null, "Delt med dig", "Notesbog", null,
            State: wholeNotebook ? "shared-notebook" : "shared-page", TargetId: wholeNotebook ? api.Notebook : api.Page.Id)];
        await using var context = await Context(api);
        Navigate(context, wholeNotebook ? $"/notebook?owner={api.Notebook}&noticeKind=notebook-share&notice={share}"
            : $"/notebook/{api.Page.Id}?noticeKind=notebook-share&notice={share}");
        var page = context.Render<NotebookPage>(p => p.Add(x => x.PageId, wholeNotebook ? null : api.Page.Id));
        page.WaitForAssertion(() => Assert.Contains(api.Read, item => item.Kind == "notebook-share" && item.ResourceId == share));
        Assert.True(api.PageListRead);
        Assert.False(api.ReadBeforeContent);
        Assert.Empty(api.Unexpected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_or_wrong_target_share_notices_never_acknowledge_an_unrelated_share(bool wrongTarget)
    {
        using var api = new Api(); var share = Guid.NewGuid();
        api.Actions = [new("notebook-share", "optional", null, null, null, share, null, "Delt", "Notesbog", null,
            State: "shared-page", TargetId: wrongTarget ? Guid.NewGuid() : api.Page.Id)];
        await using var context = await Context(api);
        Navigate(context, $"/notebook/{api.Page.Id}?noticeKind=notebook-share&notice={(wrongTarget ? share : Guid.NewGuid())}");
        var page = context.Render<NotebookPage>(p => p.Add(x => x.PageId, api.Page.Id));
        page.WaitForAssertion(() => Assert.True(api.ActionReads > 0));
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".nb-document-bar")));
        await page.InvokeAsync(() => Task.CompletedTask);
        Assert.Empty(api.Attempts);
    }

    [Fact]
    public async Task Notebook_preview_never_loads_or_acknowledges_a_target_users_comments()
    {
        using var api = new Api(); await using var context = await Context(api, preview: true);
        Navigate(context, $"/notebook/{api.Page.Id}?comments=true&noticeKind=notebook-comment&notice={api.Comment.Id}");
        var page = context.Render<NotebookPage>(p => p.Add(x => x.PageId, api.Page.Id));
        page.WaitForAssertion(() => Assert.Contains("Afslut elevvisningen", page.Markup));
        Assert.Equal(0, api.CommentReads);
        Assert.Empty(api.Attempts);
    }

    private sealed class EditorRuntime(IJSRuntime storage) : IJSRuntime
    {
        private readonly Module _module = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args) => InvokeAsync<TValue>(id, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args) => id == "import"
            ? ValueTask.FromResult((TValue)(object)_module) : storage.InvokeAsync<TValue>(id, ct, args);
        private sealed class Module : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args) => ValueTask.FromResult(default(TValue)!);
            public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(TValue)!);
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class Api : HttpMessageHandler
    {
        public Guid Hold { get; } = Guid.NewGuid();
        public Guid Notebook { get; } = Guid.NewGuid();
        private readonly Guid _ownNotebook = Guid.NewGuid();
        public QuestView Quest { get; }
        public SubmissionView Submission { get; }
        public SubmissionFeedbackView OldFeedback { get; } = new(Guid.NewGuid(), Guid.NewGuid(), null, "Tidligere feedback", "approved", DateTimeOffset.UtcNow.AddDays(-2), []);
        public SubmissionFeedbackView LatestFeedback { get; } = new(Guid.NewGuid(), Guid.NewGuid(), null, "Nyeste feedback", "approved", DateTimeOffset.UtcNow, []);
        public NotebookPageSummary Page { get; }
        public NotebookCommentView Comment { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "Underviser", "Læs denne kommentar", DateTimeOffset.UtcNow);
        public NotebookCommentView OwnComment { get; } = new(Guid.NewGuid(), LearningUiTestContext.AccountId, "Mig", "Min egen kommentar", DateTimeOffset.UtcNow);
        public bool FailSubmission, FailComments, FailReceipt, EmptyNotebook;
        public bool CanComment = true;
        public bool PageListRead, SubmissionRead, ContextRead, ReadBeforeContent;
        public int CommentReads, ActionReads;
        public ActionCenterItem[] Actions { get; set; } = [];
        public ConcurrentQueue<ActionCenterReadItem[]> Attempts { get; } = [];
        public ConcurrentQueue<ActionCenterReadItem> Read { get; } = [];
        public ConcurrentQueue<string> Unexpected { get; } = [];
        public Api()
        {
            Quest = new(Guid.NewGuid(), Hold, "API-opgaven", "Beskriv løsningen.", "individual", true, null, false);
            var participant = new SubmissionParticipantView(Guid.NewGuid(), LearningUiTestContext.AccountId, "Testelev");
            Submission = new(Guid.NewGuid(), Quest.Id, null, "approved", 3, "", [],
                [new(Guid.NewGuid(), 1, "Første version", LearningUiTestContext.AccountId, DateTimeOffset.UtcNow.AddDays(-3), [], [participant], [OldFeedback]),
                 new(Guid.NewGuid(), 2, "Seneste version", LearningUiTestContext.AccountId, DateTimeOffset.UtcNow.AddDays(-1), [], [participant], [LatestFeedback])], false, false);
            Page = new(Guid.NewGuid(), Notebook, "Noter om API", null, "note", [], null, null, null, DateTimeOffset.UtcNow, 1);
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/me/actions/read" && request.Method == HttpMethod.Post)
            {
                var batch = (await request.Content!.ReadFromJsonAsync<ActionCenterReadRequest>(ct))!.Items;
                Attempts.Enqueue(batch);
                if (batch.Any(item => item.Kind == "feedback" && !SubmissionRead || item.Kind == "notebook-comment" && CommentReads == 0 || item.Kind == "notebook-share" && !PageListRead)) ReadBeforeContent = true;
                if (FailReceipt) return new(HttpStatusCode.ServiceUnavailable);
                foreach (var item in batch) Read.Enqueue(item);
                Actions = Actions.Where(action => !batch.Any(item => item.Kind == action.Kind && item.ResourceId == action.ResourceId)).ToArray();
                return new(HttpStatusCode.NoContent);
            }
            if (path == "/api/me/actions") { ActionReads++; return Json(new ActionCenterSnapshot(DateTimeOffset.UtcNow, Actions)); }
            if (path == "/api/quests/holds") return Json(new[] { new LearningHold(Hold, "H3", false) });
            if (path == $"/api/quests/{Quest.Id}") return Json(Quest);
            if (path == "/api/quests") return Json(new[] { Quest });
            if (path == "/api/materials") return Json(Array.Empty<CourseMaterialView>());
            if (path == "/api/groups") return Json(Array.Empty<LearningGroupView>());
            if (path == "/api/submissions") return Json(new[] { Submission });
            if (path == $"/api/submissions/{Submission.Id}")
            {
                if (FailSubmission) return new(HttpStatusCode.NotFound);
                SubmissionRead = true; return Json(Submission);
            }
            if (path == "/api/emojis") return Json(Array.Empty<object>());
            if (path == "/api/notebooks/profile") return new(HttpStatusCode.NoContent);
            if (path == "/api/notebooks/mine") return Json(new NotebookInfo(CanComment ? Notebook : _ownNotebook, LearningUiTestContext.AccountId, "Testelev", true, true));
            if (path == "/api/notebooks/today") return Json(new DateOnly(2026, 11, 7));
            if (path == "/api/notebooks/templates") return Json(Array.Empty<NotebookTemplateView>());
            if (path == "/api/notebooks/shared") return Json(Array.Empty<NotebookPageSummary>());
            if (path == "/api/notion/holds") return Json(Array.Empty<NotionHold>());
            if (path == $"/api/notebooks/{Notebook}/pages") { PageListRead = true; return Json(new NotebookPageList(EmptyNotebook ? [] : [Page], EmptyNotebook ? 0 : 1)); }
            if (path == $"/api/notebooks/pages/{Page.Id}/context")
            {
                ContextRead = true;
                return Json(new NotebookDocumentContext(Page.Id, Notebook, Page.Title, "note", true, false, CanComment, false, "AA==", DateTimeOffset.UtcNow, 1, null, [new(Page.Id, Page.Title, null)], []));
            }
            if (path == $"/api/notebooks/pages/{Page.Id}/backlinks") return Json(Array.Empty<NotebookPageSummary>());
            if (path == $"/api/notebooks/pages/{Page.Id}/comments")
            {
                if (FailComments) return new(HttpStatusCode.ServiceUnavailable);
                CommentReads++; return Json(new[] { Comment, OwnComment });
            }
            Unexpected.Enqueue(request.Method + " " + path); return new(HttpStatusCode.NotFound);
        }
        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
