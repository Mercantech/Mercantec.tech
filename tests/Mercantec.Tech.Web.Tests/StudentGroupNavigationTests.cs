using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Learning;
using Mercantec.Tech.Web.Notion;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class StudentGroupNavigationTests
{
    [Fact]
    public async Task StudentFindsOwnGroupsAcrossActiveSetsWithoutTeacherControls()
    {
        var holdId = Guid.NewGuid();
        var anna = new NotionMember(Guid.NewGuid(), "Anna");
        var bo = new NotionMember(Guid.NewGuid(), "Bo");
        var setA = new GroupSetView(Guid.NewGuid(), holdId, "Projektgrupper", false, 0, 1, 2);
        var setB = new GroupSetView(Guid.NewGuid(), holdId, "Makkerpar", false, 0, 1, 2);
        var archived = new GroupSetView(Guid.NewGuid(), holdId, "Sidste projekt", true, 0, 1, 2);
        NotionGroupView[] groups = [
            new(Guid.NewGuid(), holdId, "Spilholdet", 0, [anna, bo], setA.Id),
            new(Guid.NewGuid(), holdId, "Kodeparret", 0, [anna, bo], setB.Id),
            new(Guid.NewGuid(), holdId, "Tidligere gruppe", 0, [anna, bo], archived.Id)
        ];
        var handler = new GroupHandler(holdId, [setA, setB, archived], groups,
            [new(anna.Id, anna.DisplayName, groups.Select(g => g.Id).ToArray()),
             new(bo.Id, bo.DisplayName, groups.Select(g => g.Id).ToArray())]);
        using var context = await LearningUiTestContext.CreateAsync(handler, configure: services =>
        {
            services.AddSingleton<NotionClient>();
            services.AddSingleton<GroupSetsClient>();
        });

        var cut = context.Render<HoldGroups>(parameters => parameters.Add(p => p.HoldId, holdId));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".hold-group-card").Count));
        Assert.Equal("all", cut.Find("select").GetAttribute("value"));
        Assert.Equal(["Spilholdet", "Kodeparret"], cut.FindAll(".hold-group-card h4").Select(e => e.TextContent).ToArray());
        Assert.Contains("Projektgrupper", cut.FindAll(".hold-group-set-label").Select(e => e.TextContent));
        Assert.Contains("Makkerpar", cut.FindAll(".hold-group-set-label").Select(e => e.TextContent));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("Opret", StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("form"));
        Assert.Empty(cut.FindAll("a[href$='/roulette']"));
        Assert.Contains($"/holds/{holdId}?view=quests", cut.FindAll("a").Select(a => a.GetAttribute("href")));

        cut.Find("select").Change(setA.Id.ToString());
        Assert.Equal("Spilholdet", Assert.Single(cut.FindAll(".hold-group-card h4")).TextContent);
        cut.Find("select").Change("all");
        cut.Find("input[type='checkbox']").Change(true);
        Assert.Equal(3, cut.FindAll(".hold-group-card").Count);
        Assert.Contains("Sidste projekt · Arkiveret", cut.FindAll(".hold-group-set-label").Select(e => e.TextContent));
        Assert.All(handler.Methods, method => Assert.Equal(HttpMethod.Get, method));
    }

    private sealed class GroupHandler(Guid holdId, GroupSetView[] sets, NotionGroupView[] groups, NotionHoldMember[] members) : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            var path = request.RequestUri!.AbsolutePath;
            object body = path switch
            {
                "/api/notion/holds" => new[] { new NotionHold(holdId, "Spiludvikling", false) },
                var value when value == $"/api/notion/holds/{holdId}/members" => members,
                var value when value == $"/api/notion/holds/{holdId}/groups" => groups,
                var value when value == $"/api/holds/{holdId}/group-sets" => sets,
                _ => throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType()) });
        }
    }
}
