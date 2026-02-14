<template>
  <div class="accountant-reconciliation-dashboard">
    <el-card class="header-card">
      <template #header>
        <div class="header-title">
          <h2>{{ $t("reconciliation.dashboard.title") }}</h2>
          <el-button type="primary" @click="showReportDialog = true">
            {{ $t("reconciliation.buttons.generateReport") }}
          </el-button>
        </div>
      </template>

      <el-row :gutter="20" class="stats-row">
        <el-col :span="6">
          <el-card>
            <el-statistic
              :title="$t('reconciliation.stats.totalRecords')"
              :value="stats.total_reconciliations"
            />
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card>
            <el-statistic
              :title="$t('reconciliation.stats.matched')"
              :value="stats.matched_count"
              value-style="color: #67c23a"
            />
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card>
            <el-statistic
              :title="$t('reconciliation.stats.varianceRecords')"
              :value="stats.variance_count"
              value-style="color: #e6a23c"
            />
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card>
            <el-statistic
              :title="$t('reconciliation.stats.unconfirmed')"
              :value="stats.total_reconciliations - stats.confirmed_count"
              value-style="color: #f56c6c"
            />
          </el-card>
        </el-col>
      </el-row>
    </el-card>

    <el-card class="filter-card">
      <el-form :inline="true" :model="filters">
        <el-form-item :label="$t('common.startDate')">
          <el-date-picker
            v-model="filters.startDate"
            type="date"
            :placeholder="$t('placeholders.selectStartDate')"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item :label="$t('common.endDate')">
          <el-date-picker
            v-model="filters.endDate"
            type="date"
            :placeholder="$t('placeholders.selectEndDate')"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item :label="$t('common.status')">
          <el-select
            v-model="filters.status"
            :placeholder="$t('placeholders.selectStatus')"
            clearable
          >
            <el-option :label="$t('common.all')" value="" />
            <el-option :label="$t('reconciliation.status.matched')" value="matched" />
            <el-option :label="$t('reconciliation.status.variance')" value="variance" />
            <el-option :label="$t('reconciliation.status.unmatched')" value="unmatched" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="fetchData">{{ $t("common.search") }}</el-button>
          <el-button @click="resetFilters">{{ $t("common.reset") }}</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card>
      <el-table
        v-loading="loading"
        :data="reconciliations"
        style="width: 100%"
        @row-click="showDetails"
      >
        <el-table-column
          :label="$t('reconciliation.table.warehouseOrderNo')"
          prop="warehouseOrderNo"
          width="160"
        />
        <el-table-column
          :label="$t('reconciliation.table.supplier')"
          prop="supplierName"
          width="200"
        />
        <el-table-column
          :label="$t('reconciliation.table.receivedDate')"
          prop="received_date"
          width="120"
        />
        <el-table-column :label="$t('reconciliation.table.reconAmount')" width="120">
          <template #default="{ row }"> ¥{{ row.totalAmount.toFixed(2) }} </template>
        </el-table-column>
        <el-table-column :label="$t('reconciliation.table.invoiceAmount')" width="120">
          <template #default="{ row }">
            <span v-if="row.invoiceAmount">¥{{ row.invoiceAmount.toFixed(2) }}</span>
            <span v-else class="text-muted">{{ $t("reconciliation.table.notUploaded") }}</span>
          </template>
        </el-table-column>
        <el-table-column :label="$t('reconciliation.table.variance')" width="120">
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
        <el-table-column :label="$t('common.status')" prop="status" width="100">
          <template #default="{ row }">
            <el-tag :type="getStatusType(row.status)">
              {{ getStatusText(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column :label="$t('reconciliation.table.confirmStatus')" width="100">
          <template #default="{ row }">
            <el-tag v-if="row.accountant_confirmed" type="success">{{
              $t("reconciliation.confirmStatus.confirmed")
            }}</el-tag>
            <el-tag v-else type="warning">{{ $t("reconciliation.confirmStatus.pending") }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column :label="$t('common.actions')" fixed="right" width="250">
          <template #default="{ row }">
            <el-button
              v-if="!row.accountant_confirmed"
              type="success"
              size="small"
              @click.stop="confirmReconciliation(row, true)"
            >
              {{ $t("common.confirm") }}
            </el-button>
            <el-button
              v-if="!row.accountant_confirmed"
              type="danger"
              size="small"
              @click.stop="confirmReconciliation(row, false)"
            >
              {{ $t("reconciliation.buttons.reject") }}
            </el-button>
            <el-button size="small" @click.stop="showDetails(row)">
              {{ $t("common.view") }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog
      v-model="detailsDialogVisible"
      :title="$t('reconciliation.detailsDialog.title')"
      width="1000px"
    >
      <el-descriptions v-if="selectedReconciliation" :column="2" border>
        <el-descriptions-item :label="$t('reconciliation.table.warehouseOrderNo')">
          {{ selectedReconciliation.warehouseOrderNo }}
        </el-descriptions-item>
        <el-descriptions-item :label="$t('reconciliation.table.supplier')">
          {{ selectedReconciliation.supplierName }}
        </el-descriptions-item>
        <el-descriptions-item :label="$t('reconciliation.table.receivedDate')">
          {{ selectedReconciliation.received_date }}
        </el-descriptions-item>
        <el-descriptions-item :label="$t('reconciliation.table.reconAmount')">
          ¥{{ selectedReconciliation.totalAmount.toFixed(2) }}
        </el-descriptions-item>
        <el-descriptions-item
          :label="$t('invoice.number')"
          v-if="selectedReconciliation.invoiceNumber"
        >
          {{ selectedReconciliation.invoiceNumber }}
        </el-descriptions-item>
        <el-descriptions-item
          :label="$t('reconciliation.table.invoiceAmount')"
          v-if="selectedReconciliation.invoiceAmount"
        >
          ¥{{ selectedReconciliation.invoiceAmount.toFixed(2) }}
        </el-descriptions-item>
        <el-descriptions-item :label="$t('common.status')">
          <el-tag :type="getStatusType(selectedReconciliation.status)">
            {{ getStatusText(selectedReconciliation.status) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="$t('reconciliation.detailsDialog.varianceAmount')">
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
      </el-descriptions>

      <el-card v-if="selectedReconciliation?.varianceAnalysis" style="margin-top: 20px">
        <template #header>{{ $t("reconciliation.detailsDialog.varianceAnalysis") }}</template>
        <el-descriptions :column="2" border>
          <el-descriptions-item :label="$t('reconciliation.detailsDialog.varianceAmount')">
            ¥{{ selectedReconciliation.varianceAnalysis.variance_amount.toFixed(2) }}
          </el-descriptions-item>
          <el-descriptions-item :label="$t('reconciliation.detailsDialog.variancePercentage')">
            {{ selectedReconciliation.varianceAnalysis.variance_percentage.toFixed(2) }}%
          </el-descriptions-item>
          <el-descriptions-item
            :label="$t('reconciliation.detailsDialog.varianceReason')"
            :span="2"
          >
            {{ selectedReconciliation.varianceAnalysis.variance_reason || $t("common.none") }}
          </el-descriptions-item>
        </el-descriptions>
      </el-card>

      <template #footer>
        <el-button @click="detailsDialogVisible = false">{{ $t("common.close") }}</el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="showReportDialog"
      :title="$t('reconciliation.reportDialog.title')"
      width="600px"
    >
      <el-form :model="reportForm" label-width="120px">
        <el-form-item :label="$t('reconciliation.reportDialog.reportType')">
          <el-select
            v-model="reportForm.reportType"
            :placeholder="$t('placeholders.selectReportType')"
          >
            <el-option :label="$t('reconciliation.reportDialog.types.summary')" value="summary" />
            <el-option :label="$t('reconciliation.reportDialog.types.variance')" value="variance" />
            <el-option
              :label="$t('reconciliation.reportDialog.types.supplierPerformance')"
              value="supplier"
            />
          </el-select>
        </el-form-item>
        <el-form-item :label="$t('common.startDate')">
          <el-date-picker
            v-model="reportForm.startDate"
            type="date"
            :placeholder="$t('placeholders.selectStartDate')"
            value-format="YYYY-MM-DD"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item :label="$t('common.endDate')">
          <el-date-picker
            v-model="reportForm.endDate"
            type="date"
            :placeholder="$t('placeholders.selectEndDate')"
            value-format="YYYY-MM-DD"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showReportDialog = false">{{ $t("common.cancel") }}</el-button>
        <el-button type="primary" @click="generateReport" :loading="generatingReport">
          {{ $t("reconciliation.buttons.generate") }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, reactive, onMounted } from "vue";
import { useI18n } from "vue-i18n";

import {
  fetchReconciliationDashboard,
  fetchReconciliationById,
  confirmReconciliation as confirmReconciliationAPI,
  fetchReconciliationReport,
} from "@/api/reconciliation";
import type { Reconciliation, ReconciliationStats } from "@/types";


import { useNotification } from "@/composables";
const notification = useNotification();

const { t } = useI18n();

const loading = ref(false);
const generatingReport = ref(false);
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

const filters = reactive({
  startDate: "",
  endDate: "",
  status: "",
});

const reportForm = reactive({
  reportType: "summary",
  startDate: "",
  endDate: "",
});

const detailsDialogVisible = ref(false);
const showReportDialog = ref(false);
const selectedReconciliation = ref<Reconciliation | null>(null);

const fetchData = async () => {
  loading.value = true;
  try {
    const data = await fetchReconciliationDashboard(filters);
    reconciliations.value = data.recentReconciliations;
    stats.value = data.stats;
  } catch (error) {
    console.error(t("reconciliation.messages.fetchDataFailed"), error);
    notification.error(t("reconciliation.messages.fetchDataFailed"));
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

const showDetails = async (row: Reconciliation) => {
  try {
    selectedReconciliation.value = await fetchReconciliationById(row.id);
    detailsDialogVisible.value = true;
  } catch (error) {
    console.error(t("reconciliation.messages.fetchDetailsFailed"), error);
    notification.error(t("reconciliation.messages.fetchDetailsFailed"));
  }
};

const confirmReconciliation = async (row: Reconciliation, confirm: boolean) => {
  try {
    const notes = confirm
      ? await notification.prompt(
          t("reconciliation.prompts.confirmNotes"),
          t("reconciliation.prompts.confirmTitle"),
          {
            confirmButtonText: t("common.confirm"),
            cancelButtonText: t("common.cancel"),
            inputPattern: /.*/,
            inputPlaceholder: t("reconciliation.prompts.notesPlaceholder"),
          },
        )
          .then(({ value }) => value)
          .catch(() => null)
      : await notification.prompt(
          t("reconciliation.prompts.rejectReason"),
          t("reconciliation.prompts.rejectTitle"),
          {
            confirmButtonText: t("reconciliation.buttons.reject"),
            cancelButtonText: t("common.cancel"),
            inputValidator: (value) =>
              value ? true : t("reconciliation.validation.rejectReasonRequired"),
            inputPlaceholder: t("reconciliation.prompts.rejectReasonPlaceholder"),
          },
        )
          .then(({ value }) => value)
          .catch(() => null);

    if (notes === null && !confirm) return;

    await confirmReconciliationAPI({
      reconciliationId: row.id,
      confirm,
      notes: notes || undefined,
    });

    notification.success(
      confirm
        ? t("reconciliation.messages.confirmSuccess")
        : t("reconciliation.messages.rejectSuccess"),
    );
    fetchData();
  } catch (error) {
    console.error(
      confirm
        ? t("reconciliation.messages.confirmFailed")
        : t("reconciliation.messages.rejectFailed"),
      error,
    );
    notification.error(
      confirm
        ? t("reconciliation.messages.confirmFailed")
        : t("reconciliation.messages.rejectFailed"),
    );
  }
};

const generateReport = async () => {
  generatingReport.value = true;
  try {
    const report = await fetchReconciliationReport({
      reportType: reportForm.reportType as "summary" | "variance" | "supplier",
      startDate: reportForm.startDate,
      endDate: reportForm.endDate,
    });

    notification.success(t("reconciliation.messages.reportGeneratedSuccess"));
    console.log("Generated report:", report);
    showReportDialog.value = false;
  } catch (error) {
    console.error(t("reconciliation.messages.reportGeneratedFailed"), error);
    notification.error(t("reconciliation.messages.reportGeneratedFailed"));
  } finally {
    generatingReport.value = false;
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
  // Refactored to use i18n
  return t(`reconciliation.status.${status}`, status);
};

onMounted(() => {
  fetchData();
});




</script>

<style scoped lang="scss">
.accountant-reconciliation-dashboard {
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

  .text-muted {
    color: #909399;
  }

  .variance-positive {
    color: #f56c6c;
    font-weight: bold;
  }

  .variance-negative {
    color: #67c23a;
    font-weight: bold;
  }
}
</style>
