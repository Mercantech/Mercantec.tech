import React, { useContext, useId, useRef, useState } from 'react';
import { createReactBlockSpec } from '@blocknote/react';
import { Popover } from '@mantine/core';
import { EmojiPicker, type PlatformEmoji } from './emoji-picker';
import { CustomEmojiImage, EmojiAnalyze, EmojiCatalog, EmojiCreate, EmojiReload } from './emoji-context';

const TONES = ['info', 'tip', 'warning', 'important'] as const;
export type CalloutTone = (typeof TONES)[number];

const TONE_LABEL: Record<CalloutTone, string> = {
  info: 'Info',
  tip: 'Tip',
  warning: 'Advarsel',
  important: 'Vigtigt',
};

const TONE_MARK: Record<CalloutTone, string> = {
  info: 'ℹ',
  tip: '💡',
  warning: '⚠',
  important: '★',
};

export const createCallout = createReactBlockSpec({
  type: 'callout',
  propSchema: {
    schemaVersion: { default: 1 },
    tone: { default: 'info' as CalloutTone, values: [...TONES] },
    emojiId: { default: '' },
    emojiName: { default: '' },
  },
  content: 'inline',
}, {
  render: function Callout({ block, editor, contentRef }) {
    const emojis = useContext(EmojiCatalog);
    const reload = useContext(EmojiReload);
    const createEmoji = useContext(EmojiCreate);
    const analyzeEmoji = useContext(EmojiAnalyze);
    const [opened, setOpened] = useState(false);
    const trigger = useRef<HTMLButtonElement>(null);
    const pickerId = useId();
    const tone = (TONES.includes(block.props.tone as CalloutTone) ? block.props.tone : 'info') as CalloutTone;
    const editable = editor.isEditable;
    const emoji = emojis.find(item => item.id === block.props.emojiId);
    const label = `:${emoji?.name || block.props.emojiName || 'emoji'}:`;
    const mark = emoji ? <CustomEmojiImage emoji={emoji} label={label} /> : <span aria-hidden="true">{TONE_MARK[tone]}</span>;
    const close = () => { setOpened(false); trigger.current?.focus({ preventScroll: true }); };
    const update = (props: { tone?: CalloutTone; emojiId?: string; emojiName?: string }) => {
      const current = editor.getBlock(block.id);
      if (!editor.isEditable || !current || current.type !== 'callout') { setOpened(false); return; }
      // Attribute-only updates keep the inline content, selection and undo history intact.
      editor.updateBlock(current, { props });
      setOpened(false);
      editor.focus();
    };
    const pick = (item: PlatformEmoji) => update({ emojiId: item.id, emojiName: item.name });
    const portalTarget = editor.domElement?.closest<HTMLElement>('.notion-blocknote-host') || undefined;
    return <section className={`notion-callout notion-callout-${tone}`} data-tone={tone} aria-label={`Callout · ${TONE_LABEL[tone]}`}>
      {editable
        ? <Popover opened={opened} onChange={setOpened} position="bottom-start" floatingStrategy="fixed" offset={6} width="min(340px, calc(100vw - 16px))" zIndex={100} withinPortal portalProps={{ target: portalTarget }} middlewares={{ flip: { padding: 8 }, shift: { padding: 8 }, size: { padding: 8 } }} closeOnEscape={false} returnFocus={false} transitionProps={{ duration: 0 }}>
          <Popover.Target><button
            ref={trigger}
            type="button"
            className="notion-callout-mark"
            contentEditable={false}
            aria-label="Skift callout-ikon"
            title={block.props.emojiId ? `Skift ikon · ${label}` : `Skift ikon · ${TONE_LABEL[tone]}`}
            onMouseDown={event => event.preventDefault()}
            onClick={() => { if (!opened) reload(); setOpened(value => !value); }}
          >{mark}</button></Popover.Target>
          <Popover.Dropdown className="notion-callout-icon-picker" contentEditable={false} aria-label="Callout-ikon og type" style={{ padding: 0 }} onKeyDown={event => { event.stopPropagation(); if (event.key === 'Escape') { event.preventDefault(); close(); } }}>
            <div className="notion-callout-tone-options" role="group" aria-label="Callout-type">
              {TONES.map(value => <button key={value} type="button" aria-pressed={tone === value} onClick={() => update({ tone: value })}>{TONE_MARK[value]} {TONE_LABEL[value]}</button>)}
              <button type="button" className="notion-callout-icon-reset" disabled={!block.props.emojiId} onClick={() => update({ emojiId: '', emojiName: '' })}>Standardikon</button>
            </div>
            <EmojiPicker id={pickerId} emojis={emojis} onPick={pick} onClose={close} onUpload={createEmoji} onAnalyze={analyzeEmoji} />
          </Popover.Dropdown>
        </Popover>
        : <span className="notion-callout-mark" contentEditable={false} title={block.props.emojiId ? label : undefined}>{mark}</span>}
      <div className="notion-callout-body">
        <div ref={contentRef} />
      </div>
    </section>;
  },
  toExternalHTML: ({ block, contentRef }) => {
    const tone = (TONES.includes(block.props.tone as CalloutTone) ? block.props.tone : 'info') as CalloutTone;
    const mark = block.props.emojiId && block.props.emojiName ? `:${block.props.emojiName}:` : TONE_MARK[tone];
    return <aside data-tone={tone} aria-label={`Callout · ${TONE_LABEL[tone]}`}><span>{mark}</span><div ref={contentRef} /></aside>;
  },
});
