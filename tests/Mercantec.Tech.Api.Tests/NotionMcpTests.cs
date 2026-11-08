using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Mcp;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotionMcpTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), other = Guid.NewGuid(), page = Guid.NewGuid();
    private IConfiguration config = null!;
    private FakeEngine engine = null!;
    private NotionMcpService service = null!;
    private const string Secret = "mcp-test-service-secret-at-least-32-characters";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options);
        await db.Database.EnsureCreatedAsync();
        db.NotionPages.Add(new() { Id = page, Title = "Databaser", CreatedBySub = teacher, YjsState = [1, 2, 3], DraftFingerprint = new('a', 64), CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Notion:ServiceSecret"] = Secret, ["Mcp:PublicUrl"] = "https://lup.example/api/mcp" }).Build();
        engine = new();
        service = new(db, new(db), new Clients(engine), config, new(config));
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private ClaimsPrincipal User(Guid? sub = null, bool isTeacher = true)
        => new(new ClaimsIdentity(new[] { new Claim("sub", (sub ?? teacher).ToString()) }
            .Concat(isTeacher ? [new Claim("role", "Teacher")] : []), "test"));
    private static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value, Json);
    private ApplyNotionMcpChanges Change(Guid? id = null, string text = "Ny tekst") => new(page, id ?? Guid.NewGuid(),
        [Element(new { type = "replace_text", blockId = "b1", contentFingerprint = new string('b', 64), from = 0, to = 3, text })], "Gør forklaringen tydeligere");

    [Fact]
    public async Task Real_stateless_mcp_transport_lists_five_tools_enforces_roles_and_binds_the_live_user()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Mcp:PublicUrl"] = "https://lup.example/api/mcp";
        builder.Configuration["Notion:ServiceSecret"] = Secret;
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddScoped<CourseAccess>();
        builder.Services.AddSingleton<NotebookServiceKey>();
        builder.Services.AddNotionMcp();
        builder.Services.AddSingleton<IHttpClientFactory>(new Clients(engine));
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", _ => { });
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapNotionMcp(); await app.StartAsync();
        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync(url + "/api/mcp", new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode);
        http.DefaultRequestHeaders.Authorization = new("Bearer", "student");
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync(url + "/api/mcp", new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode);
        var metadata = await http.GetStringAsync(url + "/.well-known/oauth-protected-resource/api/mcp");
        Assert.Contains("https://lup.example/api/mcp", metadata);
        Assert.DoesNotContain(Secret, metadata);
        engine.OnCall = (_, body) => Task.FromResult(Response(new { pageId = page, blocks = new[] { new { id = "b1", text = "live" } }, nextCursor = (string?)null, totalBlocks = 1 }));
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(url + "/api/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer teacher" }
        });
        await using var client = await McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();
        Assert.Equal(new[] { "apply_changes", "find_pages", "get_change", "read_page", "undo_change" }, tools.Select(t => t.Name).Order().ToArray());
        // MCP clients require object-valued top-level property schemas. JsonElement return
        // types otherwise export a boolean schema in properties.result, rejected by the TypeScript SDK.
        http.DefaultRequestHeaders.Authorization = new("Bearer", "teacher");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
        http.DefaultRequestHeaders.TryAddWithoutValidation("MCP-Protocol-Version", "2025-06-18");
        using var rawToolsResponse = await http.PostAsync(url + "/api/mcp", new StringContent("{\"jsonrpc\":\"2.0\",\"id\":4242,\"method\":\"tools/list\",\"params\":{}}", Encoding.UTF8, "application/json"));
        rawToolsResponse.EnsureSuccessStatusCode();
        var wire = await rawToolsResponse.Content.ReadAsStringAsync();
        if (rawToolsResponse.Content.Headers.ContentType?.MediaType == "text/event-stream")
            wire = wire.Split('\n').Single(line => line.StartsWith("data:", StringComparison.Ordinal))[5..].Trim();
        var rawResult = JsonSerializer.Deserialize<JsonElement>(wire).GetProperty("result");
        if (Environment.GetEnvironmentVariable("LUP_MCP_SCHEMA_OUTPUT") is { Length: > 0 } schemaPath)
            await File.WriteAllTextAsync(schemaPath, rawResult.GetRawText());
        foreach (var protocol in rawResult.GetProperty("tools").EnumerateArray())
        {
            if (!protocol.TryGetProperty("outputSchema", out var schema)) continue;
            Assert.Equal(JsonValueKind.Object, schema.ValueKind);
            if (schema.TryGetProperty("properties", out var properties))
                Assert.All(properties.EnumerateObject(), property => Assert.Equal(JsonValueKind.Object, property.Value.ValueKind));
        }
        var editSchema = tools.Single(t => t.Name == "apply_changes").JsonSchema.ToString();
        Assert.Contains("replace_text", editSchema);
        Assert.Contains("contentFingerprint", editSchema);
        Assert.DoesNotContain("user", tools.Single(t => t.Name == "read_page").JsonSchema.ToString());
        var result = await client.CallToolAsync("read_page", new Dictionary<string, object?> { ["page"] = page.ToString() });
        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        Assert.Equal("teacher", Assert.Single(engine.Calls).Body.GetProperty("accessToken").GetString());
        await app.StopAsync();
    }

    [Fact]
    public async Task Only_teachers_can_search_or_read_and_urls_never_trigger_external_fetches()
    {
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<NotionMcpException>(() => service.FindAsync(User(isTeacher: false), "", null, null, 0, 30, default))).Code);
        foreach (var url in new[] { "https://evil.example/notion/" + page, "https://lup.example@evil.example/notion/" + page,
            "https://lup.example/notebook/" + page, "/notion/" + Guid.Empty, "//lup.example/notion/" + page })
            Assert.Equal("invalid_page_url", (await Assert.ThrowsAsync<NotionMcpException>(() => service.ReadAsync(User(), "token", url, null, 30, default))).Code);
        Assert.Empty(engine.Calls);
        Assert.Equal(page, service.ResolvePage("https://lup.example/notion/" + page + "?readonly=true"));
    }

    [Fact]
    public async Task Search_filters_access_before_pagination_and_excludes_other_document_domains()
    {
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "Privat", CreatedBySub = other };
        var hold = new Hold { Id = Guid.NewGuid(), Name = "Privat hold", Forloeb = course };
        db.Hold.Add(hold);
        db.NotionPages.Add(new() { Id = Guid.NewGuid(), Title = "Hemmelig", HoldId = hold.Id, CreatedBySub = other });
        await db.SaveChangesAsync();
        var result = Element(await service.FindAsync(User(), null, null, null, 0, 30, default));
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.Equal(page, result.GetProperty("items")[0].GetProperty("pageId").GetGuid());
        Assert.Null(result.GetProperty("nextOffset").GetString());
    }

    [Fact]
    public async Task Live_read_is_delegated_with_user_token_and_continuation_is_not_discarded()
    {
        engine.OnCall = (_, body) => Task.FromResult(Response(new { blocks = new[] { new { id = "b1", text = "Live, endnu ikke gemt" } }, nextCursor = "next", totalBlocks = 70 }));
        var result = await service.ReadAsync(User(), "user-token", page.ToString(), "cursor", 30, default);
        Assert.Equal("next", result.GetProperty("document").GetProperty("nextCursor").GetString());
        var call = Assert.Single(engine.Calls);
        Assert.Equal("/internal/notion/mcp/read", call.Path);
        Assert.Equal("user-token", call.Body.GetProperty("accessToken").GetString());
        Assert.Equal("cursor", call.Body.GetProperty("cursor").GetString());
        Assert.Equal(Secret, call.ServiceKey);
        Assert.Empty(await db.NotionMcpChanges.ToListAsync());
    }

    [Fact]
    public async Task Success_requires_atomic_state_and_audit_ack_and_replay_never_calls_engine_again()
    {
        engine.OnCall = async (_, body) =>
        {
            var id = body.GetProperty("operationId").GetGuid();
            var row = await db.NotionMcpChanges.SingleAsync(c => c.Id == id);
            Assert.Equal("pending", row.Status);
            await Prepare(row);
            Assert.Equal("pending", row.Status);
            Assert.IsType<OkObjectResult>(await Store(row, [4, 5, 6]));
            return Response(new { operationId = id, status = "applied" });
        };
        var request = Change();
        var result = await service.ApplyAsync(User(), "token", request, default);
        Assert.Equal("applied", result.Status); Assert.True(result.CanUndo);
        Assert.Equal(new byte[] { 4, 5, 6 }, (await db.NotionPages.AsNoTracking().SingleAsync()).YjsState);
        var replay = await service.ApplyAsync(User(), "token", request, default);
        Assert.Equal(result.OperationId, replay.OperationId); Assert.Single(engine.Calls);
        Assert.Equal("operation_mismatch", (await Assert.ThrowsAsync<NotionMcpException>(() => service.ApplyAsync(User(), "token", Change(request.OperationId, "Andet indhold"), default))).Code);
        Assert.Single(engine.Calls);
    }

    [Fact]
    public async Task Engine_success_without_db_commit_is_never_reported_as_saved_and_retry_keeps_intent()
    {
        engine.OnCall = (_, _) => Task.FromResult(Response(new { status = "applied" }));
        var request = Change();
        Assert.Equal("change_not_durable", (await Assert.ThrowsAsync<NotionMcpException>(() => service.ApplyAsync(User(), "token", request, default))).Code);
        var row = await db.NotionMcpChanges.SingleAsync();
        Assert.Equal("pending", row.Status); Assert.Equal("change_not_durable", row.Error);
        engine.OnCall = async (_, _) => { await Prepare(row); await Store(row, [7, 8]); return Response(new { status = "applied" }); };
        Assert.Equal("applied", (await service.ApplyAsync(User(), "token", request, default)).Status);
        Assert.Equal(2, engine.Calls.Count); Assert.Single(await db.NotionMcpChanges.ToListAsync());
    }

    [Fact]
    public async Task Unknown_or_unprepared_audit_never_overwrites_document_state()
    {
        var row = new NotionMcpChange { Id = Guid.NewGuid(), PageId = page, OwnerSub = teacher, RequestHash = new('c', 64), CreatedAtUtc = DateTimeOffset.UtcNow };
        db.NotionMcpChanges.Add(row); await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await Store(row, [9, 9]));
        Assert.Equal(new byte[] { 1, 2, 3 }, (await db.NotionPages.SingleAsync()).YjsState);
        Assert.Equal("pending", row.Status);
        Assert.Equal("operation_mismatch", (await Assert.ThrowsAsync<NotionMcpException>(() => service.PrepareAsync(page,
            new(row.Id, other, row.RequestHash, Element(new { blocks = new[] { "before" } }), Element(new { blocks = new[] { "after" } })), default))).Code);
    }

    [Fact]
    public async Task Definitive_conflicts_are_visible_and_retry_can_resume_the_same_operation()
    {
        engine.OnCall = (_, _) => Task.FromResult(Response(new { error = "target_changed" }, HttpStatusCode.Conflict));
        var request = Change();
        Assert.Equal("target_changed", (await Assert.ThrowsAsync<NotionMcpException>(() => service.ApplyAsync(User(), "token", request, default))).Code);
        var row = await db.NotionMcpChanges.SingleAsync();
        Assert.Equal("conflict", row.Status);
        engine.OnCall = async (_, _) => { Assert.Equal("pending", row.Status); await Prepare(row); await Store(row, [6, 7]); return Response(new { status = "applied" }); };
        Assert.Equal("applied", (await service.ApplyAsync(User(), "token", request, default)).Status);
        Assert.Null(row.Error);
    }

    [Fact]
    public async Task A_reloaded_browser_resumes_its_pending_undo_without_needing_the_first_request_id()
    {
        var original = new NotionMcpChange { Id = Guid.NewGuid(), PageId = page, OwnerSub = teacher, RequestHash = new('c', 64), CreatedAtUtc = DateTimeOffset.UtcNow, Status = "applied",
            BeforeJson = "{\"blocks\":[\"before\"]}", AfterJson = "{\"blocks\":[\"after\"]}", AppliedAtUtc = DateTimeOffset.UtcNow };
        db.NotionMcpChanges.Add(original); await db.SaveChangesAsync();
        engine.OnCall = (_, _) => Task.FromResult(Response(new { error = "store_failed" }, HttpStatusCode.ServiceUnavailable));
        var firstReversal = Guid.NewGuid();
        await Assert.ThrowsAsync<NotionMcpException>(() => service.UndoAsync(original.Id, firstReversal, User(), "token", default));
        engine.OnCall = async (_, body) =>
        {
            Assert.Equal(firstReversal, body.GetProperty("operationId").GetGuid());
            var reverse = await db.NotionMcpChanges.SingleAsync(c => c.Id == firstReversal);
            await Prepare(reverse); await Store(reverse, [10, 11]); return Response(new { status = "applied" });
        };
        Assert.Equal("undone", (await service.UndoAsync(original.Id, Guid.NewGuid(), User(), "token", default)).Status);
        Assert.Equal(2, await db.NotionMcpChanges.CountAsync());
    }

    [Fact]
    public async Task Audit_is_private_and_current_hold_access_is_rechecked()
    {
        var row = new NotionMcpChange { Id = Guid.NewGuid(), PageId = page, OwnerSub = teacher, RequestHash = new('c', 64), CreatedAtUtc = DateTimeOffset.UtcNow };
        db.NotionMcpChanges.Add(row); await db.SaveChangesAsync();
        Assert.Equal("change_not_found", (await Assert.ThrowsAsync<NotionMcpException>(() => service.GetAsync(row.Id, User(other), default))).Code);
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "Privat", CreatedBySub = other };
        var hold = new Hold { Id = Guid.NewGuid(), Name = "Privat hold", Forloeb = course }; db.Hold.Add(hold);
        (await db.NotionPages.SingleAsync()).HoldId = hold.Id; await db.SaveChangesAsync();
        Assert.Equal("page_not_found", (await Assert.ThrowsAsync<NotionMcpException>(() => service.GetAsync(row.Id, User(), default))).Code);
        Assert.Equal("page_not_found", (await Assert.ThrowsAsync<NotionMcpException>(() => service.ListAsync(page, User(), default))).Code);
    }

    [Fact]
    public async Task Undo_is_durable_atomic_and_second_undo_is_an_idempotent_read()
    {
        var original = new NotionMcpChange { Id = Guid.NewGuid(), PageId = page, OwnerSub = teacher, RequestHash = new('c', 64), CreatedAtUtc = DateTimeOffset.UtcNow, Status = "applied",
            BeforeJson = "{\"blocks\":[\"before\"]}", AfterJson = "{\"blocks\":[\"after\"]}", AppliedAtUtc = DateTimeOffset.UtcNow };
        db.NotionMcpChanges.Add(original); await db.SaveChangesAsync();
        engine.OnCall = async (action, body) =>
        {
            Assert.Equal("/internal/notion/mcp/undo", action);
            Assert.Equal("before", body.GetProperty("originalChange").GetProperty("before").GetProperty("blocks")[0].GetString());
            var reverse = await db.NotionMcpChanges.SingleAsync(c => c.Id == body.GetProperty("operationId").GetGuid());
            await Prepare(reverse); await Store(reverse, [10, 11]); return Response(new { status = "applied" });
        };
        var reversal = Guid.NewGuid();
        var result = await service.UndoAsync(original.Id, reversal, User(), "token", default);
        Assert.Equal("undone", result.Status); Assert.False(result.CanUndo); Assert.NotNull(result.UndoneAtUtc);
        Assert.Equal("undone", (await service.UndoAsync(original.Id, reversal, User(), "token", default)).Status);
        Assert.Single(engine.Calls);
    }

    private Task Prepare(NotionMcpChange row) => service.PrepareAsync(page,
        new(row.Id, teacher, row.RequestHash, Element(new { blocks = new[] { "before" } }), Element(new { blocks = new[] { "after" } }), row.Summary, row.UndoOf), default);

    private Task<IActionResult> Store(NotionMcpChange row, byte[] bytes)
    {
        var request = new StoreNotionDocument(Convert.ToBase64String(bytes), new('d', 64), [], null, [], new(row.Id, row.OwnerSub, row.RequestHash));
        var http = new DefaultHttpContext(); http.Request.Headers["X-Notion-Service-Key"] = Secret;
        http.Request.ContentType = "application/json";
        http.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(request, Json));
        return new NotionStorageController(db, config) { ControllerContext = new() { HttpContext = http } }.WriteState(page, default);
    }
    private static HttpResponseMessage Response(object body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json") };
    private sealed class Clients(FakeEngine engine) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(engine, false); }
    private sealed class FakeEngine : HttpMessageHandler
    {
        public List<(string Path, JsonElement Body, string ServiceKey)> Calls { get; } = [];
        public Func<string, JsonElement, Task<HttpResponseMessage>>? OnCall { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(ct));
            Calls.Add((request.RequestUri!.AbsolutePath, body, request.Headers.GetValues("X-Notion-Service-Key").Single()));
            return OnCall == null ? Response(new { }) : await OnCall(request.RequestUri.AbsolutePath, body);
        }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var token = Request.Headers.Authorization.ToString();
            if (!token.StartsWith("Bearer ")) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim("sub", "8ffb71c6-9574-4f86-a6b8-69395c94cc01"), new Claim("role", token == "Bearer teacher" ? "Teacher" : "Student")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
