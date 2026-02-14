<template>
  <div class="bulk-doc-import-page">
    <PageHeader
      title="Bulk Document Import"
      subtitle="Upload required documents for multiple suppliers at once"
    />

    <!-- Supplier Selection -->
    <el-card class="selection-card" shadow="never">
      <template #header>
        <div class="card-header">
          <h2>Step 1: Select Suppliers</h2>
        </div>
      </template>

      <div class="supplier-selection">
        <el-select
          v-model="selectedSupplierIds"
          multiple
          filterable
          remote
          reserve-keyword
          placeholder="Search and select suppliers"
          :remote-method="searchSuppliers"
          :loading="loadingSuppliers"
          class="supplier-select"
          size="large"
        >
          <el-option
            v-for="supplier in supplierOptions"
            :key="supplier.id"
            :label="`${supplier.companyName} (${supplier.companyId})`"
            :value="supplier.id"
          >
            <div class="supplier-option">
              <span class="supplier-name">{{ supplier.companyName }}</span>
              <el-tag :type="getStatusType(supplier.status)" size="small">
                {{ supplier.status }}
              </el-tag>
            </div>
          </el-option>
        </el-select>

        <div v-if="selectedSupplierIds.length > 0" class="selected-info">
          <el-icon><info-filled /></el-icon>
          <span>{{ selectedSupplierIds.length }} supplier(s) selected</span>
        </div>
      </div>
    </el-card>

    <!-- Document Upload -->
    <el-card v-if="selectedSupplierIds.length > 0" class="upload-card" shadow="never">
      <template #header>
        <div class="card-header">
          <h2>Step 2: Select Documents to Upload</h2>
          <p class="hint">These documents will be uploaded to all selected suppliers</p>
        </div>
      </template>

      <!-- Default Document Type Selection -->
      <div class="default-type-section">
        <label class="default-type-label">Default Document Type (Optional)</label>
        <el-select
          v-model="defaultDocType"
          placeholder="Select a default document type for all files"
          size="large"
          clearable
          class="default-type-select"
        >
          <el-option
            v-for="type in documentTypes"
            :key="type.value"
            :label="type.label"
            :value="type.value"
          />
        </el-select>
        <p class="hint-text">
          If selected, all uploaded files will be assigned this document type by default. You can
          change individual files later.
        </p>
      </div>

      <!-- File Drop Zone -->
      <div
        class="dropzone"
        :class="{ 'is-dragover': isDragOver, 'has-files': fileItems.length > 0 }"
        @drop.prevent="handleDrop"
        @dragover.prevent="isDragOver = true"
        @dragleave.prevent="isDragOver = false"
      >
        <el-icon :size="64" class="upload-icon"><upload-filled /></el-icon>
        <h3>Drag and drop files here</h3>
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
        <p class="hint">Supported formats: PDF, JPG, PNG, DOC, DOCX (Max 10MB per file)</p>
      </div>

      <!-- File List -->
      <div v-if="fileItems.length > 0" class="file-list">
        <div class="list-header">
          <h4>Selected Documents ({{ fileItems.length }})</h4>
          <el-button size="small" text @click="clearAllFiles">
            <el-icon><delete /></el-icon>
            Clear All
          </el-button>
        </div>

        <div class="file-items">
          <div v-for="(item, index) in fileItems" :key="index" class="file-item">
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

              <el-icon class="remove-icon" @click="removeFile(index)">
                <close />
              </el-icon>
            </div>

            <!-- Document Metadata -->
            <div class="file-metadata">
              <el-select
                v-model="item.docType"
                placeholder="Document Type (Required)"
                size="small"
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
                class="meta-input date-picker"
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
                :disabled-date="(time: Date) => disabledExpiryDate(time, item)"
                class="meta-input date-picker"
              />
            </div>

            <!-- Validation Error -->
            <div v-if="item.error" class="file-error">
              <el-icon><warning-filled /></el-icon>
              <span>{{ item.error }}</span>
            </div>
          </div>
        </div>
      </div>
    </el-card>

    <!-- Upload Summary -->
    <el-card
      v-if="selectedSupplierIds.length > 0 && fileItems.length > 0"
      class="summary-card"
      shadow="never"
    >
      <template #header>
        <div class="card-header">
          <h2>Step 3: Review and Upload</h2>
        </div>
      </template>

      <div class="upload-summary">
        <div class="summary-row">
          <span class="label">Total Suppliers:</span>
          <span class="value">{{ selectedSupplierIds.length }}</span>
        </div>
        <div class="summary-row">
          <span class="label">Total Documents per Supplier:</span>
          <span class="value">{{ fileItems.length }}</span>
        </div>
        <div class="summary-row highlight">
          <span class="label">Total Documents to Upload:</span>
          <span class="value">{{ selectedSupplierIds.length * fileItems.length }}</span>
        </div>
      </div>

      <el-alert type="info" :closable="false" show-icon style="margin-bottom: 20px">
        <template #title>
          Each selected document will be uploaded to all {{ selectedSupplierIds.length }} selected
          supplier(s)
        </template>
      </el-alert>

      <div class="action-buttons">
        <el-button size="large" @click="resetForm">Cancel</el-button>
        <el-button
          type="primary"
          size="large"
          :loading="uploading"
          :disabled="!canUpload"
          @click="startBulkUpload"
        >
          <el-icon v-if="!uploading"><upload /></el-icon>
          {{ uploading ? "Uploading..." : "Start Bulk Upload" }}
        </el-button>
      </div>
    </el-card>

    <!-- Upload Results -->
    <el-card v-if="uploadResults.length > 0" class="results-card" shadow="never">
      <template #header>
        <div class="card-header">
          <h2>Upload Results</h2>
        </div>
      </template>

      <div class="results-summary">
        <el-statistic title="Total Suppliers Processed" :value="uploadResults.length" />
        <el-statistic
          title="Successful Uploads"
          :value="uploadResults.filter((r) => r.successCount > 0).length"
          value-style="color: #67c23a"
        />
        <el-statistic
          title="Failed Uploads"
          :value="uploadResults.filter((r) => r.failedCount > 0).length"
          value-style="color: #f56c6c"
        />
      </div>

      <el-table :data="uploadResults" style="width: 100%; margin-top: 20px">
        <el-table-column prop="supplierName" label="Supplier" min-width="200" />
        <el-table-column prop="successCount" label="Success" width="100" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.successCount > 0" type="success">{{ row.successCount }}</el-tag>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column prop="failedCount" label="Failed" width="100" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.failedCount > 0" type="danger">{{ row.failedCount }}</el-tag>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column label="Status" width="120" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.failedCount === 0" type="success">Complete</el-tag>
            <el-tag v-else-if="row.successCount > 0" type="warning">Partial</el-tag>
            <el-tag v-else type="danger">Failed</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="Details" width="100">
          <template #default="{ row }">
            <el-button v-if="row.failedCount > 0" size="small" text @click="showResultDetails(row)">
              View Errors
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- Error Details Dialog -->
    <el-dialog v-if="errorDialogVisible" v-model="errorDialogVisible" title="Upload Errors" width="600px">
      <div v-if="selectedResult">
        <h4>{{ selectedResult.supplierName }}</h4>
        <el-alert
          v-for="(error, index) in selectedResult.errors"
          :key="index"
          :title="error.filename"
          :description="error.error"
          type="error"
          :closable="false"
          style="margin-bottom: 10px"
        />
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed } from "vue";
import { useRouter } from "vue-router";

import PageHeader from "@/components/layout/PageHeader.vue";
import {
  Back,
  InfoFilled,
  UploadFilled,
  FolderOpened,
  Document,
  Picture,
  Close,
  Delete,
  Upload,
  WarningFilled,
} from "@element-plus/icons-vue";
import { fetchSuppliers } from "@/api/suppliers";
import type { Supplier } from "@/types";


import { useNotification } from "@/composables";
const notification = useNotification();

const router = useRouter();

interface FileItem {
  file: File;
  docType: string;
  category?: string;
  validFrom?: Date | null;
  expiresAt?: Date | null;
  error?: string;
}

interface UploadResult {
  supplierId: number;
  supplierName: string;
  successCount: number;
  failedCount: number;
  errors: Array<{ filename: string; error: string }>;
}

// Supplier Selection
const selectedSupplierIds = ref<number[]>([]);

// Check for pre-selected suppliers from router state
const preSelectedIds = (history.state as any)?.preSelectedSupplierIds;
if (preSelectedIds && Array.isArray(preSelectedIds) && preSelectedIds.length > 0) {
  selectedSupplierIds.value = preSelectedIds;
}
const supplierOptions = ref<Supplier[]>([]);
const loadingSuppliers = ref(false);

// File Upload
const fileInputRef = ref<HTMLInputElement>();
const isDragOver = ref(false);
const fileItems = ref<FileItem[]>([]);
const uploading = ref(false);
const defaultDocType = ref<string>("");

// Upload Results
const uploadResults = ref<UploadResult[]>([]);
const errorDialogVisible = ref(false);
const selectedResult = ref<UploadResult | null>(null);

const documentTypes = [
  { value: "business_license", label: "Business License" },
  { value: "tax_certificate", label: "Tax Registration Certificate" },
  { value: "bank_information", label: "Bank Account Information" },
  { value: "quality_cert", label: "Quality Certification" },
  { value: "iso_cert", label: "ISO Certification" },
  { value: "insurance", label: "Insurance Policy" },
  { value: "contract", label: "Contract" },
  { value: "incoming_packaging_transport_agreement", label: "Incoming Material Packaging and Transportation Agreement" },
  { value: "quality_compensation_agreement", label: "Quality Compensation Agreement" },
  { value: "quality_assurance_agreement", label: "Quality Assurance Agreement" },
  { value: "quality_kpi_targets", label: "Quality KPI Targets" },
  { value: "supplier_handbook_template", label: "Supplier Handbook Template" },
  { value: "supplemental_agreement", label: "Supplemental Agreement" },
  { value: "other", label: "Other Document" },
];

const requiredDocTypes = ["business_license", "tax_certificate", "bank_information"];

const isRequiredDocType = (docType: string) => {
  return requiredDocTypes.includes(docType);
};

const canUpload = computed(() => {
  if (selectedSupplierIds.value.length === 0) return false;
  if (fileItems.value.length === 0) return false;
  if (uploading.value) return false;
  return fileItems.value.every((item) => {
    if (!item.docType) return false;
    if (isRequiredDocType(item.docType) && (!item.validFrom || !item.expiresAt)) return false;
    return true;
  });
});

const getStatusType = (status: string) => {
  const typeMap: Record<string, any> = {
    approved: "success",
    qualified: "success",
    pending: "warning",
    under_review: "info",
    rejected: "danger",
  };
  return typeMap[status] || "info";
};

const searchSuppliers = async (query: string) => {
  if (!query) {
    loadingSuppliers.value = true;
    try {
      const suppliers = await fetchSuppliers({ limit: 50 });
      supplierOptions.value = suppliers;
    } catch (error) {
      console.error("Failed to fetch suppliers:", error);
      notification.error("Failed to load suppliers");
    } finally {
      loadingSuppliers.value = false;
    }
    return;
  }

  loadingSuppliers.value = true;
  try {
    const suppliers = await fetchSuppliers({ q: query, limit: 50 });
    supplierOptions.value = suppliers;
  } catch (error) {
    console.error("Failed to search suppliers:", error);
  } finally {
    loadingSuppliers.value = false;
  }
};

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
  files.forEach((file) => {
    if (!validateFile(file)) return;

    // Use default doc type if set, otherwise try to detect from filename
    const docType = defaultDocType.value || detectDocumentType(file.name);
    fileItems.value.push({
      file,
      docType,
      category: undefined,
      validFrom: null,
      expiresAt: null,
      error: undefined,
    });
  });
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

const clearAllFiles = () => {
  fileItems.value = [];
};

const disabledExpiryDate = (time: Date, item: FileItem) => {
  if (item.validFrom) {
    const validFromTime = new Date(item.validFrom).getTime();
    return time.getTime() <= validFromTime;
  }
  return time.getTime() < Date.now() - 86400000;
};

const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + " " + sizes[i];
};

const startBulkUpload = async () => {
  // Validate all items
  for (const item of fileItems.value) {
    if (!item.docType) {
      item.error = "Document type is required";
      notification.error("Please select document type for all files");
      return;
    }

    if (isRequiredDocType(item.docType)) {
      if (!item.validFrom || !item.expiresAt) {
        item.error = "Valid From and Expiry dates are required for this document type";
        notification.error(`Valid From and Expiry dates are required for ${item.file.name}`);
        return;
      }
    }

    if (item.validFrom && item.expiresAt) {
      if (new Date(item.validFrom) >= new Date(item.expiresAt)) {
        item.error = "Valid from date must be earlier than expiry date";
        notification.error("Date validation failed for " + item.file.name);
        return;
      }
    }
    item.error = undefined;
  }

  uploading.value = true;
  uploadResults.value = [];

  try {
    const { bulkUploadDocuments } = await import("@/api/documents");

    // Upload to each supplier
    for (const supplierId of selectedSupplierIds.value) {
      const supplier = supplierOptions.value.find((s) => s.id === supplierId);
      const supplierName = supplier ? supplier.companyName : `Supplier #${supplierId}`;

      try {
        const documents = fileItems.value.map((item) => ({
          file: item.file,
          docType: item.docType,
          category: item.category,
          validFrom: item.validFrom?.toISOString(),
          expiresAt: item.expiresAt?.toISOString(),
        }));
        const result = await bulkUploadDocuments(supplierId, documents);

        uploadResults.value.push({
          supplierId,
          supplierName,
          successCount: result.success.length,
          failedCount: result.failed.length,
          errors: result.failed,
        });
      } catch (error: any) {
        uploadResults.value.push({
          supplierId,
          supplierName,
          successCount: 0,
          failedCount: fileItems.value.length,
          errors: fileItems.value.map((item) => ({
            filename: item.file.name,
            error: error.message || "Upload failed",
          })),
        });
      }
    }

    const totalSuccess = uploadResults.value.reduce((sum, r) => sum + r.successCount, 0);
    const totalFailed = uploadResults.value.reduce((sum, r) => sum + r.failedCount, 0);

    if (totalFailed === 0) {
      notification.success(`Successfully uploaded ${totalSuccess} documents to all suppliers!`);
    } else {
      notification.warning(`Upload completed: ${totalSuccess} successful, ${totalFailed} failed`);
    }
  } catch (error) {
    console.error("Bulk upload failed:", error);
    notification.error("Bulk upload process failed");
  } finally {
    uploading.value = false;
  }
};

const showResultDetails = (result: UploadResult) => {
  selectedResult.value = result;
  errorDialogVisible.value = true;
};

const resetForm = () => {
  selectedSupplierIds.value = [];
  fileItems.value = [];
  uploadResults.value = [];
};

// Load initial suppliers and pre-selected suppliers
const loadInitialData = async () => {
  await searchSuppliers("");

  // Load details for pre-selected suppliers if they're not in the initial list
  if (preSelectedIds && Array.isArray(preSelectedIds) && preSelectedIds.length > 0) {
    for (const id of preSelectedIds) {
      if (!supplierOptions.value.find((s) => s.id === id)) {
        try {
          const suppliers = await fetchSuppliers({ id });
          if (suppliers.length > 0) {
            supplierOptions.value.push(suppliers[0]);
          }
        } catch (error) {
          console.error(`Failed to load supplier ${id}:`, error);
        }
      }
    }
  }
};

loadInitialData();




</script>

<style scoped>
.bulk-doc-import-page {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}


.selection-card,
.upload-card,
.summary-card,
.results-card {
  margin-bottom: 24px;
}

.card-header h2 {
  font-size: 18px;
  font-weight: 600;
  margin: 0;
  color: #303133;
}

.card-header .hint {
  margin: 8px 0 0 0;
  font-size: 14px;
  color: #909399;
}

.supplier-selection {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.supplier-select {
  width: 100%;
}

.supplier-option {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.supplier-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.selected-info {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #409eff;
  font-size: 14px;
}

.default-type-section {
  margin-bottom: 24px;
  padding: 20px;
  background: #f5f7fa;
  border-radius: 8px;
}

.default-type-label {
  display: block;
  font-size: 14px;
  font-weight: 600;
  color: #303133;
  margin-bottom: 12px;
}

.default-type-select {
  width: 100%;
  max-width: 600px;
}

.hint-text {
  margin: 8px 0 0 0;
  font-size: 13px;
  color: #909399;
  line-height: 1.5;
}

.dropzone {
  border: 2px dashed #dcdfe6;
  border-radius: 8px;
  padding: 60px 40px;
  text-align: center;
  background: #fafafa;
  transition: all 0.3s;
  cursor: pointer;
  margin-bottom: 24px;
}

.dropzone:hover,
.dropzone.is-dragover {
  border-color: #409eff;
  background: #f0f7ff;
}

.upload-icon {
  color: #909399;
  margin-bottom: 16px;
}

.dropzone h3 {
  margin: 16px 0 8px;
  font-size: 18px;
  color: #303133;
}

.dropzone p {
  margin: 8px 0;
  color: #909399;
}

.dropzone .hint {
  font-size: 12px;
  margin-top: 16px !important;
}

.file-list {
  margin-top: 24px;
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

.file-metadata {
  display: grid;
  grid-template-columns: 1fr 1fr 180px 180px;
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
  padding: 8px;
  background: #fef0f0;
  border-radius: 4px;
  font-size: 13px;
  color: #f56c6c;
}

.upload-summary {
  margin-bottom: 20px;
}

.summary-row {
  display: flex;
  justify-content: space-between;
  padding: 12px 0;
  border-bottom: 1px solid #ebeef5;
}

.summary-row.highlight {
  font-weight: 600;
  font-size: 16px;
  color: #409eff;
}

.summary-row .label {
  color: #606266;
}

.summary-row .value {
  font-weight: 500;
}

.action-buttons {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 20px;
}

.results-summary {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
  margin-bottom: 20px;
}

@media (max-width: 768px) {
  .file-metadata {
    grid-template-columns: 1fr;
  }

  .dropzone {
    padding: 40px 20px;
  }
}
</style>
