import { defineConfig } from 'vite';
import { cp, readFile, readdir } from 'node:fs/promises';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const katexPackage = new URL('./node_modules/katex/', import.meta.url);
const katexVersion = JSON.parse(readFileSync(new URL('package.json', katexPackage), 'utf8')).version;
const katexAssets = `katex-${katexVersion}`;

export default defineConfig({
  plugins: [
    { name: 'drawing-local-fonts', async closeBundle() {
      await cp(fileURLToPath(new URL('./node_modules/@excalidraw/excalidraw/dist/prod/fonts', import.meta.url)), fileURLToPath(new URL('../web/wwwroot/notion-editor/excalidraw/fonts', import.meta.url)), { recursive: true });
    } },
    { name: 'katex-local-assets', async generateBundle() {
      const fontDirectory = new URL('dist/fonts/', katexPackage);
      const styles = await readFile(new URL('dist/katex-swap.min.css', katexPackage), 'utf8');
      // The editor supports modern browsers; serve WOFF2 without copying legacy fallbacks.
      const source = styles.replace(/,url\(fonts\/[^)]*\.(?:woff|ttf)\) format\("[^"]*"\)/g, '');
      this.emitFile({ type: 'asset', fileName: `${katexAssets}/katex.css`, source });
      for (const filename of await readdir(fontDirectory)) {
        if (!filename.endsWith('.woff2')) continue;
        const font = await readFile(new URL(filename, fontDirectory));
        this.emitFile({ type: 'asset', fileName: `${katexAssets}/fonts/${filename}`, source: font });
      }
    } },
  ],
  base: '/notion-editor/',
  define: {
    'process.env.NODE_ENV': JSON.stringify('production'),
    __LUP_KATEX_STYLES__: JSON.stringify(`/notion-editor/${katexAssets}/katex.css`),
  },
  build: {
    target: 'es2022',
    outDir: '../web/wwwroot/notion-editor',
    emptyOutDir: true,
    lib: { entry: 'src/editor.tsx', formats: ['es'], fileName: () => 'editor.js', cssFileName: 'editor' },
    rollupOptions: { onwarn(warning, defaultHandler) { if(warning.code === 'MODULE_LEVEL_DIRECTIVE' && warning.message.includes('use client')) return; defaultHandler(warning); }, output: {
      assetFileNames: '[name][extname]', chunkFileNames: '[name]-[hash].js',
      manualChunks(id) {
        // Pinned Excalidraw 0.18.1 shares these pure helpers/constants with its font worker.
        // Keep them out of the UI chunk: workers cannot evaluate its DOM side effects.
        if (/\/@excalidraw\/excalidraw\/dist\/prod\/chunk-(SRAX5OIU|ZUYEQ4TG)\.js$/.test(id.replace(/\\/g, '/'))) return 'excalidraw-foundation';
      },
    } },
  },
});
