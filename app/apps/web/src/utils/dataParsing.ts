/**
 * Data parsing and resolution utilities for RFQ and related modules.
 * Provides common helper functions for data normalization, type conversion,
 * and fallback value resolution.
 */

/**
 * Converts a value to a valid number, or returns null if invalid.
 * Handles null, undefined, strings, and numbers.
 */
export function toNumber(value: unknown): number | null {
  if (value === null || value === undefined) {
    return null;
  }
  if (typeof value === "number") {
    return Number.isFinite(value) ? value : null;
  }
  if (typeof value === "string") {
    const trimmed = value.trim();
    if (trimmed === "") {
      return null;
    }
    const parsed = Number(trimmed);
    return Number.isFinite(parsed) ? parsed : null;
  }
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

/**
 * Picks the first valid number from a list of values.
 * Returns null if no valid number is found.
 */
export function pickFirstNumber(...values: unknown[]): number | null {
  for (const value of values) {
    const numericValue = toNumber(value);
    if (numericValue !== null) {
      return numericValue;
    }
  }
  return null;
}

/**
 * Picks the first defined (non-null, non-undefined, non-empty string) value from a list.
 * Returns null if no valid value is found.
 */
export function firstDefinedValue<T = unknown>(...values: T[]): T | null {
  for (const value of values) {
    if (value === undefined || value === null) {
      continue;
    }
    if (typeof value === "string") {
      const trimmed = value.trim();
      if (trimmed === "") {
        continue;
      }
      return trimmed as T;
    }
    return value;
  }
  return null;
}

/**
 * Resolves a value using multiple fallback keys from a source object.
 * Returns the first value that is not null, undefined, or empty string.
 */
export function resolveWithFallback<T = string>(
  source: Record<string, unknown>,
  keys: string[],
  defaultValue: T
): T {
  for (const key of keys) {
    const value = source[key];
    if (value !== null && value !== undefined) {
      if (typeof value === "string" && value.trim() === "") {
        continue;
      }
      return value as T;
    }
  }
  return defaultValue;
}

/**
 * Normalizes a key value for matching purposes.
 * Converts numbers to strings, lowercases strings, etc.
 */
export function normalizeKey(value: unknown): string | null {
  if (value === null || value === undefined) {
    return null;
  }
  if (typeof value === "number") {
    return Number.isFinite(value) ? value.toString() : null;
  }
  if (typeof value === "string") {
    const trimmed = value.trim();
    if (trimmed === "") {
      return null;
    }
    const numeric = Number(trimmed);
    if (!Number.isNaN(numeric) && Number.isFinite(numeric)) {
      return numeric.toString();
    }
    return trimmed.toLowerCase();
  }
  const numeric = Number(value);
  if (!Number.isNaN(numeric) && Number.isFinite(numeric)) {
    return numeric.toString();
  }
  return String(value).trim().toLowerCase() || null;
}

/**
 * Builds a set of normalized keys from a line item object.
 * Useful for matching quote items with line items using multiple possible ID field names.
 */
export function buildLineItemKeySet(lineItem: unknown): Set<string> {
  const record = lineItem && typeof lineItem === "object" ? (lineItem as Record<string, unknown>) : {};
  const keys = [
    record?.id,
    record?.lineItemId,
    record?.line_item_id,
    record?.rfqLineItemId,
    record?.rfq_line_item_id,
    record?.rfqItemId,
    record?.rfq_item_id,
    record?.itemId,
    record?.item_id,
    record?.lineNumber,
    record?.line_number,
    record?.itemNumber,
    record?.item_number,
    record?.itemNo,
    record?.item_no,
    record?.sku,
    record?.partNumber,
    record?.part_number,
  ];
  const set = new Set<string>();
  keys.forEach((raw) => {
    const key = normalizeKey(raw);
    if (key) {
      set.add(key);
    }
  });
  return set;
}

/**
 * Compares two nullable numbers for sorting.
 * Null values are sorted to the end.
 */
export function compareNullableNumbers(a: number | null, b: number | null): number {
  if (a === null && b === null) return 0;
  if (a === null) return 1;
  if (b === null) return -1;
  return a - b;
}

/**
 * Safely gets a nested property from an object using a path array.
 * Returns the default value if the path doesn't exist.
 */
export function getNestedValue<T = unknown>(
  obj: Record<string, unknown>,
  path: string[],
  defaultValue: T
): T {
  let current: unknown = obj;
  for (const key of path) {
    if (current === null || current === undefined) {
      return defaultValue;
    }
    if (typeof current === "object") {
      current = (current as Record<string, unknown>)[key];
    } else {
      return defaultValue;
    }
  }
  return (current ?? defaultValue) as T;
}
