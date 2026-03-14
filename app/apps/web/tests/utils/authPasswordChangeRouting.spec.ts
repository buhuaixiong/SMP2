import { describe, expect, it } from "vitest";

import {
  getPostAutoLoginNavigation,
  getPostLoginNavigation,
  resolveForcedPasswordChangeNavigation,
} from "@/utils/authPasswordChangeRouting";

describe("authPasswordChangeRouting", () => {
  it("redirects flagged suppliers away from normal protected routes", () => {
    expect(
      resolveForcedPasswordChangeNavigation({
        toName: "dashboard",
        toFullPath: "/dashboard",
        isAuthenticated: true,
        user: { role: "supplier", mustChangePassword: true },
        roleHomeRouteName: "dashboard",
      }),
    ).toEqual({
      name: "change-password-required",
      query: { redirect: "/dashboard" },
    });
  });

  it("allows flagged suppliers to stay on the required change-password route", () => {
    expect(
      resolveForcedPasswordChangeNavigation({
        toName: "change-password-required",
        toFullPath: "/change-password-required",
        isAuthenticated: true,
        user: { role: "supplier", mustChangePassword: true },
        roleHomeRouteName: "dashboard",
      }),
    ).toBeNull();
  });

  it("redirects non-flagged authenticated users away from the required change-password route", () => {
    expect(
      resolveForcedPasswordChangeNavigation({
        toName: "change-password-required",
        toFullPath: "/change-password-required",
        isAuthenticated: true,
        user: { role: "supplier", mustChangePassword: false },
        roleHomeRouteName: "dashboard",
      }),
    ).toEqual({ name: "dashboard" });
  });

  it("redirects flagged suppliers away from the login page after authentication", () => {
    expect(
      resolveForcedPasswordChangeNavigation({
        toName: "login",
        toFullPath: "/login",
        isAuthenticated: true,
        user: { role: "supplier", mustChangePassword: true },
        roleHomeRouteName: "dashboard",
      }),
    ).toEqual({
      name: "change-password-required",
      query: {},
    });
  });

  it("returns normal login redirect for users who do not need a password change", () => {
    expect(
      getPostLoginNavigation({ role: "purchaser", mustChangePassword: false }, "/rfq/10066"),
    ).toBe("/rfq/10066");
  });

  it("returns forced change-password redirect after login for flagged suppliers", () => {
    expect(
      getPostLoginNavigation({ role: "formal_supplier", mustChangePassword: true }, "/rfq/10066"),
    ).toEqual({
      name: "change-password-required",
      query: { redirect: "/rfq/10066" },
    });
  });

  it("returns forced change-password redirect after auto-login for flagged suppliers", () => {
    expect(
      getPostAutoLoginNavigation(
        { role: "temp_supplier", mustChangePassword: true },
        "/rfq/10066",
        "/dashboard",
      ),
    ).toEqual({
      name: "change-password-required",
      query: { redirect: "/rfq/10066" },
    });
  });
});
