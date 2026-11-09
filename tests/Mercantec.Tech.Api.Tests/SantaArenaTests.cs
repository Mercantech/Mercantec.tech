using Mercantec.Tech.Api.Arena;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class SantaArenaTests
{
    private static ArenaSimulation Match(string first = "santa", string second = "starter", bool training = false)
    {
        var simulation = new ArenaSimulation(training: training);
        simulation.AddFighter("one", "Julemanden", first);
        simulation.AddFighter("two", "Modstander", second);
        simulation.Start();
        for (var tick = 0; tick < 211; tick++) simulation.Tick();
        return simulation;
    }
    private static ArenaFighterSnapshot Fighter(ArenaSimulation simulation, string id = "one") =>
        simulation.Snapshot().Fighters.Single(fighter => fighter.Id == id);
    private static void Step(ArenaSimulation simulation, ArenaInput? one = null, ArenaInput? two = null)
    {
        var sequence = simulation.Snapshot().Tick + 1L;
        simulation.SetInput("one", (one ?? new()) with { Sequence = sequence });
        simulation.SetInput("two", (two ?? new()) with { Sequence = sequence });
        simulation.Tick();
    }
    private static void Wait(ArenaSimulation simulation, int ticks)
    { for (var tick = 0; tick < ticks; tick++) Step(simulation); }
    private static void Approach(ArenaSimulation simulation, double distance)
    {
        for (var tick = 0; tick < 150 && Fighter(simulation, "two").X - Fighter(simulation).X > distance; tick++)
            Step(simulation, new(Horizontal: 1));
        Wait(simulation, 10);
    }

    [Fact]
    public void StarReleasesExactlyOnceAtAuthoredFrameFourWithItsOwnArcCostAndCooldown()
    {
        var simulation = Match();
        Assert.Equal(AvatarEconomy.SantaId, Fighter(simulation).AvatarId);
        Step(simulation, new(Special: true));
        Assert.Empty(simulation.Snapshot().Projectiles);
        var combat = Fighter(simulation).Combat!;
        Assert.InRange(combat.Energy, 85.9, 86.1);
        Assert.Equal("star", combat.ActiveAbility);
        Assert.InRange(combat.Cooldowns.Pulse, .79, .81);
        for (var tick = 0; tick < 12; tick++)
        {
            Step(simulation);
            Assert.Empty(simulation.Snapshot().Projectiles);
            Assert.InRange(Fighter(simulation).ActionFrame, 0, 3);
        }
        Step(simulation);
        Assert.Equal(4, Fighter(simulation).ActionFrame);
        var star = Assert.Single(simulation.Snapshot().Projectiles);
        Assert.Equal("star", star.Kind);
        Assert.Equal("santa", star.SourceAvatarId);
        Assert.Equal(16, star.Radius);
        Assert.InRange(star.Vx, 589, 591);
        Assert.True(star.Vy < 0);
        var initialVy = star.Vy;
        Wait(simulation, 10);
        star = Assert.Single(simulation.Snapshot().Projectiles);
        Assert.True(star.Vy > initialVy);
        Assert.True(star.Y < Fighter(simulation).Y - 44);
        Step(simulation, new(Special: true));
        Assert.Single(simulation.Snapshot().Projectiles);
        Assert.Single(simulation.Snapshot().Events, item => item.Type == "projectile" && item.ActorId == "one");
        Assert.True(Fighter(simulation).Combat!.Cooldowns.Pulse > 0);
    }

    [Fact]
    public void AHitBeforeReleaseCancelsThePendingStarWithoutRefundOrDelayedGhostProjectile()
    {
        var simulation = Match();
        Approach(simulation, 100);
        Step(simulation, new(Special: true), new(Attack: true));
        Assert.Empty(simulation.Snapshot().Projectiles);
        for (var tick = 0; tick < 12 && Fighter(simulation).Damage == 0; tick++) Step(simulation);
        Assert.True(Fighter(simulation).Damage > 0);
        Wait(simulation, 20);
        Assert.DoesNotContain(simulation.Snapshot().Projectiles, projectile => projectile.SourceAvatarId == "santa");
        Assert.DoesNotContain(simulation.Snapshot().Events, item => item.Type == "projectile" && item.ActorId == "one");
        Assert.InRange(Fighter(simulation).Combat!.Energy, 85, 91);
        Assert.True(Fighter(simulation).Combat!.Cooldowns.Pulse > 0);
    }

    [Fact]
    public void DisconnectAndRoundResetClearPendingStarsBeforeTheyCanSpawn()
    {
        var simulation = Match();
        Step(simulation, new(Special: true));
        Wait(simulation, 5);
        simulation.SetConnected("one", false);
        simulation.SetConnected("one", true);
        Wait(simulation, 20);
        Assert.Empty(simulation.Snapshot().Projectiles);
        Assert.DoesNotContain(simulation.Snapshot().Events, item => item.Type == "projectile" && item.ActorId == "one");
        Assert.True(Fighter(simulation).Combat!.Energy < 90);
        simulation = Match();
        Step(simulation, new(Special: true));
        Wait(simulation, 5);
        simulation.Start();
        for (var tick = 0; tick < 211; tick++) simulation.Tick();
        Assert.Empty(simulation.Snapshot().Projectiles);
        Assert.Equal(100, Fighter(simulation).Combat!.Energy);
        Assert.Equal(0, Fighter(simulation).Combat!.Cooldowns.Pulse);
    }

    [Fact]
    public void GiftGrabLiftsThroughShieldWithoutRaymondCaptureOrRobotField()
    {
        var simulation = Match();
        Approach(simulation, 100);
        Step(simulation, new(Grab: true), new(Shield: true));
        for (var tick = 0; tick < 10; tick++) Step(simulation, two: new(Shield: true));
        var target = Fighter(simulation, "two");
        Assert.Equal(5, target.Damage);
        Assert.True(target.Vy < 0);
        Assert.Null(target.Combat!.CapturedBy);
        Assert.InRange(Fighter(simulation).Combat!.Energy, 80, 82);
        Assert.True(Fighter(simulation).Combat!.Cooldowns.Grab > 0);
        Assert.DoesNotContain(simulation.Snapshot().Events, item => item.Type is "capture" or "magnet");
        Assert.Contains(simulation.Snapshot().Events, item => item.Type == "throw");
    }

    [Fact]
    public void PeaceShieldConvertsAnEnemyProjectileToMagicWithoutReflectingOrFarmingUltimate()
    {
        var simulation = Match();
        Step(simulation, new(Horizontal: -1)); Wait(simulation, 10);
        Step(simulation, new(Special: true)); Wait(simulation, 27);
        var before = Fighter(simulation).Combat!.Energy;
        Step(simulation, new(Shield: true), new(Special: true));
        for (var tick = 0; tick < 50 && !simulation.Snapshot().Events.Any(item => item.Type == "absorb"); tick++)
            Step(simulation, new(Shield: true));
        Assert.Contains(simulation.Snapshot().Events, item => item.Type == "absorb" && item.ActorId == "one");
        Assert.Equal(0, Fighter(simulation).Damage);
        Assert.True(Fighter(simulation).Combat!.Energy >= Math.Min(100, before + 10));
        Assert.Equal(0, Fighter(simulation).Combat!.Ultimate);
        Assert.DoesNotContain(simulation.Snapshot().Projectiles, item => item.Reflected);
        Assert.Equal("peace-shield", Fighter(simulation).Combat!.ActiveAbility);
    }

    [Fact]
    public void SleighGlideAndChimneyRecoveryHaveTheirOwnPhysicsAndCosts()
    {
        var simulation = Match();
        Step(simulation, new(Horizontal: 1, Special: true));
        var santa = Fighter(simulation);
        Assert.InRange(santa.Vx, 649, 651);
        Assert.True(santa.Vy < 0);
        Assert.False(santa.Invulnerable);
        Assert.False(santa.Combat!.Armored);
        Assert.Equal("sleigh", santa.Combat.ActiveAbility);
        Assert.InRange(santa.Combat.Energy, 77.9, 78.1);
        Assert.InRange(santa.Combat.Cooldowns.Dash, 1.59, 1.61);
        simulation = Match();
        Step(simulation, new(Vertical: -1, Special: true));
        santa = Fighter(simulation);
        Assert.InRange(santa.Vy, -801, -799);
        Assert.False(santa.Grounded);
        Assert.InRange(santa.Combat!.Energy, 83.9, 84.1);
        Assert.Equal("recovery", santa.Combat.ActiveAbility);
        Step(simulation);
        Step(simulation, new(Vertical: -1, Special: true));
        Assert.InRange(Fighter(simulation).Combat!.Energy, 84, 84.3);
        Assert.Single(simulation.Snapshot().Events, item => item.Type == "recovery");
    }

    [Fact]
    public void ChristmasWarmthUsesMagicAndCompletesActualHealingAndShieldRestoration()
    {
        var simulation = Match();
        Step(simulation, two: new(Special: true)); Wait(simulation, 65);
        Step(simulation, two: new(Special: true)); Wait(simulation, 65);
        Assert.True(Fighter(simulation).Damage >= 12);
        for (var tick = 0; tick < 80; tick++) Step(simulation, new(Shield: true));
        Step(simulation);
        var damage = Fighter(simulation).Damage;
        var shield = Fighter(simulation).Shield;
        Step(simulation, new(Vertical: 1, Special: true));
        var combat = Fighter(simulation).Combat!;
        Assert.Equal("repair", combat.ActiveAbility);
        Assert.InRange(combat.Energy, 54.9, 55.1);
        Assert.InRange(combat.ChannelRemaining, .79, .81);
        Assert.InRange(combat.Cooldowns.Repair, 7.99, 8.01);
        Wait(simulation, 49);
        Assert.Equal(Math.Max(0, damage - 12), Fighter(simulation).Damage);
        Assert.True(Fighter(simulation).Shield >= Math.Min(100, shield + 12));
        Assert.Equal(0, Fighter(simulation).Combat!.ChannelRemaining);
    }

    [Fact]
    public void ChristmasWarmthCanBeCancelledWithoutRefundOrFreeHealing()
    {
        var simulation = Match();
        Step(simulation, two: new(Special: true)); Wait(simulation, 65);
        var damage = Fighter(simulation).Damage;
        Step(simulation, new(Vertical: 1, Special: true));
        Step(simulation, new(Horizontal: 1));
        Assert.Contains(simulation.Snapshot().Events, item => item.Type == "repair-cancel");
        Wait(simulation, 50);
        Assert.Equal(damage, Fighter(simulation).Damage);
        Assert.True(Fighter(simulation).Combat!.Energy < 65);
        Assert.True(Fighter(simulation).Combat!.Cooldowns.Repair > 0);
        Assert.Equal(0, Fighter(simulation).Combat!.ChannelRemaining);
    }

    [Fact]
    public void SantaComputerUsesItsOwnAbilitiesAndKeepsAllStateFinite()
    {
        var simulation = new ArenaSimulation(training: true);
        simulation.AddFighter("one", "Julemanden", "santa", isBot: true);
        simulation.AddFighter("two", "Robot", "bip01", isBot: true);
        simulation.Start();
        var seen = new HashSet<string>();
        for (var tick = 0; tick < 1500; tick++)
        {
            simulation.Tick();
            var state = simulation.Snapshot();
            foreach (var item in state.Events.Where(item => item.ActorId == "one")) seen.Add(item.Type);
            var santa = Fighter(simulation);
            Assert.True(double.IsFinite(santa.X) && double.IsFinite(santa.Y));
            Assert.InRange(santa.Combat!.Energy, 0, 100);
            Assert.InRange(santa.Combat.Ultimate, 0, 100);
        }
        // A real opponent may interrupt the charge; beginning it and using the
        // Santa-only lift plus star proves the CPU is not on a legacy fallback.
        Assert.Contains("ultimate-charge", seen);
        Assert.Contains("gift-grab", seen);
        Assert.Contains("projectile", seen);
    }

    [Fact]
    public void GiftFestUsesTheRealMeterAndReleasesThreeIndependentProjectiles()
    {
        var locked = Match();
        Step(locked, new(Smash: true));
        Assert.Equal(0, Fighter(locked).Combat!.ChannelRemaining);
        Assert.Empty(locked.Snapshot().Projectiles);
        var simulation = Match(training: true);
        Step(simulation, new(Smash: true));
        Assert.Equal("gift-fest-charge", Fighter(simulation).Combat!.ActiveAbility);
        Assert.InRange(Fighter(simulation).Combat!.ChannelRemaining, .79, .81);
        Assert.Equal(0, Fighter(simulation).Combat!.Ultimate);
        for (var tick = 0; tick < 48; tick++) Step(simulation, new(Smash: true));
        var gifts = simulation.Snapshot().Projectiles;
        Assert.Equal(3, gifts.Count);
        Assert.All(gifts, gift => { Assert.Equal("gift", gift.Kind); Assert.Equal("santa", gift.SourceAvatarId); });
        Assert.Equal(3, gifts.Select(gift => gift.Id).Distinct().Count());
        Assert.True(gifts.Min(gift => gift.Vy) < 0);
        Assert.True(gifts.Max(gift => gift.Vy) > 0);
        Assert.Contains(simulation.Snapshot().Events, item => item.Type == "gift-fest");
        Assert.DoesNotContain(simulation.Snapshot().Events, item => item.Type is "join" or "overload");
        simulation.SetConnected("one", false);
        simulation.SetConnected("one", true);
        Assert.Equal(0, Fighter(simulation).Combat!.Ultimate);
        Assert.True(Fighter(simulation).Combat!.Cooldowns.Ultimate > 0);
    }
}
