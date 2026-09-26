import path from 'node:path'
import { defineConfig } from 'vitest/config'

const alias = { '@': path.resolve(import.meta.dirname, './src') }

// Separate from vite.config.ts: that one is a function of the mode and carries the
// dev proxy, none of which the tests need. Only the alias has to match.
//
// Two projects: pure logic (`*.test.ts`) needs no DOM and stays fast in node;
// components (`*.test.tsx`) render into jsdom.
export default defineConfig({
  test: {
    projects: [
      {
        resolve: { alias },
        test: {
          name: 'logic',
          environment: 'node',
          include: ['src/**/*.test.ts'],
        },
      },
      {
        resolve: { alias },
        test: {
          name: 'components',
          environment: 'jsdom',
          include: ['src/**/*.test.tsx'],
          setupFiles: ['src/test/setup-dom.ts'],
        },
      },
    ],
  },
})
