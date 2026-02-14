<template>
  <div class="supplier-quote-form">
    <el-card class="rfq-info-card" shadow="never">
      <template #header>
        <div class="card-header">
          <h3>{{ t("rfq.quote.rfqInfo") }}</h3>
        </div>
      </template>
      <el-descriptions :column="2" border>
        <el-descriptions-item :label="t('rfq.form.title')">
          {{ rfq?.title }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.rfqType')">
          <el-tag :type="rfq?.rfqType === 'short_term' ? 'success' : 'warning'">
            {{ t(`rfq.rfqType.${rfq?.rfqType}`) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.deliveryPeriod')">
          {{ rfq?.deliveryPeriod }} {{ t("common.days") }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.projectLocation')">
          {{ formatProjectLocation(rfqProjectLocation) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.validUntil')">
          {{ formatDateTime(rfq?.validUntil) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.requiredDocuments')" :span="2">
          {{ requiredDocumentsLabel }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.description')" :span="2">
          {{ rfq?.description || "-" }}
        </el-descriptions-item>
      </el-descriptions>
    </el-card>

    <el-card class="quote-form-card">
      <template #header>
        <div class="card-header">
          <div class="header-title-row">
            <h3>{{ t("rfq.quote.lineItemQuotes") }}</h3>
            <el-tag :type="getProgressTagType()" size="large">
              {{ getProgressText() }}
            </el-tag>
          </div>
          <el-alert
            :title="t('rfq.quote.quoteInstruction')"
            type="info"
            :closable="false"
            show-icon
          />
          <el-progress
            :percentage="getCompletionPercentage()"
            :color="getProgressColor()"
            :stroke-width="8"
            style="margin-top: 12px"
          />
        </div>
      </template>

      <el-form ref="formRef" :model="formData" label-position="top">
        <el-table :data="quoteLineItems" border stripe max-height="600px">
          <el-table-column
            type="index"
            :label="t('rfq.items.lineNumber')"
            width="70"
            align="center"
          />

          <el-table-column :label="t('rfq.lineItems.materialCategory')" width="120">
            <template #default="{ row }">
              {{ getCategoryLabel(row.materialCategory) }}
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.items.itemName')" min-width="150">
            <template #default="{ row }">
              <div>
                <div class="item-name">{{ row.itemName }}</div>
                <div v-if="row.specifications" class="item-spec">{{ row.specifications }}</div>
              </div>
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.items.quantity')" width="120">
            <template #default="{ row }"> {{ row.quantity }} {{ row.unit }} </template>
          </el-table-column>

          <el-table-column :label="t('rfq.quote.moq')" width="150">
            <template #header>
              <div>
                {{ t('rfq.quote.moq') }}
              </div>
            </template>
            <template #default="{ row, $index }">
              <el-form-item
                :prop="`lineItems.${row.rfqLineItemId}.minimumOrderQuantity`"
                :rules="[
                  {
                    validator: (rule: any, value: any, callback: any) => {
                      const hasPrice = formData.lineItems[row.rfqLineItemId]?.unitPrice !== undefined &&
                                       formData.lineItems[row.rfqLineItemId]?.unitPrice !== null;
                      if (hasPrice && (!value || value <= 0)) {
                        callback(new Error(t('rfq.quote.moqRequiredWhenPriced')));
                      } else {
                        callback();
                      }
                    },
                    trigger: 'change'
                  }
                ]"
              >
                <el-input-number
                  v-model="formData.lineItems[row.rfqLineItemId].minimumOrderQuantity"
                  :min="1"
                  :step="1"
                  :precision="0"
                  size="small"
                  controls-position="right"
                  style="width: 100%"
                  :placeholder="t('rfq.quote.moqPlaceholder')"
                  @change="handleMoqChange(row.rfqLineItemId, $index)"
                />
              </el-form-item>
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.quote.spq')" width="150">
            <template #header>
              <div>
                {{ t('rfq.quote.spq') }}
              </div>
            </template>
            <template #default="{ row, $index }">
              <el-form-item
                :prop="`lineItems.${row.rfqLineItemId}.standardPackageQuantity`"
                :rules="[
                  {
                    validator: (rule: any, value: any, callback: any) => {
                      const hasPrice = formData.lineItems[row.rfqLineItemId]?.unitPrice !== undefined &&
                                       formData.lineItems[row.rfqLineItemId]?.unitPrice !== null;
                      if (hasPrice && (!value || value <= 0)) {
                        callback(new Error(t('rfq.quote.spqRequiredWhenPriced')));
                      } else {
                        callback();
                      }
                    },
                    trigger: 'change'
                  }
                ]"
              >
                <el-input-number
                  v-model="formData.lineItems[row.rfqLineItemId].standardPackageQuantity"
                  :min="1"
                  :step="1"
                  :precision="0"
                  size="small"
                  controls-position="right"
                  style="width: 100%"
                  :placeholder="t('rfq.quote.spqPlaceholder')"
                  @change="handleSpqChange(row.rfqLineItemId, $index)"
                />
              </el-form-item>
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.quote.unitPrice')" width="150">
            <template #header>
              <div class="header-with-help">
                {{ t('rfq.quote.unitPrice') }}
                <el-tooltip :content="t('rfq.quote.unitPriceHelpTip')" placement="top">
                  <el-icon class="help-icon"><QuestionFilled /></el-icon>
                </el-tooltip>
              </div>
            </template>
            <template #default="{ row, $index }">
              <el-form-item
                :prop="`lineItems.${row.rfqLineItemId}.unitPrice`"
                :rules="[
                  { type: 'number', min: 0, message: t('rfq.quote.unitPriceMinZero'), trigger: 'blur' }
                ]"
              >
                <el-input-number
                  v-model="formData.lineItems[row.rfqLineItemId].unitPrice"
                  :min="0"
                  :step="0.01"
                  :step-strictly="false"
                  :precision="2"
                  size="small"
                  controls-position="right"
                  style="width: 100%"
                  :placeholder="t('rfq.quote.unitPricePlaceholder')"
                  :class="{ 'has-value': formData.lineItems[row.rfqLineItemId]?.unitPrice !== undefined && formData.lineItems[row.rfqLineItemId]?.unitPrice !== null }"
                  @change="handleUnitPriceChange(row.rfqLineItemId, $index)"
                />
              </el-form-item>
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.quote.totalPrice')" width="140">
            <template #default="{ row }">
              <span class="total-price">{{ formatPrice(row.totalPrice, row.currency) }}</span>
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.quote.standardUnitCost')" min-width="220">
            <template #default="{ row }">
              <div class="tariff-cell">
                <template v-if="lineTariffs[row.rfqLineItemId]">
                  <div class="tariff-cell__value">
                    {{ formatCurrencyValue(
                      lineTariffs[row.rfqLineItemId]?.standardCost ?? null,
                      lineTariffs[row.rfqLineItemId]?.standardCostCurrency || "USD"
                    ) }}
                    <el-tag
                      v-if="lineTariffs[row.rfqLineItemId]?.hasSpecialTariff"
                      type="warning"
                      size="small"
                      effect="dark"
                    >
                      {{ t("rfq.quote.specialTariffTag") }}
                    </el-tag>
                  </div>
                  <div class="tariff-cell__meta">
                    <span>
                      {{ t("rfq.quote.tariffRateLabel", {
                        rate: formatPercentage(lineTariffs[row.rfqLineItemId]?.freightRate ?? null),
                      }) }}
                    </span>
                    <span v-if="lineTariffs[row.rfqLineItemId]?.specialTariffRate">
                      {{ t("rfq.quote.specialTariffRateLabel", {
                        rate: formatPercentage(lineTariffs[row.rfqLineItemId]?.specialTariffRate ?? null),
                      }) }}
                    </span>
                  </div>
                  <div class="tariff-cell__total">
                    {{ t("rfq.quote.standardLineTotal", {
                      value: formatCurrencyValue(
                        getStandardLineTotal(row.rfqLineItemId),
                        lineTariffs[row.rfqLineItemId]?.standardCostCurrency || "USD"
                      ),
                    }) }}
                  </div>
                </template>
                <div v-else class="tariff-cell__empty">
                  <el-icon v-if="lineTariffLoading[row.rfqLineItemId]" class="tariff-spinner">
                    <Loading />
                  </el-icon>
                  <span>{{ t("rfq.quote.tariffPending") }}</span>
                </div>
              </div>
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.lineItems.brand')" width="140">
            <template #default="{ row }">
              <el-input
                v-model="row.brand"
                size="small"
                :placeholder="t('rfq.lineItems.brandPlaceholder')"
              />
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.quote.productOriginLabel')" width="160">
            <template #header>
              <div>
                {{ t('rfq.quote.productOriginLabel') }}
              </div>
            </template>
            <template #default="{ row, $index }">
              <el-form-item
                :prop="`lineItems.${row.rfqLineItemId}.productOrigin`"
                :rules="[
                  {
                    validator: (rule: any, value: any, callback: any) => {
                      const hasPrice = formData.lineItems[row.rfqLineItemId]?.unitPrice !== undefined &&
                                       formData.lineItems[row.rfqLineItemId]?.unitPrice !== null;
                      if (hasPrice && (!value || value.trim() === '')) {
                        callback(new Error(t('rfq.quote.productOriginRequiredWhenPriced')));
                      } else {
                        callback();
                      }
                    },
                    trigger: 'change'
                  }
                ]"
              >
                <el-select
                  v-model="formData.lineItems[row.rfqLineItemId].productOrigin"
                  size="small"
                  filterable
                  clearable
                  style="width: 100%"
                  :placeholder="t('rfq.quote.productOriginPlaceholder')"
                  @change="handleProductOriginChange(row.rfqLineItemId, $index)"
                >
                  <el-option
                    v-for="option in productOriginOptions"
                    :key="option"
                    :label="option"
                    :value="option"
                  />
                </el-select>
              </el-form-item>
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.quote.deliveryPeriod')" width="140">
            <template #default="{ row, $index }">
              <el-input-number
                v-model="formData.lineItems[row.rfqLineItemId].deliveryPeriod"
                :min="1"
                :step="1"
                :precision="0"
                size="small"
                controls-position="right"
                style="width: 100%"
                :placeholder="t('rfq.quote.deliveryPeriodPlaceholder')"
                @change="handleDeliveryPeriodChange(row.rfqLineItemId, $index)"
              />
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.lineItems.notes')" min-width="220">
            <template #header>
              <div class="notes-header">
                <span>{{ t('rfq.lineItems.notes') }}</span>
                <el-tooltip :content="t('rfq.quote.notesHelpText')" placement="top">
                  <el-icon class="help-icon"><QuestionFilled /></el-icon>
                </el-tooltip>
              </div>
            </template>
            <template #default="{ row }">
              <div class="notes-field-wrapper">
                <el-input
                  v-model="row.notes"
                  type="textarea"
                  :rows="2"
                  size="small"
                  :placeholder="getNotesPlaceholder(row)"
                  :class="{
                    'notes-required': isNotesRequired(row),
                    'notes-filled': hasNotes(row)
                  }"
                />
                <el-button
                  v-if="!hasPrice(row) && !hasNotes(row)"
                  size="small"
                  type="warning"
                  plain
                  class="no-quote-btn"
                  @click="fillNoQuoteTemplate(row)"
                >
                  {{ t('rfq.quote.cannotQuote') }}
                </el-button>
              </div>
            </template>
          </el-table-column>
        </el-table>

        <div class="quote-summary">
          <div class="summary-item">
            <span class="label">{{ t("rfq.quote.totalItems") }}:</span>
            <span class="value">{{ quoteLineItems.length }}</span>
          </div>
          <div class="summary-item total">
            <span class="label">{{ t("rfq.quote.grandTotal") }}:</span>
            <span class="value">{{ formatPrice(totalAmount, formData.currency) }}</span>
          </div>
          <div
            v-if="standardCostSummary.hasData && standardCostSummary.usdTotal !== null"
            class="summary-item"
          >
            <span class="label">{{ t("rfq.quote.standardCostTotalUsd") }}:</span>
            <span class="value">{{ formatCurrencyValue(standardCostSummary.usdTotal, "USD") }}</span>
          </div>
          <div
            v-for="(amount, currency) in standardCostSummary.localTotals"
            :key="`standard-${currency}`"
            class="summary-item"
          >
            <span class="label">
              {{ t("rfq.quote.standardCostTotalWithCurrency", { currency }) }}:
            </span>
            <span class="value">{{ formatCurrencyValue(amount, String(currency)) }}</span>
          </div>
        </div>

        <el-alert
          v-if="standardCostSummary.hasData && standardCostSummary.hasSpecialTariff"
          type="warning"
          :closable="false"
          :title="t('rfq.quote.specialTariffNotice')"
          show-icon
          style="margin-top: 12px"
        />

        <el-form-item :label="t('rfq.quote.currency')" class="currency-field" required>
          <div style="display: flex; gap: 16px; align-items: flex-start;">
            <div style="flex: 0 0 220px;">
              <el-select
                v-model="formData.currency"
                style="width: 100%"
                :placeholder="t('rfq.quote.selectCurrency')"
              >
                <el-option
                  v-for="option in currencyOptions"
                  :key="option.value"
                  :label="option.label"
                  :value="option.value"
                />
              </el-select>
            </div>
            <div v-if="formData.currency === 'CNY'" style="flex: 0 0 180px;">
              <el-select
                v-model="formData.taxStatus"
                style="width: 100%"
                size="default"
              >
                <el-option value="inclusive" :label="t('rfq.quote.taxInclusive')" />
                <el-option value="exclusive" :label="t('rfq.quote.taxExclusive')" />
              </el-select>
            </div>
          </div>
          <p class="form-item-hint">{{ t('rfq.quote.currencyHint') }}</p>
        </el-form-item>

        <el-divider />

        <h4 style="margin: 24px 0 16px 0">{{ t("rfq.quote.additionalInfo") }}</h4>

        <el-form-item :label="t('rfq.quote.deliveryTerms')">
          <el-select
            v-model="formData.deliveryTerms"
            style="width: 100%"
            :placeholder="t('rfq.quote.deliveryTermsPlaceholder')"
            clearable
          >
            <el-option
              v-for="option in incotermOptions"
              :key="option.value"
              :label="option.label"
              :value="option.value"
            />
          </el-select>
        </el-form-item>

        <el-form-item
          :label="t('rfq.quote.shippingCountry')"
          prop="shippingCountry"
          :rules="[{ required: true, message: t('rfq.quote.shippingCountryRequired'), trigger: 'change' }]"
        >
          <el-select
            v-model="formData.shippingCountry"
            filterable
            clearable
            style="width: 100%"
            :placeholder="t('rfq.quote.shippingCountryPlaceholder')"
          >
            <el-option
              v-for="option in shippingCountryOptions"
              :key="option.code"
              :label="formatCountryLabel(option)"
              :value="option.code"
            />
          </el-select>
        </el-form-item>

        <el-form-item :label="t('rfq.quote.notes')">
          <el-input
            v-model="formData.notes"
            type="textarea"
            :rows="4"
            :placeholder="t('rfq.quote.notesPlaceholder')"
            maxlength="1000"
            show-word-limit
          />
        </el-form-item>

        <el-form-item :label="t('rfq.quote.attachments')">
          <el-upload
            ref="uploadRef"
            :file-list="fileList"
            :on-change="handleFileChange"
            :on-remove="handleFileRemove"
            :auto-upload="false"
            :limit="10"
            :accept="'.pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png'"
            multiple
          >
            <template #trigger>
              <el-button type="primary" plain>
                {{ t("rfq.quote.selectFiles") }}
              </el-button>
            </template>
            <template #tip>
              <div class="el-upload__tip">
                {{ t("rfq.quote.attachmentHint") }}
              </div>
            </template>
          </el-upload>
        </el-form-item>

        <div class="form-actions">
          <el-button @click="handleCancel">{{ t("common.cancel") }}</el-button>
          <el-button type="primary" :loading="submitting" @click="handleSubmit">
            {{ existingQuote ? t("rfq.quote.updateQuote") : t("rfq.quote.submitQuote") }}
          </el-button>
        </div>

        <transition name="el-fade-in">
          <el-alert
            v-if="submissionFeedback"
            class="submit-feedback"
            :type="submissionFeedback.type"
            :title="submissionFeedback.message"
            show-icon
            closable
            @close="submissionFeedback = null"
          />
        </transition>
      </el-form>
    </el-card>
  </div>
</template>

<script setup lang="ts">



import { ref, reactive, computed, onMounted, watch, nextTick } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";

import type { FormInstance, UploadFile, UploadInstance, UploadUserFile } from "element-plus";
import { Loading, QuestionFilled } from "@element-plus/icons-vue";
import {
  fetchTariffOptions,
  calculateTariff,
  type TariffCalculationResult,
  type TariffCountryOption,
} from "@/api/tariffs";
import { useNotification } from "@/composables";
import { toNumber, firstDefinedValue, pickFirstNumber } from "@/utils/dataParsing";

const notification = useNotification();

interface RfqLineItem {
  id: number;
  rfqLineItemId: number;
  materialCategory: string;
  brand?: string;
  itemName: string;
  specifications?: string;
  quantity: number;
  unit: string;
  estimatedUnitPrice?: number;
  currency: string;
}

interface QuoteLineItem extends RfqLineItem {
  unitPrice?: number;
  minimumOrderQuantity?: number;
  standardPackageQuantity?: number;
  totalPrice: number;
  deliveryPeriod?: number;
  notes?: string;
  productOrigin?: string;
  productGroup?: string;
  standardUnitCostLocal?: number | null;
  standardUnitCostUsd?: number | null;
  tariffCalculation?: TariffCalculationResult | null;
}

const props = defineProps<{
  rfq: any;
  existingQuote?: any;
}>();

const emit = defineEmits<{
  (e: "submitted"): void;
  (e: "cancel"): void;
}>();

const { t } = useI18n();
const router = useRouter();
const formRef = ref<FormInstance>();
const uploadRef = ref<UploadInstance>();
const submitting = ref(false);
const submissionFeedback = ref<{ type: "success" | "error"; message: string } | null>(null);

const existingQuote = computed(() => props.existingQuote);
const quoteLineItems = ref<QuoteLineItem[]>([]);
const fileList = ref<UploadUserFile[]>([]);
const lineTariffs = reactive<Record<number, TariffCalculationResult | null>>({});
const lineTariffLoading = reactive<Record<number, boolean>>({});
const latestTariffRequest = new Map<number, number>();

const rfqProjectLocation = computed(() =>
  firstDefinedValue(props.rfq?.projectLocation, props.rfq?.project_location, "")
);

const rfqRequiredDocuments = computed(() => {
  const docs = firstDefinedValue(props.rfq?.requiredDocuments, props.rfq?.required_documents, []);
  if (!Array.isArray(docs)) return [];
  return docs
    .map((doc) => String(doc ?? "").trim())
    .filter((doc) => doc.length > 0);
});

const requiredDocumentKeyMap: Record<string, string> = {
  technicalspec: "rfq.documents.technicalSpec",
  businesslicense: "rfq.documents.businessLicense",
  qualitycert: "rfq.documents.qualityCert",
  taxcert: "rfq.documents.taxCert",
  bankinfo: "rfq.documents.bankInfo",
  references: "rfq.documents.references",
};

const requiredDocumentsLabel = computed(() => {
  if (!rfqRequiredDocuments.value.length) return "-";
  return rfqRequiredDocuments.value
    .map((doc) => formatRequiredDocumentLabel(doc))
    .join(", ");
});

const hasRequiredDocuments = computed(() => rfqRequiredDocuments.value.length > 0);

const currencyOptions = computed(() => [
  { value: "CNY", label: t("rfq.quote.currencyOptions.CNY") },
  { value: "USD", label: t("rfq.quote.currencyOptions.USD") },
  { value: "EUR", label: t("rfq.quote.currencyOptions.EUR") },
  { value: "JPY", label: t("rfq.quote.currencyOptions.JPY") },
  { value: "THB", label: t("rfq.quote.currencyOptions.THB") },
]);

const productOriginOptions = [
  "Australia",
  "Bulgaria",
  "CHINA",
  "AMERICA",
  "Germany",
  "Spain",
  "France",
  "Indonesia",
  "Italy",
  "Japan",
  "Korea",
  "Morocco",
  "Holland",
  "Poland",
  "Singapore",
  "Slovenia",
  "TAIWAN",
  "England",
  "MEXICO",
  "THILAND",
  "vietnam",
];

const incotermOptions = computed(() => [
  { value: "EXW", label: t("supplierRegistration.incoterms.EXW") },
  { value: "FOB", label: t("supplierRegistration.incoterms.FOB") },
  { value: "CIF", label: t("supplierRegistration.incoterms.CIF") },
  { value: "CFR", label: t("supplierRegistration.incoterms.CFR") },
  { value: "DDP", label: t("supplierRegistration.incoterms.DDP") },
  { value: "DDU", label: t("supplierRegistration.incoterms.DDU") },
  { value: "DAP", label: t("supplierRegistration.incoterms.DAP") },
  { value: "DAT", label: t("supplierRegistration.incoterms.DAT") },
  { value: "FCA", label: t("supplierRegistration.incoterms.FCA") },
  { value: "CPT", label: t("supplierRegistration.incoterms.CPT") },
  { value: "CIP", label: t("supplierRegistration.incoterms.CIP") },
]);

const shippingCountryOptions = ref<TariffCountryOption[]>([]);

const formData = reactive({
  notes: "",
  currency: "CNY",
  taxStatus: "inclusive",
  deliveryTerms: "",
  shippingCountry: "",
  lineItems: {} as Record<number, {
    unitPrice?: number;
    minimumOrderQuantity?: number;
    standardPackageQuantity?: number;
    productOrigin?: string;
    productGroup?: string;
    deliveryPeriod?: number;
  }>,
});

watch(
  () => formData.shippingCountry,
  () => {
    recalcAllLineTariffs(true);
  }
);

watch(
  () => formData.currency,
  () => {
    recalcAllLineTariffs(true);
  }
);

function formatCountryLabel(option: TariffCountryOption): string {
  if (!option) return "";
  const parts: string[] = [];
  if (option.name) parts.push(option.name);
  if (option.nameZh && option.nameZh !== option.name) parts.push(option.nameZh);
  const label = parts.length > 0 ? parts.join(" / ") : option.code;
  return option.code ? `${label} (${option.code})` : label;
}

function formatProjectLocation(value?: string | null): string {
  if (!value) return "-";
  const key = `rfq.projectLocation.${value}`;
  const translated = t(key);
  return translated !== key ? translated : String(value);
}

function formatRequiredDocumentLabel(value: string): string {
  const normalized = value.toLowerCase().replace(/[^a-z0-9]/g, "");
  const key = requiredDocumentKeyMap[normalized];
  if (!key) return value;
  const translated = t(key);
  return translated !== key ? translated : value;
}

async function loadTariffOptions() {
  try {
    const { countries } = await fetchTariffOptions();
    if (Array.isArray(countries) && countries.length) {
      shippingCountryOptions.value = countries;
    }
  } catch (error) {
    console.error("[Tariff] Failed to load tariff options:", error);
  }
}

onMounted(() => {
  initializeQuoteLineItems();
  void loadTariffOptions();
});

function initializeQuoteLineItems() {
  if (props.rfq?.items && props.rfq.items.length > 0) {
    const existingQuoteItems = props.existingQuote?.quoteItems || [];

    quoteLineItems.value = props.rfq.items.map((rawItem: any) => {
      const normalizedItem: RfqLineItem = {
        ...rawItem,
        materialCategory: firstDefinedValue(
          rawItem.materialCategory,
          rawItem.materialType,
          rawItem.distributionCategory,
          rawItem.distribution_category,
          ''
        ),
        itemName: firstDefinedValue(
          rawItem.itemName,
          rawItem.item_name,
          rawItem.description,
          rawItem.remarks,
          ''
        ),
        specifications: firstDefinedValue(
          rawItem.specifications,
          rawItem.parameters,
          rawItem.remarks,
          ''
        ),
        currency: rawItem.currency ?? props.rfq?.currency ?? "CNY",
      };

      const existingItem = existingQuoteItems.find((qi: any) => qi.rfqLineItemId === normalizedItem.id);

      const unitPrice = firstDefinedValue(existingItem?.unitPrice, existingItem?.unit_price, undefined);
      const quantity = Number(normalizedItem.quantity ?? 0);
      const totalPrice = Number.isFinite(quantity) && unitPrice
        ? unitPrice * quantity
        : pickFirstNumber(existingItem?.totalPrice, existingItem?.total_price, 0);

      formData.lineItems[normalizedItem.id] = {
        unitPrice: unitPrice,
        minimumOrderQuantity: firstDefinedValue(
          existingItem?.minimumOrderQuantity,
          existingItem?.moq,
          normalizedItem.quantity,
          1
        ),
        standardPackageQuantity: firstDefinedValue(
          existingItem?.standardPackageQuantity,
          undefined
        ),
        deliveryPeriod: firstDefinedValue(existingItem?.deliveryPeriod, undefined),
        productOrigin: firstDefinedValue(existingItem?.productOrigin, existingItem?.product_origin, ''),
        productGroup: "OTHERS",
      };

      lineTariffs[normalizedItem.id] = firstDefinedValue(
        existingItem?.tariffCalculation,
        existingItem?.tariff_calculation,
        null
      );
      lineTariffLoading[normalizedItem.id] = false;

      return {
        ...normalizedItem,
        rfqLineItemId: normalizedItem.id,
        unitPrice: unitPrice,
        minimumOrderQuantity: formData.lineItems[normalizedItem.id].minimumOrderQuantity,
        standardPackageQuantity: formData.lineItems[normalizedItem.id].standardPackageQuantity,
        totalPrice: totalPrice,
        deliveryPeriod: formData.lineItems[normalizedItem.id].deliveryPeriod,
        notes: firstDefinedValue(existingItem?.notes, ''),
        brand: firstDefinedValue(existingItem?.brand, normalizedItem.brand, ''),
        productOrigin: formData.lineItems[normalizedItem.id].productOrigin,
        productGroup: "OTHERS",
        standardUnitCostLocal: firstDefinedValue(
          existingItem?.standardLineCostLocal,
          existingItem?.standardUnitCostLocal,
          null
        ),
        standardUnitCostUsd: firstDefinedValue(
          existingItem?.standardLineCostUsd,
          existingItem?.standardUnitCostUsd,
          null
        ),
        tariffCalculation: lineTariffs[normalizedItem.id],
      };
    });

    if (props.existingQuote) {
      formData.notes = firstDefinedValue(props.existingQuote.notes, '');
      formData.deliveryTerms = firstDefinedValue(props.existingQuote.deliveryTerms, '');
      formData.shippingCountry = firstDefinedValue(
        props.existingQuote.shippingCountry,
        props.existingQuote.shipping_country,
        ''
      );
      formData.taxStatus = firstDefinedValue(props.existingQuote.taxStatus, "inclusive");
    }
    formData.currency = firstDefinedValue(
      props.existingQuote?.currency,
      props.rfq?.currency,
      formData.currency
    );

    quoteLineItems.value.forEach((line) => {
      if (lineTariffs[line.rfqLineItemId] === undefined) {
        lineTariffs[line.rfqLineItemId] = null;
      }
    });

    nextTick(() => {
      recalcAllLineTariffs(false);
    });
  }
}

function handleUnitPriceChange(rfqLineItemId: number, rowIndex: number) {
  const unitPrice = formData.lineItems[rfqLineItemId]?.unitPrice;
  const row = quoteLineItems.value[rowIndex];

  if (row) {
    // Sync to row data
    row.unitPrice = unitPrice;

    // Calculate total
    if (unitPrice && row.quantity) {
      row.totalPrice = unitPrice * row.quantity;
    } else {
      row.totalPrice = 0;
    }
  }

  void recalcLineTariff(rfqLineItemId);
}

function handleMoqChange(rfqLineItemId: number, rowIndex: number) {
  const moq = formData.lineItems[rfqLineItemId]?.minimumOrderQuantity;
  const row = quoteLineItems.value[rowIndex];

  if (row) {
    row.minimumOrderQuantity = moq;
  }
}

function handleSpqChange(rfqLineItemId: number, rowIndex: number) {
  const spq = formData.lineItems[rfqLineItemId]?.standardPackageQuantity;
  const row = quoteLineItems.value[rowIndex];

  if (row) {
    row.standardPackageQuantity = spq;
  }
}

function handleProductOriginChange(rfqLineItemId: number, rowIndex: number) {
  const productOrigin = formData.lineItems[rfqLineItemId]?.productOrigin;
  const row = quoteLineItems.value[rowIndex];

  if (row) {
    row.productOrigin = productOrigin;
  }

  void recalcLineTariff(rfqLineItemId);
}

function handleDeliveryPeriodChange(rfqLineItemId: number, rowIndex: number) {
  const deliveryPeriod = formData.lineItems[rfqLineItemId]?.deliveryPeriod;
  const row = quoteLineItems.value[rowIndex];

  if (row) {
    row.deliveryPeriod = deliveryPeriod;
  }
}

function findLineItem(rfqLineItemId: number): QuoteLineItem | undefined {
  return quoteLineItems.value.find((item) => item.rfqLineItemId === rfqLineItemId);
}

function resetTariffForLine(rfqLineItemId: number) {
  lineTariffs[rfqLineItemId] = null;
  lineTariffLoading[rfqLineItemId] = false;
  const row = findLineItem(rfqLineItemId);
  if (row) {
    row.standardUnitCostLocal = null;
    row.standardUnitCostUsd = null;
    row.tariffCalculation = null;
  }
}

function getLineQuantity(rfqLineItemId: number): number {
  const row = findLineItem(rfqLineItemId);
  if (!row) return 0;
  const quantity = Number(row.quantity ?? 0);
  return Number.isFinite(quantity) ? quantity : 0;
}

function recalcAllLineTariffs(force = false) {
  quoteLineItems.value.forEach((item) => {
    if (force || !lineTariffs[item.rfqLineItemId]) {
      void recalcLineTariff(item.rfqLineItemId);
    }
  });
}

async function recalcLineTariff(rfqLineItemId: number) {
  const lineEntry = formData.lineItems[rfqLineItemId];
  const shippingCountry = formData.shippingCountry;

  if (
    !lineEntry ||
    !shippingCountry ||
    !lineEntry.productGroup ||
    !lineEntry.productOrigin ||
    !lineEntry.unitPrice ||
    lineEntry.unitPrice <= 0
  ) {
    resetTariffForLine(rfqLineItemId);
    return;
  }

  const payload = {
    originalPrice: lineEntry.unitPrice,
    shippingCountry,
    productGroup: lineEntry.productGroup,
    productOrigin: lineEntry.productOrigin,
    projectLocation: props.rfq?.projectLocation || 'HZ',
    deliveryTerms: formData.deliveryTerms || '',
    currency: formData.currency,
  };

  const requestId = Date.now();
  latestTariffRequest.set(rfqLineItemId, requestId);
  lineTariffLoading[rfqLineItemId] = true;

  try {
    const result = await calculateTariff(payload);
    if (latestTariffRequest.get(rfqLineItemId) !== requestId) {
      return;
    }

    lineTariffs[rfqLineItemId] = result;
    const row = findLineItem(rfqLineItemId);
    if (row) {
      row.standardUnitCostLocal = result.standardCostLocal ?? null;
      row.standardUnitCostUsd = result.standardCostUsd ?? null;
      row.tariffCalculation = result;
    }
  } catch (error) {
    console.error(`[Tariff] Failed to calculate standard cost for line ${rfqLineItemId}:`, error);
  } finally {
    if (latestTariffRequest.get(rfqLineItemId) === requestId) {
      lineTariffLoading[rfqLineItemId] = false;
    }
  }
}

function getTariffResult(rfqLineItemId: number): TariffCalculationResult | null {
  return lineTariffs[rfqLineItemId] || null;
}

function getStandardLineTotal(rfqLineItemId: number): number | null {
  const tariff = getTariffResult(rfqLineItemId);
  if (!tariff) {
    return null;
  }
  const quantity = getLineQuantity(rfqLineItemId);
  if (!quantity) {
    return null;
  }
  const unitCost = Number(tariff.standardCost ?? 0);
  if (!Number.isFinite(unitCost)) {
    return null;
  }
  return Number((unitCost * quantity).toFixed(2));
}

function calculateLineTotal(row: QuoteLineItem) {
  if (row.unitPrice && row.quantity) {
    row.totalPrice = row.unitPrice * row.quantity;
  } else {
    row.totalPrice = 0;
  }
}

const totalAmount = computed(() => {
  return quoteLineItems.value.reduce((sum, item) => sum + (item.totalPrice || 0), 0);
});

const standardCostSummary = computed(() => {
  const localTotals: Record<string, number> = {};
  let usdTotal = 0;
  let usdCount = 0;
  let hasData = false;
  let hasSpecialTariff = false;

  quoteLineItems.value.forEach((item) => {
    const tariff = getTariffResult(item.rfqLineItemId);
    if (!tariff) {
      return;
    }

    const quantity = getLineQuantity(item.rfqLineItemId);
    if (!quantity) {
      return;
    }

    const unitCost = Number(tariff.standardCost ?? 0);
    if (!Number.isFinite(unitCost)) {
      return;
    }

    const lineTotal = unitCost * quantity;
    const currency = tariff.standardCostCurrency || formData.currency || "CNY";

    hasData = true;
    if (currency === "USD") {
      usdTotal += lineTotal;
      usdCount += 1;
    } else {
      localTotals[currency] = (localTotals[currency] || 0) + lineTotal;
    }

    if (tariff.hasSpecialTariff) {
      hasSpecialTariff = true;
    }
  });

  return {
    hasData,
    usdTotal: usdCount > 0 ? Number(usdTotal.toFixed(2)) : null,
    localTotals: Object.fromEntries(
      Object.entries(localTotals).map(([currency, amount]) => [currency, Number(amount.toFixed(2))])
    ),
    hasSpecialTariff,
  };
});

function formatCurrencyValue(amount?: number | null, currency = "USD"): string {
  if (amount === undefined || amount === null || Number.isNaN(amount)) {
    return "-";
  }
  return `${amount.toFixed(2)} ${currency}`;
}

function formatPercentage(rate?: number | null): string {
  if (rate === undefined || rate === null || Number.isNaN(rate)) {
    return "-";
  }
  return `${(rate * 100).toFixed(2)}%`;
}

function getCategoryLabel(category?: string | null): string {
  if (!category) return "-";
  if (category === "equipment") return t("rfq.distributionCategory.equipment");
  if (category === "consumables") return t("rfq.lineItems.consumables");
  if (category === "hardware") return t("rfq.distributionCategory.hardware");
  if (category === "other") return t("common.other");
  return category;
}

function formatDateTime(dateString?: string): string {
  if (!dateString) return "-";
  try {
    return new Date(dateString).toLocaleString();
  } catch {
    return dateString;
  }
}

function formatPrice(amount?: number, currency = "CNY"): string {
  if (amount === undefined || amount === null) return "-";
  return `${amount.toFixed(2)} ${currency}`;
}

function handleFileChange(file: UploadFile, uploadFiles: UploadFile[]) {
  fileList.value = uploadFiles;
}

function handleFileRemove(file: UploadFile, uploadFiles: UploadFile[]) {
  fileList.value = uploadFiles;
}

function hasAnyAttachments(): boolean {
  if (fileList.value.length > 0) return true;
  const attachments = (existingQuote.value as { attachments?: unknown[] } | undefined)?.attachments;
  return Array.isArray(attachments) && attachments.length > 0;
}

async function handleSubmit() {
  if (!formRef.value) {
    return;
  }

  try {
    await formRef.value.validate();
  } catch {
    return;
  }

  // 验证每个行项目：要么有价格，要么有备注
  const invalidItems = quoteLineItems.value.filter((item) => {
    const entry = formData.lineItems[item.rfqLineItemId];
    if (!entry) return true;

    const hasPrice = entry.unitPrice !== undefined && entry.unitPrice !== null;
    const hasNotes = String(firstDefinedValue(item.notes, "") ?? "").trim().length > 0;

    return !hasPrice && !hasNotes;
  });

  if (invalidItems.length > 0) {
    const itemNames = invalidItems.map(i => `#${i.rfqLineItemId}: ${i.itemName}`).join(', ');
    notification.warning(
      t("rfq.quote.priceOrNotesRequired", {
        count: invalidItems.length,
        items: itemNames
      })
    );
    return;
  }

  // 验证有价格行的必填字段
  const pricedItems = quoteLineItems.value.filter((item) => {
    const entry = formData.lineItems[item.rfqLineItemId];
    return entry && entry.unitPrice !== undefined && entry.unitPrice !== null;
  });

  const invalidMoq = pricedItems.filter((item) => {
    const entry = formData.lineItems[item.rfqLineItemId];
    return !entry?.minimumOrderQuantity || entry.minimumOrderQuantity <= 0;
  });
  if (invalidMoq.length > 0) {
    notification.warning(t("rfq.quote.moqRequiredWhenPriced"));
    return;
  }

  const invalidSpq = pricedItems.filter((item) => {
    const entry = formData.lineItems[item.rfqLineItemId];
    return !entry?.standardPackageQuantity || entry.standardPackageQuantity <= 0;
  });
  if (invalidSpq.length > 0) {
    notification.warning(t("rfq.quote.spqRequiredWhenPriced"));
    return;
  }

  const missingOrigin = pricedItems.filter((item) => {
    const entry = formData.lineItems[item.rfqLineItemId];
    return !entry?.productOrigin || entry.productOrigin.trim() === "";
  });
  if (missingOrigin.length > 0) {
    notification.warning(t("rfq.quote.productOriginRequiredWhenPriced"));
    return;
  }

  if (hasRequiredDocuments.value && !hasAnyAttachments()) {
    notification.warning(t("rfq.quote.requiredDocumentsUploadRequired"));
    return;
  }

  if (!formData.shippingCountry) {
    notification.warning(t("rfq.quote.shippingCountryRequired"));
    return;
  }

  const missingProductGroup = pricedItems.filter((item) => {
    const entry = formData.lineItems[item.rfqLineItemId];
    return !entry?.productGroup || entry.productGroup.trim() === "";
  });
  if (missingProductGroup.length > 0) {
    notification.warning(t("rfq.quote.productGroupRequiredWhenPriced"));
    return;
  }

  submissionFeedback.value = null;
  submitting.value = true;

  try {
    const formDataPayload = new FormData();
    const quoteData = {
      currency: formData.currency,
      taxStatus: formData.taxStatus,
      lineItems: quoteLineItems.value.map((item) => ({
        rfqLineItemId: item.rfqLineItemId,
        unitPrice: formData.lineItems[item.rfqLineItemId]?.unitPrice,
        minimumOrderQuantity: formData.lineItems[item.rfqLineItemId]?.minimumOrderQuantity,
        standardPackageQuantity: formData.lineItems[item.rfqLineItemId]?.standardPackageQuantity,
        brand: item.brand,
        deliveryPeriod: formData.lineItems[item.rfqLineItemId]?.deliveryPeriod,
        notes: item.notes,
        productOrigin: formData.lineItems[item.rfqLineItemId]?.productOrigin,
        productGroup: formData.lineItems[item.rfqLineItemId]?.productGroup,
        taxStatus: formData.taxStatus, // Inherit taxStatus from quote level for each line item
      })),
      deliveryTerms: formData.deliveryTerms,
      shippingCountry: formData.shippingCountry,
      notes: formData.notes,
    };
    formDataPayload.append("quoteData", JSON.stringify(quoteData));

    fileList.value.forEach((file) => {
      if (file.raw) {
        formDataPayload.append("attachments", file.raw);
      }
    });

    const isUpdate = !!props.existingQuote;
    const url = isUpdate
      ? `/api/rfq-workflow/${props.rfq.id}/quotes/${props.existingQuote.id}`
      : `/api/rfq-workflow/${props.rfq.id}/quotes`;

    const token = localStorage.getItem("token");
    const headers: HeadersInit = {};
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
    const response = await fetch(url, {
      method: isUpdate ? "PUT" : "POST",
      headers,
      body: formDataPayload,
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || "Failed to submit quote");
    }

    const successKey = isUpdate ? "rfq.quote.updateSuccess" : "rfq.quote.submitSuccess";
    submissionFeedback.value = {
      type: "success",
      message: String(t(successKey)),
    };
    emit("submitted");
  } catch (error: any) {
    submissionFeedback.value = {
      type: "error",
      message: error?.message || String(t("rfq.quote.submitError")),
    };
  } finally {
    submitting.value = false;
  }
}

function handleCancel() {
  emit("cancel");
}

// UI helper functions for notes field
/**
 * 检查某行是否已填写价格
 */
function hasPrice(row: any): boolean {
  const entry = formData.lineItems[row.rfqLineItemId];
  return entry && entry.unitPrice !== undefined && entry.unitPrice !== null;
}

/**
 * 检查某行是否已填写备注
 */
function hasNotes(row: any): boolean {
  return row.notes && typeof row.notes === 'string' && row.notes.trim().length > 0;
}

/**
 * 判断备注是否为必填（无价格时必填）
 */
function isNotesRequired(row: any): boolean {
  return !hasPrice(row) && !hasNotes(row);
}

/**
 * 动态占位符文本
 */
function getNotesPlaceholder(row: any): string {
  if (hasPrice(row)) {
    return t('rfq.lineItems.notesPlaceholder'); // "可选备注"
  } else {
    return t('rfq.lineItems.notesRequiredPlaceholder'); // "请说明无法报价的原因（必填）"
  }
}

/**
 * 快速填充"无法报价"模板
 */
function fillNoQuoteTemplate(row: any): void {
  row.notes = t('rfq.quote.noQuoteTemplate');
  // 模板内容: "该物料暂无货源/不在我司供应范围内"

  notification.success(t('rfq.quote.templateFilled'));
}


// Progress tracking helpers
function getQuotedItemsCount(): number {
  return quoteLineItems.value.filter((item) => {
    const entry = formData.lineItems[item.rfqLineItemId];
    if (!entry) return false;

    // 有价格视为已响应
    const hasPrice = entry.unitPrice !== undefined && entry.unitPrice !== null;

    // 有备注视为已响应
    const hasNotes = item.notes && typeof item.notes === 'string' && item.notes.trim().length > 0;

    return hasPrice || hasNotes;
  }).length;
}

function getCompletionPercentage(): number {
  const total = quoteLineItems.value.length;
  if (total === 0) return 0;
  const quoted = getQuotedItemsCount();
  return Math.round((quoted / total) * 100);
}

function getProgressText(): string {
  const quoted = getQuotedItemsCount();
  const total = quoteLineItems.value.length;
  return `${quoted}/${total} ${t("rfq.quote.itemsResponded") || "items responded"}`;
}

function getProgressTagType(): "success" | "warning" | "info" {
  const percentage = getCompletionPercentage();
  if (percentage === 100) return "success";
  if (percentage >= 50) return "warning";
  return "info";
}

function getProgressColor(): string {
  const percentage = getCompletionPercentage();
  if (percentage === 100) return "#67c23a";
  if (percentage >= 50) return "#e6a23c";
  return "#909399";
}



</script>

<style scoped>
.supplier-quote-form {
  max-width: 1400px;
  margin: 0 auto;
}

.rfq-info-card {
  margin-bottom: 24px;
}

.card-header {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.card-header .header-title-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}

.card-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.required-header {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.quote-form-card {
  margin-bottom: 24px;
}

.item-name {
  font-weight: 500;
  color: #303133;
}

.item-spec {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}

.total-price {
  font-weight: 600;
  color: #409eff;
}

.quote-summary {
  display: flex;
  justify-content: flex-end;
  gap: 32px;
  margin-top: 24px;
  padding: 16px 24px;
  background-color: #f5f7fa;
  border-radius: 4px;
}

.summary-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.summary-item .label {
  font-size: 14px;
  color: #606266;
}

.summary-item .value {
  font-size: 16px;
  font-weight: 500;
  color: #303133;
}

.summary-item.total .label {
  font-size: 16px;
  font-weight: 600;
}

.summary-item.total .value {
  font-size: 20px;
  font-weight: 600;
  color: #409eff;
}

.tariff-cell {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-height: 64px;
}

.tariff-cell__value {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  color: #303133;
}

.tariff-cell__meta {
  display: flex;
  flex-direction: column;
  gap: 2px;
  font-size: 12px;
  color: #606266;
}

.tariff-cell__total {
  font-size: 12px;
  color: #409eff;
  font-weight: 500;
}

.tariff-cell__empty {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: #909399;
}

.tariff-spinner {
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

.submit-feedback {
  margin-top: 16px;
}

:deep(.el-form-item) {
  margin-bottom: 0;
}

:deep(.el-table .cell) {
  padding: 8px 4px;
}

/* Highlight required column */
:deep(.el-table .required-column) {
  background-color: #fef0f0;
}

:deep(.el-table thead .required-column) {
  background-color: #f56c6c;
  color: white;
}

/* Input with value gets success styling */
:deep(.el-input-number.has-value input) {
  border-color: #67c23a;
  background-color: #f0f9ff;
}

:deep(.el-input-number.has-value:hover input) {
  border-color: #67c23a;
}

/* Better form action button styling */
.form-actions el-button[type="primary"] {
  font-size: 16px;
  padding: 12px 24px;
  height: auto;
}

.currency-field {
  margin-top: 16px;
  display: flex;
  flex-direction: column;
}

.form-item-hint {
  margin: 6px 0 0;
  font-size: 12px;
  color: #909399;
}

/* 备注字段容器 */
.notes-field-wrapper {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

/* 备注必填状态（红色边框提示） */
.notes-required :deep(.el-textarea__inner) {
  border-color: #f56c6c;
  background-color: #fef0f0;
}

/* 备注已填写状态（绿色边框） */
.notes-filled :deep(.el-textarea__inner) {
  border-color: #67c23a;
  background-color: #f0f9ff;
}

/* 无法报价按钮 */
.no-quote-btn {
  width: 100%;
  font-size: 12px;
}

/* 备注列表头帮助图标 */
.notes-header {
  display: flex;
  align-items: center;
  gap: 4px;
}

/* 表头帮助图标样式 */
.header-with-help {
  display: flex;
  align-items: center;
  gap: 4px;
}

.help-icon {
  font-size: 14px;
  color: #909399;
  cursor: help;
}

.help-icon:hover {
  color: #409eff;
}
</style>
