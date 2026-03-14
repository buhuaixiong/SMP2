import { describe, expect, it } from "vitest";

import {
  getForcedPasswordChangeRedirect,
  isSupplierRoleSubjectToPasswordChange,
  shouldForcePasswordChange,
} from "@/utils/authPasswordChange";

describe("authPasswordChange", () => {
  it("recognizes supplier roles subject to forced password change", () => {
    expect(isSupplierRoleSubjectToPasswordChange("temp_supplier")).toBe(true);
    expect(isSupplierRoleSubjectToPasswordChange("formal_supplier")).toBe(true);
    expect(isSupplierRoleSubjectToPasswordChange("supplier")).toBe(true);
    expect(isSupplierRoleSubjectToPasswordChange("tracking")).toBe(false);
    expect(isSupplierRoleSubjectToPasswordChange("purchaser")).toBe(false);
  });

  it("forces password change only for flagged supplier users", () => {
    expect(shouldForcePasswordChange({ role: "supplier", mustChangePassword: true })).toBe(true);
    expect(shouldForcePasswordChange({ role: "supplier", mustChangePassword: false })).toBe(false);
    expect(shouldForcePasswordChange({ role: "tracking", mustChangePassword: true })).toBe(false);
    expect(shouldForcePasswordChange(null)).toBe(false);
  });

  it("preserves login redirect target when forcing password change", () => {
    expect(getForcedPasswordChangeRedirect("/rfq/10066")).toEqual({
      name: "change-password-required",
      query: { redirect: "/rfq/10066" },
    });
    expect(getForcedPasswordChangeRedirect()).toEqual({
      name: "change-password-required",
      query: {},
    });
  });
});
