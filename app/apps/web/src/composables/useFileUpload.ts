import { ref } from "vue";
import { useService } from "@/core/hooks";
import type { HttpService } from "@/services";
import { useNotification } from "./useNotification";
import type { AxiosProgressEvent } from "axios";

export interface FileUploadOptions<T = unknown> {
  url?: string;
  fieldName?: string;
  extra?: Record<string, unknown>;
  request?: (formData: FormData) => Promise<T>;
  silent?: boolean;
  successMessage?: string;
  errorMessage?: string;
  onProgress?: (progress: number) => void;
}

export function useFileUpload() {
  const http = useService<HttpService>("http");
  const notification = useNotification();
  const progress = ref(0);
  const uploading = ref(false);
  const lastError = ref<string | null>(null);

  const upload = async <T = unknown>(file: File | Blob, options: FileUploadOptions<T>) => {
    if (!file) {
      throw new Error("File is required for upload");
    }

    const formData = new FormData();
    formData.append(options.fieldName ?? "file", file);
    Object.entries(options.extra ?? {}).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        formData.append(key, String(value));
      }
    });

    uploading.value = true;
    lastError.value = null;
    progress.value = 0;

    const onUploadProgress = (event: AxiosProgressEvent) => {
      if (event.total) {
        progress.value = Math.round((event.loaded / event.total) * 100);
        options.onProgress?.(progress.value);
      }
    };

    try {
      let response: T;
      if (options.request) {
        response = await options.request(formData);
      } else if (options.url) {
        response = await http.request<T>({
          url: options.url,
          method: "POST",
          data: formData,
          silent: options.silent,
          headers: { "Content-Type": "multipart/form-data" },
          onUploadProgress,
        });
      } else {
        throw new Error("Either url or request must be provided");
      }

      if (options.successMessage) {
        notification.success(options.successMessage);
      }
      return response;
    } catch (error: any) {
      const message = options.errorMessage ?? error?.message ?? "文件上传失败";
      lastError.value = message;
      notification.error(message);
      throw error;
    } finally {
      uploading.value = false;
    }
  };

  const reset = () => {
    uploading.value = false;
    progress.value = 0;
    lastError.value = null;
  };

  return {
    uploading,
    progress,
    lastError,
    upload,
    reset,
  };
}

