import { describe, expect, it } from "vitest";

import { getLoginErrorMessageKey } from "@/utils/authLoginError";

const createAxiosLikeError = (
  status?: number,
  data?: Record<string, unknown>,
  message = "Request failed",
) => ({
  isAxiosError: true,
  message,
  response: status
    ? {
        status,
        data,
      }
    : undefined,
});

describe("authLoginError", () => {
  it("maps invalid credentials to invalidCredentials key", () => {
    expect(
      getLoginErrorMessageKey(
        createAxiosLikeError(401, {
          success: false,
          error: "Invalid credentials",
          code: "UNAUTHORIZED",
        }),
      ),
    ).toBe("auth.loginErrors.invalidCredentials");
  });

  it("maps locked accounts to accountLocked key", () => {
    expect(
      getLoginErrorMessageKey(
        createAxiosLikeError(423, {
          message:
            "Account has been locked due to too many failed login attempts. Please try again in 30 minutes.",
        }),
      ),
    ).toBe("auth.loginErrors.accountLocked");
  });

  it("maps login in progress responses to loginInProgress key", () => {
    expect(
      getLoginErrorMessageKey(
        createAxiosLikeError(429, {
          success: false,
          error: "Login request is being processed. Please wait a moment.",
          code: "LOGIN_IN_PROGRESS",
        }),
      ),
    ).toBe("auth.loginErrors.loginInProgress");
  });

  it("maps timeout errors to timeout key", () => {
    expect(
      getLoginErrorMessageKey(
        createAxiosLikeError(undefined, undefined, "timeout of 15000ms exceeded"),
      ),
    ).toBe("auth.loginErrors.timeout");
  });

  it("maps ECONNABORTED errors to timeout key", () => {
    expect(
      getLoginErrorMessageKey(createAxiosLikeError(undefined, undefined, "ECONNABORTED")),
    ).toBe("auth.loginErrors.timeout");
  });

  it("maps canceled request errors to timeout key", () => {
    expect(
      getLoginErrorMessageKey(
        createAxiosLikeError(undefined, undefined, "The operation was canceled"),
      ),
    ).toBe("auth.loginErrors.timeout");
  });

  it("maps frozen account errors to accountFrozen key", () => {
    expect(
      getLoginErrorMessageKey(
        createAxiosLikeError(403, {
          message: "Account is frozen. Please contact administrator.",
        }),
      ),
    ).toBe("auth.loginErrors.accountFrozen");
  });

  it("maps deleted account errors to accountDeleted key", () => {
    expect(
      getLoginErrorMessageKey(
        createAxiosLikeError(403, {
          message: "Account has been deleted.",
        }),
      ),
    ).toBe("auth.loginErrors.accountDeleted");
  });

  it("falls back to genericFailure for unknown errors", () => {
    expect(getLoginErrorMessageKey(new Error("something else"))).toBe(
      "auth.loginErrors.genericFailure",
    );
  });
});
