const TICK_RATE = 60;
const MAX_LEAD_TICKS = 1.5;
const clamp = (value, minimum, maximum) => Math.max(minimum, Math.min(maximum, value));
const number = value => Number.isFinite(value) ? value : 0;

function canBlend(before, after) {
    return before && after
        && before.stocks === after.stocks && before.out === after.out
        && before.ownerId === after.ownerId && before.kind === after.kind
        && (after.action !== 'respawn' || before.action === 'respawn')
        && Math.abs(before.x - after.x) <= 180 && Math.abs(before.y - after.y) <= 200;
}

function predict(item, ticks) {
    const time = clamp(ticks, 0, MAX_LEAD_TICKS) / TICK_RATE;
    if (!time || item.out || item.combat?.capturedBy) return item;
    return { ...item, x: item.x + number(item.vx) * time, y: item.y + (item.grounded ? 0 : number(item.vy) * time) };
}

export class ArenaMotion {
    constructor() { this.reset(); }

    reset() {
        this.frames = [];
        this.clock = null;
        this.jitter = 0;
    }

    add(state, arrival) {
        if (!state || !Number.isFinite(state.tick) || !Number.isFinite(arrival)) return;
        const previous = this.frames.at(-1);
        if (previous) {
            if (state.stageId !== previous.stageId || state.suddenDeath !== previous.suddenDeath
                || state.tick < previous.tick || arrival - previous.arrival > 350) {
                this.reset();
            } else {
                if (state.tick === previous.tick) return;
                const expected = (state.tick - previous.tick) * 1000 / TICK_RATE;
                const variation = Math.abs(arrival - previous.arrival - expected);
                this.jitter = this.jitter * .82 + Math.min(60, variation) * .18;
            }
        }
        this.frames.push({
            tick: state.tick, arrival, stageId: state.stageId, suddenDeath: state.suddenDeath,
            fighters: new Map((state.fighters || []).map(fighter => [fighter.id, fighter])),
            projectiles: new Map((state.projectiles || []).map(projectile => [projectile.id, projectile]))
        });
        if (this.frames.length > 8) this.frames.shift();
    }

    sample(now, elapsed, state, previous, receivedAt, previousAt, ownId) {
        const latest = this.frames.at(-1);
        // The browser can receive two snapshots between animation frames.
        if (previous && previous.stageId === state.stageId && previous.tick < state.tick
            && (!latest || previous.tick > latest.tick)) {
            this.add(previous, previousAt);
        }
        this.add(state, receivedAt);
        const newest = this.frames.at(-1);
        const fighters = state.fighters || [];
        const projectiles = state.projectiles || [];
        if (!newest || state.phase !== 'playing') return { fighters, projectiles };

        const age = Math.max(0, number(now) - number(receivedAt)) * TICK_RATE / 1000;
        const delay = clamp(2 + this.jitter * TICK_RATE / 1000, 2, 4);
        const target = Math.min(newest.tick + MAX_LEAD_TICKS, newest.tick + age - delay);
        const step = clamp(number(elapsed), 0, .05) * TICK_RATE;
        if (this.clock === null) this.clock = target;
        else {
            const correction = (target - (this.clock + step)) * .24;
            this.clock = Math.min(newest.tick + MAX_LEAD_TICKS,
                this.clock + clamp(step + correction, 0, step * 1.35));
        }

        const upperIndex = this.frames.findIndex(frame => frame.tick >= this.clock);
        const upper = upperIndex < 0 ? newest : this.frames[upperIndex];
        const lower = upperIndex > 0 ? this.frames[upperIndex - 1] : upper;
        const span = upper.tick - lower.tick;
        const blend = span > 0 ? clamp((this.clock - lower.tick) / span, 0, 1) : 0;
        const last = this.frames.at(-2);
        const sampleItem = (item, group) => {
            if (last && !canBlend(last[group].get(item.id), item)) return item;
            const before = lower[group].get(item.id);
            const after = upper[group].get(item.id);
            if (!canBlend(before, after) || !canBlend(after, item)) return item;
            if (this.clock > newest.tick) return predict(item, this.clock - newest.tick);
            return {
                ...item, x: before.x + (after.x - before.x) * blend,
                y: before.y + (after.y - before.y) * blend
            };
        };
        return {
            fighters: fighters.map(fighter => fighter.id === ownId
                ? (!last || canBlend(last.fighters.get(fighter.id), fighter) ? predict(fighter, age) : fighter)
                : sampleItem(fighter, 'fighters')),
            projectiles: projectiles.map(projectile => sampleItem(projectile, 'projectiles'))
        };
    }
}
