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
    private int frame;
    private int elapsed;
    private int countdownTicks;
    private int suddenDeathTicks;
    private long eventId;
    private long projectileId;
    private string phase = "lobby";
    private string? winnerId;
    private bool suddenDeath;

    public ArenaSimulation(string stageId = "rooftops", int stocks = 3, int minutes = 4)
    {
        stage = ArenaCatalog.FindStage(stageId);
        stockCount = Math.Clamp(stocks, 1, 5);
        matchTicks = Math.Clamp(minutes, 1, 10) * 60 * ArenaCatalog.TickRate;
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
            f.Shielding, f.Stocks <= 0, f.Appearance)).ToArray(),
        projectiles.Select(p => new ArenaProjectileSnapshot(p.Id, p.Owner.Id, Round(p.X),
            Round(p.Y), Round(p.Vx), Round(p.Vy), p.Radius, p.Owner.Color)).ToArray(),
        events.ToArray(), winnerId, suddenDeath);

    private Fighter? Find(string id) => fighters.FirstOrDefault(x => x.Id == id);
    private static double Round(double value) => Math.Round(value, 2);
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
        fighter.Invulnerability = Math.Max(0, fighter.Invulnerability - 1);
        fighter.DodgeInvulnerability = Math.Max(0, fighter.DodgeInvulnerability - 1);
        fighter.DodgeCooldown = Math.Max(0, fighter.DodgeCooldown - 1);
        fighter.SpecialCooldown = Math.Max(0, fighter.SpecialCooldown - 1);
        fighter.LedgeCooldown = Math.Max(0, fighter.LedgeCooldown - 1);
        fighter.DropTicks = Math.Max(0, fighter.DropTicks - 1);
        fighter.JumpBuffer = Math.Max(0, fighter.JumpBuffer - 1);
        fighter.Coyote = fighter.Grounded ? 6 : Math.Max(0, fighter.Coyote - 1);
        if (fighter.Input.Jump && !fighter.PreviousInput.Jump) fighter.JumpBuffer = 7;
        if (fighter.Hitstop > 0) { fighter.Hitstop--; return; }

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
        var free = !stunned && fighter.Attack is null && fighter.ActionTicks == 0;
        fighter.Shielding = free && fighter.Grounded && fighter.Input.Shield && fighter.Shield > 0;
        fighter.ShieldAge = fighter.Shielding ? fighter.ShieldAge + 1 : 0;
        if (fighter.Shielding)
        {
            fighter.Shield = Math.Max(0, fighter.Shield - 0.28);
            if (fighter.Shield <= 0) BreakShield(fighter);
        }
        else if (!stunned) fighter.Shield = Math.Min(100, fighter.Shield + 0.13);

        if (free && fighter.DodgeCooldown == 0 &&
            (fighter.Input.Dodge && !fighter.PreviousInput.Dodge ||
             fighter.Input.Shield && !fighter.PreviousInput.Shield &&
                (!fighter.Grounded || Math.Abs(fighter.Input.Horizontal) > .5)))
        {
            fighter.Shielding = false;
            fighter.DodgeInvulnerability = fighter.Grounded ? 16 : 13;
            fighter.ActionTicks = 26;
            fighter.DodgeCooldown = 64;
            fighter.Vx = fighter.Input.Horizontal * 540;
            if (!fighter.Grounded) fighter.Vy = fighter.Input.Vertical * 300;
            fighter.Action = "ability_4_dash";
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
                fighter.Attack = MakeAttack(fighter, "grab", 3, 4, 22, 7, 470, 74);
                fighter.Action = "ability_2_magnet";
            }
            else if (fighter.Input.Special && !fighter.PreviousInput.Special && fighter.SpecialCooldown == 0)
            {
                fighter.ChargeTicks = 0;
                Special(fighter);
            }
            else if (fighter.Grounded && fighter.Input.Smash)
            {
                fighter.ChargeTicks = Math.Min(72, fighter.ChargeTicks + 1);
                fighter.Action = "ultimate_charge";
                if (fighter.ChargeTicks >= 72) ReleaseSmash(fighter);
            }
            else if (fighter.ChargeTicks > 0) ReleaseSmash(fighter);
            else if (fighter.Input.Attack && !fighter.PreviousInput.Attack ||
                !fighter.Grounded && fighter.Input.Smash && !fighter.PreviousInput.Smash)
            {
                var aerial = !fighter.Grounded;
                fighter.Attack = MakeAttack(fighter, aerial ? "aerial" : "normal",
                    aerial ? 5 : 4, aerial ? 6 : 4, aerial ? 16 : 14,
                    aerial ? 9 : 7, aerial ? 325 : 270, aerial ? 88 : 82);
                fighter.Action = "ability_1_pulse";
            }
        }

        if (stunned)
        {
            // Directional influence is bounded and cannot erase a launch.
            fighter.Vx += fighter.Input.Horizontal * 145 * Dt;
            fighter.Vy += fighter.Input.Vertical * 75 * Dt;
        }
        else if (fighter.ActionTicks == 0 || fighter.DodgeInvulnerability == 0)
        {
            var speed = fighter.AvatarId == AvatarEconomy.RaymondId ? 305 : fighter.AvatarId == AvatarEconomy.Bip01Id ? 350 : 330;
            var target = fighter.Shielding ? 0 : fighter.Input.Horizontal * speed;
            if (fighter.Attack is not null) target *= .35;
            if (fighter.ChargeTicks > 0) target = 0;
            fighter.Vx = Approach(fighter.Vx, target,
                (fighter.Grounded ? Math.Abs(target) > 1 ? 2100 : 2700 : 820) * Dt);
        }

        var previousY = fighter.Y;
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

    private void Special(Fighter fighter)
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
                if (Math.Abs(dx) > attack.Reach * .58 + HalfWidth || Math.Abs(dy) > attack.Reach * .46 + BodyHeight * .4) continue;
                attack.HitIds.Add(target.Id);
                attacker.Invulnerability = 0;
                if (attack.Kind == "grab")
                {
                    var directionY = attacker.Input.Vertical < -.5 ? -1 : attacker.Input.Vertical > .5 ? .7 : -.4;
                    Strike(attacker, target, attack.Damage, attack.Force, attacker.Facing * .9, directionY, true);
                    Emit("throw", target.X, target.Y - 35, attacker.Id, target.Id, 1.2);
                }
                else
                {
                    var dirX = attack.Kind == "shockwave" ? Math.Sign(target.X - attacker.X) : attack.DirectionX;
                    Strike(attacker, target, attack.Damage, attack.Force, dirX, attack.DirectionY, false);
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
            target.Vx += Math.Sign(target.X - attacker.X) * 90;
            target.Hitstop = 2;
            attacker.Hitstop = target.ShieldAge < 5 ? 11 : 3;
            Emit(target.ShieldAge < 5 ? "parry" : "shield", target.X, target.Y - 35, attacker.Id, target.Id);
            if (target.Shield <= 0) BreakShield(target);
            return;
        }
        target.Shielding = false;
        target.Damage = Math.Min(999, target.Damage + damage);
        var weight = target.AvatarId == AvatarEconomy.RaymondId ? 1.10 : target.AvatarId == AvatarEconomy.Bip01Id ? .95 : 1;
        var launch = (force + target.Damage * (force / 145)) / weight;
        var length = Math.Sqrt(directionX * directionX + directionY * directionY);
        if (length < .001) { directionX = attacker.Facing; directionY = -.45; length = Math.Sqrt(1.2025); }
        target.Vx = Math.Clamp(directionX / length * launch, -1700, 1700);
        target.Vy = Math.Clamp(directionY / length * launch, -1500, 1200);
        if (target.Grounded && target.Vy > -100) target.Vy = -170;
        target.Grounded = false;
        target.Hitstun = Math.Clamp((int)(10 + launch / 27), 12, 65);
        target.Hitstop = attacker.Hitstop = Math.Clamp((int)(damage / 2), 3, 9);
        target.Attack = null;
        target.ChargeTicks = 0;
        target.ActionTicks = 0;
        target.Ledge = null;
        target.LastHitterId = attacker.Id;
        target.LastHitTick = frame;
        target.Action = "hit";
        Emit("hit", target.X, target.Y - 35, attacker.Id, target.Id, damage / 8);
    }

    private void BreakShield(Fighter fighter)
    {
        fighter.Shielding = false;
        fighter.Shield = 35;
        fighter.Hitstun = 120;
        fighter.Attack = null;
        fighter.ChargeTicks = 0;
        fighter.Vy = -320;
        fighter.Grounded = false;
        Emit("break", fighter.X, fighter.Y - 30, fighter.Id);
    }

    private void StepProjectiles()
    {
        foreach (var projectile in projectiles.ToArray())
        {
            projectile.X += projectile.Vx * Dt;
            projectile.Y += projectile.Vy * Dt;
            if (--projectile.Life <= 0 || projectile.X < stage.Left || projectile.X > stage.Right)
            { projectiles.Remove(projectile); continue; }
            foreach (var target in fighters)
            {
                if (target == projectile.Owner || !CanHit(target)) continue;
                if (Math.Abs(target.X - projectile.X) > HalfWidth + projectile.Radius ||
                    projectile.Y < target.Y - BodyHeight - projectile.Radius || projectile.Y > target.Y + projectile.Radius) continue;
                Strike(projectile.Owner, target, projectile.Damage, projectile.Force, Math.Sign(projectile.Vx), -.38, false);
                projectiles.Remove(projectile);
                break;
            }
        }
    }

    private void KnockOut(Fighter fighter)
    {
        fighter.Stocks--;
        if (fighter.Stocks == 0) fighter.EliminationTick = frame;
        fighter.Falls++;
        var scorer = fighter.LastHitterId is not null && frame - fighter.LastHitTick < 600 ? Find(fighter.LastHitterId) : null;
        if (scorer is not null) scorer.KOs++;
        Emit("ko", Math.Clamp(fighter.X, 20, 1260), Math.Clamp(fighter.Y, 20, 700), scorer?.Id, fighter.Id, 2);
        fighter.RespawnTicks = 90;
        fighter.Vx = fighter.Vy = 0;
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
            if (fighter.Stocks == 0) continue;
            ResetCombat(fighter);
            fighter.Damage = 200;
            fighter.Invulnerability = 90;
            Place(fighter, fighters.IndexOf(fighter), false);
        }
        Emit("sudden-death", 640, 320);
    }

    private void Finish(Fighter winner)
    {
        winnerId = winner.Id;
        phase = "finished";
        winner.Action = "celebrate";
        projectiles.Clear();
        Emit("win", winner.X, winner.Y - 35, winner.Id);
    }

    private void ResetCombat(Fighter fighter)
    {
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

    private static string DisplayAction(Fighter fighter)
    {
        if (fighter.Stocks <= 0) return "knockout";
        if (fighter.RespawnTicks > 0) return "respawn";
        if (fighter.Hitstun > 0 || fighter.Hitstop > 0 && !fighter.Shielding) return "hit";
        if (fighter.Shielding) return "ability_3_shield";
        if (fighter.ChargeTicks > 0) return "ultimate_charge";
        if (fighter.Attack is not null || fighter.ActionTicks > 0 || fighter.Action == "celebrate") return fighter.Action;
        if (!fighter.Grounded) return "jump";
        return Math.Abs(fighter.Vx) > 25 ? "run" : "idle";
    }

    private int ActionFrame(Fighter fighter)
    {
        if (fighter.Attack is { } attack) return Math.Min(7, attack.Age * 8 / Math.Max(1, attack.Startup + attack.Active + attack.Recovery));
        if (fighter.ChargeTicks > 0) return Math.Min(7, fighter.ChargeTicks / 9);
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
        public double Damage, Force, Reach, DirectionX, DirectionY, OffsetX, OffsetY;
        public readonly HashSet<string> HitIds = new(StringComparer.Ordinal);
    }

    private sealed class Projectile
    {
        public long Id;
        public required Fighter Owner;
        public double X, Y, Vx, Vy, Radius, Damage, Force;
        public int Life;
    }
}
