<template>
  <div class="purchaser-review-panel">
    <!-- 报价对比表 -->
    <el-card class="comparison-card">
      <template #header>
        <h3>{{ t("rfq.review.quoteComparison") }}</h3>
      </template>

      <el-table :data="comparisonData" border stripe max-height="600px">
        <el-table-column :label="t('rfq.items.itemName')" min-width="180" fixed="left">
          <template #default="{ row }">
            <div class="item-info">
              <div class="item-name">{{ row.itemName }}</div>
              <div v-if="row.specifications" class="item-spec">{{ row.specifications }}</div>
              <div class="item-quantity">{{ row.quantity }} {{ row.unit }}</div>
            </div>
          </template>
        </el-table-column>

        <el-table-column
          v-for="quote in quotes"
          :key="quote.id"
          :label="quote.supplierName"
          width="200"
        >
          <template #header>
            <div class="supplier-header">
              <div class="supplier-name">{{ quote.supplierName }}</div>
              <div class="quote-total">{{ formatPrice(quote.totalAmount, quote.currency) }}</div>
            </div>
          </template>
          <template #default="{ row }">
            <div v-if="row.quotes[quote.id]" class="quote-cell">
              <div class="unit-price">{{ formatPrice(row.quotes[quote.id].unitPrice) }}</div>
              <div class="total-price">
                {{ t("rfq.review.subtotal") }}: {{ formatPrice(row.quotes[quote.id].totalPrice) }}
              </div>
              <div v-if="row.quotes[quote.id].brand" class="brand">
                {{ row.quotes[quote.id].brand }}
              </div>
              <div v-if="row.quotes[quote.id].deliveryDate" class="delivery">
                {{ formatDate(row.quotes[quote.id].deliveryDate) }}
              </div>
            </div>
            <div v-else class="no-quote">-</div>
          </template>
        </el-table-column>
      </el-table>

      <div class="comparison-summary">
        <div class="summary-title">{{ t("rfq.review.quoteSummary") }}</div>
        <el-table :data="quotes" border>
          <el-table-column :label="t('common.supplier')" prop="supplierName" />
          <el-table-column :label="t('rfq.quote.grandTotal')" width="150">
            <template #default="{ row }">
              <span class="total-amount">{{ formatPrice(row.totalAmount, row.currency) }}</span>
            </template>
          </el-table-column>
          <el-table-column :label="t('rfq.quote.paymentTerms')" prop="paymentTerms" />
          <el-table-column :label="t('rfq.quote.deliveryDate')" width="120">
            <template #default="{ row }">
              {{ formatDate(row.deliveryDate) }}
            </template>
          </el-table-column>
          <el-table-column :label="t('common.actions')" width="150" fixed="right">
            <template #default="{ row }">
              <el-button
                :type="selectedQuoteId === row.id ? 'success' : 'primary'"
                size="small"
                @click="selectQuote(row)"
              >
                {{ selectedQuoteId === row.id ? t("rfq.review.selected") : t("rfq.review.select") }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>
    </el-card>

    <!-- 选定供应商信息 -->
    <el-card v-if="selectedQuote" class="selected-quote-card">
      <template #header>
        <h3>{{ t("rfq.review.selectedSupplier") }}</h3>
      </template>
      <el-descriptions :column="2" border>
        <el-descriptions-item :label="t('common.supplier')">
          {{ selectedQuote.supplierName }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.quote.grandTotal')">
          <span class="highlight-amount">
            {{ formatPrice(selectedQuote.totalAmount, selectedQuote.currency) }}
          </span>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.review.selectionReason')" :span="2">
          <el-input
            v-model="selectionReason"
            type="textarea"
            :rows="3"
            :placeholder="t('rfq.review.selectionReasonPlaceholder')"
            maxlength="500"
            show-word-limit
          />
        </el-descriptions-item>
      </el-descriptions>
    </el-card>

    <!-- 价格对比上传 -->
    <!-- Price comparison upload moved to RfqApprovalOperationPanel in line item workflow mode -->

    <!-- 提交评审 -->
    <div class="review-actions">
      <el-button @click="handleCancel">{{ t("common.cancel") }}</el-button>
      <el-button
        type="primary"
        :loading="submitting"
        :disabled="!canSubmit"
        @click="handleSubmitReview"
      >
        {{ t("rfq.review.submitReview") }}
      </el-button>
    </div>
  </div>
</template>

<script setup lang="ts">




import { ref, reactive, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";

import { apiFetch } from "@/api/http";


import { useNotification } from "@/composables";

const notification = useNotification();
interface Quote {
  id: number;
  supplierName: string;
  totalAmount: number;
  currency: string;
  paymentTerms?: string;
  deliveryDate?: string;
  lineItems: any[];
}

const props = defineProps<{
  rfqId: number;
  rfq: any;
  quotes: Quote[];
}>();

const emit = defineEmits<{
  (e: "submitted"): void;
  (e: "cancel"): void;
}>();

const { t } = useI18n();
const selectedQuoteId = ref<number | null>(null);
const selectedQuote = ref<Quote | null>(null);
const selectionReason = ref("");
const submitting = ref(false);

const platforms = [
  { value: "1688", label: "1688" },
  { value: "zkh", label: "震坤行" },
  { value: "jd", label: "京东" },
];

const priceComparisons = reactive<Record<string, any>>({
  "1688": { productUrl: "", platformPrice: null, fileName: "" },
  zkh: { productUrl: "", platformPrice: null, fileName: "" },
  jd: { productUrl: "", platformPrice: null, fileName: "" },
});

// 检查是否需要价格对比（耗材或五金配件）
// Price comparison requirement removed - now optional for all material categories

// 构建对比数据
const comparisonData = computed(() => {
  if (!props.rfq?.lineItems) return [];

  return props.rfq.lineItems.map((lineItem: any) => {
    const quotesMap: Record<number, any> = {};

    props.quotes.forEach((quote) => {
      const quoteLineItem = quote.lineItems.find((ql: any) => ql.rfqLineItemId === lineItem.id);
      if (quoteLineItem) {
        quotesMap[quote.id] = quoteLineItem;
      }
    });

    return {
      itemName: lineItem.itemName,
      specifications: lineItem.specifications,
      quantity: lineItem.quantity,
      unit: lineItem.unit,
      quotes: quotesMap,
    };
  });
});

const canSubmit = computed(() => {
  // Only require selected quote - price comparison is now optional
  return !!selectedQuoteId.value;
});

function selectQuote(quote: Quote) {
  selectedQuoteId.value = quote.id;
  selectedQuote.value = quote;
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

function handleUploadSuccess(response: any, platform: string) {
  if (response.data) {
    priceComparisons[platform].fileName = response.data.fileName;
    notification.success(t("rfq.review.uploadSuccess"));
  }
}

function beforeUpload(file: File) {
  const isImage = file.type.startsWith("image/");
  const isLt10M = file.size / 1024 / 1024 < 10;

  if (!isImage) {
    notification.error(t("rfq.review.uploadImageOnly"));
    return false;
  }
  if (!isLt10M) {
    notification.error(t("rfq.review.uploadSizeLimit"));
    return false;
  }
  return true;
}

async function handleSubmitReview() {
  if (!selectedQuoteId.value) {
    notification.warning(t("rfq.review.selectSupplierFirst"));
    return;
  }

  try {
    await notification.confirm(t("rfq.review.confirmSubmit"), t("common.confirm"), {
      confirmButtonText: t("common.confirm"),
      cancelButtonText: t("common.cancel"),
      type: "warning",
    });

    submitting.value = true;

    // 1. 选定供应商
    await apiFetch(`/rfq-workflow/${props.rfqId}/select-quote`, {
      method: "POST",
      body: {
        selectedQuoteId: selectedQuoteId.value,
        selectionReason: selectionReason.value,
      },
    });

    // 2. 提交评审（发起审批）
    await apiFetch(`/rfq-workflow/${props.rfqId}/submit-review`, {
      method: "POST",
    });

    notification.success(t("rfq.review.submitSuccess"));
    emit("submitted");
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error?.message || t("rfq.review.submitError"));
    }
  } finally {
    submitting.value = false;
  }
}

function handleCancel() {
  emit("cancel");
}




</script>

<style scoped>
.purchaser-review-panel {
  max-width: 1600px;
  margin: 0 auto;
}

.comparison-card {
  margin-bottom: 24px;
}

.item-info {
  padding: 4px 0;
}

.item-name {
  font-weight: 500;
  color: #303133;
  margin-bottom: 4px;
}

.item-spec {
  font-size: 12px;
  color: #909399;
  margin-bottom: 4px;
}

.item-quantity {
  font-size: 13px;
  color: #606266;
}

.supplier-header {
  text-align: center;
}

.supplier-name {
  font-weight: 600;
  color: #303133;
  margin-bottom: 4px;
}

.quote-total {
  font-size: 14px;
  color: #409eff;
  font-weight: 500;
}

.quote-cell {
  padding: 8px 4px;
}

.unit-price {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  margin-bottom: 4px;
}

.total-price {
  font-size: 12px;
  color: #606266;
  margin-bottom: 4px;
}

.brand {
  font-size: 12px;
  color: #909399;
  margin-bottom: 2px;
}

.delivery {
  font-size: 12px;
  color: #909399;
}

.no-quote {
  text-align: center;
  color: #c0c4cc;
  padding: 16px 0;
}

.comparison-summary {
  margin-top: 32px;
}

.summary-title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  margin-bottom: 16px;
}

.total-amount {
  font-size: 16px;
  font-weight: 600;
  color: #409eff;
}

.selected-quote-card {
  margin-bottom: 24px;
}

.highlight-amount {
  font-size: 18px;
  font-weight: 600;
  color: #67c23a;
}

/* Price comparison styles removed - functionality moved to RfqApprovalOperationPanel */

.review-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding-top: 24px;
  border-top: 1px solid #dcdfe6;
}
</style>
