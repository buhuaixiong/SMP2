import { valuesEqual } from "./index";

const cloneArrayValue = (value: unknown[]): unknown[] =>
  value.map((item) => {
    if (Array.isArray(item)) {
      return cloneArrayValue(item);
    }
    if (item && typeof item === "object") {
      return { ...(item as Record<string, unknown>) };
    }
    return item;
  });

const cloneValue = (value: unknown): unknown => {
  if (Array.isArray(value)) {
    return cloneArrayValue(value);
  }
  if (value && typeof value === "object") {
    return { ...(value as Record<string, unknown>) };
  }
  return value;
};

export const buildDraftPayloadSnapshot = <T extends Record<string, unknown>>(form: T): T => {
  const snapshot = {} as T;
  Object.entries(form).forEach(([key, value]) => {
    snapshot[key as keyof T] = cloneValue(value) as T[keyof T];
  });
  return snapshot;
};

export const collectChangedFields = <
  TPrevious extends Record<string, unknown>,
  TNext extends Record<string, unknown>,
>(
  previous: TPrevious,
  next: TNext,
  watchedKeys: string[],
): string[] =>
  watchedKeys.filter((key) => !valuesEqual(previous[key], next[key]));

