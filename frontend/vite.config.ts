import path from 'node:path'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Dritter Parameter leer: sonst lädt Vite nur VITE_-Variablen, und der Proxy
  // wird hier zur Bauzeit gebraucht, nicht im Browser.
  const env = loadEnv(mode, process.cwd(), '')
  const proxyTarget = env.VITE_API_PROXY

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        '@': path.resolve(import.meta.dirname, './src'),
      },
    },

    /**
     * Optionaler Proxy für die Entwicklung.
     *
     * Ohne ihn spricht der Dev-Server direkt mit der deployten API — eine
     * andere Herkunft, also CORS. Man müsste die Dev-Adresse im Backend
     * freischalten, was einen Container-Neustart kostet und eine `http://`-
     * Herkunft dauerhaft in der Freigabeliste einer Finanz-App hinterlässt.
     *
     * Mit Proxy holt der Dev-Server die Daten **serverseitig**. Für den
     * Browser kommt alles von derselben Herkunft, CORS greift gar nicht erst.
     *
     * Aktivieren über `frontend/.env.local`:
     *
     *     VITE_API_URL=/api/v1
     *     VITE_API_PROXY=https://dev-api-duofy.tom-schorn.de
     *
     * Ist `VITE_API_PROXY` nicht gesetzt, verhält sich alles wie vorher.
     * Produktionsbauten kennen die Variable nie — dort steht in
     * `VITE_API_URL` die absolute Adresse aus den Pages-Variablen.
     */
    server: proxyTarget
      ? {
          proxy: {
            '/api': {
              target: proxyTarget,
              // Ohne changeOrigin ginge der Host-Header des Dev-Servers raus,
              // und Cloudflare wüsste nicht, welche Seite gemeint ist.
              changeOrigin: true,
            },
          },
        }
      : undefined,
  }
})
