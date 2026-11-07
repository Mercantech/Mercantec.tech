using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Learning;
using Mercantec.Tech.Web.Notion;
using Mercantec.Tech.Web.Pages.Courses;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class RouletteIntegrationTests
{
    [Fact]
    public async Task The_runtime_module_uses_a_versioned_URL_to_bypass_previously_cached_speech_code()
    {
        using var api = new RouletteApi { SpeechAvailable = true };
        var module = new ControlledModule();
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));

        page.WaitForAssertion(() =>
        {
            Assert.Single(module.ImportedScripts);
            Assert.False(page.Find("option[value='rost']").HasAttribute("disabled"));
        });

        Assert.Equal("/js/group-roulette-show.js?v=announcer-v2-1", Assert.Single(module.ImportedScripts));
    }

    [Fact]
    public async Task The_table_view_reads_and_updates_the_same_theme_preference_as_the_platform()
    {
        using var api = new RouletteApi();
        var module = new ControlledModule { Theme = "hacker" };
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.Equal("hacker", page.Find("select[aria-label='Tema i platformen']").GetAttribute("value")));

        await page.Find("select[aria-label='Tema i platformen']").ChangeAsync(new() { Value = "light" });

        Assert.Equal("light", module.Theme);
        Assert.Equal("light", page.Find("select[aria-label='Tema i platformen']").GetAttribute("value"));
        Assert.Equal("light", Assert.Single(module.BrowserCalls, call => call.Identifier == "lupTheme.set").Arguments[0]);
        Assert.Equal(0, module.SpinCount);
        Assert.DoesNotContain(api.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Renaming_the_second_round_in_the_overview_sends_its_entered_set_name_when_saving()
    {
        using var api = new RouletteApi { MemberCount = 3 };
        var module = new ControlledModule();
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.False(page.Find(".roulette-spin").HasAttribute("disabled")));
        page.FindAll("label").Single(label => label.TextContent.Trim() == "Undgå tidligere makkerpar")
            .QuerySelector("input")!.Change(false);

        async Task FinishRound()
        {
            var click = page.Find(".roulette-spin").ClickAsync(new MouseEventArgs());
            await module.WaitForSpin();
            module.CompleteSpin();
            await module.WaitForSpeech();
            module.CompleteSpeech();
            await click.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await FinishRound();
        await Button(page, "Ny runde").ClickAsync(new MouseEventArgs());
        await FinishRound();
        await Button(page, "Overblik").ClickAsync(new MouseEventArgs());

        IElement Round(int number) => page.FindAll(".roulette-overview-round")
            .Single(round => round.QuerySelector("h3")!.TextContent.Trim() == $"Runde {number}");
        const string enteredName = "QA · Røst og rulning";
        Round(2).QuerySelector(".roulette-destination input")!.Change(enteredName);
        Assert.Equal(enteredName, Round(2).QuerySelector(".roulette-destination input")!.GetAttribute("value"));
        Assert.Equal("Makkerpar · Runde 1", Round(1).QuerySelector(".roulette-destination input")!.GetAttribute("value"));
        await Round(2).QuerySelectorAll("button").Single(button => button.TextContent.Trim() == "Gem runden på holdet")
            .ClickAsync(new MouseEventArgs());

        Assert.Equal(enteredName, Assert.Single(api.SetRequests).Name);
        Assert.Equal(api.CreatedSetId, Assert.Single(api.GroupBatches).GroupSetId);
        Assert.Contains($"Gemt i {enteredName}", Round(2).TextContent);
        Assert.NotNull(Round(1).QuerySelector(".roulette-destination input"));
    }

    [Fact]
    public async Task Reduced_motion_allows_the_teacher_to_explicitly_enable_animation_for_the_classroom()
    {
        using var api = new RouletteApi();
        var module = new ControlledModule { ReducedMotion = true };
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("button"), button =>
            button.TextContent.Contains("Slå animationer til", StringComparison.Ordinal)));
        var animationChoice = page.FindAll("label").Single(label => label.TextContent.Trim() == "Animationer").QuerySelector("input")!;
        Assert.False(animationChoice.HasAttribute("checked"));

        await Button(page, "Slå animationer til").ClickAsync(new MouseEventArgs());
        Assert.DoesNotContain(page.FindAll("button"), button =>
            button.TextContent.Contains("Slå animationer til", StringComparison.Ordinal));
        Assert.True(page.FindAll("label").Single(label => label.TextContent.Trim() == "Animationer").QuerySelector("input")!.HasAttribute("checked"));
        var click = page.Find(".roulette-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        var options = JsonSerializer.SerializeToElement(Assert.Single(module.Calls, call => call.Identifier == "spin").Arguments[4]);
        Assert.True(options.GetProperty("motion").GetBoolean());

        module.CompleteSpin();
        await module.WaitForSpeech();
        module.CompleteSpeech();
        await click.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(page.FindAll(".roulette-group-card"));
    }

    [Fact]
    public async Task Audio_denied_on_first_spin_turns_off_the_sound_indicator_and_explains_the_recovery()
    {
        using var api = new RouletteApi();
        var module = new ControlledModule { AudioReady = false };
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.False(page.Find(".roulette-spin").HasAttribute("disabled")));
        Assert.Contains("Lyde til", page.Find("button[data-roulette-audio]").TextContent);
        Assert.Equal("true", page.Find("button[data-roulette-audio]").GetAttribute("aria-pressed"));

        var spin = page.Find(".roulette-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        module.CompleteSpin();
        await module.WaitForSpeech();
        module.CompleteSpeech();
        await spin.WaitAsync(TimeSpan.FromSeconds(5));

        var sound = page.Find("button[data-roulette-audio]");
        Assert.Contains("Lyde fra", sound.TextContent);
        Assert.Equal("false", sound.GetAttribute("aria-pressed"));
        Assert.Contains(page.FindAll(".roulette-message"), message =>
            message.TextContent.Contains("Browseren kunne ikke aktivere lydene", StringComparison.Ordinal)
            && message.TextContent.Contains("Tryk Lyde til", StringComparison.Ordinal));
        Assert.Single(page.FindAll(".roulette-group-card"));
        Assert.False(page.Find(".roulette-spin").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData("student")]
    [InlineData("preview")]
    [InlineData("signedout")]
    public async Task Non_managing_context_never_requests_speech_or_exposes_spin_controls(string mode)
    {
        using var api = new RouletteApi();
        var module = new ControlledModule();
        using var context = await CreateContext(api, module, teacher: mode != "student", preview: mode == "preview");
        if (mode == "signedout") await context.Auth.LogoutLocalAsync();

        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));

        page.WaitForAssertion(() => Assert.Contains(mode == "signedout" ? "Log ind for at spinde" : "Grupperouletten er til underviseren",
            page.Find("h1").TextContent));
        Assert.Empty(page.FindAll(".roulette-spin"));
        Assert.DoesNotContain(api.Requests, r => r.Path == RouletteNameCatalog.ManifestPath);
        Assert.DoesNotContain(api.Requests, r => r.Path.EndsWith("/roulette/speech", StringComparison.Ordinal));
        Assert.Equal(0, module.SpinCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Missing_or_failing_pre_recorded_clips_keep_the_browser_announcement_and_manual_round_usable(bool available, bool fails)
    {
        using var api = new RouletteApi { SpeechAvailable = available };
        var module = new ControlledModule { ClipsAvailable = !fails };
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.Contains(api.Requests, r => r.Path == RouletteNameCatalog.ManifestPath));

        var click = page.Find(".roulette-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        module.CompleteSpin();
        await module.WaitForSpeech();
        Assert.Equal(1, module.BrowserSpeechCount);
        Assert.Equal(0, module.LocalSpeechCount);
        Assert.True(page.Find(".roulette-spin").HasAttribute("disabled"));
        Assert.True(Button(page, "Ny runde").HasAttribute("disabled"));

        module.CompleteSpeech();
        await click.WaitAsync(TimeSpan.FromSeconds(5));

        page.WaitForAssertion(() => Assert.False(page.Find(".roulette-spin").HasAttribute("disabled")));
        Assert.Single(page.FindAll(".roulette-group-card"));
        Assert.DoesNotContain(api.Requests, r => r.Method != HttpMethod.Get);
        Assert.DoesNotContain(api.Requests, r => r.Path.EndsWith("/roulette/speech", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Auto_spin_waits_for_both_pre_recorded_words_before_the_pause_and_next_group_without_generating_audio()
    {
        using var api = new RouletteApi { SpeechAvailable = true };
        var module = new ControlledModule();
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.False(page.Find("option[value='rost']").HasAttribute("disabled")));
        PauseInput(page).Change("0.5");

        var auto = Button(page, "Start auto-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        Assert.True(page.Find(".roulette-spin").HasAttribute("disabled"));
        Assert.True(page.Find(".roulette-lever").HasAttribute("disabled"));
        module.CompleteSpin();
        await module.WaitForSpeech();
        Assert.Equal(1, module.LocalSpeechCount);

        // Deliberately outlast the chosen 0.5-second pause while speech remains
        // pending: an implementation which fires-and-forgets speech would spin again.
        await Task.Delay(750);
        Assert.Equal(1, module.SpinCount);
        module.CompleteSpeech();
        await module.WaitForSpin();
        Assert.Equal(2, module.SpinCount);

        await Button(page, "Stop auto-spin").ClickAsync(new MouseEventArgs());
        module.CompleteSpin();
        await module.WaitForSpeech();
        module.CompleteSpeech();
        await auto.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, page.FindAll(".roulette-group-card").Count);
        Assert.Single(api.Requests, r => r.Path == RouletteNameCatalog.ManifestPath);
        Assert.All(module.Calls.Where(call => call.Identifier == "playSpeechSegments"), call =>
        {
            var clips = Assert.IsType<string[]>(call.Arguments[1]);
            Assert.Equal(2, clips.Length);
            Assert.All(clips, clip => Assert.StartsWith(RouletteNameCatalog.AssetRoot + "/", clip));
        });
        Assert.DoesNotContain(api.Requests, r => r.Method != HttpMethod.Get);
        Assert.DoesNotContain(api.Requests, r => r.Path.EndsWith("/groups/batch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stopping_during_browser_speech_finishes_only_this_group_and_allows_a_clean_new_round()
    {
        using var api = new RouletteApi();
        var module = new ControlledModule();
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.False(page.Find(".roulette-spin").HasAttribute("disabled")));
        PauseInput(page).Change("0.5");

        var auto = Button(page, "Start auto-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        module.CompleteSpin();
        await module.WaitForSpeech();
        Assert.Equal(1, module.BrowserSpeechCount);
        await Button(page, "Stop auto-spin").ClickAsync(new MouseEventArgs());
        Assert.True(Button(page, "Ny runde").HasAttribute("disabled"));
        Assert.True(page.Find(".roulette-spin").HasAttribute("disabled"));
        module.CompleteSpeech();
        await auto.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(750);
        Assert.Equal(1, module.SpinCount);
        Assert.Single(page.FindAll(".roulette-group-card"));

        await Button(page, "Ny runde").ClickAsync(new MouseEventArgs());
        await Button(page, "Start ny runde").ClickAsync(new MouseEventArgs());
        Assert.Contains("Runde 2", page.Find(".roulette-round-label").TextContent);
        Assert.Empty(page.FindAll(".roulette-group-card"));

        var manual = page.Find(".roulette-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        module.CompleteSpin();
        await module.WaitForSpeech();
        module.CompleteSpeech();
        await manual.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, module.SpinCount);
        Assert.Single(page.FindAll(".roulette-group-card"));
    }

    [Fact]
    public async Task The_round_intro_runs_with_the_reels_and_finishes_before_the_group_presentation_starts()
    {
        using var api = new RouletteApi { SpeechAvailable = true, CueBankAvailable = true, MemberCount = 6 };
        var module = new ControlledModule();
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.False(page.Find(".roulette-spin").HasAttribute("disabled")));

        var click = page.Find(".roulette-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        await module.WaitForSpeech();
        Assert.Equal(new[] { CueUrl("round-start") }, AnnouncementClips(module, 0));
        var options = JsonSerializer.SerializeToElement(Assert.Single(module.Calls, call => call.Identifier == "spin").Arguments[4]);
        Assert.True(options.GetProperty("preserveAnnouncement").GetBoolean());
        Assert.False(click.IsCompleted);

        module.CompleteSpin();
        // The visual spin has ended, but the independently gated intro still owns the microphone.
        await Task.Delay(75);
        Assert.Empty(page.FindAll(".roulette-group-card"));
        Assert.Single(module.Announcements);
        Assert.False(click.IsCompleted);
        module.CompleteSpeech();
        await module.WaitForSpeech();

        var groupName = Assert.Single(page.FindAll(".roulette-group-card h3")).TextContent;
        var nameClips = Assert.IsType<string[]>(RouletteNameCatalog.GetClipUrls(groupName));
        var presentation = AnnouncementClips(module, 1);
        Assert.Equal(4, presentation.Length);
        Assert.Equal(CueUrl("intro-group"), presentation[0]);
        Assert.Equal(nameClips, presentation[1..3]);
        Assert.Contains(RouletteAnnouncementCatalog.All.Where(cue => cue.Id.StartsWith("celebration-", StringComparison.Ordinal)),
            cue => CueUrl(cue.Id) == presentation[3]);
        Assert.True(page.Find(".roulette-spin").HasAttribute("disabled"));
        module.CompleteSpeech();
        await click.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, module.Announcements.Length);
        Assert.DoesNotContain(api.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Auto_spin_waits_for_each_full_presentation_and_the_final_round_announcement()
    {
        using var api = new RouletteApi { SpeechAvailable = true, CueBankAvailable = true, MemberCount = 6 };
        var module = new ControlledModule();
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.False(page.Find(".roulette-spin").HasAttribute("disabled")));
        PauseInput(page).Change("0.5");

        var auto = Button(page, "Start auto-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        await module.WaitForSpeech();
        module.CompleteSpeech();
        module.CompleteSpin();
        await module.WaitForSpeech();
        Assert.Equal(4, AnnouncementClips(module, 1).Length);
        await Task.Delay(750);
        Assert.Equal(1, module.SpinCount);

        module.CompleteSpeech();
        await module.WaitForSpin();
        Assert.Equal(2, module.SpinCount);
        module.CompleteSpin();
        await module.WaitForSpeech();
        Assert.Equal(4, AnnouncementClips(module, 2).Length);
        module.CompleteSpeech();
        await module.WaitForSpeech();
        Assert.Equal(new[] { CueUrl("round-complete") }, AnnouncementClips(module, 3));
        Assert.False(auto.IsCompleted);
        Assert.True(Button(page, "Ny runde").HasAttribute("disabled"));
        Assert.Equal(2, page.FindAll(".roulette-group-card").Count);

        module.CompleteSpeech();
        await auto.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(Button(page, "Ny runde").HasAttribute("disabled"));
        Assert.Equal(0, module.LocalSpeechCount);
        Assert.Equal(0, module.BrowserSpeechCount);
        Assert.DoesNotContain(api.Requests, request => request.Method != HttpMethod.Get);
    }

    [Fact]
    public async Task Names_only_mode_uses_the_two_word_clips_without_round_or_celebration_announcements()
    {
        using var api = new RouletteApi { SpeechAvailable = true, CueBankAvailable = true, MemberCount = 3 };
        var module = new ControlledModule();
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.False(page.Find(".roulette-spin").HasAttribute("disabled")));
        await page.Find("option[value='names']").ParentElement!.ChangeAsync(new() { Value = "names" });

        var click = page.Find(".roulette-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        Assert.Empty(module.Announcements);
        module.CompleteSpin();
        await module.WaitForSpeech();
        Assert.Equal(1, module.LocalSpeechCount);
        var words = Assert.IsType<string[]>(Assert.Single(module.Calls, call => call.Identifier == "playSpeechSegments").Arguments[1]);
        Assert.Equal(2, words.Length);
        module.CompleteSpeech();
        await click.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(module.Announcements);
        Assert.Contains("Runden er færdig", page.Find(".roulette-spin").TextContent);
    }

    [Theory]
    [InlineData("mute")]
    [InlineData("preview")]
    public async Task Muting_or_changing_account_context_cancels_an_intro_without_starting_late_announcements(string action)
    {
        using var api = new RouletteApi { SpeechAvailable = true, CueBankAvailable = true, MemberCount = 3 };
        var module = new ControlledModule();
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.False(page.Find(".roulette-spin").HasAttribute("disabled")));

        var click = page.Find(".roulette-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        await module.WaitForSpeech();
        if (action == "mute")
        {
            await page.Find(".roulette-top-actions button[data-roulette-voice]").ClickAsync(new MouseEventArgs());
            module.CompleteSpin();
        }
        else
        {
            context.ViewAs.Enter("preview-student", "Prøveelev", "H2", Guid.NewGuid());
            page.WaitForAssertion(() => Assert.Contains("Grupperouletten er til underviseren", page.Find("h1").TextContent));
        }
        await click.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(module.Announcements);
        Assert.Equal(0, module.LocalSpeechCount);
        Assert.Equal(0, module.BrowserSpeechCount);
        Assert.Contains(module.Calls, call => call.Identifier == (action == "mute" ? "cancelSpeech" : "cancel"));
        Assert.DoesNotContain(api.Requests, request => request.Method != HttpMethod.Get);
        if (action == "mute") Assert.Single(page.FindAll(".roulette-group-card"));
        else Assert.Empty(page.FindAll(".roulette-group-card"));
    }

    [Theory]
    [InlineData("Giv en hånd", "celebration-05", true)]
    [InlineData("Skab spænding", "drumroll", false)]
    public async Task Classroom_fun_buttons_only_present_their_cue_and_never_draw_or_save_groups(string button, string cue, bool celebration)
    {
        using var api = new RouletteApi { SpeechAvailable = true, CueBankAvailable = true };
        var module = new ControlledModule();
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.False(Button(page, button).HasAttribute("disabled")));

        var click = Button(page, button).ClickAsync(new MouseEventArgs());
        await module.WaitForSpeech();
        Assert.Equal(new[] { CueUrl(cue) }, AnnouncementClips(module, 0));
        Assert.Equal(celebration, module.Calls.Any(call => call.Identifier == "cheer"));
        Assert.True(page.Find(".roulette-spin").HasAttribute("disabled"));
        Assert.Equal(0, module.SpinCount);
        Assert.Empty(page.FindAll(".roulette-group-card"));
        Assert.DoesNotContain(api.Requests, request => request.Method != HttpMethod.Get);
        module.CompleteSpeech();
        await click.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(page.Find(".roulette-spin").HasAttribute("disabled"));
        Assert.DoesNotContain(module.Calls, call => call.Identifier == "saveSession");
    }

    [Fact]
    public async Task The_saved_announcement_runs_once_after_a_successful_save_and_never_for_a_failed_attempt()
    {
        using var api = new RouletteApi { SpeechAvailable = true, CueBankAvailable = true, MemberCount = 3 };
        var module = new ControlledModule();
        using var context = await CreateContext(api, module);
        var page = context.Render<Roulette>(p => p.Add(x => x.HoldId, api.HoldId));
        page.WaitForAssertion(() => Assert.False(page.Find(".roulette-spin").HasAttribute("disabled")));
        var spin = page.Find(".roulette-spin").ClickAsync(new MouseEventArgs());
        await module.WaitForSpin();
        await module.WaitForSpeech();
        module.CompleteSpeech();
        module.CompleteSpin();
        await module.WaitForSpeech();
        module.CompleteSpeech();
        await module.WaitForSpeech();
        module.CompleteSpeech();
        await spin.WaitAsync(TimeSpan.FromSeconds(5));

        api.FailGroupBatch = true;
        await Button(page, "Gem runden på holdet").ClickAsync(new MouseEventArgs());
        Assert.Single(api.GroupBatches);
        Assert.DoesNotContain(module.Announcements, call => AnnouncementClips(call).Contains(CueUrl("round-saved")));
        api.FailGroupBatch = false;
        var save = Button(page, "Gem runden på holdet").ClickAsync(new MouseEventArgs());
        await module.WaitForSpeech();
        Assert.Equal(2, api.GroupBatches.Count);
        Assert.Equal(new[] { CueUrl("round-saved") }, AnnouncementClips(module, 3));
        Assert.Contains("Gemt i", page.Find(".roulette-save-round").TextContent);
        module.CompleteSpeech();
        await save.WaitAsync(TimeSpan.FromSeconds(5));
        await Button(page, "Runden er gemt").ClickAsync(new MouseEventArgs());
        Assert.Equal(2, api.GroupBatches.Count);
        Assert.Single(module.Announcements, call => AnnouncementClips(call).Contains(CueUrl("round-saved")));
    }

    private static string[] AnnouncementClips(ControlledModule module, int index) => AnnouncementClips(module.Announcements[index]);
    private static string[] AnnouncementClips(JsCall call) => Assert.IsType<string[]>(call.Arguments[1]);
    private static string CueUrl(string id) => Assert.IsType<string>(RouletteAnnouncementCatalog.GetClipUrl(id));

    private static Task<LearningUiTestContext> CreateContext(RouletteApi api, ControlledModule module,
        bool teacher = true, bool preview = false)
    {
        api.CanManage = teacher;
        return LearningUiTestContext.CreateAsync(api, teacher, preview, services =>
        {
            var browser = (IJSRuntime)services.Last(s => s.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            services.AddSingleton<IJSRuntime>(new ModuleRuntime(browser, module));
            services.AddSingleton<NotionClient>();
            services.AddSingleton<GroupSetsClient>();
            services.AddSingleton<RouletteSpeechClient>();
        });
    }

    private static IElement Button(IRenderedComponent<Roulette> page, string text) =>
        page.FindAll("button").Single(button => button.TextContent.Contains(text, StringComparison.OrdinalIgnoreCase));
    private static IElement PauseInput(IRenderedComponent<Roulette> page) => page.FindAll("label")
        .Single(label => label.TextContent.Contains("Pause mellem grupper", StringComparison.Ordinal)).QuerySelector("input")!;

    private sealed record JsCall(string Identifier, object?[] Arguments);

    /// <summary>
    /// Holds the actual JS interop promises open, rather than replacing the component's
    /// spin/announcement methods. This exposes overlapping auto spins and late replies.
    /// </summary>
    private sealed class ControlledModule : IJSObjectReference
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly ConcurrentQueue<TaskCompletionSource<bool>> _spins = new();
        private readonly ConcurrentQueue<TaskCompletionSource<bool>> _speech = new();
        private readonly SemaphoreSlim _spinStarted = new(0), _speechStarted = new(0);
        public ConcurrentQueue<JsCall> Calls { get; } = new();
        public ConcurrentQueue<JsCall> BrowserCalls { get; } = new();
        public ConcurrentQueue<string> ImportedScripts { get; } = new();
        public string Theme { get; set; } = "system";
        public int SpinCount => Calls.Count(c => c.Identifier == "spin");
        public int BrowserSpeechCount => Calls.Count(c => c.Identifier == "playBrowserSpeech");
        public int LocalSpeechCount => Calls.Count(c => c.Identifier == "playSpeechSegments");
        public JsCall[] Announcements => Calls.Where(call => call.Identifier == "playAnnouncement").ToArray();
        public bool SoundAvailable { get; set; } = true;
        public bool ClipsAvailable { get; set; } = true;
        public bool? AudioReady { get; set; }
        public bool ReducedMotion { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Enqueue(new(identifier, args?.ToArray() ?? []));
            object? result = null;
            switch (identifier)
            {
                case "spin":
                    var spin = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _spins.Enqueue(spin);
                    _spinStarted.Release();
                    result = await spin.Task.WaitAsync(cancellationToken);
                    break;
                case "playBrowserSpeech":
                case "playSpeechSegments":
                case "playAnnouncement":
                    var speech = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _speech.Enqueue(speech);
                    _speechStarted.Release();
                    result = await speech.Task.WaitAsync(cancellationToken);
                    break;
                case "cancelSpeech":
                    while (_speech.TryDequeue(out var pendingSpeech)) pendingSpeech.TrySetResult(false);
                    break;
                case "cancel":
                    while (_spins.TryDequeue(out var pendingSpin)) pendingSpin.TrySetResult(false);
                    while (_speech.TryDequeue(out var interruptedSpeech)) interruptedSpeech.TrySetResult(false);
                    break;
                case "cheer":
                    result = true;
                    break;
                case "setSound":
                    result = SoundAvailable;
                    break;
                case "prepareSpeechSegments":
                    result = ClipsAvailable;
                    break;
                case "initialize":
                case "capabilities":
                case "getCapabilities":
                    result = new { reducedMotion = ReducedMotion, audioAvailable = SoundAvailable,
                        soundAvailable = SoundAvailable, audioReady = AudioReady, sound = SoundAvailable,
                        speechAvailable = true, browserSpeechAvailable = true };
                    break;
            }
            if (result is null || typeof(TValue).Name == "IJSVoidResult") return default!;
            if (result is TValue value) return value;
            return JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(result, Json), Json)!;
        }

        public void CompleteSpin(bool completed = true)
        {
            Assert.True(_spins.TryDequeue(out var gate), "A spin must be pending before the test completes it.");
            gate.SetResult(completed);
        }

        public async Task WaitForSpin() => Assert.True(await _spinStarted.WaitAsync(TimeSpan.FromSeconds(5)), "The component did not start the expected JS spin.");
        public async Task WaitForSpeech() => Assert.True(await _speechStarted.WaitAsync(TimeSpan.FromSeconds(5)), "The component did not start the expected spoken announcement.");

        public void CompleteSpeech(bool completed = true)
        {
            Assert.True(_speech.TryDequeue(out var gate), "An announcement must be pending before the test completes it.");
            gate.SetResult(completed);
        }

        public ValueTask DisposeAsync()
        {
            while (_spins.TryDequeue(out var spin)) spin.TrySetResult(false);
            while (_speech.TryDequeue(out var speech)) speech.TrySetResult(false);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ModuleRuntime(IJSRuntime browser, ControlledModule module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "lupTheme.get") return ValueTask.FromResult((TValue)(object)module.Theme);
            if (identifier == "lupTheme.set")
            {
                module.BrowserCalls.Enqueue(new(identifier, args?.ToArray() ?? []));
                module.Theme = Assert.IsType<string>(args![0]);
            }
            if (identifier != "import") return browser.InvokeAsync<TValue>(identifier, cancellationToken, args);
            module.ImportedScripts.Enqueue(Assert.IsType<string>(args![0]));
            return ValueTask.FromResult((TValue)(object)module);
        }
    }

    private sealed class RouletteApi : HttpMessageHandler
    {
        public Guid HoldId { get; } = Guid.NewGuid();
        public Guid OtherHoldId { get; } = Guid.NewGuid();
        public bool CanManage { get; set; } = true;
        public bool SpeechAvailable { get; set; }
        public bool CueBankAvailable { get; set; }
        public bool FailGroupBatch { get; set; }
        public int MemberCount { get; set; } = 9;
        public Guid CreatedSetId { get; } = Guid.NewGuid();
        public ConcurrentQueue<(HttpMethod Method, string Path)> Requests { get; } = new();
        public ConcurrentQueue<SaveGroupSet> SetRequests { get; } = new();
        public ConcurrentQueue<SaveNotionGroupBatch> GroupBatches { get; } = new();
        private readonly NotionHoldMember[] _members = Enumerable.Range(1, 9)
            .Select(i => new NotionHoldMember(Guid.NewGuid(), $"Elev {i}", [])).ToArray();
        private readonly NotionHoldMember[] _otherMembers = Enumerable.Range(1, 6)
            .Select(i => new NotionHoldMember(Guid.NewGuid(), $"Nyt hold elev {i}", [])).ToArray();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var absolutePath = request.RequestUri!.AbsolutePath;
            var path = absolutePath.StartsWith("/api/", StringComparison.Ordinal) ? absolutePath["/api/".Length..] : absolutePath;
            Requests.Enqueue((request.Method, path));
            if (request.Method == HttpMethod.Get)
            {
                if (path == RouletteNameCatalog.ManifestPath)
                    return SpeechAvailable ? Response(VoiceManifest()) : new(HttpStatusCode.NotFound);
                if (path == RouletteAnnouncementCatalog.ManifestPath)
                    return CueBankAvailable ? Response(AnnouncementManifest()) : new(HttpStatusCode.NotFound);
                if (path == "notion/holds") return Response(new[] {
                    new NotionHold(HoldId, "Testholdet", CanManage), new NotionHold(OtherHoldId, "Det nye hold", CanManage) });
                if (path == $"notion/holds/{HoldId}/members") return Response(_members.Take(MemberCount).ToArray());
                if (path == $"notion/holds/{OtherHoldId}/members") return Response(_otherMembers);
                if (path == $"notion/holds/{HoldId}/groups" || path == $"notion/holds/{OtherHoldId}/groups")
                    return Response(Array.Empty<NotionGroupView>());
                if (path == $"holds/{HoldId}/group-sets" || path == $"holds/{OtherHoldId}/group-sets")
                    return Response(Array.Empty<GroupSetView>());
            }
            if (request.Method == HttpMethod.Post && path == $"holds/{HoldId}/group-sets")
            {
                var body = (await request.Content!.ReadFromJsonAsync<SaveGroupSet>(cancellationToken))!;
                SetRequests.Enqueue(body);
                return Response(new GroupSetView(CreatedSetId, HoldId, body.Name, false, 0, 0, 0));
            }
            if (request.Method == HttpMethod.Post && path == $"notion/holds/{HoldId}/groups/batch")
            {
                var body = (await request.Content!.ReadFromJsonAsync<SaveNotionGroupBatch>(cancellationToken))!;
                GroupBatches.Enqueue(body);
                if (FailGroupBatch) return new(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "try_again" }) };
                return Response(body.Groups.Select(group => new NotionGroupView(Guid.NewGuid(), HoldId, group.Name, 0,
                    group.MembershipIds.Select(id => new NotionMember(id, _members.Single(member => member.Id == id).DisplayName)).ToArray(),
                    body.GroupSetId)).ToArray());
            }
            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        }

        private static HttpResponseMessage Response<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

        private static object VoiceManifest() => new
        {
            version = 1, complete = true, provider = "Røst", style = "Arenaspeaker", sampleRate = 24000,
            clips = RouletteNameCatalog.Starts.Select((text, index) => new { id = $"prefix-{index:00}", text, file = $"prefix-{index:00}.wav" })
                .Concat(RouletteNameCatalog.Ends.Select((text, index) => new { id = $"suffix-{index:00}", text, file = $"suffix-{index:00}.wav" })).ToArray()
        };

        private static object AnnouncementManifest() => new
        {
            version = 1, complete = true, provider = "Røst", style = "Arenaspeaker", sampleRate = 24000,
            clips = RouletteAnnouncementCatalog.All.Select(cue => new { id = cue.Id, text = cue.Text, file = cue.File }).ToArray()
        };
    }
}
