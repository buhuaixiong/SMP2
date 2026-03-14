const REQUIRED_PASSWORD_CHANGE_ROUTE = "/change-password-required";

export const getChangePasswordRequiredSuccessRedirect = (redirect?: string | null): string => {
  if (!redirect || redirect.trim().length === 0) {
    return "/";
  }

  const normalized = redirect.trim();
  return normalized === REQUIRED_PASSWORD_CHANGE_ROUTE ? "/" : normalized;
};
