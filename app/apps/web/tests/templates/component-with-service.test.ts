import { describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { useService } from "@/core/hooks";
import type { CacheService } from "@/services";
import { mountWithServices } from "../utils/testHelpers";

const TestComponent = defineComponent({
  name: "DemoComponent",
  setup() {
    const cache = useService<CacheService>("cache");
    cache.set("foo", "bar");
    return () => h("div", "demo");
  },
});

describe("component using services", () => {
  it("mounts with mocked services", async () => {
    const cacheMock = { set: vi.fn() };
    const mounted = await mountWithServices(TestComponent, {
      services: {
        cache: () => ({
          name: "cache",
          setup: () => cacheMock,
        }),
      },
    });

    expect(cacheMock.set).toHaveBeenCalledWith("foo", "bar");
    mounted.unmount();
  });
});
