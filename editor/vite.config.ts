import { defineConfig } from 'vite';
import { cp } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

export default defineConfig({
  plugins: [{ name: 'drawing-local-fonts', async closeBundle() {
    await cp(fileURLToPath(new URL('./node_modules/@excalidraw/excalidraw/dist/prod/fonts', import.meta.url)), fileURLToPath(new URL('../web/wwwroot/notion-editor/excalidraw/fonts', import.meta.url)), { recursive: true });
  } }],
  base: '/notion-editor/',
  define: { 'process.env.NODE_ENV': JSON.stringify('production') },
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
