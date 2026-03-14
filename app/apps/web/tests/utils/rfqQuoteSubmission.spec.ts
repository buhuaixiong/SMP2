import { describe, expect, it } from "vitest";

import {
  buildRfqQuoteSubmissionUrl,
  parseRfqQuoteSubmissionError,
} from "@/utils/rfqQuoteSubmission";

describe("rfq quote submission helpers", () => {
  it("builds create quote url under deployed base path", () => {
    expect(buildRfqQuoteSubmissionUrl("/smpBackend/api", 18)).toBe(
      "/smpBackend/api/rfq-workflow/18/quotes",
    );
  });

  it("builds update quote url under deployed base path", () => {
    expect(buildRfqQuoteSubmissionUrl("/smpBackend/api/", 18, 7)).toBe(
      "/smpBackend/api/rfq-workflow/18/quotes/7",
    );
  });

  it("returns server json message when response is json", async () => {
    const response = {
      headers: {
        get: (name: string) => (name.toLowerCase() === "content-type" ? "application/json" : null),
      },
      json: async () => ({ message: "RFQ is not accepting quotes." }),
      text: async () => "",
    };

    await expect(parseRfqQuoteSubmissionError(response)).resolves.toBe(
      "RFQ is not accepting quotes.",
    );
  });

  it("falls back to generic message when response is html", async () => {
    const response = {
      headers: {
        get: (name: string) => (name.toLowerCase() === "content-type" ? "text/html" : null),
      },
      json: async () => {
        throw new Error("Unexpected token '<'");
      },
      text: async () => "<!DOCTYPE html><html><body>404</body></html>",
    };

    await expect(parseRfqQuoteSubmissionError(response)).resolves.toBe("Failed to submit quote");
  });
});
