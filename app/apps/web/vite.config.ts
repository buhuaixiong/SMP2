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

  const normalizeBasePath = (value: string) => {
    if (!value || value.trim() === '') return '/'
    const withLeadingSlash = value.startsWith('/') ? value : `/${value}`
    return withLeadingSlash.endsWith('/') ? withLeadingSlash : `${withLeadingSlash}/`
  }

  const appBasePath = normalizeBasePath(env.VITE_APP_BASE_PATH || '/')

  return {
    base: appBasePath,
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
      target: 'es2022', // ¼æÈÝ¸ü¶àä¯ÀÀÆ÷
      cssTarget: 'chrome61', // CSS ¼æÈÝÐÔ
      minify: 'terser',
      terserOptions: {
        compress: {
          drop_console: false,
        },
      },
    },
  }
})