using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Updates;
using Mercantec.Tech.Api.Updates.Automation;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class PlatformUpdatesAutomationHttpTests
{
    [Fact]
    public async Task LoopbackApiPublishesAnalyzedBeforeAfterImagesAndKeepsTeacherAccessControl()
    {
        const string secret = "loopback-test-only-release-service-key-48-characters";
        const string commit = "ffffffffffffffffffffffffffffffffffffffff";
        const string previous = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        const string revision = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        var directory = Path.Combine(Path.GetTempPath(), "lup-updates-http-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var keyFile = Path.Combine(directory, "service-key");
            await File.WriteAllTextAsync(keyFile, secret);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing", ContentRootPath = directory,
                ApplicationName = typeof(PlatformUpdatesAutomationController).Assembly.GetName().Name
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddControllers().AddApplicationPart(typeof(PlatformUpdatesAutomationController).Assembly);
            builder.Services.AddAuthentication("LoopbackFixture")
                .AddScheme<AuthenticationSchemeOptions, LoopbackAuthentication>("LoopbackFixture", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.Configure<PlatformUpdatesAutomationOptions>(options =>
            { options.StoragePath = Path.Combine(directory, "private-updates"); options.ServiceKeyFile = keyFile; });
            builder.Services.Configure<OpenAiOptions>(options =>
            { options.ApiKey = "loopback-fake-openai-credential"; options.Model = "configured-fixture-model"; });
            using var modelFactory = new FakeModelFactory();
            builder.Services.AddSingleton<IHttpClientFactory>(modelFactory);
            builder.Services.AddSingleton<IDeploymentRevision>(new FixedRevision(revision));
            builder.Services.AddSingleton<PlatformUpdatesAutomationKey>();
            builder.Services.AddSingleton<PlatformUpdatesAutomationStore>();
            builder.Services.AddSingleton<PlatformUpdatesFeed>();
            builder.Services.AddScoped<PlatformUpdatesAutomationAnalyzer>();
            await using var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            try
            {
                using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
                { BaseAddress = new Uri(app.Urls.Single()) };
                var analysisRequest = new PlatformUpdatesAnalyzeRequest(previous, commit,
                    "diff --git a/web/Pages/Notebook.razor b/web/Pages/Notebook.razor\n+<input placeholder=\"Find din note\">",
                    ["web/Pages/Notebook.razor"], [new("notebook-search", "Søgning i notesbogen")], ["Kasper"]);
                using (var noKey = await client.PostAsJsonAsync("/api/platform-updates/automation/analyze", analysisRequest))
                    Assert.Equal(HttpStatusCode.Unauthorized, noKey.StatusCode);
                using (var noKey = await client.GetAsync("/api/platform-updates/automation/state"))
                    Assert.Equal(HttpStatusCode.Unauthorized, noKey.StatusCode);
                using var analysisMessage = InternalPost("analyze", analysisRequest, secret);
                using var analysisResponse = await client.SendAsync(analysisMessage);
                Assert.Equal(HttpStatusCode.OK, analysisResponse.StatusCode);
                var analysis = await analysisResponse.Content.ReadFromJsonAsync<PlatformUpdatesAnalysis>();
                var generated = Assert.Single(analysis!.Entries);
                Assert.Equal("notebook-search", generated.ScenarioId);
                Assert.Equal(1, modelFactory.Handler.Calls);
                var png = TestPng();
                var image = new PlatformUpdatesPublishImage("Notesbogens søgning med fiktive data.", Convert.ToBase64String(png));
                var publication = new PlatformUpdatesPublishRequest(previous, commit, revision,
                    new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero),
                    [new(generated.Id, generated.Title, generated.Description, ["Kasper"], image, image)]);
                using (var noKey = await client.PostAsJsonAsync("/api/platform-updates/automation/publish", publication))
                    Assert.Equal(HttpStatusCode.Unauthorized, noKey.StatusCode);
                using var publishMessage = InternalPost("publish", publication, secret);
                using var publishResponse = await client.SendAsync(publishMessage);
                Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
                using (var guest = await client.GetAsync("/api/platform-updates?includeText=true"))
                    Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
                using (var student = await RoleGet(client, "/api/platform-updates?includeText=true", "Student"))
                    Assert.Equal(HttpStatusCode.Forbidden, student.StatusCode);
                using var teacher = await RoleGet(client, "/api/platform-updates?includeText=true", "Teacher");
                Assert.Equal(HttpStatusCode.OK, teacher.StatusCode);
                Assert.Equal("no-store", teacher.Headers.CacheControl!.ToString());
                var feed = await teacher.Content.ReadFromJsonAsync<PlatformUpdate[]>();
                var entry = Assert.Single(feed!, item => item.Id == generated.Id);
                Assert.Equal(generated.Description, entry.Description);
                Assert.Equal(new[] { "Kasper" }, entry.Authors);
                Assert.NotNull(entry.After);
                Assert.NotNull(entry.Before);
                foreach (var reference in new[] { entry.After!, entry.Before! })
                {
                    var url = "/api/platform-updates/images/" + reference.Id;
                    using (var guest = await client.GetAsync(url)) Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
                    using (var student = await RoleGet(client, url, "Student")) Assert.Equal(HttpStatusCode.Forbidden, student.StatusCode);
                    using var picture = await RoleGet(client, url, "Teacher");
                    Assert.Equal(HttpStatusCode.OK, picture.StatusCode);
                    Assert.Equal("image/png", picture.Content.Headers.ContentType!.MediaType);
                    Assert.Equal(png, await picture.Content.ReadAsByteArrayAsync());
                }
                using var stateMessage = InternalPost("state", new PlatformUpdatesAutomationState(commit, revision,
                    publication.CapturedAtUtc, "ready", true, commit, revision, 1, 1), secret);
                using var stateResponse = await client.SendAsync(stateMessage);
                Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
                using var deploymentResponse = await client.GetAsync("/api/platform-updates/deployment");
                Assert.Equal(HttpStatusCode.OK, deploymentResponse.StatusCode);
                using var deploymentBody = JsonDocument.Parse(await deploymentResponse.Content.ReadAsStringAsync());
                Assert.True(deploymentBody.RootElement.GetProperty("automationReady").GetBoolean());
                Assert.Equal(revision, deploymentBody.RootElement.GetProperty("revision").GetString());
            }
            finally { await app.StopAsync(); }
        }
        finally
        {
            var target = Path.GetFullPath(directory);
            var allowed = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (target.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(target).StartsWith("lup-updates-http-tests-", StringComparison.Ordinal))
                Directory.Delete(target, true);
        }
    }

    private static HttpRequestMessage InternalPost<T>(string action, T body, string secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/platform-updates/automation/" + action)
            { Content = JsonContent.Create(body) };
        request.Headers.Add("X-LUP-Updates-Key", secret);
        return request;
    }
    private static async Task<HttpResponseMessage> RoleGet(HttpClient client, string url, string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Loopback-Test-Role", role);
        return await client.SendAsync(request);
    }
    private static byte[] TestPng()
    {
        using var source = typeof(PlatformUpdatesCatalog).Assembly.GetManifestResourceStream("standard-emojis/1f389.png")!;
        using var memory = new MemoryStream();
        source.CopyTo(memory);
        return memory.ToArray();
    }
    private sealed class FixedRevision(string revision) : IDeploymentRevision { public string? Read() => revision; }
    private sealed class FakeModelFactory : IHttpClientFactory, IDisposable
    {
        public FakeModelHandler Handler { get; } = new();
        private readonly HttpClient _client;
        public FakeModelFactory() => _client = new HttpClient(Handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        public HttpClient CreateClient(string name) => _client;
        public void Dispose() => _client.Dispose();
    }
    private sealed class FakeModelHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var answer = JsonSerializer.Serialize(new { entries = new[] { new {
                id = "loopback-notebook-search", title = "Find testnoter hurtigere",
                description = "Søg efter titler og indhold i notesbogen.", scenarioId = "notebook-search" } } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(new { status = "completed", output = new[] { new {
                type = "message", content = new[] { new { type = "output_text", text = answer } } } } }) });
        }
    }
    private sealed class LoopbackAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Loopback-Test-Role"].ToString();
            if (role is not ("Teacher" or "Student")) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", "11111111-1111-1111-1111-111111111111"), new Claim("role", role)
            ], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
