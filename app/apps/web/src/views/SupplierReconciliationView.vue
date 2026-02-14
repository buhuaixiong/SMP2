<template>
  <div class="supplier-reconciliation-view">
    <el-card class="header-card">
      <template #header>
        <div class="header-title">
          <h2>{{ t("reconciliation.title") }}</h2>
        </div>
      </template>

      <!-- Statistics Cards -->
      <el-row :gutter="20" class="stats-row">
        <el-col :span="6">
          <el-statistic
            :title="t('reconciliation.stats.totalRecords')"
            :value="stats.total_reconciliations"
          />
        </el-col>
        <el-col :span="6">
          <el-statistic :title="t('reconciliation.stats.matched')" :value="stats.matched_count">
            <template #suffix>
              <span class="stat-percent"> ({{ matchedPercent }}%) </span>
            </template>
          </el-statistic>
        </el-col>
        <el-col :span="6">
          <el-statistic
            :title="t('reconciliation.stats.varianceRecords')"
            :value="stats.variance_count"
            value-style="color: #f56c6c"
          />
        </el-col>
        <el-col :span="6">
          <el-statistic
            :title="t('reconciliation.stats.unconfirmed')"
            :value="pendingActions.pending_confirmations"
            value-style="color: #e6a23c"
          />
        </el-col>
      </el-row>
    </el-card>

    <!-- Filters -->
    <el-card class="filter-card">
      <el-form :inline="true" :model="filters">
        <el-form-item :label="t('common.startDate')">
          <el-date-picker
            v-model="filters.startDate"
            type="date"
            :placeholder="t('placeholders.selectStartDate')"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item :label="t('common.endDate')">
          <el-date-picker
            v-model="filters.endDate"
            type="date"
            :placeholder="t('placeholders.selectEndDate')"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item :label="t('common.status')">
          <el-select
            v-model="filters.status"
            :placeholder="t('placeholders.selectStatus')"
            clearable
          >
            <el-option :label="t('reconciliation.status.all')" value="" />
            <el-option :label="t('reconciliation.status.matched')" value="matched" />
            <el-option :label="t('reconciliation.status.variance')" value="variance" />
            <el-option :label="t('reconciliation.status.unmatched')" value="unmatched" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="fetchData">{{ t("common.search") }}</el-button>
          <el-button @click="resetFilters">{{ t("common.reset") }}</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- Reconciliation Table -->
    <el-card>
      <el-table
        v-loading="loading"
        :data="reconciliations"
        style="width: 100%"
        @row-click="showDetails"
      >
        <el-table-column
          prop="warehouseOrderNo"
          :label="t('reconciliation.table.warehouseOrderNo')"
          width="160"
        />
        <el-table-column
          prop="received_date"
          :label="t('reconciliation.table.receivedDate')"
          width="120"
        />
        <el-table-column
          prop="totalAmount"
          :label="t('reconciliation.table.reconciliationAmount')"
          width="120"
        >
          <template #default="{ row }"> ¥{{ row.totalAmount.toFixed(2) }} </template>
        </el-table-column>
        <el-table-column
          prop="total_quantity"
          :label="t('reconciliation.table.quantity')"
          width="100"
        />
        <el-table-column prop="status" :label="t('common.status')" width="100">
          <template #default="{ row }">
            <el-tag :type="getStatusType(row.status)">
              {{ getStatusText(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column
          prop="variance_amount"
          :label="t('reconciliation.table.varianceAmount')"
          width="120"
        >
          <template #default="{ row }">
            <span
              :class="{
                'variance-positive': row.variance_amount > 0,
                'variance-negative': row.variance_amount < 0,
              }"
            >
              {{ row.variance_amount >= 0 ? "+" : "" }}¥{{ row.variance_amount.toFixed(2) }}
            </span>
          </template>
        </el-table-column>
        <el-table-column
          prop="invoiceNumber"
          :label="t('reconciliation.table.invoiceNumber')"
          width="150"
        />
        <el-table-column
          prop="accountant_confirmed"
          :label="t('reconciliation.table.accountantConfirmed')"
          width="100"
        >
          <template #default="{ row }">
            <el-tag v-if="row.accountant_confirmed" type="success">{{
              t("reconciliation.confirmStatus.confirmed")
            }}</el-tag>
            <el-tag v-else type="warning">{{
              t("reconciliation.confirmStatus.unconfirmed")
            }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column :label="t('common.operations')" fixed="right" width="200">
          <template #default="{ row }">
            <el-button
              v-if="!row.invoice_id"
              type="primary"
              size="small"
              @click.stop="openUploadDialog(row)"
            >
              {{ t("reconciliation.uploadInvoice") }}
            </el-button>
            <el-button size="small" @click.stop="viewWarehouseReceipt(row)">
              {{ t("reconciliation.viewWarehouseReceipt") }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- Invoice Upload Dialog -->
    <el-dialog
      v-if="uploadDialogVisible"
      v-model="uploadDialogVisible"
      :title="t('reconciliation.uploadInvoiceDialog.title')"
      width="600px"
    >
      <el-form :model="uploadForm" :rules="uploadRules" ref="uploadFormRef" label-width="120px">
        <el-form-item :label="t('reconciliation.uploadInvoiceDialog.reconciliationNo')">
          <el-input v-model="currentReconciliation.warehouseOrderNo" disabled />
        </el-form-item>
        <el-form-item
          :label="t('reconciliation.uploadInvoiceDialog.invoiceNo')"
          prop="invoiceNumber"
        >
          <el-input
            v-model="uploadForm.invoiceNumber"
            :placeholder="t('reconciliation.uploadInvoiceDialog.invoiceNo')"
          />
        </el-form-item>
        <el-form-item
          :label="t('reconciliation.uploadInvoiceDialog.invoiceDate')"
          prop="invoiceDate"
        >
          <el-date-picker
            v-model="uploadForm.invoiceDate"
            type="date"
            :placeholder="t('reconciliation.uploadInvoiceDialog.invoiceDate')"
            value-format="YYYY-MM-DD"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item
          :label="t('reconciliation.uploadInvoiceDialog.invoiceAmount')"
          prop="invoiceAmount"
        >
          <el-input-number
            v-model="uploadForm.invoiceAmount"
            :precision="2"
            :min="0"
            :controls="false"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item
          :label="t('reconciliation.uploadInvoiceDialog.invoiceFile')"
          prop="invoiceFile"
        >
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :limit="1"
            :on-change="handleFileChange"
            :on-remove="handleFileRemove"
            accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png"
          >
            <el-button type="primary">{{
              t("reconciliation.uploadInvoiceDialog.selectFile")
            }}</el-button>
            <template #tip>
              <div class="el-upload__tip">
                {{ t("reconciliation.uploadInvoiceDialog.supportedFormats") }}
              </div>
            </template>
          </el-upload>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="uploadDialogVisible = false">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" :loading="uploading" @click="handleUpload">
          {{ t("common.upload") }}
        </el-button>
      </template>
    </el-dialog>

    <!-- Reconciliation Details Dialog -->
    <el-dialog
      v-if="detailsDialogVisible"
      v-model="detailsDialogVisible"
      :title="t('reconciliation.detailsDialog.title')"
      width="900px"
    >
      <el-descriptions v-if="selectedReconciliation" :column="2" border>
        <el-descriptions-item :label="t('reconciliation.table.warehouseOrderNo')">
          {{ selectedReconciliation.warehouseOrderNo }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('reconciliation.table.receivedDate')">
          {{ selectedReconciliation.received_date }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('reconciliation.table.reconciliationAmount')">
          ¥{{ selectedReconciliation.totalAmount.toFixed(2) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('reconciliation.table.quantity')">
          {{ selectedReconciliation.total_quantity }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('common.status')">
          <el-tag :type="getStatusType(selectedReconciliation.status)">
            {{ getStatusText(selectedReconciliation.status) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="t('reconciliation.table.varianceAmount')">
          <span
            :class="{
              'variance-positive': selectedReconciliation.variance_amount > 0,
              'variance-negative': selectedReconciliation.variance_amount < 0,
            }"
          >
            {{ selectedReconciliation.variance_amount >= 0 ? "+" : "" }}¥{{
              selectedReconciliation.variance_amount.toFixed(2)
            }}
          </span>
        </el-descriptions-item>
        <el-descriptions-item
          :label="t('reconciliation.table.invoiceNumber')"
          v-if="selectedReconciliation.invoiceNumber"
        >
          {{ selectedReconciliation.invoiceNumber }}
        </el-descriptions-item>
        <el-descriptions-item
          :label="t('reconciliation.uploadInvoiceDialog.invoiceDate')"
          v-if="selectedReconciliation.invoiceDate"
        >
          {{ selectedReconciliation.invoiceDate }}
        </el-descriptions-item>
        <el-descriptions-item
          :label="t('reconciliation.uploadInvoiceDialog.invoiceAmount')"
          v-if="selectedReconciliation.invoiceAmount"
        >
          ¥{{ selectedReconciliation.invoiceAmount.toFixed(2) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('reconciliation.table.accountantConfirmed')">
          <el-tag v-if="selectedReconciliation.accountant_confirmed" type="success">{{
            t("reconciliation.confirmStatus.confirmed")
          }}</el-tag>
          <el-tag v-else type="warning">{{ t("reconciliation.confirmStatus.unconfirmed") }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item
          :label="t('common.notes')"
          :span="2"
          v-if="selectedReconciliation.accountant_notes"
        >
          {{ selectedReconciliation.accountant_notes }}
        </el-descriptions-item>
      </el-descriptions>

      <!-- Variance Analysis -->
      <el-card
        v-if="selectedReconciliation?.varianceAnalysis"
        class="variance-card"
        style="margin-top: 20px"
      >
        <template #header>
          <span>{{ t("reconciliation.detailsDialog.varianceAnalysis") }}</span>
        </template>
        <el-descriptions :column="2" border>
          <el-descriptions-item :label="t('reconciliation.table.varianceAmount')">
            ¥{{ selectedReconciliation.varianceAnalysis.variance_amount.toFixed(2) }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('reconciliation.detailsDialog.variancePercentage')">
            {{ selectedReconciliation.varianceAnalysis.variance_percentage.toFixed(2) }}%
          </el-descriptions-item>
          <el-descriptions-item :label="t('reconciliation.detailsDialog.varianceReason')" :span="2">
            {{
              selectedReconciliation.varianceAnalysis.variance_reason ||
              t("reconciliation.detailsDialog.noReason")
            }}
          </el-descriptions-item>
        </el-descriptions>
      </el-card>

      <template #footer>
        <el-button @click="detailsDialogVisible = false">{{ t("common.close") }}</el-button>
      </template>
    </el-dialog>

    <!-- Warehouse Receipt Dialog -->
    <el-dialog
      v-if="warehouseDialogVisible"
      v-model="warehouseDialogVisible"
      :title="t('reconciliation.warehouseDialog.title')"
      width="800px"
    >
      <el-descriptions v-if="warehouseReceipt" :column="2" border>
        <el-descriptions-item :label="t('reconciliation.warehouseDialog.receiptNo')">
          {{ warehouseReceipt.receipt_number }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('reconciliation.warehouseDialog.receiptDate')">
          {{ warehouseReceipt.receipt_date }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('reconciliation.warehouseDialog.warehouseLocation')">
          {{
            warehouseReceipt.warehouse_location || t("reconciliation.warehouseDialog.notSpecified")
          }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('reconciliation.warehouseDialog.receivedBy')">
          {{ warehouseReceipt.received_by || t("reconciliation.warehouseDialog.notSpecified") }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('reconciliation.warehouseDialog.totalItems')">
          {{ warehouseReceipt.total_items }}
        </el-descriptions-item>
      </el-descriptions>

      <template #footer>
        <el-button @click="warehouseDialogVisible = false">{{ t("common.close") }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, reactive, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { type FormInstance, type FormRules } from "element-plus";
import { fetchSupplierReconciliations } from "@/api/reconciliation";
import type { Reconciliation, ReconciliationStats, WarehouseReceiptDetail } from "@/types";


import { useNotification } from "@/composables";
const notification = useNotification();

const { t } = useI18n();

const loading = ref(false);
const uploading = ref(false);
const reconciliations = ref<Reconciliation[]>([]);
const stats = ref<ReconciliationStats>({
  total_reconciliations: 0,
  matched_count: 0,
  variance_count: 0,
  unmatched_count: 0,
  confirmed_count: 0,
  total_variance_amount: 0,
  avg_variance_amount: 0,
});
const pendingActions = ref({
  pending_confirmations: 0,
  pending_variances: 0,
});

const filters = reactive({
  startDate: "",
  endDate: "",
  status: "",
});

const uploadDialogVisible = ref(false);
const detailsDialogVisible = ref(false);
const warehouseDialogVisible = ref(false);
const currentReconciliation = ref<Reconciliation>({} as Reconciliation);
const selectedReconciliation = ref<Reconciliation | null>(null);
const warehouseReceipt = ref<WarehouseReceiptDetail | null>(null);

const uploadForm = reactive({
  invoiceNumber: "",
  invoiceDate: "",
  invoiceAmount: 0,
  invoiceFile: null as File | null,
});

const uploadFormRef = ref<FormInstance>();
const uploadRef = ref();

const uploadRules = computed<FormRules>(() => ({
  invoiceNumber: [
    {
      required: true,
      message: t("reconciliation.validation.invoiceNoRequired"),
      trigger: "blur",
    },
  ],
  invoiceDate: [
    {
      required: true,
      message: t("reconciliation.validation.invoiceDateRequired"),
      trigger: "change",
    },
  ],
  invoiceAmount: [
    {
      required: true,
      message: t("reconciliation.validation.invoiceAmountRequired"),
      trigger: "blur",
    },
  ],
  invoiceFile: [
    {
      required: true,
      message: t("reconciliation.validation.invoiceFileRequired"),
      trigger: "change",
    },
  ],
}));

const matchedPercent = computed(() => {
  if (stats.value.total_reconciliations === 0) return 0;
  return ((stats.value.matched_count / stats.value.total_reconciliations) * 100).toFixed(1);
});

const fetchData = async () => {
  loading.value = true;
  try {
    const data = await fetchSupplierReconciliations(filters);
    reconciliations.value = data.reconciliations;
    stats.value = data.stats;
    pendingActions.value = data.pendingActions;
  } catch (error) {
    console.error(t("reconciliation.messages.loadFailed"), error);
    notification.error(t("reconciliation.messages.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const resetFilters = () => {
  filters.startDate = "";
  filters.endDate = "";
  filters.status = "";
  fetchData();
};

const openUploadDialog = (row: Reconciliation) => {
  currentReconciliation.value = row;
  uploadForm.invoiceNumber = "";
  uploadForm.invoiceDate = "";
  uploadForm.invoiceAmount = row.totalAmount;
  uploadForm.invoiceFile = null;
  uploadDialogVisible.value = true;
};

const handleFileChange = (file: any) => {
  uploadForm.invoiceFile = file.raw;
};

const handleFileRemove = () => {
  uploadForm.invoiceFile = null;
};

const handleUpload = async () => {
  if (!uploadFormRef.value) return;

  await uploadFormRef.value.validate(async (valid) => {
    if (!valid) return;

    uploading.value = true;
    try {
      const { uploadInvoice } = await import("@/api/reconciliation");
      await uploadInvoice({
        reconciliationId: currentReconciliation.value.id,
        invoiceNumber: uploadForm.invoiceNumber,
        invoiceDate: uploadForm.invoiceDate,
        invoiceAmount: uploadForm.invoiceAmount,
        invoiceFile: uploadForm.invoiceFile!,
      });

      notification.success(t("reconciliation.messages.uploadSuccess"));
      uploadDialogVisible.value = false;
      fetchData();
    } catch (error) {
      console.error(t("reconciliation.messages.uploadFailed"), error);
      notification.error(t("reconciliation.messages.uploadFailed"));
    } finally {
      uploading.value = false;
    }
  });
};

const showDetails = async (row: Reconciliation) => {
  try {
    const { fetchReconciliationById } = await import("@/api/reconciliation");
    selectedReconciliation.value = await fetchReconciliationById(row.id);
    detailsDialogVisible.value = true;
  } catch (error) {
    console.error(t("reconciliation.messages.detailsLoadFailed"), error);
    notification.error(t("reconciliation.messages.detailsLoadFailed"));
  }
};

const viewWarehouseReceipt = async (row: Reconciliation) => {
  try {
    const { fetchWarehouseReceipts } = await import("@/api/reconciliation");
    const data = await fetchWarehouseReceipts({ reconciliationId: row.id });
    if (data.warehouseReceipts.length > 0) {
      warehouseReceipt.value = data.warehouseReceipts[0];
      warehouseDialogVisible.value = true;
    } else {
      notification.warning(t("reconciliation.messages.receiptNotFound"));
    }
  } catch (error) {
    console.error(t("reconciliation.messages.receiptLoadFailed"), error);
    notification.error(t("reconciliation.messages.receiptLoadFailed"));
  }
};

const getStatusType = (status: string) => {
  const typeMap: Record<string, any> = {
    matched: "success",
    variance: "warning",
    unmatched: "info",
  };
  return typeMap[status] || "info";
};

const getStatusText = (status: string) => {
  const textMap: Record<string, string> = {
    matched: t("reconciliation.status.matched"),
    variance: t("reconciliation.status.variance"),
    unmatched: t("reconciliation.status.unmatched"),
  };
  return textMap[status] || status;
};

onMounted(() => {
  fetchData();
});




</script>

<style scoped lang="scss">
.supplier-reconciliation-view {
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

    .stat-percent {
      font-size: 14px;
      color: #606266;
      margin-left: 4px;
    }
  }

  .filter-card {
    margin-bottom: 20px;
  }

  .variance-positive {
    color: #f56c6c;
    font-weight: bold;
  }

  .variance-negative {
    color: #67c23a;
    font-weight: bold;
  }

  .variance-card {
    margin-top: 20px;
  }
}
</style>
