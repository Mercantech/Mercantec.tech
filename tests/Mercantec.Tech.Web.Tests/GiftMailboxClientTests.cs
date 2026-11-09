using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Web.Activity;
using Mercantec.Tech.Web.Gifts;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class GiftMailboxClientTests
{
    [Fact]
    public async Task Confirmed_send_notifies_avatar_progress_only_after_its_mailbox_refresh_finishes()
    {
        using var api = new Api { MailboxGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var client = new GiftMailboxClient(context.Services.GetRequiredService<HttpClient>(), context.Auth, viewAs: context.ViewAs);
        var notifications = 0;
        client.GiftSent += () => notifications++;
        var pending = client.SendAsync(new(Guid.NewGuid(), Guid.NewGuid(), "thanks", "pine", "Testhilsen", "christmas"), default);
        await api.MailboxRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, notifications);

        api.MailboxGate!.SetResult(true);
        var sent = await pending;
        Assert.Equal("christmas", sent.Theme);
        Assert.Equal(LearningUiTestContext.AccountId.ToString(), Assert.Single(api.SendSubjects));
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task Failed_send_does_not_notify_avatar_progress_or_refresh_the_mailbox()
    {
        using var api = new Api { SendFailure = true };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var client = new GiftMailboxClient(context.Services.GetRequiredService<HttpClient>(), context.Auth);
        var notifications = 0;
        client.GiftSent += () => notifications++;

        await Assert.ThrowsAsync<GiftException>(() => client.SendAsync(new(Guid.NewGuid(), Guid.NewGuid(), "thanks", "pine", "Testhilsen", "christmas"), default));

        Assert.Equal(0, notifications);
        Assert.Equal(new[] { "POST gifts" }, api.Requests.ToArray());
    }

    [Fact]
    public async Task A_send_confirmed_after_logout_cannot_notify_the_new_avatar_context()
    {
        using var api = new Api { SendGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var client = new GiftMailboxClient(context.Services.GetRequiredService<HttpClient>(), context.Auth);
        var notifications = 0;
        client.GiftSent += () => notifications++;
        var pending = client.SendAsync(new(Guid.NewGuid(), Guid.NewGuid(), "thanks", "pine", "Testhilsen", "christmas"), default);
        await api.SendRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await context.Auth.LogoutLocalAsync();
        api.SendGate!.SetResult(true);

        await Assert.ThrowsAsync<GiftException>(() => pending);

        Assert.Equal(0, notifications);
        Assert.Equal(new[] { "POST gifts" }, api.Requests.ToArray());
    }

    [Fact]
    public async Task A_direct_gift_link_reads_without_registration_mailbox_refresh_opening_or_invalidation()
    {
        using var api = new Api();
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var changes = new ActionCenterChanges(); var notifications = 0;
        changes.Changed += () => notifications++;
        var client = new GiftMailboxClient(context.Services.GetRequiredService<HttpClient>(), context.Auth, changes, context.ViewAs);

        var gift = await client.GetAsync(api.Gift.Id);

        Assert.Equal(api.Gift, gift);
        Assert.Null(gift.OpenedAtUtc);
        Assert.Equal(new[] { $"GET gifts/{api.Gift.Id}" }, api.Requests.ToArray());
        Assert.Equal(0, notifications);
        Assert.Empty(client.Received);
    }

    [Fact]
    public async Task Opening_a_confirmed_gift_invalidates_status_before_the_mailbox_refresh_finishes()
    {
        using var api = new Api { MailboxGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var changes = new ActionCenterChanges(); var notifications = 0;
        changes.Changed += () => notifications++;
        var client = new GiftMailboxClient(context.Services.GetRequiredService<HttpClient>(), context.Auth, changes, context.ViewAs);
        var pending = client.OpenAsync(api.Gift.Id, default);
        await api.MailboxRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, notifications);
        Assert.False(pending.IsCompleted);
        api.MailboxGate!.SetResult(true);
        var gift = await pending;
        Assert.NotNull(gift.OpenedAtUtc);
        Assert.Equal(0, client.Unread);
        Assert.NotNull(Assert.Single(client.Received).OpenedAtUtc);
        Assert.Equal(1, notifications);
    }

    [Theory]
    [InlineData("not-found")]
    [InlineData("malformed")]
    [InlineData("not-opened")]
    public async Task Failed_or_unconfirmed_opens_never_invalidate_status_or_refresh_the_mailbox(string failure)
    {
        using var api = new Api { OpenFailure = failure };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var changes = new ActionCenterChanges(); var notifications = 0;
        changes.Changed += () => notifications++;
        var client = new GiftMailboxClient(context.Services.GetRequiredService<HttpClient>(), context.Auth, changes, context.ViewAs);

        await Assert.ThrowsAsync<GiftException>(() => client.OpenAsync(api.Gift.Id, default));

        Assert.Equal(0, notifications);
        Assert.Equal(new[] { $"POST gifts/{api.Gift.Id}/open" }, api.Requests.ToArray());
    }

    [Fact]
    public async Task Pupil_preview_clears_the_teachers_mailbox_and_blocks_direct_reads_and_opens()
    {
        using var api = new Api();
        await using var context = await LearningUiTestContext.CreateAsync(api, teacher: true);
        var client = new GiftMailboxClient(context.Services.GetRequiredService<HttpClient>(), context.Auth, viewAs: context.ViewAs);
        await client.RefreshAsync();
        Assert.Single(client.Received);
        var readsBeforePreview = api.Requests.Count;
        context.ViewAs.Enter("pupil", "Elev", authSub: Guid.NewGuid());

        await client.RefreshAsync();
        await Assert.ThrowsAsync<GiftException>(() => client.GetAsync(api.Gift.Id));
        await Assert.ThrowsAsync<GiftException>(() => client.OpenAsync(api.Gift.Id, default));

        Assert.Empty(client.Received);
        Assert.Empty(client.Sent);
        Assert.Equal(0, client.Unread);
        Assert.Equal(readsBeforePreview, api.Requests.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_pending_gift_read_cannot_return_private_content_after_logout_or_pupil_preview(bool preview)
    {
        using var api = new Api { ReadGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var context = await LearningUiTestContext.CreateAsync(api, teacher: true);
        var client = new GiftMailboxClient(context.Services.GetRequiredService<HttpClient>(), context.Auth, viewAs: context.ViewAs);
        var pending = client.GetAsync(api.Gift.Id);
        await api.ReadRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (preview) context.ViewAs.Enter("pupil", "Elev", authSub: Guid.NewGuid());
        else await context.Auth.LogoutLocalAsync();
        api.ReadGate!.SetResult(true);

        await Assert.ThrowsAsync<GiftException>(() => pending);
        Assert.Empty(client.Received);
    }

    [Fact]
    public async Task A_confirmed_open_from_a_previous_account_does_not_invalidate_the_new_accounts_status()
    {
        using var api = new Api { OpenGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var changes = new ActionCenterChanges(); var notifications = 0;
        changes.Changed += () => notifications++;
        var client = new GiftMailboxClient(context.Services.GetRequiredService<HttpClient>(), context.Auth, changes, context.ViewAs);
        var pending = client.OpenAsync(api.Gift.Id, default);
        await api.OpenRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await context.Auth.LogoutLocalAsync();
        api.OpenGate!.SetResult(true);

        await Assert.ThrowsAsync<GiftException>(() => pending);
        Assert.Equal(0, notifications);
        Assert.DoesNotContain(api.Requests, request => request == "GET gifts" || request == "POST gifts/profile");
    }

    [Fact]
    public async Task The_existing_two_argument_constructor_still_supports_direct_gift_reads()
    {
        using var api = new Api();
        await using var context = await LearningUiTestContext.CreateAsync(api);
        var client = new GiftMailboxClient(context.Services.GetRequiredService<HttpClient>(), context.Auth);
        Assert.Equal(api.Gift.Id, (await client.GetAsync(api.Gift.Id)).Id);
    }

    private sealed class Api : HttpMessageHandler
    {
        public GiftMessage Gift { get; private set; } = new(Guid.NewGuid(), "Afsender", "Modtager", "thanks", "pine", "Privat hilsen", DateTime.UtcNow, null);
        public string? OpenFailure { get; init; }
        public bool SendFailure { get; init; }
        public TaskCompletionSource<bool>? SendGate { get; init; }
        public TaskCompletionSource<bool> SendRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool>? ReadGate { get; init; }
        public TaskCompletionSource<bool>? OpenGate { get; init; }
        public TaskCompletionSource<bool>? MailboxGate { get; init; }
        public TaskCompletionSource<bool> ReadRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> OpenRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> MailboxRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<string> Requests { get; } = [];
        public ConcurrentQueue<string?> SendSubjects { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery["/api/".Length..];
            Requests.Enqueue(request.Method + " " + path);
            if (request.Method == HttpMethod.Post && path == "gifts")
            {
                SendSubjects.Enqueue(request.Headers.TryGetValues("X-LUP-Gift-Subject", out var subjects) ? string.Join(",", subjects) : null);
                SendRequested.TrySetResult(true);
                if (SendGate is not null) await SendGate.Task.WaitAsync(cancellationToken);
                if (SendFailure) return new(HttpStatusCode.ServiceUnavailable);
                var draft = await request.Content!.ReadFromJsonAsync<GiftDraft>(cancellationToken) ?? throw new InvalidOperationException("No draft");
                return Json(new GiftMessage(draft.Id, "Afsender", "Modtager", draft.Kind, draft.Wrapping, draft.Message, DateTime.UtcNow, null, draft.Theme));
            }
            if (request.Method == HttpMethod.Get && path == $"gifts/{Gift.Id}")
            {
                ReadRequested.TrySetResult(true);
                if (ReadGate is not null) await ReadGate.Task.WaitAsync(cancellationToken);
                return Json(Gift);
            }
            if (request.Method == HttpMethod.Post && path == $"gifts/{Gift.Id}/open")
            {
                OpenRequested.TrySetResult(true);
                if (OpenGate is not null) await OpenGate.Task.WaitAsync(cancellationToken);
                if (OpenFailure == "not-found") return new(HttpStatusCode.NotFound);
                if (OpenFailure == "malformed") return new(HttpStatusCode.OK) { Content = new StringContent("{broken", System.Text.Encoding.UTF8, "application/json") };
                if (OpenFailure != "not-opened") Gift = Gift with { OpenedAtUtc = DateTime.UtcNow };
                return Json(Gift);
            }
            if (request.Method == HttpMethod.Post && path == "gifts/profile") return new(HttpStatusCode.NoContent);
            if (request.Method == HttpMethod.Get && path == "gifts")
            {
                MailboxRequested.TrySetResult(true);
                if (MailboxGate is not null) await MailboxGate.Task.WaitAsync(cancellationToken);
                return Json(new { received = new[] { Gift }, sent = Array.Empty<GiftMessage>(), unread = Gift.OpenedAtUtc is null ? 1 : 0 });
            }
            throw new InvalidOperationException($"Unexpected request {request.Method} {path}");
        }
        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
