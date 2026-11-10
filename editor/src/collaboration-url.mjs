/** @param {Pick<Location, 'protocol' | 'host'>} location */
export function collaborationUrl({ protocol, host }) {
  const socketProtocol = protocol === 'https:' ? 'wss:' : 'ws:';
  return `${socketProtocol}//${host}/collaboration?schemaVersion=9`;
}
