import * as Y from 'yjs';

/**
 * Identify the integrated CRDT operations, independent of garbage collection.
 * UndoManager retains deleted structs on clients; server GC can encode the same
 * state differently. State vectors alone miss deletions, so include DeleteSet.
 * Keep this canonical JSON schema in sync with collaboration/src/fingerprint.mjs.
 */
export function canonicalFingerprint(doc) {
  const stateVector = [...Y.decodeStateVector(Y.encodeStateVector(doc))].sort((a, b) => a[0] - b[0]);
  const deleteSet = [...Y.decodeUpdate(Y.encodeStateAsUpdate(doc)).ds.clients]
    .sort((a, b) => a[0] - b[0])
    .map(([client, ranges]) => {
      const sorted = ranges.map(range => [range.clock, range.len]).sort((a, b) => a[0] - b[0]);
      const merged = [];
      for (const [clock, len] of sorted) {
        if (len <= 0) continue;
        const previous = merged.at(-1);
        if (previous && clock <= previous[0] + previous[1]) previous[1] = Math.max(previous[0] + previous[1], clock + len) - previous[0];
        else merged.push([clock, len]);
      }
      return [client, merged];
    });
  return JSON.stringify({ stateVector, deleteSet });
}
