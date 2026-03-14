const readEnv = (key: string): string | undefined => {
  try {
    const value = (import.meta as { env?: Record<string, string | undefined> })?.env?.[key];
    if (typeof value === "string" && value.length) {
      return value;
    }
  } catch {}
  return undefined;
};

const rawBaseUrl = readEnv("VITE_APP_API_BASE_URL") || readEnv("VITE_API_BASE_URL") || "";
const normalizedBaseUrl = rawBaseUrl.replace(/\/+$/, "");
const originBaseUrl = normalizedBaseUrl.endsWith("/api")
  ? normalizedBaseUrl.slice(0, -4)
  : normalizedBaseUrl;

export const API_BASE_URL = originBaseUrl;

export const resolveApiUrl = (path: string): string => {
  if (!path) {
    return originBaseUrl;
  }
  if (/^https?:\/\//i.test(path)) {
    return path;
  }
  if (!originBaseUrl) {
    return path;
  }
  return path.startsWith("/") ? `${originBaseUrl}${path}` : `${originBaseUrl}/${path}`;
};
