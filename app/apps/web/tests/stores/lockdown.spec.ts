import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
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
    vi.useFakeTimers();
    vi.clearAllMocks();
    Object.defineProperty(document, "hidden", {
      configurable: true,
      get: () => false,
    });
  });

  afterEach(() => {
    vi.useRealTimers();
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

  it("skips polling requests while document is hidden", async () => {
    const store = useLockdownStore();
    systemApiMocks.getLockdownStatus.mockResolvedValue({
      isActive: false,
      announcement: null,
      activatedAt: null,
    });

    store.startPolling();
    await Promise.resolve();
    expect(systemApiMocks.getLockdownStatus).toHaveBeenCalledTimes(1);

    Object.defineProperty(document, "hidden", {
      configurable: true,
      get: () => true,
    });

    await vi.advanceTimersByTimeAsync(10000);
    expect(systemApiMocks.getLockdownStatus).toHaveBeenCalledTimes(1);

    store.stopPolling();
  });

  it("refreshes immediately when tab becomes visible again", async () => {
    const store = useLockdownStore();
    systemApiMocks.getLockdownStatus.mockResolvedValue({
      isActive: false,
      announcement: null,
      activatedAt: null,
    });

    Object.defineProperty(document, "hidden", {
      configurable: true,
      get: () => true,
    });

    store.startPolling();
    await Promise.resolve();
    expect(systemApiMocks.getLockdownStatus).not.toHaveBeenCalled();

    Object.defineProperty(document, "hidden", {
      configurable: true,
      get: () => false,
    });
    document.dispatchEvent(new Event("visibilitychange"));
    await Promise.resolve();

    expect(systemApiMocks.getLockdownStatus).toHaveBeenCalledTimes(1);
    store.stopPolling();
  });
});
