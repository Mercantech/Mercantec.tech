using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Wordle;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Mercantec.Tech.Api.Tests;

public sealed class WordleTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly Guid pupil = Guid.NewGuid();
    private readonly TestClock clock = new(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero));
    private readonly WordleLexicon dictionary = new([new("lupp", "level", "En bane i et spil.")], ["level", "tabel", "vælge", "koder"]);

    public async Task InitializeAsync()
    {
        SQLitePCL.Batteries_V2.Init();
        await connection.OpenAsync();
        await using var db = Open();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await connection.DisposeAsync();

    [Fact]
    public void RealCatalogHas960DistinctAnswersAndRotatesEveryPublishedHost()
    {
        var lexicon = WordleLexicon.Load();
        var key = new byte[32];
        var first = Enumerable.Range(0, 960).Select(day => WordleRules.ScheduledAnswer(lexicon, key, day)).ToArray();
        Assert.Equal(960, first.Length);
        Assert.Equal(960, first.Select(answer => answer.Word).Distinct().Count());
        Assert.Equal(WordleLexicon.HostOrder, first.Take(11).Select(answer => answer.Host));
        Assert.Equal(WordleLexicon.HostOrder, first.Skip(11).Take(11).Select(answer => answer.Host));
        Assert.All(first, answer => Assert.True(lexicon.Allows(answer.Word)));
        Assert.Equal(first, Enumerable.Range(0, 960).Select(day => WordleRules.ScheduledAnswer(lexicon, key, day)));
        Assert.Equal("raymond", WordleRules.ScheduledAnswer(lexicon, key, 960).Host);
        var alternateKey = Enumerable.Repeat((byte)1, 32).ToArray();
        Assert.Contains(Enumerable.Range(0, 11), day => first[day] != WordleRules.ScheduledAnswer(lexicon, alternateKey, day));
    }

    [Fact]
    public void MarksReserveExactLettersBeforeConsumingDuplicatesAndNormalizeDanish()
    {
        Assert.Equal([WordleMark.Absent, WordleMark.Present, WordleMark.Absent, WordleMark.Correct, WordleMark.Correct],
            WordleRules.Grade("level", "allel"));
        Assert.Equal("vågne", WordleLexicon.Normalize("  VA\u030aGNE  "));
        Assert.Null(WordleLexicon.Normalize("caféx"));
        Assert.Null(WordleLexicon.Normalize("kode!"));
    }

    [Fact]
    public async Task DailySharesOnlyMetadataAndGuessesAreValidatedPrivateAndIdempotent()
    {
        await using var db = Open();
        var player = Controller(db, pupil);
        var daily = Assert.IsType<WordleDay>(Assert.IsType<OkObjectResult>(await player.Daily(default)).Value);
        Assert.Equal(WordleRules.Start, daily.Date);
        Assert.DoesNotContain("level", JsonSerializer.Serialize(daily));
        Assert.Equal("lupp", daily.HostSlug);
        var playing = Ok(await player.Round(default));
        Assert.Empty(playing.Guesses);
        Assert.Null(playing.Answer);

        var invalid = Assert.IsType<UnprocessableEntityObjectResult>(await player.Guess(new(daily.Date, 0, "kode!"), default));
        Assert.Equal("word_length", Assert.IsType<WordleRound>(invalid.Value).Error);
        var unknown = Assert.IsType<UnprocessableEntityObjectResult>(await player.Guess(new(daily.Date, 0, "abcde"), default));
        Assert.Equal("word_unknown", Assert.IsType<WordleRound>(unknown.Value).Error);
        Assert.Empty(db.WordlePlays);

        var first = Ok(await player.Guess(new(daily.Date, 0, "TABEL"), default));
        Assert.Single(first.Guesses);
        Assert.Null(first.Explanation);
        Assert.Single(Ok(await player.Guess(new(daily.Date, 0, "tabel"), default)).Guesses);
        Assert.Equal("round_changed", Assert.IsType<WordleRound>(
            Assert.IsType<ConflictObjectResult>(await player.Guess(new(daily.Date, 0, "koder"), default)).Value).Error);

        var other = Controller(db, Guid.NewGuid());
        Assert.Empty(Ok(await other.Round(default)).Guesses);
        other.Request.Headers["X-LUP-Wordle-Subject"] = pupil.ToString();
        var mismatch = Assert.IsType<ConflictObjectResult>(await other.Guess(new(daily.Date, 0, "level"), default));
        Assert.Equal("account_changed", JsonSerializer.SerializeToElement(mismatch.Value).GetProperty("error").GetString());

        var won = Ok(await player.Guess(new(daily.Date, 1, "level"), default));
        Assert.Equal(WordleStatus.Won, won.Status);
        Assert.Equal("level", won.Answer);
        Assert.Equal("En bane i et spil.", won.Explanation);
        Assert.Equal(1, won.Streak);
        Assert.Equal(1, won.BestStreak);
        Assert.Equal(2, Ok(await player.Guess(new(daily.Date, 2, "vælge"), default)).Guesses.Count);
        Assert.Equal(2, db.WordlePlays.Single().Attempts);
    }

    [Fact]
    public async Task MidnightRejectsOldAttemptAndCompletedLossExtendsStreak()
    {
        await using var db = Open();
        var player = Controller(db, pupil);
        var startingDate = WordleRules.Start;
        await player.Guess(new(startingDate, 0, "level"), default);
        clock.Now = new DateTimeOffset(2026, 11, 14, 23, 0, 0, TimeSpan.Zero);
        var stale = Assert.IsType<ConflictObjectResult>(await player.Guess(new(startingDate, 0, "level"), default));
        var newRound = Assert.IsType<WordleRound>(stale.Value);
        Assert.Empty(newRound.Guesses);
        Assert.Equal("day_changed", newRound.Error);
        Assert.Equal(startingDate.AddDays(1), newRound.Day.Date);
        Assert.Equal(1, newRound.Streak);
        for (var attempt = 0; attempt < 6; attempt++)
            newRound = Ok(await player.Guess(new(newRound.Day.Date, attempt, "koder"), default));
        Assert.Equal(WordleStatus.Lost, newRound.Status);
        Assert.Equal("level", newRound.Answer);
        Assert.Equal(2, newRound.Streak);

        clock.Now = clock.Now.AddDays(2);
        newRound = Ok(await player.Round(default));
        Assert.Equal(0, newRound.Streak);
        Assert.Equal(2, newRound.BestStreak);
        Assert.Equal(TimeSpan.FromHours(1), newRound.Day.NextRoundAt.Offset);
        Assert.Equal(TimeSpan.FromHours(2), WordleRules.NextRound(new DateOnly(2027, 3, 28)).Offset);
    }

    [Fact]
    public async Task ScheduleIsPersistedAndPrivacyExportOnlyContainsOwnCompletedOrActiveGuesses()
    {
        await using var db = Open();
        var player = Controller(db, pupil);
        await player.Guess(new(WordleRules.Start, 0, "koder"), default);
        await Controller(db, Guid.NewGuid()).Guess(new(WordleRules.Start, 0, "tabel"), default);
        var originalKey = db.WordleSchedules.Single().Key;
        var changedConfiguration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Wordle:ScheduleKey"] = "another-key" }).Build();
        await using var reloaded = Open();
        var next = Controller(reloaded, pupil, changedConfiguration);
        Assert.Single(Ok(await next.Round(default)).Guesses);
        clock.Now = clock.Now.AddDays(1);
        await next.Daily(default);
        Assert.Equal(originalKey, reloaded.WordleSchedules.Single().Key);

        var privacy = new PrivacyController(db) { ControllerContext = player.ControllerContext };
        var file = Assert.IsType<FileContentResult>(await privacy.Export(default));
        using var document = JsonDocument.Parse(file.FileContents);
        var plays = document.RootElement.GetProperty("PlatformRecords").GetProperty("wordlePlays");
        Assert.Equal("koder", Assert.Single(plays.EnumerateArray()).GetProperty("guesses")[0].GetString());
        Assert.DoesNotContain(originalKey, document.RootElement.GetRawText());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task SimultaneousGuessesDoNotConsumeTwoAttempts(int prefilledAttempts)
    {
        var connectionString = Environment.GetEnvironmentVariable("LUP_WORDLE_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await using var sqlite = new SqliteConnection($"Data Source=wordle-race-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await sqlite.OpenAsync();
            await CheckConcurrentGuesses(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(sqlite.ConnectionString), prefilledAttempts);
            return;
        }
        var postgres = new NpgsqlConnectionStringBuilder(connectionString);
        if (postgres.Host is not ("127.0.0.1" or "localhost") || postgres.Database?.EndsWith("_qa", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Wordle PostgreSQL tests require an isolated local database with a _qa suffix.");
        await CheckConcurrentGuesses(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString), prefilledAttempts);
    }

    private async Task CheckConcurrentGuesses(DbContextOptionsBuilder<AppDbContext> options, int prefilledAttempts)
    {
        var date = new DateOnly(2030, 4, 10);
        clock.Now = new DateTimeOffset(date.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
        await using var setup = new AppDbContext(options.Options);
        if (setup.Database.IsSqlite()) await setup.Database.EnsureCreatedAsync();
        var setupPlayer = Controller(setup, pupil);
        await setupPlayer.Daily(default);
        if (prefilledAttempts > 0)
            Assert.Single(Ok(await setupPlayer.Guess(new(date, 0, "vælge"), default)).Guesses);
        var race = new PlayRace();
        var racingOptions = options.AddInterceptors(race).Options;
        await using var first = new AppDbContext(racingOptions);
        await using var second = new AppDbContext(racingOptions);
        try
        {
            var results = await Task.WhenAll(
                Controller(first, pupil).Guess(new(date, prefilledAttempts, "tabel"), default),
                Controller(second, pupil).Guess(new(date, prefilledAttempts, "koder"), default));
            var winner = Ok(Assert.Single(results.OfType<OkObjectResult>()));
            var loser = Assert.IsType<WordleRound>(Assert.Single(results.OfType<ConflictObjectResult>()).Value);
            Assert.Equal("round_changed", loser.Error);
            Assert.Equal(prefilledAttempts + 1, winner.Guesses.Count);
            Assert.Equal(winner.Guesses.Select(guess => guess.Word), loser.Guesses.Select(guess => guess.Word));
            Assert.Equal(prefilledAttempts + 1, await setup.WordlePlays.Where(play => play.UserSub == pupil)
                .Select(play => play.Attempts).SingleAsync());
        }
        finally
        {
            await setup.WordlePlays.Where(play => play.UserSub == pupil).ExecuteDeleteAsync();
        }
    }

    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private WordleController Controller(AppDbContext db, Guid subject, IConfiguration? configuration = null)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject.ToString())], "test")) };
        context.Request.Headers["X-LUP-Wordle-Subject"] = subject.ToString();
        return new WordleController(db, dictionary, clock, configuration ?? new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static WordleRound Ok(IActionResult result) => Assert.IsType<WordleRound>(Assert.IsType<OkObjectResult>(result).Value);

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class PlayRace : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int ready;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref ready) == 2) gate.TrySetResult();
            await gate.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }
}
