/// <reference types="vitest/globals" />
import { describe, expect, it, vi, beforeEach, type MockedFunction } from "vitest";

vi.mock("@/core/hooks", () => ({
  useService: vi.fn(),
}));

import { useNotification } from "@/composables/useNotification";
import { useService } from "@/core/hooks";

describe("useNotification", () => {
  const useServiceMock = useService as unknown as MockedFunction<typeof useService>;

  beforeEach(() => {
    useServiceMock.mockReset();
  });

  it("returns notification service from registry", () => {
    const service = { success: vi.fn() };
    useServiceMock.mockReturnValue(service);
    const notification = useNotification();
    expect(notification).toBe(service);
    expect(useServiceMock).toHaveBeenCalledWith("notification");
  });
});
