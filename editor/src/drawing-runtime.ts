import * as Y from 'yjs';
import { HocuspocusProvider } from '@hocuspocus/provider';
import type { AppState, BinaryFileData, BinaryFiles, ExcalidrawImperativeAPI } from '@excalidraw/excalidraw/types';
import type { OrderedExcalidrawElement } from '@excalidraw/excalidraw/element/types';
import { collaborationUrl } from './collaboration-url.mjs';
import { canonicalFingerprint } from './persistence.mjs';
import { applyLocalDrawing, drawingElementComparisonKey, readDrawingScene, validateDrawingImage } from './drawing-state.mjs';
import { DrawingApiError, drawingRequest, requestDrawingContext, uploadDrawingFile, type DrawingContext, type DrawingEnvironment, type DrawingFile, type DrawingPublicationRequest } from './drawing-api';

let sdk: Promise<typeof import('@excalidraw/excalidraw')> | undefined;
export function loadDrawingSdk() {
  (window as unknown as { EXCALIDRAW_ASSET_PATH: string }).EXCALIDRAW_ASSET_PATH = new URL('/notion-editor/excalidraw/', location.origin).href;
  return sdk ??= import('@excalidraw/excalidraw').then(module => {
    // The pinned SDK ships Danish but omits it from its selectable language list.
    // Register its own locale before InitializeApp chooses the fallback language.
    if (!module.languages.some(language => language.code === 'da-DK')) module.languages.push({ code: 'da-DK', label: 'Dansk' });
    return module;
  }).catch(error => { sdk = undefined; throw error; });
}
function asDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result)); reader.onerror = () => reject(new Error('Billedet kunne ikke læses.')); reader.readAsDataURL(blob); });
}
async function fingerprint(doc: Y.Doc) {
  const bytes = new TextEncoder().encode(canonicalFingerprint(doc));
  return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)), item => item.toString(16).padStart(2, '0')).join('');
}
const copyElements = (items: readonly OrderedExcalidrawElement[]) => JSON.parse(JSON.stringify(items)) as OrderedExcalidrawElement[];
const wait = (milliseconds: number) => new Promise(resolve => setTimeout(resolve, milliseconds));

export class DrawingController {
  env: DrawingEnvironment;
  readonly id: string;
  readonly doc = new Y.Doc();
  context: DrawingContext | null = null;
  provider: HocuspocusProvider | undefined;
  error = '';
  status = 'Indlæser…';
  dirty = false;
  changeVersion = 0;
  connected = false;
  synced = false;
  permissionToEdit = false;
  elements: OrderedExcalidrawElement[] = [];
  files: BinaryFiles = {};
  background = '#ffffff';
  viewState: Partial<Pick<AppState, 'zoom' | 'scrollX' | 'scrollY' | 'selectedElementIds' | 'activeTool'>> = {};
  private api: ExcalidrawImperativeAPI | null = null;
  private baseline: OrderedExcalidrawElement[] = [];
  private pending = new Map<string, OrderedExcalidrawElement>();
  private pendingBackground = false;
  private fileReferences: Record<string, { assetId: string }> = {};
  private uploads = new Map<string, Promise<void>>();
  private metadata = new Map<string, DrawingFile>();
  private listeners = new Set<() => void>();
  private remote = false;
  private unacknowledged = false;
  private generation = 0;
  private previewNeeded = false;
  private previewTask: Promise<void> | undefined;
  private previewRetries = 0;
  private flushTimer: ReturnType<typeof setTimeout> | undefined;
  private previewTimer: ReturnType<typeof setTimeout> | undefined;
  private refreshTask: Promise<DrawingContext> | undefined;
  private destroyed = false;
  private needsFileProjection = false;
  private localOrigin = Symbol('drawing-local');
  onDirty: (controller: DrawingController) => void = () => {};

  constructor(env: DrawingEnvironment, id: string) {
    this.env = env; this.id = id;
    this.doc.on('update', (_update: Uint8Array, origin: unknown) => {
      this.generation++;
      if (origin !== this.localOrigin) this.applyRemote();
      this.emit();
    });
  }
  subscribe(listener: () => void) { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; }
  private emit() {
    if (this.destroyed) return;
    this.changeVersion++;
    this.dirty = this.unacknowledged || this.pending.size > 0 || this.pendingBackground || this.uploads.size > 0 || this.previewNeeded || !!this.previewTask;
    this.onDirty(this);
    for (const listener of this.listeners) listener();
  }
  get canEdit() { return this.permissionToEdit && this.connected && this.synced && !this.env.presentation; }
  async refresh() {
    this.refreshTask ??= requestDrawingContext(this.env, this.id).then(context => {
      if (this.destroyed) return context;
      const previous = this.context;
      this.context = context; this.permissionToEdit = context.canEditDrawing;
      for (const item of context.files) this.metadata.set(item.id, item);
      if (!this.provider && !this.unacknowledged && !this.pending.size) {
        if (context.stateBase64) Y.applyUpdate(this.doc, Uint8Array.from(atob(context.stateBase64), item => item.charCodeAt(0)), 'drawing-context');
        else if (context.sceneJson) {
          const scene = JSON.parse(context.sceneJson);
          this.doc.transact(() => {
            this.doc.getMap('elements').clear(); this.doc.getMap('files').clear(); this.doc.getMap('scene').clear();
            for (const item of scene.elements || []) this.doc.getMap('elements').set(item.id, item);
            for (const [id, value] of Object.entries(scene.files || {})) this.doc.getMap('files').set(id, value);
            this.doc.getMap('scene').set('viewBackgroundColor', scene.appState?.viewBackgroundColor || '#ffffff');
          }, 'drawing-context');
        }
        this.applyRemote();
      }
      if (this.previewNeeded && context.previewLightUrl && context.previewDarkUrl && context.revision === previous?.revision && context.fingerprint === previous.fingerprint && !this.unacknowledged && !this.pending.size && !this.pendingBackground && !this.uploads.size) {
        // Another writer may have repaired previews after this user's access changed.
        this.previewNeeded = false; this.error = '';
      }
      this.healPreview();
      this.status = context.canEditDrawing ? 'Klar' : 'Kun læsning'; this.emit(); return context;
    }).catch(error => {
      if (error instanceof DrawingApiError && (error.status === 403 || error.status === 404)) { this.context = null; this.permissionToEdit = false; this.error = error.message; this.emit(); }
      throw error;
    }).finally(() => { this.refreshTask = undefined; });
    return this.refreshTask;
  }
  async open() {
    this.error = ''; await this.refresh();
    if (this.env.presentation || this.context?.snapshotId || this.provider) return;
    this.status = 'Forbinder…';
    this.provider = new HocuspocusProvider({
      url: collaborationUrl(location),
      name: 'drawing:' + this.id.toLowerCase(), document: this.doc,
      token: async () => { const token = await this.env.getToken(); return token.startsWith('Bearer ') ? token.slice(7) : token; },
      onStatus: ({ status }) => {
        this.connected = status === 'connected';
        if (!this.connected) this.synced = false;
        this.status = this.connected ? 'Forbundet' : 'Forbindelsen er afbrudt'; this.emit();
      },
      onSynced: ({ state }) => {
        this.synced = state;
        if (state) {
          this.status = this.unacknowledged || this.pending.size || this.uploads.size ? 'Gemmer…' : this.context?.canEditDrawing ? 'Gemt' : 'Kun læsning';
          if (this.context?.canEditDrawing && !this.doc.getMap('scene').has('viewBackgroundColor')) {
            this.doc.transact(() => this.doc.getMap('scene').set('viewBackgroundColor', this.background), this.localOrigin);
            this.unacknowledged = true; this.previewNeeded = true;
            this.status = 'Gemmer…';
          }
          this.scheduleFlush();
          this.healPreview();
        }
        this.emit();
      },
      onAuthenticationFailed: ({ reason }) => {
        this.permissionToEdit = false;
        this.connected = false;
        this.synced = false;
        this.status = 'Ikke forbundet';
        this.error = reason.includes('editor_version_mismatch')
          ? 'Editoren er opdateret. Genindlæs siden for at åbne tegningen med den nye version.'
          : 'Du har ikke længere adgang til tegningen, eller dit login er udløbet. Log ind igen.';
        this.emit();
      },
      onStateless: ({ payload }) => { void this.receiveStatus(payload); },
    });
    this.provider.awareness?.setLocalStateField('user', { name: this.env.userName, id: this.env.userSub });
    this.provider.awareness?.on('change', () => this.updateCollaborators());
    this.emit();
  }
  private async receiveStatus(payload: string) {
    try {
      const message = JSON.parse(payload);
      if (message.type === 'permissions') { this.permissionToEdit = message.canEdit === true; this.healPreview(); this.emit(); return; }
      if (message.type === 'persistenceError') { this.error = 'Tegningen er ikke gemt. Du kan stadig eksportere den.'; this.emit(); return; }
      if (message.type !== 'persisted' || typeof message.fingerprintHash !== 'string') return;
      const generation = this.generation;
      const current = await fingerprint(this.doc);
      if (this.destroyed || generation !== this.generation || current !== message.fingerprintHash) return;
      if (this.context) {
        const revision = message.revision ?? this.context.revision;
        const changed = revision !== this.context.revision || current !== this.context.fingerprint;
        this.context = { ...this.context, revision, fingerprint: current, ...(changed ? { previewLightUrl: null, previewDarkUrl: null } : {}) };
      }
      if (this.unacknowledged) this.unacknowledged = false;
      // Any authorized writer can repair the current scene's previews, including
      // remote edits whose author disconnected or lost access before PNG upload.
      this.healPreview();
      this.emit();
    } catch { /* Ignore unrelated presence events. */ }
  }
  attach(api: ExcalidrawImperativeAPI) {
    if (this.api === api) return;
    this.api = api;
    this.baseline = copyElements(api.getSceneElementsIncludingDeleted());
    this.updateCollaborators();
    void this.loadFiles().catch(error => this.fail(error));
  }
  detach() {
    if (this.api) this.localChange(this.api.getSceneElementsIncludingDeleted(), this.api.getAppState(), this.api.getFiles());
    this.flush(); this.api = null; this.schedulePreview();
  }
  private fail(error: unknown) { if (this.destroyed) return; this.error = error instanceof Error ? error.message : 'Tegningen kunne ikke gemmes.'; this.emit(); }
  localChange(elements: readonly OrderedExcalidrawElement[], appState: AppState, files: BinaryFiles) {
    // Kept only in this browser controller, including for readonly drawing viewers.
    // Private navigation and selection never enter Yjs, storage or dirty tracking.
    this.viewState = { zoom: { ...appState.zoom }, scrollX: appState.scrollX, scrollY: appState.scrollY, selectedElementIds: { ...appState.selectedElementIds }, activeTool: { ...appState.activeTool } };
    // Keep the last drag frame if the socket disconnects before readonly mode renders.
    // The connected/synced gate in flush still prevents publishing it while offline.
    if (this.remote || !this.api || !this.permissionToEdit || this.env.presentation || this.context?.snapshotId) return;
    const before = new Map(this.baseline.map(item => [item.id, drawingElementComparisonKey(item)]));
    let changed = false;
    for (const item of elements) if (before.get(item.id) !== drawingElementComparisonKey(item)) { this.pending.set(item.id, JSON.parse(JSON.stringify(item))); changed = true; }
    const backgroundChanged = this.background !== appState.viewBackgroundColor;
    const addedFiles = Object.entries(files).filter(([id, item]) => this.files[id]?.dataURL !== item.dataURL);
    // Excalidraw also fires onChange for selection, pointers, menus and parent renders.
    // Publishing those back into React causes an update loop and resets private view state.
    if (!changed && !backgroundChanged && !addedFiles.length) return;
    this.elements = copyElements(elements); this.baseline = copyElements(elements);
    if (backgroundChanged) this.pendingBackground = true;
    this.background = appState.viewBackgroundColor;
    for (const [id, item] of addedFiles) {
      this.files[id] = item;
      if (!this.fileReferences[id] && !this.uploads.has(id)) this.startUpload(id, item);
    }
    if (this.pending.size || backgroundChanged) this.scheduleFlush();
    this.emit();
  }
  private startUpload(id: string, item: BinaryFileData) {
    const operation = (async () => {
      const response = await fetch(item.dataURL); const blob = await response.blob();
      const file = new File([blob], 'drawing-image.' + (blob.type === 'image/jpeg' ? 'jpg' : blob.type === 'image/webp' ? 'webp' : blob.type === 'image/gif' ? 'gif' : 'png'), { type: blob.type });
      const invalid = validateDrawingImage(file); if (invalid) throw new Error(invalid);
      const asset = await uploadDrawingFile(this.env, this.id, file);
      if (this.destroyed) return;
      this.metadata.set(asset.id, asset); this.fileReferences[id] = { assetId: asset.id };
      for (const element of this.elements) if (element.type === 'image' && element.fileId === id && !element.isDeleted) this.pending.set(element.id, { ...element, status: 'saved' } as OrderedExcalidrawElement);
      this.needsFileProjection = true;
    })();
    this.uploads.set(id, operation);
    void operation.catch(error => this.fail(error)).finally(() => { this.uploads.delete(id); this.scheduleFlush(); this.emit(); });
  }
  private scheduleFlush() { clearTimeout(this.flushTimer); this.flushTimer = setTimeout(() => this.flush(), 67); }
  private flush() {
    clearTimeout(this.flushTimer);
    if (!this.canEdit || this.destroyed) return;
    const canonical = readDrawingScene(this.doc);
    const byId = new Map(canonical.elements.map((item: OrderedExcalidrawElement) => [item.id, item]));
    for (const [id, item] of this.pending) byId.set(id, item);
    try {
      const changed = applyLocalDrawing(this.doc, { previousElements: canonical.elements, elements: [...byId.values()], fileReferences: this.fileReferences, viewBackgroundColor: this.background, previousBackground: canonical.appState.viewBackgroundColor }, this.localOrigin);
      for (const [id, item] of this.pending) if (drawingElementComparisonKey(this.doc.getMap('elements').get(id)) === drawingElementComparisonKey(item)) this.pending.delete(id);
      if (changed) { this.unacknowledged = true; this.previewNeeded = true; this.status = 'Gemmer…'; this.error = ''; }
      this.pendingBackground = false;
      if (this.needsFileProjection) { this.needsFileProjection = false; this.applyRemote(); }
      this.emit();
    } catch (error) { this.fail(error); }
  }
  private applyRemote() {
    const scene = readDrawingScene(this.doc);
    this.fileReferences = { ...scene.files, ...this.fileReferences };
    const records = new Map(scene.elements.map((item: OrderedExcalidrawElement) => [item.id, item]));
    for (const [id, item] of this.pending) records.set(id, item);
    // The SDK mutates element objects while drawing. Never hand it objects held inside Y.Map.
    this.elements = copyElements([...records.values()].sort((a, b) => a.index < b.index ? -1 : a.index > b.index ? 1 : a.id < b.id ? -1 : 1));
    if (!this.pendingBackground) this.background = scene.appState.viewBackgroundColor;
    if (this.api) {
      this.remote = true;
      try { this.api.updateScene({ elements: this.elements, appState: { viewBackgroundColor: this.background }, captureUpdate: 'NEVER' }); this.baseline = copyElements(this.api.getSceneElementsIncludingDeleted()); }
      finally { this.remote = false; }
      void this.loadFiles().catch(error => this.fail(error));
    }
  }
  async loadFiles() {
    const references = readDrawingScene(this.doc).files as Record<string, { assetId: string }>;
    const missing = Object.entries(references).filter(([id]) => !this.files[id]);
    if (missing.some(([, ref]) => !this.metadata.has(ref.assetId))) await this.refresh();
    const loaded: BinaryFileData[] = [];
    for (const [id, reference] of missing) {
      const asset = this.metadata.get(reference.assetId); if (!asset || this.files[id]) continue;
      const response = await fetch(asset.contentUrl, { cache: 'no-store' });
      if (!response.ok) throw new Error('Et billede i tegningen kunne ikke hentes.');
      this.files[id] = { id: id as BinaryFileData['id'], dataURL: await asDataUrl(await response.blob()) as BinaryFileData['dataURL'], mimeType: asset.mimeType as BinaryFileData['mimeType'], created: new Date(asset.created).getTime() };
      loaded.push(this.files[id]);
    }
    if (this.api && loaded.length) this.api.addFiles(loaded);
  }
  pointer(pointer: { x: number; y: number; tool: 'pointer' | 'laser' }, button: 'down' | 'up') {
    this.provider?.awareness?.setLocalStateField('cursor', { pointer, button, selectedElementIds: this.api?.getAppState().selectedElementIds ?? {} });
  }
  private updateCollaborators() {
    if (!this.api || !this.provider?.awareness) return;
    const collaborators = new Map();
    for (const [client, state] of this.provider.awareness.getStates()) {
      if (client === this.doc.clientID || !state.user) continue;
      collaborators.set(String(client), { username: state.user.name, id: state.user.id, ...state.cursor });
    }
    this.api.updateScene({ collaborators });
  }
  private schedulePreview() {
    clearTimeout(this.previewTimer);
    if (!this.error && this.previewNeeded && !this.pending.size && !this.pendingBackground && !this.unacknowledged && !this.uploads.size) this.previewTimer = setTimeout(() => { void this.ensurePreview().catch(error => this.fail(error)); }, 350);
  }
  private healPreview() {
    if (!this.canEdit || !this.context?.canEditDrawing || this.context.snapshotId) return;
    if (!this.context.previewLightUrl || !this.context.previewDarkUrl) {
      this.previewNeeded = true; this.status = 'Gemmer…';
    }
    this.schedulePreview();
  }
  async ensurePreview() {
    if (!this.context || this.env.presentation || !this.context.canEditDrawing || this.context.snapshotId) return;
    if (this.previewTask) return this.previewTask;
    if (this.unacknowledged || this.pending.size || this.pendingBackground || this.uploads.size) return;
    if (!this.previewNeeded && this.context.previewLightUrl && this.context.previewDarkUrl) return;
    const generation = this.generation;
    const context = this.context;
    if (!context.fingerprint) return;
    // A head-context refresh can arrive before the corresponding remote Yjs update.
    // Wait for its matching durable ack rather than uploading a PNG of the older scene.
    if (await fingerprint(this.doc) !== context.fingerprint || generation !== this.generation || this.destroyed) return;
    if (this.previewTask) return this.previewTask;
    this.previewNeeded = true;
    this.previewTask = (async () => {
      await this.loadFiles(); const module = await loadDrawingSdk();
      const elements = this.elements.filter(item => !item.isDeleted);
      const base = { elements, files: this.files, mimeType: 'image/png', maxWidthOrHeight: 2048 };
      const preview = async (dark: boolean) => {
        if (elements.length) return module.exportToBlob({ ...base, appState: { viewBackgroundColor: this.background, exportBackground: true, exportWithDarkMode: dark, exportScale: 1, exportEmbedScene: false } });
        const canvas = document.createElement('canvas'); canvas.width = 512; canvas.height = 160;
        const context = canvas.getContext('2d')!; context.fillStyle = dark ? '#121212' : this.background; context.fillRect(0, 0, canvas.width, canvas.height);
        return new Promise<Blob>((resolve, reject) => canvas.toBlob(value => value ? resolve(value) : reject(new Error('Forhåndsvisningen kunne ikke oprettes.')), 'image/png'));
      };
      const light = await preview(false), dark = await preview(true);
      if (generation !== this.generation || this.pending.size || this.pendingBackground || this.unacknowledged) return;
      const body = new FormData(); body.append('expectedRevision', String(context.revision)); body.append('expectedFingerprint', context.fingerprint!); body.append('lightPNG', light, 'drawing-light.png'); body.append('darkPNG', dark, 'drawing-dark.png');
      const result = await drawingRequest<{ revision: number; fingerprint: string; previewLightUrl: string; previewDarkUrl: string }>(this.env, '/' + this.id + '/previews', { method: 'POST', body });
      if (generation === this.generation && this.context) { this.context = { ...this.context, previewLightUrl: result.previewLightUrl, previewDarkUrl: result.previewDarkUrl }; this.previewNeeded = false; this.previewRetries = 0; this.status = 'Gemt'; this.error = ''; }
    })().catch(async error => {
      if (error instanceof DrawingApiError && error.status === 409 && ++this.previewRetries <= 3) { await this.refresh(); this.previewNeeded = true; return; }
      this.fail(error); throw error;
    }).finally(() => { this.previewTask = undefined; this.emit(); this.schedulePreview(); });
    this.emit(); return this.previewTask;
  }
  async waitForSaved() {
    this.flush();
    const deadline = Date.now() + 20000;
    while ((this.pending.size || this.pendingBackground || this.unacknowledged || this.uploads.size) && Date.now() < deadline) { if (this.error || !this.connected) throw new Error(this.error || 'Vent på forbindelsen, før du udgiver.'); await wait(80); this.flush(); }
    if (this.pending.size || this.pendingBackground || this.unacknowledged || this.uploads.size) throw new Error('Tegningen er ikke færdig med at gemme. Prøv igen.');
    await this.ensurePreview();
    if (this.previewNeeded || this.previewTask || this.error) throw new Error(this.error || 'Tegningens forhåndsvisning er ikke gemt endnu.');
  }
  async publication(): Promise<DrawingPublicationRequest> {
    if (!this.context) await this.refresh();
    await this.waitForSaved();
    if (!this.context?.fingerprint) throw new Error('Vent på, at tegningen er gemt.');
    return { drawingId: this.id, mode: this.context.mode, expectedRevision: this.context.revision, expectedFingerprint: this.context.fingerprint };
  }
  async retrySave() {
    this.error = ''; this.previewRetries = 0; await this.refresh();
    if (!this.provider) await this.open();
    else if (!this.connected) this.provider.connect();
    for (const [id, item] of Object.entries(this.files)) if (!this.fileReferences[id] && !this.uploads.has(id)) this.startUpload(id, item);
    this.flush(); this.schedulePreview(); this.emit();
  }
  destroy() {
    this.destroyed = true; clearTimeout(this.flushTimer); clearTimeout(this.previewTimer);
    this.provider?.destroy(); this.doc.destroy(); this.listeners.clear();
  }
}
