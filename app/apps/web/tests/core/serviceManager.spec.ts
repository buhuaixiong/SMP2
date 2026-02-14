import { beforeEach, describe, expect, it, vi } from "vitest";
import { RegistryManager } from "@/core/registry";
import { ServiceManager } from "@/core/services";
import type { ServiceDefinition } from "@/core/services";

describe("ServiceManager", () => {
  let registry: RegistryManager;
  let manager: ServiceManager;

  beforeEach(() => {
    registry = new RegistryManager();
    manager = new ServiceManager({ registry, logger: { log: () => {}, error: () => {} } });
  });

  const register = (definition: ServiceDefinition, sequence = 100) => {
    registry.category<ServiceDefinition>("services").add(definition.name, definition, { sequence });
  };

  it("starts services with dependency resolution", async () => {
    const order: string[] = [];

    register(
      {
        name: "notification",
        setup: () => {
          order.push("notification");
          return { notify: vi.fn() };
        },
      },
      10,
    );

    register(
      {
        name: "http",
        dependencies: ["notification"],
        setup: ({ get }) => {
          const notification = get<{ notify: () => void }>("notification");
          expect(notification).toBeDefined();
          order.push("http");
          return { get: vi.fn(), post: vi.fn() };
        },
      },
      20,
    );

    await manager.startAll();

    expect(order).toEqual(["notification", "http"]);
    expect(manager.get("http")).toBeDefined();
    expect(manager.isStarted("notification")).toBe(true);
  });

  it("detects circular dependencies", async () => {
    register({
      name: "alpha",
      dependencies: ["beta"],
      setup: () => ({}),
    });

    register({
      name: "beta",
      dependencies: ["alpha"],
      setup: () => ({}),
    });

    await expect(manager.start("alpha")).rejects.toThrow(/Circular dependency/);
  });

  it("collects errors during startAll without throwing", async () => {
    register({
      name: "unstable",
      setup: () => {
        throw new Error("boom");
      },
    });

    await expect(manager.startAll()).resolves.toBeUndefined();
    expect(manager.getLastErrors()).toHaveLength(1);
    expect(manager.getStatus("unstable")).toBe("failed");
  });

  it("invokes teardown when stopping services", async () => {
    const teardown = vi.fn();

    register({
      name: "cache",
      setup: () => ({ entries: new Map() }),
      teardown,
    });

    await manager.startAll();
    await manager.stopAll();

    expect(teardown).toHaveBeenCalled();
    expect(() => manager.get("cache")).toThrowError(/has not been started/);
  });
});
