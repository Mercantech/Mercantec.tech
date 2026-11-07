using Mercantec.Tech.Contracts;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Courses;

public partial class Roulette
{
    private string _announcementMode = "full";
    private bool _announcementsAvailable, _reelsRolling, _cheering;
    private bool FullAnnouncements => _announcementMode == "full" && _announcementsAvailable;
    private bool CanCheer => MayManage && _module is not null && !_spinning && !_speaking && !_saving && !_testingVoice && !_autoSpin && !_cheering;

    private async Task<bool> PlayCue(string id, int generation, bool respectMode = true)
    {
        if (!_speechEnabled || _module is null || !CurrentContext(generation) || respectMode && !FullAnnouncements) return false;
        var cue = RouletteAnnouncementCatalog.All.FirstOrDefault(cue => cue.Id == id);
        if (cue is null) return false;
        var voiceMode = _voiceMode;
        _speaking = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            bool played;
            if (voiceMode == "rost")
            {
                if (!_announcementsAvailable) return false;
                played = await _module.InvokeAsync<bool>("playAnnouncement", _scope.Token, _root,
                    new[] { RouletteAnnouncementCatalog.GetClipUrl(id)! }, AudioVolume);
            }
            else played = await _module.InvokeAsync<bool>("playBrowserSpeech", _scope.Token, _root, cue.Text, AudioVolume);
            if (!CurrentContext(generation) || !_speechEnabled || _voiceMode != voiceMode) return false;
            if (played) _voiceMessage = voiceMode == "rost" ? "Opråbet er afspillet af arenaspeakeren." : "Opråbet er læst op med browserstemmen.";
            return played;
        }
        catch (Exception exception) when (exception is JSException or OperationCanceledException)
        {
            return false;
        }
        finally
        {
            if (CurrentContext(generation)) { _speaking = false; await InvokeAsync(StateHasChanged); }
        }
    }

    private async Task<bool> TryPresentWinner(string groupName, string celebration, int generation)
    {
        if (!FullAnnouncements || _module is null || !_speechEnabled || !CurrentContext(generation)) return false;
        var voiceMode = _voiceMode;
        bool played;
        if (voiceMode == "rost")
        {
            var name = Speech.Prepare(groupName);
            var intro = RouletteAnnouncementCatalog.GetClipUrl("intro-group");
            var finish = RouletteAnnouncementCatalog.GetClipUrl(celebration);
            if (name is null || intro is null || finish is null) return false;
            var clips = new[] { intro, name.ClipUrls[0], name.ClipUrls[1], finish };
            played = await _module.InvokeAsync<bool>("playAnnouncement", _scope.Token, _root, clips, AudioVolume);
        }
        else
        {
            var finish = RouletteAnnouncementCatalog.All.FirstOrDefault(cue => cue.Id == celebration)?.Text ?? "";
            played = await _module.InvokeAsync<bool>("playBrowserSpeech", _scope.Token, _root,
                $"Giv en kæmpe hånd til {SpokenName(groupName)}! {finish}", AudioVolume);
        }
        if (!CurrentContext(generation) || !_speechEnabled || voiceMode != _voiceMode) return true;
        if (played) _voiceMessage = voiceMode == "rost" ? "Gruppen er præsenteret af arenaspeakeren." : "Gruppen er præsenteret med browserstemmen.";
        return played;
    }

    private async Task AnnouncementModeChanged()
    {
        _announcementMode = _announcementMode == "names" ? "names" : "full";
        CancelVoicePreparation();
        if (_module is not null) await _module.InvokeVoidAsync("cancelSpeech", _root);
        _voiceMessage = _announcementMode == "names" ? "Kun gruppenavnene læses op."
            : _announcementsAvailable ? "Arenaspeakeren tager hele showet." : "Gruppenavnene læses op. De ekstra opråb er ikke klar endnu.";
    }

    private Task Cheer() => PlayFunCue("celebration-05", true);
    private Task Drumroll() => PlayFunCue("drumroll", false);
    private async Task PlayFunCue(string cue, bool celebration)
    {
        if (!CanCheer) return;
        _cheering = true;
        var generation = _generation;
        await InvokeAsync(StateHasChanged);
        try
        {
            if (celebration && _module is not null)
                await _module.InvokeAsync<bool>("cheer", _scope.Token, _root, _sound, AudioVolume, new { motion = _animate });
            await PlayCue(cue, generation, false);
        }
        catch (Exception exception) when (exception is JSException or OperationCanceledException) { }
        finally
        {
            if (CurrentContext(generation)) { _cheering = false; await InvokeAsync(StateHasChanged); }
        }
    }
}
