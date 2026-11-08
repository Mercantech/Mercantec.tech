using System.Text.Json;
using Mercantec.Tech.Api.Arena;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class ArenaAbilityTests
{
    private static ArenaSimulation Match(string first = "bip01", string second = "raymond", bool training = false, bool third = false)
    {
        var simulation = new ArenaSimulation(training: training);
        simulation.AddFighter("one", "Første", first);
        simulation.AddFighter("two", "Anden", second);
        if (third) simulation.AddFighter("three", "Tredje", "starter");
        simulation.Start();
        for (var i = 0; i < 211; i++) simulation.Tick();
        return simulation;
    }

    private static ArenaFighterSnapshot Fighter(ArenaSimulation simulation, string id = "one") =>
        simulation.Snapshot().Fighters.Single(x => x.Id == id);

    private static void Step(ArenaSimulation simulation, ArenaInput? one = null, ArenaInput? two = null, ArenaInput? three = null)
    {
        var sequence = simulation.Snapshot().Tick + 1L;
        simulation.SetInput("one", (one ?? new()) with { Sequence = sequence });
        simulation.SetInput("two", (two ?? new()) with { Sequence = sequence });
        if (three is not null) simulation.SetInput("three", three with { Sequence = sequence });
        simulation.Tick();
    }

    private static void Wait(ArenaSimulation simulation, int ticks)
    { for (var i = 0; i < ticks; i++) Step(simulation); }

    private static void Approach(ArenaSimulation simulation, double distance)
    {
        for (var i = 0; i < 150 && Fighter(simulation, "two").X - Fighter(simulation).X > distance; i++)
            Step(simulation, new(Horizontal: 1));
        Wait(simulation, 10);
    }

    [Fact]
    public void QuickRobotJabAndHeavyTrunkHaveDifferentStartupAndDamage()
    {
        var robot = Match("bip01", "starter"); var elephant = Match("raymond", "starter");
        Approach(robot, 85); Approach(elephant, 85);
        for (var i = 0; i < 5; i++)
        { Step(robot, new(Attack: true)); Step(elephant, new(Attack: true)); }
        Assert.InRange(Fighter(robot, "two").Damage, 5.9, 6.1);
        Assert.Equal(0, Fighter(elephant, "two").Damage);
        for (var i = 0; i < 9; i++) Step(elephant, new(Attack: true));
        Assert.InRange(Fighter(elephant, "two").Damage, 10.9, 11.1);
        Assert.InRange(Fighter(elephant).ActionFrame, 0, 7);
    }

    [Fact]
    public void PulsesDifferAndRobotAbilitiesHaveIndependentCostsAndCooldowns()
    {
        var robot = Match(); var elephant = Match("raymond", "starter");
        Step(robot, new(Special: true)); Step(elephant, new(Special: true));
        var robotPulse = Assert.Single(robot.Snapshot().Projectiles);
        var elephantPulse = Assert.Single(elephant.Snapshot().Projectiles);
        Assert.True(Math.Abs(robotPulse.Vx) > Math.Abs(elephantPulse.Vx));
        Assert.True(robotPulse.Radius < elephantPulse.Radius);
        Assert.Equal("bip01", robotPulse.SourceAvatarId);
        Assert.Equal("raymond", elephantPulse.SourceAvatarId);
        Assert.InRange(Fighter(robot).Combat!.Energy, 87.9, 88.1);
        Assert.Equal(100, Fighter(elephant).Combat!.Energy);
        Wait(robot, 22);
        Step(robot, new(Grab: true));
        var combat = Fighter(robot).Combat!;
        Assert.Equal("magnet", combat.ActiveAbility);
        Assert.True(combat.Cooldowns.Pulse > 0);
        Assert.True(combat.Cooldowns.Grab > 0);
        Assert.InRange(combat.Energy, 72, 76);
    }

    [Fact]
    public void EarlyRobotShieldReflectsOwnershipWhileElephantAbsorbsAndBuildsMeter()
    {
        var robot = Match("starter", "bip01");
        Step(robot, new(Special: true));
        ArenaProjectileSnapshot? reflected = null;
        for (var i = 0; i < 50 && reflected is null; i++)
        {
            var shield = robot.Snapshot().Projectiles.Any(p => p.OwnerId == "one" && Fighter(robot, "two").X - p.X < 80);
            Step(robot, two: new(Shield: shield));
            reflected = robot.Snapshot().Projectiles.FirstOrDefault(p => p.OwnerId == "two");
        }
        Assert.NotNull(reflected);
        Assert.Equal("reflected", reflected!.Kind);
        Assert.True(reflected.Reflected);
        Assert.True(reflected.Vx < 0);
        Assert.Equal("starter", reflected.SourceAvatarId);
        Assert.Equal(0, Fighter(robot, "two").Damage);
        Assert.Equal(0, Fighter(robot, "two").Combat!.Ultimate);

        var elephant = Match("starter", "raymond");
        Step(elephant, new(Special: true), new(Shield: true));
        for (var i = 0; i < 45; i++) Step(elephant, two: new(Shield: true));
        Assert.Empty(elephant.Snapshot().Projectiles);
        Assert.Equal(0, Fighter(elephant, "two").Damage);
        Assert.Equal(0, Fighter(elephant, "two").Vx);
        Assert.True(Fighter(elephant, "two").Combat!.Ultimate > 0);
        Assert.True(Fighter(elephant, "two").Shield < 90);
    }

    [Fact]
    public void MagnetMovesGroundedOpponentWithoutCapturingOrDamagingIt()
    {
        var simulation = Match("bip01", "starter");
        Approach(simulation, 175);
        var before = Fighter(simulation, "two").X;
        Step(simulation, new(Grab: true));
        Wait(simulation, 18);
        Assert.True(Fighter(simulation, "two").X < before - 2);
        Assert.Equal(0, Fighter(simulation, "two").Damage);
        Assert.Null(Fighter(simulation, "two").Combat!.CapturedBy);
        Assert.DoesNotContain(simulation.Snapshot().Events, e => e.Type == "capture");
    }

    [Fact]
    public void MagnetAbsorbsEnemyProjectileButDoesNotFarmOwnProjectile()
    {
        var enemy = Match("bip01", "starter");
        Approach(enemy, 190);
        Step(enemy, new(Grab: true), new(Special: true));
        Wait(enemy, 12);
        Assert.Empty(enemy.Snapshot().Projectiles);
        Assert.Contains(enemy.Snapshot().Events, e => e.Type == "absorb" && e.ActorId == "one");
        Assert.InRange(Fighter(enemy).Combat!.Energy, 88, 93);

        var own = Match("bip01", "starter");
        Step(own, new(Special: true)); Wait(own, 22);
        Step(own, new(Grab: true)); Wait(own, 10);
        Assert.DoesNotContain(own.Snapshot().Events, e => e.Type == "absorb" && e.ActorId == "one");
        Assert.InRange(Fighter(own).Combat!.Energy, 72, 77);
    }

    [Fact]
    public void RelationsGrabCapturesAndDirectionallyThrowsInsteadOfPulling()
    {
        var simulation = Match("raymond", "starter");
        Approach(simulation, 100);
        Step(simulation, new(Grab: true));
        Wait(simulation, 10);
        Assert.Equal("one", Fighter(simulation, "two").Combat!.CapturedBy);
        Assert.Equal(0, Fighter(simulation, "two").Damage);
        var gap = Fighter(simulation, "two").X - Fighter(simulation).X;
        Assert.InRange(gap, 57, 59);
        Step(simulation, new(Vertical: -1, Attack: true));
        Assert.Null(Fighter(simulation, "two").Combat!.CapturedBy);
        Assert.Equal(13, Fighter(simulation, "two").Damage);
        Assert.True(Fighter(simulation, "two").Vy < 0);
        Assert.Contains(simulation.Snapshot().Events, e => e.Type == "throw");
    }

    [Fact]
    public void ThreeJumpEdgesEscapeCaptureAndDisconnectReleasesIt()
    {
        var simulation = Match("raymond", "starter");
        Approach(simulation, 100);
        Step(simulation, new(Grab: true)); Wait(simulation, 10);
        Assert.Equal("one", Fighter(simulation, "two").Combat!.CapturedBy);
        for (var i = 0; i < 6; i++) Step(simulation, two: new(Jump: i % 2 == 0));
        Assert.Null(Fighter(simulation, "two").Combat!.CapturedBy);
        Assert.Contains(simulation.Snapshot().Events, e => e.Type == "escape");

        var disconnect = Match("raymond", "starter");
        Approach(disconnect, 100);
        Step(disconnect, new(Grab: true)); Wait(disconnect, 10);
        disconnect.SetConnected("one", false);
        Assert.Null(Fighter(disconnect, "two").Combat!.CapturedBy);
        Assert.Null(Fighter(disconnect).Combat!.ActiveAbility);
        disconnect.SetConnected("one", true);
        disconnect.SetInput("one", new(0, Horizontal: -1));
        disconnect.Tick();
        Assert.Equal(0, Fighter(disconnect).LastInputSequence);
    }

    [Theory]
    [InlineData("bip01", 8, 36, 6)]
    [InlineData("raymond", 14, 54, 8)]
    public void RepairHasDistinctResourceDurationHealingAndInterruptibleCooldown(string avatar, int heal, int duration, int seconds)
    {
        var simulation = Match(avatar, "starter");
        Step(simulation, two: new(Special: true)); Wait(simulation, 65);
        Assert.True(Fighter(simulation).Damage > 0);
        var before = Fighter(simulation).Damage;
        var shield = Fighter(simulation).Shield;
        Step(simulation, new(Vertical: 1, Special: true));
        var channel = Fighter(simulation).Combat!;
        Assert.Equal("repair", channel.ActiveAbility);
        Assert.InRange(channel.ChannelRemaining, duration / 60d - .01, duration / 60d + .01);
        Assert.InRange(channel.Cooldowns.Repair, seconds - .01, seconds + .01);
        if (avatar == "bip01") Assert.InRange(channel.Energy, 59, 61);
        else Assert.InRange(Fighter(simulation).Shield, shield - 40.1, shield - 39.7);
        Wait(simulation, duration + 1);
        Assert.Equal(Math.Max(0, before - heal), Fighter(simulation).Damage);
        Assert.Equal(0, Fighter(simulation).Combat!.ChannelRemaining);
        var repairedDamage = Fighter(simulation).Damage;
        Step(simulation, new(Vertical: 1, Special: true));
        Assert.Equal(0, Fighter(simulation).Combat!.ChannelRemaining);
        Assert.Equal(repairedDamage, Fighter(simulation).Damage);

        var cancelled = Match(avatar, "starter");
        Step(cancelled, two: new(Special: true)); Wait(cancelled, 65);
        var damaged = Fighter(cancelled).Damage;
        Step(cancelled, new(Vertical: 1, Special: true));
        Step(cancelled, new(Horizontal: 1));
        Wait(cancelled, duration + 1);
        Assert.Equal(damaged, Fighter(cancelled).Damage);
        Assert.True(Fighter(cancelled).Combat!.Cooldowns.Repair > 0);
    }

    [Fact]
    public void NoDamageRepairAndInsufficientEnergyCastsDoNotSpendOrStartCooldown()
    {
        var simulation = Match();
        Step(simulation, new(Vertical: 1, Special: true));
        Assert.Equal(100, Fighter(simulation).Combat!.Energy);
        Assert.Equal(0, Fighter(simulation).Combat!.Cooldowns.Repair);
        // Repeated real pulse casts eventually exhaust the finite energy budget.
        for (var i = 0; i < 13; i++)
        {
            Step(simulation, new(Special: true)); Wait(simulation, 40);
            Step(simulation, new(Grab: true)); Wait(simulation, 50);
        }
        Assert.InRange(Fighter(simulation).Combat!.Energy, 0, 100);
        Assert.InRange(Fighter(simulation).Combat!.Ultimate, 0, 100);
    }

    [Fact]
    public void ElephantChargeConsumesOneHitOfArmorWithoutCancellingCharge()
    {
        var simulation = Match("raymond", "starter");
        // The charge meets a projectile during its armored startup/active phase.
        Step(simulation, new(Horizontal: 1, Special: true), new(Special: true));
        Assert.True(Fighter(simulation).Combat!.Armored);
        for (var i = 0; i < 26 && Fighter(simulation).Damage == 0; i++) Step(simulation);
        Assert.InRange(Fighter(simulation).Damage, 3.8, 3.9);
        Assert.False(Fighter(simulation).Combat!.Armored);
        Assert.Equal("charge", Fighter(simulation).Combat!.ActiveAbility);
        Assert.True(Fighter(simulation).Vx > 300);
        Assert.Contains(simulation.Snapshot().Events, e => e.Type == "armor");
    }

    [Fact]
    public void TrainingUltimatesAreDistinctAndCannotBeSpammedOrRefilledByReconnect()
    {
        var robot = Match("bip01", "starter", true);
        Approach(robot, 175);
        Assert.Equal(100, Fighter(robot).Combat!.Ultimate);
        Step(robot, new(Smash: true));
        Assert.Equal(0, Fighter(robot).Combat!.Ultimate);
        for (var i = 0; i < 38; i++) Step(robot, new(Smash: true));
        Assert.True(Fighter(robot, "two").Damage >= 26);
        Assert.Empty(robot.Snapshot().Projectiles);
        Assert.InRange(Fighter(robot).Combat!.Energy, 0, 2);
        Assert.Contains(robot.Snapshot().Events, e => e.Type == "overload");
        var meter = Fighter(robot).Combat!.Ultimate;
        robot.SetConnected("one", false); robot.SetConnected("one", true);
        Assert.Equal(meter, Fighter(robot).Combat!.Ultimate);
        Wait(robot, 65); Step(robot, new(Smash: true));
        Assert.Equal(0, Fighter(robot).Combat!.ChannelRemaining);

        var elephant = Match("raymond", "starter", true);
        Step(elephant, new(Smash: true));
        for (var i = 0; i < 37; i++) Step(elephant, new(Smash: true));
        var join = Assert.Single(elephant.Snapshot().Projectiles);
        Assert.Equal("join", join.Kind);
        Assert.Equal("raymond", join.SourceAvatarId);
        Assert.True(Math.Abs(join.Vx) < 300);
        Assert.Equal(0, Fighter(elephant).Combat!.Ultimate);
        for (var i = 0; i < 100; i++) Step(elephant);
        Assert.Empty(elephant.Snapshot().Projectiles);
        Assert.True(Fighter(elephant, "two").Damage >= 26);
    }

    [Fact]
    public void NormalRoundsStartWithNoUltimateAndCombatDamageBuildsIt()
    {
        var simulation = Match("bip01", "starter");
        Assert.Equal(0, Fighter(simulation).Combat!.Ultimate);
        Step(simulation, new(Smash: true));
        Assert.Equal(0, Fighter(simulation).Combat!.ChannelRemaining);
        Wait(simulation, 25); Step(simulation, new(Special: true)); Wait(simulation, 45);
        Assert.True(Fighter(simulation, "two").Damage > 0);
        Assert.True(Fighter(simulation).Combat!.Ultimate > 0);
        var meter = Fighter(simulation).Combat!.Ultimate;
        Wait(simulation, 120);
        Assert.Equal(meter, Fighter(simulation).Combat!.Ultimate);
        simulation.Start();
        Assert.Equal(0, Fighter(simulation).Combat!.Ultimate);
        Assert.Equal(100, Fighter(simulation).Combat!.Energy);
    }

    [Fact]
    public void UniqueKitsRemainDeterministicAndFiniteAcrossLongFights()
    {
        var left = Match(training: true); var right = Match(training: true);
        for (var i = 0; i < 1800; i++)
        {
            var one = new ArenaInput(Horizontal: Math.Sin(i / 40d), Vertical: i % 140 < 30 ? -1 : i % 140 < 55 ? 1 : 0,
                Jump: i % 37 == 0, Attack: i % 23 == 0, Special: i % 53 == 0,
                Shield: i % 90 < 9, Grab: i % 79 == 0, Smash: i % 550 < 38);
            var two = one with { Horizontal = -one.Horizontal, Special = i % 61 == 0, Grab = i % 103 == 0 };
            Step(left, one, two); Step(right, one, two);
        }
        Assert.Equal(JsonSerializer.Serialize(left.Snapshot()), JsonSerializer.Serialize(right.Snapshot()));
        Assert.All(left.Snapshot().Fighters, f =>
        {
            Assert.True(double.IsFinite(f.X)); Assert.True(double.IsFinite(f.Y));
            Assert.InRange(f.Combat!.Energy, 0, 100); Assert.InRange(f.Combat.Ultimate, 0, 100);
            Assert.InRange(f.ActionFrame, 0, 7);
        });
    }

    [Fact]
    public void OverloadHitsBothSidesOnceAndEmptyEnergyRejectsPulse()
    {
        var simulation = new ArenaSimulation(training: true);
        simulation.AddFighter("three", "Venstre", "starter");
        simulation.AddFighter("one", "Robot", "bip01");
        simulation.AddFighter("two", "Højre", "starter");
        simulation.Start(); for (var i = 0; i < 211; i++) simulation.Tick();
        for (var i = 0; i < 38; i++) Step(simulation, new(Smash: true));
        Assert.Equal(26, Fighter(simulation, "two").Damage);
        Assert.Equal(26, Fighter(simulation, "three").Damage);
        Wait(simulation, 35);
        Assert.Equal(26, Fighter(simulation, "two").Damage);
        Assert.Equal(26, Fighter(simulation, "three").Damage);
        var energy = Fighter(simulation).Combat!.Energy;
        Assert.True(energy < 12);
        Assert.Equal(0, Fighter(simulation).Combat!.Cooldowns.Pulse);
        Step(simulation, new(Special: true));
        Assert.Empty(simulation.Snapshot().Projectiles);
        Assert.Equal(0, Fighter(simulation).Combat!.Cooldowns.Pulse);
        Assert.InRange(Fighter(simulation).Combat!.Energy, energy, energy + .16);
    }

    [Fact]
    public void ReflectedJoinKeepsItsSpellKindSourceAndRemainingLifetime()
    {
        var simulation = Match("raymond", "bip01", true);
        for (var i = 0; i < 38; i++) Step(simulation, new(Smash: true));
        ArenaProjectileSnapshot? reflected = null;
        for (var i = 0; i < 110 && reflected is null; i++)
        {
            var shield = simulation.Snapshot().Projectiles.Any(p => p.OwnerId == "one" &&
                Fighter(simulation, "two").X - p.X < 80);
            Step(simulation, two: new(Shield: shield));
            reflected = simulation.Snapshot().Projectiles.FirstOrDefault(p => p.OwnerId == "two");
        }
        Assert.NotNull(reflected);
        Assert.Equal("join", reflected!.Kind);
        Assert.True(reflected.Reflected);
        Assert.Equal("raymond", reflected.SourceAvatarId);
        Assert.True(reflected.Vx < 0);
        var sawPull = false;
        var exploded = false;
        for (var i = 0; i < 120; i++)
        {
            Step(simulation);
            sawPull |= simulation.Snapshot().Events.Any(e => e.Type == "join-pull" && e.ActorId == "two");
            exploded |= simulation.Snapshot().Events.Any(e => e.Type == "join-explode" && e.ActorId == "two");
        }
        Assert.True(sawPull);
        Assert.True(exploded);
        Assert.Empty(simulation.Snapshot().Projectiles);
    }

    [Fact]
    public void ThirdPartyHitReleasesCaptorsVictimWithoutThrowDamage()
    {
        var simulation = new ArenaSimulation();
        simulation.AddFighter("three", "Tredje", "starter");
        simulation.AddFighter("one", "Raymond", "raymond");
        simulation.AddFighter("two", "Fanget", "starter");
        simulation.Start(); for (var i = 0; i < 211; i++) simulation.Tick();
        Approach(simulation, 100);
        Step(simulation, new(Grab: true)); Wait(simulation, 10);
        Assert.Equal("one", Fighter(simulation, "two").Combat!.CapturedBy);
        Step(simulation, three: new(Special: true));
        for (var i = 0; i < 30 && Fighter(simulation).Damage == 0; i++) Step(simulation);
        Assert.True(Fighter(simulation).Damage > 0);
        Assert.Null(Fighter(simulation, "two").Combat!.CapturedBy);
        Assert.Equal(0, Fighter(simulation, "two").Damage);
        Assert.DoesNotContain(simulation.Snapshot().Events, e => e.Type == "throw");
    }

    [Fact]
    public void SecondHitBreaksOneHitChargeArmor()
    {
        var simulation = new ArenaSimulation();
        simulation.AddFighter("three", "Bagfra", "starter");
        simulation.AddFighter("one", "Raymond", "raymond");
        simulation.AddFighter("two", "Forfra", "starter");
        simulation.Start(); for (var i = 0; i < 211; i++) simulation.Tick();
        for (var i = 0; i < 60 && Fighter(simulation, "three").X < 460; i++)
            Step(simulation, three: new(Horizontal: 1));
        Wait(simulation, 10);
        Step(simulation, new(Horizontal: 1, Special: true), new(Special: true), new(Special: true));
        for (var i = 0; i < 45 && Fighter(simulation).Damage < 7; i++) Step(simulation);
        Assert.InRange(Fighter(simulation).Damage, 10.8, 10.9);
        Assert.False(Fighter(simulation).Combat!.Armored);
        Assert.NotEqual("charge", Fighter(simulation).Combat!.ActiveAbility);
        Assert.Single(simulation.Snapshot().Events, e => e.Type == "armor" && e.ActorId == "one");
    }

    [Theory]
    [InlineData("bip01")]
    [InlineData("raymond")]
    public void IncomingHitInterruptsRepairWithoutRefundOrHealing(string avatar)
    {
        var simulation = Match(avatar, "starter");
        Step(simulation, two: new(Special: true)); Wait(simulation, 65);
        var damage = Fighter(simulation).Damage;
        Assert.True(damage > 0);
        Step(simulation, two: new(Special: true));
        // Begin just before this second projectile arrives. Both channel lengths
        // leave enough time for the real enemy hit to interrupt completion.
        Wait(simulation, 18);
        Step(simulation, new(Vertical: 1, Special: true));
        Assert.True(Fighter(simulation).Combat!.ChannelRemaining > 0);
        for (var i = 0; i < 28 && Fighter(simulation).Damage == damage; i++) Step(simulation);
        Assert.True(Fighter(simulation).Damage > damage);
        Assert.Equal(0, Fighter(simulation).Combat!.ChannelRemaining);
        Assert.True(Fighter(simulation).Combat!.Cooldowns.Repair > 0);
        Assert.Contains(simulation.Snapshot().Events, e => e.Type == "repair-cancel");
    }

    [Fact]
    public void RecoveryAndDashHaveDifferentPhysicalTrajectories()
    {
        var robot = Match("bip01", "starter"); var elephant = Match("raymond", "starter");
        Step(robot, new(Horizontal: 1, Special: true));
        Step(elephant, new(Horizontal: 1, Special: true));
        Assert.True(Fighter(robot).Vx > Fighter(elephant).Vx + 250);
        Assert.True(Fighter(robot).Invulnerable);
        Assert.False(Fighter(elephant).Invulnerable);
        Assert.True(Fighter(elephant).Combat!.Armored);
        robot = Match("bip01", "starter"); elephant = Match("raymond", "starter");
        Step(robot, new(Horizontal: 1, Vertical: -1, Special: true));
        Step(elephant, new(Horizontal: 1, Vertical: -1, Special: true));
        Assert.True(Fighter(robot).Vy < Fighter(elephant).Vy - 100);
        Assert.True(Fighter(robot).Vx < Fighter(elephant).Vx);
        Assert.True(Fighter(robot).Combat!.Energy < 100);
        Assert.Equal(100, Fighter(elephant).Combat!.Energy);
    }

    [Fact]
    public void KnockoutClearsTrainingMeterAndAbilitiesUntilNextRound()
    {
        var simulation = Match("bip01", "starter", true);
        Step(simulation, new(Special: true)); Wait(simulation, 25);
        Assert.True(Fighter(simulation).Combat!.Energy < 100);
        for (var i = 0; i < 450 && Fighter(simulation).Stocks == 3; i++)
            Step(simulation, new(Horizontal: -1));
        Assert.Equal(2, Fighter(simulation).Stocks);
        Assert.Equal(0, Fighter(simulation).Combat!.Ultimate);
        Assert.Equal(0, Fighter(simulation).Combat!.ChannelRemaining);
        Assert.Null(Fighter(simulation).Combat!.CapturedBy);
        Assert.False(Fighter(simulation).Combat!.Armored);
        Assert.Equal(0, Fighter(simulation).Combat!.Cooldowns.Pulse);
        Wait(simulation, 100);
        Assert.Equal(0, Fighter(simulation).Combat!.Ultimate);
        simulation.Start();
        Assert.Equal(100, Fighter(simulation).Combat!.Ultimate);
    }
}
