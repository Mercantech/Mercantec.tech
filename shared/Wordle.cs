namespace Mercantec.Tech.Contracts;

public enum WordleStatus { Playing, Won, Lost }

public enum WordleMark { Absent, Present, Correct }

public sealed record WordleDay(DateOnly Date, DateTimeOffset NextRoundAt, string HostSlug, string Theme);

public sealed record WordleGuess(string Word, IReadOnlyList<WordleMark> Marks);

public sealed record WordleGuessRequest(DateOnly Date, int Attempt, string Word);

public sealed record WordleRound(
    WordleDay Day,
    IReadOnlyList<WordleGuess> Guesses,
    WordleStatus Status,
    int Streak,
    int BestStreak,
    string? Answer,
    string? Explanation,
    string? Error = null);
