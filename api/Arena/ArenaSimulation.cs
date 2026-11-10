using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Arena;

/// <summary>
/// Fixed-step, server-owned platform combat. This file has no transport, clock,
/// database or rendering dependency and is also used by local WASM practice.
/// All positions use a 1280x720 world; a fighter's position is its feet centre.
/// </summary>
public sealed class ArenaSimulation
{
    private const double Dt = 1d / ArenaCatalog.TickRate;
    private const double HalfWidth = 24;
    private const double BodyHeight = 70;
    private readonly List<Fighter> fighters = [];
    private readonly List<Projectile> projectiles = [];
    private readonly List<ArenaEvent> events = [];
    private readonly ArenaStage stage;
    private readonly int stockCount;
    private readonly int matchTicks;
    private readonly bool training;
    private int frame;
    private int elapsed;
    private int countdownTicks;
    private int suddenDeathTicks;
    private long eventId;
    private long projectileId;
    private string phase = "lobby";
    private string? winnerId;
    private bool suddenDeath;

    public ArenaSimulation(string stageId = "rooftops", int stocks = 3, int minutes = 4, bool training = false)
    {
        stage = ArenaCatalog.FindStage(stageId);
        stockCount = Math.Clamp(stocks, 1, 5);
        matchTicks = Math.Clamp(minutes, 1, 10) * 60 * ArenaCatalog.TickRate;
        this.training = training;
    }

    public int FighterCount => fighters.Count;
    public string Phase => phase;
    public string? WinnerId => winnerId;
    public ArenaStage Stage => stage;

    public void AddFighter(string id, string name, string avatarId, bool isBot = false,
        string? color = null, ArenaAppearance? appearance = null)
    {
        if (string.IsNullOrWhiteSpace(id) || fighters.Any(x => x.Id == id)) return;
        if (fighters.Count >= 4) throw new InvalidOperationException("Arenaen har plads til fire spillere.");
        if (phase is "playing" or "countdown") throw new InvalidOperationException("Kampen er allerede startet.");
        var fighter = new Fighter
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "Spiller" : name[..Math.Min(40, name.Length)],
            AvatarId = AvatarEconomy.FindAvatar(avatarId)?.Id ?? AvatarEconomy.StarterId,
            IsBot = isBot,
            Appearance = appearance is null ? null : ArenaAppearance.Sanitize(appearance),
            Color = color ?? ArenaCatalog.Colors[fighters.Count],
            Stocks = stockCount,
            Facing = fighters.Count % 2 == 0 ? 1 : -1
        };
        fighters.Add(fighter);
        Place(fighter, fighters.Count - 1, false);
    }

    public void RemoveFighter(string id)
    {
        var fighter = Find(id);
        if (fighter is null) return;
        CancelAbility(fighter);
        ReleaseCapture(fighter);
        fighter.Attack = null;
        projectiles.RemoveAll(p => p.Owner == fighter);
        if (phase is "playing" or "countdown")
        {
            fighter.Connected = false;
            fighter.Removed = true;
            fighter.Stocks = 0;
            fighter.Input = new();
            Emit("leave", fighter.X, fighter.Y, fighter.Id);
            ResolveWinner();
        }
        else fighters.Remove(fighter);
    }

    public void SetConnected(string id, bool connected)
    {
        var fighter = Find(id);
        if (fighter is null) return;
        fighter.Connected = connected;
        CancelAbility(fighter);
        ReleaseCapture(fighter);
        fighter.Attack = null;
        fighter.ChargeTicks = 0;
        // A replacement browser socket starts a new input sequence. Clear held
        // keys on both disconnect and reconnect so neither old keys nor sequence
        // numbers can lock a returning player out of the running match.
        fighter.Input = fighter.PreviousInput = new();
        fighter.LastSequence = -1;
        fighter.InputTick = frame;
    }

    public void SetInput(string id, ArenaInput input)
    {
        var fighter = Find(id);
        if (fighter is null || input.Sequence <= fighter.LastSequence) return;
        // Reject non-finite values before they can poison the simulation.
        if (!double.IsFinite(input.Horizontal) || !double.IsFinite(input.Vertical)) return;
        fighter.LastSequence = input.Sequence;
        fighter.Input = input with
        {
            Horizontal = Math.Clamp(input.Horizontal, -1, 1),
            Vertical = Math.Clamp(input.Vertical, -1, 1)
        };
        fighter.InputTick = frame;
    }

    public void Start()
    {
        if (fighters.Count < 2) throw new InvalidOperationException("Der skal være mindst to kæmpere.");
        frame = elapsed = suddenDeathTicks = 0;
        suddenDeath = false;
        winnerId = null;
        countdownTicks = 3 * ArenaCatalog.TickRate;
        phase = "countdown";
        projectiles.Clear();
        events.Clear();
        foreach (var fighter in fighters)
        {
            fighter.Stocks = stockCount;
            fighter.Removed = false;
            fighter.EliminationTick = -1;
            fighter.Damage = 0;
            fighter.KOs = fighter.Falls = 0;
            fighter.LastHitterId = null;
            ResetCombat(fighter);
            fighter.Ultimate = training && HasKit(fighter) ? 100 : 0;
            fighter.LastSequence = -1;
            fighter.Input = fighter.PreviousInput = new();
            Place(fighter, fighters.IndexOf(fighter), false);
        }
        Emit("countdown", 640, 320);
    }

    /// <summary>Advance exactly one 1/60-second step. Call from one owning thread.</summary>
    public void Tick()
    {
        frame++;
        events.RemoveAll(x => frame - x.Tick > 36);
        if (phase == "countdown")
        {
            if (--countdownTicks <= 0)
            {
                phase = "playing";
                foreach (var fighter in fighters) fighter.Invulnerability = 30;
                Emit("go", 640, 320);
            }
            return;
        }
        if (phase != "playing") return;
        elapsed++;
        if (suddenDeath) suddenDeathTicks++;

        foreach (var fighter in fighters)
        {
            if (fighter.IsBot && fighter.Stocks > 0) fighter.Input = BotInput(fighter);
            else if (frame - fighter.InputTick > 20) fighter.Input = new(fighter.LastSequence);
            StepFighter(fighter);
        }
        ResolveAttacks();
        StepFields();
        StepProjectiles();
        foreach (var fighter in fighters.Where(x => x.Stocks > 0 && x.RespawnTicks == 0))
        {
            // A long sudden death gradually closes the arena, guaranteeing an end.
            var shrink = suddenDeath ? Math.Max(0, suddenDeathTicks - 1800) * 0.1 : 0;
            if (fighter.X < stage.Left + shrink || fighter.X > stage.Right - shrink ||
                fighter.Y < stage.Top || fighter.Y > stage.Bottom)
                KnockOut(fighter);
        }
        ResolveWinner();
        if (phase == "playing" && !suddenDeath && elapsed >= matchTicks) FinishOnTime();
        foreach (var fighter in fighters) fighter.PreviousInput = fighter.Input;
    }

    public ArenaState Snapshot() => new(frame, phase, Math.Max(0, countdownTicks * Dt),
        phase == "lobby" ? matchTicks * Dt : Math.Max(0, (matchTicks - elapsed) * Dt), stage.Id,
        fighters.Select(f => new ArenaFighterSnapshot(f.Id, f.Name, f.AvatarId, f.Color,
            Round(f.X), Round(f.Y), Round(f.Vx), Round(f.Vy), f.Facing, Round(f.Damage),
            f.Stocks, DisplayAction(f), ActionFrame(f), Round(f.Shield),
            f.Invulnerability > 0 || f.DodgeInvulnerability > 0 || f.RespawnTicks > 0,
            f.Connected, f.IsBot, f.Grounded, Math.Max(0, 2 - f.JumpsUsed),
            Math.Clamp(f.ChargeTicks / 72d, 0, 1), f.KOs, f.Falls, f.LastSequence,
            f.Shielding, f.Stocks <= 0, f.Appearance, new ArenaCombatSnapshot(Round(f.Energy), Round(f.Ultimate),
                new(Round(f.PulseCooldown * Dt), Round(f.GrabCooldown * Dt), Round(f.DashCooldown * Dt),
                    Round(f.RecoveryCooldown * Dt), Round(f.RepairCooldown * Dt), Round(f.UltimateCooldown * Dt)),
                Round(f.ChannelTicks * Dt), f.ArmorHits > 0, f.CapturedBy?.Id, ActiveAbility(f)))).ToArray(),
        projectiles.Select(p => new ArenaProjectileSnapshot(p.Id, p.Owner.Id, Round(p.X),
            Round(p.Y), Round(p.Vx), Round(p.Vy), p.Radius, p.Kind == "join" ? "#ffd56b" : p.Kind == "reflected" ? "#5ee8ff" :
                p.SourceAvatarId == AvatarEconomy.Bip01Id ? "#5ee8ff" : p.SourceAvatarId == AvatarEconomy.RaymondId ? "#65a7ff" : p.SourceAvatarId == AvatarEconomy.SantaId ? "#f2d177" : p.SourceAvatarId == AvatarEconomy.LuppId ? "#c4a1ff" : p.Owner.Color,
            p.Kind, p.SourceAvatarId, p.Reflections > 0)).ToArray(),
        events.ToArray(), winnerId, suddenDeath);

    private Fighter? Find(string id) => fighters.FirstOrDefault(x => x.Id == id);
    private static double Round(double value) => Math.Round(value, 2);
    private static bool IsBip(Fighter f) => f.AvatarId == AvatarEconomy.Bip01Id;
    private static bool IsRay(Fighter f) => f.AvatarId == AvatarEconomy.RaymondId;
    private static bool IsSanta(Fighter f) => f.AvatarId == AvatarEconomy.SantaId;
    private static bool IsLupp(Fighter f) => f.AvatarId == AvatarEconomy.LuppId;
    private static bool HasKit(Fighter f) => IsBip(f) || IsRay(f) || IsSanta(f) || IsLupp(f);
    private static double Approach(double current, double target, double amount) =>
        current < target ? Math.Min(current + amount, target) : Math.Max(current - amount, target);

    private void StepFighter(Fighter fighter)
    {
        if (fighter.Stocks <= 0) return;
        if (fighter.RespawnTicks > 0)
        {
            if (--fighter.RespawnTicks == 0)
            {
                ResetCombat(fighter);
                Place(fighter, fighters.IndexOf(fighter), true);
                fighter.Invulnerability = 150;
                Emit("respawn", fighter.X, fighter.Y - 40, fighter.Id);
            }
            return;
        }
        StepKitTimers(fighter);
        fighter.Invulnerability = Math.Max(0, fighter.Invulnerability - 1);
        fighter.DodgeInvulnerability = Math.Max(0, fighter.DodgeInvulnerability - 1);
        fighter.DodgeCooldown = Math.Max(0, fighter.DodgeCooldown - 1);
        fighter.SpecialCooldown = Math.Max(0, fighter.SpecialCooldown - 1);
        fighter.LedgeCooldown = Math.Max(0, fighter.LedgeCooldown - 1);
        fighter.DropTicks = Math.Max(0, fighter.DropTicks - 1);
        fighter.JumpBuffer = Math.Max(0, fighter.JumpBuffer - 1);
        fighter.Coyote = fighter.Grounded ? 6 : Math.Max(0, fighter.Coyote - 1);
        if (fighter.CapturedBy is not null)
        {
            StepCaptured(fighter);
            return;
        }
        if (fighter.Input.Jump && !fighter.PreviousInput.Jump) fighter.JumpBuffer = 7;
        if (fighter.Hitstop > 0) { fighter.Hitstop--; return; }

        StepChannel(fighter);
        StepCapture(fighter);

        if (fighter.Ledge is { } ledge)
        {
            fighter.Action = "interact";
            fighter.ActionTicks = 2;
            fighter.Vx = fighter.Vy = 0;
            fighter.LedgeTicks++;
            var inward = fighter.LedgeSide;
            var edgeX = inward > 0 ? ledge.X : ledge.X + ledge.Width;
            if (fighter.JumpBuffer > 0)
            {
                fighter.Ledge = null;
                fighter.JumpsUsed = 1;
                fighter.Vy = -700;
                fighter.Vx = inward * 150;
                fighter.Y -= 8;
                fighter.JumpBuffer = 0;
                fighter.ActionTicks = 0;
                Emit("jump", fighter.X, fighter.Y, fighter.Id);
            }
            else if (fighter.Input.Vertical > .5 || fighter.Input.Horizontal * inward < -.5)
            {
                fighter.Ledge = null;
                fighter.ActionTicks = 0;
                fighter.Vx = -inward * 150;
                fighter.Vy = 100;
            }
            else if (fighter.Input.Horizontal * inward > .3 || fighter.LedgeTicks > 40 || fighter.Input.Attack)
            {
                fighter.Ledge = null;
                fighter.X = edgeX + inward * 40;
                fighter.Y = ledge.Y;
                fighter.Grounded = true;
                fighter.Action = "land";
                fighter.ActionTicks = 10;
                fighter.Invulnerability = 15;
            }
            else return;
        }

        var stunned = fighter.Hitstun > 0;
        if (stunned) fighter.Hitstun--;
        if (fighter.Attack is { } attack)
        {
            attack.Age++;
            if (attack.Age >= attack.Startup + attack.Active + attack.Recovery) fighter.Attack = null;
        }
        fighter.ActionTicks = Math.Max(0, fighter.ActionTicks - 1);
        var busy = fighter.ChannelTicks > 0 || fighter.FieldTicks > 0 || fighter.CapturedTarget is not null;
        var free = !stunned && !busy && fighter.Attack is null && fighter.ActionTicks == 0;
        fighter.Shielding = free && fighter.Grounded && fighter.Input.Shield && fighter.Shield > 0;
        fighter.ShieldAge = fighter.Shielding ? fighter.ShieldAge + 1 : 0;
        if (fighter.Shielding)
        {
            fighter.Shield = Math.Max(0, fighter.Shield - (IsRay(fighter) ? .34 : .28));
            if (fighter.Shield <= 0) BreakShield(fighter);
        }
        else if (!stunned) fighter.Shield = Math.Min(100, fighter.Shield + 0.13);

        if (free && fighter.DodgeCooldown == 0 &&
            (fighter.Input.Dodge && !fighter.PreviousInput.Dodge ||
             fighter.Input.Shield && !fighter.PreviousInput.Shield &&
                (!fighter.Grounded || Math.Abs(fighter.Input.Horizontal) > .5)))
        {
            fighter.Shielding = false;
            fighter.Ability = null;
            fighter.DodgeInvulnerability = fighter.Grounded ? 16 : 13;
            fighter.ActionTicks = 26;
            fighter.DodgeCooldown = 64;
            fighter.Vx = fighter.Input.Horizontal * 540;
            if (!fighter.Grounded) fighter.Vy = fighter.Input.Vertical * 300;
            SetAction(fighter, "ability_4_dash", 26);
            Emit("dodge", fighter.X, fighter.Y - 35, fighter.Id);
            free = false;
        }

        if (free && !fighter.Shielding)
        {
            if (Math.Abs(fighter.Input.Horizontal) > .12 && fighter.ChargeTicks == 0)
                fighter.Facing = fighter.Input.Horizontal > 0 ? 1 : -1;
            if (fighter.Grounded && fighter.Input.Vertical > .65 && fighter.JumpBuffer > 0 &&
                stage.Platforms.Any(p => p.OneWay && Math.Abs(fighter.Y - p.Y) < 1 &&
                    fighter.X >= p.X - HalfWidth && fighter.X <= p.X + p.Width + HalfWidth))
            {
                fighter.DropTicks = 15;
                fighter.Grounded = false;
                fighter.Y += 5;
                fighter.Vy = 100;
                fighter.JumpBuffer = 0;
                fighter.Coyote = 0;
            }
            else if (fighter.JumpBuffer > 0 && (fighter.Coyote > 0 || fighter.JumpsUsed < 2))
            {
                if (fighter.Coyote > 0) fighter.JumpsUsed = 0;
                fighter.JumpsUsed++;
                fighter.Vy = fighter.JumpsUsed == 1 ? -700 : -650;
                fighter.Grounded = false;
                fighter.Coyote = fighter.JumpBuffer = 0;
                Emit("jump", fighter.X, fighter.Y, fighter.Id, strength: fighter.JumpsUsed);
            }

            if (fighter.Input.Grab && !fighter.PreviousInput.Grab)
            {
                fighter.ChargeTicks = 0;
                Grab(fighter);
            }
            else if (fighter.Input.Special && !fighter.PreviousInput.Special &&
                (HasKit(fighter) || fighter.SpecialCooldown == 0))
            {
                fighter.ChargeTicks = 0;
                Special(fighter);
            }
            else if (HasKit(fighter) && fighter.Input.Smash && !fighter.PreviousInput.Smash)
                BeginUltimate(fighter);
            else if (!HasKit(fighter) && fighter.Grounded && fighter.Input.Smash)
            {
                fighter.ChargeTicks = Math.Min(72, fighter.ChargeTicks + 1);
                fighter.Action = "ultimate_charge";
                if (fighter.ChargeTicks >= 72) ReleaseSmash(fighter);
            }
            else if (!HasKit(fighter) && fighter.ChargeTicks > 0) ReleaseSmash(fighter);
            else if (fighter.Input.Attack && !fighter.PreviousInput.Attack ||
                !HasKit(fighter) && !fighter.Grounded && fighter.Input.Smash && !fighter.PreviousInput.Smash)
            {
                var aerial = !fighter.Grounded;
                fighter.Ability = null;
                fighter.Attack = IsRay(fighter)
                    ? MakeAttack(fighter, aerial ? "aerial" : "normal", aerial ? 9 : 10, 7, 24,
                        aerial ? 12 : 11, aerial ? 390 : 365, aerial ? 125 : 135)
                    : IsBip(fighter)
                        ? MakeAttack(fighter, aerial ? "aerial" : "normal", 3, aerial ? 5 : 3, 12,
                            aerial ? 8 : 6, aerial ? 300 : 240, aerial ? 88 : 78)
                        : IsSanta(fighter)
                            ? MakeAttack(fighter, aerial ? "aerial" : "normal", 6, 5, 18,
                                aerial ? 9 : 8, aerial ? 315 : 285, 98)
                            : IsLupp(fighter)
                                ? MakeAttack(fighter, aerial ? "aerial" : "normal", 4, 4, 15,
                                    aerial ? 8 : 7, aerial ? 305 : 260, aerial ? 92 : 85)
                            : MakeAttack(fighter, aerial ? "aerial" : "normal",
                            aerial ? 5 : 4, aerial ? 6 : 4, aerial ? 16 : 14,
                            aerial ? 9 : 7, aerial ? 325 : 270, aerial ? 88 : 82);
                SetAction(fighter, "ability_1_pulse", AttackDuration(fighter.Attack));
            }
        }

        if (stunned)
        {
            // Directional influence is bounded and cannot erase a launch.
            fighter.Vx += fighter.Input.Horizontal * 145 * Dt;
            fighter.Vy += fighter.Input.Vertical * 75 * Dt;
        }
        else if (fighter.ChannelTicks > 0 || fighter.CapturedTarget is not null || fighter.FieldTicks > 0)
            fighter.Vx = Approach(fighter.Vx, 0, 3500 * Dt);
        else if (fighter.DashTicks > 0)
            fighter.Vx = fighter.Facing * (IsLupp(fighter) ? 710 : IsSanta(fighter) ? 650 : IsRay(fighter) ? 465 : 860);
        else if (fighter.ActionTicks == 0 || fighter.DodgeInvulnerability == 0)
        {
            var speed = IsLupp(fighter) ? 340 : IsSanta(fighter) ? 320 : IsRay(fighter) ? 305 : IsBip(fighter) ? 350 : 330;
            var target = fighter.Shielding ? 0 : fighter.Input.Horizontal * speed;
            if (fighter.Attack is not null) target *= .35;
            if (fighter.ChargeTicks > 0) target = 0;
            fighter.Vx = Approach(fighter.Vx, target,
                (fighter.Grounded ? Math.Abs(target) > 1 ? 2100 : 2700 : 820) * Dt);
        }

        var previousY = fighter.Y;
        // Fields apply after input acceleration/friction. Otherwise a grounded
        // fighter's normal braking would erase a weaker pull before it moved.
        fighter.Vx = Math.Clamp(fighter.Vx + fighter.ExternalVx, -1800, 1800);
        fighter.Vy = Math.Clamp(fighter.Vy + fighter.ExternalVy, -1600, 1400);
        fighter.ExternalVx = fighter.ExternalVy = 0;
        fighter.Vy = Math.Min(1200, fighter.Vy + (fighter.Input.Vertical > .65 && !stunned ? 2650 : 1800) * Dt);
        // Releasing jump cuts only the first jump's rise; knockback is untouched.
        if (!stunned && !fighter.Input.Jump && fighter.PreviousInput.Jump && fighter.Vy < -280 && !fighter.UsedRecovery)
            fighter.Vy = -280;
        fighter.X += fighter.Vx * Dt;
        fighter.Y += fighter.Vy * Dt;
        fighter.Grounded = false;
        if (fighter.Vy >= 0)
        {
            foreach (var platform in stage.Platforms.OrderBy(p => p.Y))
            {
                if (platform.OneWay && fighter.DropTicks > 0) continue;
                if (fighter.X + HalfWidth < platform.X || fighter.X - HalfWidth > platform.X + platform.Width) continue;
                if (previousY <= platform.Y + .8 && fighter.Y >= platform.Y)
                {
                    fighter.Y = platform.Y;
                    fighter.Vy = 0;
                    fighter.Grounded = true;
                    fighter.JumpsUsed = 0;
                    fighter.UsedRecovery = false;
                    fighter.UsedAirSpecial = false;
                    break;
                }
            }
        }
        if (!fighter.Grounded && fighter.Vy > 0 && fighter.LedgeCooldown == 0 &&
            fighter.Hitstun == 0 && fighter.Attack is null && fighter.Input.Vertical < .5)
        {
            foreach (var platform in stage.Platforms.Where(p => !p.OneWay))
            {
                if (fighter.Y < platform.Y + 5 || fighter.Y > platform.Y + 80) continue;
                var left = fighter.X < platform.X && fighter.X > platform.X - 46;
                var right = fighter.X > platform.X + platform.Width && fighter.X < platform.X + platform.Width + 46;
                if (!left && !right) continue;
                fighter.Ledge = platform;
                fighter.LedgeSide = left ? 1 : -1;
                fighter.LedgeTicks = 0;
                fighter.LedgeCooldown = 105;
                fighter.X = left ? platform.X - 26 : platform.X + platform.Width + 26;
                fighter.Y = platform.Y + 40;
                fighter.Vx = fighter.Vy = 0;
                fighter.Invulnerability = 45;
                fighter.JumpsUsed = 0;
                fighter.UsedRecovery = fighter.UsedAirSpecial = false;
                Emit("ledge", fighter.X, fighter.Y - 30, fighter.Id);
                break;
            }
        }
    }

    private Attack MakeAttack(Fighter fighter, string kind, int startup, int active, int recovery,
        double damage, double force, double reach)
    {
        var horizontal = fighter.Facing;
        var vertical = fighter.Input.Vertical < -.5 ? -1 : fighter.Input.Vertical > .5 ? 1 : 0;
        return new Attack
        {
            Kind = kind, Startup = startup, Active = active, Recovery = recovery,
            Damage = damage, Force = force, Reach = reach,
            DirectionX = vertical == 0 ? horizontal : horizontal * .28,
            DirectionY = vertical == 0 ? -.45 : vertical,
            OffsetX = vertical == 0 ? horizontal * 52 : 0,
            OffsetY = vertical < 0 ? -92 : vertical > 0 ? -10 : -35
        };
    }

    private void ReleaseSmash(Fighter fighter)
    {
        var charge = fighter.ChargeTicks / 72d;
        fighter.Attack = MakeAttack(fighter, "smash", 8, 5, 26, 13 + 12 * charge, 410 + 280 * charge, 106);
        fighter.ChargeTicks = 0;
        fighter.Action = "ultimate_release";
        fighter.Invulnerability = 0;
    }

    private void Grab(Fighter fighter)
    {
        // Raymond's planted stance is essential to the short capture: aerial
        // recovery uses his specials rather than an airborne immobilizing grab.
        if (IsRay(fighter) && !fighter.Grounded) return;
        if (!HasKit(fighter))
        {
            fighter.Attack = MakeAttack(fighter, "grab", 3, 4, 22, 7, 470, 74);
            SetAction(fighter, "ability_2_magnet", 29);
            return;
        }
        if (fighter.GrabCooldown > 0) return;
        if (IsLupp(fighter))
        {
            if (!SpendEnergy(fighter, 16)) return;
            fighter.GrabCooldown = 84;
            fighter.Attack = MakeAttack(fighter, "feedback", 4, 5, 22, 6, 320, 90);
            fighter.Ability = "feedback";
            fighter.Invulnerability = 0;
            SetAction(fighter, "ability_2_magnet", 31);
            Emit("feedback", fighter.X + fighter.Facing * 50, fighter.Y - 35, fighter.Id);
            return;
        }
        if (IsSanta(fighter))
        {
            if (!SpendEnergy(fighter, 20)) return;
            fighter.GrabCooldown = 110;
            fighter.Attack = MakeAttack(fighter, "gift-grab", 6, 5, 24, 5, 340, 112);
            fighter.Ability = "gift-grab";
            fighter.Invulnerability = 0;
            SetAction(fighter, "ability_2_magnet", 35);
            Emit("gift-grab", fighter.X + fighter.Facing * 65, fighter.Y - 35, fighter.Id);
            return;
        }
        if (IsBip(fighter))
        {
            if (!SpendEnergy(fighter, 18)) return;
            fighter.GrabCooldown = 90;
            fighter.FieldTicks = 16;
            fighter.ActionTicks = 28;
            fighter.Ability = "magnet";
            SetAction(fighter, "ability_2_magnet", 28);
            Emit("magnet", fighter.X + fighter.Facing * 75, fighter.Y - 35, fighter.Id);
        }
        else
        {
            fighter.GrabCooldown = 100;
            fighter.Attack = MakeAttack(fighter, "capture", 8, 5, 24, 0, 0, 95);
            fighter.Ability = "capture";
            SetAction(fighter, "ability_2_magnet", 37);
        }
        fighter.Invulnerability = 0;
    }

    private void Special(Fighter fighter)
    {
        if (!HasKit(fighter)) { LegacySpecial(fighter); return; }
        if (IsLupp(fighter)) { LuppSpecial(fighter); return; }
        if (IsSanta(fighter)) { SantaSpecial(fighter); return; }
        if (fighter.Input.Vertical < -.5)
        {
            if (fighter.UsedRecovery || fighter.RecoveryCooldown > 0 || IsBip(fighter) && !SpendEnergy(fighter, 15)) return;
            fighter.UsedRecovery = true;
            fighter.RecoveryCooldown = IsBip(fighter) ? 45 : 64;
            fighter.Vy = IsBip(fighter) ? -900 : -715;
            fighter.Vx = fighter.Input.Horizontal * (IsBip(fighter) ? 180 : 330);
            fighter.Grounded = false;
            fighter.Attack = MakeAttack(fighter, "recovery", IsBip(fighter) ? 2 : 7,
                IsBip(fighter) ? 13 : 17, IsBip(fighter) ? 16 : 25, IsBip(fighter) ? 9 : 14,
                IsBip(fighter) ? 320 : 410, IsBip(fighter) ? 82 : 105);
            fighter.Attack.OffsetY = -75;
            fighter.Attack.DirectionX = fighter.Facing * (IsBip(fighter) ? .15 : .5);
            fighter.Attack.DirectionY = -1;
            fighter.Ability = "recovery";
            SetAction(fighter, "ability_4_dash", AttackDuration(fighter.Attack));
            Emit("recovery", fighter.X, fighter.Y, fighter.Id);
        }
        else if (fighter.Input.Vertical > .5)
        {
            if (fighter.RepairCooldown > 0 || !fighter.Grounded || fighter.Damage <= 0) return;
            if (IsBip(fighter))
            {
                if (!SpendEnergy(fighter, 40)) return;
                fighter.RepairCooldown = 360;
                fighter.ChannelTicks = fighter.ChannelTotal = 36;
            }
            else
            {
                if (fighter.Shield < 40) return;
                fighter.Shield -= 40;
                fighter.RepairCooldown = 480;
                fighter.ChannelTicks = fighter.ChannelTotal = 54;
            }
            fighter.Channel = "repair";
            fighter.Ability = "repair";
            fighter.ActionTicks = fighter.ChannelTicks;
            fighter.Vx = 0;
            SetAction(fighter, "ability_5_repair", fighter.ChannelTicks);
            Emit("repair-start", fighter.X, fighter.Y - 35, fighter.Id);
        }
        else if (Math.Abs(fighter.Input.Horizontal) > .5)
        {
            if (fighter.DashCooldown > 0 || !fighter.Grounded && fighter.UsedAirSpecial || IsBip(fighter) && !SpendEnergy(fighter, 18)) return;
            fighter.UsedAirSpecial = true;
            fighter.DashCooldown = IsBip(fighter) ? 64 : 120;
            fighter.DashTicks = IsBip(fighter) ? 12 : 29;
            fighter.Vx = fighter.Facing * (IsBip(fighter) ? 860 : 465);
            fighter.Vy = Math.Min(fighter.Vy, 0);
            fighter.Attack = MakeAttack(fighter, "dash", IsBip(fighter) ? 2 : 8,
                IsBip(fighter) ? 10 : 25, IsBip(fighter) ? 18 : 35,
                IsBip(fighter) ? 10 : 16, IsBip(fighter) ? 345 : 470, IsBip(fighter) ? 84 : 112);
            fighter.DodgeInvulnerability = IsBip(fighter) ? 5 : 0;
            fighter.ArmorHits = IsRay(fighter) ? 1 : 0;
            fighter.Ability = IsBip(fighter) ? "rocket" : "charge";
            SetAction(fighter, "ability_4_dash", AttackDuration(fighter.Attack));
        }
        else
        {
            if (fighter.PulseCooldown > 0 || IsBip(fighter) && !SpendEnergy(fighter, 12)) return;
            fighter.PulseCooldown = IsBip(fighter) ? 40 : 62;
            fighter.ActionTicks = IsBip(fighter) ? 21 : 34;
            fighter.Ability = "pulse";
            SetAction(fighter, "ability_1_pulse", fighter.ActionTicks);
            projectiles.Add(new Projectile
            {
                Id = ++projectileId, Owner = fighter, SourceAvatarId = fighter.AvatarId,
                X = fighter.X + fighter.Facing * 44, Y = fighter.Y - 42,
                Vx = fighter.Facing * (IsBip(fighter) ? 720 : 425), Vy = 0,
                Radius = IsBip(fighter) ? 12 : 24,
                Damage = IsBip(fighter) ? 7 : 13, Force = IsBip(fighter) ? 250 : 370,
                Life = IsBip(fighter) ? 68 : 88
            });
            Emit("projectile", fighter.X, fighter.Y - 42, fighter.Id);
        }
        fighter.Invulnerability = 0;
    }

    // Santa trades energy for arcing stars, a shield-bypassing lift and a
    // gliding dash. His kit has independent costs/timings, not starter defaults.
    private void SantaSpecial(Fighter fighter)
    {
        if (fighter.Input.Vertical < -.5)
        {
            if (fighter.UsedRecovery || fighter.RecoveryCooldown > 0 || !SpendEnergy(fighter, 16)) return;
            fighter.UsedRecovery = true;
            fighter.RecoveryCooldown = 55;
            fighter.Vy = -830;
            fighter.Vx = fighter.Input.Horizontal * 260;
            fighter.Grounded = false;
            fighter.Attack = MakeAttack(fighter, "recovery", 5, 12, 22, 8, 355, 94);
            fighter.Attack.OffsetY = -75;
            fighter.Attack.DirectionX = fighter.Facing * .3;
            fighter.Attack.DirectionY = -1;
            fighter.Ability = "recovery";
            SetAction(fighter, "ability_4_dash", AttackDuration(fighter.Attack));
            Emit("recovery", fighter.X, fighter.Y, fighter.Id);
        }
        else if (fighter.Input.Vertical > .5)
        {
            if (fighter.RepairCooldown > 0 || !fighter.Grounded || fighter.Damage <= 0 || !SpendEnergy(fighter, 45)) return;
            fighter.RepairCooldown = 480;
            fighter.ChannelTicks = fighter.ChannelTotal = 48;
            fighter.Channel = fighter.Ability = "repair";
            fighter.ActionTicks = 48;
            fighter.Vx = 0;
            SetAction(fighter, "ability_5_repair", 48);
            Emit("repair-start", fighter.X, fighter.Y - 35, fighter.Id);
        }
        else if (Math.Abs(fighter.Input.Horizontal) > .5)
        {
            if (fighter.DashCooldown > 0 || !fighter.Grounded && fighter.UsedAirSpecial || !SpendEnergy(fighter, 22)) return;
            fighter.UsedAirSpecial = true;
            fighter.DashCooldown = 96;
            fighter.DashTicks = 18;
            fighter.Vx = fighter.Facing * 650;
            fighter.Vy = Math.Min(fighter.Vy, -110);
            fighter.Attack = MakeAttack(fighter, "dash", 4, 14, 20, 11, 365, 94);
            fighter.Ability = "sleigh";
            SetAction(fighter, "ability_4_dash", AttackDuration(fighter.Attack));
        }
        else
        {
            if (fighter.PulseCooldown > 0 || !SpendEnergy(fighter, 14)) return;
            fighter.PulseCooldown = 48;
            fighter.ActionTicks = 26;
            fighter.Ability = "star";
            fighter.PendingStarTicks = 13; // authored release frame4 of the26-tick action
            SetAction(fighter, "ability_1_pulse", 26);
        }
        fighter.Invulnerability = 0;
    }

    private void ReleaseSantaStar(Fighter fighter)
    {
        if (!IsSanta(fighter) || !fighter.Connected || fighter.Stocks <= 0 || fighter.RespawnTicks > 0 ||
            fighter.Hitstun > 0 || fighter.Action != "ability_1_pulse" || fighter.Ability != "star") return;
        projectiles.Add(new Projectile
        {
            Id = ++projectileId, Owner = fighter, SourceAvatarId = fighter.AvatarId, Kind = "star",
            X = fighter.X + fighter.Facing * 48, Y = fighter.Y - 44,
            Vx = fighter.Facing * 590, Vy = -70, Gravity = 140,
            Radius = 16, Damage = 9, Force = 295, Life = 78
        });
        Emit("projectile", fighter.X, fighter.Y - 44, fighter.Id);
    }

    private void LuppSpecial(Fighter fighter)
    {
        if (fighter.Input.Vertical < -.5)
        {
            if (fighter.UsedRecovery || fighter.RecoveryCooldown > 0 || !SpendEnergy(fighter, 18)) return;
            fighter.UsedRecovery = true;
            fighter.RecoveryCooldown = 50;
            fighter.Vy = -850;
            fighter.Vx = fighter.Input.Horizontal * 300;
            fighter.Grounded = false;
            fighter.Attack = MakeAttack(fighter, "recovery", 3, 11, 20, 7, 310, 86);
            fighter.Attack.OffsetY = -72;
            fighter.Attack.DirectionX = fighter.Facing * .25;
            fighter.Attack.DirectionY = -1;
            fighter.Ability = "recovery";
            SetAction(fighter, "ability_4_dash", AttackDuration(fighter.Attack));
            Emit("recovery", fighter.X, fighter.Y, fighter.Id);
        }
        else if (fighter.Input.Vertical > .5)
        {
            if (fighter.RepairCooldown > 0 || !fighter.Grounded || fighter.Damage <= 0 || !SpendEnergy(fighter, 30)) return;
            fighter.RepairCooldown = 420;
            fighter.ChannelTicks = fighter.ChannelTotal = 42;
            fighter.Channel = fighter.Ability = "repair";
            fighter.ActionTicks = 42;
            fighter.Vx = 0;
            SetAction(fighter, "ability_5_repair", 42);
            Emit("repair-start", fighter.X, fighter.Y - 35, fighter.Id);
        }
        else if (Math.Abs(fighter.Input.Horizontal) > .5)
        {
            if (fighter.DashCooldown > 0 || !fighter.Grounded && fighter.UsedAirSpecial || !SpendEnergy(fighter, 20)) return;
            fighter.UsedAirSpecial = true;
            fighter.DashCooldown = 82;
            fighter.DashTicks = 15;
            fighter.Vx = fighter.Facing * 710;
            fighter.Vy = Math.Min(fighter.Vy, -70);
            fighter.Attack = MakeAttack(fighter, "dash", 3, 12, 22, 9, 325, 86);
            fighter.DodgeInvulnerability = 3;
            fighter.Ability = "iterate";
            SetAction(fighter, "ability_4_dash", AttackDuration(fighter.Attack));
        }
        else
        {
            if (fighter.PulseCooldown > 0 || !SpendEnergy(fighter, 12)) return;
            fighter.PulseCooldown = 44;
            fighter.ActionTicks = 28;
            fighter.Ability = "idea";
            fighter.PendingIdeaTicks = 14; // authored release frame4 of the28-tick action
            SetAction(fighter, "ability_1_pulse", 28);
        }
        fighter.Invulnerability = 0;
    }

    private void ReleaseLuppIdea(Fighter fighter)
    {
        if (!IsLupp(fighter) || !fighter.Connected || fighter.Stocks <= 0 || fighter.RespawnTicks > 0 ||
            fighter.Hitstun > 0 || fighter.Action != "ability_1_pulse" || fighter.Ability != "idea") return;
        projectiles.Add(new Projectile
        {
            Id = ++projectileId, Owner = fighter, SourceAvatarId = fighter.AvatarId, Kind = "idea",
            // Authored frame4 hand (238.5,133) relative to (128,224), rendered118px/256px.
            X = fighter.X + fighter.Facing * 50.93359375, Y = fighter.Y - 41.9453125,
            Vx = fighter.Facing * 560, Vy = -110, Gravity = 380,
            Radius = 14, Damage = 8, Force = 275, Life = 85, BouncesRemaining = 1
        });
        Emit("projectile", fighter.X, fighter.Y - 43, fighter.Id);
    }

    private void LegacySpecial(Fighter fighter)
    {
        if (fighter.Input.Vertical < -.5)
        {
            if (fighter.UsedRecovery) return;
            fighter.UsedRecovery = true;
            fighter.Vy = -790;
            fighter.Vx = fighter.Input.Horizontal * 210;
            fighter.Grounded = false;
            fighter.Attack = MakeAttack(fighter, "recovery", 2, 15, 14, 11, 340, 84);
            fighter.Attack.OffsetY = -75;
            fighter.Attack.DirectionX = fighter.Facing * .25;
            fighter.Attack.DirectionY = -1;
            fighter.Action = "ability_4_dash";
            fighter.SpecialCooldown = 28;
            Emit("recovery", fighter.X, fighter.Y, fighter.Id);
        }
        else if (fighter.Input.Vertical > .5)
        {
            fighter.Attack = MakeAttack(fighter, "shockwave", 12, 8, 26, 13, 370, 130);
            fighter.Attack.OffsetX = 0;
            fighter.Attack.OffsetY = -25;
            fighter.Attack.DirectionY = -.8;
            fighter.Shield = Math.Min(100, fighter.Shield + 14);
            fighter.Action = "ability_5_repair";
            fighter.SpecialCooldown = 100;
        }
        else if (Math.Abs(fighter.Input.Horizontal) > .5)
        {
            if (!fighter.Grounded && fighter.UsedAirSpecial) return;
            fighter.UsedAirSpecial = true;
            fighter.Vx = fighter.Facing * 720;
            fighter.Vy = Math.Min(fighter.Vy, 0);
            fighter.Attack = MakeAttack(fighter, "dash", 2, 12, 23, 12, 390, 86);
            fighter.ActionTicks = 14;
            fighter.DodgeInvulnerability = 5;
            fighter.Action = "ability_4_dash";
            fighter.SpecialCooldown = 52;
        }
        else
        {
            fighter.Action = "ability_1_pulse";
            fighter.ActionTicks = 26;
            fighter.SpecialCooldown = fighter.AvatarId == AvatarEconomy.Bip01Id ? 43 : 52;
            var rayon = fighter.AvatarId == AvatarEconomy.RaymondId;
            projectiles.Add(new Projectile
            {
                Id = ++projectileId, Owner = fighter, X = fighter.X + fighter.Facing * 42,
                Y = fighter.Y - 42, Vx = fighter.Facing * (rayon ? 490 : 660),
                Vy = 0,
                Radius = rayon ? 21 : 13, Damage = rayon ? 11 : 7, Force = rayon ? 340 : 245,
                Life = rayon ? 48 : 66
            });
            Emit("projectile", fighter.X, fighter.Y - 42, fighter.Id);
        }
        fighter.Invulnerability = 0;
    }

    private static int AttackDuration(Attack attack) => attack.Startup + attack.Active + attack.Recovery;

    private void SetAction(Fighter fighter, string action, int duration)
    {
        fighter.Action = action;
        fighter.ActionStartTick = frame;
        fighter.ActionDuration = Math.Max(1, duration);
    }

    private static bool SpendEnergy(Fighter fighter, double amount)
    {
        if (fighter.Energy < amount) return false;
        fighter.Energy = Math.Clamp(fighter.Energy - amount, 0, 100);
        return true;
    }

    private static string? ActiveAbility(Fighter f) => f.CapturedBy is not null ? "captured" :
        f.Shielding && IsBip(f) ? "reflect-shield" : f.Shielding && IsRay(f) ? "brace-shield" : f.Shielding && IsSanta(f) ? "peace-shield" : f.Shielding && IsLupp(f) ? "prototype-shield" :
        f.ChannelTicks > 0 || f.FieldTicks > 0 || f.CapturedTarget is not null || f.Attack is not null || f.ActionTicks > 0 ? f.Ability : null;

    private void StepKitTimers(Fighter fighter)
    {
        if (fighter.PendingStarTicks > 0 && --fighter.PendingStarTicks == 0) ReleaseSantaStar(fighter);
        if (fighter.PendingIdeaTicks > 0 && --fighter.PendingIdeaTicks == 0) ReleaseLuppIdea(fighter);
        if (fighter.PendingJamTicks > 0 && --fighter.PendingJamTicks == 0) ReleaseLuppGameJam(fighter);
        fighter.PulseCooldown = Math.Max(0, fighter.PulseCooldown - 1);
        fighter.GrabCooldown = Math.Max(0, fighter.GrabCooldown - 1);
        fighter.DashCooldown = Math.Max(0, fighter.DashCooldown - 1);
        fighter.RecoveryCooldown = Math.Max(0, fighter.RecoveryCooldown - 1);
        fighter.RepairCooldown = Math.Max(0, fighter.RepairCooldown - 1);
        fighter.UltimateCooldown = Math.Max(0, fighter.UltimateCooldown - 1);
        fighter.FieldTicks = Math.Max(0, fighter.FieldTicks - 1);
        fighter.DashTicks = Math.Max(0, fighter.DashTicks - 1);
        if (fighter.DashTicks == 0) fighter.ArmorHits = 0;
        if ((IsBip(fighter) || IsSanta(fighter) || IsLupp(fighter)) && fighter.Channel != "repair")
            fighter.Energy = Math.Min(100, fighter.Energy + (IsLupp(fighter) ? 7 : IsSanta(fighter) ? 6 : 9) * Dt);
    }

    private void BeginUltimate(Fighter fighter)
    {
        if (!HasKit(fighter) || fighter.Ultimate < 100 || fighter.UltimateCooldown > 0) return;
        fighter.Ultimate = 0;
        fighter.UltimateCooldown = IsLupp(fighter) ? 690 : IsSanta(fighter) ? 660 : 600;
        fighter.ChannelTicks = fighter.ChannelTotal = IsLupp(fighter) ? 54 : IsSanta(fighter) ? 48 : 36;
        fighter.Channel = IsLupp(fighter) ? "game-jam" : IsSanta(fighter) ? "gift-fest" : IsBip(fighter) ? "overload" : "join";
        fighter.Ability = fighter.Channel + "-charge";
        fighter.ActionTicks = fighter.ChannelTicks;
        fighter.Vx = 0;
        fighter.Invulnerability = 0;
        SetAction(fighter, "ultimate_charge", fighter.ChannelTicks);
        Emit("ultimate-charge", fighter.X, fighter.Y - 35, fighter.Id);
    }

    private void StepChannel(Fighter fighter)
    {
        if (fighter.ChannelTicks <= 0) return;
        if (fighter.Channel == "repair" &&
            (!fighter.Grounded || Math.Abs(fighter.Input.Horizontal) > .15 || fighter.Input.Jump ||
             fighter.Input.Shield || fighter.Input.Attack || fighter.Input.Grab || fighter.Input.Dodge ||
             fighter.Input.Special && !fighter.PreviousInput.Special))
        {
            CancelAbility(fighter);
            return;
        }
        fighter.ChannelTicks--;
        var earlyRelease = fighter.Channel != "repair" && fighter.ChannelTotal - fighter.ChannelTicks >= 12 && !fighter.Input.Smash;
        if (fighter.ChannelTicks > 0 && !earlyRelease) return;
        var channel = fighter.Channel;
        fighter.Channel = null;
        fighter.ChannelTicks = 0;
        fighter.ActionTicks = 0;
        if (channel == "repair")
        {
            var healed = Math.Min(fighter.Damage, IsLupp(fighter) ? 10 : IsSanta(fighter) ? 12 : IsBip(fighter) ? 8 : 14);
            fighter.Damage = Math.Max(0, fighter.Damage - healed);
            if (IsSanta(fighter)) fighter.Shield = Math.Min(100, fighter.Shield + 12);
            fighter.Ability = null;
            Emit("repair", fighter.X, fighter.Y - 35, fighter.Id, strength: healed);
        }
        else if (channel == "overload")
        {
            fighter.Energy = 0;
            fighter.Ability = "overload";
            fighter.Attack = MakeAttack(fighter, "overload", 0, 6, 30, 26, 620, 205);
            fighter.Attack.RadialRadius = 205;
            fighter.Attack.OffsetX = 0;
            fighter.Attack.OffsetY = -35;
            SetAction(fighter, "ultimate_release", 36);
            Emit("overload", fighter.X, fighter.Y - 35, fighter.Id, strength: 2.2);
        }
        else if (channel == "gift-fest")
        {
            fighter.Ability = "gift-fest";
            fighter.ActionTicks = 40;
            SetAction(fighter, "ultimate_release", 40);
            for (var index = -1; index <= 1; index++)
                projectiles.Add(new Projectile
                {
                    Id = ++projectileId, Owner = fighter, SourceAvatarId = fighter.AvatarId, Kind = "gift",
                    X = fighter.X + fighter.Facing * 56, Y = fighter.Y - 48 + index * 22,
                    Vx = fighter.Facing * 480 * Math.Cos(index * .32), Vy = 480 * Math.Sin(index * .32),
                    Gravity = 70, Radius = 18, Damage = 11, Force = 430, Life = 90
                });
            Emit("gift-fest", fighter.X + fighter.Facing * 56, fighter.Y - 48, fighter.Id, strength: 2);
        }
        else if (channel == "game-jam")
        {
            fighter.Ability = "game-jam";
            fighter.ActionTicks = 42;
            SetAction(fighter, "ultimate_release", 42);
            fighter.PendingJamTicks = 11; // authored activation frame2 of the42-tick release
        }
        else if (channel == "join")
        {
            fighter.Ability = "join";
            fighter.ActionTicks = 34;
            SetAction(fighter, "ultimate_release", 34);
            projectiles.Add(new Projectile
            {
                Id = ++projectileId, Owner = fighter, SourceAvatarId = fighter.AvatarId, Kind = "join",
                X = fighter.X + fighter.Facing * 56, Y = fighter.Y - 48,
                Vx = fighter.Facing * 245, Vy = 0, Radius = 32, Damage = 26, Force = 620, Life = 115
            });
            Emit("join", fighter.X + fighter.Facing * 56, fighter.Y - 48, fighter.Id);
        }
    }

    private void ReleaseLuppGameJam(Fighter fighter)
    {
        if (!IsLupp(fighter) || !fighter.Connected || fighter.Stocks <= 0 || fighter.RespawnTicks > 0 ||
            fighter.Hitstun > 0 || fighter.Action != "ultimate_release" || fighter.Ability != "game-jam") return;
        for (var index = -2; index <= 2; index++)
            projectiles.Add(new Projectile
            {
                Id = ++projectileId, Owner = fighter, SourceAvatarId = fighter.AvatarId, Kind = "prototype",
                X = fighter.X + fighter.Facing * 52, Y = fighter.Y - 48 + index * 18,
                Vx = fighter.Facing * 440 * Math.Cos(index * .22), Vy = -150 + index * 90,
                Gravity = 390, Radius = 16, Damage = 9, Force = 360, Life = 108, BouncesRemaining = 2
            });
        Emit("game-jam", fighter.X + fighter.Facing * 52, fighter.Y - 48, fighter.Id, strength: 2);
    }

    private void CancelAbility(Fighter fighter)
    {
        if (fighter.Channel == "repair") Emit("repair-cancel", fighter.X, fighter.Y - 35, fighter.Id);
        fighter.Channel = null;
        fighter.PendingStarTicks = 0;
        fighter.PendingIdeaTicks = 0;
        fighter.PendingJamTicks = 0;
        fighter.ChannelTicks = fighter.ChannelTotal = 0;
        fighter.FieldTicks = fighter.DashTicks = fighter.ArmorHits = 0;
        fighter.Ability = null;
        fighter.ActionTicks = 0;
    }

    private void Capture(Fighter attacker, Fighter target)
    {
        ReleaseCapture(target);
        CancelAbility(target);
        target.Attack = null;
        target.ChargeTicks = target.JumpBuffer = 0;
        target.Shielding = false;
        target.CapturedBy = attacker;
        target.Vx = target.Vy = 0;
        target.Hitstun = target.Hitstop = 0;
        target.EscapeEdges = 0;
        target.Ledge = null;
        attacker.CapturedTarget = target;
        attacker.CaptureTicks = 36;
        attacker.Attack = null;
        attacker.Vx = 0;
        attacker.Ability = "capture";
        SetAction(attacker, "ability_2_magnet", 36);
        SetAction(target, "hit", 36);
        FollowCaptor(target);
        Emit("capture", target.X, target.Y - 35, attacker.Id, target.Id);
    }

    private static void FollowCaptor(Fighter target)
    {
        if (target.CapturedBy is not { } owner) return;
        target.X = owner.X + owner.Facing * 58;
        target.Y = owner.Y - 5;
        target.Vx = target.Vy = 0;
        target.Grounded = false;
    }

    private void StepCaptured(Fighter target)
    {
        var owner = target.CapturedBy!;
        if (!owner.Connected || owner.Stocks <= 0 || owner.RespawnTicks > 0 || owner.Hitstun > 0)
        { ReleaseCapture(target); return; }
        if (target.Input.Jump && !target.PreviousInput.Jump) target.EscapeEdges++;
        if (target.EscapeEdges >= 3)
        {
            ReleaseCapture(target);
            target.Invulnerability = 18;
            target.Vx = -owner.Facing * 180;
            target.Vy = -260;
            Emit("escape", target.X, target.Y - 35, target.Id, owner.Id);
            return;
        }
        FollowCaptor(target);
    }

    private void StepCapture(Fighter owner)
    {
        if (owner.CapturedTarget is not { } target) return;
        if (!owner.Connected || !target.Connected || target.Stocks <= 0 || owner.Hitstun > 0 || !owner.Grounded)
        { ReleaseCapture(owner); return; }
        owner.CaptureTicks--;
        if (owner.CaptureTicks > 0 && !(owner.Input.Grab && !owner.PreviousInput.Grab) &&
            !(owner.Input.Attack && !owner.PreviousInput.Attack)) return;
        var directionX = owner.Input.Horizontal < -.5 ? -1 : owner.Input.Horizontal > .5 ? 1 : owner.Facing;
        var directionY = owner.Input.Vertical < -.5 ? -1 : owner.Input.Vertical > .5 ? .8 : -.45;
        ReleaseCapture(owner);
        Strike(owner, target, 13, 510, directionX, directionY, true);
        owner.ActionTicks = 25;
        SetAction(owner, "ability_2_magnet", 25);
        Emit("throw", target.X, target.Y - 35, owner.Id, target.Id, 1.7);
    }

    private void ReleaseCapture(Fighter fighter)
    {
        if (fighter.CapturedBy is { } owner)
        {
            owner.CapturedTarget = null;
            owner.CaptureTicks = 0;
            owner.ActionTicks = Math.Max(owner.ActionTicks, 12);
            fighter.CapturedBy = null;
            fighter.EscapeEdges = 0;
            Emit("release", fighter.X, fighter.Y - 35, owner.Id, fighter.Id);
        }
        if (fighter.CapturedTarget is { } target)
        {
            fighter.CapturedTarget = null;
            fighter.CaptureTicks = 0;
            target.CapturedBy = null;
            target.EscapeEdges = 0;
            Emit("release", target.X, target.Y - 35, fighter.Id, target.Id);
        }
    }

    private void StepFields()
    {
        foreach (var owner in fighters.Where(f => f.Stocks > 0 && f.RespawnTicks == 0))
        {
            if (owner.CapturedTarget is { } captive) FollowCaptor(captive);
            if (owner.FieldTicks == 0) continue;
            var x = owner.X + owner.Facing * 75;
            var y = owner.Y - 35;
            foreach (var target in fighters)
            {
                if (target == owner || !CanHit(target) || target.CapturedBy is not null || (target.X - owner.X) * owner.Facing < 0) continue;
                var dx = x - target.X;
                var dy = y - (target.Y - 35);
                if (dx * dx + dy * dy > 150 * 150) continue;
                var toward = owner.X - target.X;
                AddPull(target, Math.Sign(toward) * (target.Shielding ? 500 : 3100) * Dt,
                    target.Grounded ? 0 : dy * 4 * Dt);
                if (frame % 5 == 0) Emit("pull", target.X, target.Y - 35, owner.Id, target.Id);
            }
            foreach (var projectile in projectiles.ToArray())
            {
                if (projectile.Owner == owner) continue;
                var dx = projectile.X - x; var dy = projectile.Y - y;
                if ((projectile.X - owner.X) * owner.Facing < 0 || dx * dx + dy * dy > 150 * 150) continue;
                projectiles.Remove(projectile);
                owner.Energy = Math.Min(100, owner.Energy + 7);
                Emit("absorb", projectile.X, projectile.Y, owner.Id, projectile.Owner.Id);
            }
        }
    }

    private static void AddPull(Fighter target, double horizontal, double vertical)
    {
        target.ExternalVx = Math.Clamp(target.ExternalVx + horizontal, -160, 160);
        target.ExternalVy = Math.Clamp(target.ExternalVy + vertical, -160, 160);
    }

    private void ResolveAttacks()
    {
        // Materialize all active attacks first so trades are independent of list order.
        var attacks = fighters.Where(f => f.Stocks > 0 && f.RespawnTicks == 0 && f.Attack is { } a &&
            a.Age >= a.Startup && a.Age < a.Startup + a.Active && f.Hitstop == 0)
            .Select(f => (Fighter: f, Attack: f.Attack!)).ToArray();
        foreach (var (attacker, attack) in attacks)
        {
            foreach (var target in fighters)
            {
                if (target == attacker || !CanHit(target) || attack.HitIds.Contains(target.Id)) continue;
                var dx = target.X - (attacker.X + attack.OffsetX);
                var dy = target.Y - BodyHeight * .5 - (attacker.Y + attack.OffsetY);
                if (attack.RadialRadius > 0 ? dx * dx + dy * dy > attack.RadialRadius * attack.RadialRadius :
                    Math.Abs(dx) > attack.Reach * .58 + HalfWidth || Math.Abs(dy) > attack.Reach * .46 + BodyHeight * .4) continue;
                if (attack.Kind == "capture" && (target.CapturedBy is not null || (target.X - attacker.X) * attacker.Facing < 15)) continue;
                attack.HitIds.Add(target.Id);
                attacker.Invulnerability = 0;
                if (attack.Kind == "capture")
                {
                    Capture(attacker, target);
                    break;
                }
                else if (attack.Kind == "feedback")
                {
                    Strike(attacker, target, attack.Damage, attack.Force, attacker.Facing, -.45, true);
                    target.Facing = -attacker.Facing;
                    Emit("throw", target.X, target.Y - 35, attacker.Id, target.Id);
                }
                else if (attack.Kind is "grab" or "gift-grab")
                {
                    var directionY = attack.Kind == "gift-grab" ? -1 : attacker.Input.Vertical < -.5 ? -1 : attacker.Input.Vertical > .5 ? .7 : -.4;
                    Strike(attacker, target, attack.Damage, attack.Force, attacker.Facing * (attack.Kind == "gift-grab" ? .3 : .9), directionY, true);
                    Emit("throw", target.X, target.Y - 35, attacker.Id, target.Id, 1.2);
                }
                else
                {
                    var radial = attack.Kind is "shockwave" or "overload";
                    var dirX = radial ? Math.Sign(target.X - attacker.X) : attack.DirectionX;
                    Strike(attacker, target, attack.Damage, attack.Force, dirX, attack.Kind == "overload" ? -.65 : attack.DirectionY, false);
                }
            }
        }
    }

    private bool CanHit(Fighter fighter) => fighter.Stocks > 0 && fighter.RespawnTicks == 0 &&
        fighter.Invulnerability == 0 && fighter.DodgeInvulnerability == 0;

    private void Strike(Fighter attacker, Fighter target, double damage, double force,
        double directionX, double directionY, bool ignoresShield)
    {
        if (target.Shielding && !ignoresShield)
        {
            target.Shield = Math.Max(0, target.Shield - damage * 2.65);
            target.Damage = Math.Min(999, target.Damage + damage * .04);
            target.Vx += Math.Sign(target.X - attacker.X) * (IsRay(target) ? 18 : 90);
            target.Hitstop = 2;
            attacker.Hitstop = target.ShieldAge < 5 ? 11 : 3;
            Emit(target.ShieldAge < 5 ? "parry" : "shield", target.X, target.Y - 35, attacker.Id, target.Id);
            if (target.Shield <= 0) BreakShield(target);
            return;
        }
        if (target.ArmorHits > 0 && !ignoresShield)
        {
            target.ArmorHits--;
            var armoredDamage = damage * .55;
            target.Damage = Math.Min(999, target.Damage + armoredDamage);
            AwardCombatDamage(attacker, target, armoredDamage);
            target.LastHitterId = attacker.Id;
            target.LastHitTick = frame;
            Emit("armor", target.X, target.Y - 35, target.Id, attacker.Id, armoredDamage);
            return;
        }
        ReleaseCapture(target);
        CancelAbility(target);
        target.Shielding = false;
        target.Damage = Math.Min(999, target.Damage + damage);
        AwardCombatDamage(attacker, target, damage);
        var weight = IsLupp(target) ? .98 : IsSanta(target) ? 1.03 : IsRay(target) ? 1.10 : IsBip(target) ? .95 : 1;
        var launch = (force + target.Damage * (force / 145)) / weight;
        var length = Math.Sqrt(directionX * directionX + directionY * directionY);
        if (length < .001) { directionX = attacker.Facing; directionY = -.45; length = Math.Sqrt(1.2025); }
        target.Vx = Math.Clamp(directionX / length * launch, -1700, 1700);
        target.Vy = Math.Clamp(directionY / length * launch, -1500, 1200);
        if (target.Grounded && target.Vy > -100) target.Vy = -170;
        target.Grounded = false;
        target.Hitstun = Math.Clamp((int)(10 + launch / 27), 12, 65);
        target.HitstunDuration = target.Hitstun;
        target.Hitstop = attacker.Hitstop = Math.Clamp((int)(damage / 2), 3, 9);
        target.Attack = null;
        target.ChargeTicks = 0;
        target.ActionTicks = 0;
        target.Ledge = null;
        target.LastHitterId = attacker.Id;
        target.LastHitTick = frame;
        SetAction(target, "hit", target.Hitstun);
        Emit("hit", target.X, target.Y - 35, attacker.Id, target.Id, damage / 8);
    }

    private void BreakShield(Fighter fighter)
    {
        ReleaseCapture(fighter);
        CancelAbility(fighter);
        fighter.Shielding = false;
        fighter.Shield = 35;
        fighter.Hitstun = 120;
        fighter.HitstunDuration = 120;
        fighter.Attack = null;
        fighter.ChargeTicks = 0;
        fighter.Vy = -320;
        fighter.Grounded = false;
        Emit("break", fighter.X, fighter.Y - 30, fighter.Id);
    }

    private static void AwardCombatDamage(Fighter attacker, Fighter target, double damage)
    {
        if (attacker == target || damage <= 0) return;
        if (HasKit(attacker)) attacker.Ultimate = Math.Min(100, attacker.Ultimate + damage * 1.3);
        if (HasKit(target)) target.Ultimate = Math.Min(100, target.Ultimate + damage * .85);
    }

    private void StepProjectiles()
    {
        foreach (var projectile in projectiles.ToArray())
        {
            if (!projectiles.Contains(projectile)) continue;
            var previousBottom = projectile.Y + projectile.Radius;
            projectile.Vy += projectile.Gravity * Dt;
            projectile.X += projectile.Vx * Dt;
            projectile.Y += projectile.Vy * Dt;
            if (projectile.BouncesRemaining > 0 && projectile.Vy > 0)
                foreach (var platform in stage.Platforms.OrderBy(platform => platform.Y))
                    if (previousBottom <= platform.Y && projectile.Y + projectile.Radius >= platform.Y &&
                        projectile.X >= platform.X && projectile.X <= platform.X + platform.Width)
                    {
                        projectile.Y = platform.Y - projectile.Radius;
                        projectile.Vy = -Math.Max(160, projectile.Vy * .75);
                        projectile.BouncesRemaining--;
                        Emit("prototype-bounce", projectile.X, projectile.Y, projectile.Owner.Id);
                        break;
                    }
            if (--projectile.Life <= 0 || projectile.X < stage.Left || projectile.X > stage.Right)
            {
                if (projectile.Kind == "join") ExplodeJoin(projectile);
                else projectiles.Remove(projectile);
                continue;
            }
            if (projectile.Kind == "join")
            {
                foreach (var target in fighters)
                {
                    if (target == projectile.Owner || !CanHit(target) || target.CapturedBy is not null) continue;
                    var dx = projectile.X - target.X; var dy = projectile.Y - (target.Y - 35);
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > 210 || distance < 1) continue;
                    var pull = (target.Shielding ? 350 : 2700) * Dt;
                    AddPull(target, dx / distance * pull, target.Grounded ? 0 : dy / distance * pull);
                    if (frame % 6 == 0) Emit("join-pull", target.X, target.Y - 35, projectile.Owner.Id, target.Id);
                }
            }
            foreach (var target in fighters)
            {
                if (target == projectile.Owner || !CanHit(target)) continue;
                if (Math.Abs(target.X - projectile.X) > HalfWidth + projectile.Radius ||
                    projectile.Y < target.Y - BodyHeight - projectile.Radius || projectile.Y > target.Y + projectile.Radius) continue;
                if (target.Shielding && IsBip(target) && target.ShieldAge <= 6 && projectile.Reflections < 3)
                {
                    target.Shield = Math.Max(0, target.Shield - 8 - projectile.Damage * .4);
                    projectile.Owner = target;
                    projectile.Reflections++;
                    projectile.Vx = -projectile.Vx;
                    projectile.Vy = -projectile.Vy;
                    projectile.X = target.X + Math.Sign(projectile.Vx) * (HalfWidth + projectile.Radius + 4);
                    if (projectile.Kind is not ("join" or "gift" or "prototype")) projectile.Kind = "reflected";
                    Emit("reflect", projectile.X, projectile.Y, target.Id);
                    if (target.Shield <= 0) BreakShield(target);
                    break;
                }
                if (target.Shielding && IsLupp(target))
                {
                    target.Shield = Math.Max(0, target.Shield - projectile.Damage * 2.5);
                    if (projectile.Reflections < 3)
                    {
                        projectile.Owner = target;
                        projectile.SourceAvatarId = AvatarEconomy.LuppId;
                        projectile.Kind = "prototype";
                        projectile.Reflections++;
                        projectile.Vx = target.Facing * 360;
                        projectile.Vy = -110;
                        projectile.Gravity = 380;
                        projectile.Radius = 16;
                        projectile.Damage = Math.Min(8, projectile.Damage);
                        projectile.Force = 265;
                        projectile.Life = 70;
                        projectile.BouncesRemaining = 1;
                        projectile.X = target.X + target.Facing * (HalfWidth + projectile.Radius + 4);
                    }
                    else projectiles.Remove(projectile);
                    Emit("prototype", projectile.X, projectile.Y, target.Id);
                    if (target.Shield <= 0) BreakShield(target);
                    break;
                }
                if (target.Shielding && IsSanta(target))
                {
                    target.Shield = Math.Max(0, target.Shield - projectile.Damage * 2.3);
                    target.Energy = Math.Min(100, target.Energy + 10);
                    target.Hitstop = 2;
                    Emit("absorb", projectile.X, projectile.Y, target.Id, projectile.Owner.Id, projectile.Damage);
                    projectiles.Remove(projectile);
                    if (target.Shield <= 0) BreakShield(target);
                    break;
                }
                if (target.Shielding && IsRay(target))
                {
                    target.Shield = Math.Max(0, target.Shield - projectile.Damage * 2.45);
                    target.Ultimate = Math.Min(100, target.Ultimate + Math.Min(8, projectile.Damage * .65));
                    target.Hitstop = 2;
                    Emit("absorb", projectile.X, projectile.Y, target.Id, projectile.Owner.Id, projectile.Damage);
                    projectiles.Remove(projectile);
                    if (target.Shield <= 0) BreakShield(target);
                    break;
                }
                if (projectile.Kind == "join")
                {
                    ExplodeJoin(projectile);
                    break;
                }
                Strike(projectile.Owner, target, projectile.Damage, projectile.Force, Math.Sign(projectile.Vx), -.38, false);
                projectiles.Remove(projectile);
                break;
            }
        }
    }

    private void ExplodeJoin(Projectile projectile)
    {
        if (!projectiles.Remove(projectile)) return;
        foreach (var target in fighters)
        {
            if (target == projectile.Owner || !CanHit(target)) continue;
            var dx = target.X - projectile.X; var dy = target.Y - 35 - projectile.Y;
            if (dx * dx + dy * dy > 185 * 185) continue;
            Strike(projectile.Owner, target, projectile.Damage, projectile.Force,
                Math.Abs(dx) < 1 ? Math.Sign(projectile.Vx) : Math.Sign(dx), -.7, false);
        }
        Emit("join-explode", projectile.X, projectile.Y, projectile.Owner.Id, strength: 2.5);
    }

    private void KnockOut(Fighter fighter)
    {
        ReleaseCapture(fighter);
        CancelAbility(fighter);
        ResetKit(fighter);
        fighter.Stocks--;
        if (fighter.Stocks == 0) fighter.EliminationTick = frame;
        fighter.Falls++;
        var scorer = fighter.LastHitterId is not null && frame - fighter.LastHitTick < 600 ? Find(fighter.LastHitterId) : null;
        if (scorer is not null) scorer.KOs++;
        Emit("ko", Math.Clamp(fighter.X, 20, 1260), Math.Clamp(fighter.Y, 20, 700), scorer?.Id, fighter.Id, 2);
        fighter.RespawnTicks = 90;
        fighter.Vx = fighter.Vy = 0;
        fighter.ExternalVx = fighter.ExternalVy = 0;
        fighter.Attack = null;
        fighter.Input = new(fighter.LastSequence);
        projectiles.RemoveAll(p => p.Owner == fighter);
    }

    private void ResolveWinner()
    {
        if (phase != "playing" || fighters.Count < 2) return;
        var alive = fighters.Where(x => x.Stocks > 0).ToArray();
        if (alive.Length > 1) return;
        if (alive.Length == 0)
        {
            var eligible = fighters.Where(f => !f.Removed).ToArray();
            if (eligible.Length == 0) { phase = "finished"; return; }
            var finalElimination = eligible.Max(f => f.EliminationTick);
            var finalists = eligible.Where(f => f.EliminationTick == finalElimination).ToArray();
            if (finalists.Length == 1) Finish(finalists[0]);
            else BeginSuddenDeath(finalists);
            return;
        }
        Finish(alive[0]);
    }

    private void FinishOnTime()
    {
        var alive = fighters.Where(x => x.Stocks > 0).ToArray();
        var bestStocks = alive.Max(x => x.Stocks);
        var stockLeaders = alive.Where(x => x.Stocks == bestStocks).ToArray();
        var bestDamage = stockLeaders.Min(x => Math.Round(x.Damage, 1));
        var leaders = stockLeaders.Where(x => Math.Round(x.Damage, 1) == bestDamage).ToArray();
        if (leaders.Length == 1) Finish(leaders[0]);
        else BeginSuddenDeath(leaders);
    }

    private void BeginSuddenDeath(Fighter[] leaders)
    {
        suddenDeath = true;
        suddenDeathTicks = 0;
        projectiles.Clear();
        foreach (var fighter in fighters)
        {
            fighter.Stocks = leaders.Contains(fighter) ? 1 : 0;
            ResetCombat(fighter);
            if (fighter.Stocks == 0) continue;
            fighter.Damage = 200;
            fighter.Invulnerability = 90;
            Place(fighter, fighters.IndexOf(fighter), false);
        }
        Emit("sudden-death", 640, 320);
    }

    private void Finish(Fighter winner)
    {
        foreach (var fighter in fighters) { ReleaseCapture(fighter); CancelAbility(fighter); }
        winnerId = winner.Id;
        phase = "finished";
        SetAction(winner, "celebrate", 48);
        projectiles.Clear();
        Emit("win", winner.X, winner.Y - 35, winner.Id);
    }

    private void ResetCombat(Fighter fighter)
    {
        ReleaseCapture(fighter);
        CancelAbility(fighter);
        ResetKit(fighter);
        fighter.Damage = 0;
        fighter.Shield = 100;
        fighter.Shielding = false;
        fighter.Hitstun = fighter.Hitstop = fighter.ChargeTicks = fighter.ActionTicks = 0;
        fighter.DodgeCooldown = fighter.DodgeInvulnerability = fighter.SpecialCooldown = 0;
        fighter.DropTicks = fighter.JumpBuffer = fighter.Coyote = fighter.RespawnTicks = 0;
        fighter.LedgeCooldown = fighter.LedgeTicks = 0;
        fighter.Ledge = null;
        fighter.Invulnerability = 0;
        fighter.JumpsUsed = 0;
        fighter.UsedRecovery = fighter.UsedAirSpecial = false;
        fighter.Vx = fighter.Vy = 0;
        fighter.Attack = null;
        fighter.LastHitterId = null;
        fighter.Action = "idle";
        fighter.ActionStartTick = frame;
        fighter.ActionDuration = 1;
    }

    private static void ResetKit(Fighter fighter)
    {
        fighter.Energy = 100;
        fighter.PendingIdeaTicks = fighter.PendingStarTicks = fighter.PendingJamTicks = 0;
        fighter.Ultimate = 0;
        fighter.PulseCooldown = fighter.GrabCooldown = fighter.DashCooldown = 0;
        fighter.RecoveryCooldown = fighter.RepairCooldown = fighter.UltimateCooldown = 0;
        fighter.ChannelTicks = fighter.ChannelTotal = fighter.FieldTicks = fighter.DashTicks = fighter.ArmorHits = 0;
        fighter.Channel = fighter.Ability = null;
        fighter.CapturedBy = fighter.CapturedTarget = null;
        fighter.CaptureTicks = fighter.EscapeEdges = 0;
        fighter.ExternalVx = fighter.ExternalVy = 0;
    }

    private void Place(Fighter fighter, int index, bool elevated)
    {
        // Spawn on the lowest solid platform nearest the allotted quarter.
        var x = fighters.Count <= 2 ? index == 0 ? 440 : 840 : 350 + index * 190;
        var platform = stage.Platforms.Where(p => !p.OneWay)
            .OrderBy(p => Math.Abs(p.X + p.Width / 2 - x)).First();
        fighter.X = Math.Clamp(x, platform.X + 50, platform.X + platform.Width - 50);
        fighter.Y = platform.Y - (elevated ? 180 : 0);
        fighter.Grounded = !elevated;
        fighter.Facing = fighter.X < 640 ? 1 : -1;
    }

    private ArenaInput BotInput(Fighter bot)
    {
        var target = fighters.Where(f => f != bot && f.Stocks > 0 && f.RespawnTicks == 0)
            .OrderBy(f => Math.Abs(f.X - bot.X) + Math.Abs(f.Y - bot.Y) * .5).FirstOrDefault();
        if (target is null) return new(bot.LastSequence + 1);
        if (HasKit(bot)) return KitBotInput(bot, target);
        var dx = target.X - bot.X;
        var dy = target.Y - bot.Y;
        var closest = stage.Platforms.Where(p => !p.OneWay).OrderBy(p =>
            bot.X < p.X ? p.X - bot.X : bot.X > p.X + p.Width ? bot.X - p.X - p.Width : 0).First();
        var offstage = bot.X < closest.X + 16 || bot.X > closest.X + closest.Width - 16;
        var recovering = !bot.Grounded && bot.Y > closest.Y - 70 &&
            (offstage || bot.Y > closest.Y + 20);
        var horizontal = recovering ? Math.Sign(closest.X + closest.Width / 2 - bot.X) : Math.Abs(dx) > 64 ? Math.Sign(dx) : 0;
        var jump = (recovering && bot.JumpsUsed < 2 && bot.Vy > 20 && frame % 12 == 0) ||
            (dy < -75 && bot.JumpsUsed < 2 && frame % 38 == 0) ||
            (offstage && bot.Grounded && frame % 9 == 0);
        var recovery = recovering && bot.JumpsUsed >= 2 && bot.Vy > 20 && !bot.UsedRecovery && frame % 10 == 0;
        var nearby = Math.Abs(dx) < 105 && Math.Abs(dy) < 90;
        var danger = nearby && target.Attack is not null;
        return new(bot.LastSequence + 1, horizontal, recovery ? -1 : nearby && target.Y < bot.Y - 35 ? -1 : 0,
            jump, nearby && frame % 24 == 0, recovery || !nearby && Math.Abs(dx) < 470 && frame % 83 == 0,
            danger && frame % 70 < 25, nearby && frame % 109 == 0,
            nearby && bot.Grounded && frame % 230 is >= 170 and < 195,
            danger && frame % 113 == 0);
    }

    private ArenaInput KitBotInput(Fighter bot, Fighter target)
    {
        var sequence = bot.LastSequence + 1;
        if (bot.CapturedBy is not null) return new(sequence, Jump: frame % 2 == 0);
        if (bot.ChannelTicks > 0) return new(sequence, Smash: bot.Channel != "repair");
        if (bot.CapturedTarget is not null) return new(sequence, Horizontal: bot.Facing,
            Vertical: target.Damage > 80 ? -1 : 0, Grab: frame % 8 == 0);
        var dx = target.X - bot.X;
        var dy = target.Y - bot.Y;
        var closest = stage.Platforms.Where(p => !p.OneWay).OrderBy(p =>
            bot.X < p.X ? p.X - bot.X : bot.X > p.X + p.Width ? bot.X - p.X - p.Width : 0).First();
        var offstage = bot.X < closest.X + 16 || bot.X > closest.X + closest.Width - 16;
        var recovering = !bot.Grounded && bot.Y > closest.Y - 70 && (offstage || bot.Y > closest.Y + 20);
        var horizontal = recovering ? Math.Sign(closest.X + closest.Width / 2 - bot.X) : Math.Abs(dx) > 65 ? Math.Sign(dx) : 0;
        var jump = recovering && bot.JumpsUsed < 2 && bot.Vy > 20 && frame % 12 == 0 ||
            dy < -75 && bot.JumpsUsed < 2 && frame % 38 == 0;
        if (recovering && bot.JumpsUsed >= 2 && bot.Vy > 20 && !bot.UsedRecovery && bot.RecoveryCooldown == 0 &&
            (IsLupp(bot) ? bot.Energy >= 18 : IsSanta(bot) ? bot.Energy >= 16 : !IsBip(bot) || bot.Energy >= 15))
            return new(sequence, horizontal, -1, jump, Special: frame % 7 == 0);
        var nearby = Math.Abs(dx) < (IsRay(bot) ? 132 : 100) && Math.Abs(dy) < 90;
        var incoming = projectiles.Any(p => p.Owner != bot && Math.Abs(p.X - bot.X) < 90 &&
            Math.Abs(p.Y - (bot.Y - 35)) < 75 && (p.X - bot.X) * p.Vx < 0);
        if (bot.Grounded && (incoming || nearby && target.Attack is not null && frame % 70 < 25))
            return new(sequence, Shield: true);
        if (bot.Ultimate >= 100 && bot.UltimateCooldown == 0 && Math.Abs(dy) < 110 && Math.Abs(dx) < (IsSanta(bot) ? 420 : IsBip(bot) ? 195 : 520))
            return new(sequence, Smash: true);
        var safeRepair = bot.Grounded && Math.Abs(dx) > 300 && bot.Damage >= (IsLupp(bot) ? 10 : IsSanta(bot) ? 12 : IsBip(bot) ? 10 : 16) &&
            bot.RepairCooldown == 0 && (IsLupp(bot) ? bot.Energy >= 45 : IsSanta(bot) ? bot.Energy >= 60 : IsBip(bot) ? bot.Energy >= 55 : bot.Shield >= 70);
        if (safeRepair && frame % 20 == 0) return new(sequence, Vertical: 1, Special: true);
        if (IsBip(bot) && bot.Energy >= 30 && bot.GrabCooldown == 0 && Math.Abs(dx) is > 105 and < 205 && Math.Abs(dy) < 90 && frame % 32 == 0)
            return new(sequence, horizontal, Grab: true);
        if (IsSanta(bot) && nearby && bot.Energy >= 20 && bot.GrabCooldown == 0 && frame % 43 == 0)
            return new(sequence, horizontal, Grab: true);
        if (IsLupp(bot) && nearby && bot.Energy >= 16 && bot.GrabCooldown == 0 && frame % 41 == 0)
            return new(sequence, horizontal, Grab: true);
        if (IsRay(bot) && nearby && bot.GrabCooldown == 0 && frame % 58 == 0)
            return new(sequence, horizontal, Grab: true);
        if (bot.DashCooldown == 0 && Math.Abs(dx) is > 155 and < 330 && Math.Abs(dy) < 65 &&
            (IsLupp(bot) ? bot.Energy >= 32 : IsSanta(bot) ? bot.Energy >= 35 : !IsBip(bot) || bot.Energy >= 40) && frame % 79 == 0)
            return new(sequence, Math.Sign(dx), Special: true);
        if (!nearby && Math.Abs(dx) < 650 && Math.Abs(dy) < 85 && bot.PulseCooldown == 0 &&
            (!(IsBip(bot) || IsSanta(bot) || IsLupp(bot)) || bot.Energy >= 25) && frame % 37 == 0)
            return new(sequence, Special: true);
        return new(sequence, horizontal, nearby && dy < -35 ? -1 : 0, jump,
            Attack: nearby && frame % (IsLupp(bot) ? 23 : IsSanta(bot) ? 28 : IsBip(bot) ? 20 : 35) == 0);
    }

    private static string DisplayAction(Fighter fighter)
    {
        if (fighter.Stocks <= 0) return "knockout";
        if (fighter.RespawnTicks > 0) return "respawn";
        if (fighter.CapturedBy is not null) return "hit";
        if (fighter.ChannelTicks > 0) return fighter.Action;
        if (fighter.CapturedTarget is not null) return "ability_2_magnet";
        if (fighter.Hitstun > 0 || fighter.Hitstop > 0 && !fighter.Shielding) return "hit";
        if (fighter.Shielding) return "ability_3_shield";
        if (fighter.ChargeTicks > 0) return "ultimate_charge";
        if (fighter.Attack is not null || fighter.ActionTicks > 0 || fighter.Action == "celebrate") return fighter.Action;
        if (!fighter.Grounded) return "jump";
        return Math.Abs(fighter.Vx) > 25 ? "run" : "idle";
    }

    private int ActionFrame(Fighter fighter)
    {
        if (fighter.RespawnTicks > 0) return Math.Clamp((90 - fighter.RespawnTicks) * 8 / 90, 0, 7);
        if (fighter.ChannelTicks > 0) return Math.Clamp((fighter.ChannelTotal - fighter.ChannelTicks) * 8 / Math.Max(1, fighter.ChannelTotal), 0, 7);
        if (fighter.CapturedTarget is not null) return Math.Clamp((36 - fighter.CaptureTicks) * 8 / 36, 0, 7);
        if (fighter.CapturedBy is not null) return Math.Clamp((frame - fighter.ActionStartTick) * 8 / 36, 0, 7);
        if (fighter.Hitstun > 0) return Math.Clamp((fighter.HitstunDuration - fighter.Hitstun) * 8 / Math.Max(1, fighter.HitstunDuration), 0, 7);
        if (fighter.Shielding) return Math.Min(7, fighter.ShieldAge * 8 / 24);
        if (fighter.Attack is { } attack) return Math.Min(7, attack.Age * 8 / Math.Max(1, attack.Startup + attack.Active + attack.Recovery));
        if (fighter.ChargeTicks > 0) return Math.Min(7, fighter.ChargeTicks / 9);
        if (fighter.ActionTicks > 0 || fighter.Action == "celebrate")
            return Math.Clamp((frame - fighter.ActionStartTick) * 8 / Math.Max(1, fighter.ActionDuration), 0, 7);
        return frame / (fighter.Grounded && Math.Abs(fighter.Vx) > 25 ? 5 : 8) % 8;
    }

    private void Emit(string type, double x, double y, string? actorId = null, string? targetId = null, double strength = 1)
    {
        events.Add(new(++eventId, frame, type, Round(x), Round(y), actorId, targetId, strength));
        if (events.Count > 80) events.RemoveAt(0);
    }

    private sealed class Fighter
    {
        public required string Id;
        public required string Name;
        public required string AvatarId;
        public required string Color;
        public ArenaAppearance? Appearance;
        public bool IsBot;
        public bool Connected = true;
        public bool Removed;
        public double X, Y, Vx, Vy, Damage;
        public double ExternalVx, ExternalVy;
        public double Energy = 100, Ultimate;
        public int PulseCooldown, GrabCooldown, DashCooldown, RecoveryCooldown, RepairCooldown, UltimateCooldown;
        public int PendingStarTicks;
        public int PendingIdeaTicks;
        public int PendingJamTicks;
        public int FieldTicks, DashTicks, ArmorHits, ChannelTicks, ChannelTotal;
        public string? Channel, Ability;
        public Fighter? CapturedBy, CapturedTarget;
        public int CaptureTicks, EscapeEdges;
        public int ActionStartTick, ActionDuration = 1, HitstunDuration;
        public double Shield = 100;
        public int Facing = 1, Stocks, KOs, Falls;
        public bool Grounded, Shielding, UsedRecovery, UsedAirSpecial;
        public int JumpsUsed, Coyote, JumpBuffer, DropTicks, Hitstun, Hitstop;
        public int Invulnerability, DodgeInvulnerability, DodgeCooldown, SpecialCooldown;
        public int RespawnTicks, ChargeTicks, ActionTicks, ShieldAge, InputTick;
        public int LedgeCooldown, LedgeTicks, LedgeSide;
        public ArenaPlatform? Ledge;
        public string Action = "idle";
        public string? LastHitterId;
        public int LastHitTick, EliminationTick = -1;
        public long LastSequence = -1;
        public ArenaInput Input = new();
        public ArenaInput PreviousInput = new();
        public Attack? Attack;
    }

    private sealed class Attack
    {
        public required string Kind;
        public int Age, Startup, Active, Recovery;
        public double Damage, Force, Reach, DirectionX, DirectionY, OffsetX, OffsetY, RadialRadius;
        public readonly HashSet<string> HitIds = new(StringComparer.Ordinal);
    }

    private sealed class Projectile
    {
        public long Id;
        public required Fighter Owner;
        public string SourceAvatarId = AvatarEconomy.StarterId;
        public string Kind = "pulse";
        public double X, Y, Vx, Vy, Radius, Damage, Force, Gravity;
        public int Life, Reflections, BouncesRemaining;
    }
}
