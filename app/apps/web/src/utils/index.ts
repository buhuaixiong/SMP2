// src/utils/index.ts - Shared utilities for registration and auth modules
export const formatDateTime = (iso?: string | null) => {
  if (!iso) return "";
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleString();
};

export const optional = (value?: string | null) => {
  const text = (value ?? "").trim();
  return text.length ? text : undefined;
};

export const isPlainObject = (value: unknown): value is Record<string, unknown> =>
  value != null && typeof value === "object" && !Array.isArray(value);

export const valuesEqual = (left: unknown, right: unknown): boolean => {
  if (left === right) return true;
  if (Array.isArray(left) && Array.isArray(right)) {
    if (left.length !== right.length) return false;
    return left.every((v, i) => valuesEqual(v, right[i]));
  }
  if (isPlainObject(left) && isPlainObject(right)) {
    const leftKeys = Object.keys(left);
    const rightKeys = Object.keys(right);
    if (leftKeys.length !== rightKeys.length) return false;
    return leftKeys.every((key) => valuesEqual(left[key], right[key]));
  }
  return false;
};

export const diffKeys = (saved: Record<string, unknown>, current: Record<string, unknown>): string[] => {
  const keys = new Set([...Object.keys(saved), ...Object.keys(current)]);
  return Array.from(keys).filter((key) => !valuesEqual(saved[key], current[key]));
};

export const translateOr = (t: (key: string) => string, key: string, fallback: string) => {
  const translated = t(key);
  return translated === key ? fallback : translated;
};

export const copyToClipboard = async (value: string): Promise<boolean> => {
  if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(value);
      return true;
    } catch {
      // fallback
    }
  }
  // Legacy fallback
  if (typeof document === "undefined") return false;
  const textarea = document.createElement("textarea");
  textarea.value = value;
  textarea.style.position = "fixed";
  textarea.style.opacity = "0";
  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();
  let copied = false;
  try {
    copied = document.execCommand("copy");
  } finally {
    document.body.removeChild(textarea);
  }
  return copied;
};

export const toNumber = (v: unknown): number | null => (v == null || Number.isNaN(v) ? null : Number(v));
