type Languages = Record<string, { name: string; aliases?: readonly string[] }>;

let nextMenuId = 0;

export function createCodeLanguagePicker(
  languages: Languages,
  canEdit: () => boolean,
  onChoose: (language: string) => void,
) {
  const controller = new AbortController();
  const { signal } = controller;
  let positionListeners: AbortController | undefined;
  let selected = '';

  const picker = document.createElement('div');
  picker.className = 'notion-code-language-picker';
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'notion-code-language-button';
  button.setAttribute('aria-haspopup', 'dialog');
  button.setAttribute('aria-expanded', 'false');
  const name = document.createElement('span');
  name.className = 'notion-code-language-name';
  button.append(name);

  const menu = document.createElement('div');
  menu.id = `notion-code-language-${++nextMenuId}`;
  menu.className = 'notion-code-language-menu';
  menu.setAttribute('popover', 'auto');
  menu.setAttribute('role', 'dialog');
  menu.setAttribute('aria-label', 'Kodesprog');
  button.setAttribute('popovertarget', menu.id);
  button.setAttribute('aria-controls', menu.id);

  const search = document.createElement('input');
  search.className = 'notion-code-language-search';
  search.type = 'search';
  search.placeholder = 'Find sprog…';
  search.setAttribute('aria-label', 'Find kodesprog');
  const options = document.createElement('div');
  options.className = 'notion-code-language-options';
  menu.append(search, options);
  picker.append(button, menu);

  function renderOptions() {
    const query = search.value.trim().toLowerCase();
    const results = document.createDocumentFragment();
    for (const [id, language] of Object.entries(languages)) {
      const terms = [id, language.name, ...(language.aliases ?? [])];
      if (query && !terms.some(term => term.toLowerCase().includes(query))) continue;
      const option = document.createElement('button');
      option.type = 'button';
      option.className = 'notion-code-language-option';
      option.dataset.language = id;
      option.textContent = language.name;
      option.setAttribute('aria-pressed', String(id === selected));
      results.append(option);
    }
    if (!results.childElementCount) {
      const empty = document.createElement('p');
      empty.textContent = 'Ingen sprog matcher.';
      results.append(empty);
    }
    options.replaceChildren(results);
    options.scrollTop = 0;
  }

  function positionMenu() {
    const viewport = window.visualViewport;
    const left = (viewport?.offsetLeft ?? 0) + 8;
    const top = (viewport?.offsetTop ?? 0) + 8;
    const right = left + (viewport?.width ?? window.innerWidth) - 16;
    const bottom = top + (viewport?.height ?? window.innerHeight) - 16;
    const anchor = button.getBoundingClientRect();
    const below = bottom - anchor.bottom - 6;
    const above = anchor.top - top - 6;
    const flip = below < 260 && above > below;
    const height = Math.max(0, Math.min(bottom - top, flip ? above : below));
    menu.style.setProperty('--code-menu-max-height', `${height}px`);
    menu.style.setProperty('--code-menu-left', `${Math.max(left, Math.min(anchor.left, right - menu.offsetWidth))}px`);
    menu.style.setProperty('--code-menu-top', `${flip ? Math.max(top, anchor.top - menu.offsetHeight - 6) : Math.min(bottom, anchor.bottom + 6)}px`);
  }

  function close() {
    menu.hidePopover();
    button.focus({ preventScroll: true });
  }

  function choose(option: HTMLButtonElement) {
    const language = option.dataset.language;
    close();
    if (language && language !== selected && canEdit()) onChoose(language);
  }

  menu.addEventListener('beforetoggle', event => {
    if (event.newState !== 'open') return;
    if (!canEdit()) {
      event.preventDefault();
      return;
    }
    search.value = '';
    renderOptions();
  }, { signal });
  menu.addEventListener('toggle', event => {
    const open = event.newState === 'open';
    button.setAttribute('aria-expanded', String(open));
    positionListeners?.abort();
    if (!open) return;
    positionMenu();
    positionListeners = new AbortController();
    const positionOptions = { signal: positionListeners.signal, passive: true };
    window.addEventListener('resize', positionMenu, positionOptions);
    window.addEventListener('scroll', event => {
      if (!(event.target instanceof Node) || !menu.contains(event.target)) positionMenu();
    }, { ...positionOptions, capture: true });
    window.visualViewport?.addEventListener('resize', positionMenu, positionOptions);
    window.visualViewport?.addEventListener('scroll', positionMenu, positionOptions);
    const first = options.querySelector<HTMLButtonElement>('[aria-pressed="true"]');
    (matchMedia('(pointer: coarse)').matches ? first : search)?.focus({ preventScroll: true });
  }, { signal });
  search.addEventListener('input', renderOptions, { signal });
  options.addEventListener('click', event => {
    const option = (event.target as Element).closest<HTMLButtonElement>('.notion-code-language-option');
    if (option) choose(option);
  }, { signal });
  menu.addEventListener('keydown', event => {
    event.stopPropagation();
    if (event.key === 'Escape') {
      event.preventDefault();
      close();
      return;
    }
    const buttons = Array.from(options.querySelectorAll('button'));
    if (!buttons.length) return;
    const current = buttons.indexOf(document.activeElement as HTMLButtonElement);
    if (event.target === search && event.key === 'Enter') {
      event.preventDefault();
      choose(buttons[0]);
    } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const next = event.key === 'ArrowDown'
        ? (current + 1) % buttons.length
        : (current <= 0 ? buttons.length - 1 : current - 1);
      buttons[next].focus();
    }
  }, { signal });

  return {
    dom: picker,
    setLanguage(language: string) {
      if (language === selected) return;
      selected = language;
      name.textContent = languages[language].name;
      button.setAttribute('aria-label', `Kodesprog: ${name.textContent}`);
      for (const option of options.querySelectorAll('button')) {
        option.setAttribute('aria-pressed', String(option.dataset.language === selected));
      }
    },
    destroy() {
      positionListeners?.abort();
      controller.abort();
    },
  };
}
