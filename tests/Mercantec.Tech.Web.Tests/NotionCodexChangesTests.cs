using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Notion;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class NotionCodexChangesTests
{
    [Fact]
    public async Task History_shows_readable_before_and_after_without_editing_until_undo()
    {
        using var api = new HistoryApi();
        await using var context = Context(api);
        var component = context.Render<NotionCodexChanges>(p => p.Add(x => x.PageId, api.PageId));
        component.WaitForAssertion(() => Assert.Contains("Den forbedrede forklaring", component.Find(".codex-before-after").TextContent));
        Assert.Contains("Den oprindelige forklaring", component.Find(".codex-before-after").TextContent);
        Assert.DoesNotContain("blockContainer", component.Find(".codex-before-after").TextContent);
        Assert.Empty(api.Reversals);

        await UndoButton(component).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        component.WaitForAssertion(() => Assert.Contains("Ændringen er fortrudt", component.Find("[role=status]").TextContent));
        Assert.Single(api.Reversals);
        Assert.DoesNotContain("Fortryd ændring", component.Markup);
        Assert.Contains("Fortrudt", component.Find(".codex-history-detail").TextContent);
        Assert.All(api.Requests, path => Assert.StartsWith($"/api/notion/pages/{api.PageId}/codex-changes", path));
    }

    [Fact]
    public async Task Conflict_preserves_history_and_retry_reuses_the_same_reversal_id()
    {
        using var api = new HistoryApi { ConflictOnce = true };
        await using var context = Context(api);
        var component = context.Render<NotionCodexChanges>(p => p.Add(x => x.PageId, api.PageId));
        component.WaitForAssertion(() => Assert.NotEmpty(component.Find(".codex-before-after").TextContent));

        await UndoButton(component).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        component.WaitForAssertion(() => Assert.Contains("Teksten er ændret", component.Find("[role=alert]").TextContent));
        Assert.Empty(component.FindAll(".codex-history-notice"));
        Assert.Contains("Den forbedrede forklaring", component.Find(".codex-before-after").TextContent);

        await UndoButton(component).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        component.WaitForAssertion(() => Assert.Single(component.FindAll(".codex-history-notice")));
        Assert.Equal(2, api.Reversals.Count);
        Assert.Equal(api.Reversals[0], api.Reversals[1]);
    }

    [Fact]
    public async Task Lost_access_does_not_reveal_details_or_offer_undo()
    {
        using var api = new HistoryApi { Denied = true };
        await using var context = Context(api);
        var component = context.Render<NotionCodexChanges>(p => p.Add(x => x.PageId, api.PageId));
        component.WaitForAssertion(() => Assert.Contains("ikke længere adgang", component.Find("[role=alert]").TextContent));
        Assert.Empty(component.FindAll(".codex-before-after"));
        Assert.Empty(api.Reversals);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task Empty_history_and_close_do_not_send_mutations()
    {
        using var api = new HistoryApi { Empty = true };
        await using var context = Context(api);
        var closed = 0;
        var component = context.Render<NotionCodexChanges>(p => p.Add(x => x.PageId, api.PageId).Add(x => x.OnClose, () => closed++));
        component.WaitForAssertion(() => Assert.Contains("Ingen Codex-ændringer endnu", component.Markup));
        await component.Find("[aria-label='Luk Codex-ændringer']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.Equal(1, closed);
        Assert.Empty(api.Reversals);
        Assert.Single(api.Requests);
    }

    private static AngleSharp.Dom.IElement UndoButton(IRenderedComponent<NotionCodexChanges> component)
        => component.FindAll("button").Single(button => button.TextContent.Trim() == "Fortryd ændring");

    private static BunitContext Context(HistoryApi api)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("/js/notion-codex-changes.js?v=1").Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(new HttpClient(api) { BaseAddress = new Uri("https://lup.example/api/") });
        context.Services.AddScoped<NotionMcpClient>();
        return context;
    }

    private sealed class HistoryApi : HttpMessageHandler
    {
        public Guid PageId { get; } = Guid.NewGuid();
        private Guid OperationId { get; } = Guid.NewGuid();
        private bool _undone;
        public bool ConflictOnce { get; set; }
        public bool Denied { get; init; }
        public bool Empty { get; init; }
        public List<Guid> Reversals { get; } = [];
        public List<string> Requests { get; } = [];
        private string State => _undone ? "undone" : "applied";
        private NotionMcpChangeSummary Summary => new(OperationId, PageId, "Gør forklaringen tydeligere", State,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, _undone ? DateTimeOffset.UtcNow : null, null, !_undone);
        private NotionMcpChangeView View => new(OperationId, PageId, Summary.Summary, State, Summary.CreatedAtUtc,
            Summary.AppliedAtUtc, Summary.UndoneAtUtc, null, !_undone, Body("Den oprindelige forklaring"), Body("Den forbedrede forklaring"));
        private static JsonElement Body(string text) => JsonSerializer.SerializeToElement(new { blocks = new[] { new { block = new { kind = "element", name = "blockContainer", children = new[] { new { kind = "text", delta = new[] { new { insert = text } } } } } } } });
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.AbsolutePath);
            if (Denied) return new(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { error = "not_found" }) };
            if (request.Method == HttpMethod.Post)
            {
                var body = await request.Content!.ReadFromJsonAsync<UndoNotionMcpChange>(ct);
                Reversals.Add(body!.ReversalId);
                if (ConflictOnce) { ConflictOnce = false; return new(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { error = "target_changed" }) }; }
                _undone = true;
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(View) };
            }
            return new(HttpStatusCode.OK) { Content = request.RequestUri.AbsolutePath.EndsWith("codex-changes")
                ? JsonContent.Create(Empty ? Array.Empty<NotionMcpChangeSummary>() : new[] { Summary }) : JsonContent.Create(View) };
        }
    }
}
