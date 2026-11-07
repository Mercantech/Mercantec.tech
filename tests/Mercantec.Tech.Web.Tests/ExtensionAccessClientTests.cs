using System.Net;
using System.Text.Json;
using Mercantec.Tech.Web.Extensions;

namespace Mercantec.Tech.Web.Tests;

public sealed class ExtensionAccessClientTests
{
    private static readonly Guid Id = Guid.NewGuid();
    // Synthetic payload only: no corresponding server credential exists.
    private static readonly string DummyKey = "lup_" + new string('A', 43);

    [Fact]
    public async Task ExistingConnectionsContainOnlyMetadataEvenIfTheResponseHasExtraSecretFields()
    {
        var client = Client(_ => Json(new { enabled = true, keys = new[] { new {
            id = Id, label = "Min Chrome", createdAtUtc = DateTimeOffset.UtcNow, expiresAtUtc = DateTimeOffset.UtcNow.AddDays(90),
            key = DummyKey, keyHash = "NOT-A-REAL-HASH" } } }));
        var result = await client.Get();
        Assert.True(result.Enabled);
        Assert.Equal("Min Chrome", Assert.Single(result.Keys).Label);
        var displayData = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(DummyKey, displayData);
        Assert.DoesNotContain("NOT-A-REAL-HASH", displayData);
    }

    [Fact]
    public async Task CreationSendsOnlyALabelAndReturnsTheSingleUseCredential()
    {
        string? path = null, body = null;
        var client = Client(async request => {
            path = request.RequestUri!.PathAndQuery;
            body = await request.Content!.ReadAsStringAsync();
            Assert.Equal(HttpMethod.Post, request.Method);
            return Json(new { id = Id, key = DummyKey, label = "Arbejde", createdAtUtc = DateTimeOffset.UtcNow, expiresAtUtc = DateTimeOffset.UtcNow.AddDays(90) });
        });
        var result = await client.Create("Arbejde");
        Assert.Equal("/api/extension/access", path);
        Assert.Equal("Arbejde", JsonDocument.Parse(body!).RootElement.GetProperty("label").GetString());
        Assert.DoesNotContain(DummyKey, body!);
        Assert.Equal(DummyKey, result.Key);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task ErrorBodiesNeverExposeCredentials(HttpStatusCode status)
    {
        var client = Client(_ => new HttpResponseMessage(status) { Content = new StringContent(DummyKey) });
        var error = await Assert.ThrowsAsync<ExtensionAccessException>(() => client.Create("Chrome"));
        Assert.DoesNotContain(DummyKey, error.Message);
        Assert.DoesNotContain("lup_", error.Message);
    }

    [Fact]
    public async Task RevocationUsesOnlyTheConnectionIdAndAcceptsNoContent()
    {
        var client = Client(request => {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal($"/api/extension/access/{Id:D}", request.RequestUri!.AbsolutePath);
            Assert.Null(request.Content);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        await client.Revoke(Id);
    }

    [Fact]
    public async Task ACorruptCredentialResponseDoesNotLeakItsBody()
    {
        var client = Client(_ => Json(new { id = Id, key = "corrupt-sensitive-payload", label = "Chrome", createdAtUtc = DateTimeOffset.UtcNow, expiresAtUtc = DateTimeOffset.UtcNow.AddDays(90) }));
        var error = await Assert.ThrowsAsync<ExtensionAccessException>(() => client.Create("Chrome"));
        Assert.DoesNotContain("corrupt-sensitive-payload", error.Message);
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json") };

    private static ExtensionAccessClient Client(Func<HttpRequestMessage, HttpResponseMessage> action) => Client(request => Task.FromResult(action(request)));
    private static ExtensionAccessClient Client(Func<HttpRequestMessage, Task<HttpResponseMessage>> action) =>
        new(new HttpClient(new Handler(action)) { BaseAddress = new Uri("https://lup.invalid/api/") });
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request);
    }
}
