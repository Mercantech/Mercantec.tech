import React from 'react';
import { createReactBlockSpec } from '@blocknote/react';

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

function nextTone(tone: CalloutTone): CalloutTone {
  return TONES[(TONES.indexOf(tone) + 1) % TONES.length];
}

export const createCallout = createReactBlockSpec({
  type: 'callout',
  propSchema: {
    schemaVersion: { default: 1 },
    tone: { default: 'info' as CalloutTone, values: [...TONES] },
  },
  content: 'inline',
}, {
  render: ({ block, editor, contentRef }) => {
    const tone = (TONES.includes(block.props.tone as CalloutTone) ? block.props.tone : 'info') as CalloutTone;
    const editable = editor.isEditable;
    const cycleTone = () => {
      if (!editable) return;
      editor.updateBlock(block, { props: { tone: nextTone(tone) } });
    };
    return <section className={`notion-callout notion-callout-${tone}`} data-tone={tone} aria-label={`Callout · ${TONE_LABEL[tone]}`}>
      {editable
        ? <button
            type="button"
            className="notion-callout-mark"
            contentEditable={false}
            aria-label={`Skift callout-type (nu ${TONE_LABEL[tone]})`}
            title={`Skift type · ${TONE_LABEL[tone]}`}
            onClick={cycleTone}
          >{TONE_MARK[tone]}</button>
        : <span className="notion-callout-mark" contentEditable={false} aria-hidden="true">{TONE_MARK[tone]}</span>}
      <div className="notion-callout-body">
        <div ref={contentRef} />
      </div>
    </section>;
  },
  toExternalHTML: ({ contentRef }) => <aside><div ref={contentRef} /></aside>,
});
