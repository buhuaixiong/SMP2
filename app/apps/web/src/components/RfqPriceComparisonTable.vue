<template>
  <div class="rfq-price-comparison-table">
    <!-- Header Section -->
    <div class="comparison-header">
      <h3>{{ t('rfq.priceComparison.title') }}</h3>
    </div>

    <!-- Main Comparison Table -->
    <el-table
      :data="tableData"
      border
      stripe
      style="width: 100%"
      :empty-text="t('rfq.priceComparison.noData')"
    >
      <!-- Item Number Column -->
      <el-table-column
        :label="t('rfq.priceComparison.table.itemNumber')"
        prop="itemNumber"
        width="150"
        fixed
      >
        <template #default="{ row }">
          <div class="item-cell">
            <strong>{{ row.itemNumber }}</strong>
          </div>
        </template>
      </el-table-column>

      <!-- Item Description Column -->
      <el-table-column
        :label="t('rfq.priceComparison.table.description')"
        prop="description"
        min-width="200"
      >
        <template #default="{ row }">
          <div class="description-cell">
            {{ row.description }}
          </div>
        </template>
      </el-table-column>

      <!-- Dynamic Supplier Columns -->
      <el-table-column
        v-for="supplier in supplierList"
        :key="supplier.id"
        :label="supplier.name"
        width="120"
        align="center"
      >
        <template #default="{ row }">
          <div class="price-cell">
            <template v-if="row.supplierPrices[supplier.id]">
              <div class="price-value">
                {{ formatSupplierPrice(row.supplierPrices[supplier.id]) }}
              </div>
              <div class="price-sub" v-if="row.supplierPrices[supplier.id].originalUnitPrice !== null">
                {{ formatCurrencyValue(row.supplierPrices[supplier.id].originalUnitPrice, row.supplierPrices[supplier.id].originalCurrency || row.supplierPrices[supplier.id].currency) }}
              </div>
              <el-tag
                v-if="row.supplierPrices[supplier.id].isLowest"
                type="success"
                size="small"
                effect="dark"
              >
                {{ t('rfq.priceComparison.lowest') }}
              </el-tag>
              <el-tag
                v-if="row.supplierPrices[supplier.id].hasSpecialTariff"
                type="warning"
                size="small"
                effect="dark"
              >
                {{ t('rfq.quote.specialTariffTag') }}
              </el-tag>
            </template>
            <span v-else class="no-quote">-</span>
          </div>
        </template>
      </el-table-column>

      <!-- Market price columns removed - functionality moved to RfqApprovalOperationPanel -->

      <el-table-column
        :label="t('rfq.priceComparison.table.details')"
        width="150"
        align="center"
      >
        <template #default="{ row }">
          <el-button
            v-if="row.supplierDetails && row.supplierDetails.length"
            type="primary"
            link
            @click="handleViewSupplierDetails(row)"
          >
            {{ t('rfq.priceComparison.viewDetail') }}
          </el-button>
          <span v-else>-</span>
        </template>
      </el-table-column>

      <!-- Recommended Supplier Column -->
      <el-table-column
        :label="t('rfq.priceComparison.table.recommended')"
        width="150"
        align="center"
        fixed="right"
      >
        <template #default="{ row }">
          <div class="recommend-cell">
            <template v-if="row.recommendation">
              <el-icon class="recommend-icon"><StarFilled /></el-icon>
              <div class="supplier-name">{{ row.recommendation.name }}</div>
              <div class="price-advantage" v-if="row.recommendation.advantage">
                {{ row.recommendation.advantage }}
              </div>
            </template>
            <span v-else>-</span>
          </div>
        </template>
      </el-table-column>
    </el-table>

    <!-- Detail Dialog -->
    <PriceComparisonDetailDialog
      v-model:visible="detailDialogVisible"
      :detail-data="currentDetailData"
      :supplier-prices="currentLineItemSupplierPrices"
      @download="handleDownloadScreenshot"
    />

    <el-dialog
      v-model="supplierDetailDialogVisible"
      :title="supplierDetailDialogTitle || t('rfq.priceComparison.detailDialogTitleFallback')"
      width="960px"
      :close-on-click-modal="false"
      class="supplier-detail-dialog"
    >
      <el-table :data="supplierDetailDialogRows" border>
        <el-table-column :label="t('rfq.priceComparison.detailColumns.supplier')" min-width="200">
          <template #default="{ row }">
            <div class="detail-supplier">
              <div class="detail-supplier__name">{{ row.supplierName }}</div>
              <el-tag v-if="row.isLowest" type="success" size="small">
                {{ t('rfq.priceComparison.bestStandardCost') }}
              </el-tag>
              <div class="detail-row">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.standardCost') }}:</span>
                <span class="value">{{ formatCurrencyValue(row.standardCost, row.standardCostCurrency) }}</span>
              </div>
            </div>
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.priceComparison.detailColumns.pricing')" min-width="320">
          <template #default="{ row }">
            <div class="detail-list">
              <div class="detail-row">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.originalPrice') }}:</span>
                <span class="value">{{ formatCurrencyValue(row.originalUnitPrice, row.originalCurrency || row.standardCostCurrency) }}</span>
              </div>
              <div class="detail-row">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.baseTariff') }}:</span>
                <span class="value">{{ formatRate(row.tariffRate) }}</span>
              </div>
              <div class="detail-row">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.specialTariff') }}:</span>
                <span class="value">
                  <span v-if="row.specialTariffRate !== null">{{ formatRate(row.specialTariffRate) }}</span>
                  <span v-else>{{ t('common.none') }}</span>
                </span>
              </div>
              <div class="detail-row" v-if="row.productGroup">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.productGroup') }}:</span>
                <span class="value">{{ row.productGroup }}</span>
              </div>
              <div class="detail-row" v-if="row.productOrigin">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.productOrigin') }}:</span>
                <span class="value">{{ row.productOrigin }}</span>
              </div>
            </div>
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.priceComparison.detailColumns.terms')" min-width="320">
          <template #default="{ row }">
            <div class="detail-list">
              <div class="detail-row">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.moq') }}:</span>
                <span class="value">{{ formatQuantity(row.moq) }}</span>
              </div>
              <div class="detail-row">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.spq') }}:</span>
                <span class="value">{{ formatQuantity(row.spq) }}</span>
              </div>
              <div class="detail-row">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.leadTime') }}:</span>
                <span class="value">{{ formatLeadTime(row.deliveryPeriod) }}</span>
              </div>
              <div class="detail-row" v-if="row.deliveryTerms">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.deliveryTerms') }}:</span>
                <span class="value">{{ row.deliveryTerms }}</span>
              </div>
              <div class="detail-row" v-if="row.shippingCountry">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.shippingCountry') }}:</span>
                <span class="value">{{ row.shippingCountry }}</span>
              </div>
              <div class="detail-row" v-if="row.shippingLocation">
                <span class="label">{{ t('rfq.priceComparison.detailLabels.shippingLocation') }}:</span>
                <span class="value">{{ row.shippingLocation }}</span>
              </div>
            </div>
          </template>
        </el-table-column>
      </el-table>
      <template #footer>
        <el-button @click="supplierDetailDialogVisible = false">{{ t('common.close') }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { StarFilled } from '@element-plus/icons-vue';
import PriceComparisonDetailDialog from './PriceComparisonDetailDialog.vue';
import { getUploadUrl } from '@/utils/fileDownload';
import {
  toNumber,
  pickFirstNumber,
  firstDefinedValue,
  normalizeKey,
  buildLineItemKeySet,
  compareNullableNumbers
} from '@/utils/dataParsing';
import type {
  Quote,
  QuotePriceComparison,
  QuoteTariffCalculation,
  RfqItem,
  RfqQuoteItem,
  TariffCalculationResult
} from '@/types';

type LineItemLike = RfqItem & {
  id: number;
  lineNumber?: number | string | null;
  itemNumber?: number | string | null;
  itemName?: string | null;
  description?: string | null;
};

type QuoteItemLike = RfqQuoteItem & {
  lineItemId?: number | string | null;
  line_item_id?: number | string | null;
  rfqLineItemId?: number | string | null;
  rfq_line_item_id?: number | string | null;
  rfqItemId?: number | string | null;
  rfq_item_id?: number | string | null;
  lineNumber?: number | string | null;
  line_number?: number | string | null;
  lineNo?: number | string | null;
  line_no?: number | string | null;
  itemNumber?: number | string | null;
  item_number?: number | string | null;
  itemNo?: number | string | null;
  item_no?: number | string | null;
  sku?: number | string | null;
  unit_price?: number | string | null;
  minimum_order_quantity?: number | string | null;
  standard_package_quantity?: number | string | null;
  product_group?: string | null;
  product_origin?: string | null;
  delivery_period?: string | number | null;
  delivery_time?: string | number | null;
  tariffCalculation?: QuoteTariffCalculation | TariffCalculationResult | null;
};

type QuoteLike = Quote & {
  items?: QuoteItemLike[];
  quoteItems?: QuoteItemLike[];
  deliveryPeriod?: number | string | null;
  delivery_period?: number | string | null;
  delivery_time?: number | string | null;
  shipping_country?: string | null;
  shipping_location?: string | null;
};

type SupplierPriceEntry = {
  supplierId: number;
  supplierName: string;
  price: number | null;
  standardCost: number | null;
  standardCostCurrency: string;
  currency: string;
  originalUnitPrice: number | null;
  originalCurrency: string | null;
  hasSpecialTariff: boolean;
  isLowest: boolean;
};

type SupplierDetailRow = {
  supplierId: number;
  supplierName: string;
  standardCost: number | null;
  standardCostCurrency: string;
  originalUnitPrice: number | null;
  originalCurrency: string | null;
  tariffRate: number | null;
  specialTariffRate: number | null;
  hasSpecialTariff: boolean;
  productGroup: string | null;
  productOrigin: string | null;
  moq: number | string | null;
  spq: number | string | null;
  deliveryPeriod: number | string | null;
  deliveryTerms: string | null;
  shippingCountry: string | null;
  shippingLocation: string | null;
  isPlaceholder: boolean;
  isLowest: boolean;
};

type TableRow = LineItemLike & {
  itemNumber: string | number;
  description: string;
  supplierPrices: Record<number, SupplierPriceEntry>;
  supplierDetails: SupplierDetailRow[];
  recommendation: { name: string; advantage?: string } | null;
};

type SupplierInfo = { id: number; name: string };
type SupplierPriceSnapshot = { supplierId: number; supplierName: string; price: number };

type PriceComparisonDetail = QuotePriceComparison & {
  lineItemId?: number | null;
  lineItemNumber?: string | number | null;
  downloadUrl?: string | null;
  platform?: string;
  platformPrice?: number | null;
  productUrl?: string | null;
  originalFileName?: string | null;
  original_file_name?: string | null;
};

interface Props {
  rfqId?: number;
  lineItems: LineItemLike[];
  quotes: QuoteLike[];
  priceComparisons: Array<QuotePriceComparison | Record<string, unknown>>;
  materialType?: string;
}

const props = withDefaults(defineProps<Props>(), {
  rfqId: undefined,
  materialType: 'standard'
});

const emit = defineEmits(['refresh']);

const { t } = useI18n();

// Get unique supplier list from quotes
const supplierList = computed(() => {
  const suppliers = new Map<number, SupplierInfo>();

  props.quotes.forEach((quote) => {
    const supplierName = quote.supplierName || quote.companyName;
    if (quote.supplierId && supplierName) {
      suppliers.set(quote.supplierId, {
        id: quote.supplierId,
        name: supplierName
      });
    }
  });

  return Array.from(suppliers.values());
});

// Build table data by combining line items, quotes, and price comparisons
const DEFAULT_STANDARD_COST_CURRENCY = 'USD';

const tableData = computed(() => {
  return props.lineItems.map((item, index): TableRow => {
    const supplierPrices: Record<number, SupplierPriceEntry> = {};
    const supplierDetails: SupplierDetailRow[] = [];

    props.quotes.forEach((quote) => {
      if (!quote || !quote.supplierId) {
        return;
      }

      const supplierName = quote.supplierName || quote.companyName || `#${quote.supplierId}`;
      const quoteItems = Array.isArray(quote.items) && quote.items.length
        ? quote.items
        : (Array.isArray(quote.quoteItems) ? quote.quoteItems : []);

      const quoteItem = findMatchingQuoteItem(quoteItems, item, index);

      if (!quoteItem) {
        supplierDetails.push(buildPlaceholderDetail(quote, supplierName));
        return;
      }

      const calc = quoteItem.tariffCalculation ?? null;
      const standardCost = pickFirstNumber(
        calc?.standardCost,
        quoteItem.standardUnitCost,
        quoteItem.standardUnitCostUsd,
        quoteItem.standardUnitCostLocal
      );
      const standardCostCurrency =
        calc?.standardCostCurrency ||
        ((calc?.standardCostUsd !== null && calc?.standardCostUsd !== undefined) ? 'USD' : (quoteItem.currency || quote.currency || DEFAULT_STANDARD_COST_CURRENCY));
      const originalUnitPrice = pickFirstNumber(
        quoteItem.unitPrice,
        quoteItem.unit_price,
        calc?.originalPrice
      );
      const originalCurrency = quoteItem.currency || calc?.originalCurrency || quote.currency || null;
      const specialTariffRate = pickFirstNumber(calc?.specialTariffRate);
      const hasSpecialTariff = Boolean(
        calc?.hasSpecialTariff ||
        (specialTariffRate !== null && specialTariffRate > 0)
      );

      supplierPrices[quote.supplierId] = {
        supplierId: quote.supplierId,
        supplierName,
        price: standardCost ?? originalUnitPrice ?? null,
        standardCost,
        standardCostCurrency,
        currency: standardCostCurrency,
        originalUnitPrice,
        originalCurrency,
        hasSpecialTariff,
        isLowest: false
      };

      const rawMoq = firstDefinedValue(
        quoteItem.moq,
        quoteItem.minimumOrderQuantity,
        quoteItem.minimum_order_quantity
      ) ?? null;
      const rawSpq = firstDefinedValue(
        quoteItem.spq,
        quoteItem.standardPackageQuantity,
        quoteItem.standard_package_quantity
      ) ?? null;
      const rawDeliveryPeriod = firstDefinedValue(
        quoteItem.deliveryPeriod,
        quoteItem.delivery_period,
        quoteItem.delivery_time,
        quote.deliveryPeriod,
        quote.delivery_period,
        quote.delivery_time
      ) ?? null;

      supplierDetails.push({
        supplierId: quote.supplierId,
        supplierName,
        standardCost,
        standardCostCurrency,
        originalUnitPrice,
        originalCurrency,
        tariffRate: pickFirstNumber(calc?.tariffRate),
        specialTariffRate,
        hasSpecialTariff,
        productGroup: calc?.productGroup || quoteItem.productGroup || quoteItem.product_group || null,
        productOrigin: calc?.productOrigin || quoteItem.productOrigin || quoteItem.product_origin || null,
        moq: rawMoq,
        spq: rawSpq,
        deliveryPeriod: rawDeliveryPeriod ?? null,
        deliveryTerms: quote.deliveryTerms || quoteItem.deliveryTerms || null,
        shippingCountry: calc?.shippingCountry || quote.shippingCountry || quote.shipping_country || null,
        shippingLocation: quote.shippingLocation || quote.shipping_location || null,
        isPlaceholder: false,
        isLowest: false
      });
    });

    const numericStandardCosts = supplierDetails
      .map((detail) => toNumber(detail.standardCost))
      .filter((value): value is number => value !== null);
    const numericOriginalPrices = supplierDetails
      .map((detail) => toNumber(detail.originalUnitPrice))
      .filter((value): value is number => value !== null);

    const useOriginalForComparison = numericStandardCosts.length === 0;
    const supplierPriceList = useOriginalForComparison ? numericOriginalPrices : numericStandardCosts;
    const lowestSupplierPrice = supplierPriceList.length > 0 ? Math.min(...supplierPriceList) : null;

    const annotatedSupplierDetails = supplierDetails.map((detail) => {
      const comparisonValue = useOriginalForComparison
        ? toNumber(detail.originalUnitPrice)
        : toNumber(detail.standardCost);
      const isLowest = lowestSupplierPrice !== null &&
        comparisonValue !== null &&
        comparisonValue === lowestSupplierPrice;
      const priceEntry = supplierPrices[detail.supplierId];
      if (priceEntry) {
        priceEntry.isLowest = isLowest;
        priceEntry.price = comparisonValue ?? priceEntry.price;
      }
      return {
        ...detail,
        isLowest
      };
    });

    const sortedSupplierDetails = annotatedSupplierDetails
      .sort((a, b) => {
        const aValue = useOriginalForComparison ? toNumber(a.originalUnitPrice) : toNumber(a.standardCost);
        const bValue = useOriginalForComparison ? toNumber(b.originalUnitPrice) : toNumber(b.standardCost);
        return compareNullableNumbers(aValue, bValue);
      });

    const overallLowestPrice = supplierPriceList.length > 0 ? Math.min(...supplierPriceList) : null;

    let recommendation: { name: string; advantage?: string } | null = null;
    if (overallLowestPrice !== null) {
      const lowestSupplier = Object.values(supplierPrices).find(
        (sp) => sp.price === overallLowestPrice
      );
      if (lowestSupplier) {
        recommendation = {
          name: lowestSupplier.supplierName
        };
      }
    }

    return {
      ...item,
      itemNumber: item.lineNumber || item.itemNumber || '-',
      description: item.itemName || item.description || '-',
      supplierPrices,
      supplierDetails: sortedSupplierDetails,
      recommendation
    };
  });
});

// Dialog state
const detailDialogVisible = ref(false);
const currentDetailData = ref<PriceComparisonDetail | null>(null);
const currentLineItemSupplierPrices = ref<SupplierPriceSnapshot[]>([]);
const supplierDetailDialogVisible = ref(false);
const supplierDetailDialogRows = ref<SupplierDetailRow[]>([]);
const supplierDetailDialogTitle = ref('');

function formatSupplierPrice(priceData: SupplierPriceEntry | null | undefined): string {
  if (!priceData) {
    return '-';
  }
  const value = toNumber(priceData.standardCost ?? priceData.price);
  const currency = priceData.standardCostCurrency || priceData.currency;
  return formatCurrencyValue(value, currency);
}

function formatCurrencyValue(value: number | null | undefined, currency?: string | null): string {
  const numericValue = toNumber(value);
  if (numericValue === null) {
    return '-';
  }
  const normalizedCurrency = (currency || DEFAULT_STANDARD_COST_CURRENCY).toUpperCase();
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: normalizedCurrency,
      minimumFractionDigits: numericValue >= 1 ? 2 : 4,
      maximumFractionDigits: 4
    }).format(numericValue);
  } catch (error) {
    return `${normalizedCurrency} ${numericValue.toFixed(numericValue >= 1 ? 2 : 4)}`;
  }
}

function formatRate(rate: number | null | undefined): string {
  const numericRate = toNumber(rate);
  if (numericRate === null) {
    return '-';
  }
  return `${(numericRate * 100).toFixed(2)}%`;
}

function formatQuantity(value: unknown): string {
  if (value === null || value === undefined) {
    return '-';
  }
  const numericValue = toNumber(value);
  if (numericValue !== null) {
    return new Intl.NumberFormat().format(numericValue);
  }
  if (typeof value === 'string') {
    return value;
  }
  return String(value);
}

function formatLeadTime(value: unknown): string {
  if (value === null || value === undefined) {
    return '-';
  }
  const numericValue = toNumber(value);
  if (numericValue !== null) {
    return t('rfq.priceComparison.leadTimeDays', { days: numericValue });
  }
  if (typeof value === 'string') {
    const trimmed = value.trim();
    return trimmed === '' ? '-' : trimmed;
  }
  return String(value);
}

function findMatchingQuoteItem(
  quoteItems: QuoteItemLike[],
  lineItem: LineItemLike,
  lineItemIndex: number
): QuoteItemLike | null {
  if (!Array.isArray(quoteItems) || quoteItems.length === 0) {
    return null;
  }
  const lineItemKeys = buildLineItemKeySet(lineItem);
  if (lineItemKeys.size > 0) {
    const fieldGroups = [
      ['lineItemId', 'line_item_id', 'lineId', 'rfqLineItemId', 'rfq_line_item_id', 'rfqItemId', 'rfq_item_id'],
      ['id'],
      ['lineNumber', 'line_number', 'lineNo', 'line_no'],
      ['itemNumber', 'item_number', 'itemNo', 'item_no', 'sku']
    ];
    for (const fields of fieldGroups) {
      const matched = quoteItems.find((qi) =>
        fields.some((field) => {
          const record = qi as Record<string, unknown>;
          const key = normalizeKey(record?.[field]);
          return key !== null && lineItemKeys.has(key);
        })
      );
      if (matched) {
        return matched;
      }
    }
  }
  if (props.lineItems.length === 1 && quoteItems.length === 1) {
    return quoteItems[0];
  }
  if (quoteItems.length === 1) {
    return quoteItems[0];
  }
  if (lineItemIndex >= 0 && lineItemIndex < quoteItems.length) {
    return quoteItems[lineItemIndex];
  }
  return null;
}

function buildPlaceholderDetail(quote: QuoteLike, supplierName: string): SupplierDetailRow {
  return {
    supplierId: quote.supplierId,
    supplierName,
    standardCost: null,
    standardCostCurrency: quote.currency || DEFAULT_STANDARD_COST_CURRENCY,
    originalUnitPrice: null,
    originalCurrency: quote.currency || null,
    tariffRate: null,
    specialTariffRate: null,
    hasSpecialTariff: false,
    productGroup: null,
    productOrigin: null,
    moq: null,
    spq: null,
    deliveryPeriod: null,
    deliveryTerms: quote.deliveryTerms || null,
    shippingCountry: quote.shippingCountry || quote.shipping_country || null,
    shippingLocation: quote.shippingLocation || quote.shipping_location || null,
    isPlaceholder: true,
    isLowest: false
  };
}

function handleViewSupplierDetails(row: TableRow) {
  const details = Array.isArray(row.supplierDetails) ? row.supplierDetails : [];
  if (!details.length) {
    supplierDetailDialogRows.value = [];
    supplierDetailDialogVisible.value = false;
    return;
  }

  supplierDetailDialogRows.value = details;
  const titleParts: string[] = [];
  if (row.itemNumber && row.itemNumber !== '-') {
    titleParts.push(String(row.itemNumber));
  }
  if (row.description && row.description !== '-') {
    titleParts.push(row.description);
  }
  supplierDetailDialogTitle.value = titleParts.join(' - ');
  supplierDetailDialogVisible.value = true;
}

function handleViewDetail(data: PriceComparisonDetail) {
  currentDetailData.value = data;

  const lineItemId = typeof data.lineItemId === 'number' ? data.lineItemId : null;
  const lineItem = lineItemId ? props.lineItems.find((item) => item.id === lineItemId) : null;
  if (lineItem) {
    const supplierPrices: SupplierPriceSnapshot[] = [];
    props.quotes.forEach((quote) => {
      const items = quote.items || quote.quoteItems || [];
      const quoteItem = items.find((qi) => {
        const qiLineItemId =
          qi.lineItemId ?? qi.rfqLineItemId ?? qi.rfq_line_item_id ?? qi.line_item_id ?? null;
        return qiLineItemId !== null && Number(qiLineItemId) === lineItem.id;
      });

      const supplierName = quote.supplierName || quote.companyName || `#${quote.supplierId}`;

      const unitPrice = quoteItem ? toNumber(quoteItem.unitPrice) : null;
      if (unitPrice !== null && quote.supplierId) {
        supplierPrices.push({
          supplierId: quote.supplierId,
          supplierName,
          price: unitPrice
        });
      }
    });
    currentLineItemSupplierPrices.value = supplierPrices;
  }

  detailDialogVisible.value = true;
}

function handleDownloadScreenshot(data: PriceComparisonDetail) {
  if (data.downloadUrl) {
    window.open(data.downloadUrl, '_blank');
  }
}

function handlePriceUploaded() {
  emit('refresh');
}
</script>

<style scoped lang="scss">
.rfq-price-comparison-table {
  margin-top: 20px;

  .comparison-header {
    margin-bottom: 16px;

    h3 {
      font-size: 18px;
      font-weight: 600;
      margin: 0 0 8px 0;
      color: #303133;
    }

    .subtitle {
      font-size: 14px;
      color: #909399;
      margin: 0;
    }
  }

  .item-cell {
    strong {
      font-weight: 600;
      color: #303133;
    }
  }

  .description-cell {
    font-size: 14px;
    color: #606266;
    line-height: 1.5;
  }

  .price-cell {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 4px;

    .price-value {
      font-size: 16px;
      font-weight: 600;
      color: #303133;
    }

    .no-quote {
      color: #C0C4CC;
      font-size: 18px;
    }
  }

  .recommend-cell {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 4px;

    .recommend-icon {
      color: #F56C6C;
      font-size: 20px;
    }

    .supplier-name {
      font-weight: 600;
      color: #303133;
    }

    .price-advantage {
      font-size: 12px;
      color: #67C23A;
    }
  }

:deep(.supplier-detail-dialog) {
    .detail-supplier {
      display: flex;
      flex-direction: column;
      gap: 6px;

      &__name {
        font-weight: 600;
        color: #303133;
      }
    }

    .detail-list {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .detail-row {
      display: flex;
      gap: 8px;
      font-size: 13px;

      .label {
        color: #909399;
        min-width: 120px;
      }

      .value {
        color: #303133;
        flex: 1;
      }
    }
  }
}
</style>
