/// <reference types="vitest/globals" />
import { describe, expect, it, vi, beforeEach, type MockedFunction } from "vitest";

vi.mock("@/core/hooks", () => ({
  useService: vi.fn(),
}));

import { useService } from "@/core/hooks";
import { usePermission } from "@/composables/usePermission";

describe("usePermission", () => {
  const useServiceMock = useService as unknown as MockedFunction<typeof useService>;
  const permissionImpl = {
    getRole: () => "admin",
    getPermissions: () => ["rfq:edit", "rfq:view"],
    refresh: vi.fn().mockResolvedValue(["rfq:edit", "rfq:view"]),
    has: vi.fn((value: string) => value === "rfq:edit"),
    hasAny: vi.fn((...values: string[]) => values.includes("rfq:view")),
    hasAll: vi.fn((...values: string[]) => values.every((v) => v === "rfq:view")),
    hasRole: vi.fn((role: string) => role === "admin"),
  };

  beforeEach(() => {
    useServiceMock.mockReset();
    permissionImpl.refresh.mockClear();
    useServiceMock.mockReturnValue(permissionImpl);
  });

  it("mirrors permission service state", async () => {
    const permission = usePermission();
    expect(permission.role.value).toBe("admin");
    expect(permission.permissions.value).toEqual(["rfq:edit", "rfq:view"]);
    expect(permission.has("rfq:edit")).toBe(true);
    expect(permission.hasAny("rfq:view")).toBe(true);
    expect(permission.hasAll("rfq:view")).toBe(true);
    expect(permission.hasRole("admin")).toBe(true);

    await permission.refresh(true);
    expect(permissionImpl.refresh).toHaveBeenCalledWith(true);
  });
});
