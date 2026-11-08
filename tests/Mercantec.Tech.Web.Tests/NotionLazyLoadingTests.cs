using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Emojis;
using Mercantec.Tech.Web.Notion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NotionPage = Mercantec.Tech.Web.Pages.Notion.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class NotionLazyLoadingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Direct_links_mount_the_active_document_before_slow_sidebar_requests()
    {
        using var api = new LazyApi { MetadataGate = Gate() };
        using var context = await CreateContext(api);
        var editor = context.Services.GetRequiredService<LazyJavascript>().Editor;
        try
        {
            var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.FirstId));
            page.WaitForAssertion(() => Assert.Equal(api.FirstId, Assert.Single(editor.Mounts).PageId), Timeout);
            Assert.Equal("Første side", page.Find("#notion-title").GetAttribute("value"));

            page.WaitForAssertion(() => Assert.Contains(api.Requests, r => r.Path is "/api/notion/holds" or "/api/notion/pages"), Timeout);
            var events = api.Events.ToArray();
            var mountIndex = Array.FindIndex(events, e => e == $"mount:{api.FirstId}");
            Assert.True(mountIndex > 0);
            Assert.DoesNotContain(events.Take(mountIndex), IsSecondaryRequest);
            Assert.Single(api.Requests, r => r.Path.EndsWith("/context", StringComparison.Ordinal));
            Assert.Contains(api.Requests, r => r.Path.EndsWith("/context", StringComparison.Ordinal)
                && r.Query.Contains("includeResources=false", StringComparison.Ordinal));
            Assert.DoesNotContain(api.Requests, r => IsResourceRequest(r.Path));
        }
        finally { api.MetadataGate.TrySetResult(true); }
    }

    [Fact]
    public async Task Sidebar_failures_keep_the_active_document_available()
    {
        using var api = new LazyApi { FailMetadata = true };
        using var context = await CreateContext(api);
        var editor = context.Services.GetRequiredService<LazyJavascript>().Editor;
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.FirstId));

        page.WaitForAssertion(() => Assert.Equal(api.FirstId, Assert.Single(editor.Mounts).PageId), Timeout);
        page.WaitForAssertion(() => Assert.Contains(api.Requests, r => r.Path is "/api/notion/holds" or "/api/notion/pages"), Timeout);
        Assert.Equal("Første side", page.Find("#notion-title").GetAttribute("value"));
        Assert.Single(page.FindAll(".notion-editor-root"));
        Assert.Equal(0, editor.Destroys);
        Assert.DoesNotContain(api.Requests, r => IsResourceRequest(r.Path));
    }

    [Fact]
    public async Task Navigating_away_cancels_a_pending_document_and_ignores_its_late_response()
    {
        using var api = new LazyApi { FirstContextGate = Gate() };
        using var context = await CreateContext(api);
        var editor = context.Services.GetRequiredService<LazyJavascript>().Editor;
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.FirstId));
        try
        {
            page.WaitForAssertion(() => Assert.Contains(api.Requests, r => r.Path == api.ContextPath(api.FirstId)), Timeout);
            page.Render(p => p.Add(x => x.PageId, api.SecondId));
            page.WaitForAssertion(() => Assert.Equal(api.SecondId, Assert.Single(editor.Mounts).PageId), Timeout);
            Assert.True(api.FirstContextCancelled);

            api.FirstContextGate.TrySetResult(true);
            await api.FirstContextReturned.Task.WaitAsync(Timeout);
            await page.InvokeAsync(() => { });
            Assert.Equal("Anden side", page.Find("#notion-title").GetAttribute("value"));
            Assert.Equal(api.SecondId, Assert.Single(editor.Mounts).PageId);
            Assert.DoesNotContain(editor.ContextUpdates, update => update.PageId == api.FirstId);
        }
        finally { api.FirstContextGate.TrySetResult(true); }
    }

    [Fact]
    public async Task Navigating_away_cancels_old_sidebar_requests_without_blocking_the_next_document()
    {
        using var api = new LazyApi { MetadataGate = Gate() };
        using var context = await CreateContext(api);
        var editor = context.Services.GetRequiredService<LazyJavascript>().Editor;
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.FirstId));
        try
        {
            page.WaitForAssertion(() => Assert.Contains(api.Requests, r => r.Path is "/api/notion/holds" or "/api/notion/pages"), Timeout);
            var pendingMetadata = api.Requests.Where(r => r.Path is "/api/notion/holds" or "/api/notion/pages").ToArray();
            page.Render(p => p.Add(x => x.PageId, api.SecondId));
            page.WaitForAssertion(() => Assert.Equal(api.SecondId, editor.Mounts.Last().PageId), Timeout);
            Assert.All(pendingMetadata, request => Assert.True(request.CancellationToken.IsCancellationRequested));
            Assert.Equal("Anden side", page.Find("#notion-title").GetAttribute("value"));
            Assert.Equal(1, editor.Destroys);
        }
        finally { api.MetadataGate.TrySetResult(true); }
    }

    [Fact]
    public async Task Published_links_only_load_the_published_document_and_published_navigation()
    {
        using var api = new LazyApi();
        using var context = await CreateContext(api);
        var editor = context.Services.GetRequiredService<LazyJavascript>().Editor;
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/notion/{api.FirstId}?published=true");
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.FirstId));

        page.WaitForAssertion(() => Assert.True(Assert.Single(editor.Mounts).ReadOnly), Timeout);
        page.WaitForAssertion(() => Assert.Contains(api.Requests, r => r.Path == "/api/notion/pages"), Timeout);
        Assert.Contains(api.Requests, r => r.Path == api.ContextPath(api.FirstId, published: true));
        Assert.DoesNotContain(api.Requests, r => r.Path.EndsWith("/context", StringComparison.Ordinal));
        Assert.All(api.Requests.Where(r => r.Path == "/api/notion/pages"),
            r => Assert.Contains("publishedOnly=true", r.Query));
        Assert.Empty(page.FindAll("#notion-title"));
        Assert.DoesNotContain(api.Requests, r => IsResourceRequest(r.Path));
    }

    [Fact]
    public async Task Overlapping_resource_requests_share_one_load_and_update_without_remounting()
    {
        using var api = new LazyApi { ResourceGate = Gate() };
        using var context = await CreateContext(api);
        var editor = context.Services.GetRequiredService<LazyJavascript>().Editor;
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.FirstId));
        page.WaitForAssertion(() => Assert.Single(editor.Mounts), Timeout);
        Assert.DoesNotContain(api.Requests, r => IsResourceRequest(r.Path));

        var first = page.InvokeAsync(() => page.Instance.EnsureNotionResources());
        var second = page.InvokeAsync(() => page.Instance.EnsureNotionResources());
        try
        {
            page.WaitForAssertion(() => Assert.Contains(api.Requests, r => r.Path.EndsWith("/editor-resources", StringComparison.Ordinal)), Timeout);
            api.ResourceGate.TrySetResult(true);
            await Task.WhenAll(first, second).WaitAsync(Timeout);
            Assert.Single(api.Requests, r => r.Path.EndsWith("/editor-resources", StringComparison.Ordinal));
            Assert.Single(api.Requests, r => r.Path.EndsWith("/groups", StringComparison.Ordinal));
            Assert.Single(editor.Mounts);
            Assert.Equal(0, editor.Destroys);
            Assert.Contains(editor.ContextUpdates, update => update.Members == 1 && update.Polls == 1 && update.Groups == 1);
        }
        finally { api.ResourceGate.TrySetResult(true); }
    }

    [Fact]
    public async Task Failed_optional_resources_can_be_retried_without_reloading_the_document()
    {
        using var api = new LazyApi { FailResources = true };
        using var context = await CreateContext(api);
        var editor = context.Services.GetRequiredService<LazyJavascript>().Editor;
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.FirstId));
        page.WaitForAssertion(() => Assert.Single(editor.Mounts), Timeout);

        var loaded = true;
        await page.InvokeAsync(async () => { loaded = await page.Instance.EnsureNotionResources(); });
        Assert.False(loaded);
        Assert.Equal("Første side", page.Find("#notion-title").GetAttribute("value"));

        api.FailResources = false;
        await page.InvokeAsync(async () => { loaded = await page.Instance.EnsureNotionResources(); });
        Assert.True(loaded);
        Assert.Single(editor.Mounts);
        Assert.Equal(0, editor.Destroys);
        Assert.Contains(editor.ContextUpdates, update => update.Members == 1 && update.Polls == 1 && update.Groups == 1);
        Assert.Equal(2, api.Requests.Count(r => r.Path.EndsWith("/editor-resources", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Large_sidebars_render_a_small_window_and_keep_the_active_page_visible()
    {
        using var api = new LazyApi { AdditionalPages = 1_000 };
        using var context = await CreateContext(api);
        var editor = context.Services.GetRequiredService<LazyJavascript>().Editor;
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.FirstId));

        page.WaitForAssertion(() => Assert.Contains(editor.ContextUpdates, update => update.Pages == 1_002), Timeout);
        Assert.InRange(page.FindAll(".notion-page-row").Count, 1, 80);
        var current = Assert.Single(page.FindAll(".notion-current-page .notion-page.is-current"));
        Assert.Equal("page", current.GetAttribute("aria-current"));
        Assert.Contains("Første side", current.TextContent);
        Assert.Single(api.Requests, r => r.Path.EndsWith("/context", StringComparison.Ordinal));
        Assert.DoesNotContain(api.Requests, r => IsResourceRequest(r.Path));
        Assert.Single(editor.Mounts);
    }

    [Fact]
    public async Task Navigation_during_editor_import_mounts_only_the_latest_page_and_shares_the_import()
    {
        using var api = new LazyApi();
        using var context = await CreateContext(api);
        var javascript = context.Services.GetRequiredService<LazyJavascript>();
        javascript.ImportGate = Gate();
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.FirstId));
        try
        {
            await javascript.ImportStarted.Task.WaitAsync(Timeout);
            Assert.Empty(javascript.Editor.Mounts);
            page.Render(p => p.Add(x => x.PageId, api.SecondId));
            page.WaitForAssertion(() => Assert.Equal("Anden side", page.Find("#notion-title").GetAttribute("value")), Timeout);
            Assert.Empty(javascript.Editor.Mounts);

            javascript.ImportGate.TrySetResult(true);
            page.WaitForAssertion(() => Assert.Equal(api.SecondId, Assert.Single(javascript.Editor.Mounts).PageId), Timeout);
            Assert.Equal(1, javascript.Imports);
            Assert.Equal(0, javascript.Editor.Destroys);
            Assert.DoesNotContain(javascript.Editor.ContextUpdates, update => update.PageId == api.FirstId);
        }
        finally { javascript.ImportGate.TrySetResult(true); }
    }

    [Fact]
    public async Task Disposing_during_editor_import_releases_the_late_module_without_mounting()
    {
        using var api = new LazyApi();
        using var context = await CreateContext(api);
        var javascript = context.Services.GetRequiredService<LazyJavascript>();
        javascript.ImportGate = Gate();
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.FirstId));
        try
        {
            await javascript.ImportStarted.Task.WaitAsync(Timeout);
            var disposal = DisposeComponents();
            // Let the renderer start component disposal before the import resolves.
            await context.Renderer.Dispatcher.InvokeAsync(() => { });
            Assert.False(disposal.IsCompleted);
            Assert.Empty(javascript.Editor.Mounts);

            javascript.ImportGate.TrySetResult(true);
            await disposal.WaitAsync(Timeout);
            Assert.True(page.IsDisposed);
            Assert.Equal(1, javascript.Imports);
            Assert.Equal(1, javascript.Editor.Disposals);
            Assert.Empty(javascript.Editor.Mounts);
            Assert.Empty(javascript.Editor.ContextUpdates);
            Assert.DoesNotContain(api.Requests, request => request.Path is "/api/notion/holds" or "/api/notion/pages");
        }
        finally { javascript.ImportGate.TrySetResult(true); }

        async Task DisposeComponents() => await context.DisposeComponentsAsync();
    }

    private static bool IsSecondaryRequest(string entry) => entry.StartsWith("GET /api/notion/holds", StringComparison.Ordinal)
        || entry == "GET /api/notion/pages";
    private static bool IsResourceRequest(string path) => path.EndsWith("/groups", StringComparison.Ordinal)
        || path.EndsWith("/editor-resources", StringComparison.Ordinal);
    private static TaskCompletionSource<bool> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Task<LearningUiTestContext> CreateContext(LazyApi api)
        => LearningUiTestContext.CreateAsync(api, teacher: true, configure: services =>
        {
            var original = (IJSRuntime)services.Last(s => s.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            var javascript = new LazyJavascript(original, api);
            services.AddSingleton(javascript);
            services.AddSingleton<IJSRuntime>(javascript);
            services.AddSingleton<NotionClient>();
        });

    private sealed record EditorSnapshot(Guid PageId, bool ReadOnly, int Members, int Polls, int Groups, int Pages);
    private sealed class LazyJavascript(IJSRuntime original, LazyApi api) : IJSRuntime
    {
        public EditorModule Editor { get; } = new(api);
        private NoopSidebarModule Sidebar { get; } = new();
        public TaskCompletionSource<bool>? ImportGate { get; set; }
        public TaskCompletionSource<bool> ImportStarted { get; } = Gate();
        public int Imports;
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args)
            => InvokeAsync<TValue>(id, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args)
            => id != "import" ? original.InvokeAsync<TValue>(id, ct, args)
                : args?.FirstOrDefault()?.ToString()?.Contains("notion-editor", StringComparison.Ordinal) == true
                    ? new(Import<TValue>(ct)) : ValueTask.FromResult((TValue)(object)Sidebar);
        private async Task<TValue> Import<TValue>(CancellationToken ct)
        {
            Interlocked.Increment(ref Imports);
            ImportStarted.TrySetResult(true);
            if (ImportGate is not null) await ImportGate.Task.WaitAsync(ct);
            return (TValue)(object)Editor;
        }
    }

    private sealed class NoopSidebarModule : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EditorModule(LazyApi api) : IJSObjectReference
    {
        public ConcurrentQueue<EditorSnapshot> Mounts { get; } = new();
        public ConcurrentQueue<EditorSnapshot> ContextUpdates { get; } = new();
        public int Destroys, Disposals;
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args)
            => InvokeAsync<TValue>(id, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args)
        {
            if (id is "mount" or "mountReadOnly" or "setContext")
            {
                var json = JsonSerializer.SerializeToElement(args![1]);
                var snapshot = new EditorSnapshot(json.GetProperty("pageId").GetGuid(), id == "mountReadOnly",
                    json.GetProperty("members").GetArrayLength(), json.GetProperty("polls").GetArrayLength(), json.GetProperty("groups").GetArrayLength(),
                    json.GetProperty("pages").GetArrayLength());
                if (id == "setContext") ContextUpdates.Enqueue(snapshot);
                else { Mounts.Enqueue(snapshot); api.Events.Enqueue($"mount:{snapshot.PageId}"); }
            }
            if (id == "destroy") Interlocked.Increment(ref Destroys);
            if (id == "hasUnsavedChanges") return ValueTask.FromResult((TValue)(object)false);
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() { Interlocked.Increment(ref Disposals); return ValueTask.CompletedTask; }
    }

    private sealed record Request(string Path, string Query, CancellationToken CancellationToken);
    private sealed class LazyApi : HttpMessageHandler
    {
        public Guid FirstId { get; } = Guid.NewGuid();
        public Guid SecondId { get; } = Guid.NewGuid();
        private Guid HoldId { get; } = Guid.NewGuid();
        public TaskCompletionSource<bool>? MetadataGate { get; init; }
        public TaskCompletionSource<bool>? FirstContextGate { get; init; }
        public TaskCompletionSource<bool>? ResourceGate { get; init; }
        public TaskCompletionSource<bool> FirstContextReturned { get; } = Gate();
        public bool FailMetadata { get; init; }
        public bool FailResources { get; set; }
        public int AdditionalPages { get; init; }
        public volatile bool FirstContextCancelled;
        public ConcurrentQueue<Request> Requests { get; } = new();
        public ConcurrentQueue<string> Events { get; } = new();
        public string ContextPath(Guid id, bool published = false) => $"/api/notion/pages/{id}/{(published ? "published-context" : "context")}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Enqueue(new(path, request.RequestUri.Query, ct));
            Events.Enqueue($"{request.Method} {path}");
            if (path == "/api/emojis") return Json(Array.Empty<CustomEmojiItem>());
            if (path == "/api/references/backlinks") return Json(Array.Empty<ResolvedReference>());
            if (path == ContextPath(FirstId) || path == ContextPath(FirstId, true)
                || path == ContextPath(SecondId) || path == ContextPath(SecondId, true))
            {
                var id = path.Contains(FirstId.ToString(), StringComparison.Ordinal) ? FirstId : SecondId;
                if (id == FirstId && FirstContextGate is not null)
                {
                    using var registration = ct.Register(() => FirstContextCancelled = true);
                    // Simulate a transport that completes despite cancellation.
                    await FirstContextGate.Task;
                    FirstContextReturned.TrySetResult(true);
                }
                return Json(new NotionPageContext(id, HoldId, id == FirstId ? "Første side" : "Anden side", true,
                    [], [], DateTimeOffset.UtcNow, PublishedAtUtc: DateTimeOffset.UtcNow,
                    PublishedStateBase64: path.EndsWith("/published-context", StringComparison.Ordinal) ? "AAA=" : null));
            }
            if (path == "/api/notion/holds" || path == "/api/notion/pages")
            {
                if (MetadataGate is not null) await MetadataGate.Task.WaitAsync(ct);
                if (FailMetadata) return new(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "unavailable" }) };
                if (path == "/api/notion/holds") return Json(new[] { new NotionHold(HoldId, "Testhold", true) });
                return Json(new[]
                {
                    new NotionPageSummary(FirstId, HoldId, "Første side", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                    new NotionPageSummary(SecondId, HoldId, "Anden side", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                }.Concat(Enumerable.Range(0, AdditionalPages).Select(index =>
                    new NotionPageSummary(Guid.NewGuid(), HoldId, $"Side {index:D4}", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow))).ToArray());
            }
            if (path == $"/api/notion/holds/{HoldId}/editor-resources")
            {
                if (ResourceGate is not null) await ResourceGate.Task.WaitAsync(ct);
                if (FailResources) return new(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "unavailable" }) };
                return Json(new { members = new[] { new NotionMember(Guid.NewGuid(), "Testelev") },
                    polls = new[] { new NotionPollReference(Guid.NewGuid(), "Afstemning", "open") } });
            }
            if (path == $"/api/notion/holds/{HoldId}/groups")
            {
                if (ResourceGate is not null) await ResourceGate.Task.WaitAsync(ct);
                return Json(new[] { new NotionGroupView(Guid.NewGuid(), HoldId, "Gruppe 1", 1, []) });
            }
            return new(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}
