<template>
  <div class="price-comparison-panel">
    <el-card>
      <template #header>
        <div class="card-header">
          <h3>Market Price Comparison</h3>
          <el-button
            v-if="canAddComparison"
            type="primary"
            size="small"
            @click="showAddDialog = true"
          >
            Add Comparison
          </el-button>
        </div>
      </template>

      <div v-if="loading" class="loading-container">
        <el-skeleton :rows="3" animated />
      </div>

      <div v-else-if="comparisons.length === 0" class="empty-state">
        <el-empty description="No price comparisons yet" :image-size="100" />
      </div>

      <el-table v-else :data="comparisons" stripe>
        <el-table-column prop="online_platform" label="Platform" width="140">
          <template #default="{ row }">
            {{ getPlatformLabel(row.online_platform) }}
          </template>
        </el-table-column>

        <el-table-column prop="online_price" label="Online Price" width="130">
          <template #default="{ row }">
            {{ formatCurrency(row.online_price, row.online_currency || quoteCurrency) }}
          </template>
        </el-table-column>

        <el-table-column label="Quote Price" width="130">
          <template #default>
            {{ formatCurrency(quotePrice, quoteCurrency) }}
          </template>
        </el-table-column>

        <el-table-column label="Price Variance" width="140">
          <template #default="{ row }">
            <el-tag :type="getVarianceType(getVariancePercent(row))">
              {{ getVariancePercent(row) > 0 ? "+" : "" }}{{ getVariancePercent(row) }}%
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column label="Screenshot" width="100" align="center">
          <template #default="{ row }">
            <el-image
              v-if="row.screenshot_file"
              :src="getScreenshotUrl(row)"
              :preview-src-list="[getScreenshotUrl(row)]"
              fit="cover"
              style="width: 50px; height: 50px; cursor: pointer"
              class="screenshot-thumbnail"
            />
            <span v-else class="no-screenshot">-</span>
          </template>
        </el-table-column>

        <el-table-column prop="product_url" label="Product Link" min-width="120">
          <template #default="{ row }">
            <a v-if="row.product_url" :href="row.product_url" target="_blank" class="product-link">
              View Product
            </a>
            <span v-else>-</span>
          </template>
        </el-table-column>

        <el-table-column prop="compared_at" label="Compared At" width="160">
          <template #default="{ row }">
            {{ formatDateTime(row.compared_at) }}
          </template>
        </el-table-column>

        <el-table-column
          v-if="showComparedBy"
          prop="compared_by_name"
          label="Compared By"
          width="120"
        />
      </el-table>
    </el-card>

    <!-- Add Comparison Dialog -->
    <el-dialog
      v-model="showAddDialog"
      title="Add Price Comparison"
      width="600px"
      @close="resetForm"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="140px">
        <el-form-item label="Platform" prop="online_platform">
          <el-select v-model="form.online_platform" placeholder="Select platform">
            <el-option
              :label="t('priceComparison.platforms.zhenkunxing')"
              value="zhenkunxing"
            ></el-option>
            <el-option :label="t('priceComparison.platforms.alibaba1688')" value="1688"></el-option>
            <el-option :label="t('priceComparison.platforms.jd')" value="jd"></el-option>
            <el-option :label="t('priceComparison.platforms.taobao')" value="taobao"></el-option>
            <el-option label="Other" value="other"></el-option>
          </el-select>
        </el-form-item>

        <el-form-item label="Online Price" prop="online_price">
          <el-input-number
            v-model="form.online_price"
            :min="0"
            :step="0.01"
            :precision="2"
            style="width: 100%"
          />
        </el-form-item>

        <el-form-item label="Product URL" prop="product_url">
          <el-input v-model="form.product_url" placeholder="https://..." />
        </el-form-item>

        <el-form-item label="Screenshot" prop="screenshot">
          <el-upload
            ref="uploadRef"
            :file-list="fileList"
            :on-change="handleFileChange"
            :on-remove="handleFileRemove"
            :auto-upload="false"
            :limit="1"
            accept="image/*"
            list-type="picture-card"
          >
            <el-icon><Plus /></el-icon>
            <template #tip>
              <div class="el-upload__tip">Upload screenshot (JPG, PNG, max 5MB)</div>
            </template>
          </el-upload>
        </el-form-item>

        <el-form-item label="Notes" prop="comparison_notes">
          <el-input
            v-model="form.comparison_notes"
            type="textarea"
            :rows="3"
            placeholder="Optional notes about this comparison"
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showAddDialog = false">Cancel</el-button>
        <el-button type="primary" :loading="submitting" @click="submitComparison">
          Submit
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, onMounted, computed } from "vue";
import { useI18n } from "vue-i18n";
import { type FormInstance, type FormRules, type UploadUserFile } from "element-plus";
import { Plus } from "@element-plus/icons-vue";
import {
  createPriceComparison,
  fetchPriceComparisons,
} from "@/api/rfq";
import type { QuotePriceComparison } from "@/types";
import { OnlinePlatform } from "@/types";
import { resolveUploadDownloadUrl } from "@/utils/fileDownload";


import { useNotification } from "@/composables";
const notification = useNotification();

const { t } = useI18n();

interface Props {
  rfqId: number;
  quoteId: number;
  quotePrice: number;
  quoteCurrency?: string;
  canAddComparison?: boolean;
  showComparedBy?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  quoteCurrency: "CNY",
  canAddComparison: true,
  showComparedBy: false,
});

const emit = defineEmits<{
  refresh: [];
}>();

const loading = ref(false);
const submitting = ref(false);
const comparisons = ref<QuotePriceComparison[]>([]);
const showAddDialog = ref(false);

const formRef = ref<FormInstance>();
const uploadRef = ref();
const fileList = ref<UploadUserFile[]>([]);

const form = ref({
  online_platform: "",
  online_price: 0,
  product_url: "",
  comparison_notes: "",
  screenshot: null as File | null,
});

const rules: FormRules = {
  online_platform: [{ required: true, message: "Please select platform", trigger: "change" }],
  online_price: [
    { required: true, message: "Please enter online price", trigger: "blur" },
    { type: "number", min: 0, message: "Price must be greater than 0", trigger: "blur" },
  ],
};

async function loadComparisons() {
  loading.value = true;
  try {
    comparisons.value = await fetchPriceComparisons(props.rfqId, props.quoteId);
  } catch (error: any) {
    notification.error(error.message || "Failed to load price comparisons");
  } finally {
    loading.value = false;
  }
}

function getPlatformLabel(platform: string): string {
  const labels: Record<string, string> = {
    [OnlinePlatform.ZHENKUNXING]: "Zhenkunxing",
    [OnlinePlatform.ALIBABA_1688]: "1688",
    [OnlinePlatform.JD]: "JD.com",
    [OnlinePlatform.TAOBAO]: "Taobao",
    [OnlinePlatform.OTHER]: "Other",
  };
  return labels[platform] || platform;
}

function formatCurrency(amount: number, currency: string): string {
  const symbols: Record<string, string> = {
    CNY: "¥",
    USD: "$",
    EUR: "€",
    THB: "฿",
  };
  const symbol = symbols[currency] || currency;
  return `${symbol}${amount.toLocaleString()}`;
}

function formatDateTime(dateStr: string): string {
  try {
    return new Date(dateStr).toLocaleString();
  } catch {
    return dateStr;
  }
}

function getVarianceType(variance: number): "success" | "warning" | "danger" | "" {
  if (variance > 10) return "danger"; // Quote is >10% higher than online
  if (variance < -5) return "success"; // Quote is >5% lower than online
  return "warning"; // Within acceptable range
}

function getVariancePercent(comparison: QuotePriceComparison): number {
  if (typeof comparison.price_variance_percent === "number") {
    return comparison.price_variance_percent;
  }
  if (!comparison.online_price || !props.quotePrice) {
    return 0;
  }
  return Math.round(((props.quotePrice - comparison.online_price) / comparison.online_price) * 100);
}

function getScreenshotUrl(comparison: QuotePriceComparison): string {
  const rawPath = comparison.file_path ?? null;
  const storedName = comparison.screenshot_file ?? null;
  return resolveUploadDownloadUrl(rawPath, storedName, "rfq-attachments") ?? "";
}

function handleFileChange(file: any) {
  // Validate file size (5MB max)
  const maxSize = 5 * 1024 * 1024; // 5MB
  if (file.size > maxSize) {
    notification.warning("Screenshot must be smaller than 5MB");
    fileList.value = [];
    return;
  }

  form.value.screenshot = file.raw;
}

function handleFileRemove() {
  form.value.screenshot = null;
}

async function submitComparison() {
  if (!formRef.value) return;

  try {
    await formRef.value.validate();
  } catch {
    return;
  }

  submitting.value = true;
  try {
    const formData = new FormData();
    formData.append("online_platform", form.value.online_platform);
    formData.append("online_price", String(form.value.online_price));

    if (form.value.product_url) {
      formData.append("product_url", form.value.product_url);
    }

    if (form.value.comparison_notes) {
      formData.append("comparison_notes", form.value.comparison_notes);
    }

    if (form.value.screenshot) {
      formData.append("screenshot", form.value.screenshot);
    }

    await createPriceComparison(props.rfqId, props.quoteId, formData);
    notification.success("Price comparison added successfully");
    showAddDialog.value = false;
    await loadComparisons();
    emit("refresh");
  } catch (error: any) {
    notification.error(error.message || "Failed to add price comparison");
  } finally {
    submitting.value = false;
  }
}

function resetForm() {
  form.value = {
    online_platform: "",
    online_price: 0,
    product_url: "",
    comparison_notes: "",
    screenshot: null,
  };
  fileList.value = [];
  formRef.value?.resetFields();
}

onMounted(() => {
  loadComparisons();
});

defineExpose({
  refresh: loadComparisons,
});




</script>

<style scoped>
.price-comparison-panel {
  margin-top: 1rem;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-header h3 {
  margin: 0;
  font-size: 1.1rem;
  font-weight: 600;
}

.loading-container {
  padding: 1rem;
}

.empty-state {
  padding: 2rem 0;
}

.screenshot-thumbnail {
  border-radius: 4px;
  border: 1px solid #e5e7eb;
}

.no-screenshot {
  color: #9ca3af;
}

.product-link {
  color: #3b82f6;
  text-decoration: none;
}

.product-link:hover {
  text-decoration: underline;
}
</style>
