import { describe, expect, it } from "vitest";
import {
  buildQuoteItemIndex,
  findMatchingQuoteItemFromIndex,
  summarizeNumericValues,
} from "@/utils/rfqPriceComparisonIndex";

type QuoteItem = {
  id?: number;
  lineItemId?: number;
  line_item_id?: number;
  rfqLineItemId?: number;
  rfq_line_item_id?: number;
  lineNumber?: number;
  line_number?: number;
  itemNumber?: string;
  unitPrice?: number;
};

type Quote = {
  id: number;
  items?: QuoteItem[];
  quoteItems?: QuoteItem[];
};

describe("rfq derived data helpers", () => {
  it("indexes quote items and resolves matching line item in O(1) lookup path", () => {
    const quotes: Quote[] = [
      {
        id: 10,
        quoteItems: [
          { lineItemId: 1001, unitPrice: 12.5 },
          { lineNumber: 2, unitPrice: 9.9 },
        ],
      },
    ];

    const index = buildQuoteItemIndex(quotes as Array<Record<string, unknown>>);
    const matchById = findMatchingQuoteItemFromIndex(index, 10, { id: 1001 }, 0);
    const matchByLineNumber = findMatchingQuoteItemFromIndex(index, 10, { lineNumber: 2 }, 1);

    expect(matchById?.unitPrice).toBe(12.5);
    expect(matchByLineNumber?.unitPrice).toBe(9.9);
  });

  it("summarizes numeric values in one pass", () => {
    const result = summarizeNumericValues([10, null, 20, 30]);
    expect(result.count).toBe(3);
    expect(result.min).toBe(10);
    expect(result.max).toBe(30);
    expect(result.sum).toBe(60);
    expect(result.avg).toBe(20);
  });
});

