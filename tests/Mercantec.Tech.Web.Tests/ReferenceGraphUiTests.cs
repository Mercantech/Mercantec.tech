using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class ReferenceGraphUiTests
{
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
        Assert.Equal(400, api.GraphRequests[0].Limit);
        page.WaitForAssertion(() => Assert.NotNull(javascript!.Module.Payload));
        var nodes = javascript!.Module.Payload!.Value.GetProperty("nodes").EnumerateArray().ToArray();
        Assert.Contains(nodes, node => node.GetProperty("id").GetString() == api.Note.Reference.Key);
        Assert.Contains(page.FindAll(".reference-list a"), link => link.TextContent == api.Note.Title && link.GetAttribute("href") == api.Note.Href);

        page.Find(".reference-toolbar input[type=search]").Input("Anden note");
        page.WaitForAssertion(() => Assert.Single(api.SearchQueries), TimeSpan.FromSeconds(5));
        Assert.Contains("publishedOnly=true", api.SearchQueries[0], StringComparison.OrdinalIgnoreCase);
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".reference-results button")));
        page.Find(".reference-results button").Click();
        page.WaitForAssertion(() => Assert.Equal(2, api.GraphRequests.Count));
        Assert.Equal(api.Note.Reference, api.GraphRequests[1].Focus);
        Assert.True(api.GraphRequests[1].PublishedOnly);
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
        Assert.Equal("hold", api.GraphRequests[1].ScopeKind);
        Assert.Equal(api.Collection.ToString(), api.GraphRequests[1].ScopeId);
    }

    private sealed class GraphApi : HttpMessageHandler
    {
        public Guid Collection { get; } = Guid.NewGuid();
        public bool More;
        public ResolvedReference Note { get; } = new(new("notebook-page", Guid.NewGuid().ToString()), "Anden note", "/notebook/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee", "En delt notesbog");
        public List<ReferenceGraphRequest> GraphRequests { get; } = [];
        public List<string> SearchQueries { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/references/graph")
            {
                var input = (await request.Content!.ReadFromJsonAsync<ReferenceGraphRequest>(cancellationToken))!; GraphRequests.Add(input);
                var source = new ResolvedReference(new("notion-page", Collection.ToString()), "Fælles teori", "/notion/" + Collection, "Teori", CollectionId: Collection);
                return Json(new EntityGraph([new(source.Reference.Key, source), new(Note.Reference.Key, Note)], [new(source.Reference.Key, Note.Reference.Key)], More ? 800 : 2, More && input.Offset == 0));
            }
            if (path == "/api/references/search") { SearchQueries.Add(request.RequestUri.Query); return Json(new ReferenceSearchResult([Note], 1)); }
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
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier is "mount" or "update") Payload = JsonSerializer.SerializeToElement(args![identifier == "mount" ? 2 : 1]);
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
