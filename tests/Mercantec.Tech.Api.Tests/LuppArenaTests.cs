using Mercantec.Tech.Api.Arena;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class LuppArenaTests
{
    private static ArenaSimulation Match(bool training = false)
    {
        var simulation = new ArenaSimulation(training: training);
        simulation.AddFighter("one", "LUPP", "lupp"); simulation.AddFighter("two", "Modstander", "starter");
        simulation.Start(); for (var tick = 0; tick < 211; tick++) simulation.Tick(); return simulation;
    }
    private static ArenaFighterSnapshot Fighter(ArenaSimulation simulation, string id = "one") => simulation.Snapshot().Fighters.Single(f => f.Id == id);
    private static void Step(ArenaSimulation simulation, ArenaInput? one = null, ArenaInput? two = null)
    {
        var sequence = simulation.Snapshot().Tick + 1L;
        simulation.SetInput("one", (one ?? new()) with { Sequence = sequence });
        simulation.SetInput("two", (two ?? new()) with { Sequence = sequence }); simulation.Tick();
    }
    private static void Wait(ArenaSimulation simulation, int ticks) { for (var tick = 0; tick < ticks; tick++) Step(simulation); }
    private static void Approach(ArenaSimulation simulation, double distance)
    {
        for (var tick = 0; tick < 150 && Fighter(simulation, "two").X - Fighter(simulation).X > distance; tick++) Step(simulation, new(Horizontal: 1));
        Wait(simulation, 10);
    }

    [Fact]
    public void IdeaUsesLuppEnergyAndReleasesOnlyAtFrameFourWithOneRealPlatformBounce()
    {
        var simulation = Match(); Step(simulation, new(Special: true));
        Assert.Empty(simulation.Snapshot().Projectiles);
        Assert.Equal("idea", Fighter(simulation).Combat!.ActiveAbility);
        Assert.InRange(Fighter(simulation).Combat!.Energy, 87.9, 88.1);
        for (var tick = 0; tick < 13; tick++) { Step(simulation); Assert.Empty(simulation.Snapshot().Projectiles); }
        Step(simulation);
        Assert.Equal(4, Fighter(simulation).ActionFrame);
        var idea = Assert.Single(simulation.Snapshot().Projectiles);
        Assert.Equal("idea", idea.Kind); Assert.Equal("lupp", idea.SourceAvatarId);
        Assert.InRange(idea.Vx, 559, 561); Assert.True(idea.Vy < 0);
        Assert.Single(simulation.Snapshot().Events, item => item.Type == "projectile" && item.ActorId == "one");
        // Move the target away so the independently simulated projectile can reach the stage.
        for (var tick = 0; tick < 65; tick++) Step(simulation, two: new(Horizontal: 1));
        Assert.Single(simulation.Snapshot().Events, item => item.Type == "prototype-bounce");
    }

    [Fact]
    public void DamageDisconnectAndResetCancelPendingIdeasRatherThanLeavingGhostProjectiles()
    {
        var simulation = Match(); Approach(simulation, 100);
        Step(simulation, new(Special: true), new(Attack: true)); Wait(simulation, 35);
        Assert.True(Fighter(simulation).Damage > 0);
        Assert.DoesNotContain(simulation.Snapshot().Events, item => item.Type == "projectile" && item.ActorId == "one");
        simulation = Match(); Step(simulation, new(Special: true)); Wait(simulation, 5);
        simulation.SetConnected("one", false); simulation.SetConnected("one", true); Wait(simulation, 20);
        Assert.Empty(simulation.Snapshot().Projectiles);
        simulation.Start(); for (var tick = 0; tick < 211; tick++) simulation.Tick();
        Assert.Equal(100, Fighter(simulation).Combat!.Energy); Assert.Empty(simulation.Snapshot().Projectiles);
    }

    [Fact]
    public void FeedbackGrabPushesThroughShieldWithoutCaptureOrMagnetFallback()
    {
        var simulation = Match(); Approach(simulation, 100);
        Step(simulation, new(Grab: true), new(Shield: true));
        for (var tick = 0; tick < 10; tick++) Step(simulation, two: new(Shield: true));
        var target = Fighter(simulation, "two");
        Assert.Equal(6, target.Damage); Assert.Null(target.Combat!.CapturedBy);
        Assert.InRange(Fighter(simulation).Combat!.Energy, 84, 86);
        Assert.Contains(simulation.Snapshot().Events, item => item.Type == "feedback");
        Assert.DoesNotContain(simulation.Snapshot().Events, item => item.Type is "capture" or "magnet" or "gift-grab");
    }

    [Fact]
    public void PrototypeShieldConvertsAnEnemyShotIntoItsOwnSlowerCubeWithoutUltimateFarming()
    {
        var simulation = Match(); Step(simulation, new(Shield: true), new(Special: true));
        for (var tick = 0; tick < 60 && !simulation.Snapshot().Events.Any(item => item.Type == "prototype"); tick++) Step(simulation, new(Shield: true));
        Assert.Contains(simulation.Snapshot().Events, item => item.Type == "prototype" && item.ActorId == "one");
        var cube = Assert.Single(simulation.Snapshot().Projectiles);
        Assert.Equal("one", cube.OwnerId); Assert.Equal("lupp", cube.SourceAvatarId); Assert.Equal("prototype", cube.Kind);
        Assert.InRange(Math.Abs(cube.Vx), 359, 361);
        Assert.Equal(0, Fighter(simulation).Damage); Assert.Equal(0, Fighter(simulation).Combat!.Ultimate);
        Assert.Equal("prototype-shield", Fighter(simulation).Combat!.ActiveAbility);
    }

    [Fact]
    public void IterationAndRecoveryHaveIndependentPhysicsCostsAndCooldowns()
    {
        var simulation = Match(); Step(simulation, new(Horizontal: 1, Special: true));
        Assert.InRange(Fighter(simulation).Vx, 709, 711); Assert.InRange(Fighter(simulation).Combat!.Energy, 79.9, 80.1);
        Assert.Equal("iterate", Fighter(simulation).Combat!.ActiveAbility); Assert.False(Fighter(simulation).Combat!.Armored);
        simulation = Match(); Step(simulation, new(Vertical: -1, Special: true));
        Assert.InRange(Fighter(simulation).Vy, -821, -819); Assert.InRange(Fighter(simulation).Combat!.Energy, 81.9, 82.1);
        Assert.Equal("recovery", Fighter(simulation).Combat!.ActiveAbility);
        Step(simulation); Step(simulation, new(Vertical: -1, Special: true));
        Assert.Single(simulation.Snapshot().Events, item => item.Type == "recovery");
    }

    [Fact]
    public void PlaytestPauseHealsTenAfterARealVulnerableChannelAndCanBeCancelledWithoutRefund()
    {
        var simulation = Match(); Step(simulation, two: new(Special: true)); Wait(simulation, 65);
        var damage = Fighter(simulation).Damage; Assert.True(damage > 0);
        Step(simulation, new(Vertical: 1, Special: true));
        Assert.InRange(Fighter(simulation).Combat!.Energy, 69.9, 70.1);
        Assert.InRange(Fighter(simulation).Combat!.ChannelRemaining, .69, .71);
        Wait(simulation, 43); Assert.Equal(Math.Max(0, damage - 10), Fighter(simulation).Damage);
        simulation = Match(); Step(simulation, two: new(Special: true)); Wait(simulation, 65); damage = Fighter(simulation).Damage;
        Step(simulation, new(Vertical: 1, Special: true)); Step(simulation, new(Horizontal: 1));
        Assert.Contains(simulation.Snapshot().Events, item => item.Type == "repair-cancel");
        Wait(simulation, 45);
        Assert.Equal(damage, Fighter(simulation).Damage);
        Assert.True(Fighter(simulation).Combat!.Energy < 80);
    }

    [Fact]
    public void GameJamConsumesTheRealMeterAndReleasesFiveIndependentPrototypeCubes()
    {
        var simulation = Match(); Step(simulation, new(Smash: true)); Assert.Empty(simulation.Snapshot().Projectiles);
        Assert.Equal(0, Fighter(simulation).Combat!.ChannelRemaining);
        simulation = Match(training: true); Step(simulation, new(Smash: true));
        Assert.Equal(0, Fighter(simulation).Combat!.Ultimate);
        Assert.Equal("game-jam-charge", Fighter(simulation).Combat!.ActiveAbility);
        for (var tick = 0; tick < 54; tick++) Step(simulation, new(Smash: true));
        Assert.Empty(simulation.Snapshot().Projectiles);
        Assert.Equal(0, Fighter(simulation).ActionFrame);
        for (var tick = 0; tick < 10; tick++) { Step(simulation); Assert.Empty(simulation.Snapshot().Projectiles); }
        Step(simulation);
        Assert.Equal(2, Fighter(simulation).ActionFrame);
        var cubes = simulation.Snapshot().Projectiles;
        Assert.Equal(5, cubes.Count); Assert.Equal(5, cubes.Select(cube => cube.Id).Distinct().Count());
        Assert.All(cubes, cube => { Assert.Equal("lupp", cube.SourceAvatarId); Assert.Equal("prototype", cube.Kind); });
        Assert.Contains(simulation.Snapshot().Events, item => item.Type == "game-jam");
        Assert.DoesNotContain(simulation.Snapshot().Events, item => item.Type is "join" or "overload" or "gift-fest");
        Wait(simulation, 5);
        Assert.Single(simulation.Snapshot().Events, item => item.Type == "game-jam");
    }

    [Fact]
    public void LuppComputerUsesItsOwnKitAndKeepsTheSimulationFinite()
    {
        var simulation = new ArenaSimulation(training: true);
        simulation.AddFighter("one", "LUPP", "lupp", true); simulation.AddFighter("two", "Robot", "bip01", true); simulation.Start();
        var seen = new HashSet<string>();
        for (var tick = 0; tick < 1500; tick++)
        {
            simulation.Tick(); foreach (var item in simulation.Snapshot().Events.Where(item => item.ActorId == "one")) seen.Add(item.Type);
            var fighter = Fighter(simulation); Assert.True(double.IsFinite(fighter.X) && double.IsFinite(fighter.Y));
            Assert.InRange(fighter.Combat!.Energy, 0, 100); Assert.InRange(fighter.Combat.Ultimate, 0, 100);
        }
        Assert.Contains("ultimate-charge", seen); Assert.Contains("feedback", seen); Assert.Contains("projectile", seen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedGameJamReleaseCannotLeakThePendingFiveCubeBurst(bool reset)
    {
        var simulation = Match(training: true); Approach(simulation, 100);
        Step(simulation, new(Smash: true));
        for (var tick = 0; tick < 54; tick++) Step(simulation, new(Smash: true));
        Assert.Empty(simulation.Snapshot().Projectiles); Assert.Equal("ultimate_release", Fighter(simulation).Action);
        if (reset)
        {
            simulation.Start(); for (var tick = 0; tick < 211; tick++) simulation.Tick();
        }
        else
        {
            Step(simulation, two: new(Attack: true)); Wait(simulation, 15);
            Assert.True(Fighter(simulation).Damage > 0);
        }
        Assert.Empty(simulation.Snapshot().Projectiles);
        Assert.DoesNotContain(simulation.Snapshot().Events, item => item.Type == "game-jam");
    }
}
