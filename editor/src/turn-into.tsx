import React, { useMemo, type ReactNode } from 'react';
import { editorHasBlockWithType } from '@blocknote/core';
import { SideMenuExtension } from '@blocknote/core/extensions';
import {
  BlockColorsItem,
  blockTypeSelectItems,
  type BlockTypeSelectItem,
  DragHandleMenu,
  RemoveBlockItem,
  TableColumnHeaderItem,
  TableRowHeaderItem,
  useBlockNoteEditor,
  useComponentsContext,
  useEditorState,
  useExtensionState,
  usePortalElement,
  useUIMode,
  type IconType,
} from '@blocknote/react';
import { notionCodeBlockOptions } from './code-block';
import { buildTurnIntoUpdate, type TurnIntoBlock, type TurnIntoOptions } from './turn-into-content';

const icon = (label: string): IconType =>
  function TurnIntoIcon({ size = 16 }) {
    return <span style={{ display: 'inline-flex', width: size, height: size, alignItems: 'center', justifyContent: 'center', fontSize: Math.max(11, Number(size) - 2), lineHeight: 1 }} aria-hidden="true">{label}</span>;
  };

const turnIntoOptions: TurnIntoOptions = {
  supportedLanguages: notionCodeBlockOptions.supportedLanguages,
  defaultCodeLanguage: notionCodeBlockOptions.defaultLanguage,
};

/** Turn-into targets: paragraph, H1–H3, lists, quote, plus kode/diagram/callout/læringsmål. */
export function turnIntoSelectItems(editor: Parameters<typeof blockTypeSelectItems>[0]): BlockTypeSelectItem[] {
  const defaults = blockTypeSelectItems(editor).filter((item) => {
    if (item.props?.isToggleable === true) return false;
    if (item.type === 'heading') {
      const level = typeof item.props?.level === 'number' ? item.props.level : 1;
      return level <= 3;
    }
    return true;
  });
  return [
    ...defaults,
    { name: 'Kodeblok', type: 'codeBlock', props: { language: 'csharp' }, icon: icon('</>') },
    { name: 'Diagram', type: 'mermaidDiagram', icon: icon('◈') },
    { name: 'Callout', type: 'callout', props: { tone: 'info' }, icon: icon('▣') },
    { name: 'Læringsmål', type: 'learningGoal', icon: icon('◎') },
  ];
}

function applyTurnInto(
  editor: ReturnType<typeof useBlockNoteEditor>,
  block: { id: string },
  type: string,
  props?: Record<string, boolean | number | string>,
) {
  const selected = editor.getSelection()?.blocks;
  const targets = selected && selected.some((item) => item.id === block.id) ? selected : [block];
  const lives = targets.map((target) => (editor.getBlock(target.id) ?? target) as TurnIntoBlock);
  editor.focus();
  editor.transact(() => {
    for (const live of lives) {
      if (!live.id) continue;
      editor.updateBlock(live.id, buildTurnIntoUpdate(live, type, props, turnIntoOptions) as never);
    }
  });
}

function sameTurnIntoItem(item: BlockTypeSelectItem, block: { type: string; props: Record<string, unknown> }) {
  if (item.type !== block.type) return false;
  if (item.type === 'codeBlock' || item.type === 'callout' || item.type === 'mermaidDiagram' || item.type === 'learningGoal') return true;
  return Object.entries(item.props || {}).every(([key, value]) => block.props[key] === value);
}

/** Formatting-toolbar block type menu. Uses the same content-carrying conversion as Omdan til. */
export function NotionBlockTypeSelect() {
  const Components = useComponentsContext()!;
  const uiMode = useUIMode();
  const portalElement = usePortalElement();
  const editor = useBlockNoteEditor<any, any, any>();
  const selectedBlocks = useEditorState({
    editor,
    selector: ({ editor }) => editor.getSelection()?.blocks || [editor.getTextCursorPosition().block],
  });
  const first = selectedBlocks[0];
  const items = useMemo(
    () => turnIntoSelectItems(editor).filter((item) => editorHasBlockWithType(editor, item.type, item.props
      ? Object.fromEntries(Object.entries(item.props).map(([key, value]) => [key, typeof value])) as Record<string, 'string' | 'number' | 'boolean'>
      : undefined)),
    [editor],
  );
  if (!first || !items.some((item) => item.type === first.type) || !editor.isEditable) return null;

  return (
    <Components.FormattingToolbar.Select
      className="bn-select"
      items={items.map((item) => {
        const Icon = item.icon;
        return {
          text: item.name,
          icon: <Icon size={16} />,
          isSelected: sameTurnIntoItem(item, first),
          onClick: () => applyTurnInto(editor, first, item.type, item.props),
        };
      })}
      portalElement={portalElement}
      preventFocusOnOpen={uiMode === 'mobile'}
    />
  );
}

function TurnIntoItem({ children }: { children: ReactNode }) {
  const Components = useComponentsContext()!;
  const portalElement = usePortalElement();
  const editor = useBlockNoteEditor<any, any, any>();
  const block = useExtensionState(SideMenuExtension, {
    editor,
    selector: (state) => state?.block,
  });
  if (!block) return null;

  const items = turnIntoSelectItems(editor);

  return (
    <Components.Generic.Menu.Root position="right" sub={true} portalElement={portalElement}>
      <Components.Generic.Menu.Trigger sub={true}>
        <Components.Generic.Menu.Item className="bn-menu-item" subTrigger={true}>{children}</Components.Generic.Menu.Item>
      </Components.Generic.Menu.Trigger>
      <Components.Generic.Menu.Dropdown sub={true} className="bn-menu-dropdown bn-turn-into-menu">
        {items.map((item) => {
          const Icon = item.icon;
          const selected = sameTurnIntoItem(item, block as { type: string; props: Record<string, unknown> });
          return (
            <Components.Generic.Menu.Item
              key={`${item.type}-${JSON.stringify(item.props || {})}`}
              className="bn-menu-item"
              icon={<Icon size={16} />}
              checked={selected}
              onClick={() => applyTurnInto(editor, block, item.type, item.props)}
            >
              {item.name}
            </Components.Generic.Menu.Item>
          );
        })}
      </Components.Generic.Menu.Dropdown>
    </Components.Generic.Menu.Root>
  );
}

/** Drag-handle menu with Notion-style Turn into + delete + colors. */
export function NotionDragHandleMenu() {
  return (
    <DragHandleMenu>
      <TurnIntoItem>Omdan til</TurnIntoItem>
      <RemoveBlockItem>Slet</RemoveBlockItem>
      <BlockColorsItem>Farver</BlockColorsItem>
      <TableRowHeaderItem>Overskriftsrække</TableRowHeaderItem>
      <TableColumnHeaderItem>Overskriftskolonne</TableColumnHeaderItem>
    </DragHandleMenu>
  );
}
