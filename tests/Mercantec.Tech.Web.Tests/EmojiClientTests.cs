using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Web.Emojis;

namespace Mercantec.Tech.Web.Tests;

public sealed class EmojiClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Many_simultaneous_labels_share_one_catalogue_request()
    {
        using var api = new CatalogueApi();
        using var http = new HttpClient(api) { BaseAddress = new Uri("https://app.example/api/") };
        var client = new EmojiClient(http);
        var requests = Enumerable.Range(0, 100).Select(_ => client.List()).ToArray();
        try
        {
            await api.FirstRequested.Task.WaitAsync(Timeout);
            Assert.Equal(1, api.Requests);
            api.FirstResponse.TrySetResult(true);
            var results = await Task.WhenAll(requests).WaitAsync(Timeout);

            Assert.All(results, result => Assert.Same(results[0], result));
            Assert.Same(results[0], await client.List());
            Assert.Equal(1, api.Requests);
        }
        finally { api.FirstResponse.TrySetResult(true); }
    }

    [Fact]
    public async Task Cancelling_a_waiting_label_keeps_the_active_catalogue_request_available()
    {
        using var api = new CatalogueApi();
        using var http = new HttpClient(api) { BaseAddress = new Uri("https://app.example/api/") };
        using var cancellation = new CancellationTokenSource();
        var client = new EmojiClient(http);
        var active = client.List();
        try
        {
            await api.FirstRequested.Task.WaitAsync(Timeout);
            var cancelled = client.List(ct: cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelled);

            api.FirstResponse.TrySetResult(true);
            var result = await active.WaitAsync(Timeout);
            Assert.Same(result, await client.List());
            Assert.Equal(1, api.Requests);
        }
        finally { api.FirstResponse.TrySetResult(true); }
    }

    [Fact]
    public async Task An_inflight_response_cannot_restore_a_catalogue_invalidated_by_an_edit()
    {
        using var api = new CatalogueApi();
        using var http = new HttpClient(api) { BaseAddress = new Uri("https://app.example/api/") };
        var client = new EmojiClient(http);
        var active = client.List();
        try
        {
            await api.FirstRequested.Task.WaitAsync(Timeout);
            client.Invalidate();
            api.FirstResponse.TrySetResult(true);
            Assert.Equal("first", Assert.Single(await active.WaitAsync(Timeout)).Name);

            var refreshed = await client.List();
            Assert.Equal("updated", Assert.Single(refreshed).Name);
            Assert.Same(refreshed, await client.List());
            Assert.Equal(2, api.Requests);
        }
        finally { api.FirstResponse.TrySetResult(true); }
    }

    private sealed class CatalogueApi : HttpMessageHandler
    {
        public int Requests;
        public TaskCompletionSource<bool> FirstRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> FirstResponse { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("/api/emojis", request.RequestUri!.AbsolutePath);
            var index = Interlocked.Increment(ref Requests);
            if (index == 1)
            {
                FirstRequested.TrySetResult(true);
                await FirstResponse.Task.WaitAsync(ct);
            }
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { new CustomEmojiItem(Guid.NewGuid(), index == 1 ? "first" : "updated",
                    "image/png", 100, DateTimeOffset.UtcNow, "/api/emojis/content") })
            };
        }
    }
}
