import { apiFetch } from "./http";
import type { GlobalSearchResult } from "@/types";

export const globalSearch = (keyword: string) =>
  apiFetch<GlobalSearchResult>(`/search?q=${encodeURIComponent(keyword)}`);
