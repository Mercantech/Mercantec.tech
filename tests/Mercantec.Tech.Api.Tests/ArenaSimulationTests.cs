using System.Text.Json;
using Mercantec.Tech.Api.Arena;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class ArenaSimulationTests
{
    private static ArenaSimulation Match(string stage = "rooftops", int stocks = 3, int minutes = 4)
    {
        var simulation = new ArenaSimulation(stage, stocks, minutes);
        simulation.AddFighter("one", "Første", "starter");
        simulation.AddFighter("two", "Anden", "bip01");
        simulation.Start();
        Advance(simulation, 180);
        return simulation;
    }

    private static void Advance(ArenaSimulation simulation, int ticks)
    { for (var i = 0; i < ticks; i++) simulation.Tick(); }

    private static ArenaFighterSnapshot One(ArenaSimulation simulation) => simulation.Snapshot().Fighters[0];
    private static ArenaFighterSnapshot Two(ArenaSimulation simulation) => simulation.Snapshot().Fighters[1];

    [Fact]
    public void CountdownPreventsMovementUntilMatchStarts()
    {
        var simulation = new ArenaSimulation();
        simulation.AddFighter("one", "En", "starter");
        simulation.AddFighter("two", "To", "bip01");
        simulation.Start();
        var x = One(simulation).X;
        simulation.SetInput("one", new(1, Horizontal: 1));
        Advance(simulation, 120);
        Assert.Equal("countdown", simulation.Phase);
        Assert.Equal(x, One(simulation).X);
        Advance(simulation, 60);
        Assert.Equal("playing", simulation.Phase);
    }

    [Fact]
    public void SameInputsProduceExactlyTheSameState()
    {
        var left = Match(); var right = Match();
        for (var i = 0; i < 900; i++)
        {
            var a = new ArenaInput(i, Math.Sin(i / 60d), i % 100 < 20 ? -1 : 0,
                i % 40 == 0, i % 27 == 0, i % 63 == 0, i % 80 < 10,
                i % 97 == 0, i % 170 is > 100 and < 130, i % 125 == 0);
            var b = a with { Horizontal = -a.Horizontal };
            left.SetInput("one", a); right.SetInput("one", a);
            left.SetInput("two", b); right.SetInput("two", b);
            left.Tick(); right.Tick();
        }
        Assert.Equal(JsonSerializer.Serialize(left.Snapshot()), JsonSerializer.Serialize(right.Snapshot()));
    }

    [Fact]
    public void MovementRequiresFreshInputAndRejectsReplayAndNonFiniteValues()
    {
        var simulation = Match();
        var x = One(simulation).X;
        simulation.SetInput("one", new(10, Horizontal: 1));
        Advance(simulation, 10);
        Assert.True(One(simulation).X > x);
        simulation.SetInput("one", new(9, Horizontal: -1));
        Assert.Equal(10, One(simulation).LastInputSequence);
        simulation.SetInput("one", new(11, Horizontal: double.NaN));
        Assert.Equal(10, One(simulation).LastInputSequence);
        Advance(simulation, 65);
        Assert.Equal(0, One(simulation).Vx);
        Assert.True(double.IsFinite(One(simulation).X));
    }

    [Fact]
    public void JumpIsEdgeTriggeredAndOnlyTwoAirJumpsAreAvailable()
    {
        var simulation = Match();
        simulation.SetInput("one", new(1, Jump: true)); simulation.Tick();
        Assert.False(One(simulation).Grounded);
        Assert.Equal(1, One(simulation).JumpsRemaining);
        Advance(simulation, 8);
        Assert.Equal(1, One(simulation).JumpsRemaining);
        simulation.SetInput("one", new(2)); simulation.Tick();
        simulation.SetInput("one", new(3, Jump: true)); simulation.Tick();
        Assert.Equal(0, One(simulation).JumpsRemaining);
        var y = One(simulation).Y;
        simulation.SetInput("one", new(4)); simulation.Tick();
        simulation.SetInput("one", new(5, Jump: true)); simulation.Tick();
        Assert.Equal(0, One(simulation).JumpsRemaining);
        Assert.True(One(simulation).Y < y);
        Advance(simulation, 180);
        Assert.True(One(simulation).Grounded);
        Assert.Equal(2, One(simulation).JumpsRemaining);
    }

    [Fact]
    public void DownJumpCannotFallThroughSolidMainStage()
    {
        var simulation = Match();
        simulation.SetInput("one", new(1, Vertical: 1, Jump: true));
        Advance(simulation, 100);
        Assert.True(One(simulation).Grounded);
        Assert.Equal(565, One(simulation).Y);
        Assert.Equal(3, One(simulation).Stocks);
    }

    [Fact]
    public void DownJumpDropsThroughAnUpperOneWayPlatform()
    {
        var simulation = Match();
        simulation.SetInput("one", new(1, Jump: true)); simulation.Tick();
        Advance(simulation, 8);
        simulation.SetInput("one", new(2)); simulation.Tick();
        simulation.SetInput("one", new(3, Jump: true)); simulation.Tick();
        Advance(simulation, 85);
        Assert.True(One(simulation).Grounded);
        Assert.Equal(415, One(simulation).Y);
        simulation.SetInput("one", new(4, Vertical: 1, Jump: true)); simulation.Tick();
        Assert.False(One(simulation).Grounded);
        Advance(simulation, 60);
        Assert.Equal(565, One(simulation).Y);
        Assert.True(One(simulation).Grounded);
        Assert.Equal(3, One(simulation).Stocks);
    }

    [Fact]
    public void HeldNormalAttackDoesNotRepeatedlyDamageOneTarget()
    {
        var simulation = Match();
        for (var i = 0; i < 35; i++)
        {
            simulation.SetInput("one", new(i, Horizontal: 1));
            simulation.SetInput("two", new(i, Horizontal: -1));
            simulation.Tick();
        }
        for (var i = 35; i < 65; i++)
        {
            simulation.SetInput("one", new(i, Attack: true));
            simulation.SetInput("two", new(i));
            simulation.Tick();
        }
        Assert.InRange(Two(simulation).Damage, 6.9, 7.1);
    }

    [Fact]
    public void GrabThrowsAShieldingOpponent()
    {
        var simulation = Match();
        for (var i = 0; i < 35; i++)
        {
            simulation.SetInput("one", new(i, Horizontal: 1));
            simulation.SetInput("two", new(i, Horizontal: -1));
            simulation.Tick();
        }
        for (var i = 35; i < 48; i++)
        {
            simulation.SetInput("one", new(i, Grab: true));
            simulation.SetInput("two", new(i, Shield: true));
            simulation.Tick();
        }
        Assert.True(Two(simulation).Damage >= 7);
        Assert.Contains(simulation.Snapshot().Events, e => e.Type == "throw" && e.TargetId == "two");
        Assert.False(Two(simulation).Shielding);
    }

    [Fact]
    public void ProjectileDealsDamageAndHitsOnlyOnce()
    {
        var simulation = Match();
        Advance(simulation, 31);
        simulation.SetInput("one", new(1, Special: true));
        Advance(simulation, 50);
        Assert.InRange(Two(simulation).Damage, 6.9, 7.1);
        Assert.Empty(simulation.Snapshot().Projectiles);
        Assert.Contains(simulation.Snapshot().Events, e => e.Type == "hit");
    }

    [Fact]
    public void ShieldBlocksLaunchAndConsumesShieldEnergy()
    {
        var simulation = Match();
        Advance(simulation, 31);
        simulation.SetInput("one", new(1, Special: true));
        for (var i = 0; i < 45; i++)
        {
            simulation.SetInput("two", new(i + 1, Shield: true));
            simulation.Tick();
        }
        Assert.InRange(Two(simulation).Damage, 0, 1);
        Assert.True(Two(simulation).Shield < 90);
        Assert.True(Two(simulation).Grounded);
        Assert.InRange(Two(simulation).X, 840, 845);
    }

    [Fact]
    public void RecoverySpecialLaunchesUpAndCannotBeRepeatedMidair()
    {
        var simulation = Match();
        simulation.SetInput("one", new(1, Jump: true)); simulation.Tick();
        simulation.SetInput("one", new(2, Horizontal: 1, Vertical: -1, Special: true)); simulation.Tick();
        Assert.True(One(simulation).Vy < -700);
        Assert.Equal("ability_4_dash", One(simulation).Action);
        for (var i = 3; i < 35; i++)
        {
            simulation.SetInput("one", new(i, Horizontal: 1)); simulation.Tick();
        }
        var vy = One(simulation).Vy;
        simulation.SetInput("one", new(35, Vertical: -1, Special: true)); simulation.Tick();
        Assert.True(One(simulation).Vy > vy);
    }

    [Fact]
    public void FallingLosesStockRespawnsWithProtectionAndFinishesMatch()
    {
        var simulation = Match(stocks: 1);
        for (var i = 0; i < 450 && simulation.Phase == "playing"; i++)
        {
            simulation.SetInput("one", new(i, Horizontal: -1)); simulation.Tick();
        }
        Assert.Equal("finished", simulation.Phase);
        Assert.Equal("two", simulation.WinnerId);
        Assert.Equal(0, One(simulation).Stocks);
        Assert.Equal(1, One(simulation).Falls);
        Assert.Equal("knockout", One(simulation).Action);
    }

    [Fact]
    public void RespawnRestoresDamageAndGrantsTemporaryInvulnerability()
    {
        var simulation = Match();
        var i = 0;
        for (; i < 450 && One(simulation).Stocks == 3; i++)
        {
            simulation.SetInput("one", new(i, Horizontal: -1)); simulation.Tick();
        }
        simulation.SetInput("one", new(i + 1));
        Advance(simulation, 91);
        Assert.Equal(2, One(simulation).Stocks);
        Assert.Equal(0, One(simulation).Damage);
        Assert.True(One(simulation).Invulnerable);
        Assert.InRange(One(simulation).X, 215, 1065);
    }

    [Fact]
    public void ChargedSmashReleasesAndResetsCharge()
    {
        var simulation = Match();
        for (var i = 0; i < 40; i++)
        { simulation.SetInput("one", new(i, Smash: true)); simulation.Tick(); }
        Assert.InRange(One(simulation).Charge, .5, .6);
        Assert.Equal("ultimate_charge", One(simulation).Action);
        simulation.SetInput("one", new(41)); simulation.Tick();
        Assert.Equal(0, One(simulation).Charge);
        Assert.Equal("ultimate_release", One(simulation).Action);
    }

    [Fact]
    public void TimeoutResolvesTiedPlayersWithSuddenDeath()
    {
        var simulation = Match(minutes: 1);
        Advance(simulation, 3600);
        var state = simulation.Snapshot();
        Assert.True(state.SuddenDeath);
        Assert.Equal("playing", state.Phase);
        Assert.All(state.Fighters, f => { Assert.Equal(1, f.Stocks); Assert.Equal(200, f.Damage); });
    }

    [Theory]
    [InlineData("rooftops")]
    [InlineData("workshop")]
    [InlineData("skyline")]
    public void BotsPlayEveryStageWithoutInvalidState(string stage)
    {
        var simulation = new ArenaSimulation(stage, 3, 1);
        simulation.AddFighter("a", "Robot", "bip01", true);
        simulation.AddFighter("b", "Elefant", "raymond", true);
        simulation.AddFighter("c", "Starter", "starter", true);
        simulation.Start();
        Advance(simulation, 4500);
        var state = simulation.Snapshot();
        Assert.All(state.Fighters, f =>
        {
            Assert.True(double.IsFinite(f.X)); Assert.True(double.IsFinite(f.Y));
            Assert.InRange(f.Damage, 0, 999); Assert.InRange(f.Stocks, 0, 3);
        });
        Assert.True(state.Fighters.Sum(f => f.Falls) > 0 || state.Fighters.Sum(f => f.Damage) > 0);
    }

    [Fact]
    public void RematchResetsStocksScoresAndClock()
    {
        var simulation = Match(stocks: 1);
        for (var i = 0; i < 450 && simulation.Phase == "playing"; i++)
        { simulation.SetInput("one", new(i, Horizontal: -1)); simulation.Tick(); }
        simulation.Start();
        Assert.Equal("countdown", simulation.Phase);
        Assert.Null(simulation.WinnerId);
        Assert.All(simulation.Snapshot().Fighters, f =>
        { Assert.Equal(1, f.Stocks); Assert.Equal(0, f.Falls); Assert.Equal(0, f.KOs); });
        Assert.Equal(240, simulation.Snapshot().RemainingSeconds);
    }

    [Fact]
    public void PlayerLimitAndMinimumMatchSizeAreEnforced()
    {
        var simulation = new ArenaSimulation();
        simulation.AddFighter("one", "One", "starter");
        Assert.Throws<InvalidOperationException>(() => simulation.Start());
        simulation.AddFighter("two", "Two", "bip01");
        simulation.AddFighter("three", "Three", "raymond");
        simulation.AddFighter("four", "Four", "starter");
        Assert.Throws<InvalidOperationException>(() => simulation.AddFighter("five", "Five", "starter"));
        Assert.Equal(4, simulation.FighterCount);
    }

    [Fact]
    public void ReconnectingStartsNewInputSequenceWithoutHeldButtons()
    {
        var simulation = Match();
        simulation.SetInput("one", new(900, Horizontal: 1, Jump: true));
        simulation.Tick();
        simulation.SetConnected("one", false);
        simulation.SetConnected("one", true);
        simulation.SetInput("one", new(0, Horizontal: -1));
        simulation.Tick();
        Assert.Equal(0, One(simulation).LastInputSequence);
        Assert.True(One(simulation).Connected);
        Assert.Equal(-1, One(simulation).Facing);
    }

    [Fact]
    public void FallingNearAnEdgeCatchesLedgeAndClimbsBackToStage()
    {
        var simulation = Match();
        var sequence = 0;
        while (One(simulation).X > 191 && sequence < 100)
        {
            simulation.SetInput("one", new(sequence++, Horizontal: -1));
            simulation.Tick();
        }
        simulation.SetInput("one", new(sequence++));
        var caught = false;
        for (var i = 0; i < 65; i++)
        {
            simulation.Tick();
            caught |= simulation.Snapshot().Events.Any(e => e.Type == "ledge" && e.ActorId == "one");
        }
        Assert.True(caught);
        Assert.True(One(simulation).Grounded);
        Assert.Equal(565, One(simulation).Y);
        Assert.Equal(3, One(simulation).Stocks);
    }

    [Fact]
    public void LeavingAnActiveTwoPlayerMatchAwardsRemainingPlayer()
    {
        var simulation = Match();
        simulation.RemoveFighter("one");
        Assert.Equal("finished", simulation.Phase);
        Assert.Equal("two", simulation.WinnerId);
        Assert.False(One(simulation).Connected);
        Assert.Equal(0, One(simulation).Stocks);
    }

    [Fact]
    public void AppearanceSanitizesEachFieldAndRejectsExecutableOrMalformedValues()
    {
        var unsafeAppearance = new ArenaAppearance("url(javascript:alert(1))",
            "#abc", "<img src=x onerror=alert(1)>", "#12345g");
        Assert.Equal(new ArenaAppearance(), ArenaAppearance.Sanitize(unsafeAppearance));
        Assert.Equal(new ArenaAppearance(), ArenaAppearance.Sanitize(null));
        Assert.Equal(new ArenaAppearance(), ArenaAppearance.Sanitize(new(null!, null!, null!, null!)));

        var mixed = ArenaAppearance.Sanitize(new("#ABC123", "red", "long", "#0102fF"));
        Assert.Equal("#ABC123", mixed.Skin);
        Assert.Equal("#42302a", mixed.Hair);
        Assert.Equal("long", mixed.HairStyle);
        Assert.Equal("#0102fF", mixed.Outfit);
        Assert.Equal("cap", ArenaAppearance.Sanitize(new(HairStyle: "cap")).HairStyle);
    }

    [Fact]
    public void AppearanceSurvivesSnapshotsAndRematchesWithoutChangingGameplay()
    {
        var styled = new ArenaSimulation();
        var plain = new ArenaSimulation();
        var appearance = new ArenaAppearance("#998877", "#102030", "cap", "#aBcDeF");
        styled.AddFighter("one", "One", "starter", appearance: appearance);
        plain.AddFighter("one", "One", "starter");
        styled.AddFighter("two", "Two", "bip01");
        plain.AddFighter("two", "Two", "bip01");
        styled.Start(); plain.Start();
        Advance(styled, 180); Advance(plain, 180);
        for (var i = 0; i < 120; i++)
        {
            var input = new ArenaInput(i, Horizontal: i < 40 ? 1 : 0,
                Jump: i == 10, Attack: i == 60, Special: i == 95);
            styled.SetInput("one", input); plain.SetInput("one", input);
            styled.Tick(); plain.Tick();
        }
        Assert.Equal(appearance, One(styled).Appearance);
        Assert.Null(Two(styled).Appearance);
        Assert.Equal(One(plain), One(styled) with { Appearance = null });
        Assert.Equal(Two(plain), Two(styled));
        styled.Start();
        Assert.Equal(appearance, One(styled).Appearance);
    }

    [Fact]
    public void SimulationSanitizesAppearanceBeforeSendingItToOpponents()
    {
        var simulation = new ArenaSimulation();
        simulation.AddFighter("one", "One", "starter", appearance:
            new("#123456", "#fff;filter:url(x)", "SHORT", "rgb(1,2,3)"));
        Assert.Equal(new ArenaAppearance(Skin: "#123456"), One(simulation).Appearance);
    }
}
