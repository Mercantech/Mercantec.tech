import React, { createContext, useContext, useEffect, useMemo } from 'react';
import { DrawingController } from './drawing-runtime';
import type { DrawingEnvironment, DrawingPublicationRequest } from './drawing-api';
export { requestCreateDrawing } from './drawing-api';
export type { DrawingEnvironment } from './drawing-api';

const scopes = new Set<DrawingScope>();
const controllers = new Map<string, { value: DrawingController; users: number }>();
const requests = new Set<string>();
const key = (env: DrawingEnvironment) => `${env.domain}:${env.documentId}:${env.published ? 'published:' + JSON.stringify([...(env.bindings || [])].sort((a, b) => a.drawingId < b.drawingId ? -1 : 1)) : 'head'}`;

class DrawingScope {
  env: DrawingEnvironment;
  private owned = new Map<string, { controller: DrawingController; unsubscribe: () => void }>();
  private lastDirty = false;
  constructor(env: DrawingEnvironment) { this.env = env; }
  get(id: string) {
    const identity = key(this.env) + ':' + (this.env.presentation ? 'presentation:' : '') + id;
    let entry = this.owned.get(identity);
    if (!entry) {
      let shared = controllers.get(identity);
      if (!shared) { shared = { value: new DrawingController(this.env, id), users: 0 }; controllers.set(identity, shared); }
      shared.users++;
      const controller = shared.value;
      entry = { controller, unsubscribe: controller.subscribe(() => this.reportDirty()) };
      this.owned.set(identity, entry);
    }
    entry.controller.env = this.env;
    return entry.controller;
  }
  reportDirty() { const dirty = [...this.owned.values()].some(item => item.controller.dirty); if (dirty !== this.lastDirty) { this.lastDirty = dirty; this.env.onDirty?.(dirty); } }
  discard(controller: DrawingController) {
    for (const [identity, item] of this.owned) if (item.controller === controller) {
      item.unsubscribe(); const shared = controllers.get(identity);
      if (shared && --shared.users === 0) { shared.value.destroy(); controllers.delete(identity); }
      this.owned.delete(identity);
    }
    this.reportDirty();
  }
  destroy() {
    scopes.delete(this);
    for (const [identity, item] of this.owned) {
      item.unsubscribe(); const shared = controllers.get(identity);
      if (shared && --shared.users === 0) { shared.value.destroy(); controllers.delete(identity); }
    }
    this.owned.clear(); if (this.lastDirty) this.env.onDirty?.(false);
  }
}
const DrawingContext = createContext<{ env: DrawingEnvironment; scope: DrawingScope } | null>(null);
export function DrawingEnvironmentProvider({ value, children }: { value: DrawingEnvironment; children: React.ReactNode }) {
  const identity = key(value);
  const scope = useMemo(() => new DrawingScope(value), [identity]);
  scope.env = value;
  useEffect(() => { scopes.add(scope); return () => scope.destroy(); }, [scope]);
  return <DrawingContext.Provider value={{ env: value, scope }}>{children}</DrawingContext.Provider>;
}
export function useDrawingEnvironment() { return useContext(DrawingContext); }
export function requestOpenDrawing(blockId: string) {
  requests.add(blockId); window.dispatchEvent(new CustomEvent('lup-open-drawing', { detail: { blockId } }));
}
export function consumeDrawingOpen(blockId: string) { return requests.delete(blockId); }

export async function collectDrawingPublicationData(env: DrawingEnvironment, blocks: { type: string; props?: Record<string, unknown>; children?: any[] }[]): Promise<DrawingPublicationRequest[]> {
  const ids = new Set<string>();
  const visit = (block: typeof blocks[number]) => {
    if (block.type === 'drawing') {
      const id = block.props?.drawingId;
      if (typeof id !== 'string' || !/^[0-9a-f-]{36}$/i.test(id)) throw new Error('Opret eller fjern den tomme tegning, før du udgiver.');
      ids.add(id);
    }
    for (const child of block.children || []) visit(child);
  };
  blocks.forEach(visit);
  const scope = [...scopes].find(item => key(item.env) === key(env));
  const result: DrawingPublicationRequest[] = [];
  for (const id of ids) {
    const temporary = !scope;
    const controller = scope ? scope.get(id) : new DrawingController(env, id);
    try { result.push(await controller.publication()); } finally { if (temporary) controller.destroy(); }
  }
  return result;
}
