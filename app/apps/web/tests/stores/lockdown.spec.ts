import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { useLockdownStore } from "@/stores/lockdown";

const systemApiMocks = vi.hoisted(() => ({
  getLockdownStatus: vi.fn(),
  getFullLockdownStatus: vi.fn(),
  activateLockdown: vi.fn(),
  deactivateLockdown: vi.fn(),
  getLockdownHistory: vi.fn(),
}));

vi.mock("@/api/system", () => systemApiMocks);

describe("lockdown store resilience", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("keeps active state when polling request fails", async () => {
    const store = useLockdownStore();
    store.syncFromLockdownResponse({
      message: "locked",
      lockdown: { isActive: true, announcement: "Maintenance" },
    });

    systemApiMocks.getLockdownStatus.mockRejectedValueOnce(new Error("network error"));

    await store.checkStatus();

    expect(store.isActive).toBe(true);
    expect(store.announcement).toBe("Maintenance");
  });
});

