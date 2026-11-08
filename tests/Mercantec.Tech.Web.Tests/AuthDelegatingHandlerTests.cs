using System.Net;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Web.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class AuthDelegatingHandlerTests
{
    [Fact]
    public async Task FailedRefreshReturnsAReadableUnauthorizedResponse()
    {
        var storage = new AuthTokenJs();
        using var transport = new RecordingHandler(HttpStatusCode.Unauthorized);
        using var client = CreateClient(storage, transport);

        using var response = await client.GetAsync("me/avatars");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("{\"detail\":\"expired\"}", await response.Content.ReadAsStringAsync());
        Assert.Equal(2, storage.TokenReads); // Initial token, then failed refresh lookup.
        Assert.Equal(1, transport.Requests);
    }

    [Fact]
    public async Task EachUnauthorizedRequestGetsItsOwnRefreshAttempt()
    {
        var storage = new AuthTokenJs();
        using var transport = new RecordingHandler(HttpStatusCode.Unauthorized);
        using var client = CreateClient(storage, transport);

        using var first = await client.GetAsync("me/avatars");
        using var second = await client.GetAsync("me/avatars");

        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal("{\"detail\":\"expired\"}", await first.Content.ReadAsStringAsync());
        Assert.Equal("{\"detail\":\"expired\"}", await second.Content.ReadAsStringAsync());
        Assert.Equal(4, storage.TokenReads);
        Assert.Equal(2, transport.Requests);
    }

    [Fact]
    public async Task SuccessfulRequestKeepsItsAccountGuardAndDoesNotRefresh()
    {
        var storage = new AuthTokenJs();
        using var transport = new RecordingHandler(HttpStatusCode.OK);
        using var client = CreateClient(storage, transport);
        using var request = new HttpRequestMessage(HttpMethod.Get, "me/avatars");
        request.Headers.Add("X-LUP-Avatar-Subject", AuthTokenJs.Subject);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, storage.TokenReads);
        Assert.Equal(AuthTokenJs.Subject, transport.ExpectedSubject);
        Assert.Equal("Bearer", transport.AuthorizationScheme);
        Assert.Equal(storage.AccessToken, transport.AuthorizationToken);
    }

    private static HttpClient CreateClient(AuthTokenJs storage, HttpMessageHandler transport)
    {
        var auth = new AuthService(Options.Create(new AuthOptions()), new TokenStore(storage), new TestNavigation());
        return new HttpClient(new AuthDelegatingHandler(auth) { InnerHandler = transport })
        {
            BaseAddress = new Uri("https://platform.test/api/")
        };
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string? ExpectedSubject { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            ExpectedSubject = request.Headers.TryGetValues("X-LUP-Avatar-Subject", out var subjects) ? subjects.Single() : null;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationToken = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                RequestMessage = request,
                Content = new StringContent("{\"detail\":\"expired\"}", Encoding.UTF8, "application/problem+json")
            });
        }
    }

    private sealed class AuthTokenJs : IJSRuntime
    {
        public const string Subject = "00000000-0000-0000-0000-000000000201";
        public int TokenReads { get; private set; }
        public string AccessToken { get; } = Token();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (identifier != "mercantecAuth.getTokens") throw new InvalidOperationException(identifier);
            TokenReads++;
            // No refresh token means refresh fails locally; no real auth or
            // network endpoint is contacted by these tests.
            var json = JsonSerializer.Serialize(new
            {
                access = AccessToken,
                refresh = (string?)null,
                expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
            });
            return ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(json)!);
        }

        private static string Token()
        {
            static string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return Part("{\"alg\":\"none\",\"typ\":\"JWT\"}") + "." +
                Part(JsonSerializer.Serialize(new { sub = Subject, name = "Local test", exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() })) + ".";
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://platform.test/", "https://platform.test/");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }
}
