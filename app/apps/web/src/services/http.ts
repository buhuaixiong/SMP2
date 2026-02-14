import type { AxiosRequestConfig } from "axios";
import http, { apiFetch, del as httpDelete, get as httpGet, post as httpPost, put as httpPut } from "@/api/http";
import type { ServiceDefinition } from "@/core/services";
import type { NotificationService } from "./notification";

export interface HttpRequestConfig<T = any> extends AxiosRequestConfig<T> {
  silent?: boolean;
}

export interface HttpService {
  request<T = any>(config: HttpRequestConfig): Promise<T>;
  get<T = any>(url: string, config?: HttpRequestConfig): Promise<T>;
  post<T = any>(url: string, data?: any, config?: HttpRequestConfig): Promise<T>;
  put<T = any>(url: string, data?: any, config?: HttpRequestConfig): Promise<T>;
  delete<T = any>(url: string, config?: HttpRequestConfig): Promise<T>;
  fetch<T = any>(url: string, init?: Parameters<typeof apiFetch>[1]): Promise<T>;
}

const extractMessage = (error: unknown) => {
  if (error instanceof Error) {
    return error.message;
  }
  if (typeof error === "string") {
    return error;
  }
  return "请求失败，请稍后重试";
};

export const httpService: ServiceDefinition<HttpService> = {
  name: "http",
  dependencies: ["notification"],
  setup({ get }) {
    const notification = get<NotificationService>("notification");

    const handleError = (error: unknown, silent?: boolean) => {
      if (!silent) {
        notification.error(extractMessage(error), "网络请求错误", { sticky: false });
      }
    };

    const run = async <T>(fn: () => Promise<T>, silent?: boolean) => {
      try {
        return await fn();
      } catch (error) {
        handleError(error, silent);
        throw error;
      }
    };

    return {
      request<T>(config: HttpRequestConfig): Promise<T> {
        return run(async () => {
          const response = await http.request<T>(config);
          return response.data;
        }, config?.silent);
      },
      get<T>(url: string, config?: HttpRequestConfig): Promise<T> {
        return run(() => httpGet<T>(url, config), config?.silent);
      },
      post<T>(url: string, data?: any, config?: HttpRequestConfig): Promise<T> {
        return run(() => httpPost<T>(url, data, config), config?.silent);
      },
      put<T>(url: string, data?: any, config?: HttpRequestConfig): Promise<T> {
        return run(() => httpPut<T>(url, data, config), config?.silent);
      },
      delete<T>(url: string, config?: HttpRequestConfig): Promise<T> {
        return run(() => httpDelete<T>(url, config), config?.silent);
      },
      fetch<T>(url: string, init?: Parameters<typeof apiFetch>[1]): Promise<T> {
        const silent = (init as HttpRequestConfig)?.silent;
        return run(() => apiFetch<T>(url, init), silent);
      },
    };
  },
};
