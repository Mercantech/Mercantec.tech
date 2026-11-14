using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Data.Entities;

public sealed class WordleSchedule
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
}

public sealed class WordlePuzzle
{
    public DateOnly Date { get; set; }
    public string HostSlug { get; set; } = "";
    public string Theme { get; set; } = "";
    public string Answer { get; set; } = "";
    public string Explanation { get; set; } = "";
}

public sealed class WordlePlay
{
    public Guid UserSub { get; set; }
    public DateOnly Date { get; set; }
    public string GuessesJson { get; set; } = "[]";
    public WordleStatus Status { get; set; }
    public int Attempts { get; set; }
}
