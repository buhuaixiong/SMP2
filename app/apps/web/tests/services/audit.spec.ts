import { beforeEach, describe, expect, it, vi } from "vitest";
import { auditService } from "@/services/audit";

describe("auditService", () => {
  const http = {
    post: vi.fn(),
  };
  const notification = {
    warning: vi.fn(),
  };
  const context = {
    get: vi.fn((name: string) => {
      if (name === "http") return http;
      if (name === "notification") return notification;
      return null;
    }),
  };

  beforeEach(() => {
    vi.useFakeTimers();
    http.post.mockReset();
    notification.warning.mockReset();
    (navigator as any).sendBeacon = vi.fn().mockReturnValue(true);
  });

  it("flushes queued events", async () => {
    const service = await auditService.setup(context as any);
    service.log({ entity: "supplier", action: "view" });
    await service.flush({ force: true });
    expect(http.post).toHaveBeenCalledWith("/audit/logs", expect.anything(), { silent: true });
  });
});
