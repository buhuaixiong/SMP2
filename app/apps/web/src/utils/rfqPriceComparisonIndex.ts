import { buildLineItemKeySet, normalizeKey, pickFirstNumber } from "./dataParsing";

const QUOTE_ITEM_KEY_FIELDS = [
  "lineItemId",
  "line_item_id",
  "lineId",
  "rfqLineItemId",
  "rfq_line_item_id",
  "rfqItemId",
  "rfq_item_id",
  "id",
  "lineNumber",
  "line_number",
  "lineNo",
  "line_no",
  "itemNumber",
  "item_number",
  "itemNo",
  "item_no",
  "sku",
];

const resolveQuoteItems = (quote: Record<string, unknown>): Array<Record<string, unknown>> => {
  const items = quote.items;
  if (Array.isArray(items) && items.length) {
    return items as Array<Record<string, unknown>>;
  }
  const quoteItems = quote.quoteItems;
  if (Array.isArray(quoteItems)) {
    return quoteItems as Array<Record<string, unknown>>;
  }
  return [];
};

const resolveQuoteId = (quote: Record<string, unknown>): number | null => pickFirstNumber(quote.id);

export type QuoteItemIndex = {
  byQuoteId: Map<number, Map<string, Record<string, unknown>>>;
  orderedItemsByQuoteId: Map<number, Array<Record<string, unknown>>>;
};

export const buildQuoteItemIndex = <TQuote extends object>(
  quotes: TQuote[],
): QuoteItemIndex => {
  const byQuoteId = new Map<number, Map<string, Record<string, unknown>>>();
  const orderedItemsByQuoteId = new Map<number, Array<Record<string, unknown>>>();

  quotes.forEach((quote) => {
    const quoteRecord = quote as Record<string, unknown>;
    const quoteId = resolveQuoteId(quoteRecord);
    if (quoteId === null) {
      return;
    }

    const items = resolveQuoteItems(quoteRecord);
    orderedItemsByQuoteId.set(quoteId, items);

    const itemLookup = new Map<string, Record<string, unknown>>();
    items.forEach((item) => {
      QUOTE_ITEM_KEY_FIELDS.forEach((field) => {
        const key = normalizeKey(item[field]);
        if (key && !itemLookup.has(key)) {
          itemLookup.set(key, item);
        }
      });
    });

    byQuoteId.set(quoteId, itemLookup);
  });

  return { byQuoteId, orderedItemsByQuoteId };
};

export const findMatchingQuoteItemFromIndex = (
  index: QuoteItemIndex,
  quoteId: number | null | undefined,
  lineItem: unknown,
  lineItemIndex: number,
): Record<string, unknown> | null => {
  const normalizedQuoteId = quoteId == null ? null : pickFirstNumber(quoteId);
  if (normalizedQuoteId === null) {
    return null;
  }

  const lineItemKeys = buildLineItemKeySet(lineItem);
  const indexedItems = index.byQuoteId.get(normalizedQuoteId);
  if (indexedItems && lineItemKeys.size > 0) {
    for (const key of lineItemKeys) {
      const match = indexedItems.get(key);
      if (match) {
        return match;
      }
    }
  }

  const orderedItems = index.orderedItemsByQuoteId.get(normalizedQuoteId) ?? [];
  if (orderedItems.length === 1) {
    return orderedItems[0];
  }
  if (lineItemIndex >= 0 && lineItemIndex < orderedItems.length) {
    return orderedItems[lineItemIndex];
  }

  return null;
};

export type NumericSummary = {
  count: number;
  sum: number;
  min: number | null;
  max: number | null;
  avg: number | null;
};

export const summarizeNumericValues = (
  values: Array<number | null | undefined>,
): NumericSummary => {
  let count = 0;
  let sum = 0;
  let min: number | null = null;
  let max: number | null = null;

  values.forEach((value) => {
    if (typeof value !== "number" || !Number.isFinite(value)) {
      return;
    }
    count += 1;
    sum += value;
    if (min === null || value < min) {
      min = value;
    }
    if (max === null || value > max) {
      max = value;
    }
  });

  return {
    count,
    sum,
    min,
    max,
    avg: count > 0 ? sum / count : null,
  };
};
