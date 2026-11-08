/** References contain identity only. Reader-specific labels and destinations never enter Yjs. */
export function entityReference(kind, id) {
  if (typeof kind !== 'string' || !/^[a-z][a-z0-9-]{0,47}$/.test(kind)
      || typeof id !== 'string' || !id || id.length > 240 || /[\u0000-\u001f\u007f]/.test(id)) return null;
  return { kind, id: /^[0-9a-f-]{36}$/i.test(id) ? id.toLowerCase() : id };
}
export const referenceKey = reference => `${reference.kind}:${reference.id}`;
export function referenceKindLabel(kind) {
  return ({ 'notion-page': 'Side', 'notebook-page': 'Note / logbog', collection: 'Notesbog / samling', notebook: 'Personlig notesbog',
    hold: 'Hold', 'teaching-course': 'Undervisningsforløb', quest: 'Opgave', material: 'Materiale', poll: 'Afstemning',
    person: 'Person', member: 'Elev på hold', group: 'Gruppe', event: 'Kalenderbegivenhed', submission: 'Aflevering',
    file: 'Fil', 'notion-asset': 'Sidevedhæftning', 'notebook-asset': 'Notevedhæftning', drawing: 'Tegning',
    'learning-plan': 'Undervisningsplan', subject: 'Fag', lesson: 'Emne', project: 'Projekt', badge: 'Badge', character: 'Karakter' })[kind] || 'Indhold';
}
export function legacyReference(type, props, domain = 'notion') {
  if (type === 'pageMention' || type === 'pageLink') return entityReference(domain === 'notebook' ? 'notebook-page' : 'notion-page', props.pageId);
  if (type === 'studentMention') return entityReference('member', props.membershipId);
  return entityReference(props.kind, props.id);
}

export function createReferenceClient({ getToken, sourceKind, sourceId, holdId, publishedOnly = false, apiBase = '/api', fetchImpl = fetch }) {
  const base = apiBase.replace(/\/$/, '');
  async function request(path, options) {
    const token = await getToken();
    if (options.signal?.aborted) throw new DOMException('Søgningen er afbrudt.', 'AbortError');
    const response = await fetchImpl(base + '/references/' + path, {
      ...options, headers: { 'Content-Type': 'application/json', Authorization: token.startsWith('Bearer ') ? token : 'Bearer ' + token }, cache: 'no-store',
    });
    if (!response.ok) throw new Error('Indholdet kunne ikke hentes. Prøv igen.');
    return response.json();
  }
  return {
    async search(query, signal, offset = 0) {
      const params = new URLSearchParams({ q: query, sourceKind, sourceId, limit: '30', offset: String(offset), publishedOnly: String(publishedOnly) });
      if (holdId) params.set('holdId', holdId);
      return request('search?' + params, { signal });
    },
    async resolve(references, signal) {
      const wanted = new Set(references.map(referenceKey));
      const values = await request('resolve', { method: 'POST', signal, body: JSON.stringify({ references, sourceKind, sourceId, publishedOnly }) });
      // A response cannot inject an unrelated reference or an external/script navigation target.
      return Array.isArray(values) ? values.filter(value => {
        const reference = entityReference(value?.reference?.kind, value?.reference?.id);
        return reference && wanted.has(referenceKey(reference)) && typeof value.title === 'string'
          && typeof value.href === 'string' && /^\/(?!\/)/.test(value.href) && !/[\u0000-\u0020\\]/.test(value.href);
      }) : [];
    },
  };
}

/** The previous search always settles, even if it is superseded during the debounce. */
export function createReferenceSearch(client, delay = 180) {
  let current;
  return {
    search(query) {
      current?.cancel();
      return new Promise((resolve, reject) => {
        const controller = new AbortController();
        const operation = {
          cancel() { clearTimeout(timer); controller.abort(); resolve({ items: [], total: 0 }); },
        };
        const timer = setTimeout(async () => {
          try { const result = await client.search(query, controller.signal); if (current === operation) resolve(result); }
          catch (error) { if (current === operation && !controller.signal.aborted) reject(error); }
        }, delay);
        current = operation;
      });
    },
    destroy() { current?.cancel(); current = undefined; },
  };
}

/** Visible references are requested together, bounded to 100 identities per server call. */
export function createReferenceResolver(client, refreshMs = 45000) {
  const entries = new Map(), pending = new Map(), controllers = new Set();
  let scheduled = false, destroyed = false;
  function enqueue(reference, entry) {
    entry.generation++;
    pending.set(referenceKey(reference), reference);
    if (!scheduled) { scheduled = true; queueMicrotask(flush); }
  }
  const refresh = setInterval(() => {
    for (const entry of entries.values()) if (entry.listeners.size) {
      entry.state = { status: 'loading', value: null };
      entry.listeners.forEach(listener => listener(entry.state));
      enqueue(entry.reference, entry);
    }
  }, refreshMs);
  refresh.unref?.();
  async function flush() {
    scheduled = false;
    if (destroyed || !pending.size) return;
    const batch = [...pending.values()].slice(0, 100);
    const generations = new Map(batch.map(ref => [referenceKey(ref), entries.get(referenceKey(ref)).generation]));
    for (const ref of batch) pending.delete(referenceKey(ref));
    const controller = new AbortController(); controllers.add(controller);
    try {
      const values = await client.resolve(batch, controller.signal);
      if (destroyed) return;
      const found = new Map(values.map(value => [referenceKey(value.reference), value]));
      for (const ref of batch) {
        const entry = entries.get(referenceKey(ref));
        if (entry.generation !== generations.get(referenceKey(ref))) continue;
        entry.state = { status: 'ready', value: found.get(referenceKey(ref)) ?? null };
        entry.listeners.forEach(listener => listener(entry.state));
      }
    } catch {
      if (!destroyed) for (const ref of batch) {
        const entry = entries.get(referenceKey(ref));
        if (entry.generation !== generations.get(referenceKey(ref))) continue;
        entry.state = { status: 'error', value: null };
        entry.listeners.forEach(listener => listener(entry.state));
      }
    } finally {
      controllers.delete(controller);
      if (!destroyed && pending.size && !scheduled) { scheduled = true; queueMicrotask(flush); }
    }
  }
  return {
    subscribe(reference, listener) {
      const key = referenceKey(reference);
      let entry = entries.get(key);
      if (!entry) {
        entry = { reference, generation: 0, state: { status: 'loading', value: null }, listeners: new Set() }; entries.set(key, entry);
        enqueue(reference, entry);
      }
      entry.listeners.add(listener); listener(entry.state);
      return () => { entry.listeners.delete(listener); };
    },
    retry(reference) { const entry = entries.get(referenceKey(reference)); if (entry) { entry.state = { status: 'loading', value: null }; entry.listeners.forEach(listener => listener(entry.state)); enqueue(reference, entry); } },
    destroy() { destroyed = true; clearInterval(refresh); controllers.forEach(controller => controller.abort()); entries.clear(); pending.clear(); },
  };
}
