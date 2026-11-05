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
    return <section className={`notion-callout notion-callout-${tone}`} data-tone={tone} aria-label={`Callout · ${TONE_LABEL[tone]}`}>
      <span className="notion-callout-mark" contentEditable={false} aria-hidden="true">{TONE_MARK[tone]}</span>
      <div className="notion-callout-body">
        {editable && <label className="notion-callout-tone" contentEditable={false}>
          <span className="notion-visually-hidden">Callout-type</span>
          <select value={tone} onChange={(event) => editor.updateBlock(block, { props: { tone: event.target.value as CalloutTone } })}>
            {TONES.map((value) => <option key={value} value={value}>{TONE_LABEL[value]}</option>)}
          </select>
        </label>}
        {!editable && <strong className="notion-callout-label" contentEditable={false}>{TONE_LABEL[tone]}</strong>}
        <div ref={contentRef} />
      </div>
    </section>;
  },
  toExternalHTML: ({ contentRef }) => <aside><div ref={contentRef} /></aside>,
});
