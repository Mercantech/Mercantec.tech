using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class GroupRouletteTests
{
    [Theory]
    [InlineData(23, 4, true, 6)]
    [InlineData(23, 4, false, 4)]
    [InlineData(1, 3, true, 1)]
    [InlineData(12, 3, true, 4)]
    public void DrawUsesEveryPupilOnceAndBalancesGroupSizes(int pupils, int value, bool bySize, int count)
    {
        var ids = Enumerable.Range(0, pupils).Select(_ => Guid.NewGuid()).ToArray();
        var result = GroupRoulette.Draw(ids, value, bySize, random: new(17));
        Assert.Equal(count, result.Length);
        Assert.All(result, group => Assert.NotEmpty(group));
        Assert.Equal(ids.Order(), result.SelectMany(x => x).Order());
        Assert.InRange(result.Max(x => x.Length) - result.Min(x => x.Length), 0, 1);
        if (bySize) Assert.All(result, group => Assert.True(group.Length <= value));
    }

    [Fact]
    public void AvoidPairsUsesPreviousRoundsAndReportsSearchFailure()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        Guid[][] previous = [[ids[0], ids[1]], [ids[2], ids[3]]];
        var result = GroupRoulette.Draw(ids, 2, false, previous, new(3));
        Assert.All(result, group => Assert.False(previous.Any(old => group.All(old.Contains))));
        Assert.Throws<InvalidOperationException>(() => GroupRoulette.Draw(ids, 1, false, previous, new(3)));
    }

    [Fact]
    public void InvalidDrawCannotCreateEmptyOrDuplicateGroups()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => GroupRoulette.Draw([], 3, true));
        Assert.Throws<ArgumentException>(() => GroupRoulette.Draw([id, id], 1, false));
        Assert.Throws<ArgumentException>(() => GroupRoulette.Draw([id], 2, false));
        Assert.Throws<ArgumentException>(() => GroupRoulette.Draw([id], 0, true));
        Assert.Throws<ArgumentException>(() => GroupRoulette.Draw([id], int.MaxValue, true));
    }
}
