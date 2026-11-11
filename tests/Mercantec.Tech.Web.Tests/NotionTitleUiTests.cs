using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Emojis;
using Mercantec.Tech.Web.Notion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NotionPage = Mercantec.Tech.Web.Pages.Notion.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class NotionTitleUiTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Picking_a_title_emoji_saves_its_shortcode_and_renders_it_in_toolbar_and_sidebar()
    {
        var raymond = new CustomEmojiItem(Guid.NewGuid(), "raymond_applause", "image/webp", 12,
            DateTimeOffset.UtcNow, "/images/characters/raymond/reactions/applause.webp",
            AltText: "Raymond klapper", IsReadOnly: true);
        using var api = new TitleApi { Title = "SQL 🐘", Emojis = [raymond] };
        using var context = await CreateContext(api);
        var javascript = (TitleJavascript)context.Services.GetRequiredService<IJSRuntime>();
        var head = context.Render<HeadOutlet>();
        const string brandIcon = "images/branding/pixel-lup.svg";
        context.Render<HeadContent>(p => p.AddChildContent($"""<link rel="icon" type="image/svg+xml" href="{brandIcon}" data-lup-brand-icon="{brandIcon}">"""));
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.PageId));
        page.WaitForAssertion(() => Assert.Equal(1, javascript.Editor.Mounts), Timeout);
        Assert.Equal("SQL 🐘 · LUP", head.Find("title").TextContent);
        Assert.Equal(0, api.EmojiRequests);

        await Button(page, "Tilføj emoji til sidens titel").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=option][aria-label=':raymond_applause:']")), Timeout);
        await page.Find("[role=option][aria-label=':raymond_applause:']").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal("SQL 🐘 :raymond_applause:", api.Title), Timeout);
        Assert.Equal(raymond.ContentUrl, page.Find(".notion-title-preview img").GetAttribute("src"));
        Assert.Equal(raymond.ContentUrl, page.Find(".notion-page.is-current img").GetAttribute("src"));
        Assert.Contains("SQL 🐘", page.Find(".notion-title-preview").TextContent);
        Assert.DoesNotContain(":raymond_applause:", page.Find(".notion-page.is-current").TextContent);
        Assert.Equal("SQL 🐘 :raymond_applause:", page.Find("#notion-title").GetAttribute("value"));
        head.WaitForAssertion(() =>
        {
            Assert.Equal("SQL 🐘 · LUP", head.Find("title").TextContent);
            var icon = head.Find("link[rel=icon]");
            Assert.Equal(raymond.ContentUrl, icon.GetAttribute("href"));
            Assert.False(icon.HasAttribute("data-lup-brand-icon"));
        }, Timeout);

        page.Find("#notion-title").Input("SQL 🐘");
        await page.Find("#notion-title").BlurAsync(new FocusEventArgs());
        var restoredIcon = head.Find("link[rel=icon]");
        Assert.Equal(brandIcon, restoredIcon.GetAttribute("href"));
        Assert.Equal(brandIcon, restoredIcon.GetAttribute("data-lup-brand-icon"));
        Assert.Equal(1, api.EmojiRequests);
        Assert.Equal(1, javascript.Editor.Mounts);
        Assert.Equal(0, javascript.Editor.Destroys);
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task An_uploaded_emoji_only_title_uses_the_image_as_tab_icon_and_preserves_alt_text_on_the_page()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAACklEQVR4nGNgAAIAAAUAAXpeqz8AAAAASUVORK5CYII=");
        var id = Guid.NewGuid();
        var upload = new CustomEmojiItem(id, "egen_emoji", "image/png", png.Length,
            DateTimeOffset.UtcNow, $"/api/emojis/{id}/content?ticket=private", AltText: "Vores emoji");
        using var api = new TitleApi { Title = ":egen_emoji:", Emojis = [upload], EmojiPng = png };
        using var context = await CreateContext(api);
        var head = context.Render<HeadOutlet>();
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.PageId));

        head.WaitForAssertion(() =>
        {
            Assert.Equal("LUP", head.Find("title").TextContent);
            Assert.Equal("data:image/png;base64," + Convert.ToBase64String(png),
                head.Find("link[rel=icon]").GetAttribute("href"));
        }, Timeout);
        page.WaitForAssertion(() => Assert.Equal(upload.AltText,
            page.Find(".notion-title-preview img").GetAttribute("alt")), Timeout);
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task Toolbar_groups_title_publication_focus_and_the_persistent_AI_host_without_remounting()
    {
        using var api = new TitleApi();
        using var context = await CreateContext(api);
        var javascript = (TitleJavascript)context.Services.GetRequiredService<IJSRuntime>();
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.PageId));
        page.WaitForAssertion(() => Assert.Equal(1, javascript.Editor.Mounts), Timeout);
        var toolbar = page.Find(".notion-document-toolbar");

        Assert.NotNull(toolbar.QuerySelector(".notion-title-form #notion-title"));
        Assert.NotNull(toolbar.QuerySelector(".notion-toolbar-nav [aria-label='Fokus']"));
        Assert.NotNull(toolbar.QuerySelector(".notion-toolbar-actions .notion-ai-toggle-slot"));
        Assert.NotNull(toolbar.QuerySelector(".notion-document-menu [aria-label='Brødkrummer']"));
        Assert.Contains("Præsenter", toolbar.QuerySelector(".notion-document-menu")!.TextContent);
        Assert.Contains("Codex-ændringer", toolbar.QuerySelector(".notion-document-menu")!.TextContent);
        Assert.Equal("Starttitel", toolbar.QuerySelector("h1.notion-visually-hidden")!.TextContent);
        Assert.Empty(page.FindAll(".notion-publishing, .notion-document > .notion-title-form, .notion-document > .notion-title-preview"));
        Assert.Equal("Udgiv", Button(page, "Udgiv til holdet").TextContent.Trim());
        Assert.True(Button(page, "Udgiv til holdet").HasAttribute("disabled"));

        await page.InvokeAsync(() => page.Instance.OnNotionStatus("saved", ""));
        Assert.False(Button(page, "Udgiv til holdet").HasAttribute("disabled"));
        page.Find("#notion-title").Input("  SQL :vatson:  ");
        Assert.Single(page.FindAll(".notion-document-toolbar .notion-title-preview"));
        await Button(page, "Udgiv til holdet").ClickAsync(new MouseEventArgs());

        Assert.Equal("SQL :vatson:", Assert.Single(api.PublishedTitles));
        Assert.Equal("SQL :vatson:", page.Find("#notion-title").GetAttribute("value"));
        Assert.Equal("Opdatér", Button(page, "Opdatér udgivelsen").TextContent.Trim());
        Assert.Contains("Se udgivet", page.Find(".notion-document-menu").TextContent);
        Assert.Contains("Træk udgivelse tilbage", page.Find(".notion-document-menu").TextContent);
        Assert.Single(page.FindAll(".notion-document-toolbar .notion-ai-toggle-slot"));
        Assert.Equal(1, javascript.Editor.Mounts);
        Assert.Equal(0, javascript.Editor.Destroys);
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task Read_only_toolbar_shows_the_title_without_authoring_controls()
    {
        using var api = new TitleApi { CanEdit = false, PublishedAt = DateTimeOffset.UtcNow };
        using var context = await CreateContext(api);
        var javascript = (TitleJavascript)context.Services.GetRequiredService<IJSRuntime>();
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.PageId));
        page.WaitForAssertion(() => Assert.Equal(1, javascript.Editor.Mounts), Timeout);

        Assert.Equal("Starttitel", page.Find(".notion-document-toolbar .notion-toolbar-title").TextContent);
        Assert.Empty(page.FindAll(".notion-title-form, .notion-publish-button"));
        Assert.DoesNotContain("Codex-ændringer", page.Find(".notion-document-menu").TextContent);
        Assert.Single(page.FindAll(".notion-document-toolbar .notion-ai-toggle-slot"));
        Assert.DoesNotContain("Træk udgivelse tilbage", page.Find(".notion-document-menu").TextContent);
        Assert.Equal(0, javascript.Editor.Destroys);
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task Blur_and_form_submission_save_the_title_without_remounting_the_document()
    {
        using var api = new TitleApi();
        using var context = await CreateContext(api);
        var javascript = (TitleJavascript)context.Services.GetRequiredService<IJSRuntime>();
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.PageId));
        page.WaitForAssertion(() => Assert.Equal(1, javascript.Editor.Mounts), Timeout);
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".notion-sidebar-status")), Timeout);
        var initialContextUpdates = javascript.Editor.SetContexts;

        page.Find("#notion-title").Input("  Databaser med Vatson  ");
        Assert.Empty(api.Renames);
        await page.Find("#notion-title").BlurAsync(new FocusEventArgs());

        page.WaitForAssertion(() => Assert.Equal("Databaser med Vatson", api.Title), Timeout);
        Assert.Equal("Databaser med Vatson", page.Find("#notion-title").GetAttribute("value"));
        Assert.Contains("Databaser med Vatson", page.Find(".notion-page.is-current").TextContent);

        page.Find("#notion-title").Input("Relationer og nøgler");
        await page.Find(".notion-title-form").SubmitAsync(EventArgs.Empty);

        Assert.Equal(["Databaser med Vatson", "Relationer og nøgler"], api.Renames.ToArray());
        Assert.Equal("Relationer og nøgler", page.Find("#notion-title").GetAttribute("value"));
        Assert.Equal(1, javascript.Editor.Mounts);
        Assert.Equal(0, javascript.Editor.Destroys);
        Assert.Equal(initialContextUpdates + 2, javascript.Editor.SetContexts);
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task A_pending_save_coalesces_overlapping_events_and_preserves_the_latest_typed_title()
    {
        using var api = new TitleApi
        {
            FirstRenameGate = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        using var context = await CreateContext(api);
        var javascript = (TitleJavascript)context.Services.GetRequiredService<IJSRuntime>();
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.PageId));
        page.WaitForAssertion(() => Assert.Equal(1, javascript.Editor.Mounts), Timeout);

        page.Find("#notion-title").Input("Første ændring");
        var blur = page.Find("#notion-title").BlurAsync(new FocusEventArgs());
        try
        {
            page.WaitForAssertion(() => Assert.Equal("Første ændring", Assert.Single(api.Renames)), Timeout);
            Assert.False(page.Find("#notion-title").HasAttribute("disabled"));
            page.Find("#notion-title").Input("Den seneste ændring");
            var submit = page.Find(".notion-title-form").SubmitAsync(EventArgs.Empty);
            Assert.Single(api.Renames);

            api.FirstRenameGate!.SetResult(true);
            await Task.WhenAll(blur, submit).WaitAsync(Timeout);

            Assert.Equal(["Første ændring", "Den seneste ændring"], api.Renames.ToArray());
            Assert.Equal("Den seneste ændring", api.Title);
            Assert.Equal("Den seneste ændring", page.Find("#notion-title").GetAttribute("value"));
            Assert.Contains("Den seneste ændring", page.Find(".notion-page.is-current").TextContent);
            Assert.Equal(1, javascript.Editor.Mounts);
            Assert.Equal(0, javascript.Editor.Destroys);
            Assert.Empty(api.Unexpected);
        }
        finally
        {
            api.FirstRenameGate!.TrySetResult(true);
        }
    }

    [Fact]
    public async Task A_failed_title_save_preserves_input_and_blocks_navigation_and_publication_until_retry()
    {
        using var api = new TitleApi { FailRenames = true };
        using var context = await CreateContext(api);
        var javascript = (TitleJavascript)context.Services.GetRequiredService<IJSRuntime>();
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        var page = context.Render<NotionPage>(p => p.Add(x => x.PageId, api.PageId));
        page.WaitForAssertion(() => Assert.Equal(1, javascript.Editor.Mounts), Timeout);
        await page.InvokeAsync(() => page.Instance.OnNotionStatus("saved", ""));

        page.Find("#notion-title").Input("Titel som skal bevares");
        await page.Find("#notion-title").BlurAsync(new FocusEventArgs());
        Assert.Equal("Titel som skal bevares", page.Find("#notion-title").GetAttribute("value"));
        Assert.Contains("Dokumenttjenesten", page.Find(".notion-error").TextContent);

        await page.InvokeAsync(() => navigation.NavigateTo("/courses"));
        page.WaitForAssertion(() => Assert.Equal(NavigationState.Prevented, navigation.History.First().State), Timeout);
        await Button(page, "Udgiv til holdet").TriggerEventAsync("onclick", new MouseEventArgs());
        Assert.Empty(api.PublishedTitles);
        Assert.Equal(0, javascript.Editor.PublicationReads);
        Assert.Equal("Titel som skal bevares", page.Find("#notion-title").GetAttribute("value"));
        Assert.Equal("Starttitel", api.Title);
        Assert.Equal(0, javascript.Editor.Destroys);

        api.FailRenames = false;
        await page.Find(".notion-title-form").SubmitAsync(EventArgs.Empty);
        Assert.Empty(page.FindAll(".notion-error"));
        Assert.Equal("Titel som skal bevares", api.Title);
        await Button(page, "Udgiv til holdet").TriggerEventAsync("onclick", new MouseEventArgs());
        Assert.Equal("Titel som skal bevares", Assert.Single(api.PublishedTitles));
        Assert.Equal(1, javascript.Editor.PublicationReads);
        await page.InvokeAsync(() => navigation.NavigateTo("/courses"));
        page.WaitForAssertion(() => Assert.Equal(NavigationState.Succeeded, navigation.History.First().State), Timeout);
        Assert.Equal(1, javascript.Editor.Mounts);
        Assert.Equal(0, javascript.Editor.Destroys);
        Assert.Empty(api.Unexpected);
    }

    private static Task<LearningUiTestContext> CreateContext(TitleApi api)
        => LearningUiTestContext.CreateAsync(api, teacher: true, configure: services =>
        {
            var original = (IJSRuntime)services.Last(s => s.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            services.AddSingleton<IJSRuntime>(new TitleJavascript(original));
            services.AddSingleton<NotionClient>();
        });

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<NotionPage> page, string label)
        => page.FindAll("button").Single(button => (button.GetAttribute("aria-label") ?? button.TextContent.Trim()) == label);

    private sealed class TitleJavascript(IJSRuntime original) : IJSRuntime
    {
        public EditorModule Editor { get; } = new();
        private NoopSidebarModule Sidebar { get; } = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args)
            => InvokeAsync<TValue>(id, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args)
            => id == "import" ? ValueTask.FromResult((TValue)(object)(args?.FirstOrDefault()?.ToString()?.Contains("notion-editor", StringComparison.Ordinal) == true
                ? (IJSObjectReference)Editor : Sidebar)) : original.InvokeAsync<TValue>(id, ct, args);
    }

    private sealed class NoopSidebarModule : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EditorModule : IJSObjectReference
    {
        public int Mounts, Destroys, SetContexts, PublicationReads;
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args)
            => InvokeAsync<TValue>(id, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args)
        {
            if (id is "mount" or "mountReadOnly") Mounts++;
            if (id == "destroy") Destroys++;
            if (id == "setContext") SetContexts++;
            if (id == "hasUnsavedChanges") return ValueTask.FromResult((TValue)(object)false);
            if (id == "getPublicationData")
            {
                PublicationReads++;
                var json = JsonSerializer.Serialize(new { expectedFingerprint = new string('a', 64), assetIds = Array.Empty<Guid>() });
                return ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
            }
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TitleApi : HttpMessageHandler
    {
        public Guid PageId { get; } = Guid.NewGuid();
        private Guid HoldId { get; } = Guid.NewGuid();
        public string Title { get; set; } = "Starttitel";
        public CustomEmojiItem[] Emojis { get; init; } = [];
        public byte[]? EmojiPng { get; init; }
        public int EmojiRequests { get; private set; }
        public bool CanEdit { get; init; } = true;
        public DateTimeOffset? PublishedAt { get; set; }
        public bool FailRenames { get; set; }
        public TaskCompletionSource<bool>? FirstRenameGate { get; init; }
        public ConcurrentQueue<string> Renames { get; } = new();
        public ConcurrentQueue<string> PublishedTitles { get; } = new();
        public ConcurrentQueue<string> Unexpected { get; } = new();
        private int _renameCount;
        private NotionPageSummary Summary => new(PageId, HoldId, Title, null, DateTimeOffset.UtcNow, PublishedAt);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Patch && path == $"/api/notion/pages/{PageId}")
            {
                var input = await request.Content!.ReadFromJsonAsync<RenameNotionPage>(ct)
                    ?? throw new InvalidOperationException("A rename request must contain a title.");
                Renames.Enqueue(input.Title);
                if (Interlocked.Increment(ref _renameCount) == 1 && FirstRenameGate is not null)
                    await FirstRenameGate.Task.WaitAsync(ct);
                if (FailRenames) return new(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "unavailable" }) };
                Title = input.Title;
                return Json(Summary);
            }
            if (request.Method == HttpMethod.Post && path == $"/api/notion/pages/{PageId}/publish")
            {
                PublishedTitles.Enqueue(Title);
                PublishedAt = DateTimeOffset.UtcNow;
                return Json(Summary);
            }
            if (request.Method == HttpMethod.Get)
            {
                if (path == "/api/emojis") { EmojiRequests++; return Json(Emojis); }
                if (EmojiPng is not null && request.RequestUri.Query.Length == 0
                    && Emojis.Any(emoji => path == $"/api/emojis/{emoji.Id}/content"))
                {
                    var content = new ByteArrayContent(EmojiPng);
                    content.Headers.ContentType = new("image/png");
                    return new(HttpStatusCode.OK) { Content = content };
                }
                object? body = path switch
                {
                    "/api/notion/holds" => new[] { new NotionHold(HoldId, "Testhold", true) },
                    "/api/references/backlinks" => Array.Empty<ResolvedReference>(),
                    "/api/notion/pages" => new[] { Summary },
                    var p when p == $"/api/notion/holds/{HoldId}/groups" => Array.Empty<NotionGroupView>(),
                    var p when p == $"/api/notion/pages/{PageId}/context" => new NotionPageContext(PageId, HoldId, Title, CanEdit, [], [], DateTimeOffset.UtcNow,
                        [new(PageId, Title, null)], PublishedAt, DraftRevision: 3),
                    _ => null
                };
                if (body is not null) return Json(body);
            }
            Unexpected.Enqueue($"{request.Method} {path}");
            return new(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}
