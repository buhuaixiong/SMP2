import { describe, expect, it, vi, beforeEach } from "vitest";

const authStore = {
  user: {
    role: "admin",
    permissions: ["a", "b"],
  },
};

vi.mock("@/stores/auth", () => ({
  useAuthStore: () => authStore,
}));

const { apiFetchSpy } = vi.hoisted(() => ({
  apiFetchSpy: vi.fn(),
}));

vi.mock("@/api/http", () => ({
  apiFetch: apiFetchSpy,
}));

import { permissionService } from "@/services/permission";

describe("permissionService", () => {
  beforeEach(() => {
    apiFetchSpy.mockReset();
    authStore.user = {
      role: "admin",
      permissions: ["a", "b"],
    };
  });

  it("checks permissions from auth store", async () => {
    const service = await permissionService.setup({} as any);
    expect(service.has("a")).toBe(true);
    expect(service.hasAny("x", "b")).toBe(true);
    expect(service.hasAll("a", "b")).toBe(true);
    expect(service.hasRole("admin")).toBe(true);
  });

  it("refreshes permissions via /auth/me", async () => {
    apiFetchSpy.mockResolvedValue({ user: { permissions: ["x"], role: "manager" } });
    const service = await permissionService.setup({} as any);
    await service.refresh(true);
    expect(service.getPermissions()).toEqual(["x"]);
    expect(service.getRole()).toBe("manager");
  });
});
