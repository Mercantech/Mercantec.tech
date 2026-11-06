using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Mercantec.Tech.Api.Speech;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class RostSpeechGeneratorTests
{
    [Fact]
    public async Task ProbesActualReadinessAndSendsOnlyTextToFixedLocalRoutes()
    {
        var ready = false;
        var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal("127.0.0.1", request.RequestUri!.Host);
            Assert.Null(request.Headers.Authorization);
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/health", request.RequestUri.AbsolutePath);
                return new(HttpStatusCode.OK) { Content = new StringContent(ready ? "{\"ready\":true}" : "{\"ready\":false}") };
            }
            Assert.Equal("/speech", request.RequestUri.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Single(body.RootElement.EnumerateObject());
            Assert.Equal("Klar til grupperoulette.", body.RootElement.GetProperty("text").GetString());
            return AudioResponse(new byte[48]);
        });
        var generator = Create(handler);
        Assert.False((await generator.StatusAsync(default)).Available);
        ready = true;
        Assert.True((await generator.StatusAsync(default)).Available);
        var clip = await generator.GenerateAsync("Klar til grupperoulette.", default);
        Assert.Equal("Røst", clip.Provider); Assert.Equal("audio/wav", clip.ContentType); Assert.Equal(48, clip.Bytes.Length);
        Assert.Equal(3, handler.RequestCount);
    }

    [Theory]
    [InlineData("https://127.0.0.1:8091")]
    [InlineData("http://example.org:8091")]
    [InlineData("http://127.0.0.1:8091/external/")]
    [InlineData("http://127.0.0.1:8091/?text=private")]
    [InlineData("http://teacher:password@127.0.0.1:8091")]
    public async Task RejectsNonLocalOrDecoratedBaseAddressesBeforeSending(string url)
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException("No request should leave the process"));
        var generator = Create(handler, url);
        await Assert.ThrowsAsync<RouletteSpeechUnavailableException>(() => generator.StatusAsync(default));
        await Assert.ThrowsAsync<RouletteSpeechUnavailableException>(() => generator.GenerateAsync("Privat navn", default));
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("wrong_type")]
    [InlineData("declared_oversized")]
    [InlineData("streamed_oversized")]
    [InlineData("redirect")]
    public async Task FailsClosedForUnexpectedResponsesWithoutFollowingAnotherDestination(string failure)
    {
        var handler = new Handler((_, _) =>
        {
            var response = AudioResponse(new byte[failure == "streamed_oversized" ? RouletteSpeechService.MaxAudioBytes + 1 : 48]);
            if (failure == "wrong_type") response.Content.Headers.ContentType = new("text/html");
            if (failure == "declared_oversized") response.Content.Headers.ContentLength = RouletteSpeechService.MaxAudioBytes + 1;
            if (failure == "redirect") { response.StatusCode = HttpStatusCode.Redirect; response.Headers.Location = new("http://example.org/"); }
            return Task.FromResult(response);
        });
        await Assert.ThrowsAsync<RouletteSpeechUnavailableException>(() => Create(handler).GenerateAsync("Gruppe blå", default));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GenerationPassesCancellationToLocalHttpRequest()
    {
        var handler = new Handler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return AudioResponse([]); });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(handler).GenerateAsync("Gruppe blå", cancellation.Token));
    }

    private static HttpResponseMessage AudioResponse(byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new NonSeekableStream(bytes)) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav"); return response;
    }
    private static RostSpeechGenerator Create(Handler handler, string url = "http://127.0.0.1:8091") =>
        new(new Factory(new HttpClient(handler)), Options.Create(new RouletteSpeechOptions { Enabled = true, BaseUrl = url }));
    private sealed class Factory(HttpClient client) : IHttpClientFactory
    { public HttpClient CreateClient(string name) { Assert.Equal("rost", name); return client; } }
    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    { public override bool CanSeek => false; }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { RequestCount++; return send(request, ct); }
    }
}
