<template>
  <el-dialog
    v-model="dialogVisible"
    :title="dialogTitle"
    width="900px"
    @close="handleClose"
  >
    <div class="comparison-detail" v-if="detailData">
      <!-- Screenshot Preview -->
      <div class="screenshot-section">
        <h4>{{ t('rfq.priceComparison.dialog.screenshot') }}</h4>
        <div class="screenshot-preview">
          <el-image
            v-if="detailData.downloadUrl"
            :src="detailData.downloadUrl"
            :preview-src-list="[detailData.downloadUrl]"
            fit="contain"
            style="width: 100%; max-height: 400px;"
          >
            <template #error>
              <div class="image-error">
                <el-icon><PictureFilled /></el-icon>
                <span>{{ t('rfq.priceComparison.dialog.imageLoadFailed') }}</span>
              </div>
            </template>
          </el-image>
          <div v-else class="no-image">
            {{ t('rfq.priceComparison.dialog.noImage') }}
          </div>
        </div>
      </div>

      <!-- Platform Information -->
      <div class="info-section">
        <h4>{{ t('rfq.priceComparison.dialog.platformInfo') }}</h4>
        <el-descriptions :column="2" border>
          <el-descriptions-item :label="t('rfq.priceComparison.dialog.platform')">
            {{ platformName }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('rfq.priceComparison.dialog.price')">
            <span class="price-value">¥{{ detailData.platformPrice?.toFixed(2) || '-' }}</span>
          </el-descriptions-item>
          <el-descriptions-item :label="t('rfq.priceComparison.dialog.productUrl')" :span="2">
            <template v-if="detailData.productUrl">
              <a :href="detailData.productUrl" target="_blank" class="product-link">
                {{ detailData.productUrl }}
                <el-icon><Link /></el-icon>
              </a>
            </template>
            <span v-else>-</span>
          </el-descriptions-item>
          <el-descriptions-item :label="t('rfq.priceComparison.dialog.uploadTime')">
            {{ formatDate(detailData.uploadedAt) }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('rfq.priceComparison.dialog.fileName')">
            {{ detailData.originalFileName || detailData.original_file_name || '-' }}
          </el-descriptions-item>
        </el-descriptions>
      </div>

      <!-- Supplier Prices Comparison -->
      <div class="supplier-prices-section" v-if="supplierPrices && supplierPrices.length > 0">
        <h4>{{ t('rfq.priceComparison.dialog.supplierPrices') }}</h4>
        <el-table :data="supplierPrices" border>
          <el-table-column :label="t('rfq.priceComparison.dialog.supplier')" prop="supplierName" />
          <el-table-column :label="t('rfq.priceComparison.dialog.price')" align="right">
            <template #default="{ row }">
              <span class="price-value">¥{{ row.price?.toFixed(4) }}</span>
            </template>
          </el-table-column>
          <el-table-column :label="t('rfq.priceComparison.dialog.comparison')" align="center">
            <template #default="{ row }">
              <template v-if="detailData.platformPrice">
                <el-tag
                  v-if="row.price < detailData.platformPrice"
                  type="success"
                >
                  {{ t('rfq.priceComparison.dialog.lower') }} {{ ((detailData.platformPrice - row.price) / detailData.platformPrice * 100).toFixed(1) }}%
                </el-tag>
                <el-tag
                  v-else-if="row.price > detailData.platformPrice"
                  type="danger"
                >
                  {{ t('rfq.priceComparison.dialog.higher') }} {{ ((row.price - detailData.platformPrice) / detailData.platformPrice * 100).toFixed(1) }}%
                </el-tag>
                <el-tag v-else type="info">
                  {{ t('rfq.priceComparison.dialog.equal') }}
                </el-tag>
              </template>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- Price Analysis -->
      <div class="analysis-section" v-if="supplierPrices && supplierPrices.length > 0 && detailData.platformPrice">
        <h4>{{ t('rfq.priceComparison.dialog.analysis') }}</h4>
        <el-alert
          :title="analysisTitle"
          :type="analysisType"
          :description="analysisDescription"
          show-icon
          :closable="false"
        />
      </div>
    </div>

    <template #footer>
      <el-button @click="handleDownload" v-if="detailData?.downloadUrl">
        <el-icon><Download /></el-icon>
        {{ t('rfq.priceComparison.dialog.download') }}
      </el-button>
      <el-button type="primary" @click="handleClose">
        {{ t('common.close') }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { computed, watch, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { PictureFilled, Link, Download } from '@element-plus/icons-vue';

interface Props {
  visible: boolean;
  detailData: any;
  supplierPrices?: any[];
}

const props = defineProps<Props>();
const emit = defineEmits(['update:visible', 'download']);

const { t } = useI18n();

const dialogVisible = ref(props.visible);

watch(() => props.visible, (val) => {
  dialogVisible.value = val;
});

watch(dialogVisible, (val) => {
  emit('update:visible', val);
});

const dialogTitle = computed(() => {
  if (!props.detailData) return '';
  return t('rfq.priceComparison.dialog.title', {
    item: props.detailData.lineItemNumber || '',
    platform: platformName.value
  });
});

const platformName = computed(() => {
  if (!props.detailData) return '';
  const platform = props.detailData.platform;
  const map: Record<string, string> = {
    '1688': '1688',
    'jd': t('rfq.priceComparison.platforms.jd'),
    'zkh': t('rfq.priceComparison.platforms.zkh')
  };
  return map[platform] || platform;
});

const analysisType = computed(() => {
  if (!props.supplierPrices || !props.detailData?.platformPrice) return 'info';

  const lowestSupplierPrice = Math.min(...props.supplierPrices.map(sp => sp.price));

  if (lowestSupplierPrice < props.detailData.platformPrice) {
    return 'success'; // Supplier price is better
  } else if (lowestSupplierPrice > props.detailData.platformPrice) {
    return 'warning'; // Market price is better
  }
  return 'info';
});

const analysisTitle = computed(() => {
  if (!props.supplierPrices || !props.detailData?.platformPrice) return '';

  const lowestSupplierPrice = Math.min(...props.supplierPrices.map(sp => sp.price));
  const lowestSupplier = props.supplierPrices.find(sp => sp.price === lowestSupplierPrice);

  if (lowestSupplierPrice < props.detailData.platformPrice) {
    const diff = ((props.detailData.platformPrice - lowestSupplierPrice) / props.detailData.platformPrice * 100).toFixed(1);
    return t('rfq.priceComparison.dialog.supplierBetter', {
      supplier: lowestSupplier?.supplierName,
      percent: diff
    });
  } else if (lowestSupplierPrice > props.detailData.platformPrice) {
    const diff = ((lowestSupplierPrice - props.detailData.platformPrice) / props.detailData.platformPrice * 100).toFixed(1);
    return t('rfq.priceComparison.dialog.marketBetter', { percent: diff });
  }
  return t('rfq.priceComparison.dialog.pricesEqual');
});

const analysisDescription = computed(() => {
  if (!props.supplierPrices || !props.detailData?.platformPrice) return '';

  const lowestSupplierPrice = Math.min(...props.supplierPrices.map(sp => sp.price));

  if (lowestSupplierPrice < props.detailData.platformPrice) {
    return t('rfq.priceComparison.dialog.recommendSupplier');
  } else if (lowestSupplierPrice > props.detailData.platformPrice) {
    return t('rfq.priceComparison.dialog.considerMarket');
  }
  return '';
});

function formatDate(dateStr: string | null | undefined): string {
  if (!dateStr) return '-';
  try {
    return new Date(dateStr).toLocaleString();
  } catch {
    return dateStr;
  }
}

function handleClose() {
  dialogVisible.value = false;
}

function handleDownload() {
  emit('download', props.detailData);
}
</script>

<style scoped lang="scss">
.comparison-detail {
  .screenshot-section,
  .info-section,
  .supplier-prices-section,
  .analysis-section {
    margin-bottom: 24px;

    h4 {
      font-size: 16px;
      font-weight: 600;
      margin: 0 0 12px 0;
      color: #303133;
    }
  }

  .screenshot-preview {
    background: #f5f7fa;
    border-radius: 4px;
    padding: 16px;
    text-align: center;

    .image-error,
    .no-image {
      padding: 40px;
      color: #909399;
      font-size: 14px;

      .el-icon {
        font-size: 48px;
        margin-bottom: 12px;
      }
    }
  }

  .price-value {
    font-size: 16px;
    font-weight: 600;
    color: #E6A23C;
  }

  .product-link {
    color: #409EFF;
    text-decoration: none;
    display: inline-flex;
    align-items: center;
    gap: 4px;

    &:hover {
      text-decoration: underline;
    }
  }
}
</style>
