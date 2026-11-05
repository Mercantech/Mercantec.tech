namespace Mercantec.Tech.Contracts;

/// <summary>Creates equally sized groups; every participant occurs exactly once in a round.</summary>
public static class GroupRoulette
{
    public static Guid[][] Draw(Guid[] participants, int value, bool byGroupSize,
        IReadOnlyList<Guid[]>? previousGroups = null, Random? random = null)
    {
        if (participants is null || participants.Length is < 1 or > 250
            || participants.Distinct().Count() != participants.Length || value is < 1 or > 250)
            throw new ArgumentException("Vælg mellem 1 og 250 forskellige elever og et positivt antal.");
        var count = byGroupSize ? (participants.Length + value - 1) / value : value;
        if (count < 1 || count > participants.Length || count > 100)
            throw new ArgumentException("Antal grupper skal være mellem 1 og antal valgte elever, højst 100.");

        random ??= Random.Shared;
        var forbidden = new HashSet<(Guid, Guid)>();
        foreach (var group in previousGroups ?? [])
            for (var i = 0; i < group.Length; i++)
                for (var j = i + 1; j < group.Length; j++) forbidden.Add(Pair(group[i], group[j]));
        for (var attempt = 0; attempt < (forbidden.Count == 0 ? 1 : 750); attempt++)
        {
            var shuffled = participants.ToArray();
            random.Shuffle(shuffled);
            var groups = Enumerable.Range(0, count).Select(_ => new List<Guid>()).ToArray();
            for (var i = 0; i < shuffled.Length; i++) groups[i % count].Add(shuffled[i]);
            if (groups.All(group => !group.SelectMany((a, i) => group.Skip(i + 1).Select(b => Pair(a, b))).Any(forbidden.Contains)))
                return groups.Select(g => g.ToArray()).ToArray();
        }
        throw new InvalidOperationException("Der blev ikke fundet en fordeling uden tidligere makkerpar. Prøv et andet antal grupper, vælg andre elever, eller slå hensynet fra.");
    }

    private static (Guid, Guid) Pair(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);
}
