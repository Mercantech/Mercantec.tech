import React, { type ReactNode } from 'react';
import { AiDragHandleItem } from './ai-helper';
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
  useExtensionState,
  usePortalElement,
  type IconType,
} from '@blocknote/react';

const icon = (label: string): IconType =>
  function TurnIntoIcon({ size = 16 }) {
    return <span style={{ display: 'inline-flex', width: size, height: size, alignItems: 'center', justifyContent: 'center', fontSize: Math.max(11, Number(size) - 2), lineHeight: 1 }} aria-hidden="true">{label}</span>;
  };

/** Turn-into targets: paragraph, H1–H3, lists, quote, plus kode/callout/læringsmål. */
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
  editor.focus();
  editor.transact(() => {
    for (const target of targets) {
      editor.updateBlock(target, { type: type as never, props: props as never });
    }
  });
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
          const selected = item.type === block.type
            && Object.entries(item.props || {}).every(([key, value]) => (block.props as Record<string, unknown>)[key] === value);
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

/** Drag-handle menu: Turn into, AI helper on this block, delete, colors. */
export function NotionDragHandleMenu() {
  return (
    <DragHandleMenu>
      <TurnIntoItem>Omdan til</TurnIntoItem>
      <AiDragHandleItem />
      <RemoveBlockItem>Slet</RemoveBlockItem>
      <BlockColorsItem>Farver</BlockColorsItem>
      <TableRowHeaderItem>Overskriftsrække</TableRowHeaderItem>
      <TableColumnHeaderItem>Overskriftskolonne</TableColumnHeaderItem>
    </DragHandleMenu>
  );
}
