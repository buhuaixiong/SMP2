<template>
  <div class="approval-operation-panel">
    <div class="panel-header">
      <h4>{{ $t('rfq.lineItemWorkflow.approvalOperations') }}</h4>
      <el-tag :type="getStatusType(lineItem.status)" size="small">
        {{ $t(`rfq.lineItemWorkflow.statuses.${lineItem.status}`) }}
      </el-tag>
    </div>

    <!-- Line Item Info -->
    <el-descriptions :column="1" border size="small" class="item-info">
      <el-descriptions-item :label="$t('rfq.items.lineNumber')">
        #{{ lineItem.lineNumber }}
      </el-descriptions-item>
      <el-descriptions-item :label="$t('rfq.items.itemName')">
        {{ lineItem.itemName || lineItem.description }}
      </el-descriptions-item>
      <el-descriptions-item :label="$t('rfq.items.quantity')">
        {{ lineItem.quantity }} {{ lineItem.unit }}
      </el-descriptions-item>
      <el-descriptions-item v-if="selectedQuote" :label="$t('rfq.quote.selectedQuote')">
        <div class="selected-quote-info">
          <div>{{ selectedQuote.supplierName }}</div>
          <div class="price">{{ formatPrice(selectedQuote.totalAmount, selectedQuote.currency) }}</div>
        </div>
      </el-descriptions-item>
    </el-descriptions>

    <el-divider />

    <!-- Operations based on status and role -->

    <!-- Purchaser: Submit for approval (draft/rejected) -->
    <div v-if="canSubmit" class="operation-section">
      <h5>{{ $t('rfq.lineItemWorkflow.submitForApproval') }}</h5>
      <el-alert
        :title="$t('rfq.lineItemWorkflow.selectQuoteHint')"
        type="info"
        :closable="false"
        show-icon
        class="hint-alert"
      />
      <ConfirmButton
        type="primary"
        :disabled="!selectedQuote"
        :loading="submitting"
        :text="$t('rfq.lineItemWorkflow.submitToDirector')"
        :confirm-text="$t('rfq.lineItemWorkflow.confirmSubmit')"
        @confirm="handleSubmit"
        block
      />
    </div>

    <!-- Director: Approve/Reject -->
    <div v-else-if="canDirectorApprove" class="operation-section">
      <h5>{{ $t('rfq.lineItemWorkflow.directorApproval') }}</h5>

      <el-form :model="form" label-position="top">
        <el-form-item :label="$t('rfq.lineItemWorkflow.changeQuote')">
          <el-switch
            v-model="form.changeQuote"
            :active-text="$t('common.yes')"
            :inactive-text="$t('common.no')"
          />
        </el-form-item>

        <el-form-item v-if="form.changeQuote" :label="$t('rfq.quote.selectNewQuote')">
          <el-select v-model="form.newQuoteId" placeholder="Select quote" style="width: 100%">
            <el-option
              v-for="quote in availableQuotes"
              :key="quote.id"
              :label="`${quote.supplierName} - ${formatPrice(quote.totalAmount, quote.currency)}`"
              :value="quote.id"
            />
          </el-select>
        </el-form-item>

        <el-form-item :label="$t('common.comments')">
          <el-input
            v-model="form.comments"
            type="textarea"
            :rows="3"
            :placeholder="$t('rfq.lineItemWorkflow.commentsPlaceholder')"
          />
        </el-form-item>
      </el-form>

      <div v-if="showDirectorInviteButton" class="invite-link">
        <el-button
          type="primary"
          plain
          size="small"
          @click="openInviteDialog('procurement_director')"
        >
          {{ $t('rfq.approval.invitePurchaser') }}
        </el-button>
      </div>

      <div class="action-buttons">
        <ConfirmButton
          type="success"
          :loading="submitting"
          :text="$t('common.approve')"
          :confirm-text="$t('rfq.lineItemWorkflow.confirmApprove')"
          @confirm="handleDirectorApprove"
        />
        <ConfirmButton
          type="danger"
          confirm-type="danger"
          :loading="submitting"
          :text="$t('common.reject')"
          :confirm-text="$t('rfq.lineItemWorkflow.confirmReject')"
          @confirm="handleDirectorReject"
        />
      </div>
    </div>

    <!-- Completed or no action available -->
    <div v-else class="operation-section">
      <el-result
        :icon="lineItem.status === 'completed' ? 'success' : 'info'"
        :title="getResultTitle()"
        :sub-title="getResultSubtitle()"
      />
    </div>

    <!-- Approval History -->
    <el-divider />
    <div class="approval-history">
      <h5>{{ $t('rfq.lineItemWorkflow.approvalHistory') }}</h5>
      <el-button
        type="primary"
        text
        @click="showHistory"
      >
        {{ $t('rfq.lineItemWorkflow.viewHistory') }}
      </el-button>
    </div>

    <!-- Market Price Comparison Upload (Optional) -->
    <el-divider />
    <div v-if="lineItem" class="price-comparison-upload-section">
      <div class="section-header">
        <h5>{{ $t('rfq.lineItemWorkflow.marketPriceReference') }}</h5>
        <el-tag size="small" type="info">{{ $t('rfq.lineItemWorkflow.optionalUpload') }}</el-tag>
      </div>
      <el-alert
        :title="$t('rfq.lineItemWorkflow.priceReferenceHint')"
        type="info"
        :closable="false"
        show-icon
        class="hint-alert"
      />
      <div class="platforms-grid">
        <!-- 1688 Platform -->
        <el-card shadow="hover" class="platform-card">
          <template #header>
            <div class="platform-header">
              <span class="platform-name">1688</span>
            </div>
          </template>
          <PriceComparisonCell
            :rfq-id="rfq.id"
            :line-item="lineItem"
            platform="1688"
            :comparison-data="getPlatformComparison('1688')"
            :can-upload="canUploadMarketPrice"
            @view-detail="handleViewPriceDetail"
            @uploaded="handlePriceUploaded"
          />
        </el-card>

        <!-- JD Platform -->
        <el-card shadow="hover" class="platform-card">
          <template #header>
            <div class="platform-header">
              <span class="platform-name">{{ $t('rfq.priceComparison.jd') }}</span>
            </div>
          </template>
          <PriceComparisonCell
            :rfq-id="rfq.id"
            :line-item="lineItem"
            platform="jd"
            :comparison-data="getPlatformComparison('jd')"
            :can-upload="canUploadMarketPrice"
            @view-detail="handleViewPriceDetail"
            @uploaded="handlePriceUploaded"
          />
        </el-card>

        <!-- ZKH Platform -->
        <el-card shadow="hover" class="platform-card">
          <template #header>
            <div class="platform-header">
              <span class="platform-name">{{ $t('rfq.priceComparison.zkh') }}</span>
            </div>
          </template>
          <PriceComparisonCell
            :rfq-id="rfq.id"
            :line-item="lineItem"
            platform="zkh"
            :comparison-data="getPlatformComparison('zkh')"
            :can-upload="canUploadMarketPrice"
            @view-detail="handleViewPriceDetail"
            @uploaded="handlePriceUploaded"
          />
        </el-card>
      </div>
    </div>

    <PriceComparisonDetailDialog
      v-model:visible="priceDetailDialogVisible"
      :detail-data="priceDetailData"
      :supplier-prices="priceDetailSupplierPrices"
      @download="handleDownloadPriceDetail"
    />

    <!-- Invite Purchaser Dialog -->
    <el-dialog
      v-model="inviteDialogVisible"
      :title="inviteDialogTitle"
      width="520px"
    >
      <el-form label-position="top">
        <el-form-item :label="$t('rfq.approval.selectPurchasers')">
          <el-select
            v-model="inviteForm.purchaserIds"
            multiple
            filterable
            collapse-tags
            style="width: 100%"
            :placeholder="$t('rfq.approval.selectPurchasersPlaceholder')"
          >
            <el-option
              v-for="purchaser in availablePurchasers"
              :key="purchaser.id"
              :label="purchaser.name"
              :value="purchaser.id"
            />
          </el-select>
          <div v-if="availablePurchasers.length === 0" class="invite-hint">
            {{ $t('rfq.approval.selectPurchasersPlaceholder') }}
          </div>
        </el-form-item>
        <el-form-item :label="$t('common.comments')">
          <el-input
            v-model="inviteForm.message"
            type="textarea"
            :rows="3"
            :placeholder="$t('rfq.lineItemWorkflow.commentsPlaceholder')"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="inviteDialogVisible = false">{{ $t('common.cancel') }}</el-button>
        <el-button
          type="primary"
          :loading="inviteSubmitting"
          :disabled="inviteForm.purchaserIds.length === 0"
          @click="submitInvite"
        >
          {{ $t('common.confirm') }}
        </el-button>
      </template>
    </el-dialog>

    <!-- History Dialog -->
    <el-dialog
      v-model="historyDialogVisible"
      :title="$t('rfq.lineItemWorkflow.approvalHistory')"
      width="700px"
    >
      <RfqApprovalHistoryTimeline
        :rfq-id="rfq.id"
        :line-item-id="lineItem.id"
      />
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, reactive, watch } from 'vue'

import { useI18n } from 'vue-i18n'
import { apiFetch } from '@/api/http'
import { invitePurchasers as inviteLineItemPurchasers } from '@/api/lineItemWorkflow'
import RfqApprovalHistoryTimeline from './RfqApprovalHistoryTimeline.vue'
import PriceComparisonCell from './PriceComparisonCell.vue'
import PriceComparisonDetailDialog from './PriceComparisonDetailDialog.vue'
import ConfirmButton from './common/ConfirmButton.vue'


import { useNotification } from "@/composables";
import { extractErrorMessage } from '@/utils/errorHandling'
import type { Quote, Rfq, RfqItem, RfqQuoteItem } from '@/types'

type LineItemLike = RfqItem & {
  id: number
  status?: string
  lineItemId?: number | string | null
  line_item_id?: number | string | null
  lineId?: number | string | null
  line_id?: number | string | null
  itemId?: number | string | null
  item_id?: number | string | null
  lineNumber?: number | string | null
  line_number?: number | string | null
  itemNumber?: number | string | null
  item_number?: number | string | null
  description?: string | null
}

type QuoteItemLike = RfqQuoteItem & {
  lineItemId?: number | string | null
  rfqLineItemId?: number | string | null
  rfq_line_item_id?: number | string | null
  lineId?: number | string | null
  line_id?: number | string | null
  lineNumber?: number | string | null
  line_number?: number | string | null
  itemNumber?: number | string | null
  item_number?: number | string | null
  sku?: string | number | null
}

type QuoteLike = Quote & {
  items?: QuoteItemLike[]
  quoteItems?: QuoteItemLike[]
  supplier_id?: number | string | null
  supplier_name?: string | null
}

type PriceComparisonLike = {
  platform?: string | null
  platformKey?: string | null
  lineItemId?: number | string | null
  line_item_id?: number | string | null
  lineItemNumber?: number | string | null
  line_item_number?: number | string | null
  downloadUrl?: string | null
  platformPrice?: number | null
  productUrl?: string | null
  uploadedAt?: string | null
  originalFileName?: string | null
  original_file_name?: string | null
} & Record<string, unknown>

const notification = useNotification();
const props = defineProps<{
  rfq: Rfq
  lineItem: LineItemLike
  selectedQuote?: QuoteLike | null
  userRole: string
  priceComparisons?: PriceComparisonLike[]
}>()

const emit = defineEmits<{
  approve: [data: { comments?: string; newQuoteId?: number }]
  reject: [comments?: string]
  submit: [quoteId: number]
  refresh: []
  priceUploaded: []
}>()

const { t } = useI18n()

const submitting = ref(false)
const historyDialogVisible = ref(false)
const inviteDialogVisible = ref(false)
const inviteSubmitting = ref(false)
const inviteTargetRole = ref<'procurement_director' | null>(null)
const inviteForm = reactive({
  purchaserIds: [] as Array<string | number>,
  message: '',
})
const availablePurchasers = ref<Array<{ id: string | number; name: string }>>([])
const purchasersLoaded = ref(false)
const priceDetailDialogVisible = ref(false)
const priceDetailData = ref<PriceComparisonLike | null>(null)
const priceDetailSupplierPrices = ref<
  Array<{ supplierId: string | number | undefined; supplierName: string; price: number }>
>([])

const form = reactive({
  changeQuote: false,
  newQuoteId: null as number | null,
  comments: '',
})

const availableQuotes = computed(() => {
  // Get all quotes from RFQ
  return (props.rfq?.quotes || []) as QuoteLike[]
})

const canSubmit = computed(() => {
  const isAuthorized = props.userRole === 'purchaser' || props.userRole === 'admin'
  return (
    isAuthorized &&
    (props.lineItem.status === 'draft' || props.lineItem.status === 'rejected')
  )
})

// @deprecated 新流程不再使用采购经理审批 (2025-01)
const canDirectorApprove = computed(() => {
  const isAuthorized = props.userRole === 'procurement_director' || props.userRole === 'admin'
  return (
    isAuthorized &&
    props.lineItem.status === 'pending_director'
  )
})

const canUploadMarketPrice = computed(() => props.userRole === 'purchaser')

const isDirectorUser = computed(() => props.userRole === 'procurement_director')
const shouldEnableInvite = computed(() => isDirectorUser.value)
const showDirectorInviteButton = computed(() => isDirectorUser.value)
const inviteDialogTitle = computed(() => {
  const base = t('rfq.approval.invitePurchaser')
  if (!inviteTargetRole.value) return base
  return `${base} - ${t('common.roles.procurement_director')}`
})

watch(
  () => shouldEnableInvite.value,
  (enabled) => {
    if (enabled && !purchasersLoaded.value) {
      void loadPurchasers()
    }
  },
  { immediate: true }
)

async function handleSubmit() {
  const quoteId = props.selectedQuote?.id
  if (typeof quoteId !== 'number') return

  submitting.value = true
  try {
    emit('submit', quoteId)
    notification.success(t('rfq.lineItemWorkflow.submitSuccess'))
  } catch (error: unknown) {
    notification.error(t('rfq.lineItemWorkflow.submitFailed'))
  } finally {
    submitting.value = false
  }
}

async function handleDirectorApprove() {
  submitting.value = true
  try {
    const data: { comments?: string; newQuoteId?: number } = {
      comments: form.comments,
    }

    if (form.changeQuote && form.newQuoteId) {
      data.newQuoteId = form.newQuoteId
    }

    emit('approve', data)
    notification.success(t('rfq.lineItemWorkflow.approveSuccess'))
  } catch (error: unknown) {
    notification.error(t('rfq.lineItemWorkflow.approveFailed'))
  } finally {
    submitting.value = false
  }
}

async function handleDirectorReject() {
  submitting.value = true
  try {
    emit('reject', form.comments)
    notification.success(t('rfq.lineItemWorkflow.rejectSuccess'))
  } catch (error: unknown) {
    notification.error(t('rfq.lineItemWorkflow.rejectFailed'))
  } finally {
    submitting.value = false
  }
}

async function loadPurchasers() {
  if (purchasersLoaded.value) return

  try {
    const response = await apiFetch<{ data: Array<{ id: string | number; name: string }> }>('/users', {
      params: { role: 'purchaser' },
    })
    availablePurchasers.value = response.data || []
    purchasersLoaded.value = true
  } catch (error: unknown) {
    console.error('[RfqApprovalOperationPanel] Failed to load purchasers', error)
    notification.error(t('rfq.approval.inviteError'))
  }
}

async function openInviteDialog(role: 'procurement_director') {
  if (!props.rfq?.id || !props.lineItem?.id) {
    notification.error(t('rfq.approval.inviteError'))
    return
  }

  inviteTargetRole.value = role

  if (!purchasersLoaded.value) {
    await loadPurchasers()
  }

  inviteForm.purchaserIds = []
  inviteForm.message = ''
  inviteDialogVisible.value = true
}

async function submitInvite() {
  if (!props.rfq?.id || !props.lineItem?.id) {
    notification.error(t('rfq.approval.inviteError'))
    return
  }

  if (!inviteForm.purchaserIds.length) {
    notification.warning(t('rfq.approval.selectPurchasersFirst'))
    return
  }

  inviteSubmitting.value = true
  try {
    await inviteLineItemPurchasers(props.rfq.id, props.lineItem.id, {
      purchaserIds: inviteForm.purchaserIds,
      message: inviteForm.message,
    })
    notification.success(t('rfq.approval.inviteSuccess'))
    inviteDialogVisible.value = false
  } catch (error: unknown) {
    notification.error(extractErrorMessage(error))
  } finally {
    inviteSubmitting.value = false
  }
}

function showHistory() {
  historyDialogVisible.value = true
}

function getStatusType(status?: string) {
  const resolvedStatus = status ?? 'draft'
  const typeMap: Record<string, string> = {
    draft: 'info',
    pending_director: 'warning',
    completed: 'success',
    rejected: 'danger',
  }
  return typeMap[resolvedStatus] || 'info'
}

function formatPrice(value: number | undefined, currency: string = 'CNY') {
  if (!value) return '-'
  return `${value.toFixed(2)} ${currency}`
}

function getResultTitle() {
  if (props.lineItem.status === 'completed') {
    return t('rfq.lineItemWorkflow.completed')
  }
  return t('rfq.lineItemWorkflow.noActionRequired')
}

function getResultSubtitle() {
  return t(`rfq.lineItemWorkflow.statusDescriptions.${props.lineItem.status}`)
}

function normalizeKey(value: unknown): string | null {
  if (value === null || value === undefined) {
    return null
  }
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value.toString() : null
  }
  if (typeof value === 'string') {
    const trimmed = value.trim()
    if (!trimmed) return null
    const numeric = Number(trimmed)
    if (!Number.isNaN(numeric) && Number.isFinite(numeric)) {
      return numeric.toString()
    }
    return trimmed.toLowerCase()
  }
  try {
    const numeric = Number(value)
    if (!Number.isNaN(numeric) && Number.isFinite(numeric)) {
      return numeric.toString()
    }
  } catch {
    // ignore parse errors
  }
  const strValue = String(value).trim()
  return strValue ? strValue.toLowerCase() : null
}

function buildLineItemKeySet(detail: PriceComparisonLike | null | undefined) {
  const keys = new Set<string>()
  const addKey = (candidate: unknown) => {
    const key = normalizeKey(candidate)
    if (key) {
      keys.add(key)
    }
  }

  const lineItem = props.lineItem || {}
  ;[
    lineItem.id,
    lineItem.lineItemId,
    lineItem.line_item_id,
    lineItem.lineId,
    lineItem.line_id,
    lineItem.itemId,
    lineItem.item_id,
    lineItem.lineNumber,
    lineItem.line_number,
    lineItem.itemNumber,
    lineItem.item_number,
    detail?.lineItemId,
    detail?.lineItemNumber,
    detail?.lineItemId ?? detail?.line_item_id
  ].forEach(addKey)

  return keys
}

function findMatchingQuoteItem(quoteItems: QuoteItemLike[], lineItemKeys: Set<string>) {
  if (!Array.isArray(quoteItems) || quoteItems.length === 0 || lineItemKeys.size === 0) {
    return null
  }

  return quoteItems.find((item) => {
    const candidates = [
      item?.lineItemId,
      item?.rfqLineItemId,
      item?.rfq_line_item_id,
      item?.lineId,
      item?.line_id,
      item?.id,
      item?.lineNumber,
      item?.line_number,
      item?.itemNumber,
      item?.item_number,
      item?.sku
    ]
    return candidates.some((candidate) => {
      const key = normalizeKey(candidate)
      return key !== null && lineItemKeys.has(key)
    })
  })
}

function buildSupplierPriceRows(detail: PriceComparisonLike) {
  if (!props.rfq?.quotes) {
    return []
  }

  const lineItemKeys = buildLineItemKeySet(detail)
  if (lineItemKeys.size === 0) {
    return []
  }

  const rows: Array<{ supplierId: string | number | undefined; supplierName: string; price: number }> = []

  props.rfq.quotes.forEach((quote: QuoteLike) => {
    const items = Array.isArray(quote?.items)
      ? quote.items
      : Array.isArray(quote?.quoteItems)
        ? quote.quoteItems
        : []
    if (items.length === 0) {
      return
    }

    const matchedItem = findMatchingQuoteItem(items, lineItemKeys)
    if (
      matchedItem &&
      matchedItem.unitPrice !== undefined &&
      matchedItem.unitPrice !== null &&
      !Number.isNaN(Number(matchedItem.unitPrice))
    ) {
      rows.push({
        supplierId: quote.supplierId ?? quote.supplier_id ?? quote.id,
        supplierName:
          quote.supplierName ||
          quote.companyName ||
          quote.supplier_name ||
          t('rfq.quote.unknownSupplier'),
        price: Number(matchedItem.unitPrice)
      })
    }
  })

  return rows
}

function handleViewPriceDetail(detail: PriceComparisonLike) {
  if (!detail) return
  priceDetailData.value = detail
  priceDetailSupplierPrices.value = buildSupplierPriceRows(detail)
  priceDetailDialogVisible.value = true
}

function handleDownloadPriceDetail(detail: PriceComparisonLike) {
  const url = detail?.downloadUrl
  if (url) {
    window.open(url, '_blank', 'noopener')
  }
}

// Price Comparison Functions
function getPlatformComparison(platform: string) {
  if (!props.priceComparisons) return null
  const normalizePlatform = (value: string) => {
    const key = String(value || '').toLowerCase()
    if (key === 'zhenkunxing') return 'zkh'
    return key
  }
  const normalizeLineItemId = (value: unknown) => {
    if (value === null || value === undefined || value === '') return null
    const numeric = Number(value)
    return Number.isFinite(numeric) ? numeric : null
  }
  const targetLineItemId = normalizeLineItemId(props.lineItem?.id)
  const targetPlatform = normalizePlatform(platform)

  return props.priceComparisons.find((pc) => {
    const pcPlatform = normalizePlatform(pc.platform ?? pc.platformKey ?? '')
    if (pcPlatform !== targetPlatform) {
      return false
    }
    const pcLineItemId = normalizeLineItemId(pc.lineItemId ?? pc.line_item_id)
    if (targetLineItemId === null) {
      return pcLineItemId === null
    }
    return pcLineItemId === targetLineItemId
  })
}

function handlePriceUploaded() {
  emit('priceUploaded')
}




</script>

<style scoped lang="scss">
.approval-operation-panel {
  .panel-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 16px;

    h4 {
      margin: 0;
      font-size: 15px;
      font-weight: 600;
    }
  }

  .item-info {
    margin-bottom: 16px;

    .selected-quote-info {
      .price {
        margin-top: 4px;
        font-weight: 600;
        color: #409eff;
        font-size: 14px;
      }
    }
  }

  .operation-section {
    h5 {
      margin: 0 0 12px;
      font-size: 14px;
      font-weight: 600;
      color: #303133;
    }

    .hint-alert {
      margin-bottom: 12px;
    }

    .invite-link {
      display: flex;
      justify-content: flex-end;
      margin-top: 6px;
    }

    .action-buttons {
      display: flex;
      gap: 12px;
      margin-top: 16px;

      .el-button {
        flex: 1;
      }
    }
  }

  .approval-history {
    h5 {
      margin: 0 0 12px;
      font-size: 14px;
      font-weight: 600;
      color: #303133;
    }
  }

  .price-comparison-upload-section {
    margin-top: 16px;

    .section-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;

      h5 {
        margin: 0;
        font-size: 14px;
        font-weight: 600;
        color: #303133;
      }
    }

    .hint-alert {
      margin-bottom: 12px;
    }

    .platforms-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 8px;

      .platform-card {
        :deep(.el-card__header) {
          padding: 8px 12px;
          background: #f5f7fa;
        }

        :deep(.el-card__body) {
          padding: 12px;
        }

        .platform-header {
          .platform-name {
            font-size: 13px;
            font-weight: 500;
            color: #606266;
          }
        }
      }
    }
  }

  .invite-hint {
    margin-top: 6px;
    font-size: 12px;
    color: #909399;
  }
}
</style>
