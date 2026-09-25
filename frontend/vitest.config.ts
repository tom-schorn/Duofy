import path from 'node:path'
import { defineConfig } from 'vitest/config'

// Separate from vite.config.ts: that one is a function of the mode and carries the
// dev proxy, none of which the tests need. Only the alias has to match.
export default defineConfig({
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  test: {
    // Domain logic only for now — no DOM needed.
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
})
