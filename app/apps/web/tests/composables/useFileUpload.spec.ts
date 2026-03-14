/// <reference types="vitest/globals" />
import { describe, expect, it, vi, beforeEach, type MockedFunction } from "vitest";

vi.mock("@/core/hooks", () => ({
  useService: vi.fn(),
}));
vi.mock("@/composables/useNotification", () => ({
  useNotification: vi.fn(),
}));

import { useService } from "@/core/hooks";
import { useNotification } from "@/composables/useNotification";
import { useFileUpload } from "@/composables/useFileUpload";
import type { NotificationService } from "@/services/notification";
import type { MockNotificationService } from "@/vitest.d";

describe("useFileUpload", () => {
  const useServiceMock = useService as unknown as MockedFunction<typeof useService>;
  const useNotificationMock = useNotification as unknown as MockedFunction<typeof useNotification>;
  const http = { request: vi.fn() };
  const notification: MockNotificationService = { success: vi.fn(), error: vi.fn(), warning: vi.fn() };

  beforeEach(() => {
    useServiceMock.mockReset();
    http.request.mockReset();
    notification.success.mockReset();
    notification.error.mockReset();
    useServiceMock.mockReturnValue(http);
    useNotificationMock.mockReturnValue(notification as unknown as NotificationService);
  });

  it("uploads file via custom request hook", async () => {
    const uploader = useFileUpload();
    const file = new File(["demo"], "demo.txt", { type: "text/plain" });
    const request = vi.fn(async () => ({ ok: true }));
    const result = await uploader.upload(file, { request });
    expect(result).toEqual({ ok: true });
    expect(request).toHaveBeenCalled();
  });

  it("reports errors", async () => {
    const uploader = useFileUpload();
    const file = new File(["demo"], "demo.txt", { type: "text/plain" });
    const request = vi.fn(async () => {
      throw new Error("upload failed");
    });
    await expect(uploader.upload(file, { request, errorMessage: "failed" })).rejects.toThrow(
      "upload failed",
    );
    expect(notification.error).toHaveBeenCalledWith("failed");
  });
});
