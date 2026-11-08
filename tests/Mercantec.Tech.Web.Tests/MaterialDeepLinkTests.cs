using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Pages.Courses;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class MaterialDeepLinkTests
{
    [Fact]
    public async Task Material_deep_link_opens_only_visible_material_and_keeps_closed_viewer_closed()
    {
        using var api = new MaterialHandler();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var cut = context.Render<HoldMaterials>(p => p.Add(x => x.HoldId, api.Hold)
            .Add(x => x.RequestedMaterialId, api.Visible.Id));
        cut.WaitForAssertion(() => Assert.Contains("SQL.pdf", cut.Find(".materials-viewer").TextContent));
        Assert.Equal("SQL-introduktion", cut.Find(".material-row.is-selected h3").TextContent);
        Assert.Equal([api.Visible.FileAssetId], api.OpenedFiles);
        cut.Find(".materials-viewer button").Click();
        cut.Render(p => p.Add(x => x.HoldId, api.Hold).Add(x => x.RequestedMaterialId, api.Visible.Id));
        Assert.Empty(cut.FindAll(".materials-viewer"));
        cut.Render(p => p.Add(x => x.HoldId, api.Hold).Add(x => x.RequestedMaterialId, api.Hidden.Id));
        Assert.Empty(cut.FindAll(".materials-viewer"));
        Assert.DoesNotContain("Hemmeligt materiale", cut.Markup);
        Assert.Equal([api.Visible.FileAssetId], api.OpenedFiles);
        Assert.Contains("Materialet findes ikke, eller din konto har ikke adgang", cut.Markup);
    }

    [Fact]
    public async Task Hold_redirect_preserves_material_and_group_targets()
    {
        using var api = new MaterialHandler();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var nav = context.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo($"/holds/{api.Hold}?view=materials&material={api.Visible.Id}&group={api.Group}");
        var cut = context.Render<HoldRedirect>(p => p.Add(x => x.HoldId, api.Hold));
        cut.WaitForAssertion(() => Assert.EndsWith($"/courses/{api.Workspace}?view=materials&material={api.Visible.Id}&group={api.Group}", nav.Uri));
    }

    private sealed class MaterialHandler : HttpMessageHandler
    {
        public Guid Hold { get; } = Guid.NewGuid();
        public Guid Workspace { get; } = Guid.NewGuid();
        public Guid Group { get; } = Guid.NewGuid();
        public CourseMaterialView Visible { get; }
        public CourseMaterialView Hidden { get; }
        public List<Guid> OpenedFiles { get; } = [];
        public MaterialHandler()
        {
            Visible = new(Guid.NewGuid(), Hold, Guid.NewGuid(), "SQL-introduktion", null, true, DateTimeOffset.UtcNow);
            Hidden = new(Guid.NewGuid(), Hold, Guid.NewGuid(), "Hemmeligt materiale", null, false, DateTimeOffset.UtcNow);
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            object body;
            if (path == "/api/quests/holds") body = new[] { new LearningHold(Hold, "H1", false) };
            else if (path == "/api/materials") body = new[] { Visible, Hidden };
            else if (path == $"/api/files/{Visible.FileAssetId}/preview")
            {
                OpenedFiles.Add(Visible.FileAssetId);
                body = new FilePreview(Visible.FileAssetId, "SQL.pdf", "application/pdf", 100, "pdf", "/api/files/pdf", "/api/files/pdf", null, DateTimeOffset.UtcNow.AddMinutes(5));
            }
            else if (path == $"/api/hold-workspace/holds/{Hold}") body = new HoldWorkspaceView(Workspace, Hold, "H1", null, false, 1, 0, 1, 0, 0, 0, [], [], [], []);
            else throw new InvalidOperationException("Unexpected request " + request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType()) });
        }
    }
}
