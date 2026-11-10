using Bunit;
using Mercantec.Tech.Web.Characters;
using Mercantec.Tech.Web.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Mercantec.Tech.Web.Tests;

public sealed class AboutCharacterTests
{
    [Fact]
    public async Task Spotlight_uses_six_second_intervals_and_wraps_through_the_shared_character_catalogue()
    {
        using var context = BrowserContext();
        var clock = new ManualCharacterTime();
        var spotlight = context.Render<AboutCharacterSpotlight>(p => p.Add(x => x.Clock, clock));
        AssertProfile(spotlight, CharacterCatalog.All[0]);

        await spotlight.InvokeAsync(() => clock.Advance(TimeSpan.FromSeconds(5)));
        AssertProfile(spotlight, CharacterCatalog.All[0]);
        await spotlight.InvokeAsync(() => clock.Advance(TimeSpan.FromSeconds(1)));
        AssertProfile(spotlight, CharacterCatalog.All[1]);
        for (var index = 2; index <= CharacterCatalog.All.Count; index++)
        {
            await spotlight.InvokeAsync(() => clock.Advance(TimeSpan.FromSeconds(6)));
            AssertProfile(spotlight, CharacterCatalog.All[index % CharacterCatalog.All.Count]);
        }
    }

    [Fact]
    public async Task Pause_hover_focus_and_a_hidden_page_each_hold_the_current_profile()
    {
        using var context = BrowserContext();
        var clock = new ManualCharacterTime();
        var spotlight = context.Render<AboutCharacterSpotlight>(p => p.Add(x => x.Clock, clock));

        spotlight.Find(".spotlight-pause").Click();
        Assert.Equal("true", spotlight.Find(".spotlight-pause").GetAttribute("aria-pressed"));
        await spotlight.InvokeAsync(() => clock.Advance(TimeSpan.FromSeconds(18)));
        AssertProfile(spotlight, CharacterCatalog.All[0]);
        spotlight.Find(".spotlight-pause").Click();

        spotlight.Find("figure").TriggerEvent("onmouseenter", new MouseEventArgs());
        await spotlight.InvokeAsync(() => clock.Advance(TimeSpan.FromSeconds(12)));
        AssertProfile(spotlight, CharacterCatalog.All[0]);
        spotlight.Find("figure").TriggerEvent("onmouseleave", new MouseEventArgs());

        await spotlight.Instance.SetFocusWithin(true);
        await spotlight.InvokeAsync(() => clock.Advance(TimeSpan.FromSeconds(12)));
        AssertProfile(spotlight, CharacterCatalog.All[0]);
        await spotlight.Instance.SetFocusWithin(false);

        await spotlight.Instance.SetPageVisible(false);
        await spotlight.InvokeAsync(() => clock.Advance(TimeSpan.FromSeconds(12)));
        AssertProfile(spotlight, CharacterCatalog.All[0]);
        await spotlight.Instance.SetPageVisible(true);

        await spotlight.InvokeAsync(() => clock.Advance(TimeSpan.FromSeconds(5)));
        AssertProfile(spotlight, CharacterCatalog.All[0]);
        await spotlight.InvokeAsync(() => clock.Advance(TimeSpan.FromSeconds(1)));
        AssertProfile(spotlight, CharacterCatalog.All[1]);
    }

    [Fact]
    public async Task Manual_spotlight_controls_wrap_and_disposal_stops_future_timer_work()
    {
        using var context = BrowserContext();
        var clock = new ManualCharacterTime();
        var spotlight = context.Render<AboutCharacterSpotlight>(p => p.Add(x => x.Clock, clock));

        spotlight.Find("button[aria-label='Vis forrige karakter']").Click();
        AssertProfile(spotlight, CharacterCatalog.All[^1]);
        Assert.Contains(CharacterCatalog.All[^1].Name, spotlight.Find("[role='status']").TextContent);
        spotlight.Find("button[aria-label='Vis næste karakter']").Click();
        AssertProfile(spotlight, CharacterCatalog.All[0]);
        await spotlight.InvokeAsync(() => spotlight.Instance.DisposeAsync().AsTask());
        Assert.Equal(0, clock.ActiveTimers);
        await spotlight.InvokeAsync(() => clock.Advance(TimeSpan.FromMinutes(1)));
        AssertProfile(spotlight, CharacterCatalog.All[0]);
    }

    [Fact]
    public void Manual_browser_reaches_every_existing_character_without_automatically_advancing()
    {
        using var context = new BunitContext();
        var browser = context.Render<AboutCharacterBrowser>();
        var reached = new HashSet<string>();
        var pages = (CharacterCatalog.All.Count + 2) / 3;
        for (var page = 0; page < pages; page++)
        {
            foreach (var link in browser.FindAll(".character-browser-list a")) reached.Add(link.GetAttribute("href")!);
            Assert.Equal(3, browser.FindAll(".character-browser-list li").Count);
            browser.Find("button[aria-label='Vis næste karakterer']").Click();
        }
        Assert.Equal(CharacterCatalog.All.Select(character => character.Path).Order(), reached.Order());
        Assert.Equal(CharacterCatalog.All[0].Path, browser.Find(".character-browser-list a").GetAttribute("href"));
        browser.Find("button[aria-label='Vis forrige karakterer']").Click();
        Assert.Contains($"af {CharacterCatalog.All.Count}", browser.Find("[role='status']").TextContent);
        Assert.Contains(browser.FindAll(".character-browser-list a"), a => a.GetAttribute("href") == CharacterCatalog.All[^1].Path);
        Assert.All(browser.FindAll(".character-browser-list img"), image => Assert.Equal("lazy", image.GetAttribute("loading")));
    }

    private static void AssertProfile(IRenderedComponent<AboutCharacterSpotlight> spotlight, CharacterProfile character)
    {
        Assert.Equal(character.Path, spotlight.Find(".spotlight-profile").GetAttribute("href"));
        Assert.Contains(character.Name, spotlight.Find("figcaption").TextContent);
        Assert.Contains(character.Role, spotlight.Find("figcaption").TextContent);
        Assert.Contains(character.Home, spotlight.Find("figcaption").TextContent);
    }

    private static BunitContext BrowserContext()
    {
        var context = new BunitContext();
        var module = context.JSInterop.SetupModule(AboutCharacterSpotlight.ModulePath);
        module.Setup<int>("observe", _ => true).SetResult(1);
        module.SetupVoid("unobserve", _ => true).SetVoidResult();
        return context;
    }

    private sealed class ManualCharacterTime : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];
        private TimeSpan _elapsed;
        public int ActiveTimers => _timers.Count(timer => !timer.Disposed);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            _timers.Add(timer);
            return timer;
        }
        public void Advance(TimeSpan amount)
        {
            _elapsed += amount;
            foreach (var timer in _timers)
                while (!timer.Disposed && timer.Next <= _elapsed)
                {
                    timer.Next += timer.Period;
                    timer.Callback(timer.State);
                }
        }

        private sealed class ManualTimer(ManualCharacterTime owner, TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) : ITimer
        {
            public TimerCallback Callback { get; } = callback;
            public object? State { get; } = state;
            public TimeSpan Next { get; set; } = owner._elapsed + dueTime;
            public TimeSpan Period { get; private set; } = period;
            public bool Disposed { get; private set; }
            public bool Change(TimeSpan due, TimeSpan every) { if (Disposed) return false; Next = owner._elapsed + due; Period = every; return true; }
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
