using Bunit;
using Mercantec.Tech.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class SnowControlsTests
{
    [Fact]
    public void Saved_snow_preference_is_available_as_a_named_select_without_a_pause_button()
    {
        using var context = new BunitContext();
        var javascript = new SnowJavascript { State = new() { Mode = "off", Paused = true } };
        context.Services.AddSingleton<IJSRuntime>(javascript);
        var controls = context.Render<SnowControls>(p => p.Add(x => x.Id, "test-snow"));

        var select = controls.Find("select");
        Assert.Equal("off", select.GetAttribute("value"));
        Assert.Equal("test-snow-mode", select.Id);
        Assert.Equal(select.Id, controls.Find("label").GetAttribute("for"));
        Assert.Equal("test-snow-description", select.GetAttribute("aria-describedby"));
        Assert.Equal(new[] { "auto", "on", "off" }, controls.FindAll("option").Select(option => option.GetAttribute("value")));
        Assert.Contains("stille sne", controls.Find("[role=status]").TextContent);
        Assert.Empty(controls.FindAll("button, details"));
        Assert.False(select.HasAttribute("disabled"));
    }

    [Fact]
    public void Moving_snow_remains_an_explicit_option_for_a_person_who_prefers_reduced_motion()
    {
        using var context = new BunitContext();
        var javascript = new SnowJavascript { State = new() { Mode = "auto", Paused = true, ReducedMotion = true } };
        context.Services.AddSingleton<IJSRuntime>(javascript);
        var controls = context.Render<SnowControls>();
        Assert.Contains("reduceret bevægelse", controls.Find("[role=status]").TextContent);

        controls.Find("select").Change("on");

        Assert.Equal(new[] { "on" }, javascript.ChangedModes);
        Assert.Equal("on", controls.Find("select").GetAttribute("value"));
        Assert.Contains("Du har valgt bevægelig sne", controls.Find("[role=status]").TextContent);
        Assert.DoesNotContain("Start sne", controls.Markup);
        Assert.DoesNotContain("Pause sne", controls.Markup);
    }

    [Fact]
    public async Task Observed_state_updates_the_setting_and_the_observer_is_removed_on_disposal()
    {
        using var context = new BunitContext();
        var javascript = new SnowJavascript();
        context.Services.AddSingleton<IJSRuntime>(javascript);
        var controls = context.Render<SnowControls>();
        var observer = javascript.Observer!;
        Assert.Equal(1, javascript.Observations);

        await controls.InvokeAsync(() => observer.Value.SnowChanged(new() { Mode = "on", Hidden = true }));
        Assert.Equal("on", controls.Find("select").GetAttribute("value"));
        Assert.Contains("fanen er skjult", controls.Find("[role=status]").TextContent);

        await context.DisposeComponentsAsync();
        Assert.Equal(new[] { 17 }, javascript.Unobserved);
    }

    [Fact]
    public void Snow_settings_are_in_the_christmas_picker_and_release_the_observer_when_the_theme_changes()
    {
        using var context = new BunitContext();
        var javascript = new SnowJavascript();
        context.Services.AddSingleton<IJSRuntime>(javascript);
        var picker = context.Render<ThemePicker>(p => p.Add(x => x.Id, "header-theme").Add(x => x.Value, "light"));
        Assert.Empty(picker.FindComponents<SnowControls>());
        Assert.Equal(0, javascript.Observations);

        picker.Render(p => p.Add(x => x.Value, "christmas"));
        Assert.Single(picker.FindComponents<SnowControls>());
        Assert.Equal("header-theme-snow-mode", picker.Find("select").Id);
        Assert.Equal(1, javascript.Observations);

        picker.Render(p => p.Add(x => x.Value, "newyear"));
        Assert.Empty(picker.FindComponents<SnowControls>());
        Assert.Equal(new[] { 17 }, javascript.Unobserved);
    }

    [Fact]
    public void The_seasonal_corner_contains_no_snow_controls()
    {
        using var context = new BunitContext();
        context.ComponentFactories.AddStub<GiftLauncher>();
        context.ComponentFactories.AddStub<NewYearControls>();
        var decorations = context.Render<ThemeDecorations>();

        Assert.Single(decorations.FindAll("aside.christmas-corner"));
        Assert.Empty(decorations.FindComponents<SnowControls>());
        Assert.Empty(decorations.FindAll(".snow-toggle, .snow-settings, .snow-preferences"));
        Assert.Contains("Gaver i LUP", decorations.Find("aside").GetAttribute("aria-label"));
    }

    private sealed class SnowJavascript : IJSRuntime
    {
        public SnowControls.SnowState State { get; set; } = new();
        public DotNetObjectReference<SnowControls>? Observer { get; private set; }
        public int Observations { get; private set; }
        public List<string> ChangedModes { get; } = [];
        public List<int> Unobserved { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            object? result;
            switch (identifier)
            {
                case "lupTheme.snowState": result = State; break;
                case "lupTheme.observeSnow":
                    Observer = (DotNetObjectReference<SnowControls>)args![0]!;
                    Observations++;
                    result = 17;
                    break;
                case "lupTheme.setSnowMode":
                    var mode = (string)args![0]!;
                    ChangedModes.Add(mode);
                    State = new() { Mode = mode, ReducedMotion = State.ReducedMotion, Hidden = State.Hidden };
                    result = State;
                    break;
                case "lupTheme.unobserveSnow":
                    Unobserved.Add((int)args![0]!);
                    result = null;
                    break;
                default: throw new InvalidOperationException("Unexpected JavaScript call: " + identifier);
            }
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
    }
}
