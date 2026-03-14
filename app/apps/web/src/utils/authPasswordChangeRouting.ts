import {
  getForcedPasswordChangeRedirect,
  shouldForcePasswordChange,
} from "@/utils/authPasswordChange";

type PasswordChangeRouteUser = {
  role?: string | null;
  mustChangePassword?: boolean | null;
};

type PasswordChangeNavigationContext = {
  toName?: string | null;
  toFullPath: string;
  isAuthenticated: boolean;
  user?: PasswordChangeRouteUser | null;
  roleHomeRouteName: string;
};

export const resolveForcedPasswordChangeNavigation = ({
  toName,
  toFullPath,
  isAuthenticated,
  user,
  roleHomeRouteName,
}: PasswordChangeNavigationContext) => {
  const requiresPasswordChange = shouldForcePasswordChange(user);

  if (toName === "login" && isAuthenticated) {
    return requiresPasswordChange ? getForcedPasswordChangeRedirect() : { name: roleHomeRouteName };
  }

  if (requiresPasswordChange && toName !== "change-password-required") {
    return getForcedPasswordChangeRedirect(toFullPath);
  }

  if (toName === "change-password-required" && !requiresPasswordChange && isAuthenticated) {
    return { name: roleHomeRouteName };
  }

  return null;
};

export const getPostLoginNavigation = (user?: PasswordChangeRouteUser | null, redirect = "/") =>
  shouldForcePasswordChange(user) ? getForcedPasswordChangeRedirect(redirect) : redirect;

export const getPostAutoLoginNavigation = (
  user?: PasswordChangeRouteUser | null,
  redirectPath?: string | null,
  fallbackPath = "/dashboard",
) => {
  const targetPath = redirectPath || fallbackPath;
  return shouldForcePasswordChange(user) ? getForcedPasswordChangeRedirect(targetPath) : targetPath;
};
