<template>
  <div class="price-comparison-section">
    <div class="section-header">
      <h4>{{ $t('rfq.lineItemWorkflow.priceComparison') }}</h4>
    </div>

    <!-- Line Items Table -->
    <el-table
      :data="lineItems"
      border
      highlight-current-row
      :row-class-name="getRowClassName"
      @current-change="handleCurrentChange"
      height="400"
      style="width: 100%"
    >
      <el-table-column type="index" label="#" width="50" align="center" />

      <el-table-column
        prop="lineNumber"
        :label="$t('rfq.items.lineNumber')"
        width="80"
        align="center"
      />

      <el-table-column :label="$t('rfq.items.itemName')" min-width="180">
        <template #default="{ row }">
          <div class="item-name">
            <div>{{ row.itemName || row.description }}</div>
            <div class="brand" v-if="row.brand">
              <el-tag size="small" type="info">{{ row.brand }}</el-tag>
            </div>
          </div>
        </template>
      </el-table-column>

      <el-table-column :label="$t('rfq.items.specifications')" width="180">
        <template #default="{ row }">
          <el-tooltip
            v-if="row.specifications"
            :content="row.specifications"
            placement="top"
          >
            <div class="specifications">{{ truncate(row.specifications, 30) }}</div>
          </el-tooltip>
          <span v-else>-</span>
        </template>
      </el-table-column>

      <el-table-column :label="$t('rfq.items.quantity')" width="100">
        <template #default="{ row }">
          {{ row.quantity }} {{ row.unit }}
        </template>
      </el-table-column>

      <el-table-column :label="$t('rfq.lineItemWorkflow.status')" width="140">
        <template #default="{ row }">
          <el-tag :type="getStatusType(row.status)" size="small">
            {{ $t(`rfq.lineItemWorkflow.statuses.${row.status}`) }}
          </el-tag>
        </template>
      </el-table-column>

      <el-table-column
        :label="$t('rfq.lineItemWorkflow.currentApprover')"
        width="120"
      >
        <template #default="{ row }">
          <span>{{ getApproverLabel(row) }}</span>
        </template>
      </el-table-column>
    </el-table>

    <!-- Quote Comparison (Card View) -->
    <div v-if="selectedLineItemId && filteredQuotes.length > 0" class="quote-comparison">
      <div class="quote-header">
        <h4>{{ $t('rfq.lineItemWorkflow.quoteComparison') }}</h4>
        <span class="quote-count">{{ filteredQuotes.length }} {{ $t('rfq.quote.quotes') }}</span>
      </div>

      <!-- Cards View -->
      <div class="quote-cards-wrapper">
        <!-- Detail Button -->
        <div class="detail-button-container">
          <el-button type="primary" text @click="showDetailDialog = true">
            {{ $t('rfq.quoteComparison.viewDetails') }}
            <el-icon><ArrowRight /></el-icon>
          </el-button>
        </div>

        <!-- Supplier Cards -->
        <div class="supplier-cards-grid">
        <div
          v-for="quoteData in filteredQuotes"
          :key="quoteData.id"
          class="supplier-card"
          :class="{
            selected: isQuoteSelected(quoteData.id),
            'has-special-tariff': quoteData.hasSpecialTariff,
            'read-only': isSelectionLocked
          }"
          @click="handleQuoteCardClick(quoteData.id)"
        >
          <div class="card-header">
            <div class="supplier-avatar">
              {{ getSupplierInitials(quoteData.supplierName) }}
            </div>

            <div class="supplier-info">
              <div class="company-name" :title="quoteData.supplierName">
                {{ quoteData.supplierName || '-' }}
              </div>
              <el-tag
                v-if="isSelectionLocked && isQuoteSelected(quoteData.id)"
                type="success"
                size="small"
              >
                {{ $t('rfq.lineItemWorkflow.purchaserSelected') }}
              </el-tag>
              <div class="tag-row">
                <el-tag
                  v-if="quoteData.status && quoteData.status !== 'submitted'"
                  :type="getQuoteStatusType(quoteData.status)"
                  size="small"
                    effect="plain"
                    class="status-pill"
                  >
                    {{ $t(`rfq.quote.statuses.${quoteData.status}`) }}
                  </el-tag>
                  <el-tag
                    v-if="quoteData.hasSpecialTariff"
                    type="warning"
                    size="small"
                    effect="plain"
                    class="tariff-pill"
                  >
                    {{ $t('rfq.quote.specialTariffTag') }}
                  </el-tag>
                </div>
              </div>

              <el-tag
                v-if="quoteData.isLowest"
                size="small"
                type="success"
                effect="dark"
                class="lowest-badge"
              >
                {{ $t('rfq.priceComparison.lowest') }}
              </el-tag>
            </div>

            <div class="price-highlight">
              <span class="price-value">
                {{ formatUnitPrice(quoteData.unitPrice, quoteData.currency, quoteData.unit) }}
              </span>
              <span class="price-sub">
                {{ formatPrice(quoteData.totalAmount, quoteData.currency) }}
              </span>
            </div>

            <div class="moq-chip">
              <span class="meta-label">{{ $t('rfq.quote.moq') }}</span>
              <span class="meta-value">{{ formatQuantity(quoteData.moq, quoteData.unit) }}</span>
            </div>

            <div class="card-footer">
              <span class="selection-hint">
                {{
                  isQuoteSelected(quoteData.id)
                    ? isSelectionLocked
                      ? $t('rfq.lineItemWorkflow.purchaserSelected')
                      : $t('rfq.review.selected')
                    : isSelectionLocked
                      ? $t('rfq.lineItemWorkflow.viewOnly')
                      : $t('rfq.review.select')
                }}
              </span>
              <div
                class="radio-dot"
                :class="{
                  selected: isQuoteSelected(quoteData.id),
                  disabled: isSelectionLocked
                }"
              ></div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <el-empty
      v-else-if="selectedLineItemId"
      :description="$t('rfq.lineItemWorkflow.noQuotes')"
      :image-size="100"
    />

    <!-- Detail Comparison Dialog -->
    <el-dialog
      v-model="showDetailDialog"
      :title="$t('rfq.quoteComparison.detailDialogTitle')"
      width="1200px"
      :close-on-click-modal="false"
    >
      <div v-if="selectedLineItem" class="dialog-header-info">
        <p><strong>{{ $t('rfq.items.itemName') }}:</strong> {{ selectedLineItem.itemName || selectedLineItem.description }}</p>
        <p><strong>{{ $t('rfq.items.quantity') }}:</strong> {{ selectedLineItem.quantity }} {{ selectedLineItem.unit }}</p>
      </div>

      <el-table :data="filteredQuotes" border stripe>
        <el-table-column :label="$t('supplier.companyName')" min-width="160" fixed>
          <template #default="{ row }">
            <el-link
              type="primary"
              :underline="false"
              class="supplier-name-link"
              @click.stop="handleSupplierNameClick(row)"
            >
              {{ row.supplierName }}
              <el-icon class="link-icon"><TopRight /></el-icon>
            </el-link>
          </template>
        </el-table-column>
        <el-table-column :label="$t('rfq.lineItemWorkflow.unitStandardCost')" width="130">
          <template #default="{ row }">
            <strong style="color: #409eff;">{{ formatUnitPrice(row.unitPrice, row.currency, row.unit) }}</strong>
          </template>
        </el-table-column>
        <el-table-column :label="$t('rfq.quote.originalUnitPrice')" width="130">
          <template #default="{ row }">
            {{ formatUnitPrice(row.originalUnitPrice, row.originalCurrency, row.unit) }}
          </template>
        </el-table-column>
        <el-table-column :label="$t('rfq.quoteComparison.shippingFee')" width="120">
          <template #default="{ row }">
            <span v-if="row.shippingFee !== null && row.shippingFee !== undefined">
              {{ formatPrice(row.shippingFee, row.currency) }}
            </span>
            <span v-else>
              {{ formatPercentage(row.freightRate ?? 0) }}
            </span>
          </template>
        </el-table-column>
        <el-table-column :label="$t('rfq.quote.specialTariff')" width="100">
          <template #default="{ row }">
            <span :style="{ color: row.specialTariff > 0 ? '#e6a23c' : '#67c23a' }">
              {{ row.specialTariff > 0 ? `${row.specialTariff}%` : '0%' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column :label="$t('rfq.quote.moq')" width="90" align="center">
          <template #default="{ row }">
            {{ row.moq || '-' }}
          </template>
        </el-table-column>
        <el-table-column :label="$t('rfq.quote.spq')" width="90" align="center">
          <template #default="{ row }">
            {{ row.spq || '-' }}
          </template>
        </el-table-column>
        <el-table-column :label="$t('rfq.quote.deliveryTime')" width="110" prop="deliveryTime" />
        <el-table-column :label="$t('rfq.quote.productOrigin')" width="100" prop="productOrigin" />
        <el-table-column :label="$t('rfq.quoteComparison.quoteAttachments')" min-width="180">
          <template #default="{ row }">
            <div v-if="row.attachments && row.attachments.length" class="quote-attachments-cell">
              <div
                v-for="file in row.attachments"
                :key="file.id || file.storedName || file.originalName || file.downloadUrl"
                class="quote-attachment-row"
              >
                <span class="quote-attachment-name" :title="file.originalName || file.storedName">
                  {{ file.originalName || file.storedName || '-' }}
                </span>
                <el-button
                  size="small"
                  link
                  type="primary"
                  @click.stop="openAttachment(file)"
                >
                  {{ $t('common.view') }}
                </el-button>
                <el-button
                  size="small"
                  link
                  type="info"
                  @click.stop="downloadAttachment(file)"
                >
                  {{ $t('rfq.quotes.download') }}
                </el-button>
              </div>
            </div>
            <span v-else>-</span>
          </template>
        </el-table-column>
      </el-table>

      <template #footer>
        <el-button @click="showDetailDialog = false">{{ $t('common.close') }}</el-button>
      </template>
    </el-dialog>

    <!-- Supplier Profile Dialog -->
    <el-dialog
      v-model="showSupplierProfileDialog"
      :title="$t('rfq.quotes.supplierProfile')"
      width="700px"
    >
      <div v-if="selectedSupplierForProfile" class="supplier-profile">
        <el-tabs type="border-card">
          <el-tab-pane :label="$t('rfq.quotes.basicInfo')">
            <el-descriptions :column="2" border>
              <el-descriptions-item :label="$t('supplier.companyName')">
                {{ selectedSupplierForProfile.companyName || selectedSupplierForProfile.supplierName }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.companyId')">
                {{ selectedSupplierForProfile.companyId || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.stage')">
                <el-tag :type="selectedSupplierForProfile.stage === 'formal' ? 'success' : 'warning'">
                  {{ $t(`supplier.stages.${selectedSupplierForProfile.stage ?? 'null'}`) }}
                </el-tag>
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.category')">
                {{ selectedSupplierForProfile.category || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.region')">
                {{ selectedSupplierForProfile.region || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.address')">
                {{ selectedSupplierForProfile.address || '-' }}
              </el-descriptions-item>
            </el-descriptions>
          </el-tab-pane>

          <el-tab-pane :label="$t('rfq.quotes.financialInfo')">
            <el-descriptions :column="2" border>
              <el-descriptions-item :label="$t('supplier.registeredCapital')">
                <el-text v-if="selectedSupplierForProfile.registeredCapital" type="primary" size="large">
                  {{ formatCurrencyValue(selectedSupplierForProfile.registeredCapital, 'CNY') }}
                </el-text>
                <span v-else>-</span>
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.registrationDate')">
                {{ selectedSupplierForProfile.registrationDate ? formatDateValue(selectedSupplierForProfile.registrationDate) : '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.paymentTerms')">
                {{ selectedSupplierForProfile.paymentTerms || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.paymentCurrency')">
                {{ selectedSupplierForProfile.paymentCurrency || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.bankName')">
                {{ selectedSupplierForProfile.bankName || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.bankAccount')">
                {{ selectedSupplierForProfile.bankAccount || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.taxRegistrationNumber')">
                {{ selectedSupplierForProfile.taxRegistrationNumber || '-' }}
              </el-descriptions-item>
            </el-descriptions>
          </el-tab-pane>

          <el-tab-pane :label="$t('rfq.quotes.legalInfo')">
            <el-descriptions :column="2" border>
              <el-descriptions-item :label="$t('supplier.businessRegistrationNumber')">
                <el-text type="primary">
                  {{ selectedSupplierForProfile.businessRegistrationNumber || '-' }}
                </el-text>
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.legalRepresentative')">
                {{ selectedSupplierForProfile.legalRepresentative || '-' }}
              </el-descriptions-item>
            </el-descriptions>
          </el-tab-pane>

          <el-tab-pane :label="$t('rfq.quotes.contactInfo')">
            <el-descriptions :column="2" border>
              <el-descriptions-item :label="$t('supplier.contactPerson')">
                {{ selectedSupplierForProfile.contactPerson || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.contactPhone')">
                {{ selectedSupplierForProfile.contactPhone || '-' }}
              </el-descriptions-item>
              <el-descriptions-item :label="$t('supplier.contactEmail')" :span="2">
                {{ selectedSupplierForProfile.contactEmail || '-' }}
              </el-descriptions-item>
            </el-descriptions>
          </el-tab-pane>

          <el-tab-pane
            :label="$t('rfq.quotes.documentsInfo')"
            v-if="selectedSupplierForProfile.supplierDocuments?.length"
          >
            <el-table :data="selectedSupplierForProfile.supplierDocuments" border>
              <el-table-column :label="$t('supplier.documentCategory')" width="180">
                <template #default="{ row }">
                  <el-tag size="small">{{ $t(`supplier.documentCategories.${row.category}`) }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column :label="$t('supplier.fileName')" width="200">
                <template #default="{ row }">
                  {{ row.originalName || row.file_name || '-' }}
                </template>
              </el-table-column>
              <el-table-column :label="$t('supplier.uploadedAt')" width="160">
                <template #default="{ row }">
                  {{ formatDateValue(row.uploadedAt || row.uploaded_at) }}
                </template>
              </el-table-column>
              <el-table-column :label="$t('supplier.expiryDate')" width="120">
                <template #default="{ row }">
                  <span v-if="row.expiresAt || row.expiry_date">
                    {{ formatDateValue(row.expiresAt || row.expiry_date) }}
                  </span>
                  <span v-else>-</span>
                </template>
              </el-table-column>
              <el-table-column :label="$t('supplier.verified')" width="100" align="center">
                <template #default="{ row }">
                  <el-icon v-if="row.verified" color="green" size="20"><CircleCheck /></el-icon>
                  <el-icon v-else color="gray" size="20"><Clock /></el-icon>
                </template>
              </el-table-column>
            </el-table>
          </el-tab-pane>
        </el-tabs>
      </div>
    </el-dialog>

    <!-- Quote Details Dialog -->
    <el-dialog
      v-model="detailsDialogVisible"
      :title="$t('rfq.quote.details')"
      width="800px"
    >
      <RfqQuoteComparison
        v-if="selectedQuoteForDetails"
        :quotes="selectedQuoteForDetailsList"
        :line-item="selectedLineItem"
      />
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ArrowRight, TopRight, CircleCheck, Clock } from '@element-plus/icons-vue'
import RfqQuoteComparison from './RfqQuoteComparison.vue'
import { downloadFile, resolveUploadDownloadUrl } from '@/utils/fileDownload'
import type {
  Quote,
  QuoteAttachment,
  QuoteTariffCalculation,
  Rfq,
  RfqItem,
  RfqQuoteItem,
  TariffCalculationResult
} from '@/types'

type LineItemLike = RfqItem & {
  id: number
  status?: string
  currentApproverRole?: string | null
  selectedQuoteId?: number | string | null
  selected_quote_id?: number | string | null
  description?: string | null
  brand?: string | null
  specifications?: string | null
  quantity?: number | null
  unit?: string | null
}

type QuoteItemLike = RfqQuoteItem & {
  rfqLineItemId?: number | null
  freightRate?: number | string | null
  freight_rate?: number | string | null
  freightAmount?: number | string | null
  shippingFee?: number | string | null
  shipping_fee?: number | string | null
  specialTariffRate?: number | string | null
  special_tariff_rate?: number | string | null
  standard_unit_cost_local?: number | string | null
  standard_unit_cost_usd?: number | string | null
  standard_package_quantity?: number | string | null
  standardPackQuantity?: number | string | null
  minimum_order_quantity?: number | string | null
  delivery_period?: number | string | null
  delivery_time?: number | string | null
  unit_price?: number | string | null
  tariffCalculation?: QuoteTariffCalculation | TariffCalculationResult | null
}

type QuoteAttachmentLike = QuoteAttachment & {
  stored_name?: string | null
  storedFileName?: string | null
  stored_file_name?: string | null
  filePath?: string | null
  file_path?: string | null
  fileName?: string | null
  file_name?: string | null
  original_name?: string | null
}

type QuoteLike = Quote & {
  quoteId?: number | string | null
  vendorName?: string | null
  supplier?: { name?: string | null } | null
  deliveryPeriod?: number | string | null
  delivery_period?: number | string | null
  delivery_time?: number | string | null
  freightRate?: number | string | null
  freight_rate?: number | string | null
  freightAmount?: number | string | null
  shippingFee?: number | string | null
  shipping_fee?: number | string | null
  special_tariff_rate?: number | string | null
  exchangeRate?: number | string | null
  unit_price?: number | string | null
  productOrigin?: string | null
  quoteItems?: QuoteItemLike[]
  attachments?: QuoteAttachmentLike[]
}

type UnknownRecord = Record<string, unknown>

type QuoteCardSummary = {
  id: number | string
  supplierName: string
  unitPrice: number | null
  currency: string
  originalCurrency: string  // 供应商原始报价货币
  unit: string
  totalAmount: number | null
  deliveryTime: string | Date | null
  status?: string
  originalUnitPrice: number | null
  shippingFee: number | null
  freightRate: number | null
  specialTariff: number | null
  moq: number | null
  spq: number | null
  productOrigin: string | null
  hasSpecialTariff: boolean
  isLowest?: boolean
  attachments: QuoteAttachment[]
}

const props = defineProps<{
  rfq: Rfq
  lineItems: LineItemLike[]
  quotes: QuoteLike[]
  selectedLineItemId: number | null
  userRole?: string
}>()

const emit = defineEmits<{
  selectItem: [itemId: number]
  selectQuote: [quoteId: number | string]
}>()

const { t } = useI18n()

const showDetailDialog = ref(false)
const detailsDialogVisible = ref(false)
const selectedQuoteForDetails = ref<QuoteLike | null>(null)
const showSupplierProfileDialog = ref(false)
const selectedSupplierForProfile = ref<QuoteLike | null>(null)
const selectedQuoteForDetailsList = computed<QuoteLike[]>(() =>
  selectedQuoteForDetails.value ? [selectedQuoteForDetails.value] : []
)
const currencySymbols: Record<string, string> = {
  CNY: '\u00a5',
  USD: '$',
  EUR: '\u20ac',
  JPY: '\u00a5',
  THB: '\u0e3f'
}
const decimalFormatter = new Intl.NumberFormat('zh-CN', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2
})
const quantityFormatter = new Intl.NumberFormat('zh-CN', {
  maximumFractionDigits: 0
})
const statusApproverFallback: Record<string, string> = {
  pending_director: 'procurement_director',
  pending_po: 'purchaser'
}

/**
 * Get the approver label for a line item based on its status
 */
function getApproverLabel(lineItem: LineItemLike): string {
  // Priority 1: Use currentApproverRole if available
  if (lineItem.currentApproverRole) {
    const roleKey = `common.roles.${lineItem.currentApproverRole}`
    return t(roleKey)
  }

  // Priority 2: Infer from status
  const statusKey = lineItem.status ?? ''
  const approverRole = statusApproverFallback[statusKey]
  if (approverRole) {
    return t(`common.roles.${approverRole}`)
  }

  // Priority 3: Handle special statuses
  if (lineItem.status === 'completed') {
    return t('common.completed')
  }

  if (lineItem.status === 'draft' || lineItem.status === 'rejected') {
    // For draft/rejected status, show the purchaser who needs to submit
    return t('common.roles.purchaser')
  }

  return '-'
}

const selectedLineItem = computed(() => {
  if (!props.selectedLineItemId) return null
  return props.lineItems.find((item) => item.id === props.selectedLineItemId)
})

const selectedQuoteId = computed(() => selectedLineItem.value?.selectedQuoteId ?? null)
const localSelectedQuoteId = ref<string | number | null>(selectedQuoteId.value)
const firstDefinedValue = <T>(...values: Array<T | null | undefined>) => {
  for (const value of values) {
    if (value !== undefined && value !== null && value !== '') {
      return value
    }
  }
  return null
}

const firstDefinedNumber = (...values: Array<number | string | null | undefined>) => {
  for (const value of values) {
    if (value === undefined || value === null || value === '') {
      continue
    }
    const numeric = Number(value)
    if (!Number.isNaN(numeric)) {
      return numeric
    }
  }
  return null
}

const toNumeric = (value: unknown): number | null => {
  if (value === undefined || value === null || value === '') {
    return null
  }
  const numeric = Number(value)
  return Number.isFinite(numeric) ? numeric : null
}

const toRate = (value: unknown): number | null => {
  if (value === undefined || value === null || value === '') {
    return null
  }
  if (typeof value === 'string') {
    const cleaned = value.replace('%', '').trim()
    if (!cleaned.length) {
      return null
    }
    const numeric = Number(cleaned)
    return Number.isNaN(numeric) ? null : numeric / 100
  }
  const numeric = Number(value)
  return Number.isNaN(numeric) ? null : numeric
}

const toRecord = (value: unknown): UnknownRecord | null =>
  value && typeof value === 'object' ? (value as UnknownRecord) : null

const resolveStandardCostUsd = (quoteItem: QuoteItemLike, quote: QuoteLike): number | null => {
  const tariffCalc = quoteItem.tariffCalculation ?? null
  const tariffRecord = toRecord(tariffCalc)

  const directUsd = toNumeric(
    tariffCalc?.standardCostUsd ??
      quoteItem.standardUnitCostUsd ??
      quoteItem.standardCostUsd ??
      quoteItem.standard_unit_cost_usd,
  )
  if (directUsd !== null) {
    return directUsd
  }

  const directLocal = toNumeric(
    tariffCalc?.standardCostLocal ??
      quoteItem.standardUnitCostLocal ??
      quoteItem.standardCostLocal ??
      quoteItem.standard_unit_cost_local,
  )
  const exchangeRate = toNumeric(
    tariffCalc?.exchangeRate ??
      quoteItem.exchangeRate ??
      quote.exchangeRate,
  )
  if (directLocal !== null && exchangeRate !== null && exchangeRate > 0) {
    return directLocal * exchangeRate
  }

  const unitPrice = toNumeric(
    quoteItem.unitPrice ??
    quoteItem.standardUnitCost ??
    quoteItem.standardUnitCostLocal ??
    quote.unitPrice ??
    quote.unit_price,
  )
  if (unitPrice === null || exchangeRate === null || exchangeRate <= 0) {
    return null
  }

  const freightRate =
    toRate(
      tariffRecord?.freightRate ??
        tariffRecord?.freight_rate ??
        tariffRecord?.tariffRate ??
        quoteItem.freightRate ??
        quoteItem.freight_rate ??
        quote.freightRate ??
        quote.freight_rate,
    ) ?? 0
  const specialRate =
    toRate(
      tariffRecord?.specialTariffRate ??
        tariffRecord?.special_tariff_rate ??
        quoteItem.specialTariffRate ??
        quoteItem.special_tariff_rate ??
        quote.special_tariff_rate,
    ) ?? 0

  const totalRate = 1 + freightRate + specialRate
  if (totalRate <= 0) {
    return null
  }

  return unitPrice * exchangeRate * totalRate
}

const pickUnitPrice = (quoteItem: QuoteItemLike, quote: QuoteLike) => {
  const tariffCalc = quoteItem.tariffCalculation ?? null
  const resolvedUsd = resolveStandardCostUsd(quoteItem, quote)
  if (resolvedUsd !== null) {
    return {
      amount: Number(resolvedUsd),
      currency: 'USD',
    }
  }

  const fallbackCandidates = [
    {
      amount: tariffCalc?.standardCost,
      currency: tariffCalc?.standardCostCurrency || 'USD'
    },
    { amount: quoteItem.standardUnitCostLocal, currency: quoteItem.currency },
    { amount: quoteItem.standardUnitCost, currency: quoteItem.currency },
    { amount: quoteItem.unitPrice, currency: quoteItem.currency },
    { amount: quote.unitPrice, currency: quote.currency },
  ]

  for (const candidate of fallbackCandidates) {
    if (candidate.amount !== undefined && candidate.amount !== null && candidate.amount !== '') {
      const numeric = Number(candidate.amount)
      if (Number.isNaN(numeric)) {
        continue
      }
      return {
        amount: numeric,
        currency: candidate.currency || 'USD',
      }
    }
  }

  return {
    amount: null,
    currency: 'USD',
  }
}

watch(
  selectedQuoteId,
  (newValue) => {
    localSelectedQuoteId.value = newValue ?? null
  },
  { immediate: true }
)

const isSelectionLocked = computed(() => {
  if (!selectedLineItem.value) {
    return false
  }

  if (
    props.userRole === 'procurement_director'
  ) {
    return true
  }

  const status = selectedLineItem.value.status ?? ''
  return !['draft', 'rejected'].includes(status)
})

function handleQuoteCardClick(quoteId: number | string) {
  if (isSelectionLocked.value) {
    return
  }
  localSelectedQuoteId.value = quoteId
  emit('selectQuote', quoteId)
}

const filteredQuotes = computed<QuoteCardSummary[]>(() => {
  if (!selectedLineItem.value) return []

  const transformed = props.quotes
    .map((quote) => {
      // Find the quote item for the selected line item
      const quoteItem = quote.quoteItems?.find(
        (item) => item.rfqLineItemId === selectedLineItem.value?.id
      )

      if (!quoteItem) return null
      const tariffCalc = quoteItem.tariffCalculation ?? null
      const tariffRecord = toRecord(tariffCalc)

      const { amount: unitPrice, currency } = pickUnitPrice(quoteItem, quote)
      const unit = selectedLineItem.value?.unit || 'EA'
      const totalAmount = firstDefinedNumber(
        quoteItem.totalAmount,
        quoteItem.totalPrice,
        quote.totalAmount
      )
      const shippingFee = firstDefinedNumber(
        toNumeric(tariffRecord?.freightAmount),
        toNumeric(tariffRecord?.shippingFee),
        toNumeric(tariffRecord?.freightCost),
        quoteItem.freightAmount,
        quoteItem.shippingFee,
        quoteItem.shipping_fee,
        quote.freightAmount,
        quote.shippingFee,
        quote.shipping_fee,
      )
      const specialTariffRate = firstDefinedNumber(
        toNumeric(tariffRecord?.specialTariffRate),
        quoteItem.specialTariffRate,
        quote.special_tariff_rate
      )
      const freightRate =
        toRate(tariffRecord?.freightRate) ??
        toRate(tariffRecord?.freight_rate) ??
        toRate(quoteItem.freightRate) ??
        toRate(quoteItem.freight_rate) ??
        toRate(quote.freightRate) ??
        toRate(quote.freight_rate) ??
        0
      const moq = firstDefinedNumber(
        quoteItem.minimumOrderQuantity,
        quoteItem.minimum_order_quantity,
        quoteItem.moq
      )
      const spq = firstDefinedNumber(
        quoteItem.spq,
        quoteItem.standardPackageQuantity,
        quoteItem.standard_package_quantity,
        quoteItem.standardPackQuantity
      )
      const deliveryPeriod = firstDefinedValue(
        quoteItem.deliveryPeriod,
        quoteItem.delivery_period,
        quoteItem.delivery_time,
        quote.deliveryPeriod,
        quote.delivery_period,
        quote.delivery_time,
        quoteItem.deliveryDate,
        quote.deliveryDate
      )

      return {
        id: quote.id ?? quote.quoteId ?? quoteItem.id ?? `${quote.companyName}-${selectedLineItem.value?.id}`,
        supplierName:
          quote.companyName ||
          quote.supplierName ||
          quote.vendorName ||
          quote.supplier?.name ||
          '-',
        unitPrice,
        currency,
        originalCurrency: quote.currency || 'CNY',
        unit,
        totalAmount,
        deliveryTime: deliveryPeriod,
        status: quote.status,
        // Detail comparison fields
        originalUnitPrice: firstDefinedNumber(
          quoteItem.unitPrice,
          quoteItem.unit_price
        ),
        shippingFee,
        freightRate,
        specialTariff: specialTariffRate,
        moq,
        spq,
        productOrigin: quoteItem.productOrigin ?? quote.productOrigin ?? null,
        hasSpecialTariff: Boolean(quoteItem.tariffCalculation?.hasSpecialTariff),
        attachments: normalizeQuoteAttachments(quote.attachments ?? [])
      }
    })
    .filter((quote): quote is NonNullable<typeof quote> => Boolean(quote)) as QuoteCardSummary[]
  if (!transformed.length) {
    return []
  }

  let visibleQuotes = transformed

  if (props.userRole === 'department_user') {
    const selectedQuoteId =
      selectedLineItem.value?.selectedQuoteId ??
      selectedLineItem.value?.selected_quote_id ??
      null

    if (selectedQuoteId) {
      visibleQuotes = transformed.filter(
        (quote) => Number(quote.id) === Number(selectedQuoteId)
      )
    } else {
      visibleQuotes = transformed.filter((quote) => quote.status === 'selected')
    }
  }

  if (!visibleQuotes.length) {
    return []
  }

  const lowestUnitPrice = visibleQuotes.reduce((min, quote) => {
    if (quote.unitPrice === null || quote.unitPrice === undefined) {
      return min
    }
    return quote.unitPrice < min ? quote.unitPrice : min
  }, Number.POSITIVE_INFINITY)

  return visibleQuotes.map((quote) => ({
    ...quote,
    isLowest:
      lowestUnitPrice !== Number.POSITIVE_INFINITY &&
      quote.unitPrice !== null &&
      quote.unitPrice !== undefined &&
      quote.unitPrice === lowestUnitPrice
  }))
})

function handleCurrentChange(row: LineItemLike | null) {
  if (row) {
    emit('selectItem', row.id)
  }
}

function handleQuoteClick(row: QuoteCardSummary) {
  emit('selectQuote', row.id)
}

function viewQuoteDetails(quote: QuoteLike) {
  selectedQuoteForDetails.value = quote
  detailsDialogVisible.value = true
}

function getRowClassName({ row }: { row: LineItemLike }) {
  if (row.id === props.selectedLineItemId) {
    return 'selected-row'
  }
  return ''
}

function getQuoteRowClassName({ row }: { row: QuoteCardSummary }) {
  if (row.id === selectedLineItem.value?.selectedQuoteId) {
    return 'selected-quote'
  }
  return ''
}

function getStatusType(status: string) {
  const typeMap: Record<string, string> = {
    draft: 'info',
    pending_director: 'warning',
    pending_po: 'primary',
    completed: 'success',
    rejected: 'danger',
  }
  return typeMap[status] || 'info'
}

function getQuoteStatusType(status: string) {
  const typeMap: Record<string, string> = {
    draft: 'info',
    submitted: 'primary',
    selected: 'success',
    rejected: 'danger',
  }
  return typeMap[status] || 'info'
}

const normalizeQuoteAttachments = (
  attachments: QuoteAttachmentLike[] | null | undefined
): QuoteAttachment[] => {
  return (attachments ?? []).map((file) => {
    const storedName =
      file.storedName ??
      file.stored_name ??
      file.storedFileName ??
      file.stored_file_name ??
      null
    const rawPath = file.filePath ?? file.file_path ?? null
    const resolvedDownloadUrl = resolveUploadDownloadUrl(rawPath, storedName, 'rfq-attachments')
    const downloadUrl = resolvedDownloadUrl ?? file.downloadUrl ?? null
    return {
      ...file,
      storedName,
      originalName: file.originalName ?? file.original_name ?? file.fileName ?? file.file_name,
      downloadUrl,
    }
  })
}

function openAttachment(attachment: QuoteAttachment) {
  if (!attachment.downloadUrl) {
    return
  }
  window.open(String(attachment.downloadUrl), '_blank')
}

async function downloadAttachment(attachment: QuoteAttachment) {
  if (!attachment.downloadUrl) {
    return
  }
  const filename = attachment.originalName || attachment.storedName || 'download'
  try {
    await downloadFile(String(attachment.downloadUrl), filename)
  } catch (error) {
    console.error('[RfqPriceComparisonSection] download failed', error)
  }
}

function formatCurrencyValue(value: number | null | undefined, currency: string = 'CNY') {
  if (value === null || value === undefined) return '-'
  const symbol = currencySymbols[currency] || currency
  return `${symbol}${decimalFormatter.format(value)}`
}

function formatPrice(value: number | null | undefined, currency: string = 'CNY') {
  return formatCurrencyValue(value, currency)
}

function formatDateValue(dateStr: string | Date | null | undefined): string {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  if (isNaN(date.getTime())) return '-'
  return date.toLocaleDateString('zh-CN')
}

function truncate(text: string, length: number) {
  if (!text) return ''
  if (text.length <= length) return text
  return text.substring(0, length) + '...'
}

// Format unit price with currency and unit (Option B: ¥7.05/EA)
function formatUnitPrice(price: number | null | undefined, currency: string = 'CNY', unit: string = 'EA'): string {
  const formatted = formatCurrencyValue(price, currency)
  if (formatted === '-') {
    return '-'
  }
  return `${formatted}/${unit}`
}

function formatQuantity(value: number | null | undefined, unit: string = 'EA') {
  if (value === null || value === undefined || value <= 0) {
    return '-'
  }
  return `${quantityFormatter.format(value)} ${unit}`
}

function formatPercentage(rate: number | null | undefined) {
  if (rate === null || rate === undefined || Number.isNaN(rate)) {
    return '-'
  }
  return `${(rate * 100).toFixed(2)}%`
}

function getSupplierInitials(name?: string) {
  if (!name) return '?'
  const trimmed = name.trim()
  if (!trimmed) return '?'

  const parts = trimmed.split(/\s+/).filter(Boolean)
  if (parts.length === 0) {
    return trimmed.slice(0, 2).toUpperCase()
  }
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase()
  }
  return (parts[0][0] + parts[1][0]).toUpperCase()
}

function isQuoteSelected(quoteId: number | string) {
  if (localSelectedQuoteId.value === null || localSelectedQuoteId.value === undefined) {
    return false
  }
  return String(quoteId) === String(localSelectedQuoteId.value)
}

// Handle supplier name click to show profile
function handleSupplierNameClick(row: QuoteCardSummary) {
  // Find the full quote object from props.quotes by matching ID
  const fullQuote = props.quotes.find(q =>
    Number(q.id) === Number(row.id)
  )

  if (fullQuote) {
    selectedSupplierForProfile.value = fullQuote
    showSupplierProfileDialog.value = true
  } else {
    console.warn('[RfqPriceComparisonSection] Full quote not found for ID:', row.id)
  }
}
</script>

<style scoped lang="scss">
.price-comparison-section {
  display: flex;
  flex-direction: column;
  height: 100%;

  .section-header {
    padding: 16px;
    border-bottom: 1px solid #ebeef5;

    h4 {
      margin: 0;
      font-size: 16px;
      font-weight: 600;
    }
  }

  :deep(.selected-row) {
    background-color: #ecf5ff !important;
  }

  :deep(.selected-quote) {
    background-color: #f0f9ff !important;
  }

  .item-name {
    .brand {
      margin-top: 4px;
    }
  }

  .specifications {
    font-size: 12px;
    color: #606266;
    line-height: 1.4;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .quote-comparison {
    margin-top: 20px;
    padding: 0 16px 16px;

    .quote-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;

      h4 {
        margin: 0;
        font-size: 15px;
        font-weight: 600;
      }

      .quote-count {
        font-size: 13px;
        color: #909399;
      }
    }

    .total-amount {
      font-weight: 600;
      color: #409eff;
    }

    .quote-cards-wrapper {
      display: flex;
      gap: 16px;
      align-items: flex-start;
    }

    .detail-button-container {
      flex-shrink: 0;
      padding-top: 8px;
    }

    .supplier-cards-grid {
      flex: 1;
      display: flex;
      gap: 16px;
      flex-wrap: wrap;
    }

    .supplier-card {
      position: relative;
      flex: 1 1 220px;
      min-width: 200px;
      max-width: 240px;
      min-height: 200px;
      padding: 16px 18px;
      border: 1px solid #e4e7ed;
      border-radius: 14px;
      background: linear-gradient(180deg, #ffffff 0%, #f8fbff 100%);
      cursor: pointer;
      transition: transform 0.25s ease, box-shadow 0.25s ease, border-color 0.25s ease;
      display: flex;
      flex-direction: column;
      gap: 12px;

      &:hover {
        border-color: #a0cfff;
        box-shadow: 0 12px 24px rgba(64, 158, 255, 0.16);
        transform: translateY(-4px);
      }

      &.selected {
        border-color: #409eff;
        background: linear-gradient(180deg, #f0f7ff 0%, #ffffff 100%);
        box-shadow: 0 10px 24px rgba(64, 158, 255, 0.24);
      }

      &.has-special-tariff {
        box-shadow: inset 3px 0 0 #f3d19e;
      }

      &.read-only {
        cursor: default;
        pointer-events: none;
        opacity: 0.65;
        border-style: dashed;
        background: #f5f7fa;
        box-shadow: none;

        &:hover {
          transform: none;
          box-shadow: none;
        }
      }

      .card-header {
        display: flex;
        align-items: center;
        gap: 10px;
        width: 100%;
      }

      .supplier-avatar {
        width: 40px;
        height: 40px;
        border-radius: 10px;
        background: #ecf5ff;
        color: #409eff;
        font-weight: 600;
        font-size: 15px;
        display: flex;
        align-items: center;
        justify-content: center;
        text-transform: uppercase;
      }

      .supplier-info {
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 4px;
        overflow: hidden;
      }

      .company-name {
        font-size: 13px;
        font-weight: 600;
        color: #303133;
        line-height: 1.3;
        max-height: 36px;
        overflow: hidden;
        text-overflow: ellipsis;
        word-break: break-word;
      }

      .tag-row {
        display: flex;
        flex-wrap: wrap;
        gap: 4px;
      }

      .status-pill,
      .tariff-pill {
        border-color: transparent;
      }

      .lowest-badge {
        margin-left: auto;
      }

      .price-highlight {
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        gap: 2px;
      }

      .price-value {
        font-size: 22px;
        font-weight: 700;
        color: #1f2d3d;
        line-height: 1.2;
      }

      .price-sub {
        font-size: 12px;
        color: #909399;
      }

      .moq-chip {
        align-self: flex-start;
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 4px 10px;
        border-radius: 999px;
        border: 1px solid #e1f3d8;
        background: #f0f9eb;
      }

      .meta-label {
        font-size: 12px;
        color: #67c23a;
      }

      .meta-value {
        font-size: 13px;
        font-weight: 600;
        color: #2f4056;
      }

      .card-footer {
        margin-top: auto;
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding-top: 12px;
        border-top: 1px dashed #e4e7ed;
      }

      .selection-hint {
        font-size: 12px;
        color: #909399;
      }

      &.selected .selection-hint {
        color: #409eff;
        font-weight: 600;
      }

      &.read-only .selection-hint {
        color: #a0a0a0;
      }

      .radio-dot {
        width: 18px;
        height: 18px;
        border: 2px solid #c0c4cc;
        border-radius: 50%;
        transition: all 0.25s;

        &.selected {
          border-color: #409eff;
          background-color: #409eff;
          position: relative;

          &::after {
            content: '';
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            width: 8px;
            height: 8px;
            background-color: white;
            border-radius: 50%;
          }
        }

        &.disabled {
          border-color: #dcdfe6;
          background-color: #f0f0f0;
        }
      }
    }
  }
}

.dialog-header-info {
  padding: 12px 16px;
  background-color: #f5f7fa;
  border-radius: 4px;
  margin-bottom: 16px;

  p {
    margin: 4px 0;
    font-size: 14px;
    color: #606266;

    strong {
      color: #303133;
      margin-right: 8px;
    }
  }
}

.quote-attachments-cell {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.quote-attachment-row {
  display: flex;
  align-items: center;
  gap: 6px;
}

.quote-attachment-name {
  max-width: 160px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12px;
  color: #606266;
}

/* Supplier name link in comparison dialog */
.supplier-name-link {
  font-weight: 600;
  font-size: 13px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.supplier-name-link .link-icon {
  font-size: 12px;
  opacity: 0.7;
  transition: opacity 0.2s;
}

.supplier-name-link:hover .link-icon {
  opacity: 1;
}

/* Supplier profile dialog styles */
.supplier-profile {
  :deep(.el-tabs__content) {
    padding: 16px;
  }
}
</style>
