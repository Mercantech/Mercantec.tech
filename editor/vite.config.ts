import { defineConfig } from 'vite';

export default defineConfig({
  base: '/notion-editor/',
  define: { 'process.env.NODE_ENV': JSON.stringify('production') },
  build: {
    target: 'es2022',
    outDir: '../web/wwwroot/notion-editor',
    emptyOutDir: true,
    lib: { entry: 'src/editor.tsx', formats: ['es'], fileName: () => 'editor.js', cssFileName: 'editor' },
    rollupOptions: { onwarn(warning, defaultHandler) { if(warning.code === 'MODULE_LEVEL_DIRECTIVE' && warning.message.includes('use client')) return; defaultHandler(warning); }, output: { assetFileNames: '[name][extname]', chunkFileNames: '[name]-[hash].js' } },
  },
});
