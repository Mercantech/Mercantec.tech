using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Web.Activity;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Gifts;
using Microsoft.Extensions.DependencyInjection;
using GiftPage = Mercantec.Tech.Web.Pages.Gifts.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class GiftInboxUiTests
{
    private static Task<LearningUiTestContext> Context(GiftApi api, bool teacher = false, bool preview = false) =>
        LearningUiTestContext.CreateAsync(api, teacher, preview,
            services => services.AddSingleton<GiftMailboxClient>());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Guests_and_teacher_preview_cannot_read_or_open_personal_gifts(bool preview)
    {
        using var api = new GiftApi();
        using var context = await Context(api, teacher: preview, preview: preview);
        if (!preview) await context.Auth.LogoutLocalAsync();

        var page = context.Render<GiftPage>(p => p.Add(x => x.GiftId, api.Gift.Id));

        Assert.Empty(page.FindComponents<GiftReveal>());
        Assert.DoesNotContain(api.Gift.SenderName, page.Markup);
        Assert.DoesNotContain(api.Gift.Message, page.Markup);
        Assert.Empty(api.Requests);
        Assert.Empty(page.FindAll(".gift-magic-box"));
        Assert.Contains(page.FindAll("a"), link => link.GetAttribute("href") == (preview ? "/student" : "/login"));
    }

    [Fact]
    public async Task Reading_an_unopened_gift_renders_the_package_without_marking_it_open()
    {
        using var api = new GiftApi();
        using var context = await Context(api);

        var page = context.Render<GiftPage>(p => p.Add(x => x.GiftId, api.Gift.Id));

        page.WaitForAssertion(() => Assert.Single(page.FindComponents<GiftReveal>()));
        Assert.False(page.FindComponent<GiftReveal>().Instance.Revealed);
        Assert.False(page.Find(".gift-magic-box").HasAttribute("disabled"));
        Assert.Empty(page.FindAll(".gift-message-paper, dialog"));
        Assert.DoesNotContain(api.Gift.Message, page.Markup);
        Assert.Equal(new[] { ("GET", $"gifts/{api.Gift.Id:D}") }, api.Requests.ToArray());
        Assert.Equal(0, api.OpenRequests);
        Assert.Null(api.Gift.OpenedAtUtc);
    }

    [Fact]
    public async Task Clicking_the_package_opens_it_once_reveals_the_message_and_invalidates_action_status()
    {
        using var api = new GiftApi();
        using var context = await Context(api);
        var invalidations = 0;
        context.Services.GetRequiredService<ActionCenterChanges>().Changed += () => Interlocked.Increment(ref invalidations);
        var page = context.Render<GiftPage>(p => p.Add(x => x.GiftId, api.Gift.Id));
        page.WaitForAssertion(() => Assert.Single(page.FindComponents<GiftReveal>()));
        Assert.Equal(0, invalidations);

        page.Find(".gift-magic-box").Click();

        page.WaitForAssertion(() => Assert.Contains(api.Gift.Message, page.Find(".gift-message-paper").TextContent));
        Assert.True(page.FindComponent<GiftReveal>().Instance.Revealed);
        Assert.True(page.Find(".gift-magic-box").HasAttribute("disabled"));
        Assert.Equal(1, api.OpenRequests);
        Assert.Equal(1, invalidations);
        Assert.NotNull(api.Gift.OpenedAtUtc);
        Assert.Equal(0, context.Services.GetRequiredService<GiftMailboxClient>().Unread);
        Assert.Empty(api.UnexpectedRequests);
    }

    [Fact]
    public async Task An_external_mailbox_open_updates_the_visible_inbox_receipt_without_navigation()
    {
        using var api = new GiftApi();
        using var context = await Context(api);
        var mailbox = context.Services.GetRequiredService<GiftMailboxClient>();
        var page = context.Render<GiftPage>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".gift-page-state.unopened")));
        var rowHref = $"/gifts/{api.Gift.Id:D}";
        Assert.Contains(page.FindAll("a"), link => link.GetAttribute("href") == rowHref);

        await page.InvokeAsync(async () => { await mailbox.OpenAsync(api.Gift.Id, CancellationToken.None); });

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".gift-page-state.unopened")));
        Assert.Single(page.FindAll(".gift-page-row"));
        Assert.Contains(page.FindAll("a"), link => link.GetAttribute("href") == rowHref);
        Assert.Equal(1, api.OpenRequests);
        Assert.Equal(0, mailbox.Unread);
        Assert.Empty(api.UnexpectedRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_gifts_and_other_recipients_404_responses_show_an_error_without_private_content(bool otherRecipient)
    {
        using var api = new GiftApi();
        using var context = await Context(api);
        var id = otherRecipient ? api.OtherRecipientGift.Id : Guid.Parse("99999999-9999-4999-8999-999999999999");

        var page = context.Render<GiftPage>(p => p.Add(x => x.GiftId, id));

        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role='alert']")));
        Assert.Empty(page.FindComponents<GiftReveal>());
        Assert.DoesNotContain(api.Gift.Message, page.Markup);
        Assert.DoesNotContain(api.OtherRecipientGift.SenderName, page.Markup);
        Assert.DoesNotContain(api.OtherRecipientGift.Message, page.Markup);
        Assert.Equal(new[] { ("GET", $"gifts/{id:D}") }, api.Requests.ToArray());
        Assert.Equal(0, api.OpenRequests);
        Assert.Empty(api.UnexpectedRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Logging_out_or_entering_teacher_preview_removes_a_previously_visible_package(bool preview)
    {
        using var api = new GiftApi();
        using var context = await Context(api, teacher: preview);
        var page = context.Render<GiftPage>(p => p.Add(x => x.GiftId, api.Gift.Id));
        page.WaitForAssertion(() => Assert.Single(page.FindComponents<GiftReveal>()));
        Assert.Contains(api.Gift.SenderName, page.Markup);
        var readCount = api.Requests.Count;

        if (preview) await page.InvokeAsync(() => context.ViewAs.Enter("pupil", "Valgt elev", authSub: Guid.NewGuid()));
        else await page.InvokeAsync(context.Auth.LogoutLocalAsync);

        page.WaitForAssertion(() => Assert.Empty(page.FindComponents<GiftReveal>()));
        Assert.DoesNotContain(api.Gift.SenderName, page.Markup);
        Assert.DoesNotContain(api.Gift.Message, page.Markup);
        Assert.Equal(readCount, api.Requests.Count);
        Assert.Equal(0, api.OpenRequests);
    }

    private sealed class GiftApi : HttpMessageHandler
    {
        public GiftMessage Gift { get; private set; } = new(
            Guid.Parse("22222222-2222-4222-8222-222222222222"), "Afsender fra mit hold", "Testelev", "thanks", "pine",
            "Tak for hjælpen med vores database.", new DateTime(2026, 11, 10, 9, 30, 0, DateTimeKind.Utc), null);
        public GiftMessage OtherRecipientGift { get; } = new(
            Guid.Parse("33333333-3333-4333-8333-333333333333"), "Privat afsender til en anden", "En anden modtager", "warmth", "gold",
            "Denne hilsen tilhører en anden konto.", new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc), null);
        public ConcurrentQueue<(string Method, string Path)> Requests { get; } = [];
        public ConcurrentQueue<string> UnexpectedRequests { get; } = [];
        public int OpenRequests => Requests.Count(request => request.Method == "POST" && request.Path.EndsWith("/open", StringComparison.Ordinal));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = request.RequestUri!.PathAndQuery["/api/".Length..];
            Requests.Enqueue((request.Method.Method, path));
            if (request.Method == HttpMethod.Get && path.StartsWith("gifts/", StringComparison.Ordinal))
                return Task.FromResult(path == $"gifts/{Gift.Id:D}" ? Json(Gift) : NotFound());
            if (request.Method == HttpMethod.Post && path == $"gifts/{Gift.Id:D}/open")
            {
                Gift = Gift with { OpenedAtUtc = new DateTime(2026, 11, 10, 10, 30, 0, DateTimeKind.Utc) };
                return Task.FromResult(Json(Gift));
            }
            if (request.Method == HttpMethod.Post && path == "gifts/profile")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            if (request.Method == HttpMethod.Get && path == "gifts")
                return Task.FromResult(Json(new { received = new[] { Gift }, sent = Array.Empty<GiftMessage>(), unread = Gift.OpenedAtUtc is null ? 1 : 0 }));
            UnexpectedRequests.Enqueue($"{request.Method} {path}");
            return Task.FromResult(NotFound());
        }
        private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
        private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { error = "gift_not_found" }) };
    }
}
