using Bunit;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Games;
using Mercantec.Tech.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using CatalogPage = Mercantec.Tech.Web.Pages.Games;
using ClassicPage = Mercantec.Tech.Web.Pages.ClassicGame;

namespace Mercantec.Tech.Web.Tests;

public sealed class GamesCatalogTests
{
    [Fact]
    public void PublicCatalogListsPlayableGamesWithoutRuneRunOrStudentUploads()
    {
        using var context = new BunitContext();
        var page = context.Render<CatalogPage>();
        Assert.Equal("Spil", page.Find("h1").TextContent);
        Assert.Equal("/games/arena", page.Find(".arena-feature a").GetAttribute("href"));
        Assert.Equal(
            ["/games/bomberman", "/games/wizard", "/games/tetris", "/games/pong", "/games/tower-defense"],
            page.FindAll(".classic-game a").Select(link => link.GetAttribute("href") ?? "").ToArray());
        Assert.Contains(page.FindAll("a"), link => link.GetAttribute("href") == "/games/guide");
        Assert.Equal("/games/wordle", page.Find("a.games-wordle-play").GetAttribute("href"));
        Assert.DoesNotContain("Rune Run", page.Markup);
        Assert.Empty(page.FindAll("input[type=file]"));
        Assert.All(page.FindAll("a"), link => Assert.True(PublicPageAccess.Allows(link.GetAttribute("href")!)));
        Assert.True(PublicPageAccess.Allows("/games"));
        Assert.False(PublicPageAccess.Allows(GamesCatalog.GameProjectsPath));
    }

    [Theory]
    [InlineData("bomberman", null, "/Bomberman/")]
    [InlineData("wizard", null, "/Wizard/")]
    [InlineData("tetris", null, "/Tetris/")]
    [InlineData("pong", null, "/Pong/")]
    [InlineData("tower-defense", null, "/TowerDefense/")]
    [InlineData("guide", null, "/guide")]
    [InlineData(null, "tetris", "/manual/tetris")]
    public void LegacyPageKeepsTheGameOnItsOwnOrigin(string? id, string? manual, string expected)
    {
        using var context = new BunitContext();
        var page = context.Render<ClassicPage>(parameters => parameters
            .Add(component => component.PageId, id)
            .Add(component => component.ManualId, manual));
        var frame = page.Find("iframe");
        var url = "https://games.mercantec.tech" + expected;
        Assert.Equal(url, frame.GetAttribute("src"));
        Assert.Equal("no-referrer", frame.GetAttribute("referrerpolicy"));
        Assert.DoesNotContain("allow-top-navigation", frame.GetAttribute("sandbox"));
        Assert.Contains(page.FindAll("a"), link => link.GetAttribute("href") == url && link.GetAttribute("target") == "_blank");
    }

    [Fact]
    public void UnknownLegacyRouteCannotFrameAnArbitraryDestination()
    {
        using var context = new BunitContext();
        var page = context.Render<ClassicPage>(parameters => parameters.Add(component => component.PageId, "https://other.example"));
        Assert.Empty(page.FindAll("iframe"));
        Assert.False(PublicPageAccess.Allows("/games/unknown"));
        Assert.False(PublicPageAccess.Allows("/games/manual/private"));
    }

    [Theory]
    [InlineData("/games/avatar-demo", "/games")]
    [InlineData("/games/mine", "/projects/mine?category=spil")]
    [InlineData("/games/upload", "/projects/mine?category=spil#upload-spil")]
    public void OldBookmarksNavigateToTheRelevantNewPage(string oldPath, string newPath)
    {
        using var context = new BunitContext();
        var nav = context.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(oldPath);
        context.Render<LegacyGameRoute>();
        Assert.Equal(new Uri(new Uri(nav.BaseUri), newPath).AbsoluteUri, nav.Uri);
    }
}
