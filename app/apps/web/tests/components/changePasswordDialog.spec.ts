import { describe, expect, it } from "vitest";

import {
  getChangePasswordServerErrors,
  validateConfirmPasswordValue,
  validateNewPasswordValue,
} from "@/components/changePasswordDialog";

const t = (key: string) => key;

describe("changePasswordDialog", () => {
  it("rejects a new password shorter than 8 characters", () => {
    expect(validateNewPasswordValue("oldpassword", "short", t)).toBe(
      "auth.changePassword.validation.passwordMinLength",
    );
  });

  it("rejects a new password that matches the current password", () => {
    expect(validateNewPasswordValue("same-password", "same-password", t)).toBe(
      "auth.changePassword.validation.passwordSameAsCurrent",
    );
  });

  it("rejects a mismatched confirmation password", () => {
    expect(validateConfirmPasswordValue("new-password-123", "new-password-456", t)).toBe(
      "auth.changePassword.validation.passwordMismatch",
    );
  });

  it("maps backend current-password errors to the currentPassword field", () => {
    expect(getChangePasswordServerErrors("Current password is incorrect.", t)).toEqual({
      currentPassword: "auth.changePassword.validation.currentPasswordIncorrect",
    });
  });

  it("maps backend short-password errors to the newPassword field", () => {
    expect(getChangePasswordServerErrors("New password must be at least 8 characters.", t)).toEqual(
      {
        newPassword: "auth.changePassword.validation.passwordMinLength",
      },
    );
  });

  it("maps backend same-password errors to the newPassword field", () => {
    expect(
      getChangePasswordServerErrors("New password must be different from the current password.", t),
    ).toEqual({
      newPassword: "auth.changePassword.validation.passwordSameAsCurrent",
    });
  });
});
