import { describe, expect, it, vi, beforeEach } from "vitest";
import { cacheService } from "@/services/cache";

describe("cacheService", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  it("stores and retrieves values with ttl", async () => {
    const service = await cacheService.setup({} as any);
    service.set("foo", 1, 1000);
    expect(service.get("foo")).toBe(1);
    vi.advanceTimersByTime(1500);
    expect(service.get("foo")).toBeUndefined();
  });

  it("getOrSet resolves factory result", async () => {
    const service = await cacheService.setup({} as any);
    const value = await service.getOrSet("bar", async () => 42);
    expect(value).toBe(42);
    const cached = await service.getOrSet("bar", async () => 10);
    expect(cached).toBe(42);
  });
});
