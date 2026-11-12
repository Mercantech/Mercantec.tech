const buttons = Object.entries({ jump: 1, attack: 2, special: 4, shield: 8, grab: 16, smash: 32, dodge: 64 });
export const arenaNeutralInput = Object.freeze({ horizontal: 0, vertical: 0,
    jump: false, attack: false, special: false, shield: false, grab: false, smash: false, dodge: false });

// Keep the newest held state and the presses that happened while transport was busy.
// A delayed connection must not fire a backlog of old attacks.
export class ArenaInputBuffer {
    constructor() {
        this.edges = new Map();
        this.clear();
    }

    get pressed() {
        let mask = 0;
        for (const bit of this.edges.keys()) mask |= bit;
        return mask;
    }

    clear() {
        this.held = arenaNeutralInput;
        this.dirty = true;
        this.edges.clear();
    }

    update(held, now) {
        this.expire(now);
        let changed = held.horizontal !== this.held.horizontal || held.vertical !== this.held.vertical;
        for (const [button, bit] of buttons) {
            if (held[button] === this.held[button]) continue;
            changed = true;
            if (held[button]) {
                this.edges.delete(bit);
                this.edges.set(bit, { at: now, horizontal: held.horizontal, vertical: held.vertical });
            }
        }
        this.held = held;
        this.dirty ||= changed;
        return changed;
    }

    expire(now) {
        for (const [bit, edge] of this.edges) if (now - edge.at > 200) this.edges.delete(bit);
    }

    packet(sequence, now) {
        this.expire(now);
        const packet = { type: 'input', sequence, ...this.held };
        if (this.edges.size) {
            const edge = Array.from(this.edges.values()).at(-1);
            Object.assign(packet, { pressed: this.pressed, pressHorizontal: edge.horizontal, pressVertical: edge.vertical });
        }
        return packet;
    }

    sent() { this.edges.clear(); this.dirty = false; }
}
