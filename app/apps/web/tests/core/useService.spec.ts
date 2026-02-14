/// <reference types="vitest/globals" />
import { describe, expect, it, vi, beforeEach, type MockedFunction } from "vitest";

vi.mock("@/core/services", () => {
  return {
    useServiceManager: vi.fn(),
  };
});

import { useService, useServices } from "@/core/hooks/useService";
import { useServiceManager } from "@/core/services";
import type { ServiceManager } from "@/core/services";
import type { MockServiceManager } from "@/vitest.d";

describe("useService", () => {
  const manager: MockServiceManager = {
    has: vi.fn(),
    isStarted: vi.fn(),
    get: vi.fn(),
  };
  const useServiceManagerMock = useServiceManager as unknown as MockedFunction<
    typeof useServiceManager
  >;

  beforeEach(() => {
    manager.has.mockReset();
    manager.isStarted.mockReset();
    manager.get.mockReset();
    useServiceManagerMock.mockReturnValue(manager as unknown as ServiceManager);
  });

  it("returns service instance when registered and started", () => {
    manager.has.mockReturnValue(true);
    manager.isStarted.mockReturnValue(true);
    manager.get.mockReturnValue({ ping: "pong" });

    const service = useService<{ ping: string }>("notification");
    expect(service.ping).toBe("pong");
  });

  it("throws when service not registered", () => {
    manager.has.mockReturnValue(false);
    expect(() => useService("missing")).toThrow(/not registered/);
  });

  it("throws when service not started", () => {
    manager.has.mockReturnValue(true);
    manager.isStarted.mockReturnValue(false);
    expect(() => useService("http")).toThrow(/has not been started/);
  });

  it("returns multiple services as record", () => {
    manager.has.mockReturnValue(true);
    manager.get.mockImplementation((name: string) => name.toUpperCase());
    const services = useServices(["a", "b"] as const);
    expect(services).toEqual({ a: "A", b: "B" });
  });
});
