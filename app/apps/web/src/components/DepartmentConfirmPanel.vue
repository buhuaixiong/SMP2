<template>
  <div class="department-confirm-panel">
    <el-card>
      <template #header>
        <h3>{{ t("rfq.pr.confirmPR") }}</h3>
      </template>

      <!-- 提示信息 -->
      <el-alert
        :title="t('rfq.pr.confirmInstruction')"
        type="warning"
        :closable="false"
        show-icon
        style="margin-bottom: 24px"
      />

      <!-- RFQ信息 -->
      <el-descriptions :column="2" border style="margin-bottom: 24px">
        <el-descriptions-item :label="t('rfq.form.title')">
          {{ rfq?.title }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.approval.selectedSupplier')">
          {{ selectedQuote?.supplierName }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.quote.grandTotal')">
          <span class="highlight-amount">
            {{ formatPrice(selectedQuote?.totalAmount, selectedQuote?.currency) }}
          </span>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.pr.approvalStatus')">
          <el-tag type="success">{{ t("rfq.approval.status.approved") }}</el-tag>
        </el-descriptions-item>
      </el-descriptions>

      <!-- 需求明细 -->
      <h4 style="margin: 24px 0 16px 0">{{ t("rfq.lineItems.title") }}</h4>
      <el-table :data="rfq?.lineItems" border stripe style="margin-bottom: 24px">
        <el-table-column type="index" :label="t('rfq.items.lineNumber')" width="70" />
        <el-table-column :label="t('rfq.items.itemName')" min-width="150">
          <template #default="{ row }">
            <div>
              <div>{{ row.itemName }}</div>
              <div v-if="row.specifications" class="item-spec">{{ row.specifications }}</div>
            </div>
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.items.quantity')" width="120">
          <template #default="{ row }"> {{ row.quantity }} {{ row.unit }} </template>
        </el-table-column>
        <el-table-column :label="t('rfq.approval.quotePrice')" width="140">
          <template #default="{ row }">
            {{ getQuotePrice(row.id) }}
          </template>
        </el-table-column>
      </el-table>

      <!-- PR信息 -->
      <h4 style="margin: 24px 0 16px 0">{{ t("rfq.pr.prInfo") }}</h4>
      <el-descriptions :column="2" border style="margin-bottom: 24px">
        <el-descriptions-item :label="t('rfq.pr.prNumber')">
          <span class="pr-number">{{ prRecord?.pr_number }}</span>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.pr.prDate')">
          {{ formatDate(prRecord?.pr_date) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.pr.filledBy')">
          {{ prRecord?.filled_by }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.pr.filledAt')">
          {{ formatDateTime(prRecord?.filled_at) }}
        </el-descriptions-item>
        <el-descriptions-item v-if="prRecord?.confirmation_notes" :label="t('rfq.pr.notes')" :span="2">
          {{ prRecord.confirmation_notes }}
        </el-descriptions-item>
      </el-descriptions>

      <!-- 确认表单 -->
      <el-form label-position="top">
        <el-form-item :label="t('rfq.pr.confirmationStatus')">
          <el-radio-group v-model="confirmationStatus">
            <el-radio value="confirmed">{{ t("rfq.pr.confirm") }}</el-radio>
            <el-radio value="rejected">{{ t("rfq.pr.reject") }}</el-radio>
          </el-radio-group>
        </el-form-item>

        <el-form-item :label="t('rfq.pr.confirmationNotes')">
          <el-input
            v-model="confirmationNotes"
            type="textarea"
            :rows="4"
            :placeholder="t('rfq.pr.confirmationNotesPlaceholder')"
            maxlength="500"
            show-word-limit
          />
        </el-form-item>

        <div class="form-actions">
          <el-button @click="handleCancel">{{ t("common.cancel") }}</el-button>
          <el-button
            :type="confirmationStatus === 'confirmed' ? 'success' : 'danger'"
            :loading="submitting"
            @click="handleSubmit"
          >
            {{
              confirmationStatus === "confirmed"
                ? t("rfq.pr.confirmAndComplete")
                : t("rfq.pr.rejectPR")
            }}
          </el-button>
        </div>
      </el-form>
    </el-card>
  </div>
</template>

<script setup lang="ts">




import { ref } from "vue";
import { useI18n } from "vue-i18n";

import { confirmPr } from "@/api/rfq";


import { useNotification } from "@/composables";

const notification = useNotification();
const props = defineProps<{
  rfqId: number;
  rfq: any;
  selectedQuote: any;
  prRecord: any;
}>();

const emit = defineEmits<{
  (e: "confirmed"): void;
  (e: "cancelled"): void;
}>();

const { t } = useI18n();
const submitting = ref(false);
const confirmationStatus = ref<"confirmed" | "rejected">("confirmed");
const confirmationNotes = ref("");

function getQuotePrice(lineItemId: number): string {
  if (!props.selectedQuote?.lineItems) return "-";
  const quoteLineItem = props.selectedQuote.lineItems.find(
    (item: any) => item.rfqLineItemId === lineItemId,
  );
  return quoteLineItem ? formatPrice(quoteLineItem.unitPrice) : "-";
}

function formatPrice(amount?: number, currency = "CNY"): string {
  if (amount === undefined || amount === null) return "-";
  return `${amount.toFixed(2)} ${currency}`;
}

function formatDate(dateString?: string): string {
  if (!dateString) return "-";
  try {
    return new Date(dateString).toLocaleDateString();
  } catch {
    return dateString;
  }
}

function formatDateTime(dateString?: string): string {
  if (!dateString) return "-";
  try {
    return new Date(dateString).toLocaleString();
  } catch {
    return dateString;
  }
}

async function handleSubmit() {
  try {
    const confirmMessage =
      confirmationStatus.value === "confirmed"
        ? t("rfq.pr.confirmSubmitConfirm", { prNumber: props.prRecord?.pr_number })
        : t("rfq.pr.confirmSubmitReject", { prNumber: props.prRecord?.pr_number });

    await notification.confirm(confirmMessage, t("common.confirm"), {
      confirmButtonText: t("common.confirm"),
      cancelButtonText: t("common.cancel"),
      type: confirmationStatus.value === "confirmed" ? "success" : "warning",
    });

    submitting.value = true;

    await confirmPr(props.rfqId, {
      confirmationStatus: confirmationStatus.value,
      confirmationNotes: confirmationNotes.value,
    });

    notification.success(
      confirmationStatus.value === "confirmed"
        ? t("rfq.pr.confirmSuccess")
        : t("rfq.pr.rejectSuccess"),
    );
    emit("confirmed");
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error?.message || t("rfq.pr.confirmError"));
    }
  } finally {
    submitting.value = false;
  }
}

function handleCancel() {
  emit("cancelled");
}




</script>

<style scoped>
.department-confirm-panel {
  max-width: 1200px;
  margin: 0 auto;
}

.highlight-amount {
  font-size: 18px;
  font-weight: 600;
  color: #67c23a;
}

.item-spec {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}

.pr-number {
  font-size: 18px;
  font-weight: 600;
  color: #409eff;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 24px;
  padding-top: 24px;
  border-top: 1px solid #dcdfe6;
}
</style>
