import { computed, reactive, ref } from "vue";

export interface DirectoryFilters {
  q: string;
  status: string;
  stage: string;
  category: string;
  region: string;
  importance: string;
  tag: string;
  completionStatus: string;
  missingDocument: string[];
}

export interface QuickFilterState {
  showAdvancedFilters: boolean;
}

export const createEmptyFilters = (): DirectoryFilters => ({
  q: "",
  status: "",
  stage: "",
  category: "",
  region: "",
  importance: "",
  tag: "",
  completionStatus: "",
  missingDocument: [],
});

export function useSupplierDirectoryFilters() {
  const filters = reactive(createEmptyFilters());
  const showAdvancedFilters = ref(false);

  const toggleAdvancedFilters = () => {
    showAdvancedFilters.value = !showAdvancedFilters.value;
  };

  const resetFilters = () => {
    Object.assign(filters, createEmptyFilters());
  };

  const missingDocumentFilter = computed(() => filters.missingDocument.slice());

  const toggleMissingDocumentFilter = (code: string) => {
    const index = filters.missingDocument.indexOf(code);
    if (index >= 0) {
      filters.missingDocument.splice(index, 1);
      return;
    }
    filters.missingDocument.push(code);
  };

  const toggleNeedsAttentionFilter = () => {
    filters.completionStatus =
      filters.completionStatus === "needs_attention" ? "" : "needs_attention";
  };

  const clearQuickFilters = () => {
    filters.completionStatus = "";
    filters.missingDocument = [];
  };

  return {
    filters,
    showAdvancedFilters,
    toggleAdvancedFilters,
    resetFilters,
    missingDocumentFilter,
    toggleMissingDocumentFilter,
    toggleNeedsAttentionFilter,
    clearQuickFilters,
  };
}
