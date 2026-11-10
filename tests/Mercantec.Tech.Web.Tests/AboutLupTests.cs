using Bunit;
using Mercantec.Tech.Web.Branding;
using Mercantec.Tech.Web.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class AboutLupTests
{
    [Fact]
    public void About_can_be_read_without_auth_services_and_explains_both_audiences()
    {
        using var context = BrowserContext();
        var page = context.Render<AboutLup>();

        Assert.Equal("Om LUP", Assert.Single(page.FindAll("h1")).TextContent);
        var pupil = page.Find("#for-elever");
        var teacher = page.Find("#for-undervisere");
        Assert.Contains("Find dagens arbejde", pupil.TextContent);
        Assert.Contains("Aflevér dit arbejde", pupil.TextContent);
        Assert.Contains("Saml dit forløb", teacher.TextContent);
        Assert.Contains("giv feedback", teacher.TextContent);
        Assert.Contains("Kasper og Mathias", page.Markup);
        Assert.Empty(page.FindAll(".about-role-links"));
        Assert.DoesNotContain(page.FindAll("a"), a => a.TextContent is "Jeg er elev" or "Jeg underviser");
    }

    [Fact]
    public void Overview_and_login_links_offer_a_way_forward_without_replacing_existing_routes()
    {
        using var context = BrowserContext();
        var page = context.Render<AboutLup>();

        Assert.Contains("Oversigt", page.Find("a[href='/library']").TextContent);
        Assert.NotEmpty(page.FindAll("a[href='/login']"));
        Assert.Single(page.FindAll("a[href='/library?category=courses']"));
        Assert.Single(page.FindAll("a[href='/characters']"));
        Assert.DoesNotContain(page.FindAll("a"), a => a.GetAttribute("href") is "/overview" or "/source");
    }

    [Fact]
    public void Characters_appear_only_in_the_hero_with_a_link_to_the_full_catalogue()
    {
        using var context = BrowserContext();
        var page = context.Render<AboutLup>();

        Assert.Single(page.FindComponents<AboutCharacterSpotlight>());
        Assert.Single(page.FindAll(".about-character-spotlight"));
        Assert.Empty(page.FindAll(".about-character-browser, .about-characters"));
        Assert.Contains("LUPP", page.Find(".spotlight-portrait img").GetAttribute("alt"));
        Assert.Equal("/characters/lupp", page.Find(".spotlight-profile").GetAttribute("href"));
        Assert.Equal("/characters", page.Find(".spotlight-all").GetAttribute("href"));
    }

    [Fact]
    public void The_name_explanations_share_the_footer_catalogue_and_keep_the_original_attribution()
    {
        using var context = BrowserContext();
        var page = context.Render<AboutLup>();

        var explanations = page.Find(".about-expansions");
        foreach (var expansion in LupIdentity.Expansions)
        {
            Assert.Contains(expansion.Name, explanations.TextContent);
            Assert.Contains(expansion.Description, explanations.TextContent);
        }
        Assert.Contains(LupIdentity.OfficialName, Assert.Single(page.FindAll(".about-expansions .is-official")).TextContent);
        var attribution = page.Find(".about-attribution a");
        Assert.Contains("github.com/git/git/", attribution.GetAttribute("href"));
        Assert.Equal("noopener", attribution.GetAttribute("rel"));
        Assert.Single(page.FindAll("a[href='https://github.com/Mercantech/Mercantec.tech']"));
    }

    private static BunitContext BrowserContext()
    {
        var context = new BunitContext();
        var module = context.JSInterop.SetupModule(AboutCharacterSpotlight.ModulePath);
        module.Setup<int>("observe", _ => true).SetResult(1);
        module.SetupVoid("unobserve", _ => true).SetVoidResult();
        return context;
    }
}
