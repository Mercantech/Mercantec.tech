using Bunit;
using Mercantec.Tech.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class ProfileThemeSettingsTests
{
    [Theory]
    [InlineData("system", "System", "Hacker", "hacker")]
    [InlineData("hacker", "Hacker", "System", "system")]
    public void System_has_its_own_button_and_uses_the_same_theme_callback(
        string theme, string currentLabel, string nextLabel, string nextTheme)
    {
        using var context = new BunitContext();
        var changes = new List<string>();
        var callback = EventCallback.Factory.Create<string>(this, value => changes.Add(value));
        context.Services.AddCascadingValue("LupTheme", _ => theme);
        context.Services.AddCascadingValue("LupThemeChanged", _ => callback);

        var settings = context.Render<ProfileThemeSettings>();

        Assert.Equal("System", Label(settings.Find("button.profile-theme-system")));
        var fixedThemes = settings.FindAll(".profile-theme-options button");
        Assert.Equal(5, fixedThemes.Count);
        Assert.DoesNotContain(fixedThemes, option => Label(option) == "System");
        Assert.Equal(currentLabel, Label(Assert.Single(settings.FindAll("button[aria-pressed='true']"))));

        settings.FindAll("button").Single(option => Label(option) == nextLabel).Click();

        Assert.Equal(nextTheme, Assert.Single(changes));
    }

    private static string Label(AngleSharp.Dom.IElement option) =>
        option.GetAttribute("aria-label") ?? option.TextContent.Trim();
}
