import { beforeEach, describe, expect, it, vi } from "vitest";

const apiFetchMock = vi.fn();

vi.mock("@/api/http", () => ({
  apiFetch: apiFetchMock,
  BASE_URL: "/api",
}));

describe("rfq round workflow api", () => {
  beforeEach(() => {
    apiFetchMock.mockReset();
  });

  it("loads comparison print data with latest scope by default", async () => {
    apiFetchMock.mockResolvedValue({ data: { rfqId: 1, scope: "latest" } });

    const { fetchRfqComparisonPrintData } = await import("@/api/rfq");
    await fetchRfqComparisonPrintData(18);

    expect(apiFetchMock).toHaveBeenCalledWith("/rfq-workflow/18/comparison-print-data", {
      params: { scope: "latest" },
    });
  });

  it("loads comparison print data with all-round scope when requested", async () => {
    apiFetchMock.mockResolvedValue({ data: { rfqId: 1, scope: "all" } });

    const { fetchRfqComparisonPrintData } = await import("@/api/rfq");
    await fetchRfqComparisonPrintData(18, "all");

    expect(apiFetchMock).toHaveBeenCalledWith("/rfq-workflow/18/comparison-print-data", {
      params: { scope: "all" },
    });
  });

  it("posts extend deadline payload to the round action endpoint", async () => {
    apiFetchMock.mockResolvedValue({ data: { id: 9, roundNumber: 2 } });

    const { extendRfqBidDeadline } = await import("@/api/rfq");
    await extendRfqBidDeadline(44, {
      newDeadline: "2026-03-20T10:00:00Z",
      reason: "Need more supplier responses",
    });

    expect(apiFetchMock).toHaveBeenCalledWith("/rfq-workflow/44/extend-bid-deadline", {
      method: "POST",
      body: {
        newDeadline: "2026-03-20T10:00:00Z",
        reason: "Need more supplier responses",
      },
    });
  });

  it("posts next round payload with selected suppliers", async () => {
    apiFetchMock.mockResolvedValue({ data: { id: 10, roundNumber: 3 } });

    const { startNextRfqBidRound } = await import("@/api/rfq");
    await startNextRfqBidRound(44, {
      deadline: "2026-03-25T10:00:00Z",
      reason: "Rebid shortlisted suppliers",
      supplierIds: [1001, 1002],
    });

    expect(apiFetchMock).toHaveBeenCalledWith("/rfq-workflow/44/start-next-round", {
      method: "POST",
      body: {
        deadline: "2026-03-25T10:00:00Z",
        reason: "Rebid shortlisted suppliers",
        supplierIds: [1001, 1002],
      },
    });
  });
});
