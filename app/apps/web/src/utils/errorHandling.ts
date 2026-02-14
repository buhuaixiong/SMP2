import type { ApiErrorResponse } from "@/types/api";
import { isApiErrorResponse, isAxiosError, isLockdownResponse } from "@/types/api";

/**
 * Extract user-friendly error message.
 * Priority: Lockdown > ApiErrorResponse > AxiosError > Error > Default
 */
export function extractErrorMessage(error: unknown, devMode = import.meta.env.DEV): string {
  if (isAxiosError(error)) {
    const data = error.response?.data;

    if (isLockdownResponse(data)) {
      return data.message || "System is under maintenance. Please try again later.";
    }

    if (isApiErrorResponse(data)) {
      return data.error || data.message || "Request failed";
    }

    return error.message || "Network error. Please check your connection.";
  }

  if (error instanceof Error) {
    if (!devMode && error.message.includes("stack")) {
      return "Operation failed. Please contact an administrator.";
    }
    return error.message;
  }

  return "Unknown error";
}

/**
 * Extract error code (for programmatic handling).
 */
export function getErrorCode(error: unknown): string | undefined {
  if (isAxiosError(error)) {
    const data = error.response?.data;
    if (isApiErrorResponse(data)) {
      return data.code;
    }
  }
  return undefined;
}

/**
 * Extract error details (dev-only).
 */
export function getErrorDetails(
  error: unknown,
  devMode = import.meta.env.DEV,
): Record<string, unknown> | null {
  if (!devMode) return null;

  if (isAxiosError(error)) {
    const data = error.response?.data;
    if (isApiErrorResponse(data) && data.details) {
      return data.details;
    }
  }
  return null;
}

/**
 * Determine if error is retryable.
 */
export function isRetryableError(error: unknown): boolean {
  if (isAxiosError(error)) {
    const status = error.response?.status;
    return status !== undefined && status >= 500;
  }
  return false;
}

// Re-export for compatibility if needed in callers.
export type { ApiErrorResponse };
