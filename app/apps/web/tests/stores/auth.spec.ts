import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";

const { apiFetchSpy } = vi.hoisted(() => ({
  apiFetchSpy: vi.fn(),
}));

vi.mock("@/api/http", () => ({
  apiFetch: apiFetchSpy,
}));

import { useAuthStore } from "@/stores/auth";

describe("auth store", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    apiFetchSpy.mockReset();
    localStorage.clear();
  });

  it("refreshes permissions from /auth/me api envelope", async () => {
    const store = useAuthStore();
    store.token = "token";
    store.user = {
      id: "u-1",
      name: "Buyer",
      role: "purchaser",
      supplierId: null,
      tempAccountId: null,
      relatedApplicationId: null,
      accountType: null,
      mustChangePassword: false,
      permissions: [],
      orgUnits: [],
      adminUnits: [],
      purchasingGroups: [],
      isPurchasingGroupLeader: false,
      isOrgUnitAdmin: false,
      functions: [],
    };

    apiFetchSpy.mockResolvedValue({
      success: true,
      data: {
        user: {
          id: "u-1",
          name: "Buyer",
          role: "purchaser",
          mustChangePassword: true,
          permissions: ["item_master.view.own"],
        },
      },
    });

    await store.fetchMe();

    expect(store.user?.permissions).toEqual(["item_master.view.own"]);
    expect(store.user?.mustChangePassword).toBe(true);
  });
});
