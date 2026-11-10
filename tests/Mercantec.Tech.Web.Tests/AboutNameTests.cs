using Bunit;
using Mercantec.Tech.Web.Branding;
using Mercantec.Tech.Web.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class AboutNameTests
{
    [Fact]
    public void Every_shared_name_and_explanation_remains_visible_in_the_redesigned_section()
    {
        using var context = new BunitContext();
        var section = context.Render<AboutName>();

        Assert.Equal("Hvad står LUP for?", section.Find("h2").TextContent);
        Assert.Equal(LupIdentity.Expansions.Count, section.FindAll(".about-expansions>li").Count);
        foreach (var expansion in LupIdentity.Expansions)
        {
            Assert.Contains(section.FindAll(".about-expansion-copy"), copy =>
                copy.QuerySelector("h3")?.TextContent == expansion.Name &&
                copy.QuerySelector("p")?.TextContent == expansion.Description);
        }
    }

    [Fact]
    public void Only_the_official_definition_receives_the_distinctive_treatment()
    {
        using var context = new BunitContext();
        var section = context.Render<AboutName>();

        var official = Assert.Single(section.FindAll(".about-expansions>.is-official"));
        Assert.Equal(LupIdentity.OfficialName, official.QuerySelector("h3")!.TextContent);
        Assert.Equal("true", official.QuerySelector(".about-name-mark")!.GetAttribute("aria-hidden"));
        Assert.Equal(LupIdentity.Expansions.Count(expansion => !expansion.IsOfficial),
            section.FindAll(".about-expansions>li:not(.is-official)").Count);
        Assert.Equal(section.Find("h2").Id, section.Find("section").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void The_original_attribution_remains_a_safe_external_link()
    {
        using var context = new BunitContext();
        var section = context.Render<AboutName>();

        var attribution = section.Find(".about-attribution a");
        Assert.Equal("Gits første README", attribution.TextContent);
        Assert.Equal("https://github.com/git/git/blob/e83c5163316f89bfbde7d9ab23ca2e25604af290/README#L1-L11", attribution.GetAttribute("href"));
        Assert.Equal("_blank", attribution.GetAttribute("target"));
        Assert.Equal("noopener", attribution.GetAttribute("rel"));
    }
}
