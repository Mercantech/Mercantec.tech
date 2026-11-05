/** Shared Mermaid source prep and error text for Notion editor + course pages. */

export const MERMAID_RENDER_CONFIG = {
  startOnLoad: false,
  securityLevel: 'strict' as const,
  flowchart: { useMaxWidth: true, htmlLabels: false },
  sequence: { useMaxWidth: true },
  suppressErrorRendering: true,
};

export function normalizeMermaidSource(raw: string): string {
  return String(raw ?? '').replace(/\u00A0/g, ' ').trim();
}

/**
 * Teachers often paste prose on the same line as an edge (e.g. `D --> E Den vil ikke…`).
 * Mermaid treats the tail as invalid syntax; strip it when it clearly is not diagram code.
 */
export function repairAccidentalProseAfterEdgeTarget(line: string): string {
  const arrow = line.lastIndexOf('-->');
  if (arrow === -1) return line;
  const head = line.slice(0, arrow + 3);
  const tail = line.slice(arrow + 3);
  const targetMatch = tail.match(
    /^(\s*(?:[\w-]+|\[[^\]]*\]|\{[^}]*\}|\([^)]*\)))(?:\s+(.*))?$/,
  );
  if (!targetMatch?.[2]) return line;
  const rest = targetMatch[2].trim();
  if (!rest || rest.startsWith('%%')) return line;
  if (/^(style|class|classDef|subgraph|end|click|linkStyle|direction)\b/i.test(rest)) return line;
  return head + targetMatch[1];
}

export function prepareMermaidSource(raw: string): { source: string; repaired: boolean } {
  const normalized = normalizeMermaidSource(raw);
  if (!normalized) return { source: '', repaired: false };
  const lines = normalized.split('\n');
  let repaired = false;
  const out = lines.map((line) => {
    const fixed = repairAccidentalProseAfterEdgeTarget(line);
    if (fixed !== line) repaired = true;
    return fixed;
  });
  return { source: out.join('\n'), repaired };
}

type MermaidErrorLike = {
  message?: string;
  hash?: { text?: string; line?: number; loc?: { first_line?: number } };
};

export function formatMermaidRenderError(err: unknown): string {
  const base = 'Diagrammet kunne ikke tegnes.';
  const e = err as MermaidErrorLike;
  const message = typeof e?.message === 'string' ? e.message.trim() : '';
  if (!message) return `${base} Ret Mermaid-koden, og prøv igen.`;

  const lineNo = e.hash?.loc?.first_line ?? (typeof e.hash?.line === 'number' ? e.hash.line + 1 : undefined);
  const snippet = e.hash?.text ? ` «${e.hash.text}»` : '';
  const lineHint = lineNo ? ` (linje ${lineNo}${snippet})` : snippet ? ` (${snippet.trim()})` : '';

  if (/^Parse error/i.test(message)) {
    return `${base} ${message.split('\n')[0]}${lineHint}.`;
  }
  return `${base} ${message.split('\n')[0]}`;
}

export function stripScriptsFromSvg(svg: string): string {
  if (!/<script[\s>]/i.test(svg)) return svg;
  try {
    const doc = new DOMParser().parseFromString(svg, 'image/svg+xml');
    if (doc.querySelector('parsererror')) return svg;
    doc.querySelectorAll('script').forEach((node) => node.remove());
    return doc.documentElement.outerHTML;
  } catch {
    return svg;
  }
}
