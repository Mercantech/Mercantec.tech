using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Wordle;

internal static class WordleRules
{
    internal const int MaxAttempts = 6;
    internal static readonly DateOnly Start = new(2026, 11, 14);
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    internal static DateOnly LocalDate(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Copenhagen).DateTime);

    internal static DateTimeOffset NextRound(DateOnly date)
    {
        var midnight = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, Copenhagen.GetUtcOffset(midnight));
    }

    internal static WordleAnswer ScheduledAnswer(WordleLexicon lexicon, byte[] key, int dayNumber)
    {
        var answers = lexicon.HostAnswers(dayNumber, out var occurrence);
        var cycle = occurrence / answers.Length;
        var cycleKey = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(cycle.ToString(CultureInfo.InvariantCulture)));
        return answers.OrderBy(answer => Convert.ToHexString(HMACSHA256.HashData(cycleKey,
                Encoding.UTF8.GetBytes(answer.Host + ":" + answer.Word))), StringComparer.Ordinal)
            .ElementAt(occurrence % answers.Length);
    }

    internal static WordleMark[] Grade(string answer, string guess)
    {
        var marks = new WordleMark[5];
        var remaining = new Dictionary<char, int>();
        for (var index = 0; index < answer.Length; index++)
        {
            if (answer[index] == guess[index])
                marks[index] = WordleMark.Correct;
            else
                remaining[answer[index]] = remaining.GetValueOrDefault(answer[index]) + 1;
        }
        for (var index = 0; index < guess.Length; index++)
        {
            if (marks[index] == WordleMark.Correct || remaining.GetValueOrDefault(guess[index]) == 0)
                continue;
            marks[index] = WordleMark.Present;
            remaining[guess[index]]--;
        }
        return marks;
    }

    internal static (int Current, int Best) Streaks(IEnumerable<DateOnly> ascendingDates, DateOnly today)
    {
        var last = DateOnly.MinValue;
        var current = 0;
        var best = 0;
        foreach (var date in ascendingDates)
        {
            current = date.DayNumber == last.DayNumber + 1 ? current + 1 : 1;
            best = Math.Max(best, current);
            last = date;
        }
        return (today.DayNumber - last.DayNumber <= 1 ? current : 0, best);
    }
}
