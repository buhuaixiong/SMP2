/// <reference types="vite/client" />
/// <reference types="vitest" />

interface ImportMetaEnv {
  readonly VITE_APP_BASE_PATH?: string;
  readonly VITE_APP_API_BASE_URL?: string;
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_SUPPLIER_REGISTRATION_URL?: string;
  readonly VITE_THIRD_PARTY_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

declare module "*.vue" {
  import type { DefineComponent } from "vue";
  const component: DefineComponent<Record<string, unknown>, Record<string, unknown>, any>;
  export default component;
}