<template>
  <div class="document-upload-widget">
    <div class="widget-header">
      <h3>Documents</h3>
      <el-button type="primary" size="small" @click="showBulkUpload = true">
        Bulk Upload
      </el-button>
    </div>

    <!-- Missing Documents Alert -->
    <el-alert
      v-if="missingDocuments.length > 0"
      type="warning"
      :closable="false"
      show-icon
      class="missing-alert"
    >
      <template #title> {{ missingDocuments.length }} required document(s) missing </template>
      <ul class="missing-list">
        <li v-for="doc in missingDocuments" :key="doc.type">
          {{ doc.label }}
        </li>
      </ul>
    </el-alert>

    <!-- Upload Area -->
    <div
      class="upload-dropzone"
      :class="{ 'is-dragover': isDragOver }"
      @drop.prevent="handleDrop"
      @dragover.prevent="isDragOver = true"
      @dragleave.prevent="isDragOver = false"
    >
      <el-icon :size="48" class="upload-icon"><upload-filled /></el-icon>
      <p class="upload-text">Drag and drop files here</p>
      <p class="upload-hint">or</p>
      <el-button type="primary" @click="triggerFileInput"> Browse Files </el-button>
      <input
        ref="fileInput"
        type="file"
        multiple
        accept=".pdf,.jpg,.jpeg,.png,.doc,.docx"
        style="display: none"
        @change="handleFileSelect"
      />
      <p class="upload-limit">Max file size: 10MB. Supported: PDF, JPG, PNG, DOC, DOCX</p>
    </div>

    <!-- Upload Form (when file selected) -->
    <el-dialog
      v-model="showUploadDialog"
      title="Upload Document"
      width="500px"
      :close-on-click-modal="false"
    >
      <el-form :model="uploadForm" label-width="140px">
        <el-form-item label="File">
          <div class="selected-file">
            <el-icon><document /></el-icon>
            <span>{{ uploadForm.file?.name }}</span>
            <span class="file-size">({{ formatFileSize(uploadForm.file?.size) }})</span>
          </div>
        </el-form-item>

        <el-form-item label="Document Type" required>
          <el-select v-model="uploadForm.docType" placeholder="Select document type">
            <el-option
              v-for="type in documentTypes"
              :key="type.value"
              :label="type.label"
              :value="type.value"
            />
          </el-select>
        </el-form-item>

        <el-form-item label="Category">
          <el-select v-model="uploadForm.category" placeholder="Select category (optional)">
            <el-option label="Compliance" value="compliance" />
            <el-option label="Financial" value="finance" />
            <el-option label="Tax" value="tax" />
            <el-option label="Quality" value="quality" />
            <el-option label="Legal" value="legal" />
            <el-option label="Other" value="other" />
          </el-select>
        </el-form-item>

        <el-form-item
          :label="isRequiredDocType ? 'Valid From Date' : 'Valid From Date'"
          :required="isRequiredDocType"
        >
          <el-date-picker
            v-model="uploadForm.validFrom"
            type="date"
            :placeholder="
              isRequiredDocType
                ? 'Select valid from date (required)'
                : 'Select valid from date (optional)'
            "
            :disabled-date="disabledDate"
          />
        </el-form-item>

        <el-form-item
          :label="isRequiredDocType ? 'Expiry Date' : 'Expiry Date'"
          :required="isRequiredDocType"
        >
          <el-date-picker
            v-model="uploadForm.expiresAt"
            type="date"
            :placeholder="
              isRequiredDocType ? 'Select expiry date (required)' : 'Select expiry date (optional)'
            "
            :disabled-date="disabledExpiryDate"
          />
        </el-form-item>

        <el-form-item label="Notes">
          <el-input
            v-model="uploadForm.notes"
            type="textarea"
            :rows="3"
            placeholder="Additional notes (optional)"
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="closeUploadDialog">Cancel</el-button>
        <el-button
          type="primary"
          :loading="uploading"
          :disabled="!uploadForm.docType"
          @click="submitUpload"
        >
          Upload
        </el-button>
      </template>
    </el-dialog>

    <!-- Existing Documents List -->
    <div v-if="documents.length > 0" class="documents-list">
      <h4>Uploaded Documents ({{ documents.length }})</h4>
      <div class="document-grid">
        <div
          v-for="doc in sortedDocuments"
          :key="doc.id"
          class="document-card"
          :class="{ 'is-expired': isExpired(doc), 'is-expiring': isExpiringSoon(doc) }"
        >
          <div class="doc-header">
            <el-icon :size="24"><document /></el-icon>
            <div class="doc-info">
              <span class="doc-name">{{ doc.originalName || doc.docType }}</span>
              <span class="doc-type">{{ getDocumentTypeLabel(doc.docType) }}</span>
            </div>
            <el-dropdown trigger="click" @command="handleDocAction($event, doc)">
              <el-icon class="doc-menu"><more-filled /></el-icon>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="download">Download</el-dropdown-item>
                  <el-dropdown-item command="renew" v-if="isExpired(doc) || isExpiringSoon(doc)">
                    Renew
                  </el-dropdown-item>
                  <el-dropdown-item command="confirmDelete" divided>Delete</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </div>

          <div class="doc-meta">
            <div v-if="doc.validFrom" class="doc-validity">
              <el-icon><calendar /></el-icon>
              <span>Valid From: {{ formatDocumentDate(doc.validFrom as string | null) }}</span>
            </div>
            <div v-if="doc.expiresAt" class="doc-expiry">
              <el-icon><calendar /></el-icon>
              <span :class="{ 'text-danger': isExpired(doc), 'text-warning': isExpiringSoon(doc) }">
                {{ isExpired(doc) ? "Expired" : "Expires" }}:
                {{ formatDocumentDate(doc.expiresAt as string | null) }}
              </span>
            </div>
            <div class="doc-upload-info">
              <span>Uploaded {{ formatDocumentDate(doc.uploadedAt as string | null) }}</span>
              <span v-if="doc.uploadedBy"> by {{ doc.uploadedBy }}</span>
            </div>
          </div>

          <div v-if="doc.status" class="doc-status">
            <el-tag :type="getStatusType(doc.status)" size="small">
              {{ doc.status }}
            </el-tag>
          </div>
        </div>
      </div>
    </div>

    <div v-else-if="!loading" class="empty-state">
      <el-empty description="No documents uploaded yet" />
    </div>

    <!-- Bulk Upload Dialog -->
    <BulkDocumentUpload
      v-model="showBulkUpload"
      :supplier-id="supplierId"
      @success="handleBulkUploadSuccess"
    />

    <!-- Delete Confirmation Dialog -->
    <el-dialog
      v-model="showDeleteDialog"
      title="Delete Document"
      width="400px"
      :close-on-click-modal="false"
    >
      <p style="margin-bottom: 20px; color: #606266;">
        Are you sure you want to delete "{{ docToDelete?.originalName || docToDelete?.docType }}"?
      </p>
      <p style="margin-bottom: 20px; color: #909399; font-size: 14px;">
        Slide to confirm deletion:
      </p>
      <SlideConfirmButton
        :slide-text="$t('common.slideToConfirm')"
        :confirmed-text="$t('common.confirmed')"
        track-color="#f56c6c"
        @confirm="handleDelete"
      />
      <template #footer>
        <el-button @click="showDeleteDialog = false">Cancel</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, watch, onMounted, onUnmounted, nextTick } from "vue";

import { UploadFilled, Document, MoreFilled, Calendar } from "@element-plus/icons-vue";
import { uploadDocument, downloadDocument, deleteDocument, renewDocument } from "@/api/documents";
import BulkDocumentUpload from "./BulkDocumentUpload.vue";
import SlideConfirmButton from "./common/SlideConfirmButton.vue";
import type { SupplierDocument } from "@/types";


import { useNotification } from "@/composables";

const notification = useNotification();
interface Props {
  supplierId: number;
  documents: SupplierDocument[];
  missingDocuments: Array<{ type: string; label: string }>;
  loading?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
});

const emit = defineEmits<{
  (e: "refresh"): void;
}>();

const fileInput = ref<HTMLInputElement>();
const isDragOver = ref(false);
const showUploadDialog = ref(false);
const uploading = ref(false);
const showBulkUpload = ref(false);
const isMounted = ref(false);

const ensureDomReady = async () => {
  if (!isMounted.value || typeof document === "undefined" || typeof window === "undefined") {
    return false;
  }
  await nextTick();
  return true;
};

const uploadForm = ref({
  file: null as File | null,
  docType: "",
  category: "",
  validFrom: null as Date | null,
  expiresAt: null as Date | null,
  notes: "",
});

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

const isRequiredDocType = computed(() => {
  return requiredDocTypes.includes(uploadForm.value.docType);
});

const sortedDocuments = computed(() => {
  return [...props.documents].sort((a, b) => {
    // Expired first, then expiring soon, then by upload date
    const aExpired = isExpired(a);
    const bExpired = isExpired(b);
    if (aExpired !== bExpired) return aExpired ? -1 : 1;

    const aExpiring = isExpiringSoon(a);
    const bExpiring = isExpiringSoon(b);
    if (aExpiring !== bExpiring) return aExpiring ? -1 : 1;

    return new Date(b.uploadedAt).getTime() - new Date(a.uploadedAt).getTime();
  });
});

onMounted(() => {
  isMounted.value = true;
});

onUnmounted(() => {
  isMounted.value = false;
});

const triggerFileInput = async () => {
  if (!(await ensureDomReady())) {
    return;
  }
  fileInput.value?.click();
};

const handleFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files.length > 0) {
    handleFiles(Array.from(target.files));
  }
};

const handleDrop = (event: DragEvent) => {
  isDragOver.value = false;
  if (event.dataTransfer?.files) {
    handleFiles(Array.from(event.dataTransfer.files));
  }
};

const handleFiles = (files: File[]) => {
  if (files.length === 1) {
    const file = files[0];
    if (!validateFile(file)) return;
    uploadForm.value.file = file;
    // Auto-detect document type from filename
    uploadForm.value.docType = detectDocumentType(file.name);
    showUploadDialog.value = true;
  } else if (files.length > 1) {
    // Open bulk upload for multiple files
    showBulkUpload.value = true;
  }
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
    notification.error(`File size exceeds 10MB limit: ${file.name}`);
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
  if (lower.includes("license") || lower.includes("business")) return "business_license";
  if (lower.includes("tax")) return "tax_certificate";
  if (lower.includes("bank")) return "bank_information";
  if (lower.includes("quality")) return "quality_cert";
  if (lower.includes("iso")) return "iso_cert";
  if (lower.includes("insurance")) return "insurance";
  if (lower.includes("contract")) return "contract";
  return "";
};

const submitUpload = async () => {
  if (!uploadForm.value.file || !uploadForm.value.docType) {
    notification.warning("Please select file and document type");
    return;
  }

  // Validate dates for required document types
  if (isRequiredDocType.value) {
    if (!uploadForm.value.validFrom || !uploadForm.value.expiresAt) {
      notification.error("Valid From and Expiry dates are required for this document type");
      return;
    }
  }

  // Validate date range if both dates are provided
  if (uploadForm.value.validFrom && uploadForm.value.expiresAt) {
    if (new Date(uploadForm.value.validFrom) >= new Date(uploadForm.value.expiresAt)) {
      notification.error("Valid from date must be earlier than expiry date");
      return;
    }
  }

  uploading.value = true;
  try {
    await uploadDocument(props.supplierId, {
      file: uploadForm.value.file,
      docType: uploadForm.value.docType,
      category: uploadForm.value.category || undefined,
      validFrom: uploadForm.value.validFrom?.toISOString() || undefined,
      expiresAt: uploadForm.value.expiresAt?.toISOString() || undefined,
      notes: uploadForm.value.notes || undefined,
    });

    notification.success("Document uploaded successfully");
    closeUploadDialog();
    emit("refresh");
  } catch (error) {
    console.error("Upload failed:", error);
    notification.error(error instanceof Error ? error.message : "Failed to upload document");
  } finally {
    uploading.value = false;
  }
};

const closeUploadDialog = async () => {
  showUploadDialog.value = false;
  uploadForm.value = {
    file: null,
    docType: "",
    category: "",
    validFrom: null,
    expiresAt: null,
    notes: "",
  };
  if (await ensureDomReady()) {
    if (fileInput.value) {
      fileInput.value.value = "";
    }
  }
};

const handleDocAction = async (command: string, doc: SupplierDocument) => {
  switch (command) {
    case "download":
      await handleDownload(doc);
      break;
    case "renew":
      await handleRenew(doc);
      break;
    case "confirmDelete":
      confirmDelete(doc);
      break;
  }
};

const handleDownload = async (doc: SupplierDocument) => {
  if (!(await ensureDomReady())) {
    return;
  }
  try {
    const blob = await downloadDocument(props.supplierId, doc.id);
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = doc.originalName || `document-${doc.id}.pdf`;
    a.click();
    window.URL.revokeObjectURL(url);
  } catch (error) {
    notification.error("Failed to download document");
  }
};

const handleRenew = async (doc: SupplierDocument) => {
  if (!(await ensureDomReady())) {
    return;
  }
  const input = document.createElement("input");
  input.type = "file";
  input.accept = ".pdf,.jpg,.jpeg,.png,.doc,.docx";
  input.onchange = async (e: Event) => {
    const target = e.target as HTMLInputElement;
    if (target.files && target.files[0]) {
      const file = target.files[0];
      if (!validateFile(file)) return;

      try {
        await renewDocument(props.supplierId, doc.id, file);
        notification.success("Document renewed successfully");
        emit("refresh");
      } catch (error) {
        notification.error("Failed to renew document");
      }
    }
  };
  input.click();
};

const docToDelete = ref<SupplierDocument | null>(null);
const showDeleteDialog = ref(false);

const confirmDelete = (doc: SupplierDocument) => {
  docToDelete.value = doc;
  showDeleteDialog.value = true;
};

const handleDelete = async () => {
  if (!docToDelete.value) return;

  try {
    await deleteDocument(props.supplierId, docToDelete.value.id);
    notification.success("Document deleted successfully");
    showDeleteDialog.value = false;
    docToDelete.value = null;
    emit("refresh");
  } catch (error) {
    notification.error("Failed to delete document");
  }
};

const handleBulkUploadSuccess = () => {
  showBulkUpload.value = false;
  emit("refresh");
};

const isExpired = (doc: SupplierDocument): boolean => {
  if (!doc.expiresAt) return false;
  return new Date(doc.expiresAt) < new Date();
};

const isExpiringSoon = (doc: SupplierDocument): boolean => {
  if (!doc.expiresAt || isExpired(doc)) return false;
  const daysUntilExpiry = Math.ceil(
    (new Date(doc.expiresAt).getTime() - Date.now()) / (1000 * 60 * 60 * 24),
  );
  return daysUntilExpiry <= 30;
};

const disabledDate = (time: Date) => {
  return time.getTime() < Date.now() - 86400000; // Can't select dates before today
};

const disabledExpiryDate = (time: Date) => {
  if (uploadForm.value.validFrom) {
    // Expiry date must be after valid from date
    const validFromTime = new Date(uploadForm.value.validFrom).getTime();
    return time.getTime() <= validFromTime;
  }
  return time.getTime() < Date.now() - 86400000; // Can't select dates before today
};

const formatDocumentDate = (date?: string | null): string => {
  if (!date) {
    return "N/A";
  }
  const parsed = new Date(date);
  if (Number.isNaN(parsed.getTime())) {
    return "N/A";
  }
  return parsed.toLocaleDateString();
};

const formatFileSize = (bytes?: number): string => {
  if (!bytes) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + " " + sizes[i];
};

const getDocumentTypeLabel = (type?: string | null): string => {
  const found = documentTypes.find((t) => t.value === type);
  if (found?.label) {
    return found.label;
  }
  if (type) {
    return type;
  }
  return "Unknown";
};

const getStatusType = (status: string): "success" | "warning" | "danger" | "info" => {
  switch (status) {
    case "approved":
      return "success";
    case "pending":
      return "warning";
    case "rejected":
      return "danger";
    default:
      return "info";
  }
};




</script>

<style scoped>
.document-upload-widget {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.widget-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.widget-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
}

.missing-alert {
  margin: 0;
}

.missing-list {
  margin: 8px 0 0;
  padding-left: 20px;
}

.missing-list li {
  margin: 4px 0;
}

.upload-dropzone {
  border: 2px dashed #dcdfe6;
  border-radius: 12px;
  padding: 40px 20px;
  text-align: center;
  transition: all 0.3s;
  background: #fafafa;
}

.upload-dropzone:hover,
.upload-dropzone.is-dragover {
  border-color: #409eff;
  background: #f0f7ff;
}

.upload-icon {
  color: #909399;
  margin-bottom: 12px;
}

.upload-text {
  font-size: 16px;
  color: #303133;
  margin: 8px 0;
}

.upload-hint {
  font-size: 14px;
  color: #909399;
  margin: 8px 0 16px;
}

.upload-limit {
  font-size: 12px;
  color: #909399;
  margin: 16px 0 0;
}

.selected-file {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: #f5f7fa;
  border-radius: 6px;
}

.file-size {
  color: #909399;
  font-size: 12px;
}

.documents-list h4 {
  margin: 0 0 16px;
  font-size: 16px;
  font-weight: 600;
}

.document-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}

.document-card {
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  padding: 16px;
  background: #fff;
  transition: all 0.3s;
}

.document-card:hover {
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.document-card.is-expired {
  border-color: #f56c6c;
  background: #fef0f0;
}

.document-card.is-expiring {
  border-color: #e6a23c;
  background: #fdf6ec;
}

.doc-header {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  margin-bottom: 12px;
}

.doc-info {
  flex: 1;
  min-width: 0;
}

.doc-name {
  display: block;
  font-weight: 500;
  font-size: 14px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.doc-type {
  display: block;
  font-size: 12px;
  color: #909399;
  margin-top: 2px;
}

.doc-menu {
  cursor: pointer;
  color: #909399;
}

.doc-menu:hover {
  color: #409eff;
}

.doc-meta {
  display: flex;
  flex-direction: column;
  gap: 8px;
  font-size: 12px;
  color: #606266;
}

.doc-validity {
  display: flex;
  align-items: center;
  gap: 4px;
  color: #67c23a;
}

.doc-expiry {
  display: flex;
  align-items: center;
  gap: 4px;
}

.text-danger {
  color: #f56c6c;
  font-weight: 500;
}

.text-warning {
  color: #e6a23c;
  font-weight: 500;
}

.doc-upload-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  color: #909399;
}

.doc-status {
  margin-top: 12px;
}

.empty-state {
  padding: 40px 20px;
  text-align: center;
}
</style>
