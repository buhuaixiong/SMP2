import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useVisibilityAwarePolling } from "@/composables/useVisibilityAwarePolling";

describe("useVisibilityAwarePolling", () => {
  let hidden = false;

  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
    hidden = false;
    Object.defineProperty(document, "hidden", {
      configurable: true,
      get: () => hidden,
    });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("dedupes in-flight polling calls", async () => {
    let resolveFirst: (() => void) | null = null;
    const task = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveFirst = resolve;
        }),
    );

    const polling = useVisibilityAwarePolling({
      task,
      baseIntervalMs: 1000,
      jitterRatio: 0,
    });

    polling.start();
    await Promise.resolve();
    expect(task).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1000);
    expect(task).toHaveBeenCalledTimes(1);

    resolveFirst?.();
    await Promise.resolve();

    await vi.advanceTimersByTimeAsync(1000);
    expect(task).toHaveBeenCalledTimes(2);

    polling.stop();
  });

  it("applies exponential backoff after failures", async () => {
    const task = vi
      .fn<() => Promise<void>>()
      .mockRejectedValueOnce(new Error("network"))
      .mockResolvedValue(undefined);

    const polling = useVisibilityAwarePolling({
      task,
      baseIntervalMs: 1000,
      maxBackoffMultiplier: 4,
      jitterRatio: 0,
    });

    polling.start();
    await Promise.resolve();
    expect(task).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1000);
    expect(task).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1000);
    expect(task).toHaveBeenCalledTimes(2);

    polling.stop();
  });

  it("pauses polling when hidden and refreshes immediately when visible", async () => {
    hidden = true;
    const task = vi.fn<() => Promise<void>>().mockResolvedValue(undefined);

    const polling = useVisibilityAwarePolling({
      task,
      baseIntervalMs: 1000,
      hiddenIntervalMs: 5000,
      jitterRatio: 0,
    });

    polling.start();
    await Promise.resolve();
    expect(task).toHaveBeenCalledTimes(0);

    hidden = false;
    document.dispatchEvent(new Event("visibilitychange"));
    await Promise.resolve();
    expect(task).toHaveBeenCalledTimes(1);

    hidden = true;
    await vi.advanceTimersByTimeAsync(6000);
    expect(task).toHaveBeenCalledTimes(1);

    hidden = false;
    document.dispatchEvent(new Event("visibilitychange"));
    await Promise.resolve();
    expect(task).toHaveBeenCalledTimes(2);

    polling.stop();
  });
});
