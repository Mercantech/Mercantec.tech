import { blockToNode, createExtension, nodeToBlock, trackPosition, type BlockNoteEditor } from '@blocknote/core';
import { Plugin, PluginKey, type EditorState } from 'prosemirror-state';
import { Decoration, DecorationSet } from 'prosemirror-view';
import type { Node as PmNode } from 'prosemirror-model';
import { absolutePositionToRelativePosition, relativePositionToAbsolutePosition, ySyncPluginKey, yUndoPluginKey } from 'y-prosemirror';
import * as Y from 'yjs';
import { canonicalAiJson, aiReceiptKey, aiHasReceipt, findAiBlockNodes, aiSelectionSliceMatches, buildAiReviewTransaction } from './ai-review-core.mjs';
import type { AiBlock, AiHunk, AiNativeTarget, AiPosition, AiRun } from './ai-native-types';

type Editor = BlockNoteEditor<any, any, any>;
type Prepared = { id: string; order: number; from: number; to: number; nodes: PmNode[]; selection: boolean };
const reviewKey = new PluginKey('nativeAiReview');
const reviewable = (hunk: AiHunk) => hunk.status === 'ready' || hunk.status === 'conflict';

function containingBlock(doc: PmNode, pos: number) {
  const resolved = doc.resolve(pos);
  for (let depth = resolved.depth; depth > 0; depth--) {
    if (typeof resolved.node(depth).attrs.id === 'string') return { id: resolved.node(depth).attrs.id as string, pos: resolved.before(depth) };
  }
  return null;
}

/** Private review state lives outside the editor document and is never serialized by BlockNote. */
export class NativeAiReview {
  editor?: Editor;
  run: AiRun | null = null;
  version = 0;
  enabled = false;
  conflicts = new Map<string, string>();
  onAccept?: (ids: string[]) => void;
  onReject?: (ids: string[]) => void;
  onAdjust?: (id: string) => void;
  private listeners = new Set<() => void>();
  private runtimeAnchors = new WeakMap<AiNativeTarget, { from: () => number; to: () => number }>();
  private scheduled = false;
  constructor(readonly ydoc: Y.Doc) {}
  subscribe = (listener: () => void) => { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; };
  snapshot = () => this.version;
  private emit() { this.version++; for (const listener of this.listeners) listener(); }
  refresh() {
    if (this.editor?.prosemirrorView) this.editor.transact(tr => { tr.setMeta(reviewKey, true).setMeta('addToHistory', false); });
    this.emit();
  }
  documentChanged() {
    if (this.scheduled) return;
    this.scheduled = true;
    queueMicrotask(() => {
      this.scheduled = false;
      if (!this.editor?.prosemirrorView) return;
      this.inspect(this.editor.prosemirrorState.doc);
      // Yjs updates its position mapping after the editor transaction. Rebuild here,
      // rather than resolving old Yjs positions against the new ProseMirror document.
      this.refresh();
    });
  }
  setRun(run: AiRun | null) {
    this.run = run;
    if (this.editor) this.inspect(this.editor.prosemirrorState.doc);
    this.refresh();
  }
  hasReceipt(hunk: AiHunk) { return aiHasReceipt(this.ydoc.getMap('ai-review-receipts'), hunk); }
  capture(blockId?: string): AiNativeTarget {
    if (!this.editor) throw new Error('Editoren er ikke klar.');
    const editor = this.editor; const state = editor.prosemirrorState;
    const ids: string[] = [];
    if (!blockId && !state.selection.empty) {
      const { from, to } = state.selection;
      state.doc.nodesBetween(from, to, (node, pos) => {
        if (typeof node.attrs.id === 'string' && node.firstChild && pos + 1 < to && pos + 1 + node.firstChild.nodeSize > from) ids.push(node.attrs.id);
      });
      const selection = { from: this.position(from, 'right'), to: this.position(to, 'left'), slice: state.doc.slice(from, to).toJSON(), text: state.doc.textBetween(from, to, '\n') };
      const target = { blockIds: [...new Set(ids)], selection };
      this.runtimeAnchors.set(target, { from: trackPosition(editor, from, 'right'), to: trackPosition(editor, to, 'left') });
      return target;
    }
    const id = blockId || editor.getTextCursorPosition().block.id;
    if (!editor.getBlock(id)) throw new Error('Blokken findes ikke længere. Vælg et nyt sted.');
    return { blockIds: [id] };
  }
  private position(pos: number, side: 'left' | 'right'): AiPosition {
    const editor = this.editor!; const state = editor.prosemirrorState;
    const block = containingBlock(state.doc, pos);
    if (!block) throw new Error('Markér teksten inde i dokumentet.');
    const binding = ySyncPluginKey.getState(state)?.binding;
    let relative: unknown;
    if (binding) {
      try { relative = Y.relativePositionToJSON(absolutePositionToRelativePosition(pos + (side === 'right' ? 1 : -1), binding.type, binding.mapping)); } catch { /* Stable block offsets remain a conservative fallback. */ }
    }
    return { blockId: block.id, offset: pos - block.pos, ...(relative ? { relative, relativeOffset: side === 'right' ? -1 : 1 } : {}) };
  }
  private resolvePosition(position: AiPosition, doc: PmNode) {
    const state = this.editor!.prosemirrorState;
    const binding = ySyncPluginKey.getState(state)?.binding;
    if (position.relative && binding) {
      const resolved = relativePositionToAbsolutePosition(this.ydoc, binding.type, Y.createRelativePositionFromJSON(position.relative), binding.mapping);
      if (resolved !== null) {
        const pos = resolved + (position.relativeOffset || 0);
        if (pos >= 0 && pos <= doc.content.size && containingBlock(doc, pos)?.id === position.blockId) return pos;
      }
      throw new Error('Målteksten er slettet. Få forslaget opdateret.');
    }
    const block = findAiBlockNodes(doc, [position.blockId])[0];
    if (!block || position.offset >= block.node.nodeSize) throw new Error('Målteksten er ændret eller slettet.');
    return block.from + position.offset;
  }
  private selectionRange(hunk: AiHunk, doc: PmNode) {
    const target = hunk.target;
    if (!target?.selection) throw new Error('Markeringen mangler. Vælg teksten igen og justér forslaget.');
    const local = this.runtimeAnchors.get(target);
    const from = local ? local.from() : this.resolvePosition(target.selection.from, doc);
    const to = local ? local.to() : this.resolvePosition(target.selection.to, doc);
    if (!aiSelectionSliceMatches(doc, from, to, target.selection.slice)) throw new Error('Teksten eller formateringen i markeringen er ændret. Få forslaget opdateret.');
    return { from, to };
  }
  captureHunkTarget(hunk: AiHunk): AiNativeTarget {
    if (!this.editor) throw new Error('Editoren er ikke klar.');
    if (hunk.operation !== 'replace_selection') return { blockIds: hunk.targetBlockIds };
    if (!hunk.target?.selection) throw new Error('Markér teksten igen for at justere dette forslag.');
    const doc = this.editor.prosemirrorState.doc;
    const from = this.resolvePosition(hunk.target.selection.from, doc);
    const to = this.resolvePosition(hunk.target.selection.to, doc);
    if (from >= to) throw new Error('Målteksten er slettet. Markér det nye område, og start en ny opgave.');
    return { blockIds: hunk.targetBlockIds, selection: { from: this.position(from, 'right'), to: this.position(to, 'left'), slice: doc.slice(from, to).toJSON(), text: doc.textBetween(from, to, '\n') } };
  }
  prepare(hunk: AiHunk, doc = this.editor!.prosemirrorState.doc): Prepared {
    if (this.hasReceipt(hunk)) throw new Error('Forslaget er allerede godkendt.');
    let from: number; let to: number;
    const selection = hunk.operation === 'replace_selection';
    if (selection) ({ from, to } = this.selectionRange(hunk, doc));
    else {
      const blocks = findAiBlockNodes(doc, hunk.targetBlockIds);
      if (!blocks.length || blocks.length !== hunk.targetBlockIds.length) throw new Error('En målblok er slettet. Få forslaget opdateret.');
      const current = blocks.map(block => nodeToBlock(block.node, doc));
      if (canonicalAiJson(current) !== canonicalAiJson(hunk.before)) throw new Error('Målteksten er ændret. Få forslaget opdateret mod den nye tekst.');
      for (let index = 1; index < blocks.length; index++) if (blocks[index - 1].to !== blocks[index].from) throw new Error('Målblokkene er flyttet fra hinanden. Få forslaget opdateret.');
      from = blocks[0].from; to = blocks[blocks.length - 1].to;
      if (hunk.operation === 'insert_after') from = to;
      if (hunk.operation === 'insert_before') to = from;
      if (hunk.operation === 'insert_after' || hunk.operation === 'insert_before') {
        const siblings: { order: number; from: number; to: number }[] = [];
        for (const receipt of this.ydoc.getMap<any>('ai-review-receipts').values()) {
          if (receipt.runId !== this.run?.id || receipt.operation !== hunk.operation || canonicalAiJson(receipt.targetBlockIds) !== canonicalAiJson(hunk.targetBlockIds) || !Array.isArray(receipt.insertedBlockIds)) continue;
          const nodes = findAiBlockNodes(doc, receipt.insertedBlockIds);
          if (nodes.length) siblings.push({ order: receipt.order, from: nodes[0].from, to: nodes[nodes.length - 1].to });
        }
        const lower = siblings.filter(item => item.order < hunk.order).sort((a, b) => b.order - a.order)[0];
        const higher = siblings.filter(item => item.order > hunk.order).sort((a, b) => a.order - b.order)[0];
        from = to = lower?.to ?? higher?.from ?? from;
      }
    }
    const schema = doc.type.schema;
    const existingIds = new Set<string>();
    doc.descendants(node => { if (typeof node.attrs.id === 'string') existingIds.add(node.attrs.id); });
    const replacedIds = new Set<string>();
    if (selection) {
      // Only fully selected containers may keep their IDs. Partial edge containers
      // stay in the document and keep their original outside text and identity.
      doc.descendants((node, pos) => { if (typeof node.attrs.id === 'string' && pos >= from && pos + node.nodeSize <= to) replacedIds.add(node.attrs.id); });
    } else if (hunk.operation === 'replace' || hunk.operation === 'delete') {
      doc.nodesBetween(from, to, node => { if (typeof node.attrs.id === 'string') replacedIds.add(node.attrs.id); });
    }
    const usedIds = new Set<string>();
    const convert = (block: AiBlock): any => {
      const config = this.editor!.schema.blockSchema[block.type];
      if (!config || !schema.nodes[block.type]) throw new Error('Forslaget indeholder en bloktype, som editoren ikke understøtter.');
      const converted: any = { type: block.type };
      if (block.props) converted.props = block.props;
      if (config.content !== 'none' && block.content !== undefined) converted.content = block.content;
      else if (config.content === 'none' && block.content !== undefined && block.content !== null && block.content !== '' && !(Array.isArray(block.content) && block.content.length === 0)) throw new Error('Forslaget indeholder tekst i en blok uden tekstindhold. Få forslaget justeret.');
      if (block.id && existingIds.has(block.id)) {
        if (!replacedIds.has(block.id) || usedIds.has(block.id)) throw new Error('Forslaget genbruger et blok-id uden for det berørte område. Få forslaget justeret.');
        converted.id = block.id; usedIds.add(block.id);
      }
      if (block.children !== undefined) {
        if (!Array.isArray(block.children)) throw new Error('Forslagets underblokke kunne ikke læses.');
        converted.children = block.children.map(convert);
      }
      return converted;
    };
    const nodes = hunk.after.map(block => {
      return blockToNode(convert(block), schema);
    });
    return { id: hunk.id, order: hunk.order, from, to, nodes: hunk.operation === 'delete' ? [] : nodes, selection };
  }
  private inspect(doc: PmNode) {
    const conflicts = new Map<string, string>();
    for (const hunk of this.run?.hunks || []) {
      if (!reviewable(hunk) || this.hasReceipt(hunk)) continue;
      try { this.prepare(hunk, doc); } catch (error) { conflicts.set(hunk.id, error instanceof Error ? error.message : 'Målteksten er ændret.'); }
    }
    this.conflicts = conflicts;
  }
  accept(ids: string[]) {
    if (!this.editor || !this.enabled || !this.run) throw new Error('Forbind til dokumentet, før du godkender.');
    const hunks = this.run.hunks.filter(hunk => ids.includes(hunk.id) && reviewable(hunk) && !this.hasReceipt(hunk));
    if (!hunks.length) return [];
    // Full validation and transaction construction happen before any Yjs mutation.
    const state = this.editor.prosemirrorState;
    const prepared = hunks.map(hunk => this.prepare(hunk, state.doc));
    const transaction = buildAiReviewTransaction(state, prepared);
    const undo = yUndoPluginKey.getState(state)?.undoManager;
    undo?.stopCapturing();
    this.ydoc.transact(() => {
      this.editor!.prosemirrorView.dispatch(transaction);
      const receipts = this.ydoc.getMap('ai-review-receipts');
      for (const hunk of hunks) receipts.set(aiReceiptKey(hunk), { runId: this.run!.id, version: hunk.version, acceptedAt: new Date().toISOString(), order: hunk.order, operation: hunk.operation, targetBlockIds: hunk.targetBlockIds, insertedBlockIds: prepared.find(item => item.id === hunk.id)!.nodes.map(node => node.attrs.id).filter(Boolean) });
    }, ySyncPluginKey);
    undo?.stopCapturing();
    this.refresh();
    return hunks;
  }
  decorations(doc: PmNode) {
    const decorations: Decoration[] = [];
    for (const hunk of this.run?.hunks || []) {
      if (!reviewable(hunk) || this.hasReceipt(hunk)) continue;
      let prepared: Prepared | null = null; let conflict = '';
      try { prepared = this.prepare(hunk, doc); } catch (error) { conflict = error instanceof Error ? error.message : 'Målteksten er ændret.'; }
      if (prepared && !['insert_after', 'insert_before'].includes(hunk.operation)) {
        if (prepared.selection) decorations.push(Decoration.inline(prepared.from, prepared.to, { class: 'native-ai-removed' }));
        else for (const block of findAiBlockNodes(doc, hunk.targetBlockIds)) decorations.push(Decoration.node(block.from, block.to, { class: 'native-ai-removed-block' }));
      }
      const anchor = prepared?.to ?? findAiBlockNodes(doc, hunk.targetBlockIds)[0]?.to ?? Math.max(0, doc.content.size - 1);
      decorations.push(Decoration.widget(Math.min(anchor, doc.content.size), () => this.widget(hunk, conflict), { side: 1, key: `${hunk.id}:${hunk.version}:${conflict}:${this.enabled}`, stopEvent: () => true, ignoreSelection: true }));
    }
    return DecorationSet.create(doc, decorations);
  }
  private widget(hunk: AiHunk, conflict: string) {
    const article = document.createElement('aside'); article.className = 'native-ai-proposal' + (conflict ? ' is-conflict' : ''); article.contentEditable = 'false';
    article.setAttribute('aria-label', 'AI-forslag ' + (hunk.order + 1)); article.dataset.aiHunk = hunk.id;
    const head = document.createElement('div'); head.className = 'native-ai-proposal-head';
    const title = document.createElement('strong'); title.textContent = conflict ? 'Teksten er ændret' : hunk.operation === 'delete' ? 'Foreslår at fjerne' : 'Foreslået ændring'; head.append(title);
    const badge = document.createElement('span'); badge.textContent = 'AI · privat kladde'; head.append(badge); article.append(head);
    if (conflict) { const note = document.createElement('p'); note.className = 'native-ai-conflict'; note.textContent = conflict; article.append(note); }
    if (hunk.explanation) { const note = document.createElement('p'); note.className = 'native-ai-explanation'; note.textContent = hunk.explanation; article.append(note); }
    if (hunk.before.some(block => ['table', 'codeBlock', 'mermaidDiagram'].includes(block.type))) {
      const before = document.createElement('details'); const summary = document.createElement('summary'); summary.textContent = 'Før ændringen'; before.append(summary); const preview = document.createElement('div'); preview.className = 'native-ai-before'; appendAiPreview(preview, hunk.before); before.append(preview); article.append(before);
    }
    if (hunk.after.length) { const after = document.createElement('div'); after.className = 'native-ai-added'; appendAiPreview(after, hunk.after); article.append(after); }
    const actions = document.createElement('div'); actions.className = 'native-ai-review-actions';
    for (const [label, callback, disabled] of [
      ['Godkend', () => this.onAccept?.([hunk.id]), !this.enabled || !!conflict],
      ['Afvis', () => this.onReject?.([hunk.id]), !this.enabled],
      ['Justér', () => this.onAdjust?.(hunk.id), !this.enabled],
    ] as const) {
      const button = document.createElement('button'); button.type = 'button'; button.textContent = label; button.disabled = disabled; button.addEventListener('click', callback); actions.append(button);
    }
    article.append(actions); return article;
  }
}

/** No node views or document attrs: proposals exist solely as private local decorations. */
export function nativeAiReviewExtension(review: NativeAiReview) {
  return createExtension(({ editor }: { editor: Editor }) => {
    review.editor = editor;
    return {
      key: 'nativeAiReview',
      prosemirrorPlugins: [new Plugin({
        key: reviewKey,
        state: { init: (_, state) => review.decorations(state.doc), apply: (tr, previous) => tr.getMeta(reviewKey) ? review.decorations(tr.doc) : tr.docChanged ? previous.map(tr.mapping, tr.doc) : previous },
        props: { decorations: state => reviewKey.getState(state) },
        view: () => ({ update: (view, previous) => { if (view.state.doc !== previous.doc) review.documentChanged(); }, destroy: () => { review.editor = undefined; } }),
      })],
    };
  })();
}

function appendInline(parent: HTMLElement, content: unknown) {
  if (typeof content === 'string') { parent.append(document.createTextNode(content)); return; }
  if (!Array.isArray(content)) return;
  for (const value of content) {
    if (typeof value === 'string') { parent.append(document.createTextNode(value)); continue; }
    if (!value || typeof value !== 'object') continue;
    const item = value as Record<string, any>;
    if (item.type === 'link') {
      const link = document.createElement('a');
      if (typeof item.href === 'string' && /^(https?:\/\/|\/[^/]|#)/.test(item.href)) { link.href = item.href; link.target = '_blank'; link.rel = 'noopener noreferrer'; }
      appendInline(link, item.content); parent.append(link); continue;
    }
    const span = document.createElement('span');
    span.textContent = typeof item.text === 'string' ? item.text : item.type === 'entityMention' ? '↗ ' + (item.props?.label || item.props?.title || 'Reference') : item.type === 'customEmoji' ? ':' + (item.props?.name || 'emoji') + ':' : '[Reference]';
    if (item.styles?.bold) span.style.fontWeight = '700';
    if (item.styles?.italic) span.style.fontStyle = 'italic';
    if (item.styles?.underline) span.style.textDecoration = 'underline';
    if (item.styles?.strike) span.style.textDecoration = 'line-through';
    if (item.styles?.code) span.className = 'native-ai-inline-code';
    parent.append(span);
  }
}

/** Safe, inert rich previews never mount uploaders, drawings, reference searches or collaboration. */
export function appendAiPreview(parent: HTMLElement, blocks: AiBlock[]) {
  for (const block of blocks) {
    const props = block.props || {};
    let element: HTMLElement;
    if (block.type === 'table') {
      element = document.createElement('table');
      const content = block.content as { rows?: { cells?: unknown[] }[] } | undefined;
      for (const row of content?.rows || []) {
        const tr = document.createElement('tr');
        for (const cell of row.cells || []) { const td = document.createElement('td'); appendInline(td, cell && typeof cell === 'object' && 'content' in cell ? (cell as { content: unknown }).content : cell); tr.append(td); }
        element.append(tr);
      }
    } else if (block.type === 'codeBlock' || block.type === 'mermaidDiagram') {
      element = document.createElement('pre');
      const code = document.createElement('code');
      if (block.type === 'mermaidDiagram') code.textContent = typeof props.source === 'string' ? props.source : '';
      else appendInline(code, block.content);
      element.append(code);
      if (props.language) element.setAttribute('data-language', String(props.language));
    } else if (block.type === 'divider' || block.type === 'slideBreak') element = document.createElement('hr');
    else if (['pageMedia', 'drawing', 'image', 'video', 'audio', 'file', 'entityLink', 'pageLink', 'holdGroup', 'holdPoll', 'classRoster'].includes(block.type)) {
      element = document.createElement('p'); element.className = 'native-ai-preserved-content'; element.textContent = '↗ ' + String(props.name || props.title || props.label || ({ drawing: 'Tegning', pageMedia: 'Vedhæftet materiale', entityLink: 'Reference', pageLink: 'Sidelink' } as Record<string, string>)[block.type] || block.type);
    } else {
      const heading = block.type === 'heading' ? Math.max(1, Math.min(6, Number(props.level) || 2)) : 0;
      element = document.createElement(heading ? 'h' + heading : block.type === 'quote' ? 'blockquote' : 'p');
      if (block.type === 'bulletListItem') element.append(document.createTextNode('• '));
      if (block.type === 'numberedListItem') element.append(document.createTextNode('1. '));
      if (block.type === 'checkListItem') element.append(document.createTextNode(props.checked ? '☑ ' : '☐ '));
      if (block.type === 'callout' || block.type === 'learningGoal') element.className = 'native-ai-callout';
      appendInline(element, block.content);
    }
    parent.append(element);
    if (block.children?.length) { const children = document.createElement('div'); children.className = 'native-ai-preview-children'; appendAiPreview(children, block.children); parent.append(children); }
  }
}
