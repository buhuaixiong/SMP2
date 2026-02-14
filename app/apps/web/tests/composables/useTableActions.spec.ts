/// <reference types="vitest/globals" />
import { describe, expect, it, vi, beforeEach, type MockedFunction } from "vitest";

vi.mock("@/composables/useNotification", () => ({
  useNotification: vi.fn(),
}));

import { useNotification } from "@/composables/useNotification";
import { useTableActions } from "@/composables/useTableActions";
import type { NotificationService } from "@/services/notification";
import type { MockNotificationService } from "@/vitest.d";

describe("useTableActions", () => {
  const notification: MockNotificationService = {
    warning: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
  };
  const useNotificationMock = useNotification as unknown as MockedFunction<typeof useNotification>;

  beforeEach(() => {
    useNotificationMock.mockReturnValue(notification as unknown as NotificationService);
    notification.warning.mockReset();
    notification.success.mockReset();
    notification.error.mockReset();
  });

  it("executes actions with selected rows", async () => {
    const actions = useTableActions<{ id: number }>();
    actions.setSelection([{ id: 1 }]);
    await actions.runAction(async (rows) => {
      expect(rows).toHaveLength(1);
    }, {
      requireSelection: true,
      successMessage: "done",
    });
    expect(notification.success).toHaveBeenCalledWith("done");
  });

  it("warns when required selection is missing", async () => {
    const actions = useTableActions<{ id: number }>();
    await actions.runAction(async () => undefined, {
      requireSelection: true,
      emptySelectionMessage: "none",
    });
    expect(notification.warning).toHaveBeenCalledWith("none");
  });
});
