import { describe, expect, it, vi } from "vitest";
import { Registry, RegistryManager } from "@/core/registry";

describe("Registry", () => {
  it("adds, retrieves, and removes entries", () => {
    const registry = new Registry<number>();

    registry.add("alpha", 1).add("beta", 2);
    expect(registry.get("alpha")).toBe(1);
    expect(registry.get("beta")).toBe(2);
    expect(registry.get("gamma", 42)).toBe(42);

    expect(registry.has("alpha")).toBe(true);
    expect(registry.keys()).toEqual(["alpha", "beta"]);

    expect(registry.remove("alpha")).toBe(true);
    expect(registry.has("alpha")).toBe(false);
  });

  it("preserves order using sequence information", () => {
    const registry = new Registry<number>();
    registry.add("first", 1, { sequence: 20 });
    registry.add("second", 2, { sequence: 10 });
    registry.add("third", 3, { sequence: 10 }); // fallback to insertion order

    expect(registry.keys()).toEqual(["second", "third", "first"]);
    expect(registry.getAll()).toEqual([2, 3, 1]);
  });

  it("emits change events and allows unsubscribing", () => {
    const registry = new Registry<number>();
    const listener = vi.fn();
    const unsubscribe = registry.on(listener);

    registry.add("x", 99);
    registry.remove("x");
    unsubscribe();
    registry.add("y", 1);

    expect(listener).toHaveBeenCalledTimes(2);
    expect(listener.mock.calls[0][0].type).toBe("add");
    expect(listener.mock.calls[1][0].type).toBe("remove");
  });

  it("clears all listeners when clear is invoked", () => {
    const registry = new Registry<number>();
    registry.add("x", 1);
    registry.add("y", 2);
    const listener = vi.fn();
    registry.on(listener);

    registry.clear();
    expect(listener).toHaveBeenCalled();
    expect(registry.keys()).toEqual([]);
  });
});

describe("RegistryManager", () => {
  it("creates and caches category registries", () => {
    const manager = new RegistryManager();
    const services = manager.category<string>("services");
    services.add("one", "alpha");

    const sameReference = manager.category<string>("services");
    expect(sameReference.get("one")).toBe("alpha");
    expect(manager.hasCategory("services")).toBe(true);
    expect(manager.removeCategory("services")).toBe(true);
  });
});
