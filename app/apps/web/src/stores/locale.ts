import { defineStore } from "pinia";
import {
  DEFAULT_LOCALE,
  SUPPORTED_LOCALES,
  type SupportedLocale,
  localeOptions,
  persistLocale,
  resolveInitialLocale,
  setI18nLocale,
  getActiveLocale,
} from "@/i18n";

const isSupported = (value: string): value is SupportedLocale =>
  SUPPORTED_LOCALES.includes(value as SupportedLocale);

export const useLocaleStore = defineStore("locale", {
  state: () => ({
    currentLocale: getActiveLocale() ?? DEFAULT_LOCALE,
  }),
  getters: {
    availableLocales: () => localeOptions,
  },
  actions: {
    async initialize(locale?: SupportedLocale) {
      const resolved = locale ?? resolveInitialLocale();
      await this.setLocale(resolved);
    },
    async setLocale(locale: SupportedLocale | string) {
      if (!isSupported(locale)) {
        return;
      }
      await setI18nLocale(locale);
      this.currentLocale = locale;
      persistLocale(locale);
    },
  },
});
