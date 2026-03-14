import { computed } from "vue";
import { getActiveIntlLocale, getIntlLocaleCode } from "@/i18n";
import { useLocaleStore } from "@/stores/locale";

const toDate = (value: Date | string | number): Date | null => {
  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? null : value;
  }
  if (typeof value === "number") {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  }
  if (typeof value === "string") {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  }
  return null;
};

const toNumber = (value: number | string): number | null => {
  if (typeof value === "number") {
    return Number.isFinite(value) ? value : null;
  }
  if (typeof value === "string") {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
};

export const formatDate = (
  value: Date | string | number,
  locale: string = getActiveIntlLocale(),
  options: Intl.DateTimeFormatOptions = {},
): string => {
  const date = toDate(value);
  if (!date) {
    return "";
  }
  const formatter = new Intl.DateTimeFormat(locale, options);
  return formatter.format(date);
};

export const formatNumber = (
  value: number | string,
  locale: string = getActiveIntlLocale(),
  options: Intl.NumberFormatOptions = {},
): string => {
  const parsed = toNumber(value);
  if (parsed === null) {
    return "";
  }
  const formatter = new Intl.NumberFormat(locale, options);
  return formatter.format(parsed);
};

export const formatCurrency = (
  value: number | string,
  currency: string,
  locale: string = getActiveIntlLocale(),
  options: Intl.NumberFormatOptions = {},
): string => formatNumber(value, locale, { style: "currency", currency, ...options });

export const useLocaleFormatters = () => {
  const localeStore = useLocaleStore();
  const intlLocale = computed(() => getIntlLocaleCode(localeStore.currentLocale));

  return {
    currentLocale: computed(() => localeStore.currentLocale),
    intlLocale,
    formatDate: (value: Date | string | number, options: Intl.DateTimeFormatOptions = {}) =>
      formatDate(value, intlLocale.value, options),
    formatNumber: (value: number | string, options: Intl.NumberFormatOptions = {}) =>
      formatNumber(value, intlLocale.value, options),
    formatCurrency: (
      value: number | string,
      currency: string,
      options: Intl.NumberFormatOptions = {},
    ) => formatCurrency(value, currency, intlLocale.value, options),
  };
};
