import React, { useEffect, useMemo, useRef, useState } from 'react';
import { emojiCategories, filterCustomEmojis } from './custom-emoji.mjs';
import { normalizeEmojiName, suggestEmojiName, validateEmojiFile } from './emoji-upload.mjs';

export type PlatformEmoji = {
  id: string;
  name: string;
  contentUrl: string;
  isStandard?: boolean;
  category?: string;
  keywords?: string[];
};

export function EmojiPicker({ id, emojis, onPick, onClose, onUpload }: {
  id: string;
  emojis: PlatformEmoji[];
  onPick: (emoji: PlatformEmoji) => void;
  onClose: () => void;
  onUpload?: (file: File, name: string, signal: AbortSignal) => Promise<PlatformEmoji>;
}) {
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('all');
  const [preview, setPreview] = useState<PlatformEmoji | null>(null);
  const search = useRef<HTMLInputElement>(null);
  const grid = useRef<HTMLDivElement>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const nameInput = useRef<HTMLInputElement>(null);
  const upload = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  const [file, setFile] = useState<File | null>(null);
  const [name, setName] = useState('');
  const [filePreview, setFilePreview] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const results = useMemo(() => filterCustomEmojis(emojis, query, category) as PlatformEmoji[], [emojis, query, category]);
  const activePreview = preview && results.some(item => item.id === preview.id) ? preview : null;

  useEffect(() => { search.current?.focus({ preventScroll: true }); }, []);
  useEffect(() => { grid.current?.scrollTo({ top: 0 }); }, [query, category]);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; upload.current?.abort(); }; }, []);
  useEffect(() => {
    if (!file) { setFilePreview(''); search.current?.focus({ preventScroll: true }); return; }
    const url = URL.createObjectURL(file);
    setFilePreview(url);
    nameInput.current?.focus({ preventScroll: true });
    nameInput.current?.select();
    return () => URL.revokeObjectURL(url);
  }, [file]);

  const cancelUpload = () => {
    if (upload.current) return;
    setFile(null); setError('');
    search.current?.focus({ preventScroll: true });
  };
  const chooseFile = (event: React.ChangeEvent<HTMLInputElement>) => {
    const selected = event.target.files?.[0];
    event.target.value = '';
    if (!selected || upload.current) return;
    const invalid = validateEmojiFile(selected);
    if (invalid) { setError(invalid); return; }
    setError('');
    setName(suggestEmojiName(selected.name, emojis.map(item => item.name)));
    setFile(selected);
  };
  const saveUpload = async () => {
    if (!file || !onUpload || upload.current) return;
    const normalized = normalizeEmojiName(name);
    if (!normalized) { setError('Navnet skal være 2–32 tegn: små bogstaver, tal og _.'); nameInput.current?.focus(); return; }
    const controller = new AbortController();
    upload.current = controller;
    setBusy(true); setError('');
    try {
      const created = await onUpload(file, normalized, controller.signal);
      if (!mounted.current || controller.signal.aborted) return;
      setFile(null); setQuery(created.name); setCategory('custom');
      onPick(created);
    } catch (error) {
      if (mounted.current && !controller.signal.aborted)
        setError(error instanceof Error ? error.message : 'Emojien kunne ikke gemmes. Prøv igen.');
    } finally {
      if (upload.current === controller) upload.current = null;
      if (mounted.current) setBusy(false);
    }
  };

  const onKeyDown = (event: React.KeyboardEvent) => {
    // Keep search and grid shortcuts out of BlockNote's document shortcuts.
    event.stopPropagation();
    if (event.nativeEvent.isComposing) return;
    if (event.key === 'Escape') {
      event.preventDefault();
      if (busy) return;
      if (file) cancelUpload();
      else onClose();
    } else if (event.key === 'Enter' && file && event.target instanceof HTMLInputElement) {
      // The picker can be hosted inside another form; keep Enter local.
      event.preventDefault();
      void saveUpload();
    }
  };
  const moveInGrid = (event: React.KeyboardEvent<HTMLButtonElement>, index: number) => {
    const buttons = grid.current?.querySelectorAll<HTMLButtonElement>('button');
    if (!buttons?.length) return;
    const columns = getComputedStyle(grid.current!).gridTemplateColumns.split(' ').length;
    let next: number;
    switch (event.key) {
      case 'ArrowRight': next = Math.min(buttons.length - 1, index + 1); break;
      case 'ArrowLeft': next = Math.max(0, index - 1); break;
      case 'ArrowDown': next = Math.min(buttons.length - 1, index + columns); break;
      case 'ArrowUp': next = index - columns; break;
      case 'Home': next = 0; break;
      case 'End': next = buttons.length - 1; break;
      default: return;
    }
    event.preventDefault();
    if (next < 0) search.current?.focus();
    else buttons[next].focus({ preventScroll: true });
    if (next >= 0) buttons[next].scrollIntoView({ block: 'nearest', inline: 'nearest' });
  };
  const reset = () => { setQuery(''); setCategory('all'); search.current?.focus(); };

  return <section id={id} className="notion-emoji-panel" aria-label="Platformens emojis" onKeyDown={onKeyDown}>
    <div className="notion-emoji-search-row">
      <label className="notion-emoji-search">
        <svg className="notion-emoji-search-icon" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 4.5 4.5"/></svg>
        <input ref={search} type="search" value={query} onChange={event => setQuery(event.target.value)} aria-label="Søg efter emoji" placeholder="Søg navn eller fx glad, raket…" autoComplete="off" maxLength={100} disabled={!!file} onKeyDown={event => {
          if (event.key === 'ArrowDown') {
            const first = grid.current?.querySelector<HTMLButtonElement>('button');
            if (first) { event.preventDefault(); first.focus({ preventScroll: true }); }
          }
        }} />
      </label>
      {onUpload && <button type="button" className="notion-emoji-add" disabled={busy} onClick={() => fileInput.current?.click()} aria-label="Tilføj emoji" title="Tilføj emoji">+</button>}
      <button type="button" className="notion-emoji-close" disabled={busy} onClick={onClose} aria-label="Luk emoji-vælger" title="Luk (Esc)">×</button>
    </div>
    {onUpload && <input ref={fileInput} className="notion-emoji-file" type="file" accept="image/png,image/jpeg,image/gif,image/webp" aria-label="Vælg emoji-billede" onChange={chooseFile} disabled={busy} tabIndex={-1} />}
    {file ? <div className="notion-emoji-upload" aria-label="Tilføj emoji" aria-busy={busy}>
      <div className="notion-emoji-upload-file">{filePreview && <img src={filePreview} alt="Forhåndsvisning" />}<span>{file.name}</span></div>
      <label className="notion-emoji-upload-name">Emoji-navn<input ref={nameInput} value={name} onChange={event => { setName(event.target.value); setError(''); }} maxLength={32} autoComplete="off" spellCheck={false} disabled={busy} aria-invalid={!normalizeEmojiName(name)} aria-describedby={`${id}-upload-hint`} /></label>
      <p id={`${id}-upload-hint`}>Foreslået fra filnavnet. Du kan ændre navnet før du gemmer. Brug 2–32 tegn: a-z, 0-9 og _.</p>
      <p className="notion-emoji-upload-shortcode">:{normalizeEmojiName(name) ?? name}:</p>
      {error && <p className="notion-emoji-upload-error" role="alert">{error}</p>}
      <div className="notion-emoji-upload-actions"><button type="button" className="notion-emoji-upload-save" disabled={busy || !onUpload || !normalizeEmojiName(name)} onClick={() => void saveUpload()}>{busy ? 'Gemmer…' : 'Gem og brug'}</button><button type="button" disabled={busy} onClick={cancelUpload}>Annuller</button></div>
      {busy && <p role="status">Gemmer emoji…</p>}
    </div> : <>
    {error && <p className="notion-emoji-upload-error" role="alert">{error}</p>}
    <div className="notion-emoji-categories" role="group" aria-label="Emojikategorier">
      {emojiCategories.map(item => <button key={item.id} type="button" aria-pressed={category === item.id} onClick={() => setCategory(item.id)}>{item.label}</button>)}
    </div>
    <div className="notion-emoji-results" role="status" aria-live="polite">{results.length} {results.length === 1 ? 'emoji' : 'emojis'}</div>
    <div ref={grid} className="notion-emoji-grid" role="group" aria-label="Emojis" onMouseLeave={() => setPreview(null)}>
      {results.map((item, index) => <button key={item.id} type="button" className="notion-emoji-option" title={`:${item.name}:`} aria-label={`:${item.name}:`} onClick={() => onPick(item)} onFocus={() => setPreview(item)} onMouseEnter={() => setPreview(item)} onKeyDown={event => moveInGrid(event, index)}><img src={item.contentUrl} alt="" draggable={false} loading="lazy" /></button>)}
      {results.length === 0 && <div className="notion-emoji-empty">
        <p>{emojis.length === 0 ? 'Ingen emojis endnu.' : category === 'custom' && !query ? 'Ingen egne emojis endnu.' : 'Ingen emojis matcher dine filtre.'}</p>
        {query || category !== 'all' ? <button type="button" onClick={reset}>Nulstil filtre</button> : <a href="/emojis">Åbn biblioteket</a>}
      </div>}
    </div>
    <div className="notion-emoji-preview" aria-hidden="true">
      {activePreview ? <><img src={activePreview.contentUrl} alt="" /><span>:{activePreview.name}:</span><small>{activePreview.isStandard ? 'Standard' : 'Egen emoji'}</small></> : <span>Vælg en emoji · Esc lukker</span>}
    </div>
    </>}
  </section>;
}
