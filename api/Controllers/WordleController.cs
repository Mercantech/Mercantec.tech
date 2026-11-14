using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Wordle;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/games/wordle")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WordleController(AppDbContext db, WordleLexicon lexicon, TimeProvider clock, IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous, HttpGet("daily")]
    public async Task<IActionResult> Daily(CancellationToken ct)
    {
        var today = WordleRules.LocalDate(clock.GetUtcNow());
        if (today < WordleRules.Start) return NotStarted();
        return Ok(ToDay(await GetPuzzle(today, ct)));
    }

    [HttpGet("round")]
    public async Task<IActionResult> Round(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject) return Unauthorized();
        var today = WordleRules.LocalDate(clock.GetUtcNow());
        if (today < WordleRules.Start) return NotStarted();
        var puzzle = await GetPuzzle(today, ct);
        return Ok(await ToRound(subject, puzzle, await GetPlay(subject, today, ct), ct));
    }

    [HttpPost("guess")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Guess(WordleGuessRequest request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject) return Unauthorized();
        if (!Guid.TryParse(Request.Headers["X-LUP-Wordle-Subject"], out var expected) || expected != subject)
            return Conflict(new { error = "account_changed" });
        var today = WordleRules.LocalDate(clock.GetUtcNow());
        if (today < WordleRules.Start) return NotStarted();
        var puzzle = await GetPuzzle(today, ct);
        var play = await GetPlay(subject, today, ct, track: true);
        if (request.Date != today)
            return Conflict(await ToRound(subject, puzzle, play, ct, "day_changed"));

        var word = WordleLexicon.Normalize(request.Word);
        if (request.Attempt != (play?.Attempts ?? 0))
            return await RetryResult(subject, puzzle, play, request.Attempt, word, ct);
        if (play is { Status: not WordleStatus.Playing })
            return Ok(await ToRound(subject, puzzle, play, ct));
        if (word is null)
            return UnprocessableEntity(await ToRound(subject, puzzle, play, ct, "word_length"));
        if (!lexicon.Allows(word) && word != puzzle.Answer)
            return UnprocessableEntity(await ToRound(subject, puzzle, play, ct, "word_unknown"));

        var words = Guesses(play);
        words.Add(word);
        if (play is null)
        {
            play = new WordlePlay { UserSub = subject, Date = today };
            db.WordlePlays.Add(play);
        }
        play.GuessesJson = JsonSerializer.Serialize(words);
        play.Attempts = words.Count;
        play.Status = word == puzzle.Answer ? WordleStatus.Won
            : words.Count == WordleRules.MaxAttempts ? WordleStatus.Lost : WordleStatus.Playing;

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(play).State = EntityState.Detached;
            var winner = await GetPlay(subject, today, ct);
            if (winner is null || winner.Attempts <= request.Attempt) throw;
            return await RetryResult(subject, puzzle, winner, request.Attempt, word, ct);
        }

        return Ok(await ToRound(subject, puzzle, play, ct));
    }

    private async Task<IActionResult> RetryResult(Guid subject, WordlePuzzle puzzle, WordlePlay? play,
        int requestedAttempt, string? word, CancellationToken ct)
    {
        var words = Guesses(play);
        if (requestedAttempt >= 0 && requestedAttempt < words.Count && words[requestedAttempt] == word)
            return Ok(await ToRound(subject, puzzle, play, ct));
        return Conflict(await ToRound(subject, puzzle, play, ct, "round_changed"));
    }

    private async Task<WordlePuzzle> GetPuzzle(DateOnly today, CancellationToken ct)
    {
        var existing = await FindPuzzle(today, ct);
        if (existing is not null) return existing;

        var dayNumber = today.DayNumber - WordleRules.Start.DayNumber;
        var entry = WordleRules.ScheduledAnswer(lexicon, await GetKey(ct), dayNumber);
        var puzzle = new WordlePuzzle
        {
            Date = today, HostSlug = entry.Host, Theme = WordleLexicon.Theme(entry.Host),
            Answer = entry.Word, Explanation = entry.Explanation
        };
        db.WordlePuzzles.Add(puzzle);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(puzzle).State = EntityState.Detached;
            var winner = await FindPuzzle(today, ct);
            if (winner is null) throw;
            return winner;
        }
        return puzzle;
    }

    private async Task<byte[]> GetKey(CancellationToken ct)
    {
        var existing = await FindKey(ct);
        if (existing is not null) return Convert.FromHexString(existing);

        var configuredKey = configuration["Wordle:ScheduleKey"];
        var key = string.IsNullOrWhiteSpace(configuredKey) ? RandomNumberGenerator.GetBytes(32)
            : SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        var schedule = new WordleSchedule { Id = 1, Key = Convert.ToHexString(key) };
        db.WordleSchedules.Add(schedule);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(schedule).State = EntityState.Detached;
            var winner = await FindKey(ct);
            if (winner is null) throw;
            return Convert.FromHexString(winner);
        }
        return key;
    }

    private Task<string?> FindKey(CancellationToken ct) => db.WordleSchedules.AsNoTracking()
        .Where(schedule => schedule.Id == 1).Select(schedule => schedule.Key).SingleOrDefaultAsync(ct);

    private Task<WordlePuzzle?> FindPuzzle(DateOnly date, CancellationToken ct) => db.WordlePuzzles.AsNoTracking()
        .SingleOrDefaultAsync(puzzle => puzzle.Date == date, ct);

    private Task<WordlePlay?> GetPlay(Guid subject, DateOnly date, CancellationToken ct, bool track = false) =>
        (track ? db.WordlePlays.AsTracking() : db.WordlePlays.AsNoTracking())
            .SingleOrDefaultAsync(play => play.UserSub == subject && play.Date == date, ct);

    private async Task<WordleRound> ToRound(Guid subject, WordlePuzzle puzzle, WordlePlay? play,
        CancellationToken ct, string? error = null)
    {
        var completedDates = await db.WordlePlays.AsNoTracking()
            .Where(row => row.UserSub == subject && row.Status != WordleStatus.Playing && row.Date <= puzzle.Date)
            .OrderBy(row => row.Date).Select(row => row.Date).ToArrayAsync(ct);
        var streaks = WordleRules.Streaks(completedDates, puzzle.Date);
        var guesses = Guesses(play).Select(word => new WordleGuess(word, WordleRules.Grade(puzzle.Answer, word))).ToArray();
        var status = play?.Status ?? WordleStatus.Playing;
        return new WordleRound(ToDay(puzzle), guesses, status, streaks.Current, streaks.Best,
            status == WordleStatus.Playing ? null : puzzle.Answer,
            status == WordleStatus.Playing ? null : puzzle.Explanation, error);
    }

    private static List<string> Guesses(WordlePlay? play) => play is null ? [] :
        JsonSerializer.Deserialize<List<string>>(play.GuessesJson) ?? [];

    private static WordleDay ToDay(WordlePuzzle puzzle) => new(puzzle.Date, WordleRules.NextRound(puzzle.Date), puzzle.HostSlug, puzzle.Theme);

    private IActionResult NotStarted() => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "wordle_not_started" });
}
