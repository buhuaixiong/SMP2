import { defineConfig } from "vitest/config";
import vue from "@vitejs/plugin-vue";
import AutoImport from "unplugin-auto-import/vite";
import Components from "unplugin-vue-components/vite";
import { ElementPlusResolver } from "unplugin-vue-components/resolvers";

export default defineConfig({
  plugins: [
    vue(),
    AutoImport({
      resolvers: [ElementPlusResolver()],
    }),
    Components({
      resolvers: [ElementPlusResolver()],
    }),
  ],
  resolve: {
    alias: {
      "@": "/src",
    },
  },
  test: {
    environment: "jsdom",
    include: ["tests/**/*.spec.{ts,tsx,js,jsx}"],
    exclude: [
      "tests/e2e/**",
      "**/node_modules/**",
      "dist/**",
      "supplier-backend/**",
      "extracted_backend/**",
    ],
  },
});
