import { createHash } from 'node:crypto';
import * as Y from 'yjs';

// A client UndoManager can retain deleted Item content while the server has
// garbage-collected it. The binary updates differ, although their CRDT state is
// identical. Clocks plus deleted ranges describe progress independently of GC.
export function fingerprintHash(state, stateVector) {
  const vector = [...Y.decodeStateVector(stateVector)].sort((a, b) => a[0] - b[0]);
  const deleted = [...Y.decodeUpdate(state).ds.clients].sort((a, b) => a[0] - b[0]);
  const deleteSet = deleted.map(([client, ranges]) => {
    const merged = [];
    for (const range of [...ranges].sort((a, b) => a.clock - b.clock)) {
      const previous = merged.at(-1);
      if (previous && range.clock <= previous[0] + previous[1]) {
        previous[1] = Math.max(previous[0] + previous[1], range.clock + range.len) - previous[0];
      } else {
        merged.push([range.clock, range.len]);
      }
    }
    return [client, merged];
  });
  return createHash('sha256').update(JSON.stringify({ stateVector: vector, deleteSet })).digest('hex');
}
