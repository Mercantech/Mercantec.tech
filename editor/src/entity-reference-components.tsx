import React, { createContext, useContext, useEffect, useId, useMemo, useState } from 'react';
import { createReferenceClient, createReferenceResolver, createReferenceSearch, entityReference, referenceKindLabel } from './entity-references.mjs';

export interface EntityReference { kind: string; id: string }
export interface ResolvedReference { reference: EntityReference; title: string; href: string; contextLabel: string; mentionStatus?: string | null }
export interface ReferenceEnvironment { sourceKind: string; sourceId: string; holdId?: string | null; publishedOnly?: boolean; apiBase?: string; getToken: () => Promise<string>; revision?: string | number; canEdit: boolean }
type Client = ReturnType<typeof createReferenceClient>;
const References = createContext<{ client: Client; resolver: ReturnType<typeof createReferenceResolver>; canEdit: boolean } | null>(null);
export function ReferenceProvider({ environment, children }: { environment: ReferenceEnvironment; children: React.ReactNode }) {
  const value = useMemo(() => {
    const client = createReferenceClient({...environment,holdId:environment.holdId});
    return { client, resolver: createReferenceResolver(client), canEdit: environment.canEdit };
  }, [environment.sourceKind, environment.sourceId, environment.holdId, environment.publishedOnly, environment.apiBase, environment.revision, environment.canEdit]);
  useEffect(() => () => value.resolver.destroy(), [value]);
  return <References.Provider value={value}>{children}</References.Provider>;
}
export function useReferenceSearch() {
  const context = useContext(References);
  const search = useMemo(() => context ? createReferenceSearch(context.client) : null, [context]);
  useEffect(() => () => search?.destroy(), [search]);
  return search;
}
export function mentionStatusLabel(status?: string | null) {
  if (status === 'no-account') return 'Ingen LUP-konto · får ingen besked';
  if (status === 'no-access') return 'Har ikke læseadgang · besked afventer adgang';
  if (status === 'on-publish') return 'Får besked ved udgivelse';
  if (status === 'will-notify') return 'Besked vises, når siden er læsbar';
  if (status === 'self') return 'Du nævner dig selv';
  return status && !['available', 'can-read', 'ready'].includes(status) ? status : '';
}
export function ReferenceView({ reference, block = false }: { reference: EntityReference | null; block?: boolean }) {
  const context = useContext(References);
  const [state, setState] = useState<{ status: string; value: ResolvedReference | null; scope?: typeof context }>({ status: 'loading', value: null });
  useEffect(() => {
    setState({ status: 'loading', value: null });
    return reference && context ? context.resolver.subscribe(reference, (next: {status:string;value:ResolvedReference|null}) => setState({...next,scope:context})) : undefined;
  }, [context, reference?.kind, reference?.id]);
  const value = state.scope === context && state.value?.reference.kind === reference?.kind && state.value?.reference.id === reference?.id ? state.value : null;
  const status = context?.canEdit ? mentionStatusLabel(value?.mentionStatus) : '';
  if (!reference || !value) return <span className="notion-unavailable" contentEditable={false}>{reference && state.status === 'loading' ? 'Henter reference…' : state.status === 'error' ? <>Referencen kunne ikke hentes <button type="button" onClick={()=>{if(reference)context?.resolver.retry(reference);}}>Prøv igen</button></> : 'Indhold er ikke tilgængeligt'}</span>;
  const person = reference.kind === 'person' || reference.kind === 'member';
  return <a className={block ? 'notion-entity-link' : 'notion-mention'} href={value.href} title={[referenceKindLabel(reference.kind), value.contextLabel, status].filter(Boolean).join(' · ')} contentEditable={false}>
    <span aria-hidden="true">{person ? '@' : '↗'} </span>{value.title}{block && <small>{referenceKindLabel(reference.kind)} · {value.contextLabel}</small>}
    {person && status && <span className="notion-reference-status">{status}</span>}
  </a>;
}
export function ReferencePicker({ onPick }: { onPick: (reference: EntityReference) => void }) {
  const search = useReferenceSearch();
  const [query, setQuery] = useState(''), [items, setItems] = useState<ResolvedReference[]>([]);
  const [loading, setLoading] = useState(true), [error, setError] = useState('');
  const [selected, setSelected] = useState(0);
  const label = useId();
  useEffect(() => {
    let live = true; setLoading(true); setError('');
    search?.search(query).then((result: { items: ResolvedReference[] }) => { if (live) { setItems(result.items); setSelected(0); } })
      .catch(() => { if (live) setError('Søgningen kunne ikke hentes. Prøv igen.'); })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [search, query]);
  return <div className="notion-reference-picker" onKeyDown={event => {
    event.stopPropagation();
    if (event.key === 'ArrowDown') { event.preventDefault(); setSelected(value => Math.min(items.length - 1, value + 1)); }
    if (event.key === 'ArrowUp') { event.preventDefault(); setSelected(value => Math.max(0, value - 1)); }
    if (event.key === 'Enter' && items[selected]) { event.preventDefault(); onPick(items[selected].reference); }
  }}>
    <label htmlFor={label}>Søg i LUP</label><input id={label} type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Side, person, opgave, materiale…" autoComplete="off" maxLength={200} />
    {loading && <p role="status">Søger…</p>}{error && <p role="alert">{error}</p>}
    {!loading && !error && !items.length && <p>Ingen tilgængelige resultater.</p>}
    <ul aria-label="Tilgængeligt indhold">{items.map((item, index) => <li key={item.reference.kind + ':' + item.reference.id}><button type="button" className={index === selected ? 'is-selected' : undefined} onClick={() => { const ref = entityReference(item.reference.kind, item.reference.id); if (ref) onPick(ref); }}><strong>{item.title}</strong><small>{referenceKindLabel(item.reference.kind)} · {item.contextLabel}</small>{mentionStatusLabel(item.mentionStatus) && <small>{mentionStatusLabel(item.mentionStatus)}</small>}</button></li>)}</ul>
  </div>;
}
