import type { AiCreateRequest, AiRun, AiRunSummary } from './ai-native-types';
import { aiNativeError } from './ai-native-errors';

const base = '/api/ai/editor/runs';
export function createNativeAiApi(getToken: () => Promise<string>) {
  const request = async (path: string, method = 'GET', body?: unknown, signal?: AbortSignal) => {
    const token = await getToken();
    const response = await fetch(base + path, { method, signal, cache: 'no-store', headers: { Authorization: 'Bearer ' + token, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) }, body: body === undefined ? undefined : JSON.stringify(body) });
    if (!response.ok) {
      let message = 'AI-opgaven kunne ikke hentes. Prøv igen.';
      try { const error = await response.json(); message = aiNativeError(error.detail || error.message || error.error, message); } catch { /* Non-JSON proxy errors retain an actionable message. */ }
      throw new Error(typeof message === 'string' ? message : 'AI-opgaven kunne ikke hentes.');
    }
    if (response.status === 204) return undefined;
    return response.json();
  };
  return {
    list: (pageId: string, signal?: AbortSignal): Promise<AiRunSummary[]> => request('?pageId=' + encodeURIComponent(pageId), 'GET', undefined, signal),
    read: (id: string): Promise<AiRun> => request('/' + encodeURIComponent(id)),
    create: (body: AiCreateRequest): Promise<AiRun> => request('', 'POST', body),
    adjust: (id: string, body: unknown): Promise<AiRun> => request('/' + encodeURIComponent(id) + '/adjust', 'POST', body),
    continue: (id: string, body: unknown): Promise<AiRun> => request('/' + encodeURIComponent(id) + '/continue', 'POST', body),
    cancel: (id: string): Promise<AiRun> => request('/' + encodeURIComponent(id) + '/cancel', 'POST'),
    review: (id: string, hunkId: string, status: string, version: number): Promise<AiRun> => request('/' + encodeURIComponent(id) + '/hunks/' + encodeURIComponent(hunkId), 'PATCH', { status, version }),
    remove: (id: string) => request('/' + encodeURIComponent(id), 'DELETE'),
    async stream(id: string, after: number, signal: AbortSignal, onRun: (run: AiRun) => void, onDelta: (text: string) => void) {
      const token = await getToken();
      const response = await fetch(base + '/' + encodeURIComponent(id) + '/events?after=' + after, { signal, cache: 'no-store', headers: { Authorization: 'Bearer ' + token, Accept: 'text/event-stream' } });
      if (!response.ok || !response.body) throw new Error(response.status === 403 ? 'Du har ikke længere adgang til denne side.' : 'Forbindelsen til AI blev afbrudt. Forslagene er gemt.');
      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      try {
        while (!signal.aborted) {
          const next = await reader.read();
          buffer = (buffer + decoder.decode(next.value, { stream: !next.done })).replace(/\r\n/g, '\n');
          let end: number;
          while ((end = buffer.indexOf('\n\n')) >= 0) {
            const frame = buffer.slice(0, end); buffer = buffer.slice(end + 2);
            let event = 'message'; const data: string[] = [];
            for (const line of frame.split('\n')) {
              if (line.startsWith('event:')) event = line.slice(6).trim();
              if (line.startsWith('data:')) data.push(line.slice(5).trimStart());
            }
            if (!data.length) continue;
            const value = JSON.parse(data.join('\n'));
            if (event === 'run') onRun(value);
            else if (event === 'delta' && typeof value.text === 'string') onDelta(value.text);
          }
          if (next.done) return;
        }
      } finally { reader.releaseLock(); }
    },
  };
}
