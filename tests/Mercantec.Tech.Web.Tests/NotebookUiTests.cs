using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Notebooks;
using Mercantec.Tech.Web.Notion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NotebookPage = Mercantec.Tech.Web.Pages.Notebook.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class NotebookUiTests
{
    [Fact]
    public async Task Typing_enables_create_and_find_buttons_without_blurring_the_input()
    {
        using var api = new NotebookApi();
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(s => s.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            services.AddSingleton<IJSRuntime>(new NotebookJavascript(original));
            services.AddSingleton<NotebookClient>(); services.AddSingleton<NotionClient>();
        });
        var page = context.Render<NotebookPage>(p => p.Add(x => x.PageId, api.Page.Id));
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".nb-title-input")));

        Button(page, "Ny note").Click();
        Assert.True(Button(page, "Opret").HasAttribute("disabled"));
        page.Find(".nb-create-form input").Input("Mit nye emne");
        Assert.False(Button(page, "Opret").HasAttribute("disabled"));
        page.Find(".nb-create-form input").Input("");
        Assert.True(Button(page, "Opret").HasAttribute("disabled"));

        Button(page, "Del").Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".nb-search-user input")));
        Assert.True(Button(page, "Find").HasAttribute("disabled"));
        page.Find(".nb-search-user input").Input("E");
        Assert.True(Button(page, "Find").HasAttribute("disabled"));
        page.Find(".nb-search-user input").Input("Emil");
        Assert.False(Button(page, "Find").HasAttribute("disabled"));
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task Comments_preserve_typed_metadata_and_the_mounted_editor()
    {
        using var api = new NotebookApi();
        NotebookJavascript? javascript = null;
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(s => s.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript = new(original);
            services.AddSingleton<IJSRuntime>(javascript);
            services.AddSingleton<NotebookClient>(); services.AddSingleton<NotionClient>();
        });
        var page = context.Render<NotebookPage>(p => p.Add(x => x.PageId, api.Page.Id));
        page.WaitForAssertion(() => Assert.Equal(1, javascript!.Editor.Mounts));

        page.Find(".nb-title-input").Input("Min titel under redigering");
        page.Find(".nb-sidebar-toggle").Click();
        Assert.Contains("is-open", page.Find("#nb-sidebar").ClassName);
        page.Find(".nb-sidebar-toggle").Click();
        Button(page, "Kommentarer").Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".nb-comments textarea")));
        page.Find(".nb-comments textarea").Input("Kan vi drøfte denne løsning?");
        Button(page, "Tilføj kommentar").Click();

        page.WaitForAssertion(() => Assert.Contains("Kan vi drøfte denne løsning?", page.Find(".nb-comments").TextContent));
        Assert.Equal("Min titel under redigering", page.Find(".nb-title-input").GetAttribute("value"));
        Assert.Equal(1, javascript!.Editor.Mounts);
        Assert.Equal(0, javascript.Editor.Destroys);
        Assert.Empty(api.Unexpected);
    }

    [Fact]
    public async Task Shared_reader_has_no_comment_or_share_controls_and_revocation_clears_document()
    {
        using var api = new NotebookApi { Reader = true };
        NotebookJavascript? javascript = null;
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(s => s.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript = new(original);
            services.AddSingleton<IJSRuntime>(javascript);
            services.AddSingleton<NotebookClient>(); services.AddSingleton<NotionClient>();
        });
        var page = context.Render<NotebookPage>(p => p.Add(x => x.PageId, api.Page.Id));
        page.WaitForAssertion(() => Assert.Equal(1, javascript!.Editor.ReadOnlyMounts));
        Assert.Empty(page.FindAll(".nb-title-input"));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() is "Kommentarer" or "Del");
        Assert.DoesNotContain(api.Requests, p => p.EndsWith("/comments", StringComparison.Ordinal));

        await page.InvokeAsync(page.Instance.OnNotebookFocus);
        Assert.Equal(1, javascript!.Editor.SetContexts);
        Assert.Equal(1, javascript.Editor.ReadOnlyMounts);
        Assert.Equal(0, javascript.Editor.AppliedStates);

        api.Revoked = true;
        await page.InvokeAsync(page.Instance.OnNotebookFocus);

        page.WaitForAssertion(() => Assert.Contains("Adgangen til siden er trukket tilbage", page.Markup));
        Assert.Empty(page.FindAll(".nb-reader-title"));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Vis flere sider");
        Assert.Equal(1, javascript!.Editor.Destroys);
        Assert.Empty(api.Unexpected);
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<NotebookPage> page, string label)
        => page.FindAll("button").Single(b => b.TextContent.Trim() == label);

    private sealed class NotebookJavascript(IJSRuntime original) : IJSRuntime
    {
        public EditorModule Editor { get; } = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args) => InvokeAsync<TValue>(id, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args)
        {
            if (id == "import") return ValueTask.FromResult((TValue)(object)Editor);
            return original.InvokeAsync<TValue>(id, ct, args);
        }
    }
    private sealed class EditorModule : IJSObjectReference
    {
        public int Mounts, ReadOnlyMounts, Destroys, SetContexts, AppliedStates;
        public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? args) => InvokeAsync<TValue>(id, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? args)
        {
            if (id == "mount") Mounts++;
            if (id == "mountReadOnly") ReadOnlyMounts++;
            if (id == "destroy") Destroys++;
            if (id == "setContext") SetContexts++;
            if (id == "applyReadOnlyState") AppliedStates++;
            if (id == "hasUnsavedChanges") return ValueTask.FromResult((TValue)(object)false);
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class NotebookApi : HttpMessageHandler
    {
        private readonly Guid _notebook = Guid.NewGuid(), _sharedNotebook = Guid.NewGuid();
        public bool Reader, Revoked;
        public NotebookPageSummary Page { get; }
        public List<string> Requests { get; } = [];
        public List<string> Unexpected { get; } = [];
        private readonly List<NotebookCommentView> _comments = [];
        public NotebookApi() => Page = new(Guid.NewGuid(), _notebook, "Databaseovervejelser", null, "note", ["sql"], null, null, null, DateTimeOffset.UtcNow, 1);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);
            var notebook = Reader ? _sharedNotebook : _notebook;
            var page = Page with { NotebookId = notebook };
            object? body = path switch
            {
                "/api/notebooks/profile" => null,
                "/api/notebooks/mine" => new NotebookInfo(_notebook, LearningUiTestContext.AccountId, "Testelev", true, true),
                "/api/notebooks/shared" => Reader ? new[] { page } : Array.Empty<NotebookPageSummary>(),
                "/api/notebooks/templates" => Array.Empty<NotebookTemplateView>(),
                "/api/notebooks/today" => new DateOnly(2026, 11, 7),
                "/api/notion/holds" => Array.Empty<NotionHold>(),
                var p when p == $"/api/notebooks/{notebook}/pages" => new NotebookPageList([page], 1),
                var p when p == $"/api/notebooks/{notebook}/shares" => Array.Empty<NotebookShareView>(),
                var p when p == $"/api/notebooks/pages/{Page.Id}/context" => new NotebookDocumentContext(Page.Id, notebook, Page.Title, "note", true, !Reader, !Reader, !Reader, request.RequestUri.Query.Contains("knownRevision=", StringComparison.Ordinal) ? null : "AA==", DateTimeOffset.UtcNow, 1, null, [new(Page.Id, Page.Title, null)], ["sql"]),
                var p when p == $"/api/notebooks/pages/{Page.Id}/backlinks" => Array.Empty<NotebookPageSummary>(),
                _ => null
            };
            if (path.EndsWith("/context", StringComparison.Ordinal) && Revoked) return new(HttpStatusCode.NotFound);
            if (path == $"/api/notebooks/pages/{Page.Id}/comments")
            {
                if (request.Method == HttpMethod.Post)
                {
                    var input = await request.Content!.ReadFromJsonAsync<AddNotebookComment>(ct);
                    var comment = new NotebookCommentView(Guid.NewGuid(), LearningUiTestContext.AccountId, "Testelev", input!.Text, DateTimeOffset.UtcNow);
                    _comments.Add(comment); body = comment;
                }
                else body = _comments.ToArray();
            }
            if (path == "/api/notebooks/profile") return new(HttpStatusCode.NoContent);
            if (body is null) { Unexpected.Add(path); return new(HttpStatusCode.NotFound); }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
        }
    }
}
