export type AiBlock = { id?: string; type: string; props?: Record<string, unknown>; content?: unknown; children?: AiBlock[] };
export type AiPosition = { relative?: unknown; relativeOffset?: number; blockId: string; offset: number };
export type AiNativeTarget = { blockIds: string[]; selection?: { from: AiPosition; to: AiPosition; slice: unknown; text: string } };
export type AiHunk = {
  id: string; version: number; order: number;
  operation: 'insert_after' | 'insert_before' | 'replace' | 'delete' | 'replace_selection';
  targetBlockIds: string[]; baseFingerprint: string; before: AiBlock[]; after: AiBlock[];
  status: 'ready' | 'accepted' | 'rejected' | 'conflict'; explanation: string; sourceIds: string[];
  target?: AiNativeTarget | null;
};
export type AiSource = { id: string; kind: string; sourceId: string; title: string; href: string; excerpt?: string; location?: string; state: string };
export type AiRun = {
  id: string; pageId: string; instruction: string; mode: string; scope: string;
  status: 'queued' | 'gathering' | 'generating' | 'ready' | 'needs_continuation' | 'cancelled' | 'failed';
  hunks: AiHunk[]; sources: AiSource[];
  coverage: { activeBlocks: number; totalBlocks: number; activeTokens: number; externalTokens: number; partial: boolean; detail?: string };
  progress: string; error?: string; model: string; eventCursor: number; outputTokens: number;
  createdAtUtc: string; expiresAtUtc: string;
};
export type AiCreateRequest = { pageId: string; instruction: string; mode: string; scope: string; document: unknown; target: AiNativeTarget; excludedSourceIds: string[]; clientRequestId: string };
export type AiRunSummary = Pick<AiRun, 'id' | 'pageId' | 'instruction' | 'mode' | 'scope' | 'status' | 'createdAtUtc' | 'expiresAtUtc'>;
export const aiRunWorking = (run?: AiRun | null) => !!run && ['queued', 'gathering', 'generating'].includes(run.status);
