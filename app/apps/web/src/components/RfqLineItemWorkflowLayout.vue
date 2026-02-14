<template>
  <div class="rfq-line-item-workflow" v-loading="loading">
    <el-alert
      v-if="quotesLocked && visibilityReason"
      type="warning"
      :closable="false"
      class="quote-lock-alert"
    >
      <template #title>
        {{ visibilityReason.message }}
      </template>
      <p class="quote-lock-meta">
        <span>
          {{ visibilityReason.submittedCount ?? 0 }}/{{ visibilityReason.totalInvited ?? 0 }}
          {{ $t('rfq.quote.quotes') }}
        </span>
        <span v-if="visibilityReason.deadline">
          · {{ formatDateTime(visibilityReason.deadline) }}
        </span>
      </p>
    </el-alert>

    <!-- Header -->
    <div class="workflow-header">
      <h3>{{ $t('rfq.lineItemWorkflow.title') }}</h3>
      <div class="header-info">
        <el-tag type="info" size="large">
          {{ $t('rfq.lineItemWorkflow.totalItems', { count: lineItems.length }) }}
        </el-tag>
        <el-tag v-if="pendingCount > 0" type="warning" size="large">
          {{ $t('rfq.lineItemWorkflow.pendingItems', { count: pendingCount }) }}
        </el-tag>
        <el-tag v-if="completedCount > 0" type="success" size="large">
          {{ $t('rfq.lineItemWorkflow.completedItems', { count: completedCount }) }}
        </el-tag>

        <!-- Batch Generate PR Excel Button -->
        <el-button
          v-if="canGeneratePr && pendingPoItems.length > 0"
          type="primary"
          :icon="Download"
          @click="handleBatchGeneratePr"
        >
          {{ $t('rfq.pr.batchGenerate') }} ({{ pendingPoItems.length }})
        </el-button>
      </div>
    </div>

    <!-- Main Layout: Left-Right Split -->
    <div class="workflow-content">
      <!-- Left: Price Comparison (60%) -->
      <div class="price-comparison-section">
        <RfqPriceComparisonSection
          :rfq="rfq"
          :line-items="lineItems"
          :quotes="quotes"
          :selected-line-item-id="selectedLineItemId"
          :user-role="userRole"
          @select-item="handleSelectLineItem"
          @select-quote="handleSelectQuote"
        />
      </div>

      <!-- Right: Approval Operations (40%) -->
      <div class="approval-operation-section">
        <RfqApprovalOperationPanel
          v-if="selectedLineItem"
          :rfq="rfq"
          :line-item="selectedLineItem"
          :selected-quote="selectedQuote"
          :user-role="userRole"
          :price-comparisons="rfq.priceComparisons"
          @approve="handleApprove"
          @reject="handleReject"
          @submit="handleSubmit"
          @refresh="loadRfqData"
          @price-uploaded="loadRfqData"
        />
        <el-empty
          v-else
          :description="$t('rfq.lineItemWorkflow.selectItemHint')"
          :image-size="120"
        />
      </div>
    </div>

    <!-- Batch PR Generation Dialog -->
    <RfqPoAttachmentDialog
      v-model="prDialogVisible"
      :rfq-id="rfqId"
      :rfq="rfq"
      :line-items="pendingPoItemsWithQuotes"
      :preselected-item-id="selectedLineItemId"
      @success="handlePrGenerateSuccess"
    />
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted, watch } from "vue";
import { Download } from "@element-plus/icons-vue";
import { useI18n } from "vue-i18n";
import { useAuthStore } from "@/stores/auth";
import { fetchRfqById, fetchRfqWorkflow } from "@/api/rfq";
import {
  submitLineItem,
  directorApprove,
} from "@/api/lineItemWorkflow";
import { resolveUploadDownloadUrl } from "@/utils/fileDownload";
import { firstDefinedValue, pickFirstNumber } from "@/utils/dataParsing";
import { extractErrorMessage } from "@/utils/errorHandling";
import RfqPriceComparisonSection from "./RfqPriceComparisonSection.vue";
import RfqApprovalOperationPanel from "./RfqApprovalOperationPanel.vue";
import RfqPoAttachmentDialog from "./RfqPoAttachmentDialog.vue";
import { useNotification } from "@/composables";
import type { Quote, Rfq, RfqItem, RfqQuoteItem } from "@/types";

const notification = useNotification();

type WorkflowLineItem = RfqItem & {
  id?: number;
  status?: string | null;
  selectedQuoteId?: number | string | null;
  selected_quote_id?: number | string | null;
  lineNumber?: number | string | null;
  line_number?: number | string | null;
  description?: string | null;
  currency?: string | null;
  unitPrice?: number | null;
  unit_price?: number | null;
  totalPrice?: number | null;
  total_price?: number | null;
};

type QuoteItemLike = RfqQuoteItem & {
  lineItemId?: number | string | null;
  rfqLineItemId?: number | string | null;
  rfq_line_item_id?: number | string | null;
  deliveryPeriod?: number | string | null;
  delivery_period?: number | string | null;
  delivery_time?: number | string | null;
  currency_code?: string | null;
  unit_price?: number | string | null;
  total_price?: number | string | null;
};

type QuoteLike = Quote & {
  lineItems?: QuoteItemLike[];
  items?: QuoteItemLike[];
  quoteItems?: QuoteItemLike[];
  unit_price?: number | string | null;
  total_price?: number | string | null;
  deliveryPeriod?: number | string | null;
  delivery_period?: number | string | null;
  supplier_id?: number | string | null;
  supplier_name?: string | null;
  companyName?: string | null;
  company_id?: string | null;
  supplierCode?: string | null;
};

type PriceComparisonLike = Record<string, unknown> & {
  platform?: string | null;
  platformKey?: string | null;
  platform_key?: string | null;
  fileName?: string | null;
  file_name?: string | null;
  filePath?: string | null;
  file_path?: string | null;
  storedFileName?: string | null;
  stored_file_name?: string | null;
  originalFileName?: string | null;
  original_file_name?: string | null;
  productUrl?: string | null;
  product_url?: string | null;
  platformPrice?: number | null;
  platform_price?: number | null;
  downloadUrl?: string | null;
  lineItemId?: number | null;
  line_item_id?: number | null;
};

type VisibilityReasonLike = {
  submittedCount?: number;
  totalInvited?: number;
  deadline?: string | null;
  message?: string;
};

type RfqWorkflowResponse = Rfq & {
  lineItems?: WorkflowLineItem[];
  items?: WorkflowLineItem[];
  quotes?: QuoteLike[];
  priceComparisons?: PriceComparisonLike[];
  quotesVisible?: boolean;
  visibilityReason?: VisibilityReasonLike | null;
};

type NormalizedWorkflowLineItem = WorkflowLineItem & {
  id: number;
  status?: string;
  selectedQuoteId?: number | null;
};

const props = defineProps<{
  rfqId: number
}>()

const { t } = useI18n()
const authStore = useAuthStore()

const loading = ref(false)
const rfq = ref<RfqWorkflowResponse>({} as RfqWorkflowResponse)
const lineItems = ref<NormalizedWorkflowLineItem[]>([])
const quotes = ref<QuoteLike[]>([])
const selectedLineItemId = ref<number | null>(null)
const selectedQuoteId = ref<number | null>(null)
const prDialogVisible = ref(false)

const userRole = computed(() => authStore.user?.role || '')
const quotesLocked = computed(() => {
  return rfq.value.quotesVisible === false && Boolean(rfq.value.visibilityReason)
})
const visibilityReason = computed(() => rfq.value?.visibilityReason || null)

const selectedLineItem = computed(() => {
  if (!selectedLineItemId.value) return null
  return lineItems.value.find((item) => item.id === selectedLineItemId.value)
})

const selectedQuote = computed(() => {
  if (!selectedQuoteId.value) return null
  return quotes.value.find((quote) => quote.id === selectedQuoteId.value)
})

const canGeneratePr = computed(() => userRole.value === 'purchaser')

const canModifySelection = computed(() => {
  if (!selectedLineItem.value) return false
  if (userRole.value !== 'purchaser') return false
  const status = selectedLineItem.value.status ?? ''
  return ['draft', 'rejected'].includes(status)
})

const pendingCount = computed(() => {
  const pendingStatuses = [
    'pending_director',
    'pending_po',
  ]
  return lineItems.value.filter((item) => pendingStatuses.includes(item.status ?? '')).length
})

const completedCount = computed(() => {
  return lineItems.value.filter((item) => (item.status ?? '') === 'completed').length
})

const pendingPoItems = computed(() => {
  return lineItems.value.filter((item) => (item.status ?? '') === 'pending_po')
})

const pendingPoItemsWithQuotes = computed(() => {
  const rawQuotes = Array.isArray(quotes.value) ? quotes.value : []

  return pendingPoItems.value.map((item) => {
    const selectedQuoteId = pickFirstNumber(item.selectedQuoteId, item.selected_quote_id)
    const selectedQuote = selectedQuoteId
      ? rawQuotes.find((quote) => pickFirstNumber(quote.id) === selectedQuoteId)
      : null

    const quoteLineItems = selectedQuote
      ? (selectedQuote.lineItems || selectedQuote.items || selectedQuote.quoteItems || [])
      : []

    const matchedQuoteLineItem = quoteLineItems.find((qli) => {
      const linkedId = firstDefinedValue(qli.rfqLineItemId, qli.lineItemId, qli.rfq_line_item_id)
      return linkedId === item.id
    })

    const resolvedCurrency = String(
      firstDefinedValue(
      matchedQuoteLineItem?.currency,
      matchedQuoteLineItem?.currency_code,
      selectedQuote?.currency,
      item.currency,
      'CNY'
    ) ?? 'CNY')

    const resolvedUnitPrice = pickFirstNumber(
      matchedQuoteLineItem?.unitPrice,
      matchedQuoteLineItem?.unit_price,
      selectedQuote?.unitPrice,
      selectedQuote?.unit_price,
      item.unitPrice,
      item.unit_price,
      0
    ) ?? 0

    const quantity = pickFirstNumber(item.quantity, 1) ?? 1
    const resolvedTotalPrice = pickFirstNumber(
      matchedQuoteLineItem?.totalPrice,
      matchedQuoteLineItem?.total_price,
      selectedQuote?.totalAmount,
      selectedQuote?.total_price,
      item.totalPrice,
      item.total_price,
      resolvedUnitPrice * quantity
    ) ?? 0

    const resolvedSupplierId =
      pickFirstNumber(selectedQuote?.supplierId, selectedQuote?.supplier_id) ?? 0
    const resolvedSupplierName = String(
      firstDefinedValue(
        selectedQuote?.supplierName,
        selectedQuote?.companyName,
        selectedQuote?.supplier_name,
        ''
      ) ?? ''
    )
    const resolvedSupplierCodeRaw = firstDefinedValue(
      selectedQuote?.companyId,
      selectedQuote?.company_id,
      selectedQuote?.supplierCode
    )
    const resolvedSupplierCode =
      resolvedSupplierCodeRaw === null || resolvedSupplierCodeRaw === undefined
        ? undefined
        : String(resolvedSupplierCodeRaw)

    return {
      id: item.id,
      lineNumber: Number(item.lineNumber ?? item.line_number ?? 0),
      quantity: Number(item.quantity ?? 0),
      unit: String(item.unit ?? ""),
      itemName: item.itemName,
      description: item.description ?? undefined,
      currency: item.currency ?? undefined,
      status: item.status ?? undefined,
      selectedQuoteId: selectedQuoteId ?? null,
      selectedQuote: selectedQuote
        ? {
            id: selectedQuote.id,
            supplierId: resolvedSupplierId,
            supplierName: resolvedSupplierName,
            supplierCode: resolvedSupplierCode,
            currency: resolvedCurrency,
            unitPrice: resolvedUnitPrice,
            totalPrice: resolvedTotalPrice,
            deliveryPeriod: firstDefinedValue(
              matchedQuoteLineItem?.deliveryPeriod,
              matchedQuoteLineItem?.delivery_period,
              selectedQuote?.deliveryPeriod,
              selectedQuote?.delivery_period,
              null
            ),
          }
        : null,
    }
  })
})

type UnknownRecord = Record<string, unknown>;

function normalizePriceComparisonRecords(records: unknown[] | undefined | null): PriceComparisonLike[] {
  if (!Array.isArray(records)) return []

  const parseLineItemId = (value: unknown) => {
    if (value === null || value === undefined || value === '') return null
    const numeric = Number(value)
    return Number.isFinite(numeric) ? numeric : null
  }

  const toNullableString = (value: unknown): string | null => {
    if (value === null || value === undefined) return null
    if (typeof value === 'string') {
      const trimmed = value.trim()
      return trimmed ? trimmed : null
    }
    if (typeof value === 'number' || typeof value === 'boolean') {
      return String(value)
    }
    return null
  }

  return records
    .map((record) => (record && typeof record === 'object' ? (record as UnknownRecord) : null))
    .filter((record): record is UnknownRecord => record !== null)
    .map((record) => {
    const platformRaw = record.platform ?? record.platformKey ?? record.platform_key ?? ''
    let platform = String(platformRaw || '').toLowerCase()
    if (platform === 'zhenkunxing') {
      platform = 'zkh'
    }
    if (platform !== '1688' && platform !== 'jd' && platform !== 'zkh') {
      platform = String(platformRaw || '')
    }

    const fileNameValue = firstDefinedValue(
      record.originalFileName,
      record.original_file_name,
      record.fileName,
      record.file_name
    )
    const fileName = toNullableString(fileNameValue)
    const rawPathValue = firstDefinedValue(record.filePath, record.file_path)
    const rawPath = typeof rawPathValue === 'string' ? rawPathValue : null
    const storedFileNameValue = firstDefinedValue(
      record.storedFileName,
      record.stored_file_name,
      rawPath ? rawPath.split('/').pop() : null
    )
    const storedFileName = toNullableString(storedFileNameValue)
    const productUrl = toNullableString(firstDefinedValue(record.productUrl, record.product_url))
    const platformPrice = pickFirstNumber(record.platformPrice, record.platform_price)

    const downloadUrl = resolveUploadDownloadUrl(rawPath, storedFileName, 'rfq_price_comparison')

    return {
      ...record,
      platform,
      fileName,
      originalFileName:
        toNullableString(firstDefinedValue(record.originalFileName, record.original_file_name)) ??
        fileName,
      productUrl,
      platformPrice,
      downloadUrl,
      lineItemId: parseLineItemId(record.lineItemId ?? record.line_item_id),
    }
  })
}

// Load RFQ data
async function loadRfqData() {
  loading.value = true
  try {
    let data: RfqWorkflowResponse
    try {
      data = await fetchRfqWorkflow(props.rfqId)
    } catch (workflowError) {
      console.warn(
        '[RfqLineItemWorkflow] fetchRfqWorkflow failed, falling back to fetchRfqById',
        workflowError
      )
      data = await fetchRfqById(props.rfqId)
    }

    if (!Array.isArray(data.items) && Array.isArray(data.lineItems)) {
      data.items = data.lineItems
    }

    data.priceComparisons = normalizePriceComparisonRecords(data.priceComparisons)
    rfq.value = data

    const detailedLineItems = (
      Array.isArray(data.lineItems)
        ? data.lineItems
        : Array.isArray(data.items)
          ? data.items
          : []
    ) as WorkflowLineItem[]

    const normalizedLineItems = detailedLineItems
      .map((item) => {
        const resolvedId = pickFirstNumber(item.id)
        if (resolvedId === null) {
          return null
        }
        const normalizedSelectedQuoteId = pickFirstNumber(
          item.selectedQuoteId,
          item.selected_quote_id
        )
        return {
          ...item,
          lineNumber: Number(item.lineNumber ?? item.line_number ?? 0),
          id: resolvedId,
          status: item.status ?? undefined,
          selectedQuoteId: normalizedSelectedQuoteId ?? null,
        } as NormalizedWorkflowLineItem
      })
      .filter((item): item is NormalizedWorkflowLineItem => item !== null)

    lineItems.value = normalizedLineItems
    quotes.value = data.quotes || []

    if (!selectedLineItemId.value && normalizedLineItems.length > 0) {
      selectedLineItemId.value = normalizedLineItems[0].id
    }

    if (selectedLineItemId.value) {
      const currentLineItem = normalizedLineItems.find(
        (item) => item.id === selectedLineItemId.value
      )
      const normalizedSelectedQuoteId = currentLineItem?.selectedQuoteId ?? null
      if (normalizedSelectedQuoteId !== null) {
        selectedQuoteId.value = normalizedSelectedQuoteId
      }
    }
  } catch (error: unknown) {
    notification.error(extractErrorMessage(error) || t('common.loadFailed'))
  } finally {
    loading.value = false
  }
}

// Select line item
function handleSelectLineItem(itemId: number) {
  selectedLineItemId.value = itemId
  // Auto-select quote if line item has selected quote
  const item = lineItems.value.find((i) => i.id === itemId)
  const normalizedSelectedQuoteId = item?.selectedQuoteId ?? null
  if (normalizedSelectedQuoteId !== null) {
    selectedQuoteId.value = normalizedSelectedQuoteId
  }
}

// Select quote
function handleSelectQuote(quoteId: number | string) {
  if (!canModifySelection.value) {
    return
  }
  const normalizedQuoteId = pickFirstNumber(quoteId)
  selectedQuoteId.value = normalizedQuoteId ?? null
}

function formatDateTime(value?: string | null) {
  if (!value) return '-'
  try {
    return new Date(value).toLocaleString()
  } catch {
    return value as string
  }
}

// Submit for approval (Purchaser)
async function handleSubmit(quoteId: number) {
  if (!selectedLineItemId.value) return

  loading.value = true
  try {
    await submitLineItem(props.rfqId, selectedLineItemId.value, quoteId)
    notification.success(t('rfq.lineItemWorkflow.submitSuccess'))
    await loadRfqData()
  } catch (error: unknown) {
    notification.error(extractErrorMessage(error) || t('common.operationFailed'))
  } finally {
    loading.value = false
  }
}

// Director approve
async function handleApprove(data: { comments?: string; newQuoteId?: number }) {
  if (!selectedLineItemId.value) return

  loading.value = true
  try {
    const item = selectedLineItem.value
    if (!item) return

    const requestData = {
      decision: 'approved' as const,
      comments: data.comments,
      newQuoteId: data.newQuoteId,
    }

    if (item.status === 'pending_director') {
      await directorApprove(props.rfqId, selectedLineItemId.value, requestData)
      notification.success(t('rfq.lineItemWorkflow.directorApproveSuccess'))
    }

    await loadRfqData()
  } catch (error: unknown) {
    notification.error(extractErrorMessage(error) || t('common.operationFailed'))
  } finally {
    loading.value = false
  }
}

// Director reject
async function handleReject(comments?: string) {
  if (!selectedLineItemId.value) return

  loading.value = true
  try {
    const item = selectedLineItem.value
    if (!item) return

    const requestData = {
      decision: 'rejected' as const,
      comments,
    }

    if (item.status === 'pending_director') {
      await directorApprove(props.rfqId, selectedLineItemId.value, requestData)
      notification.success(t('rfq.lineItemWorkflow.directorRejectSuccess'))
    }

    await loadRfqData()
  } catch (error: unknown) {
    notification.error(extractErrorMessage(error) || t('common.operationFailed'))
  } finally {
    loading.value = false
  }
}

// Batch generate PR Excel
function handleBatchGeneratePr() {
  if (!canGeneratePr.value) {
    return
  }

  if (pendingPoItems.value.length === 0) {
    notification.warning(t('rfq.pr.noLineItemsSelected'))
    return
  }

  prDialogVisible.value = true
}

// Handle PR generation success
function handlePrGenerateSuccess() {
  notification.success(t('rfq.pr.generateSuccess'))
  loadRfqData()
}

// Watch rfqId changes
watch(
  () => props.rfqId,
  () => {
    loadRfqData()
  },
  { immediate: true }
)

onMounted(() => {
  loadRfqData()
})




</script>

<style scoped lang="scss">
.rfq-line-item-workflow {
  .quote-lock-alert {
    margin-bottom: 16px;
  }

  .quote-lock-meta {
    margin: 4px 0 0;
    font-size: 13px;
    color: #606266;
  }

  .workflow-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;
    padding: 16px;
    background: #fff;
    border-radius: 4px;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);

    h3 {
      margin: 0;
      font-size: 18px;
      font-weight: 600;
    }

    .header-info {
      display: flex;
      gap: 12px;
    }
  }

  .workflow-content {
    display: flex;
    gap: 20px;
    min-height: 600px;

    .price-comparison-section {
      flex: 0 0 60%;
      background: #fff;
      border-radius: 4px;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
      overflow: hidden;
    }

    .approval-operation-section {
      flex: 0 0 calc(40% - 20px);
      background: #fff;
      border-radius: 4px;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
      padding: 20px;
      overflow-y: auto;
    }
  }
}
</style>
