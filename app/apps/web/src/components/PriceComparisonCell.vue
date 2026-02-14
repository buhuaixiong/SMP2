<template>
  <div class="price-comparison-cell">
    <template v-if="comparisonData">
      <!-- Uploaded state -->
      <div class="uploaded-state">
        <el-tag type="success" size="small">
          {{ t('rfq.priceComparison.cell.uploaded') }}
        </el-tag>
        <div class="price-display" v-if="comparisonData.platformPrice">
          ¥{{ comparisonData.platformPrice.toFixed(2) }}
        </div>
        <el-button
          text
          type="primary"
          size="small"
          @click="viewDetail"
        >
          {{ t('rfq.priceComparison.cell.viewDetail') }}
        </el-button>
      </div>
    </template>

    <template v-else>
      <!-- Not uploaded state -->
      <div v-if="canUpload" class="not-uploaded-state">
        <el-button
          size="small"
          type="primary"
          plain
          :disabled="!rfqId"
          @click="showUploadDialog"
        >
          <el-icon><Upload /></el-icon>
          {{ t('rfq.lineItemWorkflow.uploadMarketPrice') }}
        </el-button>
      </div>
      <div v-else class="upload-restricted-state">
        <el-icon class="lock-icon"><Lock /></el-icon>
        <div class="restricted-copy">
          <span class="title">{{ t('rfq.priceComparison.cell.purchaserOnly') }}</span>
          <span class="hint">{{ t('rfq.priceComparison.cell.viewOnlyHint') }}</span>
        </div>
      </div>
    </template>

    <!-- Upload Dialog -->
    <el-dialog
      v-model="uploadDialogVisible"
      :title="t('rfq.priceComparison.uploadDialog.title', { platform: platformName, item: lineItem.itemNumber })"
      width="500px"
    >
      <el-form :model="uploadForm" label-width="100px" @submit.prevent="handleUpload">
        <el-form-item :label="t('rfq.priceComparison.uploadDialog.screenshot')">
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :limit="1"
            accept="image/*"
            :on-change="handleFileChange"
            :file-list="fileList"
          >
            <el-button size="small">{{ t('rfq.priceComparison.uploadDialog.chooseFile') }}</el-button>
            <template #tip>
              <div class="el-upload__tip">
                {{ t('rfq.priceComparison.uploadDialog.fileHint') }}
              </div>
            </template>
          </el-upload>
        </el-form-item>

        <el-form-item :label="t('rfq.priceComparison.uploadDialog.price')" required>
          <el-input
            v-model.number="uploadForm.price"
            type="number"
            :placeholder="t('rfq.priceComparison.uploadDialog.pricePlaceholder')"
            step="0.01"
          >
            <template #prepend>¥</template>
          </el-input>
        </el-form-item>

        <el-form-item :label="t('rfq.priceComparison.uploadDialog.productUrl')">
          <el-input
            v-model="uploadForm.url"
            :placeholder="t('rfq.priceComparison.uploadDialog.urlPlaceholder')"
          />
        </el-form-item>

        <el-form-item :label="t('rfq.priceComparison.uploadDialog.notes')">
          <el-input
            v-model="uploadForm.notes"
            type="textarea"
            :rows="3"
            :placeholder="t('rfq.priceComparison.uploadDialog.notesPlaceholder')"
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="uploadDialogVisible = false">
          {{ t('common.cancel') }}
        </el-button>
        <el-button type="primary" :loading="uploading" @click="handleUpload">
          {{ t('common.submit') }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed } from 'vue';
import { useI18n } from 'vue-i18n';

import { Upload, Lock } from '@element-plus/icons-vue';
import { uploadPriceComparison } from '@/api/rfq';


import { useNotification } from "@/composables";

const notification = useNotification();
interface Props {
  rfqId?: number;
  lineItem: any;
  platform: string;
  comparisonData?: any;
  canUpload?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  rfqId: undefined,
  canUpload: true
});
const emit = defineEmits(['uploaded', 'viewDetail']);

const { t } = useI18n();

const uploadDialogVisible = ref(false);
const uploading = ref(false);
const fileList = ref<any[]>([]);
const selectedFile = ref<File | null>(null);

const uploadForm = ref({
  price: null as number | null,
  url: '',
  notes: ''
});

const platformName = computed(() => {
  const map: Record<string, string> = {
    '1688': '1688',
    'jd': t('rfq.priceComparison.platforms.jd'),
    'zkh': t('rfq.priceComparison.platforms.zkh')
  };
  return map[props.platform] || props.platform;
});

function showUploadDialog() {
  if (!props.canUpload) {
    return;
  }
  uploadDialogVisible.value = true;
  // Reset form
  uploadForm.value = {
    price: null,
    url: '',
    notes: ''
  };
  fileList.value = [];
  selectedFile.value = null;
}

function handleFileChange(file: any) {
  selectedFile.value = file.raw;
  fileList.value = [file];
}

async function handleUpload() {
  if (!props.canUpload) {
    return;
  }
  if (!props.rfqId) {
    notification.error(t('rfq.priceComparison.uploadDialog.rfqIdRequired'));
    return;
  }

  if (!selectedFile.value) {
    notification.warning(t('rfq.priceComparison.uploadDialog.pleaseSelectFile'));
    return;
  }

  if (!uploadForm.value.price || uploadForm.value.price <= 0) {
    notification.warning(t('rfq.priceComparison.uploadDialog.pleaseEnterPrice'));
    return;
  }

  uploading.value = true;

  try {
    await uploadPriceComparison(
      props.rfqId,
      props.platform,
      selectedFile.value,
      uploadForm.value.price,
      uploadForm.value.url,
      props.lineItem.id // Pass lineItemId for multi-line RFQs
    );

    notification.success(t('rfq.priceComparison.uploadSuccess'));
    uploadDialogVisible.value = false;
    emit('uploaded');
  } catch (error: any) {
    console.error('Upload failed:', error);
    notification.error(error.message || t('rfq.priceComparison.uploadFailed'));
  } finally {
    uploading.value = false;
  }
}

function viewDetail() {
  emit('viewDetail', {
    ...props.comparisonData,
    lineItemId: props.lineItem.id,
    lineItemNumber: props.lineItem.itemNumber,
    lineItemDescription: props.lineItem.description
  });
}




</script>

<style scoped lang="scss">
.price-comparison-cell {
  padding: 8px 4px;

  .uploaded-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 6px;

    .price-display {
      font-size: 14px;
      font-weight: 600;
      color: #303133;
    }
  }

  .not-uploaded-state {
    display: flex;
    justify-content: center;
  }

  .upload-restricted-state {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    text-align: left;
    padding: 4px 0;

    .lock-icon {
      color: #c0c4cc;
      font-size: 16px;
    }

    .restricted-copy {
      display: flex;
      flex-direction: column;
      font-size: 12px;
      line-height: 1.3;

      .title {
        font-weight: 600;
        color: #606266;
      }

      .hint {
        color: #909399;
      }
    }
  }
}
</style>
