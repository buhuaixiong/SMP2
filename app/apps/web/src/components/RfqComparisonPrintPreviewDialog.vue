<template>
  <el-dialog
    :model-value="modelValue"
    :title="t('rfq.print.title')"
    width="92%"
    top="3vh"
    destroy-on-close
    @update:model-value="emit('update:modelValue', $event)"
  >
    <div v-loading="loading" class="rfq-print-preview-dialog">
      <el-alert
        type="info"
        :closable="false"
        show-icon
        class="preview-hint"
        :title="t('rfq.print.previewHint')"
      />

      <div class="scope-toolbar">
        <span class="scope-label">{{ t("rfq.print.scope") }}</span>
        <el-radio-group v-model="scope" :disabled="loading">
          <el-radio-button label="latest">{{ t("rfq.print.scopeLatest") }}</el-radio-button>
          <el-radio-button label="all">{{ t("rfq.print.scopeAll") }}</el-radio-button>
        </el-radio-group>
      </div>

      <div v-if="data" ref="previewRef" class="rfq-print-preview">
        <div class="preview-header">
          <div>
            <div class="preview-title">{{ t("rfq.print.title") }}</div>
            <div class="preview-subtitle">RFQ #{{ data.rfqId }} - {{ data.title || "-" }}</div>
          </div>
          <div class="preview-meta">
            <div>{{ t("rfq.print.printedAt") }}: {{ formatDateTime(data.printedAt) }}</div>
            <div>{{ t("rfq.print.printedBy") }}: {{ data.printedBy || "-" }}</div>
          </div>
        </div>

        <section class="preview-section">
          <h3>{{ t("rfq.print.basicInfo") }}</h3>
          <div class="info-grid">
            <div><strong>{{ t("rfq.detail.rfqId") }}</strong><span>#{{ data.rfqId }}</span></div>
            <div><strong>{{ t("rfq.form.rfqType") }}</strong><span>{{ data.rfqType || "-" }}</span></div>
            <div><strong>{{ t("rfq.materialType.label") }}</strong><span>{{ data.materialType || "-" }}</span></div>
            <div><strong>{{ t("rfq.form.budgetAmount") }}</strong><span>{{ formatMoney(data.budgetAmount, data.currency) }}</span></div>
            <div><strong>{{ t("rfq.detail.createdBy") }}</strong><span>{{ data.createdBy || "-" }}</span></div>
            <div><strong>{{ t("rfq.print.reviewCompletedAt") }}</strong><span>{{ formatDateTime(data.reviewCompletedAt) }}</span></div>
            <div><strong>{{ t("rfq.form.requestingParty") }}</strong><span>{{ data.requestingParty || "-" }}</span></div>
            <div><strong>{{ t("rfq.form.requestingDepartment") }}</strong><span>{{ data.requestingDepartment || "-" }}</span></div>
            <div><strong>{{ t("rfq.form.validUntil") }}</strong><span>{{ formatDateTime(data.validUntil) }}</span></div>
            <div><strong>{{ t("common.status") }}</strong><span>{{ data.status || "-" }}</span></div>
            <div><strong>{{ t("common.createdAt") }}</strong><span>{{ formatDateTime(data.createdAt) }}</span></div>
            <div><strong>{{ t("rfq.print.selectedSuppliers") }}</strong><span>{{ data.selectedSupplierSummary || "-" }}</span></div>
            <div><strong>{{ t("rfq.print.scope") }}</strong><span>{{ scopeLabel }}</span></div>
            <div><strong>{{ t("rfq.rounds.roundNumber") }}</strong><span>{{ currentRoundLabel }}</span></div>
          </div>
          <p class="description">{{ data.description || "-" }}</p>
        </section>

        <section v-if="scope === 'latest'" class="preview-section">
          <h3>{{ t("rfq.print.supplierSummary") }}</h3>
          <div class="summary-stats">
            <span>{{ t("rfq.print.invitedCount") }}: {{ data.invitedSupplierCount }}</span>
            <span>{{ t("rfq.print.submittedCount") }}: {{ data.submittedSupplierCount }}</span>
            <span>{{ t("rfq.print.withdrawnCount") }}: {{ data.withdrawnSupplierCount }}</span>
          </div>
          <table class="preview-table">
            <thead>
              <tr>
                <th>{{ t("supplier.name") }}</th>
                <th>{{ t("supplier.supplierCode") }}</th>
                <th>{{ t("supplier.vendorCode") }}</th>
                <th>{{ t("rfq.detail.preBidQuoteStatus") }}</th>
                <th>{{ t("rfq.detail.quoteSubmittedAt") }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, index) in data.supplierSummary" :key="`${row.supplierName}-${index}`">
                <td>{{ row.supplierName || "-" }}</td>
                <td>{{ row.supplierCode || "-" }}</td>
                <td>{{ row.vendorCode || "-" }}</td>
                <td>{{ translateQuoteStatus(row.quoteStatus) }}</td>
                <td>{{ formatDateTime(row.quoteSubmittedAt) }}</td>
              </tr>
            </tbody>
          </table>
        </section>

        <section v-if="scope === 'latest'" class="preview-section">
          <h3>{{ t("rfq.print.quoteDetails") }}</h3>
          <table class="preview-table compact">
            <thead>
              <tr>
                <th>{{ t("common.no") }}</th>
                <th>{{ t("rfq.print.materialCode") }}</th>
                <th>{{ t("rfq.lineItems.itemName") }}</th>
                <th>{{ t("rfq.lineItems.specification") }}</th>
                <th>{{ t("rfq.lineItems.unit") }}</th>
                <th>{{ t("rfq.lineItems.quantity") }}</th>
                <th>{{ t("supplier.name") }}</th>
                <th>{{ t("rfq.print.quotedUnitPrice") }}</th>
                <th>{{ t("rfq.print.quotedTotalPrice") }}</th>
                <th>{{ t("rfq.print.selectedResult") }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, index) in data.quoteRows" :key="`${row.lineNumber}-${row.supplierName}-${index}`">
                <td>{{ row.lineNumber ?? index + 1 }}</td>
                <td>{{ row.materialCode || "-" }}</td>
                <td>{{ row.itemName || "-" }}</td>
                <td>{{ row.specifications || "-" }}</td>
                <td>{{ row.unit || "-" }}</td>
                <td>{{ formatQuantity(row.quantity) }}</td>
                <td>{{ row.supplierName || "-" }}</td>
                <td>{{ formatMoney(row.quotedUnitPrice, row.quoteCurrency) }}</td>
                <td>{{ formatMoney(row.quotedTotalPrice, row.quoteCurrency) }}</td>
                <td>
                  <span v-if="row.isSelected">
                    {{ row.selectedSupplierName || row.supplierName || "-" }}
                    ({{ formatMoney(row.selectedUnitPrice, row.selectedCurrency || row.quoteCurrency) }})
                  </span>
                  <span v-else>-</span>
                </td>
              </tr>
            </tbody>
          </table>
        </section>

        <section v-if="scope === 'all' && data.roundGroups?.length" class="preview-section">
          <h3>{{ t("rfq.print.supplierSummary") }}</h3>
          <div class="summary-stats">
            <span>{{ t("rfq.rounds.totalRounds") }}: {{ data.roundGroups.length }}</span>
            <span>{{ currentRoundLabel }}</span>
          </div>
        </section>

        <section class="preview-section">
          <h3>{{ t("rfq.print.auditTrail") }}</h3>
          <table class="preview-table compact">
            <thead>
              <tr>
                <th v-if="showRoundColumn">{{ t("rfq.rounds.roundNumber") }}</th>
                <th>{{ t("common.time") }}</th>
                <th>{{ t("common.user") }}</th>
                <th>{{ t("rfq.print.auditEntity") }}</th>
                <th>{{ t("rfq.print.auditAction") }}</th>
                <th>{{ t("rfq.print.auditDetail") }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, index) in data.auditRows" :key="`${row.occurredAt}-${index}`">
                <td v-if="showRoundColumn">{{ formatRoundNumber(row.roundNumber) }}</td>
                <td>{{ row.occurredAt || "-" }}</td>
                <td>{{ row.actorName || "-" }}</td>
                <td>{{ row.entityType || "-" }}</td>
                <td>{{ row.action || "-" }}</td>
                <td>{{ row.detail || "-" }}</td>
              </tr>
            </tbody>
          </table>
        </section>

        <section v-if="scope === 'all' && data.roundGroups?.length" class="preview-section">
          <h3>{{ t("rfq.print.allRounds") }}</h3>
          <div v-for="group in data.roundGroups" :key="group.roundId ?? group.roundNumber" class="round-group">
            <h4>{{ t("rfq.rounds.roundLabel", { number: group.roundNumber }) }}</h4>
            <div class="summary-stats">
              <span>{{ t("rfq.print.invitedCount") }}: {{ group.invitedSupplierCount }}</span>
              <span>{{ t("rfq.print.submittedCount") }}: {{ group.submittedSupplierCount }}</span>
              <span>{{ t("rfq.print.withdrawnCount") }}: {{ group.withdrawnSupplierCount }}</span>
              <span>{{ t("rfq.rounds.deadline") }}: {{ formatDateTime(group.bidDeadline) }}</span>
            </div>
            <table class="preview-table">
              <thead>
                <tr>
                  <th>{{ t("supplier.name") }}</th>
                  <th>{{ t("supplier.supplierCode") }}</th>
                  <th>{{ t("supplier.vendorCode") }}</th>
                  <th>{{ t("rfq.detail.preBidQuoteStatus") }}</th>
                  <th>{{ t("rfq.detail.quoteSubmittedAt") }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(row, index) in group.supplierSummary" :key="`${group.roundNumber}-${row.supplierName}-${index}`">
                  <td>{{ row.supplierName || "-" }}</td>
                  <td>{{ row.supplierCode || "-" }}</td>
                  <td>{{ row.vendorCode || "-" }}</td>
                  <td>{{ translateQuoteStatus(row.quoteStatus) }}</td>
                  <td>{{ formatDateTime(row.quoteSubmittedAt) }}</td>
                </tr>
              </tbody>
            </table>

            <table class="preview-table compact round-quotes-table">
              <thead>
                <tr>
                  <th>{{ t("common.no") }}</th>
                  <th>{{ t("rfq.print.materialCode") }}</th>
                  <th>{{ t("rfq.lineItems.itemName") }}</th>
                  <th>{{ t("supplier.name") }}</th>
                  <th>{{ t("rfq.print.quotedUnitPrice") }}</th>
                  <th>{{ t("rfq.print.quotedTotalPrice") }}</th>
                  <th>{{ t("rfq.print.selectedResult") }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(row, index) in group.quoteRows" :key="`${group.roundNumber}-${row.lineNumber}-${row.supplierName}-${index}`">
                  <td>{{ row.lineNumber ?? index + 1 }}</td>
                  <td>{{ row.materialCode || "-" }}</td>
                  <td>{{ row.itemName || "-" }}</td>
                  <td>{{ row.supplierName || "-" }}</td>
                  <td>{{ formatMoney(row.quotedUnitPrice, row.quoteCurrency) }}</td>
                  <td>{{ formatMoney(row.quotedTotalPrice, row.quoteCurrency) }}</td>
                  <td>
                    <span v-if="row.isSelected">
                      {{ row.selectedSupplierName || row.supplierName || "-" }}
                    </span>
                    <span v-else>-</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <el-empty v-else-if="!loading" :description="t('rfq.print.noData')" />
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="emit('update:modelValue', false)">{{ t("common.close") }}</el-button>
        <el-button type="primary" :loading="loading || printing" @click="printPreview">
          {{ t("rfq.print.printOrSavePdf") }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from "vue";
import { fetchRfqComparisonPrintData } from "@/api/rfq";
import type { RfqComparisonPrintData, RfqComparisonPrintScope } from "@/types";
import { useNotification } from "@/composables";
import { extractErrorMessage } from "@/utils/errorHandling";
import { useI18n } from "vue-i18n";

const props = defineProps<{
  modelValue: boolean;
  rfqId: number | null;
}>();

const emit = defineEmits<{
  (e: "update:modelValue", value: boolean): void;
}>();

const { t } = useI18n();
const notification = useNotification();
const loading = ref(false);
const printing = ref(false);
const data = ref<RfqComparisonPrintData | null>(null);
const previewRef = ref<HTMLElement | null>(null);
const scope = ref<RfqComparisonPrintScope>("latest");

const scopeLabel = computed(() =>
  scope.value === "all" ? t("rfq.print.scopeAll") : t("rfq.print.scopeLatest"),
);

const showRoundColumn = computed(() => scope.value === "all");

const currentRoundLabel = computed(() => {
  const roundNumber = data.value?.currentRound?.roundNumber ?? data.value?.latestRound?.roundNumber;
  return roundNumber ? t("rfq.rounds.roundLabel", { number: roundNumber }) : "-";
});

watch(
  () => [props.modelValue, props.rfqId, scope.value] as const,
  async ([visible, rfqId]) => {
    if (!visible || !rfqId) {
      return;
    }

    loading.value = true;
    try {
      data.value = await fetchRfqComparisonPrintData(rfqId, scope.value);
    } catch (error: unknown) {
      data.value = null;
      notification.error(extractErrorMessage(error) || t("rfq.print.loadError"));
    } finally {
      loading.value = false;
    }
  },
  { immediate: true },
);

function formatDateTime(value: string | null | undefined): string {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
}

function formatMoney(value: number | null | undefined, currency: string | null | undefined): string {
  if (value == null || Number.isNaN(Number(value))) return "-";
  return `${Number(value).toFixed(4)} ${currency || ""}`.trim();
}

function formatQuantity(value: number | null | undefined): string {
  if (value == null || Number.isNaN(Number(value))) return "-";
  return Number(value).toFixed(4).replace(/\.0+$/, "").replace(/(\.\d*?)0+$/, "$1");
}

function translateQuoteStatus(status: string | null | undefined): string {
  return t(`rfq.quoteStatus.${status || "not_submitted"}`);
}

function formatRoundNumber(value: number | null | undefined): string {
  return value ? t("rfq.rounds.roundLabel", { number: value }) : "-";
}

async function printPreview() {
  if (!data.value || !previewRef.value) {
    return;
  }

  printing.value = true;
  try {
    await nextTick();
    const printWindow = window.open("", "_blank", "width=1200,height=900");
    if (!printWindow) {
      throw new Error(t("rfq.print.popupBlocked"));
    }

    const printableHtml = `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8" />
        <title>${t("rfq.print.title")}</title>
        <style>
          body { font-family: Arial, "Microsoft YaHei", sans-serif; margin: 24px; color: #111827; }
          .preview-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 20px; }
          .preview-title { font-size: 24px; font-weight: 700; margin-bottom: 8px; }
          .preview-subtitle, .preview-meta { font-size: 12px; color: #4b5563; }
          .preview-section { margin-bottom: 24px; page-break-inside: avoid; }
          .preview-section h3 { font-size: 16px; margin: 0 0 12px; border-bottom: 1px solid #d1d5db; padding-bottom: 6px; }
          .info-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px 16px; margin-bottom: 12px; }
          .info-grid div { display: flex; gap: 8px; font-size: 12px; }
          .description { margin: 0; font-size: 12px; line-height: 1.5; white-space: pre-wrap; }
          .summary-stats { display: flex; gap: 24px; margin-bottom: 10px; font-size: 12px; }
          .preview-table { width: 100%; border-collapse: collapse; font-size: 11px; }
          .preview-table th, .preview-table td { border: 1px solid #374151; padding: 6px 8px; text-align: left; vertical-align: top; }
          .preview-table thead th { background: #f3f4f6; }
          .compact th, .compact td { padding: 5px 6px; }
          @media print {
            body { margin: 12px; }
            .preview-section { page-break-inside: auto; }
            .preview-table thead { display: table-header-group; }
          }
        </style>
      </head>
      <body>${previewRef.value.innerHTML}</body>
      </html>`;

    printWindow.document.open();
    printWindow.document.write(printableHtml);
    printWindow.document.close();
    printWindow.focus();
    printWindow.print();
  } catch (error: unknown) {
    notification.error(extractErrorMessage(error) || t("rfq.print.printError"));
  } finally {
    printing.value = false;
  }
}
</script>

<style scoped>
.rfq-print-preview-dialog {
  min-height: 240px;
}

.preview-hint {
  margin-bottom: 16px;
}

.scope-toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}

.scope-label {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.rfq-print-preview {
  background: #fff;
  border: 1px solid #dcdfe6;
  border-radius: 8px;
  padding: 24px;
  color: #111827;
}

.preview-header {
  display: flex;
  justify-content: space-between;
  gap: 24px;
  margin-bottom: 24px;
}

.preview-title {
  font-size: 24px;
  font-weight: 700;
}

.preview-subtitle,
.preview-meta {
  font-size: 13px;
  color: #4b5563;
  line-height: 1.6;
}

.preview-section {
  margin-bottom: 24px;
}

.round-group {
  margin-top: 20px;
}

.round-group h4 {
  margin: 0 0 12px;
  font-size: 14px;
  color: #303133;
}

.round-quotes-table {
  margin-top: 12px;
}

.preview-section h3 {
  margin: 0 0 12px;
  font-size: 16px;
  border-bottom: 1px solid #dcdfe6;
  padding-bottom: 8px;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px 18px;
  margin-bottom: 12px;
}

.info-grid div {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 13px;
}

.description {
  margin: 0;
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
}

.summary-stats {
  display: flex;
  gap: 24px;
  margin-bottom: 12px;
  font-size: 13px;
  color: #374151;
}

.preview-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}

.preview-table th,
.preview-table td {
  border: 1px solid #4b5563;
  padding: 8px;
  text-align: left;
  vertical-align: top;
}

.preview-table thead th {
  background: #f5f7fa;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

@media (max-width: 900px) {
  .preview-header,
  .summary-stats,
  .scope-toolbar {
    flex-direction: column;
    gap: 12px;
    align-items: flex-start;
  }

  .info-grid {
    grid-template-columns: 1fr;
  }
}
</style>
