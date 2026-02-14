<template>
  <el-dialog
    v-model="visible"
    title="Bulk Document Upload"
    :width="900"
    :close-on-click-modal="false"
    class="bulk-upload-dialog"
  >
    <div class="bulk-upload-container">
      <!-- Drop Zone -->
      <div
        class="bulk-dropzone"
        :class="{ 'is-dragover': isDragOver }"
        @drop.prevent="handleDrop"
        @dragover.prevent="isDragOver = true"
        @dragleave.prevent="isDragOver = false"
      >
        <el-icon :size="64" class="upload-icon"><upload-filled /></el-icon>
        <h3>Drag and drop multiple files here</h3>
        <p>or</p>
        <el-button type="primary" size="large" @click="triggerFileInput">
          <el-icon><folder-opened /></el-icon>
          Browse Files
        </el-button>
        <input
          ref="fileInputRef"
          type="file"
          multiple
          accept=".pdf,.jpg,.jpeg,.png,.doc,.docx"
          style="display: none"
          @change="handleFileSelect"
        />
        <p class="dropzone-hint">
          Supported formats: PDF, JPG, PNG, DOC, DOCX (Max 10MB per file, up to 10 files)
        </p>
      </div>

      <!-- File List -->
      <transition name="slide-down">
        <div v-if="fileItems.length > 0" class="file-list">
          <div class="list-header">
            <h4>Selected Files ({{ fileItems.length }})</h4>
            <el-button size="small" text @click="clearAll">
              <el-icon><delete /></el-icon>
              Clear All
            </el-button>
          </div>

          <div class="file-items">
            <div
              v-for="(item, index) in fileItems"
              :key="index"
              class="file-item"
              :class="{ 'has-error': item.error, 'is-uploaded': item.uploaded }"
            >
              <div class="file-info">
                <el-icon :size="24" class="file-icon">
                  <document v-if="item.file.type === 'application/pdf'" />
                  <picture v-else-if="item.file.type.startsWith('image/')" />
                  <document v-else />
                </el-icon>

                <div class="file-details">
                  <span class="file-name">{{ item.file.name }}</span>
                  <span class="file-size">{{ formatFileSize(item.file.size) }}</span>
                </div>

                <el-icon v-if="!item.uploaded" class="remove-icon" @click="removeFile(index)">
                  <close />
                </el-icon>

                <el-icon v-else class="success-icon" color="#67C23A">
                  <circle-check />
                </el-icon>
              </div>

              <!-- Document Type Selection -->
              <div class="file-metadata">
                <el-select
                  v-model="item.docType"
                  placeholder="Document Type"
                  size="small"
                  :disabled="item.uploaded || uploading"
                  class="meta-input"
                >
                  <el-option
                    v-for="type in documentTypes"
                    :key="type.value"
                    :label="type.label"
                    :value="type.value"
                  />
                </el-select>

                <el-select
                  v-model="item.category"
                  placeholder="Category (Optional)"
                  size="small"
                  clearable
                  :disabled="item.uploaded || uploading"
                  class="meta-input"
                >
                  <el-option label="Compliance" value="compliance" />
                  <el-option label="Financial" value="finance" />
                  <el-option label="Tax" value="tax" />
                  <el-option label="Quality" value="quality" />
                  <el-option label="Legal" value="legal" />
                  <el-option label="Other" value="other" />
                </el-select>

                <el-date-picker
                  v-model="item.validFrom"
                  type="date"
                  :placeholder="
                    isRequiredDocType(item.docType)
                      ? 'Valid From (Required)'
                      : 'Valid From (Optional)'
                  "
                  size="small"
                  clearable
                  :disabled="item.uploaded || uploading"
                  :disabled-date="disabledDate"
                  class="meta-input"
                  style="width: 180px"
                />

                <el-date-picker
                  v-model="item.expiresAt"
                  type="date"
                  :placeholder="
                    isRequiredDocType(item.docType)
                      ? 'Expiry Date (Required)'
                      : 'Expiry Date (Optional)'
                  "
                  size="small"
                  clearable
                  :disabled="item.uploaded || uploading"
                  :disabled-date="(time: Date) => disabledExpiryDate(time, item)"
                  class="meta-input"
                  style="width: 180px"
                />
              </div>

              <!-- Error Message -->
              <transition name="fade">
                <div v-if="item.error" class="file-error">
                  <el-icon><warning-filled /></el-icon>
                  <span>{{ item.error }}</span>
                </div>
              </transition>

              <!-- Upload Progress -->
              <transition name="fade">
                <div v-if="item.uploading" class="file-progress">
                  <el-progress :percentage="item.progress || 0" :show-text="false" />
                  <span class="progress-text">Uploading...</span>
                </div>
              </transition>
            </div>
          </div>
        </div>
      </transition>

      <!-- Upload Summary -->
      <transition name="fade">
        <div v-if="uploadComplete && uploadResult" class="upload-summary">
          <el-result
            :icon="uploadResult.failed.length === 0 ? 'success' : 'warning'"
            :title="`Upload ${uploadResult.failed.length === 0 ? 'Complete' : 'Completed with Errors'}`"
          >
            <template #sub-title>
              <div class="summary-stats">
                <div class="stat-item success">
                  <el-icon :size="20"><circle-check /></el-icon>
                  <span>{{ uploadResult.success.length }} successful</span>
                </div>
                <div v-if="uploadResult.failed.length > 0" class="stat-item error">
                  <el-icon :size="20"><circle-close /></el-icon>
                  <span>{{ uploadResult.failed.length }} failed</span>
                </div>
              </div>

              <div v-if="uploadResult.failed.length > 0" class="failed-list">
                <h5>Failed uploads:</h5>
                <ul>
                  <li v-for="(fail, index) in uploadResult.failed" :key="index">
                    <strong>{{ fail.filename }}:</strong> {{ fail.error }}
                  </li>
                </ul>
              </div>
            </template>
          </el-result>
        </div>
      </transition>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">Cancel</el-button>
        <el-button type="primary" :loading="uploading" :disabled="!canUpload" @click="startUpload">
          <el-icon><upload /></el-icon>
          Upload {{ fileItems.length }} File(s)
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">




import { ref, computed } from "vue";

import {
  UploadFilled,
  FolderOpened,
  Document,
  Picture,
  Close,
  CircleCheck,
  CircleClose,
  Delete,
  Upload,
  WarningFilled,
} from "@element-plus/icons-vue";
import { bulkUploadDocuments } from "@/api/documents";


import { useNotification } from "@/composables";
const notification = useNotification();

interface FileItem {
  file: File;
  docType: string;
  category?: string;
  validFrom?: Date | null;
  expiresAt?: Date | null;
  error?: string;
  uploading?: boolean;
  uploaded?: boolean;
  progress?: number;
}

interface Props {
  modelValue: boolean;
  supplierId: number;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  (e: "update:modelValue", value: boolean): void;
  (e: "success"): void;
}>();

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val),
});

const fileInputRef = ref<HTMLInputElement>();
const isDragOver = ref(false);
const fileItems = ref<FileItem[]>([]);
const uploading = ref(false);
const uploadComplete = ref(false);
const uploadResult = ref<any>(null);

const documentTypes = [
  { value: "business_license", label: "Business License" },
  { value: "tax_certificate", label: "Tax Registration Certificate" },
  { value: "bank_information", label: "Bank Account Information" },
  { value: "quality_cert", label: "Quality Certification" },
  { value: "iso_cert", label: "ISO Certification" },
  { value: "insurance", label: "Insurance Policy" },
  { value: "contract", label: "Contract" },
  { value: "other", label: "Other Document" },
];

const requiredDocTypes = ["business_license", "tax_certificate", "bank_information"];

const isRequiredDocType = (docType: string) => {
  return requiredDocTypes.includes(docType);
};

const canUpload = computed(() => {
  if (fileItems.value.length === 0) return false;
  if (uploading.value) return false;
  if (uploadComplete.value) return false;
  return fileItems.value.every((item) => item.docType && !item.error);
});

const triggerFileInput = () => {
  fileInputRef.value?.click();
};

const handleFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files) {
    addFiles(Array.from(target.files));
  }
  if (fileInputRef.value) {
    fileInputRef.value.value = "";
  }
};

const handleDrop = (event: DragEvent) => {
  isDragOver.value = false;
  if (event.dataTransfer?.files) {
    addFiles(Array.from(event.dataTransfer.files));
  }
};

const addFiles = (files: File[]) => {
  if (fileItems.value.length + files.length > 10) {
    notification.warning("Maximum 10 files allowed");
    return;
  }

  files.forEach((file) => {
    if (!validateFile(file)) return;

    const docType = detectDocumentType(file.name);
    fileItems.value.push({
      file,
      docType,
      category: undefined,
      validFrom: null,
      expiresAt: null,
      error: undefined,
      uploading: false,
      uploaded: false,
      progress: 0,
    });
  });
};

const disabledExpiryDate = (time: Date, item: FileItem) => {
  if (item.validFrom) {
    // Expiry date must be after valid from date
    const validFromTime = new Date(item.validFrom).getTime();
    return time.getTime() <= validFromTime;
  }
  return time.getTime() < Date.now() - 86400000;
};

const validateFile = (file: File): boolean => {
  const maxSize = 10 * 1024 * 1024; // 10MB
  const allowedTypes = [
    "application/pdf",
    "image/jpeg",
    "image/png",
    "application/msword",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  ];

  if (file.size > maxSize) {
    notification.error(`File too large: ${file.name} (Max 10MB)`);
    return false;
  }

  if (!allowedTypes.includes(file.type) && !file.name.match(/\.(pdf|jpe?g|png|docx?)$/i)) {
    notification.error(`Unsupported file type: ${file.name}`);
    return false;
  }

  return true;
};

const detectDocumentType = (filename: string): string => {
  const lower = filename.toLowerCase();
  if (lower.includes("license") || lower.includes("营业执照")) return "business_license";
  if (lower.includes("tax") || lower.includes("税务")) return "tax_certificate";
  if (lower.includes("bank") || lower.includes("银行")) return "bank_information";
  if (lower.includes("quality") || lower.includes("质量")) return "quality_cert";
  if (lower.includes("iso")) return "iso_cert";
  if (lower.includes("insurance") || lower.includes("保险")) return "insurance";
  if (lower.includes("contract") || lower.includes("合同")) return "contract";
  return "";
};

const removeFile = (index: number) => {
  fileItems.value.splice(index, 1);
};

const clearAll = () => {
  fileItems.value = [];
  uploadComplete.value = false;
  uploadResult.value = null;
};

const disabledDate = (time: Date) => {
  return time.getTime() < Date.now() - 86400000;
};

const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + " " + sizes[i];
};

const startUpload = async () => {
  uploading.value = true;
  uploadComplete.value = false;

  // Validate all items have required fields and dates
  for (const item of fileItems.value) {
    if (!item.docType) {
      item.error = "Document type is required";
      uploading.value = false;
      notification.error("Please select document type for all files");
      return;
    }

    // Validate dates for required document types
    if (isRequiredDocType(item.docType)) {
      if (!item.validFrom || !item.expiresAt) {
        item.error = "Valid From and Expiry dates are required for this document type";
        uploading.value = false;
        notification.error(`Valid From and Expiry dates are required for ${item.file.name}`);
        return;
      }
    }

    // Validate date range if both dates are provided
    if (item.validFrom && item.expiresAt) {
      if (new Date(item.validFrom) >= new Date(item.expiresAt)) {
        item.error = "Valid from date must be earlier than expiry date";
        uploading.value = false;
        notification.error("Date validation failed for " + item.file.name);
        return;
      }
    }
    item.error = undefined;
  }

  try {
    // Prepare documents array
    const documents = fileItems.value.map((item) => ({
      file: item.file,
      docType: item.docType,
      category: item.category,
      validFrom: item.validFrom?.toISOString(),
      expiresAt: item.expiresAt?.toISOString(),
    }));

    // Mark all as uploading
    fileItems.value.forEach((item) => {
      item.uploading = true;
      item.progress = 50;
    });

    // Call bulk upload API
    const result = await bulkUploadDocuments(props.supplierId, documents);

    // Update upload status
    fileItems.value.forEach((item) => {
      item.uploading = false;
      item.progress = 100;
    });

    // Match results with file items
    result.success.forEach((doc: any) => {
      const item = fileItems.value.find((i) => i.file.name === doc.originalName);
      if (item) {
        item.uploaded = true;
        item.error = undefined;
      }
    });

    result.failed.forEach((fail: any) => {
      const item = fileItems.value.find((i) => i.file.name === fail.filename);
      if (item) {
        item.error = fail.error;
        item.uploaded = false;
      }
    });

    uploadResult.value = result;
    uploadComplete.value = true;

    if (result.failed.length === 0) {
      notification.success(`Successfully uploaded ${result.success.length} documents`);
      setTimeout(() => {
        emit("success");
        handleClose();
      }, 2000);
    } else {
      notification.warning(
        `Uploaded ${result.success.length} documents, ${result.failed.length} failed`,
      );
    }
  } catch (error) {
    console.error("Bulk upload failed:", error);
    notification.error("Bulk upload failed");
    fileItems.value.forEach((item) => {
      item.uploading = false;
      item.error = "Upload failed";
    });
  } finally {
    uploading.value = false;
  }
};

const handleClose = () => {
  if (!uploading.value) {
    visible.value = false;
    // Reset state after animation
    setTimeout(() => {
      fileItems.value = [];
      uploadComplete.value = false;
      uploadResult.value = null;
    }, 300);
  }
};




</script>

<style scoped>
.bulk-upload-container {
  min-height: 400px;
}

.bulk-dropzone {
  border: 2px dashed #dcdfe6;
  border-radius: 12px;
  padding: 60px 40px;
  text-align: center;
  background: #fafafa;
  transition: all 0.3s;
  cursor: pointer;
}

.bulk-dropzone:hover,
.bulk-dropzone.is-dragover {
  border-color: #409eff;
  background: #f0f7ff;
}

.upload-icon {
  color: #909399;
  margin-bottom: 16px;
}

.bulk-dropzone h3 {
  margin: 16px 0 8px;
  font-size: 18px;
  color: #303133;
}

.bulk-dropzone p {
  margin: 8px 0;
  color: #909399;
}

.dropzone-hint {
  font-size: 12px;
  margin-top: 16px !important;
}

.file-list {
  margin-top: 24px;
  animation: slideDown 0.3s ease;
}

@keyframes slideDown {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid #ebeef5;
}

.list-header h4 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.file-items {
  display: flex;
  flex-direction: column;
  gap: 12px;
  max-height: 400px;
  overflow-y: auto;
}

.file-item {
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 12px;
  background: white;
  transition: all 0.3s;
}

.file-item:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.file-item.has-error {
  border-color: #f56c6c;
  background: #fef0f0;
}

.file-item.is-uploaded {
  border-color: #67c23a;
  background: #f0f9ff;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
}

.file-icon {
  color: #409eff;
  flex-shrink: 0;
}

.file-details {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.file-name {
  font-size: 14px;
  font-weight: 500;
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.file-size {
  font-size: 12px;
  color: #909399;
}

.remove-icon {
  cursor: pointer;
  color: #909399;
  transition: color 0.3s;
  flex-shrink: 0;
}

.remove-icon:hover {
  color: #f56c6c;
}

.success-icon {
  flex-shrink: 0;
}

.file-metadata {
  display: grid;
  grid-template-columns: 1fr 1fr auto auto;
  gap: 8px;
}

.meta-input {
  width: 100%;
}

.file-error {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 8px;
  padding: 6px 8px;
  background: #fef0f0;
  border-radius: 4px;
  font-size: 13px;
  color: #f56c6c;
}

.file-progress {
  margin-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.progress-text {
  font-size: 12px;
  color: #606266;
}

.upload-summary {
  margin-top: 24px;
}

.summary-stats {
  display: flex;
  justify-content: center;
  gap: 24px;
  margin: 16px 0;
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
  font-weight: 500;
}

.stat-item.success {
  color: #67c23a;
}

.stat-item.error {
  color: #f56c6c;
}

.failed-list {
  text-align: left;
  margin-top: 16px;
  padding: 16px;
  background: #fef0f0;
  border-radius: 8px;
}

.failed-list h5 {
  margin: 0 0 12px;
  color: #f56c6c;
}

.failed-list ul {
  margin: 0;
  padding-left: 20px;
}

.failed-list li {
  margin: 6px 0;
  font-size: 13px;
  color: #606266;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.fade-enter-active,
.fade-leave-active {
  transition: all 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

.slide-down-enter-active,
.slide-down-leave-active {
  transition: all 0.3s ease;
  max-height: 600px;
  overflow: hidden;
}

.slide-down-enter-from,
.slide-down-leave-to {
  max-height: 0;
  opacity: 0;
}

@media (max-width: 768px) {
  .file-metadata {
    grid-template-columns: 1fr;
  }

  .bulk-dropzone {
    padding: 40px 20px;
  }
}
</style>
