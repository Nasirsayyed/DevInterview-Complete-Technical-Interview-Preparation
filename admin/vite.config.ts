import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// VITE_API_BASE_URL points at the ASP.NET Core API (default: local dev server). No secrets are ever bundled.
export default defineConfig({
  plugins: [react()],
  server: { port: 5173 },
  build: { chunkSizeWarningLimit: 1200 },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
});
