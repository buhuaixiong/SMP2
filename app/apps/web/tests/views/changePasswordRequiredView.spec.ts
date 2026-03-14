import { describe, expect, it } from "vitest";

import { getChangePasswordRequiredSuccessRedirect } from "@/views/changePasswordRequiredView";

describe("changePasswordRequiredView", () => {
  it("returns the requested redirect path after successful password change", () => {
    expect(getChangePasswordRequiredSuccessRedirect("/rfq/10066")).toBe("/rfq/10066");
  });

  it("falls back to root when no redirect is provided", () => {
    expect(getChangePasswordRequiredSuccessRedirect()).toBe("/");
    expect(getChangePasswordRequiredSuccessRedirect("")).toBe("/");
  });

  it("prevents redirecting back into the required password change page", () => {
    expect(getChangePasswordRequiredSuccessRedirect("/change-password-required")).toBe("/");
  });
});
