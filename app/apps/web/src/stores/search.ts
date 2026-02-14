import { defineStore } from "pinia";
import { ref } from "vue";
import { globalSearch } from "@/api/search";
import type { GlobalSearchResult } from "@/types";

export const useSearchStore = defineStore("global-search", () => {
  const keyword = ref("");
  const loading = ref(false);
  const results = ref<GlobalSearchResult | null>(null);
  const error = ref<string | null>(null);

  const runSearch = async (query: string) => {
    keyword.value = query;
    if (!query.trim()) {
      results.value = null;
      return null;
    }
    loading.value = true;
    error.value = null;
    try {
      const data = await globalSearch(query);
      results.value = data;
      return data;
    } catch (err) {
      error.value = err instanceof Error ? err.message : "Search failed";
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const clear = () => {
    keyword.value = "";
    results.value = null;
    error.value = null;
  };

  return {
    keyword,
    loading,
    results,
    error,
    runSearch,
    clear,
  };
});
