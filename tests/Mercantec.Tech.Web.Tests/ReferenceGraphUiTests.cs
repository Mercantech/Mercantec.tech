using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Emojis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class ReferenceGraphUiTests
{
    [Fact]
    public async Task Graph_titles_render_known_emoji_and_preserve_unknown_shortcodes()
    {
        const string title = ":chameleon_coffee: Machine Learning :ukendt:";
        var emoji = new CustomEmojiItem(Guid.NewGuid(), "chameleon_coffee", "image/webp", 12,
            DateTimeOffset.UtcNow, "/images/characters/chameleon/reactions/coffee.webp", AltText: "Kamæleonen holder kaffepause");
        using var api = new GraphApi(title) { Emoji = emoji };
        GraphJavascript? javascript = null;
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript = new GraphJavascript(original);
            services.AddSingleton<IJSRuntime>(javascript);
        });

        var page = context.Render<ReferenceGraphView>();

        page.WaitForAssertion(() =>
        {
            Assert.NotNull(javascript!.Module.Payload);
            var node = javascript.Module.Payload.Value.GetProperty("nodes").EnumerateArray()
                .Single(node => node.GetProperty("id").GetString() == api.Note.Reference.Key);
            Assert.Equal(title, node.GetProperty("title").GetString());
            var parts = node.GetProperty("titleParts").EnumerateArray().ToArray();
            Assert.Equal(2, parts.Length);
            Assert.Equal(emoji.ContentUrl, parts[0].GetProperty("imageUrl").GetString());
            Assert.Equal(emoji.AltText, parts[0].GetProperty("alt").GetString());
            Assert.Equal(" Machine Learning :ukendt:", parts[1].GetProperty("text").GetString());

            var link = page.Find($".reference-list a[href='{api.Note.Href}']");
            Assert.Equal(" Machine Learning :ukendt:", link.TextContent);
            var image = link.QuerySelector("img.custom-emoji");
            Assert.NotNull(image);
            Assert.Equal(emoji.ContentUrl, image.GetAttribute("src"));
            Assert.Equal(emoji.AltText, image.GetAttribute("alt"));
        });
        Assert.Equal(1, api.EmojiRequests);
    }

    [Fact]
    public async Task Published_graph_keeps_typed_nodes_canonical_destinations_and_publication_mode_when_searching()
    {
        using var api = new GraphApi();
        GraphJavascript? javascript = null;
        await using var context = await LearningUiTestContext.CreateAsync(api, teacher: true, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript = new GraphJavascript(original); services.AddSingleton<IJSRuntime>(javascript);
        });
        var page = context.Render<ReferenceGraphView>(parameters => parameters.Add(x => x.ScopeKind, "collection").Add(x => x.ScopeId, api.Collection.ToString()).Add(x => x.PublishedOnly, true));
        page.WaitForAssertion(() => Assert.Single(api.GraphRequests));
        Assert.Equal("collection", api.GraphRequests[0].ScopeKind);
        Assert.Equal(api.Collection.ToString(), api.GraphRequests[0].ScopeId);
        Assert.True(api.GraphRequests[0].PublishedOnly);
        Assert.True(api.GraphRequests[0].LinkedPagesOnly);
        Assert.Equal(400, api.GraphRequests[0].Limit);
        page.WaitForAssertion(() => Assert.NotNull(javascript!.Module.Payload));
        var nodes = javascript!.Module.Payload!.Value.GetProperty("nodes").EnumerateArray().ToArray();
        Assert.Contains(nodes, node => node.GetProperty("id").GetString() == api.Note.Reference.Key);
        Assert.Contains(page.FindAll(".reference-list a"), link => link.TextContent == api.Note.Title && link.GetAttribute("href") == api.Note.Href);

        page.Find(".reference-toolbar input[type=search]").Input("Anden note");
        page.WaitForAssertion(() => Assert.Single(api.SearchQueries), TimeSpan.FromSeconds(5));
        Assert.Contains("publishedOnly=true", api.SearchQueries[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("contentOnly=true", api.SearchQueries[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("linkedPagesOnly=true", api.SearchQueries[0], StringComparison.OrdinalIgnoreCase);
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".reference-results button")));
        page.Find(".reference-results button").Click();
        page.WaitForAssertion(() => Assert.Equal(2, api.GraphRequests.Count));
        Assert.Equal(api.Note.Reference, api.GraphRequests[1].Focus);
        Assert.True(api.GraphRequests[1].PublishedOnly);
        Assert.True(api.GraphRequests[1].LinkedPagesOnly);
    }

    [Fact]
    public async Task Other_content_can_be_enabled_and_switching_back_resets_the_focused_graph_and_filters()
    {
        using var api = new GraphApi { IncludeFile = true, More = true };
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            services.AddSingleton<IJSRuntime>(new GraphJavascript(original));
        });
        var page = context.Render<ReferenceGraphView>();
        page.WaitForAssertion(() => Assert.Single(api.GraphRequests));
        Assert.True(api.GraphRequests[0].LinkedPagesOnly);
        Assert.DoesNotContain(api.File.Title, page.Markup);

        page.Find(".reference-content-toggle input").Change(true);
        page.WaitForAssertion(() => Assert.Contains(api.File.Title, page.Markup));
        Assert.False(api.GraphRequests[1].LinkedPagesOnly);
        page.Find(".reference-type select").Change("file");
        Assert.Equal(api.File.Title, page.Find(".reference-list a").TextContent);
        page.Find(".reference-toolbar input[type=search]").Input(api.File.Title);
        page.WaitForAssertion(() => Assert.Contains("linkedPagesOnly=false", Assert.Single(api.SearchQueries)));
        page.WaitForElement(".reference-results button");
        page.FindAll(".reference-results button").Single(button => button.TextContent.Contains(api.File.Title)).Click();
        page.WaitForAssertion(() => Assert.Equal(api.File.Reference, api.GraphRequests[2].Focus));
        page.FindAll("button").Single(button => button.TextContent.Contains("Hent næste udsnit", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => Assert.Equal(400, api.GraphRequests[3].Offset));

        page.Find(".reference-content-toggle input").Change(false);
        page.WaitForAssertion(() => Assert.Equal(5, api.GraphRequests.Count));
        var linked = api.GraphRequests[4];
        Assert.True(linked.LinkedPagesOnly);
        Assert.Null(linked.Focus);
        Assert.Equal(0, linked.Offset);
        Assert.DoesNotContain(api.File.Title, page.Markup);
        Assert.Empty(page.FindAll(".reference-results button"));
        Assert.Contains(api.Note.Title, page.Find(".reference-list").TextContent);
    }

    [Fact]
    public async Task Empty_linked_graph_explains_how_to_show_other_content()
    {
        using var api = new GraphApi { NoLinkedPages = true, IncludeFile = true };
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            services.AddSingleton<IJSRuntime>(new GraphJavascript(original));
        });
        var page = context.Render<ReferenceGraphView>();
        Assert.Equal("Ingen sidelinks at vise", page.Find(".reference-empty h2").TextContent);
        Assert.Contains("Vis filer og andet indhold", page.Find(".reference-empty p").TextContent);
        page.Find(".reference-content-toggle input").Change(true);
        page.WaitForAssertion(() => Assert.Contains(api.File.Title, page.Markup));
        Assert.Empty(page.FindAll(".reference-empty"));
    }

    [Fact]
    public async Task Requesting_more_keeps_each_graph_slice_bounded_and_retains_the_scope()
    {
        using var api = new GraphApi { More = true };
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            services.AddSingleton<IJSRuntime>(new GraphJavascript(original));
        });
        var page = context.Render<ReferenceGraphView>(parameters => parameters.Add(x => x.ScopeKind, "hold").Add(x => x.ScopeId, api.Collection.ToString()));
        page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), button => button.TextContent.Contains("Hent næste udsnit", StringComparison.Ordinal)));
        page.FindAll("button").Single(button => button.TextContent.Contains("Hent næste udsnit", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => Assert.Equal(2, api.GraphRequests.Count));
        Assert.Equal(400, api.GraphRequests[1].Offset);
        Assert.Equal(400, api.GraphRequests[1].Limit);
        Assert.True(api.GraphRequests[1].LinkedPagesOnly);
        Assert.Equal("hold", api.GraphRequests[1].ScopeKind);
        Assert.Equal(api.Collection.ToString(), api.GraphRequests[1].ScopeId);
    }

    [Fact]
    public async Task Graph_search_list_type_filter_and_callbacks_never_expose_people_or_polls_from_an_old_response()
    {
        using var api = new GraphApi { IncludeExcluded = true };
        GraphJavascript? javascript = null;
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript = new GraphJavascript(original); services.AddSingleton<IJSRuntime>(javascript);
        });
        var page = context.Render<ReferenceGraphView>();
        page.WaitForAssertion(() => Assert.NotNull(javascript!.Module.Payload));
        var payload = javascript!.Module.Payload!.Value;
        Assert.Equal(2, payload.GetProperty("nodes").GetArrayLength());
        Assert.Single(payload.GetProperty("edges").EnumerateArray());
        Assert.DoesNotContain("Skjult elev", page.Markup);
        Assert.DoesNotContain("Skjult person", page.Markup);
        Assert.DoesNotContain("Skjult afstemning", page.Markup);
        Assert.DoesNotContain(page.FindAll(".reference-type option"), option => option.GetAttribute("value") is "person" or "member" or "poll");
        var navigation = context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        var before = navigation.Uri;
        await page.InvokeAsync(() => page.Instance.OnGraphOpen(api.Person.Reference.Key));
        await page.InvokeAsync(() => page.Instance.OnGraphMenu(api.Person.Reference.Key, 1, 1));
        Assert.Equal(before, navigation.Uri);
        Assert.Empty(page.FindAll(".reference-selected"));
        page.Find(".reference-toolbar input[type=search]").Input("Skjult");
        page.WaitForAssertion(() => Assert.Single(api.SearchQueries));
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".reference-results button")));
        Assert.DoesNotContain("Skjult elev", page.Markup);
        Assert.DoesNotContain("Skjult person", page.Markup);
        Assert.DoesNotContain("Skjult afstemning", page.Markup);
    }

    [Fact]
    public async Task Display_preferences_restore_and_changes_configure_without_reloading_topology_or_sending_page_data()
    {
        using var culture = new CultureScope("da-DK");
        using var api = new GraphApi();
        GraphJavascript? javascript = null;
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript = new GraphJavascript(original);
            javascript.Module.StoredSettings = """{"nodeSize":2,"labelSize":1.4,"labelWeight":700,"linkThickness":1.8,"showLabels":false,"labelThreshold":1.2,"activeId":"person:private","query":"private search"}""";
            services.AddSingleton<IJSRuntime>(javascript);
        });
        var page = context.Render<ReferenceGraphView>();
        page.WaitForAssertion(() => Assert.NotNull(javascript!.Module.Payload));
        Assert.Equal(2, javascript!.Module.Payload!.Value.GetProperty("options").GetProperty("nodeSize").GetDouble());
        Assert.Equal(1.2, javascript.Module.Payload.Value.GetProperty("options").GetProperty("labelThreshold").GetDouble());
        Assert.Equal("1.4", page.Find("input[aria-label='Tekststørrelse']").GetAttribute("value"));
        Assert.Equal("700", page.Find(".reference-weight select").GetAttribute("value"));
        Assert.True(page.Find(".reference-settings").HasAttribute("hidden"));
        page.Find("button[aria-controls]").Click();
        Assert.False(page.Find(".reference-settings").HasAttribute("hidden"));
        var mounts = javascript.Module.Mounts; var updates = javascript.Module.Updates;
        page.Find("input[aria-label='Tekststørrelse']").Input("1.7");
        page.WaitForAssertion(() => Assert.Equal(1.7, javascript.Module.Options!.Value.GetProperty("labelSize").GetDouble()));
        await javascript.Module.SavedReady.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1.7, javascript.Module.Saved!.Value.GetProperty("labelSize").GetDouble());
        Assert.False(javascript.Module.Saved.Value.TryGetProperty("activeId", out _));
        Assert.False(javascript.Module.Saved.Value.TryGetProperty("query", out _));
        Assert.Equal(mounts, javascript.Module.Mounts); Assert.Equal(updates, javascript.Module.Updates);
        Assert.Single(api.GraphRequests);
        page.Find(".reference-reset").Click();
        page.WaitForAssertion(() => Assert.Equal(1, javascript.Module.Options!.Value.GetProperty("labelSize").GetDouble()));
        Assert.Equal(0.2, javascript.Module.Options!.Value.GetProperty("labelThreshold").GetDouble());
        Assert.Equal("0,2×", page.Find("input[aria-label='Titler fra zoom']").ParentElement!.QuerySelector("output")!.TextContent);
        Assert.Equal(1, javascript.Module.Resets);
        Assert.Single(api.GraphRequests);
    }

    [Fact]
    public async Task Keyboard_list_can_highlight_and_clear_a_node_without_fetching_a_new_graph()
    {
        using var api = new GraphApi();
        GraphJavascript? javascript = null;
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript = new GraphJavascript(original); services.AddSingleton<IJSRuntime>(javascript);
        });
        var page = context.Render<ReferenceGraphView>();
        page.WaitForAssertion(() => Assert.NotNull(javascript!.Module.Payload));
        page.FindAll(".reference-list li").Single(li => li.QuerySelector("a")?.TextContent == api.Note.Title).QuerySelector("button")!.Click();
        page.WaitForAssertion(() => Assert.Equal(api.Note.Title, page.Find(".reference-selected h3").TextContent));
        Assert.Equal(api.Note.Reference.Key, javascript!.Module.Focuses.Last());
        page.WaitForAssertion(() => Assert.Equal(api.Note.Reference.Key, javascript.Module.Options!.Value.GetProperty("activeId").GetString()));
        Assert.Single(api.GraphRequests);
        page.FindAll(".reference-selected button").Single(button => button.TextContent == "Fjern markering").Click();
        Assert.Empty(page.FindAll(".reference-selected"));
        Assert.Equal("", javascript.Module.Focuses.Last());
        Assert.Single(api.GraphRequests);
    }

    [Fact]
    public async Task Every_range_preserves_fractional_values_with_Danish_number_formatting()
    {
        using var culture = new CultureScope("da-DK");
        using var api = new GraphApi();
        GraphJavascript? javascript = null;
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript = new GraphJavascript(original); services.AddSingleton<IJSRuntime>(javascript);
        });
        var page = context.Render<ReferenceGraphView>();
        page.WaitForAssertion(() => Assert.NotNull(javascript!.Module.Payload));
        var ranges = new[] { ("Punktstørrelse", "nodeSize"), ("Tekststørrelse", "labelSize"), ("Forbindelsernes tykkelse", "linkThickness"),
            ("Titler fra zoom", "labelThreshold"), ("Spredning", "repelForce"), ("Samling i midten", "centerForce"),
            ("Forbindelsernes længde", "linkDistance"), ("Forbindelsernes styrke", "linkForce") };
        foreach (var (label, property) in ranges)
        {
            page.Find($"input[aria-label='{label}']").Input("1.5");
            page.WaitForAssertion(() => Assert.Equal(1.5, javascript!.Module.Options!.Value.GetProperty(property).GetDouble()));
        }
        Assert.Single(api.GraphRequests);
    }

    [Fact]
    public async Task Changing_scope_cancels_old_search_and_clears_its_query_and_results()
    {
        using var api = new GraphApi { SearchGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        GraphJavascript? javascript = null;
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript = new GraphJavascript(original); services.AddSingleton<IJSRuntime>(javascript);
        });
        var page = context.Render<ReferenceGraphView>(parameters => parameters.Add(x => x.ScopeKind, "collection").Add(x => x.ScopeId, "old-scope"));
        try
        {
            page.WaitForAssertion(() => Assert.NotNull(javascript!.Module.Payload));
            var pendingSearch = page.Find("input[type=search]").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "old query" });
            await api.SearchStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            page.Render(parameters => parameters.Add(x => x.ScopeKind, "collection").Add(x => x.ScopeId, "new-scope"));
            api.SearchGate.TrySetResult();
            await pendingSearch;
            page.WaitForAssertion(() => Assert.Equal("new-scope", api.GraphRequests.Last().ScopeId));
            Assert.Equal("", page.Find("input[type=search]").GetAttribute("value"));
            Assert.Empty(page.FindAll(".reference-results"));
            Assert.DoesNotContain("Søger i indhold", page.Markup);
        }
        finally { api.SearchGate.TrySetResult(); }
    }

    [Fact]
    public async Task Scope_changes_wait_for_pending_mount_and_destroy_before_mounting_the_latest_host()
    {
        using var api = new GraphApi();
        GraphJavascript? javascript = null;
        await using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var original = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            javascript = new GraphJavascript(original);
            javascript.Module.MountGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            javascript.Module.DestroyGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            services.AddSingleton<IJSRuntime>(javascript);
        });
        var module = javascript!.Module;
        var page = context.Render<ReferenceGraphView>(parameters => parameters.Add(x => x.ScopeKind, "collection").Add(x => x.ScopeId, "first"));
        try
        {
            await module.MountStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            page.Render(parameters => parameters.Add(x => x.ScopeKind, "collection").Add(x => x.ScopeId, "second"));
            Assert.Single(api.GraphRequests);
            module.MountGate!.TrySetResult();
            await module.DestroyStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            page.Render(parameters => parameters.Add(x => x.ScopeKind, "collection").Add(x => x.ScopeId, "latest"));
            Assert.Single(api.GraphRequests);
            module.DestroyGate!.TrySetResult();
            page.WaitForAssertion(() => Assert.Equal("latest", api.GraphRequests.Last().ScopeId));
            page.WaitForAssertion(() => Assert.Equal(2, module.Mounts));
            Assert.Equal(new[] { "first", "latest" }, api.GraphRequests.Select(request => request.ScopeId));
            Assert.Equal(1, module.Destroys);
            Assert.Equal(new[] { "mount", "destroy", "mount" }, module.Lifecycle);
        }
        finally { module.MountGate!.TrySetResult(); module.DestroyGate!.TrySetResult(); }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
        private readonly System.Globalization.CultureInfo previousUi = System.Globalization.CultureInfo.CurrentUICulture;
        public CultureScope(string name) => System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(name);
        public void Dispose() { System.Globalization.CultureInfo.CurrentCulture = previous; System.Globalization.CultureInfo.CurrentUICulture = previousUi; }
    }

    private sealed class GraphApi(string noteTitle = "Anden note") : HttpMessageHandler
    {
        public Guid Collection { get; } = Guid.NewGuid();
        public bool More;
        public bool IncludeExcluded;
        public bool IncludeFile { get; init; }
        public bool NoLinkedPages { get; init; }
        public CustomEmojiItem? Emoji { get; init; }
        public int EmojiRequests { get; private set; }
        public TaskCompletionSource? SearchGate;
        public TaskCompletionSource SearchStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ResolvedReference Note { get; } = new(new("notebook-page", Guid.NewGuid().ToString()), noteTitle, "/notebook/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee", "En delt notesbog");
        public ResolvedReference File { get; } = new(new("file", Guid.NewGuid().ToString()), "Vedhæftet fil", "/files/file", "Materiale");
        public ResolvedReference Person { get; } = new(new("person", Guid.NewGuid().ToString()), "Skjult person", "/people/person", "Elev");
        public ResolvedReference Member { get; } = new(new("member", Guid.NewGuid().ToString()), "Skjult elev", "/people/member", "Elev");
        public ResolvedReference Poll { get; } = new(new("poll", Guid.NewGuid().ToString()), "Skjult afstemning", "/polls/poll", "Afstemning");
        public List<ReferenceGraphRequest> GraphRequests { get; } = [];
        public List<string> SearchQueries { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/emojis")
            {
                EmojiRequests++;
                return Json(Emoji is { } emoji ? new[] { emoji } : Array.Empty<CustomEmojiItem>());
            }
            if (path == "/api/references/graph")
            {
                var input = (await request.Content!.ReadFromJsonAsync<ReferenceGraphRequest>(cancellationToken))!; GraphRequests.Add(input);
                if (input.LinkedPagesOnly && NoLinkedPages) return Json(new EntityGraph([], [], 0, false));
                var source = new ResolvedReference(new("notion-page", Collection.ToString()), "Fælles teori", "/notion/" + Collection, "Teori", CollectionId: Collection);
                ReferenceGraphNode[] nodes = [new(source.Reference.Key, source), new(Note.Reference.Key, Note)];
                ReferenceGraphEdge[] edges = [new(source.Reference.Key, Note.Reference.Key)];
                if (IncludeFile && !input.LinkedPagesOnly)
                {
                    nodes = [.. nodes, new(File.Reference.Key, File)];
                    edges = [.. edges, new(source.Reference.Key, File.Reference.Key)];
                }
                if (IncludeExcluded)
                {
                    nodes = nodes.Concat(new[] { Person, Member, Poll }.Select(reference => new ReferenceGraphNode(reference.Reference.Key, reference))).ToArray();
                    edges = edges.Concat(new[] { Person, Member, Poll }.Select(reference => new ReferenceGraphEdge(source.Reference.Key, reference.Reference.Key))).ToArray();
                }
                return Json(new EntityGraph(nodes, edges, More ? 800 : nodes.Length, More && input.Offset == 0));
            }
            if (path == "/api/references/search")
            {
                SearchQueries.Add(request.RequestUri.Query); SearchStarted.TrySetResult();
                if (SearchGate is not null) await SearchGate.Task;
                List<ResolvedReference> matches = [Note];
                if (IncludeExcluded) matches.AddRange([Person, Member, Poll]);
                if (IncludeFile && request.RequestUri.Query.Contains("linkedPagesOnly=false")) matches.Add(File);
                return Json(new ReferenceSearchResult(matches.ToArray(), matches.Count));
            }
            return new(HttpStatusCode.NotFound);
        }
        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
    private sealed class GraphJavascript(IJSRuntime original) : IJSRuntime
    {
        public GraphModule Module { get; } = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => identifier == "import" ? ValueTask.FromResult((TValue)(object)Module) : original.InvokeAsync<TValue>(identifier, cancellationToken, args);
    }
    private sealed class GraphModule : IJSObjectReference
    {
        public JsonElement? Payload;
        public JsonElement? Options;
        public JsonElement? Saved;
        public TaskCompletionSource<JsonElement> SavedReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? StoredSettings;
        public int Mounts, Updates, Resets, Destroys;
        public TaskCompletionSource? MountGate, DestroyGate;
        public TaskCompletionSource MountStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DestroyStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Lifecycle { get; } = [];
        public List<string> Focuses { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "readSettings") return StoredSettings is null ? default(TValue)! : JsonSerializer.Deserialize<TValue>(StoredSettings, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            if (identifier is "mount" or "update") { Payload = JsonSerializer.SerializeToElement(args![identifier == "mount" ? 2 : 1], new JsonSerializerOptions(JsonSerializerDefaults.Web)); if (identifier == "mount") Mounts++; else Updates++; Options = Payload.Value.GetProperty("options"); }
            if (identifier == "mount") { Lifecycle.Add("mount"); MountStarted.TrySetResult(); if (MountGate is not null) await MountGate.Task; }
            if (identifier == "destroy") { Destroys++; Lifecycle.Add("destroy"); DestroyStarted.TrySetResult(); if (DestroyGate is not null) await DestroyGate.Task; }
            if (identifier == "configure") Options = JsonSerializer.SerializeToElement(args![1]);
            if (identifier == "saveSettings") { Saved = JsonSerializer.SerializeToElement(args![0], new JsonSerializerOptions(JsonSerializerDefaults.Web)); SavedReady.TrySetResult(Saved.Value); }
            if (identifier == "resetSettings") Resets++;
            if (identifier == "focus") Focuses.Add((string)args![1]!);
            return default(TValue)!;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
