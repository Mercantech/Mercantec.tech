/// <reference types="vite/client" />

declare const __LUP_KATEX_STYLES__: string;

const maxSourceLength = 12_000;
const renderVersions = new WeakMap<HTMLElement, number>();
let katexStyles: Promise<unknown> | undefined;

type LatexState = 'ready' | 'empty' | 'error';

export const latexRenderOptions = {
  displayMode: true,
  output: 'htmlAndMathml',
  throwOnError: true,
  trust: false,
  maxExpand: 600,
  maxSize: 24,
  strict: 'ignore',
} satisfies import('katex').KatexOptions;

export function isLatexLanguage(language: unknown): boolean {
  return typeof language === 'string' && /^(latex|tex)$/i.test(language.trim());
}

export function latexExpression(source: string): string {
  const expression = source.trim();
  const delimiter = [['$$', '$$'], ['\\[', '\\]'], ['\\(', '\\)'], ['$', '$']].find(([open, close]) =>
    expression.length >= open.length + close.length && expression.startsWith(open) && expression.endsWith(close));
  return delimiter ? expression.slice(delimiter[0].length, -delimiter[1].length).trim() : expression;
}

function loadStyles(): Promise<unknown> {
  if (import.meta.env.DEV) return import('katex/dist/katex-swap.min.css');
  katexStyles ??= new Promise<void>((resolve, reject) => {
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = __LUP_KATEX_STYLES__;
    link.addEventListener('load', () => resolve(), { once: true });
    link.addEventListener('error', () => {
      link.remove();
      reject(new Error('KaTeX stylesheet unavailable'));
    }, { once: true });
    document.head.append(link);
  }).catch(error => {
    katexStyles = undefined;
    throw error;
  });
  return katexStyles;
}

export async function renderLatex(container: HTMLElement, source: string): Promise<LatexState | undefined> {
  const version = (renderVersions.get(container) ?? 0) + 1;
  renderVersions.set(container, version);

  function show(state: LatexState, message?: string): LatexState {
    container.dataset.latexState = state;
    if (message !== undefined) container.textContent = message;
    return state;
  }

  const expression = latexExpression(source);
  if (!expression) return show('empty', 'Skriv en LaTeX-formel.');
  if (expression.length > maxSourceLength) return show('error', 'Formlen er for lang til at blive vist.');

  let katex: typeof import('katex');
  try {
    [katex] = await Promise.all([import('katex'), loadStyles()]);
  } catch {
    if (renderVersions.get(container) === version) return show('error', 'Formelvisningen kunne ikke indlæses.');
    return;
  }
  if (renderVersions.get(container) !== version) return;

  try {
    katex.default.render(expression, container, latexRenderOptions);
    return show('ready');
  } catch {
    return show('error', 'Formlen kunne ikke vises. Kontrollér LaTeX-kilden.');
  }
}
