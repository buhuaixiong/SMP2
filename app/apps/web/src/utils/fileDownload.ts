import { nextTick } from "vue";
import { resolveApiUrl } from "./apiBaseUrl";

const ensureDomReady = async () => {
  if (typeof document === "undefined" || typeof window === "undefined") {
    return false;
  }
  await nextTick();
  return true;
};

const getAuthToken = () => {
  try {
    return localStorage.getItem("token");
  } catch {
    return null;
  }
};

export async function downloadFile(url: string, filename?: string): Promise<void> {
  try {
    const token = getAuthToken();
    const response = await fetch(url, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });

    if (!response.ok) {
      throw new Error(`Failed to download file: ${response.statusText}`);
    }

    const blob = await response.blob();
    if (!(await ensureDomReady())) {
      throw new Error("DOM is not available for file download.");
    }

    const downloadUrl = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = downloadUrl;
    link.download = filename || "download";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(downloadUrl);
  } catch (error) {
    console.error("Download failed:", error);
    throw error;
  }
}

export async function openFileInNewTab(url: string): Promise<void> {
  const token = getAuthToken();
  const response = await fetch(url, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });
  if (!response.ok) {
    throw new Error(`Failed to open file: ${response.statusText}`);
  }
  const blob = await response.blob();
  if (typeof window === "undefined") {
    throw new Error("Window is not available.");
  }
  const blobUrl = window.URL.createObjectURL(blob);
  window.open(blobUrl, "_blank", "noopener,noreferrer");
  window.setTimeout(() => {
    window.URL.revokeObjectURL(blobUrl);
  }, 60_000);
}

export function getUploadUrl(storedFileName: string, category: string = "rfq-attachments"): string {
  if (!storedFileName) return "";
  return resolveApiUrl(`/uploads/${category}/${storedFileName}`);
}

export function getSecureDownloadUrl(
  storedFileName: string,
  category: string = "rfq-attachments",
): string {
  if (!storedFileName) return "";
  return resolveApiUrl(`/uploads/${category}/${storedFileName}`);
}

export function resolveUploadDownloadUrl(
  rawPath?: string | null,
  storedFileName?: string | null,
  category: string = "rfq-attachments",
): string | null {
  if (typeof rawPath === "string" && rawPath.length > 0) {
    const normalizedPath = rawPath.replace(/\\/g, "/");
    const uploadsIndex = normalizedPath.indexOf("/uploads/");
    if (uploadsIndex >= 0) {
      let relativePath = normalizedPath.slice(uploadsIndex);
      if (!relativePath.startsWith("/")) {
        relativePath = `/${relativePath}`;
      }
      return resolveApiUrl(relativePath);
    }
  }

  if (storedFileName) {
    return getSecureDownloadUrl(storedFileName, category);
  }

  return null;
}

export async function downloadFileSecure(
  storedFileName: string,
  originalFileName: string,
  category: string = "rfq-attachments",
): Promise<void> {
  const url = getSecureDownloadUrl(storedFileName, category);
  await downloadFile(url, originalFileName);
}
