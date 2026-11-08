using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Emojis;
using Mercantec.Tech.Web.Notion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NotionPage = Mercantec.Tech.Web.Pages.Notion.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class NotionSidebarTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Hiding_and_reopening_the_menu_keeps_the_document_and_cached_navigation()
    {
        using var api = new SidebarApi();
        var javascript = new SidebarJavascript();
        using var context = await CreateContext(api, javascript);
        var page = Render(context, api, javascript);
        var requests = api.Requests.Count;

        page.Find(".notion-sidebar-toggle").Click();
        Assert.Equal("false", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        Assert.Equal("Vis sidemenu", page.Find(".notion-sidebar-toggle").GetAttribute("aria-label"));
        Assert.DoesNotContain("sidebar-open", page.Find(".notion-workspace").ClassList);
        Assert.Single(page.FindAll("#notion-sidebar"));
        Assert.Equal("Relationer og nøgler", page.Find("#notion-title").GetAttribute("value"));

        page.Find(".notion-sidebar-toggle").Click();
        Assert.Equal("true", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        Assert.Equal("Skjul sidemenu", page.Find(".notion-sidebar-toggle").GetAttribute("aria-label"));
        Assert.Contains("sidebar-open", page.Find(".notion-workspace").ClassList);
        Assert.Equal(requests, api.Requests.Count);
        Assert.Equal(1, javascript.Editor.Mounts);
        Assert.Equal(0, javascript.Editor.Destroys);
    }

    [Fact]
    public async Task Search_finds_children_in_closed_folders_without_fetching_or_remounting()
    {
        using var api = new SidebarApi();
        var javascript = new SidebarJavascript();
        using var context = await CreateContext(api, javascript);
        var page = Render(context, api, javascript);
        var requests = api.Requests.Count;
        Assert.DoesNotContain(PageLinks(page), link => link.TextContent.Contains("CSS layout", StringComparison.Ordinal));

        page.Find("#notion-page-search").Input("cSs");
        var result = Assert.Single(PageLinks(page));
        Assert.Contains("CSS layout", result.TextContent);
        Assert.Equal("Relationer og nøgler", page.Find("#notion-title").GetAttribute("value"));
        Assert.Equal(requests, api.Requests.Count);
        Assert.Equal(1, javascript.Editor.Mounts);
        Assert.Equal(0, javascript.Editor.Destroys);

        page.Find("[aria-label='Ryd sidesøgning']").Click();
        Assert.Equal("", page.Find("#notion-page-search").GetAttribute("value"));
        Assert.DoesNotContain(PageLinks(page), link => link.TextContent.Contains("CSS layout", StringComparison.Ordinal));
        Assert.Contains(PageLinks(page), link => link.TextContent.Contains("Relationer og nøgler", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Empty_search_results_keep_the_current_document_and_allow_clearing()
    {
        using var api = new SidebarApi();
        var javascript = new SidebarJavascript();
        using var context = await CreateContext(api, javascript);
        var page = Render(context, api, javascript);

        page.Find("#notion-page-search").Input("en side som ikke eksisterer");
        Assert.Empty(PageLinks(page));
        Assert.Single(page.FindAll("[aria-label='Ryd sidesøgning']"));
        Assert.Equal("Relationer og nøgler", page.Find("#notion-title").GetAttribute("value"));
        Assert.Single(page.FindAll(".notion-editor-root"));
        Assert.Equal(1, javascript.Editor.Mounts);
    }

    [Fact]
    public async Task Active_ancestors_open_automatically_and_other_folders_can_be_expanded_independently()
    {
        using var api = new SidebarApi();
        var javascript = new SidebarJavascript();
        using var context = await CreateContext(api, javascript);
        var page = Render(context, api, javascript);
        var requests = api.Requests.Count;

        Assert.Equal("true", page.Find("[aria-label='Fold Databaser sammen']").GetAttribute("aria-expanded"));
        Assert.Equal("false", page.Find("[aria-label='Fold Webudvikling ud']").GetAttribute("aria-expanded"));
        Assert.Contains(PageLinks(page), link => link.TextContent.Contains("Relationer og nøgler", StringComparison.Ordinal));

        page.Find("[aria-label='Fold Webudvikling ud']").Click();
        Assert.Contains(PageLinks(page), link => link.TextContent.Contains("CSS layout", StringComparison.Ordinal));
        page.Find("[aria-label='Fold Webudvikling sammen']").Click();
        Assert.DoesNotContain(PageLinks(page), link => link.TextContent.Contains("CSS layout", StringComparison.Ordinal));
        Assert.Equal(requests, api.Requests.Count);
        Assert.Equal(1, javascript.Editor.Mounts);
        Assert.Empty(page.FindAll("button button"));
    }

    [Fact]
    public async Task Escape_clears_a_search_before_hiding_the_menu()
    {
        using var api = new SidebarApi();
        var javascript = new SidebarJavascript();
        using var context = await CreateContext(api, javascript);
        var page = Render(context, api, javascript);
        page.Find("#notion-page-search").Input("Relationer");

        await page.Find("#notion-page-search").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal("", page.Find("#notion-page-search").GetAttribute("value"));
        Assert.Equal("true", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));

        await page.Find("#notion-page-search").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal("false", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        Assert.Equal(1, javascript.Editor.Mounts);
        Assert.Equal(0, javascript.Editor.Destroys);
    }

    [Fact]
    public async Task Mobile_drawer_opens_and_closes_without_changing_the_document_or_desktop_preference()
    {
        using var api = new SidebarApi();
        var javascript = new SidebarJavascript();
        using var context = await CreateContext(api, javascript);
        var page = Render(context, api, javascript);
        var requests = api.Requests.Count;

        await page.InvokeAsync(() => page.Instance.OnNotionSidebarViewport(true, true));
        Assert.Equal("false", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        page.Find(".notion-sidebar-toggle").Click();
        Assert.Equal("true", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        await page.InvokeAsync(() => page.Instance.CloseNotionSidebar());
        Assert.Equal("false", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));

        await page.InvokeAsync(() => page.Instance.OnNotionSidebarViewport(false, true));
        Assert.Equal("true", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        Assert.Equal(requests, api.Requests.Count);
        Assert.Equal(1, javascript.Editor.Mounts);
        Assert.Equal(0, javascript.Editor.Destroys);
    }

    [Fact]
    public async Task Published_readers_can_search_but_cannot_create_pages()
    {
        using var api = new SidebarApi { Teacher = false };
        var javascript = new SidebarJavascript();
        using var context = await CreateContext(api, javascript);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/notion/{api.ActiveId}?published=true");
        var page = Render(context, api, javascript);

        Assert.Empty(page.FindAll("[aria-label='Opret dokument']"));
        page.Find("#notion-page-search").Input("CSS");
        Assert.Contains("CSS layout", Assert.Single(PageLinks(page)).TextContent);
        Assert.Empty(page.FindAll("#notion-title"));
        Assert.Single(page.FindAll(".notion-editor-root"));
        Assert.Equal(1, javascript.Editor.Mounts);
    }

    [Fact]
    public async Task Slow_document_navigation_preserves_the_sidebar_root_and_its_binding()
    {
        using var api = new SidebarApi();
        var javascript = new SidebarJavascript();
        using var context = await CreateContext(api, javascript);
        var page = Render(context, api, javascript);
        page.WaitForAssertion(() => Assert.Single(javascript.Sidebar.MountedRoots), Timeout);
        var mountedRoot = javascript.Sidebar.MountedRoots.Single();
        api.DelayedPageId = api.CssId;
        api.ContextGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            page.Render(p => p.Add(x => x.PageId, api.CssId));
            page.WaitForAssertion(() => Assert.Contains($"/api/notion/pages/{api.CssId}/context", api.Requests), Timeout);
            Assert.Single(page.FindAll(".notion-workspace"));
            Assert.Single(javascript.Sidebar.MountedRoots);
            Assert.Empty(javascript.Sidebar.DestroyedRoots);

            api.ContextGate.TrySetResult(true);
            page.WaitForAssertion(() => Assert.Equal(2, javascript.Editor.Mounts), Timeout);
            Assert.Equal("CSS layout", page.Find("#notion-title").GetAttribute("value"));
            Assert.Equal(mountedRoot, Assert.Single(javascript.Sidebar.MountedRoots));
            Assert.Empty(javascript.Sidebar.DestroyedRoots);

            await page.InvokeAsync(() => page.Instance.OnNotionSidebarViewport(true, true));
            page.Find(".notion-sidebar-toggle").Click();
            await page.InvokeAsync(() => page.Instance.CloseNotionSidebar());
            Assert.Equal("false", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        }
        finally { api.ContextGate.TrySetResult(true); }
    }

    [Fact]
    public async Task Late_preferences_preserve_a_user_choice_while_still_applying_the_mobile_viewport()
    {
        using var api = new SidebarApi();
        var javascript = new SidebarJavascript();
        javascript.Sidebar.MountGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        javascript.Sidebar.MountPreferences = new { mobile = true, desktopOpen = true };
        using var context = await CreateContext(api, javascript);
        try
        {
            var page = Render(context, api, javascript);
            await javascript.Sidebar.MountStarted.Task.WaitAsync(Timeout);
            Assert.Equal(1, javascript.Editor.Mounts);
            Assert.Equal("Relationer og nøgler", page.Find("#notion-title").GetAttribute("value"));

            // The user can close and reopen before browser preferences arrive.
            page.Find(".notion-sidebar-toggle").Click();
            page.Find(".notion-sidebar-toggle").Click();
            Assert.Equal("true", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));

            javascript.Sidebar.MountGate.TrySetResult(true);
            page.WaitForAssertion(() => Assert.Equal("dialog", page.Find("#notion-sidebar").GetAttribute("role")), Timeout);
            Assert.Equal("true", page.Find("#notion-sidebar").GetAttribute("aria-modal"));
            Assert.Equal("true", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
            Assert.Equal(1, javascript.Editor.Mounts);
            Assert.Equal(0, javascript.Editor.Destroys);

            await page.InvokeAsync(() => page.Instance.CloseNotionSidebar());
            Assert.Equal("false", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
            Assert.Null(page.Find("#notion-sidebar").GetAttribute("aria-modal"));
        }
        finally { javascript.Sidebar.MountGate.TrySetResult(true); }
    }

    [Fact]
    public async Task Creating_a_child_on_mobile_opens_the_form_and_releases_the_drawer_before_navigation()
    {
        using var api = new SidebarApi();
        var javascript = new SidebarJavascript();
        using var context = await CreateContext(api, javascript);
        var page = Render(context, api, javascript);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        await page.InvokeAsync(() => page.Instance.OnNotionSidebarViewport(true, true));
        Assert.Equal("false", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));

        page.FindAll(".notion-document-menu button").Single(button => button.TextContent.Trim() == "Ny underside").Click();
        Assert.Equal("true", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        Assert.Equal("dialog", page.Find("#notion-sidebar").GetAttribute("role"));
        Assert.Equal("true", page.Find("#notion-sidebar").GetAttribute("aria-modal"));
        page.Find(".notion-create input").Change("Ny underside om databaser");
        await page.Find(".notion-create").SubmitAsync(EventArgs.Empty);

        var created = Assert.Single(api.CreateRequests);
        Assert.Equal(api.ActiveId, created.ParentPageId);
        Assert.Equal("Ny underside om databaser", created.Title);
        Assert.Contains($"/notion/{api.CreatedId}", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal("false", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        Assert.Null(page.Find("#notion-sidebar").GetAttribute("aria-modal"));
        Assert.Empty(page.FindAll("#notion-sidebar-backdrop"));

        // Apply the destination parameters as the router does after navigation.
        page.Render(p => p.Add(x => x.PageId, api.CreatedId));
        page.WaitForAssertion(() => Assert.Equal(api.CreatedId, javascript.Editor.MountedPages.Last()), Timeout);
        Assert.Equal("Ny underside om databaser", page.Find("#notion-title").GetAttribute("value"));
        Assert.Equal("false", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        Assert.Null(page.Find("#notion-sidebar").GetAttribute("aria-modal"));
        Assert.Empty(page.FindAll("#notion-sidebar-backdrop"));
    }

    [Fact]
    public async Task Global_notebooks_use_collection_navigation_and_keep_search_and_hiding_without_hold_tools()
    {
        using var api = new SidebarApi { Global = true };
        var javascript = new SidebarJavascript();
        using var context = await CreateContext(api, javascript);
        var page = Render(context, api, javascript);
        Assert.Empty(page.FindAll(".notion-hold-picker"));
        Assert.DoesNotContain(page.FindAll("a"), link => link.TextContent.Contains("Elever og grupper", StringComparison.Ordinal)
            || link.TextContent.Contains("Graf for holdet", StringComparison.Ordinal));
        Assert.Contains("Notesbogsværktøjer", page.Find(".notion-sidebar-tools summary").TextContent);
        var query = Assert.Single(api.PageQueries);
        Assert.Contains($"collectionId={api.CollectionId}", query, StringComparison.Ordinal);
        Assert.DoesNotContain("holdId=", query, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Requests, path => path.StartsWith("/api/notion/holds", StringComparison.Ordinal));
        var requests = api.Requests.Count;

        page.Find("#notion-page-search").Input("CSS");
        Assert.Contains("CSS layout", Assert.Single(PageLinks(page)).TextContent);
        page.Find(".notion-sidebar-toggle").Click();
        Assert.Equal("false", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        page.Find(".notion-sidebar-toggle").Click();
        Assert.Equal("true", page.Find(".notion-sidebar-toggle").GetAttribute("aria-expanded"));
        Assert.Contains("CSS layout", Assert.Single(PageLinks(page)).TextContent);
        Assert.Equal("Relationer og nøgler", page.Find("#notion-title").GetAttribute("value"));
        Assert.Equal(requests, api.Requests.Count);
        Assert.Equal(1, javascript.Editor.Mounts);
        Assert.Equal(0, javascript.Editor.Destroys);
    }

    private static IRenderedComponent<NotionPage> Render(LearningUiTestContext context, SidebarApi api, SidebarJavascript javascript)
    {
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.ActiveId));
        page.WaitForAssertion(() => Assert.Equal(1, javascript.Editor.Mounts), Timeout);
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".notion-sidebar-status")), Timeout);
        page.WaitForAssertion(() => Assert.Contains(PageLinks(page), link => link.TextContent.Contains("Databaser", StringComparison.Ordinal)), Timeout);
        return page;
    }

    private static AngleSharp.Dom.IElement[] PageLinks(IRenderedComponent<NotionPage> page)
        => page.FindAll(".notion-page-list .notion-page").ToArray();

    private static Task<LearningUiTestContext> CreateContext(SidebarApi api, SidebarJavascript javascript)
        => LearningUiTestContext.CreateAsync(api, teacher: api.Teacher, configure: services =>
        {
            var original = (IJSRuntime)services.Last(s => s.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript.Original = original;
            services.AddSingleton<IJSRuntime>(javascript);
            services.AddSingleton<NotionClient>();
        });

    private sealed class SidebarJavascript : IJSRuntime
    {
        public IJSRuntime Original { get; set; } = default!;
        public EditorModule Editor { get; } = new();
        public SidebarModule Sidebar { get; } = new();
        public ConcurrentQueue<string> Calls { get; } = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args)
            => InvokeAsync<TValue>(id, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args)
        {
            Calls.Enqueue(id);
            if (id == "import")
                return ValueTask.FromResult((TValue)(object)(args?.FirstOrDefault()?.ToString()?.Contains("notion-editor", StringComparison.Ordinal) == true
                    ? (IJSObjectReference)Editor : Sidebar));
            return Original.InvokeAsync<TValue>(id, ct, args);
        }
    }

    private sealed class SidebarModule : IJSObjectReference
    {
        public ConcurrentQueue<string> Calls { get; } = new();
        public ConcurrentQueue<string> MountedRoots { get; } = new();
        public ConcurrentQueue<string> DestroyedRoots { get; } = new();
        public TaskCompletionSource<bool>? MountGate { get; set; }
        public TaskCompletionSource<bool> MountStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object? MountPreferences { get; set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args)
            => InvokeAsync<TValue>(id, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args)
        {
            Calls.Enqueue(id);
            if (args?.FirstOrDefault() is ElementReference root)
            {
                if (id == "mount") MountedRoots.Enqueue(root.Id);
                if (id == "destroy") DestroyedRoots.Enqueue(root.Id);
            }
            if (id == "mount")
            {
                MountStarted.TrySetResult(true);
                return new(CompleteMount<TValue>(ct));
            }
            return ValueTask.FromResult(default(TValue)!);
        }
        private async Task<TValue> CompleteMount<TValue>(CancellationToken ct)
        {
            if (MountGate is not null) await MountGate.Task.WaitAsync(ct);
            return MountPreferences is null ? default!
                : JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(MountPreferences), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EditorModule : IJSObjectReference
    {
        public int Mounts, Destroys, ContextUpdates;
        public ConcurrentQueue<Guid> MountedPages { get; } = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args)
            => InvokeAsync<TValue>(id, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args)
        {
            if (id is "mount" or "mountReadOnly")
            {
                Interlocked.Increment(ref Mounts);
                MountedPages.Enqueue(JsonSerializer.SerializeToElement(args![1]).GetProperty("pageId").GetGuid());
            }
            if (id == "destroy") Interlocked.Increment(ref Destroys);
            if (id == "setContext") Interlocked.Increment(ref ContextUpdates);
            if (id == "hasUnsavedChanges") return ValueTask.FromResult((TValue)(object)false);
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SidebarApi : HttpMessageHandler
    {
        public Guid HoldId { get; } = Guid.NewGuid();
        public Guid CollectionId { get; } = Guid.NewGuid();
        public Guid DatabaseId { get; } = Guid.NewGuid();
        public Guid ActiveId { get; } = Guid.NewGuid();
        public Guid NormalizationId { get; } = Guid.NewGuid();
        public Guid WebId { get; } = Guid.NewGuid();
        public Guid CssId { get; } = Guid.NewGuid();
        public Guid IntroductionId { get; } = Guid.NewGuid();
        public Guid CreatedId { get; } = Guid.NewGuid();
        private NotionPageSummary? _createdPage;
        public bool Teacher { get; init; } = true;
        public bool Global { get; init; }
        public Guid? DelayedPageId { get; set; }
        public TaskCompletionSource<bool>? ContextGate { get; set; }
        public ConcurrentQueue<string> Requests { get; } = new();
        public ConcurrentQueue<string> PageQueries { get; } = new();
        public ConcurrentQueue<CreateNotionPage> CreateRequests { get; } = new();
        public NotionPageSummary[] Pages =>
        [
            Summary(DatabaseId, "Databaser"),
            Summary(ActiveId, "Relationer og nøgler", DatabaseId),
            Summary(NormalizationId, "Normalisering", DatabaseId),
            Summary(WebId, "Webudvikling"),
            Summary(CssId, "CSS layout", WebId),
            Summary(IntroductionId, "Introduktion"),
            ..(_createdPage is { } created ? new[] { created } : [])
        ];

        private NotionPageSummary Summary(Guid id, string title, Guid? parent = null)
            => new(id, Global ? null : HoldId, title, parent, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                CollectionId: Global ? CollectionId : null);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Enqueue(path);
            if (request.Method == HttpMethod.Post && path == "/api/notion/pages") return Create(request, ct);
            if (path == "/api/emojis") return Json(Array.Empty<CustomEmojiItem>());
            if (path == "/api/references/backlinks") return Json(Array.Empty<ResolvedReference>());
            if (path == "/api/notion/holds") return Json(new[] { new NotionHold(HoldId, "Programmering H1", Teacher) });
            if (path == "/api/notion/pages") { PageQueries.Enqueue(request.RequestUri.Query); return Json(Pages); }
            if (path == $"/api/notion/holds/{HoldId}/groups") return Json(Array.Empty<NotionGroupView>());
            if (path == $"/api/notion/holds/{HoldId}/editor-resources") return Json(new NotionEditorResources([], []));
            var page = Pages.FirstOrDefault(p => path == $"/api/notion/pages/{p.Id}/context"
                || path == $"/api/notion/pages/{p.Id}/published-context");
            if (page is not null)
            {
                var published = path.EndsWith("/published-context", StringComparison.Ordinal);
                var body = new NotionPageContext(page.Id, Global ? null : HoldId, page.Title, Teacher && !published, [], [], page.UpdatedAtUtc,
                    PublishedAtUtc: page.PublishedAtUtc, PublishedStateBase64: !Teacher || published ? "AAA=" : null,
                    CollectionId: Global ? CollectionId : null);
                return page.Id == DelayedPageId && ContextGate is not null ? DelayedJson(body, ContextGate.Task, ct) : Json(body);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private async Task<HttpResponseMessage> Create(HttpRequestMessage request, CancellationToken ct)
        {
            var input = await request.Content!.ReadFromJsonAsync<CreateNotionPage>(ct)
                ?? throw new InvalidOperationException("A page request requires a title and parent.");
            CreateRequests.Enqueue(input);
            _createdPage = Summary(CreatedId, input.Title, input.ParentPageId) with { PublishedAtUtc = null };
            return await Json(_createdPage);
        }

        private static Task<HttpResponseMessage> Json(object body)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });

        private static async Task<HttpResponseMessage> DelayedJson(object body, Task gate, CancellationToken ct)
        { await gate.WaitAsync(ct); return await Json(body); }
    }
}
