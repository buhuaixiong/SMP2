import { isApiErrorResponse, isAxiosError } from "@/types/api";

const includesText = (value: string | undefined, expected: string) =>
  value?.toLowerCase().includes(expected.toLowerCase()) ?? false;

export function getLoginErrorMessageKey(error: unknown): string {
  if (isAxiosError(error)) {
    const status = error.response?.status;
    const data = error.response?.data;
    const message = error.message || "";
    const apiCode = isApiErrorResponse(data) ? data.code : undefined;
    const apiMessage =
      typeof data === "object" && data !== null
        ? String(
            (data as Record<string, unknown>).error ??
              (data as Record<string, unknown>).message ??
              "",
          )
        : "";

    if (
      includesText(message, "timeout") ||
      includesText(message, "econnaborted") ||
      includesText(message, "canceled") ||
      includesText(message, "cancelled")
    ) {
      return "auth.loginErrors.timeout";
    }

    if (status === 429 && apiCode === "LOGIN_IN_PROGRESS") {
      return "auth.loginErrors.loginInProgress";
    }

    if (status === 423 || includesText(apiMessage, "locked")) {
      return "auth.loginErrors.accountLocked";
    }

    if (status === 403 && includesText(apiMessage, "frozen")) {
      return "auth.loginErrors.accountFrozen";
    }

    if (status === 403 && includesText(apiMessage, "deleted")) {
      return "auth.loginErrors.accountDeleted";
    }

    if (status === 401) {
      return "auth.loginErrors.invalidCredentials";
    }
  }

  return "auth.loginErrors.genericFailure";
}
