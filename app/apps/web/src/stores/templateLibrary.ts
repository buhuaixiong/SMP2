import { defineStore } from "pinia";
import { reactive, ref } from "vue";

import { fetchTemplates, fetchTemplateHistory, uploadTemplateDocument } from "@/api/templates";
import type { TemplateDefinition, TemplateFileRecord } from "@/types";

interface BooleanMap {
  [code: string]: boolean;
}

interface HistoryMap {
  [code: string]: TemplateFileRecord[];
}

export const useTemplateLibraryStore = defineStore("templateLibrary", () => {
  const templates = ref<TemplateDefinition[]>([]);
  const loading = ref(false);
  const uploading = ref(false);
  const history = ref<HistoryMap>({});
  const historyLoading = reactive<BooleanMap>({});

  const loadTemplates = async () => {
    loading.value = true;
    try {
      templates.value = await fetchTemplates();
    } finally {
      loading.value = false;
    }
  };

  const loadHistory = async (code: string) => {
    historyLoading[code] = true;
    try {
      const response = await fetchTemplateHistory(code);
      history.value = {
        ...history.value,
        [code]: response.history,
      };
      return response.history;
    } finally {
      historyLoading[code] = false;
    }
  };

  const uploadTemplate = async (code: string, file: File) => {
    uploading.value = true;
    try {
      templates.value = await uploadTemplateDocument(code, file);
      await loadHistory(code);
    } finally {
      uploading.value = false;
    }
  };

  const reset = () => {
    templates.value = [];
    history.value = {};
    Object.keys(historyLoading).forEach((key) => {
      delete historyLoading[key];
    });
  };

  return {
    templates,
    loading,
    uploading,
    history,
    historyLoading,
    loadTemplates,
    loadHistory,
    uploadTemplate,
    reset,
  };
});
