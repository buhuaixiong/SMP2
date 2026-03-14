import type { FormRules } from "element-plus";

type Translate = (key: string) => string;

export const validateNewPasswordValue = (
  currentPassword: string,
  newPassword: string,
  t: Translate,
): string | null => {
  if (!newPassword.trim()) {
    return t("auth.changePassword.validation.newPasswordRequired");
  }

  if (newPassword.length < 8) {
    return t("auth.changePassword.validation.passwordMinLength");
  }

  if (currentPassword && newPassword === currentPassword) {
    return t("auth.changePassword.validation.passwordSameAsCurrent");
  }

  return null;
};

export const validateConfirmPasswordValue = (
  newPassword: string,
  confirmPassword: string,
  t: Translate,
): string | null => {
  if (!confirmPassword.trim()) {
    return t("auth.changePassword.validation.confirmPasswordRequired");
  }

  if (confirmPassword !== newPassword) {
    return t("auth.changePassword.validation.passwordMismatch");
  }

  return null;
};

export const getChangePasswordServerErrors = (
  message: string,
  t: Translate,
): Partial<Record<"currentPassword" | "newPassword", string>> => {
  switch (message) {
    case "Current password is incorrect.":
      return { currentPassword: t("auth.changePassword.validation.currentPasswordIncorrect") };
    case "New password must be at least 8 characters.":
      return { newPassword: t("auth.changePassword.validation.passwordMinLength") };
    case "New password must be different from the current password.":
      return { newPassword: t("auth.changePassword.validation.passwordSameAsCurrent") };
    default:
      return {};
  }
};

export const createChangePasswordRules = (
  form: { currentPassword: string; newPassword: string; confirmPassword: string },
  t: Translate,
): FormRules => ({
  currentPassword: [
    {
      required: true,
      message: t("auth.changePassword.validation.currentPasswordRequired"),
      trigger: "blur",
    },
  ],
  newPassword: [
    {
      validator: (_rule, value, callback) => {
        const message = validateNewPasswordValue(
          form.currentPassword,
          typeof value === "string" ? value : "",
          t,
        );
        callback(message ? new Error(message) : undefined);
      },
      trigger: ["blur", "change"],
    },
  ],
  confirmPassword: [
    {
      validator: (_rule, value, callback) => {
        const message = validateConfirmPasswordValue(
          form.newPassword,
          typeof value === "string" ? value : "",
          t,
        );
        callback(message ? new Error(message) : undefined);
      },
      trigger: ["blur", "change"],
    },
  ],
});
