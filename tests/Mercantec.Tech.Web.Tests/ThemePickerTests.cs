using Bunit;
using Mercantec.Tech.Web.Components;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class ThemePickerTests
{
    [Fact]
    public void Theme_control_is_named_and_links_to_its_appearance_panel()
    {
        using var context = new BunitContext();
        var picker = context.Render<ThemePicker>(p => p.Add(x => x.Value, "newyear"));
        var trigger = picker.Find("summary");
        Assert.Contains("Tema", trigger.TextContent);
        Assert.Contains("Nytår", trigger.GetAttribute("aria-label"));
        Assert.Equal(picker.Find("section").Id, trigger.GetAttribute("aria-controls"));
        Assert.Equal("Vælg tema", picker.Find("section").GetAttribute("aria-label"));
        Assert.Equal(6, picker.FindAll("button").Count);
    }

    [Theory]
    [InlineData("light", "Lyst")]
    [InlineData("dark", "Mørkt")]
    [InlineData("christmas", "Jul")]
    [InlineData("newyear", "Nytår")]
    public void The_current_choice_has_an_explicit_pressed_state_and_a_visible_check(string id, string name)
    {
        using var context = new BunitContext();
        var picker = context.Render<ThemePicker>(p => p.Add(x => x.Value, id));
        var selected = Assert.Single(picker.FindAll("button[aria-pressed='true']"));
        Assert.Equal(name, selected.GetAttribute("aria-label"));
        Assert.Single(selected.QuerySelectorAll(".theme-option-check svg"));
        Assert.Equal(5, picker.FindAll("button[aria-pressed='false']").Count);
        Assert.Contains(name, picker.Find(".theme-current").TextContent);
    }

    [Fact]
    public void Picking_a_theme_requests_the_exact_new_value()
    {
        using var context = new BunitContext();
        var changes = new List<string>();
        var callback = EventCallback.Factory.Create<string>(this, value => changes.Add(value));
        var picker = context.Render<ThemePicker>(p => p.Add(x => x.Value, "light").Add(x => x.ValueChanged, callback));
        picker.Find("button[aria-label='Jul']").Click();
        picker.Find("button[aria-label='Hacker']").Click();
        picker.Find("button[aria-label='System']").Click();
        Assert.Equal(new[] { "christmas", "hacker", "system" }, changes);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("christmas")]
    [InlineData("newyear")]
    [InlineData("hacker")]
    public void Every_theme_picker_contains_only_theme_choices_without_snow_settings(string theme)
    {
        using var context = new BunitContext();
        var picker = context.Render<ThemePicker>(p => p.Add(x => x.Value, theme));

        Assert.Equal(6, picker.FindAll("button").Count);
        Assert.Empty(picker.FindAll("select, input, .snow-preferences, .snow-controls, .snow-toggle, .snow-settings"));
        Assert.DoesNotContain("Julesne", picker.Markup);
        Assert.DoesNotContain("Pause sne", picker.Markup);
        Assert.DoesNotContain("Start sne", picker.Markup);
    }

    [Fact]
    public void The_seasonal_corner_retains_gifts_without_any_snow_controls()
    {
        using var context = new BunitContext();
        context.ComponentFactories.AddStub<GiftLauncher>();
        context.ComponentFactories.AddStub<NewYearControls>();
        var decorations = context.Render<ThemeDecorations>();

        Assert.Single(decorations.FindAll("#lup-snow-host"));
        Assert.Single(decorations.FindAll("aside.christmas-corner"));
        Assert.Empty(decorations.FindAll(".snow-toggle, .snow-settings, .snow-preferences, .snow-controls"));
        Assert.Contains("Gaver i LUP", decorations.Find("aside").GetAttribute("aria-label"));
    }
}
