// src/api/http.ts - Simplified HTTP Client
import axios, { AxiosError, AxiosInstance, type AxiosRequestConfig } from "axios";
import { isLockdownResponse } from "@/types/api";
import { extractErrorMessage } from "@/utils/errorHandling";

const readEnv = (key: string): string | undefined => {
  const meta = import.meta as { env?: Record<string, string | undefined> };
  const v = meta?.env?.[key];
  if (typeof v === "string" && v.length) return v;
  const proc = typeof process !== "undefined" ? (process as { env?: Record<string, string | undefined> }) : undefined;
  const v2 = proc?.env?.[key];
  if (typeof v2 === "string" && v2.length) return v2;
  return undefined;
};

const normalizeBaseUrl = (url: string) => url.replace(/\/+$/, "") + (/(^|\/)api(\/|$)/.test(url) ? "" : "/api");

const rawBaseUrl = readEnv("VITE_APP_API_BASE_URL") || readEnv("VITE_API_BASE_URL") || "/api";
export const BASE_URL = normalizeBaseUrl(rawBaseUrl);
export const TIMEOUT = 15000;

const forceHttps =
  (readEnv("VITE_FORCE_HTTPS") || "").toLowerCase() === "true" ||
  (typeof window !== "undefined" && window.location?.protocol === "https:");

export const normalizeToHttpsUrl = (url: string, shouldForceHttps = forceHttps) => {
  if (!shouldForceHttps || !url) return url;
  if (/^http:\/\//i.test(url)) {
    return url.replace(/^http:\/\//i, "https://");
  }
  return url;
};

const enforceHttps = (url: string) => normalizeToHttpsUrl(url, forceHttps);

const isLoginRequest = (url?: string) => url && /\/auth\/login(?:\?|$)/i.test(url);

let isHandlingUnauthorized = false;

const authStore = {
  getToken: () => localStorage.getItem("token"),
  logout: () => {
    localStorage.removeItem("token");
    localStorage.removeItem("user");
  },
  redirectToLogin: async () => {
    if (typeof window === "undefined" || isHandlingUnauthorized) return;
    isHandlingUnauthorized = true;
    try {
      authStore.logout();
      if (window.location.pathname !== "/login") window.location.href = "/login";
    } finally {
      isHandlingUnauthorized = false;
    }
  },
};

const http: AxiosInstance = axios.create({ baseURL: BASE_URL, timeout: TIMEOUT, withCredentials: false });

http.interceptors.request.use((config) => {
  if (forceHttps && typeof config.url === "string" && config.url.startsWith("http://")) {
    config.url = enforceHttps(config.url);
  }
  const token = authStore.getToken();
  if (token) {
    const headers = config.headers ?? {};
    if ("set" in headers && typeof headers.set === "function") {
      (headers as { set: (key: string, value: string) => void }).set("Authorization", `Bearer ${token}`);
    } else {
      (headers as Record<string, string>).Authorization = `Bearer ${token}`;
    }
    config.headers = headers;
  }
  return config;
});

http.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    const status = error.response?.status;
    const url = error.config?.url;

    if (status === 401) {
      if (url && !isLoginRequest(url)) await authStore.redirectToLogin();
    }
    if (status === 403) {
      console.warn("Forbidden: No permission to access this resource");
    }
    if (status === 503) {
      const data = error.response?.data;
      if (isLockdownResponse(data)) {
        try {
          const { useLockdownStore } = await import("@/stores/lockdown");
          useLockdownStore().syncFromLockdownResponse({
            message: data.message,
            lockdown: data.lockdown === true ? { isActive: true } : data.lockdown,
          });
        } catch (e) {
          console.error("[HTTP] Failed to sync lockdown state", e);
        }
      }
    }

    const serverMessage = extractErrorMessage(error);
    if (serverMessage) error.message = serverMessage;
    return Promise.reject(error);
  }
);

const patchUrl = (url: string) => {
  if (!url.startsWith("/")) return enforceHttps(url);
  if (url === "/api") return "/";
  if (url.startsWith("/api/")) return url.slice(4);
  return url;
};

type ApiFetchInit = {
  method?: string;
  headers?: Record<string, string>;
  body?: unknown;
  data?: unknown;
  params?:
    | Record<string, string | number | boolean | null | undefined>
    | URLSearchParams
    | object;
  signal?: AbortSignal;
  timeout?: number;
  parseData?: boolean;
};

/**
 * @deprecated Prefer apiFetch<T>(url, init) with explicit generics.
 */
export function apiFetch<T = any>(url: string, init?: ApiFetchInit): Promise<T>;
export function apiFetch<T>(url: string, init?: ApiFetchInit): Promise<T>;
export async function apiFetch<T>(url: string, init: ApiFetchInit = {}): Promise<T> {
  const method = (init.method || "GET").toUpperCase();
  let data = init.data ?? init.body;
  const isFormData = typeof FormData !== "undefined" && data instanceof FormData;
  const headers: Record<string, string> = { ...(init.headers || {}) };
  const trimmedString = typeof data === "string" ? data.trim() : "";
  const looksLikeJson = trimmedString.startsWith("{") || trimmedString.startsWith("[");

  if (data && !isFormData && typeof data === "object" && !(data instanceof ArrayBuffer)) {
    headers["Content-Type"] = headers["Content-Type"] || "application/json";
  }
  if (data && !isFormData && typeof data === "string" && looksLikeJson) {
    headers["Content-Type"] = headers["Content-Type"] || "application/json";
  }

  const res = await http.request<T>({
    url: patchUrl(url),
    method: method as "GET" | "POST" | "PUT" | "DELETE" | "PATCH" | "HEAD",
    headers,
    params: init.params,
    data,
    signal: init.signal,
    timeout: init.timeout,
  });
  return res.data;
}

export const get = <T = any>(url: string, config?: AxiosRequestConfig) =>
  http.get<T>(patchUrl(url), config).then((r) => r.data);
export const post = <T = any>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
  http.post<T>(patchUrl(url), data, config).then((r) => r.data);
export const put = <T = any>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
  http.put<T>(patchUrl(url), data, config).then((r) => r.data);
export const del = <T = any>(url: string, config?: AxiosRequestConfig) =>
  http.delete<T>(patchUrl(url), config).then((r) => r.data);
export default http;
