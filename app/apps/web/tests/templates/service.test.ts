import { describe, expect, it, vi } from "vitest";
import type { ServiceDefinition } from "@/core/services";
import { startMockServices } from "../setup/mockServices";

/**
 * 模板：复制到 `apps/web/tests/services/<name>.spec.ts` 并根据需要调整
 */

const mockDefinition: ServiceDefinition = {
  name: "demo",
  dependencies: ["notification"],
  async setup({ get }) {
    const notification = get("notification") as any;
    return {
      async run() {
        notification.success("ok");
        return "ok";
      },
    };
  },
};

describe("demo service", () => {
  it("runs and interacts with dependencies", async () => {
    const notification = { success: vi.fn() };
    const manager = await startMockServices({
      notification: () => ({
        name: "notification",
        setup: () => notification,
      }),
      demo: () => mockDefinition,
    });

    const demo = await manager.start("demo") as any;
    await demo.run();
    expect(notification.success).toHaveBeenCalledWith("ok");
  });
});
