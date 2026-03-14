/// <reference types="vitest/globals" />
import { beforeEach, describe, expect, it, vi, type MockedFunction } from "vitest";

vi.mock("@/core/hooks", () => ({
  useService: vi.fn(),
}));
vi.mock("@/composables/useNotification", () => ({
  useNotification: vi.fn(),
}));

import { useService } from "@/core/hooks";
import { useNotification } from "@/composables/useNotification";
import { useApprovalWorkflow } from "@/composables/useApprovalWorkflow";
import type { NotificationService } from "@/services/notification";
import type { MockNotificationService } from "@/vitest.d";

describe("useApprovalWorkflow", () => {
  const useServiceMock = useService as unknown as MockedFunction<typeof useService>;
  const useNotificationMock = useNotification as unknown as MockedFunction<typeof useNotification>;

  const http = { post: vi.fn(), get: vi.fn() };
  const audit = { logUpdate: vi.fn() };
  const notification: MockNotificationService = {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
  };

  beforeEach(() => {
    useServiceMock.mockReset();
    useNotificationMock.mockReset();
    http.post.mockReset();
    http.get.mockReset();
    audit.logUpdate.mockReset();
    notification.success.mockReset();
    notification.error.mockReset();
    notification.warning.mockReset();

    useServiceMock.mockImplementation((name: string) => {
      if (name === "http") return http;
      if (name === "audit") return audit;
      throw new Error(`Unknown service ${name}`);
    });
    useNotificationMock.mockReturnValue(notification as unknown as NotificationService);
  });

  it("approves via http service and logs audit entry", async () => {
    http.post.mockResolvedValue({});
    const workflow = useApprovalWorkflow("rfq");
    await workflow.approve(10, 20, { comments: "ok" });
    expect(http.post).toHaveBeenCalledWith(
      "/api/rfq/10/approvals/20/approve",
      { comments: "ok" },
      { silent: true },
    );
    expect(audit.logUpdate).toHaveBeenCalledWith("rfq", {
      entityId: 10,
      action: "approve",
      approvalId: 20,
      payload: { comments: "ok" },
    });
  });

  it("warns when inviting purchasers without selection", async () => {
    const workflow = useApprovalWorkflow("rfq");
    await workflow.invitePurchasers(1, 2, { purchaserIds: [] });
    expect(notification.warning).toHaveBeenCalled();
    expect(http.post).not.toHaveBeenCalledWith("/api/rfq/1/approvals/2/invite-purchasers", expect.anything(), expect.anything());
  });

  it("fetches workflow history", async () => {
    http.get.mockResolvedValue([{ id: 1 }]);
    const workflow = useApprovalWorkflow("rfq");
    const history = await workflow.fetchHistory(42);
    expect(http.get).toHaveBeenCalledWith("/api/rfq/42/approvals");
    expect(history).toEqual([{ id: 1 }]);
  });
});
