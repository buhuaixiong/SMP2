type PasswordChangeUserLike = {
  role?: string | null;
  mustChangePassword?: boolean | null;
};

const SUPPLIER_ROLES_REQUIRING_PASSWORD_CHANGE = new Set([
  "temp_supplier",
  "formal_supplier",
  "supplier",
]);

export const isSupplierRoleSubjectToPasswordChange = (role?: string | null): boolean =>
  SUPPLIER_ROLES_REQUIRING_PASSWORD_CHANGE.has(
    String(role ?? "")
      .trim()
      .toLowerCase(),
  );

export const shouldForcePasswordChange = (user?: PasswordChangeUserLike | null): boolean =>
  Boolean(user?.mustChangePassword) && isSupplierRoleSubjectToPasswordChange(user?.role);

export const getForcedPasswordChangeRedirect = (redirect?: string | null) => ({
  name: "change-password-required",
  query: redirect ? { redirect } : {},
});
