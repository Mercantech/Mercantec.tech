import test from 'node:test';
import assert from 'node:assert/strict';
import katex from 'katex';
import { isLatexLanguage, latexExpression, latexRenderOptions, renderLatex } from '../src/latex-renderer.ts';

test('recognises existing LaTeX blocks and strips complete math delimiters for display', () => {
  const formula = String.raw`\frac{a}{b}`;
  assert.equal(isLatexLanguage(' LaTeX '), true);
  assert.equal(isLatexLanguage('tex'), true);
  assert.equal(isLatexLanguage('text'), false);
  for (const [open, close] of [['$$', '$$'], ['\\[', '\\]'], ['\\(', '\\)'], ['$', '$']]) {
    assert.equal(latexExpression(' ' + open + '\n' + formula + '\n' + close + ' '), formula);
  }
  assert.equal(latexExpression(formula), formula);
  assert.equal(latexExpression('\\[' + formula), '\\[' + formula);
});

test('renders accessible display math and rejects unsafe attributes and unbounded macros', () => {
  const render = source => katex.renderToString(source, latexRenderOptions);
  assert.match(render('\\text{Accuracy} = \\frac{3}{4}'), /<mfrac>/);
  assert.doesNotMatch(render('\\href{https://example.com}{x}'), /<a\b|href=/);
  assert.doesNotMatch(render('\\includegraphics{https://example.com/img.png}'), /<img\b|src=/);
  assert.throws(() => render('\\frac{a}'), katex.ParseError);
  assert.throws(() => render('\\def\\loop{\\loop}\\loop'), katex.ParseError);
});

test('empty and oversized input shows a safe fallback before loading KaTeX', async () => {
  const container = { dataset: {}, textContent: '' };
  assert.equal(await renderLatex(container, '  $$ $$  '), 'empty');
  assert.equal(await renderLatex(container, 'x'.repeat(12_001)), 'error');
  assert.equal(container.dataset.latexState, 'error');
  assert.match(container.textContent, /for lang/);
});
