using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Activity;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class ActionCenterCoverageTests
{
    private static readonly Guid Hold = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Workspace = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static ActionCenterItem Personal(string kind, Guid? target = null, string? state = null) => new(kind, "optional", null, null, null,
        Guid.NewGuid(), null, "Personlig besked", "Personligt", null, State: state, TargetId: target);
    private static ActionCenterItem CourseItem(string kind, string severity = "optional", Guid? target = null, Guid? submission = null) => new(kind,
        severity, Hold, Workspace, Guid.NewGuid(), Guid.NewGuid(), submission, "Min opgave", "H3", null, State: "unread", TargetId: target);

    [Fact]
    public async Task Personal_gift_without_a_hold_is_an_actual_optional_action_and_links_directly_to_the_gift()
    {
        var gift = Personal("gift", state: "unopened");
        using var api = new Api { Items = [gift] };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        Assert.Equal("me/actions?version=2", api.LastGet);
        Assert.Equal(ActionCenterStatus.Optional, service.Status);
        Assert.Equal($"/gifts/{gift.ResourceId}", ActionCenterPresentation.Href(Assert.Single(service.Items)));
        Assert.Equal("Uåbnet gave", ActionCenterPresentation.KindLabel(gift));
    }

    [Fact]
    public async Task Every_supported_news_destination_uses_typed_source_and_target_identifiers()
    {
        var page = Guid.NewGuid(); var notebook = Guid.NewGuid(); var quest = Guid.NewGuid(); var submission = Guid.NewGuid();
        var comment = Personal("notebook-comment", page, "unread");
        var share = Personal("notebook-share", notebook, "shared-notebook");
        var feedback = CourseItem("feedback", target: quest, submission: submission);
        var sideQuest = CourseItem("quest") with { State = "side" };
        using var api = new Api { Items = [comment, share, feedback, sideQuest] };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        Assert.Equal(4, service.OptionalCount);
        Assert.Equal($"/notebook/{page}?noticeKind=notebook-comment&notice={comment.ResourceId}&comments=true", ActionCenterPresentation.Href(comment));
        Assert.Equal($"/notebook?owner={notebook}&noticeKind=notebook-share&notice={share.ResourceId}", ActionCenterPresentation.Href(share));
        Assert.Equal($"/quests/{quest}?workspace={Workspace}&course={feedback.CourseId}&hold={Hold}&submission={submission}&notice={feedback.ResourceId}", ActionCenterPresentation.Href(feedback));
        Assert.Equal("Side quest", ActionCenterPresentation.KindLabel(sideQuest));
    }

    [Theory]
    [InlineData("gift")]
    [InlineData("notebook-comment")]
    [InlineData("feedback")]
    public async Task Malformed_personal_or_target_context_never_turns_into_a_clear_or_misrouted_snapshot(string kind)
    {
        var item = kind == "gift" ? Personal(kind, state: "unopened") with { HoldId = Hold }
            : kind == "feedback" ? CourseItem(kind) : Personal(kind);
        using var api = new Api { Items = [item] };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        Assert.Equal(ActionCenterStatus.Unknown, service.Status);
        Assert.Null(service.Snapshot);
    }

    [Fact]
    public async Task Reading_news_clears_only_receipted_items_and_refreshes_the_shared_snapshot()
    {
        var feedback = CourseItem("feedback", target: Guid.NewGuid(), submission: Guid.NewGuid());
        var missing = CourseItem("submission", "important");
        using var api = new Api { Items = [feedback, missing] };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        Assert.True(await service.MarkReadAsync("feedback", feedback.ResourceId));
        Assert.Equal("submission", Assert.Single(service.Items).Kind);
        Assert.Equal(ActionCenterStatus.Important, service.Status);
        Assert.True(await service.MarkReadAsync("submission", missing.ResourceId));
        Assert.Single(api.Acknowledgements);
        Assert.Single(service.Items);
    }

    [Fact]
    public async Task Failed_receipt_keeps_the_news_pending_and_does_not_claim_it_was_read()
    {
        var comment = Personal("notebook-comment", Guid.NewGuid(), "unread");
        using var api = new Api { Items = [comment], FailAck = true };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        Assert.False(await service.MarkReadAsync("notebook-comment", comment.ResourceId));
        Assert.Single(service.Items);
        Assert.Equal(ActionCenterStatus.Optional, service.Status);
    }

    [Fact]
    public async Task Pupil_preview_cannot_acknowledge_personal_events_with_the_teachers_account()
    {
        using var api = new Api();
        await using var context = await LearningUiTestContext.CreateAsync(api, teacher: true, preview: true);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        Assert.False(await service.MarkReadAsync("feedback", Guid.NewGuid()));
        Assert.Empty(api.Acknowledgements);
    }

    [Fact]
    public async Task Reading_a_long_visible_list_uses_bounded_batches_without_losing_receipts()
    {
        using var api = new Api { Items = Enumerable.Range(0, 205).Select(_ => Personal("notebook-comment", Guid.NewGuid(), "unread")).ToArray() };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        var entries = service.Items.Select(i => new ActionCenterReadItem(i.Kind, i.ResourceId)).ToArray();
        Assert.True(await service.MarkReadAsync(entries));
        Assert.Equal(new[] { 200, 5 }, api.Acknowledgements.Select(r => r.Items.Length));
        Assert.Equal(ActionCenterStatus.Clear, service.Status);
    }

    private sealed class Api : HttpMessageHandler
    {
        public ActionCenterItem[] Items { get; set; } = [];
        public bool FailAck { get; set; }
        public string? LastGet { get; private set; }
        public List<ActionCenterReadRequest> Acknowledgements { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery["/api/".Length..];
            if (request.Method == HttpMethod.Get && path.StartsWith("me/actions?version=2", StringComparison.Ordinal))
            {
                LastGet = path;
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new ActionCenterSnapshot(DateTimeOffset.UtcNow, Items)) };
            }
            if (request.Method == HttpMethod.Post && path == "me/actions/read")
            {
                var body = await request.Content!.ReadFromJsonAsync<ActionCenterReadRequest>(cancellationToken);
                Acknowledgements.Add(body!);
                if (FailAck) return new(HttpStatusCode.ServiceUnavailable);
                Items = Items.Where(i => !body!.Items.Any(mark => mark.Kind == i.Kind && mark.ResourceId == i.ResourceId)).ToArray();
                return new(HttpStatusCode.NoContent);
            }
            throw new InvalidOperationException($"Unexpected request {request.Method} {path}");
        }
    }
}
