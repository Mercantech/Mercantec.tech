using Bunit;
using Mercantec.Tech.Web.Components;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class ThemePickerTests
{
    [Fact]
    public void Theme_control_is_named_and_links_to_its_appearance_panel()
    {
        using var context = CreateContext();
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
        using var context = CreateContext();
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
        using var context = CreateContext();
        var changes = new List<string>();
        var callback = EventCallback.Factory.Create<string>(this, value => changes.Add(value));
        var picker = context.Render<ThemePicker>(p => p.Add(x => x.Value, "light").Add(x => x.ValueChanged, callback));
        picker.Find("button[aria-label='Jul']").Click();
        picker.Find("button[aria-label='Hacker']").Click();
        picker.Find("button[aria-label='System']").Click();
        Assert.Equal(new[] { "christmas", "hacker", "system" }, changes);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Setup<SnowControls.SnowState>("lupTheme.snowState").SetResult(new());
        context.JSInterop.Setup<int>("lupTheme.observeSnow", _ => true).SetResult(1);
        context.JSInterop.SetupVoid("lupTheme.unobserveSnow", _ => true).SetVoidResult();
        return context;
    }
}
