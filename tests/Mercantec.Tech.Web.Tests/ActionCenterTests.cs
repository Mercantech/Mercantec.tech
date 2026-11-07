using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Activity;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Polls;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class ActionCenterTests
{
    private static readonly Guid Hold = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Workspace = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Resource = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static ActionCenterItem Item(string severity, string kind = "submission", int count = 1) => new(kind, severity, Hold, Workspace, null, Resource, null, "Opgave", "H3", null, count);
    private static Task<LearningUiTestContext> Context(Api api, bool teacher = false, bool preview = false) => LearningUiTestContext.CreateAsync(api, teacher, preview,
        services => { services.AddSingleton<ActionCenterChanges>(); services.AddSingleton<ActionCenterService>(); });

    [Theory]
    [InlineData("important", "important")]
    [InlineData("optional", "optional")]
    [InlineData("empty", "clear")]
    public async Task Bell_uses_actual_action_status_and_a_text_label(string severity, string expected)
    {
        using var api = new Api { Items = severity == "empty" ? [] : [Item(severity)] };
        await using var context = await Context(api);
        var bell = context.Render<NotificationBell>();
        bell.WaitForAssertion(() => Assert.Equal(expected, bell.Find("a").GetAttribute("data-status")));
        Assert.StartsWith("Notifikationer:", bell.Find("a").GetAttribute("aria-label"));
        Assert.Equal("/notifications", bell.Find("a").GetAttribute("href"));
        Assert.Equal(1, api.Reads);
    }

    [Fact]
    public async Task Important_wins_over_optional_and_review_counts_are_real_submission_counts()
    {
        using var api = new Api { Items = [Item("optional", "review", 3), Item("important")] };
        await using var context = await Context(api);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        Assert.Equal(ActionCenterStatus.Important, service.Status);
        Assert.Equal(1, service.ImportantCount);
        Assert.Equal(3, service.OptionalCount);
        Assert.Contains("3 andre handlinger", service.Label);
        await service.EnsureAsync();
        Assert.Equal(1, api.Reads); // The page and bell share the same cached snapshot.
    }

    [Fact]
    public async Task Failed_refresh_never_displays_green_from_an_old_empty_snapshot()
    {
        using var api = new Api();
        await using var context = await Context(api);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        Assert.Equal(ActionCenterStatus.Clear, service.Status);
        api.Fail = true;
        await service.RefreshAsync(force: true);
        Assert.Equal(ActionCenterStatus.Unknown, service.Status);
        Assert.NotNull(service.Error);
    }

    [Fact]
    public async Task Retry_with_an_old_empty_snapshot_stays_neutral_until_a_new_response_arrives()
    {
        using var api = new Api();
        await using var context = await Context(api);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        Assert.Equal(ActionCenterStatus.Clear, service.Status);
        api.Fail = true;
        await service.RefreshAsync(force: true);
        Assert.Equal(ActionCenterStatus.Unknown, service.Status);
        api.Fail = false;
        var retryGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Gate = retryGate;
        var pending = service.RefreshAsync(force: true);
        Assert.Equal(ActionCenterStatus.Loading, service.Status);
        Assert.DoesNotContain("Intet mangler", service.Label);
        retryGate.SetResult(true);
        await pending;
        Assert.Equal(ActionCenterStatus.Clear, service.Status);
    }

    [Fact]
    public async Task Unknown_server_status_is_neutral_instead_of_looking_clear()
    {
        using var api = new Api { Items = [Item("unsupported-status")] };
        await using var context = await Context(api);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        Assert.Equal(ActionCenterStatus.Unknown, service.Status);
        Assert.Null(service.Snapshot);
        Assert.NotNull(service.Error);
    }

    [Fact]
    public async Task Preview_loads_target_account_status_instead_of_teachers_snapshot()
    {
        using var api = new Api { Items = [Item("optional", "review", 3)] };
        await using var context = await Context(api, teacher: true);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        var bell = context.Render<NotificationBell>();
        var target = Guid.NewGuid();
        api.Items = [];
        context.ViewAs.Enter("student-lectio", "Elev", authSub: target);
        bell.WaitForAssertion(() => Assert.Equal($"me/actions?asUser={target}", api.LastPath));
        bell.WaitForAssertion(() => Assert.Equal("clear", bell.Find("a").GetAttribute("data-status")));
        Assert.Equal(ActionCenterStatus.Clear, service.Status);
        Assert.Equal(0, service.OptionalCount);
        Assert.Equal(2, api.Reads);
    }

    [Fact]
    public async Task An_old_request_cannot_restore_teacher_actions_after_switching_to_a_pupil()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var api = new Api { Items = [Item("optional", "review", 3)], Gate = gate };
        await using var context = await Context(api, teacher: true);
        var service = context.Services.GetRequiredService<ActionCenterService>();
        var oldRequest = service.EnsureAsync();
        var bell = context.Render<NotificationBell>();
        Assert.Equal(ActionCenterStatus.Loading, service.Status);
        api.Items = [];
        var target = Guid.NewGuid();
        context.ViewAs.Enter("pupil", "Elev", authSub: target);
        gate.SetResult(true);
        await oldRequest;
        bell.WaitForAssertion(() => Assert.Equal("clear", bell.Find("a").GetAttribute("data-status")));
        Assert.Equal($"me/actions?asUser={target}", api.LastPath);
        Assert.Equal(ActionCenterStatus.Clear, service.Status);
        Assert.Empty(service.Items);
    }

    [Fact]
    public async Task Preview_without_an_account_never_reads_teachers_personal_actions_or_shows_green()
    {
        using var api = new Api();
        await using var context = await Context(api, teacher: true);
        context.ViewAs.Enter("unlinked-student", "Elev");
        var service = context.Services.GetRequiredService<ActionCenterService>();
        await service.EnsureAsync();
        Assert.Equal(0, api.Reads);
        Assert.Equal(ActionCenterStatus.Unknown, service.Status);
    }

    [Fact]
    public async Task Successful_poll_answer_refreshes_the_bell_without_navigation()
    {
        using var api = new Api { Items = [Item("optional", "poll")] };
        await using var context = await Context(api);
        var bell = context.Render<NotificationBell>();
        bell.WaitForAssertion(() => Assert.Equal("optional", bell.Find("a").GetAttribute("data-status")));
        var client = new PollClient(context.Services.GetRequiredService<HttpClient>(), context.Services.GetRequiredService<ActionCenterChanges>());
        api.Items = [];
        await client.Answer(Resource, new PollAnswer(0, null));
        bell.WaitForAssertion(() => Assert.Equal("clear", bell.Find("a").GetAttribute("data-status")));
        Assert.Equal(2, api.Reads);
    }

    private sealed class Api : HttpMessageHandler
    {
        public ActionCenterItem[] Items { get; set; } = [];
        public bool Fail { get; set; }
        public TaskCompletionSource<bool>? Gate { get; set; }
        public int Reads { get; private set; }
        public string? LastPath { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery["/api/".Length..];
            if (request.Method == HttpMethod.Get && path.StartsWith("me/actions", StringComparison.Ordinal))
            {
                LastPath = path; Reads++;
                var gate = Gate; Gate = null;
                if (gate is not null) await gate.Task.WaitAsync(cancellationToken);
                return Fail ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Json(new ActionCenterSnapshot(DateTimeOffset.UtcNow, Items));
            }
            if (request.Method == HttpMethod.Post && path == $"polls/{Resource}/answer")
                return Json(new PollView(Resource, Hold, "H3", "Spørgsmål", "choice", ["Ja", "Nej"], "open", "live", false, 2, true, 0, null, 1, [1, 0], null, DateTime.UtcNow));
            throw new InvalidOperationException($"Unexpected request {request.Method} {path}");
        }
        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
