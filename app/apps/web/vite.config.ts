import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import { fileURLToPath, URL } from 'node:url'

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  process.env.VITE_CSP_CONNECT = env.VITE_CSP_CONNECT ?? "'self'"
  console.log(`[vite] CSP connect-src => `)

  const rawBasePath = env.VITE_APP_BASE_PATH || '/'
  const normalizedBasePath = rawBasePath.startsWith('/')
    ? (rawBasePath.endsWith('/') ? rawBasePath : `${rawBasePath}/`)
    : `/${rawBasePath}/`

  // Network drives on Windows often throw fs.watch UNKNOWN errors.
  // Enable polling in dev by default on Windows, and allow env override.
  const watchIntervalRaw = Number(env.VITE_WATCH_INTERVAL ?? process.env.CHOKIDAR_INTERVAL ?? 1000)
  const watchInterval = Number.isFinite(watchIntervalRaw) ? watchIntervalRaw : 1000
  const usePollingRaw = env.VITE_USE_POLLING ?? process.env.CHOKIDAR_USEPOLLING ?? (process.platform === 'win32' ? 'true' : 'false')
  const usePolling = `${usePollingRaw}`.toLowerCase() === 'true'

  return {
    base: normalizedBasePath,
    plugins: [
      vue(),
      AutoImport({
        resolvers: [ElementPlusResolver()],
      }),
      Components({
        resolvers: [
          ElementPlusResolver({
            importStyle: true,
          }),
        ],
      }),
    ],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      watch: usePolling
        ? {
            usePolling: true,
            interval: watchInterval,
          }
        : undefined,
      proxy: {
        '/api': {
          target: env.VITE_API_BASE_URL || 'http://localhost:5001',
          changeOrigin: true,
          secure: false,
        },
        '/uploads': {
          target: env.VITE_API_BASE_URL || 'http://localhost:5001',
          changeOrigin: true,
          secure: false,
        },
      },
    },
    build: {
      target: 'es2022', // 兼容更多浏览器
      cssTarget: 'chrome61', // CSS 兼容性
      minify: 'terser',
      terserOptions: {
        compress: {
          drop_console: false,
        },
      },
    },
  }
})
