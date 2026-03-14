import { describe, expect, it, vi, beforeEach } from "vitest";

const elementPlusMocks = vi.hoisted(() => {
  const notificationSpy = vi.fn();
  const messageSpy = vi.fn();
  const confirmSpy = vi.fn().mockResolvedValue("ok");
  return {
    notificationSpy,
    messageSpy,
    confirmSpy,
    module: {
      ElNotification: notificationSpy,
      ElMessage: messageSpy,
      ElMessageBox: {
        confirm: confirmSpy,
      },
    },
  };
});

vi.mock("element-plus", () => elementPlusMocks.module);

import { notificationService } from "@/services/notification";

describe("notificationService", () => {
  beforeEach(() => {
    elementPlusMocks.notificationSpy.mockClear();
    elementPlusMocks.messageSpy.mockClear();
    elementPlusMocks.confirmSpy.mockClear();
  });

  it("sends typed notifications", async () => {
    const service = await notificationService.setup({} as any);
    service.success("completed");
    service.error("failed", "Error", { sticky: true });
    expect(elementPlusMocks.notificationSpy).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({ type: "success", message: "completed" }),
    );
    expect(elementPlusMocks.notificationSpy).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({ type: "error", duration: 0 }),
    );
  });

  it("shows confirm dialog", async () => {
    const service = await notificationService.setup({} as any);
    await service.confirm("Are you sure?");
    expect(elementPlusMocks.confirmSpy).toHaveBeenCalled();
  });
});
