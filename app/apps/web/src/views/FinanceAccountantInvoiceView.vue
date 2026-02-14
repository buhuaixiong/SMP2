<template>
  <div class="finance-accountant-invoice-view">
    <el-card class="header-card">
      <template #header>
        <div class="header-title">
          <h2>{{ t("financeInvoice.title") }}</h2>
          <el-button type="primary" @click="showUploadDialog = true">
            <el-icon><Upload /></el-icon>
            {{ t("financeInvoice.uploadInvoice") }}
          </el-button>
        </div>
      </template>

      <!-- Statistics -->
      <el-row :gutter="20" class="stats-row">
        <el-col :span="6">
          <el-card>
            <el-statistic
              :title="t('financeInvoice.stats.totalInvoices')"
              :value="stats.total_invoices || 0"
            />
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card>
            <el-statistic
              :title="t('financeInvoice.stats.pendingInvoices')"
              :value="stats.pending_invoices || 0"
              :value-style="{ color: '#e6a23c' }"
            />
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card>
            <el-statistic
              :title="t('financeInvoice.stats.verifiedInvoices')"
              :value="stats.verified_invoices || 0"
              :value-style="{ color: '#67c23a' }"
            />
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card>
            <el-statistic
              :title="t('financeInvoice.stats.exceptionInvoices')"
              :value="stats.exception_invoices || 0"
              :value-style="{ color: '#f56c6c' }"
            />
          </el-card>
        </el-col>
      </el-row>
    </el-card>

    <!-- Filters -->
    <el-card class="filter-card">
      <el-form :inline="true" :model="filters">
        <el-form-item :label="t('financeInvoice.filters.invoiceType')">
          <el-select
            v-model="filters.type"
            :placeholder="t('financeInvoice.filters.selectType')"
            clearable
          >
            <el-option :label="t('financeInvoice.filters.all')" value="" />
            <el-option
              :label="t('financeInvoice.filters.temporarySupplier')"
              value="temporary_supplier"
            />
            <el-option
              :label="t('financeInvoice.filters.formalSupplier')"
              value="formal_supplier"
            />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('financeInvoice.filters.status')">
          <el-select
            v-model="filters.status"
            :placeholder="t('financeInvoice.filters.selectStatus')"
            clearable
          >
            <el-option :label="t('financeInvoice.filters.all')" value="" />
            <el-option :label="t('financeInvoice.filters.pending')" value="pending" />
            <el-option :label="t('financeInvoice.filters.verified')" value="verified" />
            <el-option :label="t('financeInvoice.filters.rejected')" value="rejected" />
            <el-option :label="t('financeInvoice.filters.exception')" value="exception" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="fetchInvoices">{{
            t("financeInvoice.filters.query")
          }}</el-button>
          <el-button @click="resetFilters">{{ t("financeInvoice.filters.reset") }}</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- Invoice Table -->
    <el-card>
      <el-table v-loading="loading" :data="invoices" style="width: 100%" @row-click="showDetails">
        <el-table-column
          prop="invoice_number"
          :label="t('financeInvoice.table.invoiceNumber')"
          width="160"
        />
        <el-table-column
          prop="supplier_name"
          :label="t('financeInvoice.table.supplier')"
          width="200"
        />
        <el-table-column
          prop="issue_date"
          :label="t('financeInvoice.table.issueDate')"
          width="120"
        />
        <el-table-column prop="amount" :label="t('financeInvoice.table.amount')" width="120">
          <template #default="{ row }">
            ¥{{ (row.amount || row.total_amount || 0).toFixed(2) }}
          </template>
        </el-table-column>
        <el-table-column prop="type" :label="t('financeInvoice.table.type')" width="120">
          <template #default="{ row }">
            <el-tag :type="row.type === 'temporary_supplier' ? 'warning' : 'success'">
              {{
                row.type === "temporary_supplier"
                  ? t("financeInvoice.table.temporary")
                  : t("financeInvoice.table.formal")
              }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="status" :label="t('financeInvoice.table.status')" width="100">
          <template #default="{ row }">
            <el-tag :type="getStatusType(row.status)">
              {{ getStatusText(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column
          prop="uploaded_at"
          :label="t('financeInvoice.table.uploadedAt')"
          width="160"
        />
        <el-table-column :label="t('financeInvoice.table.actions')" fixed="right" width="280">
          <template #default="{ row }">
            <el-button size="small" @click.stop="viewInvoice(row)">
              {{ t("financeInvoice.table.view") }}
            </el-button>
            <el-button
              v-if="row.status === 'pending'"
              type="success"
              size="small"
              @click.stop="openReviewDialog(row)"
            >
              {{ t("financeInvoice.table.review") }}
            </el-button>
            <el-button type="danger" size="small" @click.stop="confirmDelete(row)">
              {{ t("financeInvoice.table.delete") }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- Upload Dialog -->
    <el-dialog
      v-model="showUploadDialog"
      :title="t('financeInvoice.uploadDialog.title')"
      width="600px"
      :close-on-click-modal="false"
    >
      <el-form ref="uploadFormRef" :model="uploadForm" :rules="uploadRules" label-width="120px">
        <el-form-item :label="t('financeInvoice.uploadDialog.supplier')" prop="supplier_id">
          <el-select
            v-model="uploadForm.supplier_id"
            filterable
            :placeholder="t('financeInvoice.uploadDialog.selectSupplier')"
            style="width: 100%"
          >
            <el-option
              v-for="supplier in suppliers"
              :key="supplier.id"
              :label="`${supplier.companyName} (${supplier.companyId})`"
              :value="supplier.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('financeInvoice.uploadDialog.invoiceNumber')" prop="invoice_number">
          <el-input
            v-model="uploadForm.invoice_number"
            :placeholder="t('financeInvoice.uploadDialog.invoiceNumberPlaceholder')"
          />
        </el-form-item>
        <el-form-item :label="t('financeInvoice.uploadDialog.issueDate')" prop="invoice_date">
          <el-date-picker
            v-model="uploadForm.invoice_date"
            type="date"
            :placeholder="t('financeInvoice.uploadDialog.selectIssueDate')"
            value-format="YYYY-MM-DD"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item :label="t('financeInvoice.uploadDialog.amount')" prop="amount">
          <el-input-number
            v-model="uploadForm.amount"
            :min="0"
            :precision="2"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item :label="t('financeInvoice.uploadDialog.type')" prop="type">
          <el-select
            v-model="uploadForm.type"
            :placeholder="t('financeInvoice.uploadDialog.selectInvoiceType')"
            style="width: 100%"
          >
            <el-option
              :label="t('financeInvoice.uploadDialog.temporaryInvoice')"
              value="temporary_supplier"
            />
            <el-option
              :label="t('financeInvoice.uploadDialog.formalInvoice')"
              value="formal_supplier"
            />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('financeInvoice.uploadDialog.taxRate')" prop="tax_rate">
          <el-input
            v-model="uploadForm.tax_rate"
            :placeholder="t('financeInvoice.uploadDialog.taxRatePlaceholder')"
          />
        </el-form-item>
        <el-form-item :label="t('financeInvoice.uploadDialog.invoiceFile')" prop="file">
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :limit="1"
            :on-change="handleFileChange"
            :on-exceed="handleExceed"
            accept=".pdf,.jpg,.jpeg,.png,.doc,.docx,.xls,.xlsx"
          >
            <el-button type="primary">{{ t("financeInvoice.uploadDialog.selectFile") }}</el-button>
            <template #tip>
              <div class="el-upload__tip">
                {{ t("financeInvoice.uploadDialog.supportedFormats") }}
              </div>
            </template>
          </el-upload>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showUploadDialog = false">{{
          t("financeInvoice.uploadDialog.cancel")
        }}</el-button>
        <el-button type="primary" :loading="uploading" @click="submitUpload">
          {{ t("financeInvoice.uploadDialog.upload") }}
        </el-button>
      </template>
    </el-dialog>

    <!-- Review Dialog -->
    <el-dialog
      v-model="showReviewDialog"
      :title="t('financeInvoice.reviewDialog.title')"
      width="600px"
      :close-on-click-modal="false"
    >
      <el-form ref="reviewFormRef" :model="reviewForm" label-width="120px">
        <el-form-item :label="t('financeInvoice.reviewDialog.result')">
          <el-radio-group v-model="reviewForm.status">
            <el-radio label="verified">{{ t("financeInvoice.reviewDialog.pass") }}</el-radio>
            <el-radio label="rejected">{{ t("financeInvoice.reviewDialog.reject") }}</el-radio>
            <el-radio label="exception">{{
              t("financeInvoice.reviewDialog.markAsException")
            }}</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item :label="t('financeInvoice.reviewDialog.notes')">
          <el-input
            v-model="reviewForm.review_notes"
            type="textarea"
            :rows="3"
            :placeholder="t('financeInvoice.reviewDialog.notesPlaceholder')"
          />
        </el-form-item>
        <el-form-item
          v-if="reviewForm.status === 'rejected'"
          :label="t('financeInvoice.reviewDialog.rejectionReason')"
        >
          <el-input
            v-model="reviewForm.rejection_reason"
            type="textarea"
            :rows="3"
            :placeholder="t('financeInvoice.reviewDialog.rejectionReasonPlaceholder')"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showReviewDialog = false">{{
          t("financeInvoice.reviewDialog.cancel")
        }}</el-button>
        <el-button type="primary" :loading="reviewing" @click="submitReview">
          {{ t("financeInvoice.reviewDialog.submit") }}
        </el-button>
      </template>
    </el-dialog>

    <!-- Details Dialog -->
    <el-dialog
      v-model="showDetailsDialog"
      :title="t('financeInvoice.detailsDialog.title')"
      width="800px"
    >
      <el-descriptions v-if="selectedInvoice" :column="2" border>
        <el-descriptions-item :label="t('financeInvoice.detailsDialog.invoiceNumber')">
          {{ selectedInvoice.invoice_number }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('financeInvoice.detailsDialog.supplier')">
          {{ selectedInvoice.supplier_name }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('financeInvoice.detailsDialog.issueDate')">
          {{ selectedInvoice.issue_date || selectedInvoice.invoice_date }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('financeInvoice.detailsDialog.amount')">
          ¥{{ (selectedInvoice.amount || selectedInvoice.total_amount || 0).toFixed(2) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('financeInvoice.detailsDialog.type')">
          <el-tag :type="selectedInvoice.type === 'temporary_supplier' ? 'warning' : 'success'">
            {{
              selectedInvoice.type === "temporary_supplier"
                ? t("financeInvoice.detailsDialog.temporarySupplier")
                : t("financeInvoice.detailsDialog.formalSupplier")
            }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="t('financeInvoice.detailsDialog.status')">
          <el-tag :type="getStatusType(selectedInvoice.status)">
            {{ getStatusText(selectedInvoice.status) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item
          :label="t('financeInvoice.detailsDialog.taxRate')"
          v-if="selectedInvoice.tax_rate"
        >
          {{ selectedInvoice.tax_rate }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('financeInvoice.detailsDialog.uploadedAt')">
          {{ selectedInvoice.uploaded_at || selectedInvoice.created_at }}
        </el-descriptions-item>
        <el-descriptions-item
          :label="t('financeInvoice.detailsDialog.reviewNotes')"
          :span="2"
          v-if="selectedInvoice.review_notes"
        >
          {{ selectedInvoice.review_notes }}
        </el-descriptions-item>
        <el-descriptions-item
          :label="t('financeInvoice.detailsDialog.rejectionReason')"
          :span="2"
          v-if="selectedInvoice.rejection_reason"
        >
          {{ selectedInvoice.rejection_reason }}
        </el-descriptions-item>
        <el-descriptions-item
          :label="t('financeInvoice.detailsDialog.validationErrors')"
          :span="2"
          v-if="selectedInvoice.validation_errors"
        >
          <el-alert type="warning" :closable="false">
            {{ selectedInvoice.validation_errors }}
          </el-alert>
        </el-descriptions-item>
      </el-descriptions>

      <el-card v-if="selectedInvoice?.file" style="margin-top: 20px">
        <template #header>{{ t("financeInvoice.detailsDialog.attachmentInfo") }}</template>
        <div class="file-info">
          <el-icon><Document /></el-icon>
          <span>{{ selectedInvoice.file.original_name }}</span>
          <el-button type="primary" size="small" @click="downloadInvoiceFile">
            {{ t("financeInvoice.detailsDialog.download") }}
          </el-button>
        </div>
      </el-card>

      <template #footer>
        <el-button @click="showDetailsDialog = false">{{
          t("financeInvoice.detailsDialog.close")
        }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, reactive, onMounted } from "vue";
import { useI18n } from "vue-i18n";

import { Upload, Document } from "@element-plus/icons-vue";
import type { FormInstance, UploadInstance, UploadFile, UploadFiles } from "element-plus";
import {
  fetchInvoices as apiFetchInvoices,
  fetchInvoiceById,
  deleteInvoice,
  reviewInvoice,
  uploadInvoice,
  fetchInvoiceStats,
  getInvoiceDownloadUrl,
} from "@/api/invoices";
import type { InvoiceListParams } from "@/api/invoices";
import { listSuppliers } from "@/api/suppliers";
import type { Invoice, Supplier } from "@/types";
import { openFileInNewTab } from "@/utils/fileDownload";


import { useNotification } from "@/composables";
const notification = useNotification();

const { t } = useI18n();

const loading = ref(false);
const uploading = ref(false);
const reviewing = ref(false);
const invoices = ref<Invoice[]>([]);
const suppliers = ref<Supplier[]>([]);
const selectedInvoice = ref<any>(null);

const stats = ref({
  total_invoices: 0,
  pending_invoices: 0,
  verified_invoices: 0,
  rejected_invoices: 0,
  exception_invoices: 0,
  large_amount_invoices: 0,
  avg_review_days: 0,
});

const filters = reactive({
  type: "",
  status: "",
});

const showUploadDialog = ref(false);
const showReviewDialog = ref(false);
const showDetailsDialog = ref(false);

const uploadFormRef = ref<FormInstance>();
const reviewFormRef = ref<FormInstance>();
const uploadRef = ref<UploadInstance>();

const uploadForm = reactive({
  supplier_id: null as number | null,
  invoice_number: "",
  invoice_date: "",
  amount: 0,
  type: "formal_supplier",
  tax_rate: "",
  invoice_type: "",
  file: null as File | null,
});

const reviewForm = reactive({
  status: "verified",
  review_notes: "",
  rejection_reason: "",
});

const uploadRules = {
  supplier_id: [
    { required: true, message: t("financeInvoice.validation.supplierRequired"), trigger: "change" },
  ],
  invoice_number: [
    {
      required: true,
      message: t("financeInvoice.validation.invoiceNumberRequired"),
      trigger: "blur",
    },
  ],
  invoice_date: [
    {
      required: true,
      message: t("financeInvoice.validation.issueDateRequired"),
      trigger: "change",
    },
  ],
  amount: [
    { required: true, message: t("financeInvoice.validation.amountRequired"), trigger: "blur" },
  ],
  type: [
    { required: true, message: t("financeInvoice.validation.typeRequired"), trigger: "change" },
  ],
  file: [
    { required: true, message: t("financeInvoice.validation.fileRequired"), trigger: "change" },
  ],
};

const fetchData = async () => {
  loading.value = true;
  const params: InvoiceListParams = {
    type: filters.type ? (filters.type as InvoiceListParams["type"]) : undefined,
    status: filters.status ? (filters.status as InvoiceListParams["status"]) : undefined,
  };
  try {
    const data = await apiFetchInvoices(params);
    invoices.value = data;
  } catch (error) {
    console.error(t("financeInvoice.messages.loadFailed"), error);
    notification.error(t("financeInvoice.messages.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const fetchInvoices = () => {
  fetchData();
};

const fetchStats = async () => {
  try {
    const data = await fetchInvoiceStats();
    stats.value = data;
  } catch (error) {
    console.error(t("financeInvoice.messages.statsFailed"), error);
    notification.error(t("financeInvoice.messages.statsFailed"));
  }
};

const fetchSuppliersList = async () => {
  try {
    const response = await listSuppliers({ limit: 500 });
    suppliers.value = response?.data ?? [];
  } catch (error) {
    console.error(t("financeInvoice.messages.suppliersFailed"), error);
    notification.error(t("financeInvoice.messages.suppliersFailed"));
  }
};

const resetFilters = () => {
  filters.type = "";
  filters.status = "";
  fetchData();
};

const handleFileChange = (file: UploadFile, uploadFiles: UploadFiles) => {
  if (file.raw) {
    uploadForm.file = file.raw;
  }
};

const handleExceed = () => {
  notification.warning(t("financeInvoice.messages.fileExceedsLimit"));
};

const submitUpload = async () => {
  if (!uploadFormRef.value) return;

  await uploadFormRef.value.validate(async (valid) => {
    if (!valid) return;

    if (!uploadForm.file) {
      notification.error(t("financeInvoice.messages.selectFile"));
      return;
    }

    uploading.value = true;
    try {
      await uploadInvoice({
        supplier_id: uploadForm.supplier_id!,
        invoice_number: uploadForm.invoice_number,
        invoice_date: uploadForm.invoice_date,
        amount: uploadForm.amount,
        type: uploadForm.type as "temporary_supplier" | "formal_supplier",
        tax_rate: uploadForm.tax_rate,
        invoice_type: uploadForm.invoice_type,
        file: uploadForm.file,
      });

      notification.success(t("financeInvoice.messages.uploadSuccess"));
      showUploadDialog.value = false;
      resetUploadForm();
      fetchData();
      fetchStats();
    } catch (error: any) {
      console.error(t("financeInvoice.messages.uploadFailed"), error);
      notification.error(error.message || t("financeInvoice.messages.uploadFailed"));
    } finally {
      uploading.value = false;
    }
  });
};

const resetUploadForm = () => {
  uploadForm.supplier_id = null;
  uploadForm.invoice_number = "";
  uploadForm.invoice_date = "";
  uploadForm.amount = 0;
  uploadForm.type = "formal_supplier";
  uploadForm.tax_rate = "";
  uploadForm.invoice_type = "";
  uploadForm.file = null;
  uploadRef.value?.clearFiles();
};

const viewInvoice = async (row: Invoice) => {
  try {
    selectedInvoice.value = await fetchInvoiceById(row.id);
    showDetailsDialog.value = true;
  } catch (error) {
    console.error(t("financeInvoice.messages.viewDetailsFailed"), error);
    notification.error(t("financeInvoice.messages.viewDetailsFailed"));
  }
};

const showDetails = (row: Invoice) => {
  viewInvoice(row);
};

const openReviewDialog = (row: Invoice) => {
  selectedInvoice.value = row;
  reviewForm.status = "verified";
  reviewForm.review_notes = "";
  reviewForm.rejection_reason = "";
  showReviewDialog.value = true;
};

const submitReview = async () => {
  if (!selectedInvoice.value) return;

  reviewing.value = true;
  try {
    await reviewInvoice(selectedInvoice.value.id, {
      status: reviewForm.status as "verified" | "rejected" | "exception",
      review_notes: reviewForm.review_notes,
      rejection_reason: reviewForm.rejection_reason,
    });

    notification.success(t("financeInvoice.messages.reviewCompleted"));
    showReviewDialog.value = false;
    fetchData();
    fetchStats();
  } catch (error: any) {
    console.error(t("financeInvoice.messages.reviewFailed"), error);
    notification.error(error.message || t("financeInvoice.messages.reviewFailed"));
  } finally {
    reviewing.value = false;
  }
};

const confirmDelete = async (row: Invoice) => {
  try {
    await notification.confirm(
      t("financeInvoice.messages.deleteConfirm"),
      t("financeInvoice.messages.confirmDelete"),
      {
        type: "warning",
        confirmButtonText: t("financeInvoice.messages.confirm"),
        cancelButtonText: t("financeInvoice.messages.cancel"),
      },
    );

    await deleteInvoice(row.id);
    notification.success(t("financeInvoice.messages.deleteSuccess"));
    fetchData();
    fetchStats();
  } catch (error: any) {
    if (error !== "cancel") {
      console.error(t("financeInvoice.messages.deleteFailed"), error);
      notification.error(error.message || t("financeInvoice.messages.deleteFailed"));
    }
  }
};

const downloadInvoiceFile = async () => {
  if (!selectedInvoice.value) return;
  const url = getInvoiceDownloadUrl(selectedInvoice.value.id);
  try {
    await openFileInNewTab(url);
  } catch (error: any) {
    notification.error(error?.message || t("financeInvoice.messages.downloadFailed"));
  }
};

const getStatusType = (status: string) => {
  const typeMap: Record<string, any> = {
    pending: "warning",
    verified: "success",
    rejected: "danger",
    exception: "danger",
  };
  return typeMap[status] || "info";
};

const getStatusText = (status: string) => {
  const statusKey = `financeInvoice.filters.${status}`;
  const translated = t(statusKey);
  return translated !== statusKey ? translated : status;
};

onMounted(() => {
  fetchData();
  fetchStats();
  fetchSuppliersList();
});




</script>

<style scoped lang="scss">
.finance-accountant-invoice-view {
  padding: 20px;

  .header-card {
    margin-bottom: 20px;

    .header-title {
      display: flex;
      justify-content: space-between;
      align-items: center;

      h2 {
        margin: 0;
      }
    }

    .stats-row {
      margin-top: 20px;
    }
  }

  .filter-card {
    margin-bottom: 20px;
  }

  .file-info {
    display: flex;
    align-items: center;
    gap: 10px;

    .el-icon {
      font-size: 24px;
    }

    span {
      flex: 1;
    }
  }
}
</style>
