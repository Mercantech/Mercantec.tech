using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Gifts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class GiftLauncherFlowTests
{
    [Fact]
    public async Task Sending_locks_the_draft_and_failed_retry_keeps_the_request_id_then_announces_success()
    {
        using var api = new GiftApi();
        var setup = await Context(api);
        await using var context = setup.Context;
        var launcher = context.Render<GiftLauncher>();
        launcher.Find(".gift-launcher").Click();
        launcher.WaitForAssertion(() => Assert.Equal(2, launcher.FindAll(".gift-recipient").Count));
        launcher.Find(".gift-recipient input").Change(true);
        launcher.FindAll(".gift-kind").Single(choice => choice.TextContent.Contains("Tak for hjælpen")).Click();
        launcher.FindAll(".gift-wrap-option").Single(choice => choice.TextContent.Contains("Grøn gave")).Click();
        launcher.Find("textarea").Input("Tak for hjælpen med vores projekt.");
        var pending = launcher.Find(".gift-actions .gift-primary").ClickAsync(new MouseEventArgs());

        try
        {
            await api.FirstSendRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
            launcher.WaitForAssertion(() => Assert.True(launcher.FindComponent<GiftComposer>().Instance.Busy));
            for (var index = 0; index < 3; index++)
            {
                Assert.True(launcher.FindAll(".gift-tabs button")[index].HasAttribute("disabled"));
                await launcher.FindAll(".gift-tabs button")[index].TriggerEventAsync("onclick", new MouseEventArgs());
            }
            await launcher.FindAll(".gift-recipient input")[1].TriggerEventAsync("onchange", new ChangeEventArgs { Value = true });
            await launcher.FindAll(".gift-kind")[0].TriggerEventAsync("onclick", new MouseEventArgs());
            await launcher.Find("textarea").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "Changed while sending" });
            // Invoke the component callbacks too, so the launcher's own guards are exercised.
            await launcher.InvokeAsync(() => launcher.FindComponent<GiftComposer>().Instance.OnPreview.InvokeAsync());
            await launcher.InvokeAsync(() => launcher.FindComponent<GiftComposer>().Instance.OnSend.InvokeAsync());

            Assert.Empty(launcher.FindComponents<GiftReveal>());
            Assert.Equal("true", launcher.FindAll(".gift-tabs button")[1].GetAttribute("aria-pressed"));
            var composer = launcher.FindComponent<GiftComposer>().Instance;
            Assert.Equal(api.Recipients[0].UserSub, composer.RecipientId);
            Assert.Equal("thanks", composer.Kind);
            Assert.Equal("pine", composer.Wrapping);
            Assert.Equal("Tak for hjælpen med vores projekt.", composer.Message);
            Assert.Single(api.Drafts);
        }
        finally
        {
            api.ReleaseFirstSend.TrySetResult(true);
        }
        await pending;
        launcher.WaitForAssertion(() => Assert.Single(launcher.FindAll("#gift-error")));
        Assert.Contains("#gift-error", setup.Runtime.FocusTargets);
        Assert.Equal("Tak for hjælpen med vores projekt.", launcher.FindComponent<GiftComposer>().Instance.Message);
        Assert.False(launcher.Find(".gift-actions .gift-primary").HasAttribute("disabled"));

        // A theme change after an uncertain send cannot relabel the same request.
        setup.Runtime.Theme = "newyear";

        await launcher.Find(".gift-actions .gift-primary").ClickAsync(new MouseEventArgs());

        launcher.WaitForAssertion(() => Assert.Contains(api.Recipients[0].DisplayName, launcher.Find("#gift-feedback").TextContent));
        Assert.Equal("true", launcher.FindAll(".gift-tabs button")[2].GetAttribute("aria-pressed"));
        Assert.Empty(launcher.FindComponents<GiftComposer>());
        Assert.Contains(api.Recipients[0].DisplayName, launcher.Find(".gift-list-item").TextContent);
        Assert.Contains("#gift-feedback", setup.Runtime.FocusTargets);
        var drafts = api.Drafts.ToArray();
        Assert.Equal(2, drafts.Length);
        Assert.NotEqual(Guid.Empty, drafts[0].Id);
        Assert.Equal("christmas", drafts[0].Theme);
        Assert.Equal(drafts[0], drafts[1]);
        Assert.Equal(1, setup.Runtime.ThemeReads);
        Assert.Single(api.Sent);

        // A genuinely new gift captures the currently selected theme afresh.
        await launcher.FindAll(".gift-tabs button")[1].ClickAsync(new MouseEventArgs());
        await launcher.Find(".gift-actions .gift-primary").ClickAsync(new MouseEventArgs());
        launcher.WaitForAssertion(() => Assert.Equal(3, api.Drafts.Count));
        var nextDraft = api.Drafts.Last();
        Assert.NotEqual(drafts[0].Id, nextDraft.Id);
        Assert.Equal("newyear", nextDraft.Theme);
        Assert.Equal(2, setup.Runtime.ThemeReads);
        Assert.Empty(api.UnexpectedRequests);
    }

    [Fact]
    public async Task Guest_preview_reveals_a_local_package_without_any_mailbox_or_send_requests()
    {
        using var api = new GiftApi();
        var setup = await Context(api);
        await using var context = setup.Context;
        await context.Auth.LogoutLocalAsync();
        var launcher = context.Render<GiftLauncher>();
        launcher.Find(".gift-launcher").Click();
        launcher.Find(".gift-welcome .gift-secondary").Click();

        Assert.True(launcher.FindComponent<GiftReveal>().Instance.Preview);
        Assert.False(launcher.FindComponent<GiftReveal>().Instance.Revealed);
        Assert.Empty(launcher.FindComponents<GiftComposer>());
        Assert.Contains("#gift-preview-title", setup.Runtime.FocusTargets);
        launcher.Find(".gift-magic-box").Click();

        launcher.WaitForAssertion(() => Assert.Single(launcher.FindAll(".gift-message-paper")));
        Assert.True(launcher.FindComponent<GiftReveal>().Instance.Revealed);
        launcher.Find(".gift-reveal-actions .gift-secondary").Click();
        Assert.Single(launcher.FindAll("#gift-welcome-title"));
        Assert.Contains("#gift-welcome-title", setup.Runtime.FocusTargets);
        Assert.Empty(api.Requests);
        Assert.Empty(api.Drafts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unsupported-theme")]
    public async Task An_unknown_initial_theme_is_sent_as_unknown_and_cannot_be_relabelled_by_retry(string? theme)
    {
        using var api = new GiftApi();
        api.ReleaseFirstSend.TrySetResult(true);
        var setup = await Context(api);
        await using var context = setup.Context;
        setup.Runtime.Theme = theme;
        var launcher = context.Render<GiftLauncher>();
        launcher.Find(".gift-launcher").Click();
        launcher.WaitForAssertion(() => Assert.Equal(2, launcher.FindAll(".gift-recipient").Count));
        launcher.Find(".gift-recipient input").Change(true);
        await launcher.Find(".gift-actions .gift-primary").ClickAsync(new MouseEventArgs());
        launcher.WaitForAssertion(() => Assert.Single(launcher.FindAll("#gift-error")));
        Assert.Null(api.Drafts.Single().Theme);

        setup.Runtime.Theme = "christmas";
        await launcher.Find(".gift-actions .gift-primary").ClickAsync(new MouseEventArgs());
        launcher.WaitForAssertion(() => Assert.Single(launcher.FindAll("#gift-feedback")));

        Assert.Equal(2, api.Drafts.Count);
        Assert.All(api.Drafts, draft => Assert.Null(draft.Theme));
        Assert.Equal(api.Drafts.First().Id, api.Drafts.Last().Id);
        Assert.Equal(1, setup.Runtime.ThemeReads);
    }

    [Fact]
    public async Task Teacher_student_preview_cannot_open_the_launcher_even_when_a_disabled_click_is_dispatched()
    {
        using var api = new GiftApi();
        var setup = await Context(api, preview: true);
        await using var context = setup.Context;
        var launcher = context.Render<GiftLauncher>();

        Assert.True(launcher.Find(".gift-launcher").HasAttribute("disabled"));
        await launcher.Find(".gift-launcher").TriggerEventAsync("onclick", new MouseEventArgs());

        Assert.Empty(launcher.FindComponents<GiftComposer>());
        Assert.Empty(launcher.FindComponents<GiftReveal>());
        Assert.Empty(launcher.FindAll(".gift-tabs"));
        Assert.Empty(api.Requests);
        Assert.Empty(api.Drafts);
    }

    private static async Task<(LearningUiTestContext Context, GiftRuntime Runtime)> Context(GiftApi api, bool preview = false)
    {
        GiftRuntime? runtime = null;
        var context = await LearningUiTestContext.CreateAsync(api, teacher: preview, preview: preview, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            runtime = new GiftRuntime(original);
            services.AddSingleton<IJSRuntime>(runtime);
            services.AddSingleton<GiftMailboxClient>();
        });
        return (context, runtime!);
    }

    private sealed class GiftRuntime(IJSRuntime inner) : IJSRuntime
    {
        private readonly ConcurrentQueue<string> _focusTargets = [];
        public string[] FocusTargets => _focusTargets.ToArray();
        public string? Theme { get; set; } = "christmas";
        public int ThemeReads { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "lupGiftUi.focus") _focusTargets.Enqueue(args?[1]?.ToString() ?? "");
            if (identifier == "lupTheme.get")
            {
                ThemeReads++;
                return ValueTask.FromResult((TValue)(object)Theme!);
            }
            return inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }
    }

    private sealed class GiftApi : HttpMessageHandler
    {
        public GiftRecipient[] Recipients { get; } =
        [
            new(Guid.Parse("22222222-2222-4222-8222-222222222222"), "Sara Testperson", false),
            new(Guid.Parse("33333333-3333-4333-8333-333333333333"), "Emil Testperson", true)
        ];
        public ConcurrentQueue<GiftDraft> Drafts { get; } = [];
        public ConcurrentQueue<GiftMessage> Sent { get; } = [];
        public ConcurrentQueue<string> Requests { get; } = [];
        public ConcurrentQueue<string> UnexpectedRequests { get; } = [];
        public TaskCompletionSource<bool> FirstSendRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseFirstSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = request.RequestUri!.PathAndQuery["/api/".Length..];
            Requests.Enqueue(request.Method + " " + path);
            if (request.Method == HttpMethod.Post && path == "gifts/profile") return new(HttpStatusCode.NoContent);
            if (request.Method == HttpMethod.Get && path == "gifts") return Json(new { received = Array.Empty<GiftMessage>(), sent = Sent.ToArray(), unread = 0 });
            if (request.Method == HttpMethod.Get && path == "gifts/recipients") return Json(Recipients);
            if (request.Method == HttpMethod.Post && path == "gifts")
            {
                var draft = await request.Content!.ReadFromJsonAsync<GiftDraft>(cancellationToken) ?? throw new InvalidOperationException("Missing gift draft.");
                Drafts.Enqueue(draft);
                if (Drafts.Count == 1)
                {
                    FirstSendRequested.TrySetResult(true);
                    await ReleaseFirstSend.Task.WaitAsync(cancellationToken);
                    return new(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "unavailable" }) };
                }
                var gift = new GiftMessage(draft.Id, "Testelev", Recipients.Single(person => person.UserSub == draft.RecipientSub).DisplayName,
                    draft.Kind, draft.Wrapping, draft.Message, DateTime.UtcNow, null, draft.Theme);
                Sent.Enqueue(gift);
                return Json(gift);
            }
            UnexpectedRequests.Enqueue(request.Method + " " + path);
            return new(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json<T>(T content) => new(HttpStatusCode.OK) { Content = JsonContent.Create(content) };
    }
}
