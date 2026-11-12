using Mercantec.Tech.Api.Arena;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class ArenaInputPracticeTests
{
    [Fact]
    public void ShortJumpSurvivesReleaseBeforeTickAndAHeldButtonDoesNotRepeat()
    {
        var rig = Rig.Match();
        var floor = rig.Player.Y;
        rig.Send(new(Jump: true));
        rig.Send(new());
        rig.Tick();
        Assert.False(rig.Player.Grounded);
        Assert.True(rig.Player.Y < floor);
        Assert.Equal(1, rig.Player.JumpsRemaining);

        for (var i = 0; i < 12; i++) rig.Step(new(Jump: true));
        Assert.Equal(0, rig.Player.JumpsRemaining);
        Assert.Equal(2, rig.State.Events.Count(e => e.Type == "jump"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReleasedDirectionalSpecialUsesAxesFromPress(bool explicitPress)
    {
        var rig = Rig.Match("bip01");
        rig.Send(explicitPress
            ? new(Pressed: (int)ArenaPress.Special, PressVertical: -1)
            : new(Vertical: -1, Special: true));
        rig.Send(new(Horizontal: .25));
        rig.Tick();
        Assert.Equal("recovery", rig.Player.Combat!.ActiveAbility);
        Assert.True(rig.Player.Vy < -800);
        Assert.Empty(rig.State.Projectiles);
        Assert.Equal(85, rig.Player.Combat.Energy);
    }

    [Fact]
    public void PressedAndHeldVersionsOfOneMoveCastOnce()
    {
        var rig = Rig.Match("bip01");
        rig.Send(new(Special: true, Pressed: (int)ArenaPress.Special));
        rig.Send(new(Special: true));
        rig.Tick();
        var pulse = Assert.Single(rig.State.Projectiles);
        for (var i = 0; i < 58; i++) rig.Step(new(Special: true));
        Assert.DoesNotContain(rig.State.Projectiles, projectile => projectile.Id != pulse.Id);
        Assert.InRange(rig.Opponent.Damage, 6.9, 7.1);
    }

    [Fact]
    public void ShortSpecialNearEndOfAttackRecoveryFiresWhenFree()
    {
        var rig = Rig.Match("bip01");
        rig.Step(new(Attack: true));
        rig.Tick(13);
        rig.Send(new(Special: true));
        rig.Send(new());
        rig.Tick();
        Assert.Empty(rig.State.Projectiles);
        rig.Tick(5);
        Assert.Single(rig.State.Projectiles);
        Assert.Equal("pulse", rig.Player.Combat!.ActiveAbility);
    }

    [Fact]
    public void ActionPressedTooEarlyDuringRecoveryExpires()
    {
        var rig = Rig.Match("bip01");
        rig.Step(new(Attack: true));
        rig.Tick(2);
        rig.Send(new(Special: true));
        rig.Send(new());
        rig.Tick(24);
        Assert.Empty(rig.State.Projectiles);
        Assert.Equal(100, rig.Player.Combat!.Energy);
        Assert.Equal(0, rig.Player.Combat.Cooldowns.Pulse);
    }

    [Fact]
    public void GroundedGrabQueuedJustBeforeLandingExecutesOnLanding()
    {
        var rig = Rig.Match("raymond");
        rig.Approach(95);
        rig.Send(new(Jump: true));
        rig.Send(new());
        rig.Tick();
        var floor = rig.Opponent.Y;
        for (var i = 0; i < 60 && !(rig.Player.Vy > 0 && floor - rig.Player.Y < 8); i++) rig.Tick();
        Assert.False(rig.Player.Grounded);
        rig.Send(new(Grab: true));
        rig.Send(new());
        rig.Tick(12);
        Assert.Equal("one", rig.Opponent.Combat!.CapturedBy);
    }

    [Fact]
    public void CaptureEscapeProgressCountsShortTapsOncePerTick()
    {
        var rig = Rig.Match("raymond");
        rig.Approach(95);
        rig.Step(new(Grab: true));
        rig.Tick(9);
        var before = rig.Opponent.Combat!;
        Assert.Equal("one", before.CapturedBy);
        Assert.InRange(before.CaptureRemaining, .45, .60);

        // Two presses in the same simulation tick cannot skip two escape steps.
        for (var i = 0; i < 2; i++)
        {
            rig.Send(new(Jump: true), "two");
            rig.Send(new(), "two");
        }
        rig.Tick();
        Assert.Equal(1, rig.Opponent.Combat!.EscapePresses);
        Assert.True(rig.Opponent.Combat.CaptureRemaining < before.CaptureRemaining);

        rig.Send(new(Pressed: (int)ArenaPress.Jump), "two");
        rig.Tick();
        Assert.Equal(2, rig.Opponent.Combat!.EscapePresses);
        rig.Send(new(Jump: true), "two");
        rig.Send(new(), "two");
        rig.Tick();
        Assert.Null(rig.Opponent.Combat!.CapturedBy);
        Assert.Equal(0, rig.Opponent.Combat.CaptureRemaining);
        Assert.Contains(rig.State.Events, e => e.Type == "escape" && e.ActorId == "two");
    }

    [Fact]
    public void ReconnectClearsQueuedActionsAndAcceptsANewInputSequence()
    {
        var rig = Rig.Match("bip01");
        rig.Send(new(Pressed: (int)ArenaPress.Special));
        rig.Simulation.SetConnected("one", false);
        rig.Simulation.SetConnected("one", true);
        rig.Tick();
        Assert.Empty(rig.State.Projectiles);
        rig.Simulation.SetInput("one", new(0, Pressed: (int)ArenaPress.Special));
        rig.Simulation.SetInput("one", new(0, Pressed: (int)ArenaPress.Special));
        rig.Tick();
        Assert.Single(rig.State.Projectiles);
        Assert.Equal(0, rig.Player.LastInputSequence);
    }

    [Fact]
    public void SandboxCannotBeEnabledOnNormalMatch()
    {
        var rig = Rig.Match(bot: true);
        Assert.False(rig.Simulation.ConfigurePractice("one", "two", new()));
        Assert.False(rig.Simulation.SetPracticeDifficulty("hard"));
        Assert.False(rig.Simulation.ResetPractice());
        Assert.False(rig.Simulation.RefillPracticeResources());
        Assert.False(rig.State.IsPracticeSandbox);
        Assert.True(rig.State.RemainingSeconds > 0);
    }

    [Fact]
    public void TimedPracticeDifficultyKeepsCurrentMatchAndClock()
    {
        var rig = Rig.Match(training: true, bot: true);
        Assert.True(rig.Simulation.SetPracticeDifficulty("easy"));
        rig.Simulation.Start();
        rig.Tick(211);
        var before = rig.State;
        Assert.True(rig.Simulation.SetPracticeDifficulty("hard"));
        Assert.False(rig.Simulation.SetPracticeDifficulty("unknown"));
        Assert.False(rig.State.IsPracticeSandbox);
        Assert.Equal(before.Tick, rig.State.Tick);
        Assert.Equal(before.RemainingSeconds, rig.State.RemainingSeconds);
        Assert.Equal(before.Fighters[0], rig.Player);
    }

    [Fact]
    public void PracticeResetRestoresPositionsDamageAndResourcesWithoutReplayingInputs()
    {
        var rig = Rig.Match("bip01", training: true, bot: true);
        Assert.True(rig.Simulation.ConfigurePractice("one", "two",
            new(OpponentMode: "idle", Position: "edge", PlayerDamage: 45, OpponentDamage: 70)));
        var initial = rig.Player;
        Assert.True(rig.State.IsPracticeSandbox);
        Assert.Equal("playing", rig.State.Phase);
        Assert.Equal(0, rig.State.Countdown);
        Assert.Equal(0, rig.State.RemainingSeconds);
        Assert.Equal(45, initial.Damage);
        Assert.Equal(70, rig.Opponent.Damage);

        for (var i = 0; i < 10; i++) rig.Step(new(Horizontal: 1));
        rig.Step(new(Special: true));
        Assert.True(rig.Player.X > initial.X);
        Assert.Equal(88, rig.Player.Combat!.Energy);
        var position = rig.Player.X;
        Assert.True(rig.Simulation.RefillPracticeResources());
        Assert.Equal(position, rig.Player.X);
        Assert.Equal(100, rig.Player.Combat!.Energy);

        rig.Send(new(Pressed: (int)ArenaPress.Jump));
        var lastSequence = rig.Player.LastInputSequence;
        var lastTick = rig.State.Tick;
        Assert.True(rig.Simulation.ResetPractice());
        rig.Simulation.SetInput("one", new(lastSequence - 1, Pressed: (int)ArenaPress.Jump));
        rig.Tick();
        Assert.Equal(lastTick + 1, rig.State.Tick);
        Assert.Equal(lastSequence, rig.Player.LastInputSequence);
        Assert.Equal(initial.X, rig.Player.X);
        Assert.Equal(initial.Y, rig.Player.Y);
        Assert.Equal(45, rig.Player.Damage);
        Assert.True(rig.Player.Grounded);
        Assert.Empty(rig.State.Projectiles);
    }

    [Fact]
    public void PracticeDummyCanBlockAndRepeatAChosenMove()
    {
        var rig = Rig.Match(training: true, bot: true);
        Assert.True(rig.Simulation.ConfigurePractice("one", "two", new(OpponentMode: "block")));
        rig.Tick(8);
        Assert.True(rig.Opponent.Shielding);
        Assert.Empty(rig.State.Projectiles);

        Assert.True(rig.Simulation.ConfigurePractice("one", "two", new(OpponentMode: "repeat", RepeatedMove: "pulse")));
        rig.Tick(35);
        Assert.Empty(rig.State.Projectiles);
        rig.Tick();
        Assert.Equal("two", Assert.Single(rig.State.Projectiles).OwnerId);
        rig.Tick(35);
        Assert.InRange(rig.Player.Damage, 6.9, 7.1);
    }

    [Fact]
    public void OffstageDrillCanRecoverAndARepetitionDoesNotSpendStocks()
    {
        var rig = Rig.Match("bip01", training: true, bot: true);
        Assert.True(rig.Simulation.ConfigurePractice("one", "two",
            new(OpponentMode: "idle", Position: "offstage", InfiniteResources: true)));
        var start = rig.Player;
        Assert.False(start.Grounded);
        Assert.True(start.Vy > 0);
        rig.Step(new(Pressed: (int)ArenaPress.Special, PressHorizontal: 1, PressVertical: -1));
        Assert.True(rig.Player.Vy < -800);
        Assert.True(rig.Player.Combat!.Energy < 100);
        rig.Tick();
        Assert.Equal(100, rig.Player.Combat!.Energy);

        Assert.True(rig.Simulation.ResetPractice());
        for (var i = 0; i < 150 && rig.Player.Falls == 0; i++) rig.Tick();
        Assert.True(rig.Player.Falls > 0);
        Assert.Equal(start.Stocks, rig.Player.Stocks);
        Assert.Equal("playing", rig.State.Phase);
        rig.Tick(45);
        Assert.Equal(start.X, rig.Player.X);
        Assert.Equal(start.Y, rig.Player.Y);
    }

    private sealed class Rig(ArenaSimulation simulation)
    {
        private long sequence;
        public ArenaSimulation Simulation => simulation;
        public ArenaState State => simulation.Snapshot();
        public ArenaFighterSnapshot Player => State.Fighters[0];
        public ArenaFighterSnapshot Opponent => State.Fighters[1];

        public static Rig Match(string avatar = "starter", bool training = false, bool bot = false)
        {
            var simulation = new ArenaSimulation(training: training);
            simulation.AddFighter("one", "Spiller", avatar);
            simulation.AddFighter("two", "Modstander", "starter", isBot: bot);
            var rig = new Rig(simulation);
            if (!training)
            {
                simulation.Start();
                rig.Tick(211);
            }
            return rig;
        }

        public void Send(ArenaInput input, string id = "one") => simulation.SetInput(id, input with { Sequence = ++sequence });

        public void Step(ArenaInput input)
        {
            Send(input);
            Tick();
        }

        public void Tick(int ticks = 1)
        {
            for (var i = 0; i < ticks; i++) simulation.Tick();
        }

        public void Approach(double distance)
        {
            for (var i = 0; i < 100 && Opponent.X - Player.X > distance; i++) Step(new(Horizontal: 1));
            Step(new());
            Tick(10);
        }
    }
}
