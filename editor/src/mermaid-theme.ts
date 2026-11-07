export type MermaidPalette = {
  paper: string;
  ink: string;
  muted: string;
  rule: string;
  accent: string;
  tint: string;
  dark: boolean;
  fontFamily: string;
};

function luminance(color: string): number {
  const channels = color.match(/[0-9a-f]{2}/gi)!.map(channel => parseInt(channel, 16) / 255);
  return channels.reduce((sum, value, index) =>
    sum + (value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4)
      * [.2126, .7152, .0722][index], 0);
}

/** Resolve inherited CSS colors before Mermaid applies its hex-only color calculations. */
export function readMermaidPalette(probe: HTMLElement): MermaidPalette {
  const document = probe.ownerDocument;
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = 1;
  const context = canvas.getContext('2d', { willReadFrequently: true });
  const originalColor = probe.style.getPropertyValue('color');
  const originalPriority = probe.style.getPropertyPriority('color');

  const read = (variable: string, fallback: string) => {
    probe.style.setProperty('color', 'var(' + variable + ', ' + fallback + ')', 'important');
    const color = getComputedStyle(probe).color;
    if (!context) return fallback;
    context.fillStyle = fallback;
    context.fillRect(0, 0, 1, 1);
    context.fillStyle = color;
    context.fillRect(0, 0, 1, 1);
    const channels = context.getImageData(0, 0, 1, 1).data;
    return '#' + Array.from(channels).slice(0, 3).map(value => value.toString(16).padStart(2, '0')).join('');
  };

  try {
    const paper = read('--notion-paper', '#ffffff');
    const ink = read('--notion-ink', '#17243b');
    return {
      paper,
      ink,
      muted: read('--notion-muted', '#63728b'),
      rule: read('--notion-rule', '#dfe7f2'),
      accent: read('--notion-blue', '#2452e8'),
      tint: read('--notion-tint', '#eaf0ff'),
      dark: luminance(paper) < luminance(ink),
      fontFamily: getComputedStyle(probe).fontFamily || '"DM Sans", system-ui, sans-serif',
    };
  } finally {
    if (originalColor) probe.style.setProperty('color', originalColor, originalPriority);
    else probe.style.removeProperty('color');
  }
}

export function mermaidThemeVariables(palette: MermaidPalette) {
  const { paper, ink, muted, rule, accent, tint, dark, fontFamily } = palette;
  return {
    darkMode: dark,
    background: paper,
    fontFamily,
    fontSize: '14px',
    primaryColor: tint,
    primaryTextColor: ink,
    primaryBorderColor: rule,
    secondaryColor: paper,
    secondaryTextColor: ink,
    secondaryBorderColor: rule,
    tertiaryColor: paper,
    tertiaryTextColor: ink,
    tertiaryBorderColor: rule,
    lineColor: muted,
    textColor: ink,
    mainBkg: tint,
    nodeBkg: tint,
    nodeBorder: rule,
    nodeTextColor: ink,
    clusterBkg: paper,
    clusterBorder: rule,
    defaultLinkColor: muted,
    edgeLabelBackground: paper,
    titleColor: ink,
    arrowheadColor: muted,
    actorBkg: tint,
    actorTextColor: ink,
    actorBorder: rule,
    actorLineColor: muted,
    signalColor: muted,
    signalTextColor: ink,
    labelBoxBkgColor: paper,
    labelBoxBorderColor: rule,
    labelTextColor: ink,
    loopTextColor: ink,
    noteBkgColor: tint,
    noteTextColor: ink,
    noteBorderColor: rule,
    activationBkgColor: tint,
    activationBorderColor: rule,
    sequenceNumberColor: paper,
    attributeBackgroundColorOdd: tint,
    attributeBackgroundColorEven: paper,
    classText: ink,
    stateLabelColor: ink,
    stateBkg: tint,
    transitionColor: muted,
    transitionLabelColor: ink,
    labelBackgroundColor: paper,
    compositeBackground: paper,
    compositeTitleBackground: tint,
    compositeBorder: rule,
    innerEndBackground: ink,
    specialStateColor: ink,
    errorBkgColor: paper,
    errorTextColor: ink,
    pie1: tint,
    pie2: paper,
    pie3: tint,
    pie4: paper,
    pie5: tint,
    pie6: paper,
    pie7: tint,
    pie8: paper,
    pie9: tint,
    pie10: paper,
    pie11: tint,
    pie12: paper,
    pieTitleTextColor: ink,
    pieSectionTextColor: ink,
    pieLegendTextColor: ink,
    pieStrokeColor: rule,
    pieOuterStrokeColor: rule,
    pieSectionTextSize: '14px',
    pieLegendTextSize: '14px',
    pieTitleTextSize: '16px',
    cScale0: tint,
    cScale1: paper,
    cScale2: tint,
    cScale3: paper,
    cScale4: tint,
    cScale5: paper,
    cScale6: tint,
    cScale7: paper,
    cScale8: tint,
    cScale9: paper,
    cScale10: tint,
    cScale11: paper,
    scaleLabelColor: ink,
    taskBkgColor: tint,
    taskBorderColor: rule,
    taskTextColor: ink,
    taskTextOutsideColor: ink,
    activeTaskBkgColor: tint,
    activeTaskBorderColor: accent,
    doneTaskBkgColor: paper,
    doneTaskBorderColor: rule,
    gridColor: rule,
    todayLineColor: accent,
  };
}


function repairInlineNodeContrast(svg: Element, palette: MermaidPalette) {
  const context = globalThis.document.createElement('canvas').getContext('2d', { willReadFrequently: true });
  if (!context) return;
  const contrast = (a: string, b: string) => {
    const bright = Math.max(luminance(a), luminance(b));
    const dark = Math.min(luminance(a), luminance(b));
    return (bright + .05) / (dark + .05);
  };

  for (const node of svg.querySelectorAll('.node')) {
    const shape = node.querySelector<SVGElement>(':scope > .label-container[style]');
    const fill = shape?.style.fill;
    if (!fill || fill === 'none' || fill.includes('var(') || !CSS.supports('color', fill)) continue;
    const labels = Array.from(node.querySelectorAll<SVGElement | HTMLElement>('.label, .label *, .nodeLabel'));
    if (labels.some(label => label.style?.color || label.style?.fill)) continue;

    context.fillStyle = palette.paper;
    context.fillRect(0, 0, 1, 1);
    context.fillStyle = fill;
    context.fillRect(0, 0, 1, 1);
    const channels = context.getImageData(0, 0, 1, 1).data;
    const background = '#' + Array.from(channels).slice(0, 3)
      .map(value => value.toString(16).padStart(2, '0')).join('');
    const ink = contrast(palette.ink, background) >= 4.5 ? palette.ink
      : contrast('#000000', background) >= contrast('#ffffff', background) ? '#000000' : '#ffffff';
    for (const label of labels) {
      label.style?.setProperty('color', ink);
      if (label.matches('text, tspan, .nodeLabel, foreignObject *')) label.style?.setProperty('fill', ink);
    }
  }
}

/** Preserve native SVG dimensions and aspect ratio so responsive CSS can fit the full diagram. */
export function prepareMermaidSvg(markup: string, palette: MermaidPalette): string {
  const document = new DOMParser().parseFromString(markup, 'image/svg+xml');
  const svg = document.documentElement;
  if (svg.localName !== 'svg' || document.querySelector('parsererror')) {
    throw new Error('Diagrammets SVG kunne ikke læses.');
  }

  svg.querySelectorAll('script').forEach(script => script.remove());
  const viewBox = svg.getAttribute('viewBox')?.trim().split(/[\s,]+/).map(Number);
  if (viewBox?.length === 4 && viewBox.every(Number.isFinite) && viewBox[2] > 0 && viewBox[3] > 0) {
    svg.setAttribute('width', String(viewBox[2]));
    svg.setAttribute('height', String(viewBox[3]));
  }
  const style = (svg as unknown as SVGSVGElement).style;
  style.removeProperty('max-width');
  style.removeProperty('width');
  style.removeProperty('height');
  if (!style.color) style.color = palette.ink;
  repairInlineNodeContrast(svg, palette);
  return new XMLSerializer().serializeToString(svg);
}
