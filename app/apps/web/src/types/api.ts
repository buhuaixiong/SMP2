import axios, { type AxiosError } from "axios";

// ==================== Base API response types ====================

export interface ApiSuccessResponse<T> {
  success: true;
  data: T;
  message?: string;
}

export interface ApiErrorResponse {
  success: false;
  error: string;
  code: string;
  message?: string;
  details?: Record<string, unknown>;
  /** @hidden Only available in dev environments. */
  stack?: string;
}

export type ApiResponse<T> = ApiSuccessResponse<T> | ApiErrorResponse;

// ==================== Paginated response ====================

export interface PaginatedResponse<T> {
  success: true;
  data: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  message?: string;
}

// ==================== Lockdown response ====================

export interface LockdownErrorResponse {
  error: string;
  message: string;
  lockdown: true;
  retryAfter?: number;
}

export function isLockdownResponse(data: unknown): data is LockdownErrorResponse {
  if (!data || typeof data !== "object") return false;
  return "lockdown" in data && (data as LockdownErrorResponse).lockdown === true;
}

// ==================== Error guards ====================

export function isAxiosError(error: unknown): error is AxiosError {
  return axios.isAxiosError(error);
}

export function isApiErrorResponse(data: unknown): data is ApiErrorResponse {
  if (!data || typeof data !== "object") return false;
  return (
    "success" in data &&
    (data as ApiErrorResponse).success === false &&
    "error" in data &&
    "code" in data
  );
}
