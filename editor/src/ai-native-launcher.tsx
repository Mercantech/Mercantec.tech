import type { Ref } from 'react';
import { createPortal } from 'react-dom';

type NativeAiLauncherProps = {
  slot: HTMLElement | null;
  buttonRef: Ref<HTMLButtonElement>;
  panelId: string;
  visible: boolean;
  enabled: boolean;
  onToggle: () => void;
};

/** Keep the editor's launcher and selection intact when hosted in the page toolbar. */
export function NativeAiLauncher({ slot, buttonRef, panelId, visible, enabled, onToggle }: NativeAiLauncherProps) {
  const button = <button ref={buttonRef} type="button" className="native-ai-toolbar-toggle"
    aria-expanded={visible} aria-controls={panelId}
    aria-label={visible ? 'Skjul AI-panelet' : 'Åbn AI-panelet'} disabled={!enabled}
    onMouseDown={event => event.preventDefault()} onClick={onToggle}>
    <span aria-hidden="true">✦</span> AI <span className="native-ai-toggle-detail">{visible ? 'Skjul' : 'Skrivepartner'}</span>
  </button>;

  return slot ? createPortal(button, slot) : <div className="native-ai-taskbar">{button}</div>;
}
