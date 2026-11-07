export interface DrawingEnvironment {
  domain: 'notion' | 'notebook';
  documentId: string;
  published: boolean;
  readOnly: boolean;
  presentation?: boolean;
  theme: 'light' | 'dark';
  getToken: () => Promise<string>;
  userName: string;
  userSub?: string;
  onDirty?: (dirty: boolean) => void;
  bindings?: { drawingId: string; blockId: string; mode: string; snapshotId?: string | null; revision: number; fingerprint: string }[];
}
export interface DrawingFile { id: string; mimeType: string; sizeBytes: number; created: string; contentUrl: string }
export interface DrawingContext {
  id: string; domain: 'notion' | 'notebook'; documentId: string; blockId: string;
  canRead: boolean; canEditDrawing: boolean; canManageDrawing: boolean; mode: 'snapshot' | 'live';
  revision: number; fingerprint: string | null; accessVersion: number;
  stateBase64: string | null; sceneJson: string | null; snapshotId: string | null;
  previewLightUrl: string | null; previewDarkUrl: string | null; files: DrawingFile[]; updatedAtUtc: string;
}
export interface DrawingGrant { id: string; userSub: string | null; holdId: string | null; displayName: string }
export interface DrawingCandidates { people: { id: string; displayName: string }[]; holds: { id: string; displayName: string }[] }
export interface DrawingPublicationRequest { drawingId: string; mode: string; expectedRevision: number; expectedFingerprint: string }
export class DrawingApiError extends Error { constructor(message: string, readonly status: number) { super(message); } }
export class DrawingParentMismatchError extends Error { constructor() { super('Tegningen tilhører en anden side.'); } }

export async function drawingRequest<T>(env: DrawingEnvironment, path: string, init: RequestInit = {}): Promise<T> {
  const token = await env.getToken();
  init.signal?.throwIfAborted();
  if (!token) throw new Error('Log ind igen for at åbne tegningen.');
  const headers = new Headers(init.headers);
  headers.set('Authorization', token.startsWith('Bearer ') ? token : 'Bearer ' + token);
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json');
  const response = await fetch('/api/drawings' + path, { ...init, headers, cache: 'no-store' });
  if (!response.ok) {
    const message = response.status === 401 ? 'Log ind igen for at åbne tegningen.'
      : response.status === 403 || response.status === 404 ? 'Du har ikke længere adgang til tegningen.'
      : response.status === 409 ? 'Tegningen er ændret. Vent på lagring, og prøv igen.'
      : response.status === 413 ? 'Tegningen eller billedet er for stort. Du kan stadig eksportere din tegning.'
      : response.status === 400 ? 'Tegningen eller filen er ugyldig.'
      : 'Tegningen kunne ikke gemmes. Kontrollér forbindelsen, og prøv igen.';
    throw new DrawingApiError(message, response.status);
  }
  return (response.status === 204 ? undefined : await response.json()) as T;
}

export function requestCreateDrawing(env: DrawingEnvironment, blockId: string, sourceDrawingId?: string) {
  return drawingRequest<DrawingContext>(env, '', { method: 'POST', body: JSON.stringify({ domain: env.domain, documentId: env.documentId, blockId, sourceDrawingId }) });
}
export async function requestDrawingContext(env: DrawingEnvironment, id: string) {
  const query = new URLSearchParams();
  if (env.published) query.set('published', 'true');
  const binding = env.bindings?.find(item => item.drawingId.toLowerCase() === id.toLowerCase());
  if (binding?.snapshotId) query.set('snapshotId', binding.snapshotId);
  const context = await drawingRequest<DrawingContext>(env, '/' + encodeURIComponent(id) + '/context' + (query.size ? '?' + query : ''));
  if (context.domain !== env.domain || context.documentId.toLowerCase() !== env.documentId.toLowerCase()) throw new DrawingParentMismatchError();
  return context;
}
export function uploadDrawingFile(env: DrawingEnvironment, id: string, file: File) {
  const body = new FormData(); body.append('file', file, file.name);
  return drawingRequest<DrawingFile>(env, '/' + encodeURIComponent(id) + '/assets', { method: 'POST', body });
}
