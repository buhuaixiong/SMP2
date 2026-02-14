<template>
  <div class="file-upload-with-validation">
    <el-upload
      ref="uploadRef"
      :action="uploadUrl"
      :headers="uploadHeaders"
      :data="uploadData"
      :multiple="maxFileCount > 1"
      :limit="maxFileCount"
      :accept="acceptAttribute"
      :before-upload="handleBeforeUpload"
      :on-success="handleSuccess"
      :on-error="handleError"
      :on-exceed="handleExceed"
      :on-remove="handleRemove"
      :file-list="fileList"
      :disabled="disabled || loading"
      :drag="drag"
      :list-type="listType"
      :auto-upload="autoUpload"
    >
      <template #trigger>
        <el-button :icon="UploadFilled" :loading="loading" :disabled="disabled">
          {{ t("common.selectFile") }}
        </el-button>
      </template>

      <template v-if="drag" #default>
        <el-icon class="el-icon--upload"><upload-filled /></el-icon>
        <div class="el-upload__text">
          {{ t("common.dragFileHere") }} <em>{{ t("common.clickToUpload") }}</em>
        </div>
      </template>

      <template #tip>
        <div class="el-upload__tip">
          <div v-if="!loading && config">
            <el-tag size="small" type="info">
              {{ t("fileUpload.allowedFormats") }}: {{ allowedFormatsList }}
            </el-tag>
            <el-tag size="small" type="info" style="margin-left: 8px">
              {{ t("fileUpload.maxSize") }}: {{ maxFileSizeMB }}MB
            </el-tag>
            <el-tag size="small" type="info" style="margin-left: 8px">
              {{ t("fileUpload.maxCount") }}: {{ maxFileCount }}
            </el-tag>
            <el-tag v-if="virusScanEnabled" size="small" type="success" style="margin-left: 8px">
              {{ t("fileUpload.virusScanEnabled") }}
            </el-tag>
          </div>
          <div v-if="loading" style="color: #909399">{{ t("common.loading") }}...</div>
        </div>
      </template>
    </el-upload>

    <!-- Scan status display -->
    <div v-if="showScanStatus && scanningFiles.length > 0" class="scan-status">
      <el-alert
        :title="t('fileUpload.virusScanning')"
        type="info"
        :closable="false"
        show-icon
        style="margin-top: 12px"
      >
        <div v-for="file in scanningFiles" :key="file.name" class="scanning-file">
          <el-icon class="is-loading"><Loading /></el-icon>
          <span>{{ file.name }}</span>
        </div>
      </el-alert>
    </div>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted, watch } from "vue";
import { useI18n } from "vue-i18n";

import { UploadFilled, Loading } from "@element-plus/icons-vue";
import { useFileValidation } from "../composables/useFileValidation";
import type { UploadInstance, UploadProps, UploadUserFile } from "element-plus";
import { resolveApiUrl } from "@/utils/apiBaseUrl";


import { useNotification } from "@/composables";

const notification = useNotification();
interface Props {
  scenario?: string;
  modelValue?: any[];
  disabled?: boolean;
  drag?: boolean;
  listType?: "text" | "picture" | "picture-card";
  autoUpload?: boolean;
  showScanStatus?: boolean;
}

interface Emits {
  (e: "update:modelValue", files: any[]): void;
  (e: "success", response: any, file: any): void;
  (e: "error", error: any, file: any): void;
  (e: "remove", file: any): void;
}

const props = withDefaults(defineProps<Props>(), {
  scenario: "general_upload",
  modelValue: () => [],
  disabled: false,
  drag: false,
  listType: "text",
  autoUpload: true,
  showScanStatus: true,
});

const emit = defineEmits<Emits>();

const { t } = useI18n();
const uploadRef = ref<UploadInstance>();
const fileList = ref<UploadUserFile[]>([]);
const scanningFiles = ref<File[]>([]);

// Use file validation composable
const {
  config,
  loading,
  error,
  allowedFormats,
  allowedFormatsList,
  maxFileSizeMB,
  maxFileCount,
  virusScanEnabled,
  acceptAttribute,
  loadConfig,
  validateFile,
  validateFiles,
  beforeUpload,
} = useFileValidation(props.scenario);

// Load configuration on mount
onMounted(async () => {
  await loadConfig();
});

// Watch scenario changes
watch(
  () => props.scenario,
  async () => {
    await loadConfig();
  },
);

// Watch model value changes
watch(
  () => props.modelValue,
  (newValue) => {
    if (newValue && newValue.length > 0) {
      fileList.value = newValue.map((file: any) => ({
        name: file.name || file.original_name,
        url: file.url || file.file_path,
        status: "success",
        uid: file.id || Date.now() + Math.random(),
      }));
    } else {
      fileList.value = [];
    }
  },
  { immediate: true },
);

// Upload configuration
const uploadUrl = computed(() => {
  return resolveApiUrl(`/api/file-uploads/${props.scenario}`);
});

const uploadHeaders = computed(() => {
  const token = localStorage.getItem("token");
  return {
    Authorization: `Bearer ${token}`,
  };
});

const uploadData = computed(() => {
  return {
    scenario: props.scenario,
  };
});

// Event handlers
function handleBeforeUpload(file: File): boolean {
  const validation = validateFile(file);

  if (!validation.valid) {
    notification.error(validation.error || t("fileUpload.validationFailed"));
    return false;
  }

  if (virusScanEnabled.value) {
    scanningFiles.value.push(file);
  }

  return true;
}

function handleSuccess(response: any, file: any) {
  // Remove from scanning list
  const index = scanningFiles.value.findIndex((f) => f.name === file.name);
  if (index !== -1) {
    scanningFiles.value.splice(index, 1);
  }

  notification.success(t("fileUpload.uploadSuccess"));
  emit("success", response, file);
  emit("update:modelValue", [...fileList.value]);
}

function handleError(error: any, file: any) {
  // Remove from scanning list
  const index = scanningFiles.value.findIndex((f) => f.name === file.name);
  if (index !== -1) {
    scanningFiles.value.splice(index, 1);
  }

  let errorMessage = t("fileUpload.uploadFailed");

  if (error?.message) {
    errorMessage = error.message;
  } else if (typeof error === "string") {
    try {
      const parsed = JSON.parse(error);
      errorMessage = parsed.message || errorMessage;
    } catch {
      errorMessage = error;
    }
  }

  notification.error(errorMessage);
  emit("error", error, file);
}

function handleExceed() {
  notification.warning(t("fileUpload.exceedLimit", { count: maxFileCount.value }));
}

function handleRemove(file: any) {
  emit("remove", file);
  emit("update:modelValue", [...fileList.value]);
}

// Expose methods
defineExpose({
  upload: () => uploadRef.value?.submit(),
  clearFiles: () => uploadRef.value?.clearFiles(),
  abort: (file: any) => uploadRef.value?.abort(file),
});




</script>

<style scoped>
.file-upload-with-validation {
  width: 100%;
}

.el-upload__tip {
  margin-top: 8px;
  font-size: 12px;
  color: #606266;
}

.el-upload__tip .el-tag {
  margin-bottom: 4px;
}

.scan-status {
  margin-top: 12px;
}

.scanning-file {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 0;
}

.scanning-file .is-loading {
  font-size: 14px;
  color: #409eff;
}
</style>
