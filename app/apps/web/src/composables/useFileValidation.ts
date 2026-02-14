/**
 * File Validation Composable
 * Provides client-side file validation based on scenario configuration
 */

import { ref, computed } from "vue";
import { useI18n } from "vue-i18n";
import { fetchFileUploadConfig } from "../api/fileUploadConfig";
import type { FileUploadConfig } from "../types/fileUpload";


import { useNotification } from "@/composables";

const notification = useNotification();

export function useFileValidation(scenario: string = "general_upload") {
  const { t } = useI18n();
  const config = ref<FileUploadConfig | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  // Load configuration
  async function loadConfig() {
    loading.value = true;
    error.value = null;

    try {
      config.value = await fetchFileUploadConfig(scenario);
    } catch (err: any) {
      error.value = err.message || t("fileUpload.messages.loadFailed");
      console.error("[FileValidation] Error loading config:", err);

      // Set default fallback config
      config.value = {
        id: 0,
        scenario: "general_upload",
        scenario_name: t("fileUpload.defaultScenarioName"),
        scenario_description: null,
        allowed_formats: ".pdf,.doc,.docx,.xlsx,.xls,.jpg,.jpeg,.png,.zip",
        max_file_size: 50 * 1024 * 1024,
        max_file_count: 5,
        enable_virus_scan: 1,
        created_at: "",
        updated_at: "",
        updated_by: null,
      };
    } finally {
      loading.value = false;
    }
  }

  // Computed properties
  const allowedFormats = computed(() => {
    if (!config.value) return [];
    return config.value.allowed_formats.split(",").map((f) => f.trim().toLowerCase());
  });

  const allowedFormatsList = computed(() => {
    return allowedFormats.value.join(", ");
  });

  const maxFileSizeMB = computed(() => {
    if (!config.value) return 0;
    return (config.value.max_file_size / (1024 * 1024)).toFixed(2);
  });

  const maxFileCount = computed(() => {
    return config.value?.max_file_count || 5;
  });

  const virusScanEnabled = computed(() => {
    return config.value?.enable_virus_scan === 1;
  });

  // Validation functions
  function validateFileType(file: File): { valid: boolean; error?: string } {
    const ext = "." + file.name.split(".").pop()?.toLowerCase();

    if (!allowedFormats.value.includes(ext)) {
      return {
        valid: false,
        error: t("fileUpload.validation.typeNotAllowed", { ext, allowed: allowedFormatsList.value }),
      };
    }

    return { valid: true };
  }

  function validateFileSize(file: File): { valid: boolean; error?: string } {
    if (!config.value) return { valid: false, error: t("fileUpload.validation.configNotLoaded") };

    if (file.size > config.value.max_file_size) {
      const sizeMB = (file.size / (1024 * 1024)).toFixed(2);
      return {
        valid: false,
        error: t("fileUpload.validation.sizeExceeded", { size: sizeMB, limit: maxFileSizeMB.value }),
      };
    }

    return { valid: true };
  }

  function validateFileCount(files: File[]): { valid: boolean; error?: string } {
    if (!config.value) return { valid: false, error: t("fileUpload.validation.configNotLoaded") };

    if (files.length > config.value.max_file_count) {
      return {
        valid: false,
        error: t("fileUpload.validation.countExceeded", { count: files.length, limit: config.value.max_file_count }),
      };
    }

    return { valid: true };
  }

  function validateFile(file: File): { valid: boolean; error?: string } {
    // Check file type
    const typeValidation = validateFileType(file);
    if (!typeValidation.valid) {
      return typeValidation;
    }

    // Check file size
    const sizeValidation = validateFileSize(file);
    if (!sizeValidation.valid) {
      return sizeValidation;
    }

    return { valid: true };
  }

  function validateFiles(files: File[]): { valid: boolean; error?: string } {
    // Check file count
    const countValidation = validateFileCount(files);
    if (!countValidation.valid) {
      return countValidation;
    }

    // Validate each file
    for (const file of files) {
      const fileValidation = validateFile(file);
      if (!fileValidation.valid) {
        return fileValidation;
      }
    }

    return { valid: true };
  }

  // Before upload handler for Element Plus upload component
  function beforeUpload(file: File): boolean {
    const validation = validateFile(file);

    if (!validation.valid) {
      notification.error(validation.error || t("fileUpload.validation.validationFailed"));
      return false;
    }

    return true;
  }

  // File size formatter
  function formatFileSize(bytes: number): string {
    if (bytes === 0) return "0 B";

    const k = 1024;
    const sizes = ["B", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + " " + sizes[i];
  }

  // Get accept attribute for input element
  const acceptAttribute = computed(() => {
    return allowedFormats.value.join(",");
  });

  return {
    // State
    config,
    loading,
    error,

    // Computed
    allowedFormats,
    allowedFormatsList,
    maxFileSizeMB,
    maxFileCount,
    virusScanEnabled,
    acceptAttribute,

    // Methods
    loadConfig,
    validateFile,
    validateFiles,
    validateFileType,
    validateFileSize,
    validateFileCount,
    beforeUpload,
    formatFileSize,
  };
}
