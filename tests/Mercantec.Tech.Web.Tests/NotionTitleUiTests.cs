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
        => page.FindAll("button").Single(button => button.TextContent.Trim() == label);

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
            if (id == "mount") Mounts++;
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
        public string Title { get; private set; } = "Starttitel";
        public bool FailRenames { get; set; }
        public TaskCompletionSource<bool>? FirstRenameGate { get; init; }
        public ConcurrentQueue<string> Renames { get; } = new();
        public ConcurrentQueue<string> PublishedTitles { get; } = new();
        public ConcurrentQueue<string> Unexpected { get; } = new();
        private int _renameCount;
        private DateTimeOffset? _publishedAt;
        private NotionPageSummary Summary => new(PageId, HoldId, Title, null, DateTimeOffset.UtcNow, _publishedAt);

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
                _publishedAt = DateTimeOffset.UtcNow;
                return Json(Summary);
            }
            if (request.Method == HttpMethod.Get)
            {
                object? body = path switch
                {
                    "/api/emojis" => Array.Empty<CustomEmojiItem>(),
                    "/api/notion/holds" => new[] { new NotionHold(HoldId, "Testhold", true) },
                    "/api/references/backlinks" => Array.Empty<ResolvedReference>(),
                    "/api/notion/pages" => new[] { Summary },
                    var p when p == $"/api/notion/holds/{HoldId}/groups" => Array.Empty<NotionGroupView>(),
                    var p when p == $"/api/notion/pages/{PageId}/context" => new NotionPageContext(PageId, HoldId, Title, true, [], [], DateTimeOffset.UtcNow,
                        [new(PageId, Title, null)], _publishedAt, DraftRevision: 3),
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
