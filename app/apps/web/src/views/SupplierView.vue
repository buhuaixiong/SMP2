<template>
  <div class="supplier-view">
    <PageHeader :title="t('directory.title')">
      <template #actions>
        <button class="add-btn" type="button" @click="showCreateDialog = true">
          + {{ t("supplier.addNew") }}
        </button>
      </template>
    </PageHeader>

    <div class="search-bar">
      <input
        v-model.trim="searchQuery"
        type="text"
        :placeholder="t('supplier.search')"
        class="search-input"
      />
    </div>

    <div class="supplier-table">
      <div v-if="supplierStore.loading" class="loading">{{ t("common.loading") }}...</div>
      <div v-else-if="filteredSuppliers.length === 0" class="empty-state">
        {{ t("directory.filters.title") }} {{ t("common.search") }} {{ t("directory.empty") }}
      </div>
      <div v-else class="table-container">
        <div class="table-header">
          <div class="col-company">{{ t("supplier.fields.companyName") }}</div>
          <div class="col-category">{{ t("supplier.fields.category") }}</div>
          <div class="col-status">{{ t("common.status") }}</div>
          <div class="col-actions">{{ t("common.actions") }}</div>
        </div>
        <div v-for="supplier in filteredSuppliers" :key="supplier.id" class="table-row">
          <div class="col-company">
            <div class="company-name">{{ supplier.companyName }}</div>
            <div class="company-contact">
              {{ supplier.contactPerson }} - {{ supplier.contactEmail }}
            </div>
          </div>
          <div class="col-category">{{ supplier.category }}</div>
          <div class="col-status">
            <span :class="['status-tag', getStatusClass(supplier.status)]">
              {{ getStatusText(supplier.status) }}
            </span>
          </div>
          <div class="col-actions">
            <button class="btn-small btn-info" type="button" @click="viewSupplier(supplier)">
              {{ t("common.view") }}
            </button>
            <button
              v-if="canApprove(supplier)"
              class="btn-small btn-success"
              type="button"
              @click="openApprovalDialog(supplier)"
            >
              {{ t("approval.review") }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showCreateDialog" class="modal-overlay" @click="closeCreateDialog">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h2>{{ t("supplier.addNew") }}</h2>
          <button class="close-btn" type="button" @click="closeCreateDialog">x</button>
        </div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-item">
              <label>{{ t("supplier.fields.companyName") }} *</label>
              <input
                v-model.trim="supplierForm.companyName"
                type="text"
                class="form-input"
                required
              />
            </div>
            <div class="form-item">
              <label>{{ t("supplier.fields.companyId") }} *</label>
              <input
                v-model.trim="supplierForm.companyId"
                type="text"
                class="form-input"
                required
              />
            </div>
            <div class="form-item">
              <label>{{ t("supplier.fields.contactPerson") }} *</label>
              <input
                v-model.trim="supplierForm.contactPerson"
                type="text"
                class="form-input"
                required
              />
            </div>
            <div class="form-item">
              <label>{{ t("supplier.fields.contactPhone") }} *</label>
              <input
                v-model.trim="supplierForm.contactPhone"
                type="tel"
                class="form-input"
                required
              />
            </div>
            <div class="form-item">
              <label>{{ t("supplier.fields.contactEmail") }} *</label>
              <input
                v-model.trim="supplierForm.contactEmail"
                type="email"
                class="form-input"
                required
              />
            </div>
            <div class="form-item">
              <label>{{ t("supplier.fields.category") }} *</label>
              <select v-model="supplierForm.category" class="form-input" required>
                <option value="">{{ t("directory.filters.category") }}</option>
                <option value="Raw material">{{ t("supplier.categories.rawMaterial") }}</option>
                <option value="IT">{{ t("supplier.categories.it") }}</option>
                <option value="Services">{{ t("supplier.categories.services") }}</option>
              </select>
            </div>
            <div class="form-item form-item-full">
              <label>{{ t("supplier.fields.address") }} *</label>
              <textarea
                v-model.trim="supplierForm.address"
                class="form-input"
                rows="3"
                required
              ></textarea>
            </div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn-cancel" type="button" @click="closeCreateDialog">
            {{ t("common.cancel") }}
          </button>
          <button class="btn-primary" type="button" :disabled="creating" @click="createSupplier">
            {{
              creating ? t("supplier.notifications.createSuccess") + "..." : t("supplier.addNew")
            }}
          </button>
        </div>
      </div>
    </div>

    <div v-if="showApprovalDialog" class="modal-overlay" @click="closeApprovalDialog">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h2>{{ t("approvalQueue.title") }}</h2>
          <button class="close-btn" type="button" @click="closeApprovalDialog">x</button>
        </div>
        <div class="modal-body">
          <div class="supplier-info" v-if="currentSupplier">
            <h3>{{ currentSupplier.companyName }}</h3>
            <p>
              {{ t("supplier.fields.contactPerson") }}: {{ currentSupplier.contactPerson }} -
              {{ currentSupplier.contactPhone }}
            </p>
            <p>{{ t("supplier.fields.contactEmail") }}: {{ currentSupplier.contactEmail }}</p>
            <p>
              {{ t("supplier.status.underReview") }}: {{ getStatusText(currentSupplier.status) }}
            </p>
          </div>

          <div class="form-grid">
            <div class="form-item">
              <label>{{ t("approval.decision") }} *</label>
              <div class="radio-group">
                <label class="radio-item">
                  <input v-model="approvalForm.decision" type="radio" value="approved" />
                  <span>{{ t("approval.approve") }}</span>
                </label>
                <label class="radio-item">
                  <input v-model="approvalForm.decision" type="radio" value="rejected" />
                  <span>{{ t("approval.reject") }}</span>
                </label>
              </div>
            </div>

            <div class="form-item form-item-full" v-if="approvalForm.decision === 'approved'">
              <label>{{ t("approval.comment") }}</label>
              <textarea
                v-model="approvalForm.comments"
                class="form-input"
                rows="3"
                :placeholder="t('common.optional')"
              ></textarea>
            </div>

            <div class="form-item form-item-full" v-if="approvalForm.decision === 'approved'">
              <label>{{ t("rfq.form.requiredDocuments") }} ({{ t("common.optional") }})</label>
              <textarea
                v-model="approvalForm.emailContent"
                class="form-input"
                rows="4"
                :placeholder="t('rfq.form.requiredDocumentsPlaceholder')"
              ></textarea>
              <p class="helper-text">{{ t("rfq.detail.rfqInfo") }}: {{ safeEmailContent }}</p>
            </div>

            <div class="form-item form-item-full" v-if="approvalForm.decision === 'rejected'">
              <label>{{ t("requisition.dialog.reason") }} *</label>
              <textarea
                v-model="approvalForm.rejectionReason"
                class="form-input"
                rows="3"
                :placeholder="t('requisition.dialog.placeholder')"
              ></textarea>
              <p class="helper-text">{{ t("rfq.detail.rfqInfo") }}: {{ safeRejectionReason }}</p>
            </div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn-cancel" type="button" @click="closeApprovalDialog">
            {{ t("common.cancel") }}
          </button>
          <button
            class="btn-primary"
            type="button"
            :disabled="!approvalForm.decision || approving"
            @click="submitApproval"
          >
            {{
              approving
                ? t("approval.notifications.approveSuccess") + "..."
                : t("approvalQueue.title")
            }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">




import { computed, onMounted, reactive, ref, watch } from "vue";

import { useI18n } from "vue-i18n";
import PageHeader from "@/components/layout/PageHeader.vue";
import { useAuthStore } from "@/stores/auth";
import { useSupplierStore } from "@/stores/supplier";
import { getStatusText } from "@/utils/helpers";
import type { Supplier } from "@/types";
import { sanitizeInput } from "@/utils/security";


import { useNotification } from "@/composables";

const notification = useNotification();
defineOptions({ name: "SupplierView" });

const { t } = useI18n();
const authStore = useAuthStore();
const supplierStore = useSupplierStore();

const searchQuery = ref("");
const showCreateDialog = ref(false);
const showApprovalDialog = ref(false);
const currentSupplier = ref<Supplier | null>(null);
const creating = ref(false);
const approving = ref(false);

const supplierForm = reactive({
  companyName: "",
  companyId: "",
  contactPerson: "",
  contactPhone: "",
  contactEmail: "",
  category: "",
  address: "",
});

const approvalForm = reactive({
  decision: "",
  comments: "",
  rejectionReason: "",
  emailContent: "",
});

const filteredSuppliers = computed(() => {
  if (!searchQuery.value) return supplierStore.suppliers;
  const query = searchQuery.value.toLowerCase();
  return supplierStore.suppliers.filter(
    (supplier) =>
      supplier.companyName.toLowerCase().includes(query) ||
      supplier.contactPerson.toLowerCase().includes(query) ||
      supplier.contactEmail.toLowerCase().includes(query),
  );
});

const getStatusClass = (status: string) => {
  if (status === "approved") return "success";
  if (status === "rejected") return "danger";
  if (status.includes("pending")) return "warning";
  return "info";
};

const canApprove = (supplier: Supplier) => {
  const role = authStore.role ?? authStore.user?.role ?? null;
  const approvalMap: Record<string, string> = {
    pending_purchaser: "purchaser",
    pending_purchase_manager: "purchase_manager",
    pending_finance_manager: "finance_manager",
  };
  return approvalMap[supplier.status] === role;
};

const viewSupplier = (supplier: Supplier) => {
  notification.alert(
    `${t("supplier.fields.contactPerson")}: ${supplier.contactPerson}\n${t("supplier.fields.contactEmail")}: ${supplier.contactEmail}\n${t("supplier.fields.address")}: ${supplier.address}`,
    supplier.companyName,
    {
      confirmButtonText: t("common.close"),
    },
  );
};

const openApprovalDialog = (supplier: Supplier) => {
  currentSupplier.value = supplier;
  showApprovalDialog.value = true;
};

const closeCreateDialog = () => {
  showCreateDialog.value = false;
  resetSupplierForm();
};

const closeApprovalDialog = () => {
  showApprovalDialog.value = false;
  resetApprovalForm();
};

const resetSupplierForm = () => {
  Object.assign(supplierForm, {
    companyName: "",
    companyId: "",
    contactPerson: "",
    contactPhone: "",
    contactEmail: "",
    category: "",
    address: "",
  });
};

const resetApprovalForm = () => {
  Object.assign(approvalForm, {
    decision: "",
    comments: "",
    rejectionReason: "",
    emailContent: "",
  });
};

const createSupplier = async () => {
  if (!supplierForm.companyName || !supplierForm.contactPerson || !supplierForm.category) {
    notification.warning(t("validation.required") + " (*)");
    return;
  }

  try {
    creating.value = true;
    await supplierStore.createSupplier({
      ...supplierForm,
      actorId: authStore.user ? String(authStore.user.id) : undefined,
      actorName: authStore.user?.name,
    });
    notification.success(t("supplier.notifications.createSuccess"));
    closeCreateDialog();
    await supplierStore.fetchSuppliers({}, true);
  } catch (error) {
    const message =
      error instanceof Error ? error.message : t("errors.general") + ", " + t("errors.serverError");
    notification.error(message);
  } finally {
    creating.value = false;
  }
};

const safeEmailContent = computed(() => sanitizeInput(approvalForm.emailContent));
const safeRejectionReason = computed(() => sanitizeInput(approvalForm.rejectionReason));

watch(
  () => approvalForm.comments,
  (value) => {
    const sanitized = sanitizeInput(value);
    if (sanitized !== value) approvalForm.comments = sanitized;
  },
);

watch(
  () => approvalForm.emailContent,
  (value) => {
    const sanitized = sanitizeInput(value);
    if (sanitized !== value) approvalForm.emailContent = sanitized;
  },
);

watch(
  () => approvalForm.rejectionReason,
  (value) => {
    const sanitized = sanitizeInput(value);
    if (sanitized !== value) approvalForm.rejectionReason = sanitized;
  },
);

const submitApproval = async () => {
  if (!currentSupplier.value) {
    return;
  }
  if (!approvalForm.decision) {
    notification.warning(t("approvalQueue.title") + " " + t("common.submit"));
    return;
  }

  try {
    approving.value = true;
    const comment =
      approvalForm.decision === "approved"
        ? approvalForm.comments
        : approvalForm.rejectionReason || t("supplier.status.rejected");

    await supplierStore.approveSupplier(
      currentSupplier.value.id,
      approvalForm.decision as "approved" | "rejected",
      comment,
    );

    if (approvalForm.decision === "approved") {
      console.info(
        t("rfq.form.requiredDocuments") + " " + t("rfq.detail.rfqInfo") + ":",
        safeEmailContent.value,
      );
    }

    notification.success(t("approval.notifications.approveSuccess"));
    closeApprovalDialog();
    await supplierStore.fetchSuppliers({}, true);
  } catch (error) {
    const message =
      error instanceof Error
        ? error.message
        : t("approvalQueue.title") + " " + t("errors.general") + ", " + t("errors.serverError");
    notification.error(message);
  } finally {
    approving.value = false;
  }
};

onMounted(async () => {
  await supplierStore.fetchSuppliers();
});




</script>

<style scoped>
.supplier-view {
  padding: 24px;
  max-width: 1200px;
  margin: 0 auto;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}



.add-btn {
  padding: 10px 20px;
  border: none;
  background: #5d5cde;
  color: white;
  border-radius: 6px;
  cursor: pointer;
}

.search-bar {
  margin-bottom: 20px;
}

.search-input {
  width: 100%;
  padding: 12px 16px;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  font-size: 14px;
}

.supplier-table {
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  padding: 0 0 16px 0;
}

.loading,
.empty-state {
  padding: 40px;
  text-align: center;
  color: #909399;
}

.table-header,
.table-row {
  display: grid;
  grid-template-columns: 2fr 1fr 1fr 1fr;
  gap: 16px;
  padding: 16px 24px;
  align-items: center;
}

.table-header {
  font-weight: 600;
  color: #909399;
  border-bottom: 1px solid #ebeef5;
}

.table-row {
  border-bottom: 1px solid #f2f6fc;
}

.col-company {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.company-name {
  font-weight: 600;
  color: #303133;
}

.company-contact {
  color: #909399;
  font-size: 12px;
}

.col-actions {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
}

.btn-small {
  padding: 6px 12px;
  border-radius: 4px;
  border: none;
  cursor: pointer;
  font-size: 12px;
}

.btn-info {
  background: #ecf5ff;
  color: #409eff;
}

.btn-success {
  background: #67c23a;
  color: white;
}

.status-tag {
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
  display: inline-block;
}

.status-tag.success {
  background: #f0f9ff;
  color: #67c23a;
}

.status-tag.warning {
  background: #fdf6ec;
  color: #e6a23c;
}

.status-tag.danger {
  background: #fef0f0;
  color: #f56c6c;
}

.status-tag.info {
  background: #f4f4f5;
  color: #909399;
}

.modal-overlay {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.35);
  backdrop-filter: blur(2px);
  z-index: 50;
}

.modal-content {
  background: white;
  border-radius: 8px;
  width: min(640px, 90vw);
  max-height: 90vh;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px;
  border-bottom: 1px solid #e4e7ed;
}

.close-btn {
  background: none;
  border: none;
  font-size: 18px;
  cursor: pointer;
  color: #909399;
}

.modal-body {
  padding: 20px;
  overflow-y: auto;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.form-item {
  display: flex;
  flex-direction: column;
}

.form-item-full {
  grid-column: 1 / -1;
}

.form-item label {
  margin-bottom: 8px;
  font-weight: 500;
  color: #303133;
}

.form-input {
  padding: 8px 12px;
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  font-size: 14px;
}

.form-input:focus {
  outline: none;
  border-color: #5d5cde;
}

.radio-group {
  display: flex;
  gap: 16px;
}

.radio-item {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}

.supplier-info {
  background: #f5f7fa;
  padding: 16px;
  border-radius: 6px;
  margin-bottom: 20px;
}

.helper-text {
  margin-top: 6px;
  font-size: 12px;
  color: #909399;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding: 20px;
  border-top: 1px solid #e4e7ed;
}

.btn-cancel {
  padding: 8px 16px;
  background: #f5f7fa;
  color: #606266;
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  cursor: pointer;
}

.btn-primary {
  padding: 8px 16px;
  background: #5d5cde;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.btn-primary:disabled {
  background: #ccc;
  cursor: not-allowed;
}

@media (max-width: 768px) {
  .table-header,
  .table-row {
    grid-template-columns: 1fr;
  }

  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
