using Bunit;
using Mercantec.Tech.Web.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class HackerCyberLabTests
{
    [Fact]
    public void Learner_can_read_terminal_output_and_reset_the_exercise()
    {
        using var context = new BunitContext();
        context.JSInterop.SetupVoid("lupCyberLabScroll", _ => true).SetVoidResult();
        var lab = context.Render<HackerCyberLab>();

        lab.FindAll(".hacker-shortcuts button").Single(button => button.TextContent == "ls").Click();
        Assert.Contains("incident.txt", lab.Find(".hacker-entry pre").TextContent);

        lab.Find("#hacker-lab-command").Input("cat auth.log");
        lab.Find(".hacker-command-form").Submit();
        Assert.Equal(2, lab.FindAll(".hacker-entry").Count);
        Assert.Contains("cat auth.log", lab.FindAll(".hacker-entry .hacker-prompt")[1].TextContent);
        Assert.Equal("", lab.Find("#hacker-lab-command").GetAttribute("value"));
        Assert.Equal(
            new[] { "/characters/bee", "/characters/beaver", "/characters/fox" },
            lab.FindAll(".hacker-hints a").Select(link => link.GetAttribute("href")));

        lab.Find(".hacker-reset").Click();
        Assert.Empty(lab.FindAll(".hacker-entry"));
        Assert.Contains("Hændelse under undersøgelse", lab.Find(".hacker-status").TextContent);
    }
}
