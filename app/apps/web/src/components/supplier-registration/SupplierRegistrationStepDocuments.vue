<template>
  <section class="wizard-step">
    <h2 class="step-title">{{ title }}</h2>
    <p class="step-description">{{ description }}</p>

    <div class="documents-intro">
      <p>
        {{
          translateOr(
            "supplierRegistration.documents.instructions",
            "Please upload clear copies of the required certificates so we can verify your registration.",
          )
        }}
      </p>
      <p class="documents-hint">
        {{
          translateOr(
            "supplierRegistration.documents.acceptedTypes",
            "Accepted formats: PDF, JPG, PNG. Maximum size 10MB per file.",
          )
        }}
      </p>
    </div>

    <div class="documents-grid">
      <div class="document-card">
        <h3>
          {{
            translateOr(
              "supplierRegistration.documents.businessLicense.title",
              "Business License",
            )
          }}
        </h3>
        <p class="document-hint">
          {{
            translateOr(
              "supplierRegistration.documents.businessLicense.hint",
              "Government issued business registration certificate.",
            )
          }}
        </p>
        <div class="document-actions">
          <input
            ref="businessInput"
            type="file"
            class="file-input"
            :accept="acceptAttribute"
            @change="onFileSelected('businessLicenseFile', $event)"
          />
          <el-button type="primary" @click="triggerFileDialog(businessInput)" :loading="isLoading">
            {{
              form.businessLicenseFile
                ? translateOr(
                    "supplierRegistration.documents.actions.replace",
                    "Replace File",
                  )
                : translateOr("supplierRegistration.documents.actions.upload", "Upload File")
            }}
          </el-button>
          <el-button
            v-if="form.businessLicenseFile"
            type="danger"
            text
            size="small"
            @click="removeFile('businessLicenseFile')"
          >
            {{ translateOr("common.remove", "Remove") }}
          </el-button>
        </div>
        <div v-if="form.businessLicenseFile" class="file-meta">
          <strong>{{ form.businessLicenseFile.name }}</strong>
          <span>({{ formatSize(form.businessLicenseFile.size) }})</span>
        </div>
        <p v-else class="file-empty">
          {{
            translateOr(
              "supplierRegistration.documents.noFile",
              "No file selected yet.",
            )
          }}
        </p>
      </div>

      <div class="document-card">
        <h3>
          {{
            translateOr(
              "supplierRegistration.documents.bankCertificate.title",
              "Bank Account Certificate",
            )
          }}
        </h3>
        <p class="document-hint">
          {{
            translateOr(
              "supplierRegistration.documents.bankCertificate.hint",
              "Bank-issued account opening information or equivalent proof.",
            )
          }}
        </p>
        <div class="document-actions">
          <input
            ref="bankInput"
            type="file"
            class="file-input"
            :accept="acceptAttribute"
            @change="onFileSelected('bankAccountFile', $event)"
          />
          <el-button type="primary" @click="triggerFileDialog(bankInput)" :loading="isLoading">
            {{
              form.bankAccountFile
                ? translateOr(
                    "supplierRegistration.documents.actions.replace",
                    "Replace File",
                  )
                : translateOr("supplierRegistration.documents.actions.upload", "Upload File")
            }}
          </el-button>
          <el-button
            v-if="form.bankAccountFile"
            type="danger"
            text
            size="small"
            @click="removeFile('bankAccountFile')"
          >
            {{ translateOr("common.remove", "Remove") }}
          </el-button>
        </div>
        <div v-if="form.bankAccountFile" class="file-meta">
          <strong>{{ form.bankAccountFile.name }}</strong>
          <span>({{ formatSize(form.bankAccountFile.size) }})</span>
        </div>
        <p v-else class="file-empty">
          {{
            translateOr(
              "supplierRegistration.documents.noFile",
              "No file selected yet.",
            )
          }}
        </p>
      </div>
    </div>

    <SupplierRegistrationPreviewPanel
      class="preview-container"
      :translate-or="translateOr"
      :preview-loading="previewLoading"
      :preview-error="previewError"
      :approval-preview="approvalPreview"
      :format-eta="formatEta"
      :request-preview="requestPreview"
    />
  </section>
</template>

<script setup lang="ts">




import { ref, onMounted, onUnmounted, nextTick } from "vue";

import type { ApprovalPreview } from "@/api/suppliers";
import SupplierRegistrationPreviewPanel from "./SupplierRegistrationPreviewPanel.vue";
import type { RegistrationDocumentUpload, SupplierRegistrationFormState } from "./formState";


import { useNotification } from "@/composables";

const notification = useNotification();
const MAX_SIZE = 10 * 1024 * 1024;
const ACCEPTED_MIME = new Set([
  "application/pdf",
  "image/jpeg",
  "image/png",
  "image/jpg",
]);
const ACCEPTED_EXTENSIONS = [".pdf", ".jpg", ".jpeg", ".png"];

const props = defineProps<{
  title: string;
  description: string;
  form: SupplierRegistrationFormState;
  translateOr: (key: string, fallback: string) => string;
  approvalPreview: ApprovalPreview | null;
  previewLoading: boolean;
  previewError: string | null;
  formatEta: (value: string) => string;
  requestPreview: () => void;
}>();
const emit = defineEmits<{
  (e: "update:form", value: SupplierRegistrationFormState): void;
}>();

type DocumentField = "businessLicenseFile" | "bankAccountFile";

const businessInput = ref<HTMLInputElement | null>(null);
const bankInput = ref<HTMLInputElement | null>(null);
const isLoading = ref(false);
const isMounted = ref(false);

const acceptAttribute = ACCEPTED_EXTENSIONS.join(",");

const ensureDomReady = async () => {
  if (!isMounted.value || typeof document === "undefined") {
    return false;
  }
  await nextTick();
  return true;
};

onMounted(() => {
  isMounted.value = true;
});

onUnmounted(() => {
  isMounted.value = false;
});

const guessMimeFromName = (name: string) => {
  const lower = name.toLowerCase();
  if (lower.endsWith(".pdf")) {
    return "application/pdf";
  }
  if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) {
    return "image/jpeg";
  }
  if (lower.endsWith(".png")) {
    return "image/png";
  }
  return "application/octet-stream";
};

const readAsDataUrl = (file: File) =>
  new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === "string") {
        resolve(reader.result);
      } else {
        reject(new Error("Failed to read file"));
      }
    };
    reader.onerror = () => reject(reader.error ?? new Error("Failed to read file"));
    reader.readAsDataURL(file);
  });

const formatSize = (size?: number | null) => {
  if (!size || Number.isNaN(size)) {
    return "0 KB";
  }
  if (size < 1024) {
    return `${size} B`;
  }
  if (size < 1024 * 1024) {
    return `${(size / 1024).toFixed(1)} KB`;
  }
  return `${(size / (1024 * 1024)).toFixed(1)} MB`;
};

const triggerFileDialog = async (inputEl: HTMLInputElement | null) => {
  if (!(await ensureDomReady())) {
    return;
  }
  inputEl?.click();
};

const updateFormField = (
  field: DocumentField,
  value: RegistrationDocumentUpload | null,
) => {
  emit("update:form", { ...props.form, [field]: value });
};

const removeFile = (field: DocumentField) => {
  updateFormField(field, null);
};

const isTypeAllowed = (file: File) => {
  if (file.type && ACCEPTED_MIME.has(file.type)) {
    return true;
  }
  const name = file.name.toLowerCase();
  return ACCEPTED_EXTENSIONS.some((ext) => name.endsWith(ext));
};

const onFileSelected = async (field: DocumentField, event: Event) => {
  const input = event.target as HTMLInputElement | null;
  const file = input?.files?.[0] ?? null;
  if (input) {
    input.value = "";
  }
  if (!file) {
    return;
  }

  if (file.size > MAX_SIZE) {
    notification.error(
      props.translateOr(
        "supplierRegistration.documents.errors.fileTooLarge",
        "File size exceeds 10MB limit.",
      ),
    );
    return;
  }
  if (!isTypeAllowed(file)) {
    notification.error(
      props.translateOr(
        "supplierRegistration.documents.errors.unsupportedType",
        "Unsupported file type. Please upload a PDF or image.",
      ),
    );
    return;
  }

  try {
    isLoading.value = true;
    const content = await readAsDataUrl(file);
    const upload: RegistrationDocumentUpload = {
      name: file.name,
      type: file.type || guessMimeFromName(file.name),
      size: file.size,
      content,
    };
    updateFormField(field, upload);
    notification.success(
      props.translateOr("supplierRegistration.documents.success", "File ready for submission."),
    );
  } catch (error) {
    console.error("Failed to read file", error);
    notification.error(
      props.translateOr(
        "supplierRegistration.documents.errors.readFailed",
        "Unable to read the selected file. Please try again.",
      ),
    );
  } finally {
    isLoading.value = false;
  }
};




</script>

<style scoped>
.documents-intro {
  margin-bottom: 20px;
  color: rgba(48, 49, 51, 0.88);
}

.documents-hint {
  margin: 4px 0 0;
  font-size: 13px;
  color: #909399;
}

.documents-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 20px;
  margin-bottom: 32px;
}

.document-card {
  background: rgba(255, 255, 255, 0.9);
  border-radius: 16px;
  padding: 20px;
  box-shadow: 0 10px 26px rgba(31, 41, 55, 0.12);
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.document-card h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
}

.document-hint {
  margin: 0;
  color: #606266;
  font-size: 14px;
}

.document-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.file-input {
  display: none;
}

.file-meta {
  font-size: 13px;
  color: #606266;
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.file-empty {
  font-size: 13px;
  color: #a0a3ad;
  margin: 0;
}

@media (max-width: 768px) {
  .documents-grid {
    grid-template-columns: 1fr;
  }
}
</style>
