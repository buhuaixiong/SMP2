import type { WritableComputedRef } from "vue";
import { createI18n, type LocaleMessageDictionary, type VueMessageType } from "vue-i18n";

export const SUPPORTED_LOCALES = ["en", "zh", "th"] as const;
export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number];
export const DEFAULT_LOCALE: SupportedLocale = "zh"; // 默认语言：中文
export const FALLBACK_LOCALE: SupportedLocale = "en"; // 回退语言：英文（翻译缺失时使用）

type LocaleMetadata = {
  code: string;
  name: string;
};

type LocaleMessages = LocaleMessageDictionary<VueMessageType> & {
  locale: LocaleMetadata;
};

const localeModuleImports = import.meta.glob("./locales/*/*.json", {
  import: "default",
});

const localeModuleLoaders = new Map<SupportedLocale, Map<string, () => Promise<unknown>>>();
for (const locale of SUPPORTED_LOCALES) {
  localeModuleLoaders.set(locale, new Map());
}

for (const [modulePath, loader] of Object.entries(localeModuleImports)) {
  const normalizedPath = modulePath.replace(/\\/g, "/");
  const segments = normalizedPath.split("/");
  const localeSegment = segments.at(-2);
  const fileName = segments.at(-1);

  if (!localeSegment || !fileName) {
    continue;
  }

  if (!SUPPORTED_LOCALES.includes(localeSegment as SupportedLocale)) {
    continue;
  }

  const moduleName = fileName.replace(/\.json$/, "");
  const localeKey = localeSegment as SupportedLocale;
  localeModuleLoaders.get(localeKey)!.set(moduleName, loader as () => Promise<unknown>);
}

const localeMetadataMap = new Map<SupportedLocale, LocaleMetadata>();
const LOCALE_METADATA_OVERRIDES: Partial<Record<SupportedLocale, LocaleMetadata>> = {
  zh: { code: "zh-CN", name: "中文" },
  th: { code: "th-TH", name: "Thai" },
};

const loadLocaleMessages = async (locale: SupportedLocale): Promise<LocaleMessages> => {
  const loaderMap = localeModuleLoaders.get(locale);
  if (!loaderMap) {
    throw new Error(`Unknown locale "${locale}".`);
  }
  if (loaderMap.size === 0 && locale !== FALLBACK_LOCALE) {
    return loadLocaleMessages(FALLBACK_LOCALE);
  }

  const entries = await Promise.all(
    [...loaderMap.entries()].map(async ([moduleName, loadModule]) => {
      const moduleData = await loadModule();
      return [moduleName, moduleData] as const;
    }),
  );

  const bundle = Object.fromEntries(entries) as LocaleMessages;

  if (!("locale" in bundle)) {
    if (locale !== FALLBACK_LOCALE) {
      return loadLocaleMessages(FALLBACK_LOCALE);
    }
    throw new Error(
      `Locale metadata missing for "${locale}". Ensure ./locales/${locale}/locale.json exists.`,
    );
  }

  return bundle;
};

await Promise.all(
  SUPPORTED_LOCALES.map(async (locale) => {
    const loader = localeModuleLoaders.get(locale)?.get("locale");
    if (!loader) {
      const override = LOCALE_METADATA_OVERRIDES[locale];
      if (!override) {
        throw new Error(`Missing locale metadata loader for "${locale}".`);
      }
      localeMetadataMap.set(locale, override);
      return;
    }
    const metadata = (await loader()) as LocaleMetadata;
    localeMetadataMap.set(locale, metadata);
  }),
);

const STORAGE_KEY = "supplier-system.locale";

const normalizeLocale = (value?: string | null): SupportedLocale | null => {
  if (!value) {
    return null;
  }
  const candidate = value.toLowerCase();
  return (
    SUPPORTED_LOCALES.find(
      (locale) => candidate === locale || candidate.startsWith(`${locale}-`),
    ) ?? null
  );
};

const readStoredLocale = (): SupportedLocale | null => {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    return normalizeLocale(stored);
  } catch {
    return null;
  }
};

const detectNavigatorLocale = (): SupportedLocale | null => {
  if (typeof navigator === "undefined") {
    return null;
  }
  const preferred = Array.isArray(navigator.languages) ? navigator.languages : [navigator.language];
  for (const locale of preferred) {
    const normalized = normalizeLocale(locale);
    if (normalized) {
      return normalized;
    }
  }
  return null;
};

export const resolveInitialLocale = (): SupportedLocale =>
  readStoredLocale() ?? detectNavigatorLocale() ?? DEFAULT_LOCALE;

const localeMessageCache = new Map<SupportedLocale, LocaleMessages>();

const initialLocale = resolveInitialLocale();
const initialMessages = await loadLocaleMessages(initialLocale);
localeMessageCache.set(initialLocale, initialMessages);

// 确保英文作为回退语言始终被加载
if (initialLocale !== FALLBACK_LOCALE) {
  const fallbackMessages = await loadLocaleMessages(FALLBACK_LOCALE);
  localeMessageCache.set(FALLBACK_LOCALE, fallbackMessages);
}

const buildMessageRecord = () =>
  Object.fromEntries(localeMessageCache) as Record<SupportedLocale, LocaleMessages>;

const i18n = createI18n({
  legacy: false,
  globalInjection: true,
  locale: initialLocale,
  fallbackLocale: FALLBACK_LOCALE, // 使用英文作为回退语言
  messages: buildMessageRecord(),
} as any) as ReturnType<typeof createI18n>;

const getLocaleRef = (): WritableComputedRef<SupportedLocale> =>
  i18n.global.locale as unknown as WritableComputedRef<SupportedLocale>;

const ensureLocaleMessages = async (locale: SupportedLocale) => {
  if (localeMessageCache.has(locale)) {
    return;
  }
  const messages = await loadLocaleMessages(locale);
  localeMessageCache.set(locale, messages);
  i18n.global.setLocaleMessage(locale, messages);
};

export const localeOptions = SUPPORTED_LOCALES.map((code) => {
  const metadata = localeMetadataMap.get(code);
  if (!metadata) {
    throw new Error(`Missing metadata for locale "${code}".`);
  }
  return {
    value: code,
    label: metadata.name,
    intlCode: metadata.code,
  };
});

export const getIntlLocaleCode = (locale: SupportedLocale): string => {
  const metadata = localeMetadataMap.get(locale);
  if (!metadata) {
    throw new Error(`Missing metadata for locale "${locale}".`);
  }
  return metadata.code;
};

export const setI18nLocale = async (locale: SupportedLocale) => {
  await ensureLocaleMessages(locale);
  getLocaleRef().value = locale;
};

export const persistLocale = (locale: SupportedLocale) => {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(STORAGE_KEY, locale);
  } catch {
    // ignore write failures (e.g., private mode)
  }
};

export const getActiveLocale = (): SupportedLocale => getLocaleRef().value;

export const getActiveIntlLocale = (): string => getIntlLocaleCode(getLocaleRef().value);

export default i18n;
