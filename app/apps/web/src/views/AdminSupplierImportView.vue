<template>
  <div class="import-page">
    <PageHeader
      :title="t('adminSupplierImport.title')"
      :subtitle="t('adminSupplierImport.subtitle')"
    />

    <section class="card upload-card">
      <h2>{{ t("adminSupplierImport.upload.title") }}</h2>
      <p class="card-hint">{{ t("adminSupplierImport.upload.supportedFormats") }}</p>
      <ul class="field-list">
        <li v-for="field in requiredColumns" :key="field.key" class="field-item">
          <span class="field-name">{{ field.label }}</span>
          <span class="field-note">{{ field.note }}</span>
        </li>
      </ul>

      <div class="upload-controls">
        <label class="upload-button">
          <input type="file" accept=".xls,.xlsx" @change="onFileChange" />
          {{ t("adminSupplierImport.upload.selectFile") }}
        </label>
        <span class="file-name">{{ selectedFileName }}</span>
        <button
          type="button"
          class="primary-button"
          :disabled="!selectedFile || uploading"
          @click="submitImport"
        >
          {{
            uploading
              ? t("adminSupplierImport.upload.importing")
              : t("adminSupplierImport.upload.start")
          }}
        </button>
        <button
          v-if="selectedFile"
          type="button"
          class="secondary-button"
          :disabled="uploading"
          @click="clearSelection"
        >
          {{ t("common.clear") }}
        </button>
      </div>
      <p class="card-hint">
        {{ t("adminSupplierImport.upload.importNote") }}
      </p>
    </section>

    <section v-if="summary" class="card results-card">
      <header class="section-header">
        <div>
          <h2>{{ t("adminSupplierImport.summary.title") }}</h2>
          <p class="section-subtitle">
            {{ summary.sheetName || t("adminSupplierImport.summary.untitledSheet") }}
          </p>
        </div>
        <span v-if="lastRunTimestamp" class="timestamp">
          {{ t("adminSupplierImport.summary.runAt", { time: lastRunTimestamp }) }}
        </span>
      </header>

      <div class="summary-grid">
        <div class="summary-item">
          <span class="summary-label">{{
            t("adminSupplierImport.summary.stats.rowsScanned")
          }}</span>
          <span class="summary-value">{{ summary.scannedRows }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-label">{{ t("adminSupplierImport.summary.stats.imported") }}</span>
          <span class="summary-value success">{{ summary.importedRows }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-label">{{
            t("adminSupplierImport.summary.stats.newSuppliers")
          }}</span>
          <span class="summary-value">{{ summary.created }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-label">{{ t("adminSupplierImport.summary.stats.updated") }}</span>
          <span class="summary-value">{{ summary.updated }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-label">{{ t("adminSupplierImport.summary.stats.skipped") }}</span>
          <span class="summary-value warning">{{ summary.skipped }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-label">{{
            t("adminSupplierImport.summary.stats.passwordsReset")
          }}</span>
          <span class="summary-value">{{ summary.passwordResets }}</span>
        </div>
      </div>
      <p class="card-hint">
        {{ t("adminSupplierImport.summary.passwordHint") }}
      </p>

      <div v-if="results.length" class="table-block">
        <h3>{{ t("adminSupplierImport.summary.successTable.title") }}</h3>
        <table>
          <thead>
            <tr>
              <th>{{ t("adminSupplierImport.summary.successTable.headers.excelRow") }}</th>
              <th>{{ t("adminSupplierImport.summary.successTable.headers.action") }}</th>
              <th>{{ t("adminSupplierImport.summary.successTable.headers.supplierCode") }}</th>
              <th>{{ t("adminSupplierImport.summary.successTable.headers.supplierName") }}</th>
              <th>{{ t("adminSupplierImport.summary.successTable.headers.systemId") }}</th>
              <th>{{ t("adminSupplierImport.summary.successTable.headers.defaultPassword") }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in results" :key="`${row.action}-${row.rowNumber}-${row.supplierId}`">
              <td>{{ row.rowNumber }}</td>
              <td>
                <span :class="['tag', row.action === 'created' ? 'tag-success' : 'tag-info']">
                  {{
                    row.action === "created"
                      ? t("adminSupplierImport.summary.successTable.tags.created")
                      : t("adminSupplierImport.summary.successTable.tags.updated")
                  }}
                </span>
              </td>
              <td>{{ row.companyId }}</td>
              <td>{{ row.companyName }}</td>
              <td>#{{ row.supplierId }}</td>
              <td>{{ row.defaultPassword ?? "--" }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="summary.errors.length" class="table-block error-block">
        <h3>{{ t("adminSupplierImport.summary.errors.title") }}</h3>
        <table>
          <thead>
            <tr>
              <th>{{ t("adminSupplierImport.summary.errors.headers.excelRow") }}</th>
              <th>{{ t("adminSupplierImport.summary.errors.headers.reason") }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="error in summary.errors" :key="`${error.row}-${error.message}`">
              <td>{{ error.row }}</td>
              <td>{{ error.message }}</td>
            </tr>
          </tbody>
        </table>
        <p class="error-hint">
          {{ t("adminSupplierImport.summary.errors.hint") }}
        </p>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">




import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useAuthStore } from "@/stores/auth";
import { useRouter } from "vue-router";

import PageHeader from "@/components/layout/PageHeader.vue";
import { importSuppliersFromExcel } from "@/api/suppliers";
import type { SupplierImportSummary, SupplierImportResult } from "@/types";


import { useNotification } from "@/composables";

const notification = useNotification();
defineOptions({ name: "AdminSupplierImportView" });

const router = useRouter();
const authStore = useAuthStore();
const { t } = useI18n();

const selectedFile = ref<File | null>(null);
const uploading = ref(false);
const summary = ref<SupplierImportSummary | null>(null);
const results = ref<SupplierImportResult[]>([]);
const lastRunAt = ref<string | null>(null);

const requiredColumns = computed(() => [
  {
    key: "vendor_code",
    label: t("adminSupplierImport.fields.vendorCode.label"),
    note: t("adminSupplierImport.fields.vendorCode.note"),
  },
  {
    key: "supplier_name",
    label: t("adminSupplierImport.fields.supplierName.label"),
    note: t("adminSupplierImport.fields.supplierName.note"),
  },
  {
    key: "contact",
    label: t("adminSupplierImport.fields.contact.label"),
    note: t("adminSupplierImport.fields.contact.note"),
  },
  {
    key: "phone",
    label: t("adminSupplierImport.fields.phone.label"),
    note: t("adminSupplierImport.fields.phone.note"),
  },
  {
    key: "email",
    label: t("adminSupplierImport.fields.email.label"),
    note: t("adminSupplierImport.fields.email.note"),
  },
  {
    key: "address",
    label: t("adminSupplierImport.fields.address.label"),
    note: t("adminSupplierImport.fields.address.note"),
  },
  {
    key: "payment_term",
    label: t("adminSupplierImport.fields.paymentTerm.label"),
    note: t("adminSupplierImport.fields.paymentTerm.note"),
  },
  {
    key: "currency",
    label: t("adminSupplierImport.fields.currency.label"),
    note: t("adminSupplierImport.fields.currency.note"),
  },
  {
    key: "tax",
    label: t("adminSupplierImport.fields.tax.label"),
    note: t("adminSupplierImport.fields.tax.note"),
  },
  {
    key: "fax",
    label: t("adminSupplierImport.fields.fax.label"),
    note: t("adminSupplierImport.fields.fax.note"),
  },
]);

const selectedFileName = computed(
  () => selectedFile.value?.name ?? t("adminSupplierImport.upload.noFileSelected"),
);

const lastRunTimestamp = computed(() => {
  if (!lastRunAt.value) {
    return "";
  }
  const date = new Date(lastRunAt.value);
  if (Number.isNaN(date.getTime())) {
    return lastRunAt.value;
  }
  return date.toLocaleString();
});

const resetResults = () => {
  summary.value = null;
  results.value = [];
  lastRunAt.value = null;
};

const clearSelection = () => {
  selectedFile.value = null;
  resetResults();
};

const onFileChange = (event: Event) => {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0] ?? null;
  selectedFile.value = file;
  target.value = "";
  if (file) {
    resetResults();
  }
};

const submitImport = async () => {
  const token =
    typeof authStore.getToken === "function"
      ? authStore.getToken()
      : window.localStorage?.getItem("token");
  if (!token) {
    notification.error(t("adminSupplierImport.notifications.sessionExpired"));
    router.push({ name: "login", query: { redirect: router.currentRoute.value.fullPath } });
    return;
  }
  if (!selectedFile.value) {
    notification.warning(t("adminSupplierImport.notifications.noFile"));
    return;
  }

  uploading.value = true;
  try {
    const data = await importSuppliersFromExcel(selectedFile.value);
    summary.value = data.summary;
    results.value = data.results;
    lastRunAt.value = new Date().toISOString();
    notification.success(
      t("adminSupplierImport.notifications.importFinished", { count: data.summary.importedRows }),
    );
  } catch (error: unknown) {
    const fallback = t("adminSupplierImport.notifications.importFailed");
    const message = error instanceof Error && error.message ? error.message : fallback;
    notification.error(message);
  } finally {
    uploading.value = false;
  }
};




</script>

<style scoped>
.import-page {
  max-width: 960px;
  margin: 0 auto;
  padding: 32px 24px 48px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}




.link-button {
  align-self: center;
  padding: 10px 18px;
  border-radius: 999px;
  border: 1px solid #6366f1;
  color: #4338ca;
  background: #eef2ff;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}

.link-button:hover {
  background: #c7d2fe;
}

.card {
  background: #ffffff;
  border-radius: 16px;
  box-shadow: 0 18px 42px rgba(51, 65, 85, 0.12);
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.upload-card h2,
.results-card h2 {
  margin: 0;
  font-size: 22px;
  font-weight: 700;
  color: #1f2937;
}

.card-hint {
  margin: 0;
  color: #6b7280;
  font-size: 14px;
}

.field-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 12px;
}

.field-item {
  background: #f9fafb;
  border-radius: 12px;
  padding: 12px 16px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.field-name {
  font-weight: 600;
  color: #1f2937;
}

.field-note {
  color: #6b7280;
  font-size: 13px;
}

.upload-controls {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
}

.upload-button {
  position: relative;
  overflow: hidden;
  padding: 10px 18px;
  border-radius: 999px;
  background: #6366f1;
  color: #fff;
  cursor: pointer;
  font-weight: 600;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.upload-button input {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
}

.file-name {
  min-width: 160px;
  color: #4b5563;
  font-size: 14px;
}

.primary-button,
.secondary-button {
  padding: 10px 18px;
  border-radius: 999px;
  border: none;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}

.primary-button {
  background: #4338ca;
  color: #fff;
}

.primary-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.secondary-button {
  background: #f3f4f6;
  color: #1f2937;
}

.secondary-button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.section-subtitle {
  margin: 4px 0 0;
  color: #6b7280;
  font-size: 14px;
}

.timestamp {
  color: #6b7280;
  font-size: 13px;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 12px;
}

.summary-item {
  background: #f3f4f6;
  border-radius: 12px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  text-align: center;
}

.summary-label {
  color: #6b7280;
  font-size: 13px;
}

.summary-value {
  font-size: 20px;
  font-weight: 700;
  color: #1f2937;
}

.summary-value.success {
  color: #047857;
}

.summary-value.warning {
  color: #b45309;
}

.table-block {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.table-block h3 {
  margin: 8px 0 0;
  font-size: 18px;
  color: #1f2937;
}

.table-block table {
  width: 100%;
  border-collapse: collapse;
  border-radius: 12px;
  overflow: hidden;
}

.table-block th,
.table-block td {
  padding: 10px 12px;
  border: 1px solid #e5e7eb;
  font-size: 14px;
  text-align: left;
}

.table-block thead {
  background: #f9fafb;
}

.tag {
  display: inline-flex;
  align-items: center;
  padding: 2px 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;
}

.tag-success {
  background: #ecfdf5;
  color: #047857;
}

.tag-info {
  background: #e0f2fe;
  color: #0369a1;
}

.error-block {
  border-top: 1px solid #fee2e2;
  padding-top: 16px;
}

.error-block table thead {
  background: #fee2e2;
  color: #b91c1c;
}

.error-hint {
  margin: 0;
  color: #b91c1c;
  font-size: 13px;
}

@media (max-width: 768px) {
  .link-button {
    align-self: flex-start;
  }

  .upload-controls {
    flex-direction: column;
    align-items: stretch;
  }

  .file-name {
    min-width: unset;
  }
}
</style>
