using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Characters;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages;

public partial class Wordle
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly string[] Keyboard = ["QWERTYUIOPÅ", "ASDFGHJKLÆØ", "ZXCVBNM"];
    private readonly CancellationTokenSource _lifetime = new();
    private WordleDay? _day;
    private WordleRound? _round;
    private ElementReference _entry;
    private string? _subject;
    private string? _error;
    private string? _feedback;
    private string _input = "";
    private int _generation;
    private bool _loginRequired;
    private bool _submitting;
    private bool _playedBefore;
    private bool _focusEntry;
    private bool _disposed;

    private bool CanPlay => Auth.IsAuthenticated && !_loginRequired;
    private CharacterProfile? Host => _day is null ? null : CharacterCatalog.Find(_day.HostSlug);
    private string NextRoundLabel => _day?.NextRoundAt.ToString("'den' d. MMMM 'kl.' HH.mm", Danish) ?? "";

    protected override async Task OnInitializedAsync()
    {
        _subject = Auth.Subject;
        Auth.AuthStateChanged += OnAuthChanged;
        await LoadAsync();
        if (!_disposed) _ = WatchDayAsync(_lifetime.Token);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusEntry) return;
        _focusEntry = false;
        if (_round?.Status == WordleStatus.Playing)
            await _entry.FocusAsync(preventScroll: true);
    }

    private async Task LoadAsync()
    {
        var generation = ++_generation;
        var subject = Auth.Subject;
        var ct = _lifetime.Token;
        _error = null;
        try
        {
            if (CanPlay)
            {
                var result = await Games.GetRoundAsync(ct);
                if (generation == _generation && !_disposed && subject == Auth.Subject)
                {
                    _focusEntry = _round is null;
                    SetRound(result);
                }
            }
            else
            {
                var day = await Games.GetDayAsync(ct);
                if (generation == _generation && !_disposed) _day = day;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            if (generation != _generation || _disposed) return;
            if (ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } && CanPlay)
            {
                RequireLogin();
                await LoadAsync();
            }
            else
                _error = "Dagens Wordle kunne ikke hentes. Prøv igen.";
        }
    }

    private async Task WatchDayAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var untilChange = _day?.NextRoundAt - DateTimeOffset.UtcNow;
                var delay = untilChange is { TotalSeconds: > 0 and < 60 }
                    ? untilChange.Value + TimeSpan.FromMilliseconds(250)
                    : TimeSpan.FromMinutes(1);
                await Task.Delay(delay, ct);
                await InvokeAsync(async () =>
                {
                    if (_disposed || _submitting || _day is null || _day.NextRoundAt > DateTimeOffset.UtcNow)
                        return;
                    await LoadAsync();
                    if (!_disposed) StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private void OnAuthChanged()
    {
        if (_disposed || _subject == Auth.Subject) return;
        _subject = Auth.Subject;
        _ = InvokeAsync(async () =>
        {
            if (_disposed) return;
            _round = null;
            _submitting = false;
            _loginRequired = false;
            _playedBefore = false;
            _input = "";
            _feedback = null;
            StateHasChanged();
            await LoadAsync();
            if (!_disposed) StateHasChanged();
        });
    }

    private async Task SubmitAsync()
    {
        if (!CanPlay || _submitting || _input.Length != 5 || Auth.Subject is not { } subject
            || _round is not { Status: WordleStatus.Playing } round)
            return;

        var generation = _generation;
        var ct = _lifetime.Token;
        var guess = _input;
        _submitting = true;
        _error = null;
        _feedback = null;
        try
        {
            var result = await Games.GuessAsync(new(round.Day.Date, round.Guesses.Count, guess), subject, ct);
            if (_disposed || generation != _generation || subject != Auth.Subject) return;

            SetRound(result);
            _error = result.Error switch
            {
                "word_length" => "Gættet skal have fem bogstaver.",
                "word_unknown" => "Ordet er ikke i ordlisten. Du har stadig samme antal forsøg.",
                "round_changed" => "Runden blev opdateret fra en anden fane eller enhed.",
                "day_changed" => "En ny dag er begyndt. Her er dagens runde.",
                null => null,
                _ => "Gættet kunne ikke gemmes. Prøv igen."
            };
            if (result.Error is null && result.Guesses.Count > round.Guesses.Count)
                _feedback = Describe(result.Guesses[^1]);
            if (result.Error is not ("word_length" or "word_unknown"))
                _input = "";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            if (_disposed || generation != _generation || subject != Auth.Subject) return;
            if (ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
                RequireLogin();
            else
                _error = "Gættet kunne ikke bekræftes. Prøv igen med samme ord.";
        }
        finally
        {
            if (!_disposed && generation == _generation)
            {
                _submitting = false;
                _focusEntry = true;
            }
        }
    }

    private void RequireLogin()
    {
        _loginRequired = true;
        _round = null;
        _input = "";
        _feedback = null;
    }

    private void SetRound(WordleRound result)
    {
        if (_day?.Date != result.Day.Date)
        {
            _input = "";
            _feedback = null;
        }
        _day = result.Day;
        _round = result;
        _playedBefore |= result.Guesses.Count > 0 || result.BestStreak > 0;
    }

    private void SetInput(string? value)
    {
        _input = new string((value ?? "").Normalize(NormalizationForm.FormC)
            .ToUpperInvariant().Where(IsLetter).Take(5).ToArray());
        ClearGuessFeedback();
    }

    private void AddLetter(char letter)
    {
        if (_submitting || _input.Length >= 5) return;
        _input += letter;
        ClearGuessFeedback();
    }

    private void EraseLetter()
    {
        if (_submitting || _input.Length == 0) return;
        _input = _input[..^1];
        ClearGuessFeedback();
    }

    private void ClearGuessFeedback()
    {
        _error = null;
        _feedback = null;
    }

    private static bool IsLetter(char letter) => letter is >= 'A' and <= 'Z' or 'Æ' or 'Ø' or 'Å';

    private WordleMark? KeyMark(char key)
    {
        WordleMark? strongest = null;
        if (_round is null) return strongest;
        foreach (var guess in _round.Guesses)
            for (var i = 0; i < guess.Word.Length; i++)
                if (char.ToUpperInvariant(guess.Word[i]) == key && (strongest is null || guess.Marks[i] > strongest))
                    strongest = guess.Marks[i];

        return strongest;
    }

    private static string MarkClass(WordleMark? mark) => mark switch
    {
        WordleMark.Correct => "mark-correct",
        WordleMark.Present => "mark-present",
        WordleMark.Absent => "mark-absent",
        _ => ""
    };

    private static string MarkSymbol(WordleMark mark) => mark switch
    {
        WordleMark.Correct => "✓",
        WordleMark.Present => "↔",
        _ => "×"
    };

    private static string MarkLabel(WordleMark mark) => mark switch
    {
        WordleMark.Correct => "rigtig plads",
        WordleMark.Present => "forkert plads",
        _ => "ingen plads"
    };

    private static string Describe(WordleGuess guess) => string.Join(", ",
        guess.Word.Select((letter, index) => $"{char.ToUpperInvariant(letter)}: {MarkLabel(guess.Marks[index])}")) + ".";

    private static string DaysLabel(int days) => days == 1 ? "1 dag" : $"{days} dage";

    public void Dispose()
    {
        _disposed = true;
        Auth.AuthStateChanged -= OnAuthChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
