using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Activity;
using Mercantec.Tech.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class NotificationDropdownTests
{
    private static readonly Guid Hold = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Workspace = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Course = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid Submission = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static ActionCenterItem Item(string title, string severity = "important", string kind = "submission", Guid? resource = null, Guid? submission = null) =>
        new(kind, severity, Hold, Workspace, Course, resource ?? Guid.NewGuid(), submission, title, "H3", DateTimeOffset.UtcNow.AddDays(1));
    private static Task<LearningUiTestContext> Context(Api api, bool teacher = false) =>
        LearningUiTestContext.CreateAsync(api, teacher, configure: NotificationDropdownTestSupport.Configure);
    private static AngleSharp.Dom.IElement Trigger(IRenderedComponent<NotificationBell> bell) => bell.Find("button.notification-bell");
    private static void WaitReady(IRenderedComponent<NotificationBell> bell) => bell.WaitForAssertion(() =>
        Assert.Contains(Trigger(bell).GetAttribute("data-status"), new[] { "important", "optional", "clear" }));

    [Fact]
    public async Task Clicking_the_bell_opens_an_accessible_preview_without_changing_the_page()
    {
        using var api = new Api { Items = [Item("Besvar afstemningen", kind: "poll")] };
        await using var context = await Context(api);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/courses");
        var bell = context.Render<NotificationBell>();
        WaitReady(bell);
        var originalUri = navigation.Uri;
        Assert.Equal("false", Trigger(bell).GetAttribute("aria-expanded"));
        Assert.Empty(bell.FindAll(".notification-dropdown"));

        Trigger(bell).Click();

        var preview = bell.Find(".notification-dropdown");
        Assert.Equal("region", preview.GetAttribute("role"));
        Assert.Equal("Notifikationer", preview.GetAttribute("aria-label"));
        Assert.Equal(preview.Id, Trigger(bell).GetAttribute("aria-controls"));
        Assert.Equal("true", Trigger(bell).GetAttribute("aria-expanded"));
        Assert.Equal(originalUri, navigation.Uri);
        Assert.Contains("Besvar afstemningen", Assert.Single(bell.FindAll(".notification-preview-link")).TextContent);
        Assert.Contains(bell.FindAll("a"), link => link.GetAttribute("href") == "/notifications" && link.TextContent.Contains("Se alle notifikationer"));
    }

    [Fact]
    public async Task Preview_links_keep_the_workspace_selected_course_and_submission_context()
    {
        var poll = Guid.NewGuid(); var quest = Guid.NewGuid(); var review = Guid.NewGuid();
        using var api = new Api { Items = [Item("Afstemning", "optional", "poll", poll),
            Item("Ret din aflevering", "important", "submission", quest, Submission), Item("Giv feedback", "important", "review", review)] };
        await using var context = await Context(api);
        var bell = context.Render<NotificationBell>();
        WaitReady(bell);
        Trigger(bell).Click();

        var links = bell.FindAll(".notification-preview-link").Select(link => link.GetAttribute("href")).ToArray();
        Assert.Contains($"/polls/{poll}?workspace={Workspace}&course={Course}&hold={Hold}", links);
        Assert.Contains($"/quests/{quest}?workspace={Workspace}&course={Course}&hold={Hold}&submission={Submission}", links);
        Assert.Contains($"/courses/{Workspace}?view=submissions&course={Course}&quest={review}&status=submitted", links);
    }

    [Fact]
    public async Task Preview_prioritizes_important_actions_and_keeps_every_action_directly_accessible()
    {
        using var api = new Api { Items = [Item("Valgfri 1", "optional"), Item("Vigtig 1"), Item("Valgfri 2", "optional"),
            Item("Vigtig 2"), Item("Valgfri 3", "optional"), Item("Vigtig 3"), Item("Valgfri 4", "optional"),
            Item("Vigtig 4"), Item("Valgfri 5", "optional")] };
        await using var context = await Context(api);
        var bell = context.Render<NotificationBell>();
        WaitReady(bell);
        Trigger(bell).Click();

        var rows = bell.FindAll(".notification-preview-link");
        Assert.Equal(api.Items.Length, rows.Count);
        Assert.Contains(rows, row => row.TextContent.Contains("Valgfri 5"));
        Assert.All(rows.Take(4), row => Assert.Contains("Vigtig", row.TextContent));
        Assert.All(rows.Skip(4), row => Assert.Contains("Valgfri", row.TextContent));
        Assert.Contains(bell.FindAll("a"), link => link.GetAttribute("href") == "/notifications");
    }

    [Fact]
    public async Task An_empty_successful_snapshot_shows_clear_status_and_the_full_overview_link()
    {
        using var api = new Api();
        await using var context = await Context(api);
        var bell = context.Render<NotificationBell>();
        WaitReady(bell);
        Trigger(bell).Click();

        Assert.Equal("clear", Trigger(bell).GetAttribute("data-status"));
        Assert.Contains("Intet mangler", bell.Find(".notification-dropdown").TextContent);
        Assert.Empty(bell.FindAll(".notification-preview-link"));
        Assert.Contains(bell.FindAll("a"), link => link.GetAttribute("href") == "/notifications");
    }

    [Fact]
    public async Task Loading_never_looks_clear_and_updates_the_open_preview_when_the_response_arrives()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var api = new Api { Items = [Item("Aflever opgaven")], Gate = gate };
        await using var context = await Context(api);
        var bell = context.Render<NotificationBell>();
        bell.WaitForAssertion(() => Assert.Equal("loading", Trigger(bell).GetAttribute("data-status")));
        Trigger(bell).Click();

        Assert.Equal("true", Trigger(bell).GetAttribute("aria-busy"));
        Assert.Empty(bell.FindAll(".notification-preview-link"));
        Assert.DoesNotContain("Intet mangler", bell.Find(".notification-dropdown").TextContent);
        gate.SetResult(true);
        bell.WaitForAssertion(() => Assert.Contains("Aflever opgaven", Assert.Single(bell.FindAll(".notification-preview-link")).TextContent));
        Assert.Equal("important", Trigger(bell).GetAttribute("data-status"));
        Assert.Equal("true", Trigger(bell).GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task A_failed_read_is_honest_and_can_be_retried_inside_the_open_preview()
    {
        using var api = new Api { Fail = true };
        await using var context = await Context(api);
        var bell = context.Render<NotificationBell>();
        bell.WaitForAssertion(() => Assert.Equal("unknown", Trigger(bell).GetAttribute("data-status")));
        Trigger(bell).Click();

        Assert.Single(bell.FindAll(".notification-dropdown [role='alert']"));
        Assert.Empty(bell.FindAll(".notification-preview-link"));
        Assert.DoesNotContain("Intet mangler", bell.Find(".notification-dropdown").TextContent);
        api.Fail = false; api.Items = [Item("Du mangler at svare", kind: "poll")];
        await bell.FindAll(".notification-dropdown button").Single(button => button.TextContent.Contains("Prøv igen")).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        bell.WaitForAssertion(() => Assert.Contains("Du mangler at svare", Assert.Single(bell.FindAll(".notification-preview-link")).TextContent));
        Assert.Empty(bell.FindAll(".notification-dropdown [role='alert']"));
        Assert.Equal("true", Trigger(bell).GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task Toggle_close_button_and_navigation_close_the_preview()
    {
        using var api = new Api { Items = [Item("Læs opgaven")] };
        await using var context = await Context(api);
        var bell = context.Render<NotificationBell>();
        WaitReady(bell);
        Trigger(bell).Click();
        Trigger(bell).Click();
        Assert.Empty(bell.FindAll(".notification-dropdown"));
        Assert.Equal("false", Trigger(bell).GetAttribute("aria-expanded"));
        Trigger(bell).Click();
        bell.Find(".notification-close").Click();
        Assert.Empty(bell.FindAll(".notification-dropdown"));
        Trigger(bell).Click();
        bell.Find(".notification-preview-link").Click();
        Assert.Empty(bell.FindAll(".notification-dropdown"));
        Trigger(bell).Click();
        await bell.InvokeAsync(() => context.Services.GetRequiredService<NavigationManager>().NavigateTo("/notifications"));
        Assert.Empty(bell.FindAll(".notification-dropdown"));
        Assert.Equal("false", Trigger(bell).GetAttribute("aria-expanded"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Outside_click_and_escape_callbacks_close_without_changing_the_page(bool returnFocus)
    {
        using var api = new Api { Items = [Item("Læs opgaven")] };
        await using var context = await Context(api);
        var bell = context.Render<NotificationBell>();
        WaitReady(bell);
        Trigger(bell).Click();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;

        await bell.InvokeAsync(() => bell.Instance.DismissNotifications(returnFocus));

        Assert.Empty(bell.FindAll(".notification-dropdown"));
        Assert.Equal("false", Trigger(bell).GetAttribute("aria-expanded"));
        Assert.Equal(originalUri, navigation.Uri);
    }

    [Fact]
    public async Task Switching_the_preview_account_closes_old_personal_actions_before_loading_the_new_account()
    {
        using var api = new Api { Items = [Item("Privat underviserhandling", kind: "review")] };
        await using var context = await Context(api, teacher: true);
        var bell = context.Render<NotificationBell>();
        WaitReady(bell);
        Trigger(bell).Click();
        Assert.Contains("Privat underviserhandling", bell.Markup);
        api.Items = [];
        var pupil = Guid.NewGuid();
        await bell.InvokeAsync(() => context.ViewAs.Enter("pupil", "Elev", authSub: pupil));

        bell.WaitForAssertion(() => Assert.Empty(bell.FindAll(".notification-dropdown")));
        Assert.DoesNotContain("Privat underviserhandling", bell.Markup);
        Assert.Equal("false", Trigger(bell).GetAttribute("aria-expanded"));
        bell.WaitForAssertion(() => Assert.Equal("clear", Trigger(bell).GetAttribute("data-status")));
        Assert.Equal($"me/actions?version=2&asUser={pupil}", api.LastPath);
        Trigger(bell).Click();
        Assert.Contains("Intet mangler", bell.Find(".notification-dropdown").TextContent);
    }

    private sealed class Api : HttpMessageHandler
    {
        public ActionCenterItem[] Items { get; set; } = [];
        public bool Fail { get; set; }
        public TaskCompletionSource<bool>? Gate { get; set; }
        public string? LastPath { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery["/api/".Length..];
            if (request.Method != HttpMethod.Get || !path.StartsWith("me/actions", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unexpected request {request.Method} {path}");
            LastPath = path;
            var gate = Gate; Gate = null;
            if (gate is not null) await gate.Task.WaitAsync(cancellationToken);
            return Fail ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ActionCenterSnapshot(DateTimeOffset.UtcNow, Items)) };
        }
    }
}

internal static class NotificationDropdownTestSupport
{
    internal static void Configure(IServiceCollection services)
    {
        var storageRuntime = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
        services.AddSingleton<IJSRuntime>(new ModuleRuntime(storageRuntime));
        services.AddSingleton<ActionCenterChanges>();
        services.AddSingleton<ActionCenterService>();
    }

    private sealed class ModuleRuntime(IJSRuntime storage) : IJSRuntime
    {
        private readonly Module _module = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => identifier == "import" ? ValueTask.FromResult((TValue)(object)_module) : storage.InvokeAsync<TValue>(identifier, cancellationToken, args);
    }
    private sealed class Module : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
