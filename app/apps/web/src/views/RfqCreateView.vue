<template>
  <div class="rfq-create-view">
    <PageHeader :title="t('rfq.create.title')">
      <template #actions>
        <el-upload
          ref="excelUploadRef"
          action="#"
          :auto-upload="false"
          :on-change="handleExcelFileSelect"
          :show-file-list="false"
          accept=".xlsx,.xls,.xlsm"
          style="display: inline-block; margin-right: 8px"
        >
          <el-button type="success" :icon="Upload">
            {{ t("rfq.import.button") }}
          </el-button>
        </el-upload>
        <el-button @click="goBack">{{ t("common.back") }}</el-button>
      </template>
    </PageHeader>

    <el-card class="wizard-card">
      <el-alert
        v-if="prefillNotice"
        class="prefill-alert"
        :title="t('rfq.create.prefilledFromRequisition')"
        type="success"
        show-icon
        :closable="false"
      >
        <template #default>
          <div class="prefill-alert-body">
            <span>{{ t("requisition.requisitionNumber") }} #{{ prefillNotice.id }}</span>
            <el-button
              link
              type="primary"
              size="small"
              @click="openSourceRequisition(prefillNotice.id)"
            >
              {{ t("requisition.list.actions.view") }}
            </el-button>
          </div>
        </template>
      </el-alert>

      <el-alert
        v-if="importedFromExcel"
        class="prefill-alert"
        :title="t('rfq.import.alertTitle')"
        type="success"
        show-icon
        closable
        @close="importedFromExcel = false"
      >
        <template #default>
          <div class="prefill-alert-body">
            <span>{{ t("rfq.import.alertDescription", { count: formData.lineItems?.length || 0 }) }}</span>
            <span v-if="importedPrNumber" style="margin-left: 12px; color: #909399">
              PR: {{ importedPrNumber }}
            </span>
          </div>
        </template>
      </el-alert>
      <template v-if="prefillWarnings.length">
        <el-alert
          v-for="(warning, index) in prefillWarnings"
          :key="`prefill-warning-${index}`"
          class="prefill-alert warning"
          type="warning"
          show-icon
          :closable="false"
        >
          <template #title>{{ warning }}</template>
        </el-alert>
      </template>
      <!-- Step Progress -->
      <el-steps :active="currentStep" align-center finish-status="success">
        <el-step :title="t('rfq.materialType.label')" :icon="Grid" />
        <el-step :title="t('rfq.create.rfqDetails')" :icon="Edit" />
        <el-step :title="t('rfq.create.step4')" :icon="User" />
        <el-step :title="t('rfq.create.step5')" :icon="Check" />
      </el-steps>

      <el-divider />

      <!-- Step Content -->
      <div class="step-content">
        <!-- Step 1: Material Category Selection (IDM/DM) -->
        <div v-if="currentStep === 0" class="step-panel">
          <h2>{{ t("rfq.create.selectMaterialType") }}</h2>
          <p class="step-description">{{ t("rfq.create.materialTypeDesc") }}</p>

          <div class="material-type-selection">
            <el-radio-group v-model="formData.materialCategoryType" size="large">
              <el-card
                class="material-type-card"
                :class="{ selected: formData.materialCategoryType === 'IDM' }"
                shadow="hover"
                @click="formData.materialCategoryType = 'IDM'"
              >
                <el-radio value="IDM">
                  <div class="material-type-content">
                    <h3>{{ t("rfq.materialType.idm") }}</h3>
                    <p>{{ t("rfq.materialType.idmDesc") }}</p>
                  </div>
                </el-radio>
              </el-card>

              <el-card
                class="material-type-card"
                :class="{ selected: formData.materialCategoryType === 'DM' }"
                shadow="hover"
                @click="formData.materialCategoryType = 'DM'"
              >
                <el-radio value="DM" disabled>
                  <div class="material-type-content">
                    <h3>{{ t("rfq.materialType.dm") }}</h3>
                    <p>{{ t("rfq.materialType.dmDesc") }}</p>
                    <el-tag type="info" size="small">即将上线</el-tag>
                  </div>
                </el-radio>
              </el-card>
            </el-radio-group>
          </div>
        </div>

        <!-- Step 2: RFQ Details (Upper: Basic Info, Lower: Line Items) -->
        <div v-if="currentStep === 1" class="step-panel">
          <h2>{{ t("rfq.create.rfqDetails") }}</h2>
          <p class="step-description">填写询价单基本信息和需求行明细</p>

          <!-- Upper section: Basic Information -->
          <div class="rfq-details-upper">
            <h3 class="section-title">基本信息</h3>
            <RfqForm
              ref="rfqFormRef"
              :model-value="formData"
              @update:model-value="handleFormUpdate"
            />
          </div>

          <el-divider />

          <!-- Lower section: Line Items -->
          <div class="rfq-details-lower">
            <RfqLineItemsEditor v-model="formData.lineItems" />
          </div>
        </div>

        <!-- Step 3: Supplier Invitation -->
        <div v-if="currentStep === 2" class="step-panel">
          <h2>{{ t("rfq.create.inviteSuppliers") }}</h2>
          <p class="step-description">{{ t("rfq.create.supplierInvitationDesc") }}</p>
          <RfqSupplierInvitation
            v-model="formData.supplierIds"
            v-model:external-emails="formData.externalEmails"
          />

          <!-- Minimum Supplier Count Notice -->
          <el-alert
            :title="t('rfq.create.minSupplierNotice', { count: formData.minSupplierCount || 3 })"
            type="info"
            :closable="false"
            style="margin-top: 24px"
          />

          <!-- Exception Note (shown when below minimum) -->
          <div
            v-if="totalSupplierCount < (formData.minSupplierCount || 3)"
            style="margin-top: 16px"
          >
            <el-alert :title="t('rfq.create.belowMinimum')" type="warning" :closable="false">
              <template #default>
                <p style="margin: 8px 0">{{ t("rfq.create.exceptionRequired") }}</p>
                <el-input
                  v-model="formData.supplierExceptionNote"
                  type="textarea"
                  :rows="3"
                  :placeholder="t('rfq.create.exceptionNotePlaceholder')"
                  maxlength="500"
                  show-word-limit
                />
              </template>
            </el-alert>
          </div>
        </div>

        <!-- Step 4: Review & Submit -->
        <div v-if="currentStep === 3" class="step-panel">
          <h2>{{ t("rfq.create.reviewSubmit") }}</h2>
          <p class="step-description">{{ t("rfq.create.reviewDesc") }}</p>

          <!-- 物料清单摘要 -->
          <h3 style="margin: 24px 0 16px 0">
            {{ t("rfq.lineItems.title") }} ({{ formData.lineItems?.length || 0 }} 项)
          </h3>
          <el-table
            v-if="formData.lineItems && formData.lineItems.length > 0"
            :data="formData.lineItems"
            border
          >
            <el-table-column type="index" :label="t('rfq.items.lineNumber')" width="70" />
            <el-table-column :label="t('rfq.lineItems.materialCategory')" width="120">
              <template #default="{ row }">
                {{
                  row.materialCategory === "equipment"
                    ? t("rfq.distributionCategory.equipment")
                    : row.materialCategory === "consumables"
                      ? t("rfq.lineItems.consumables")
                      : row.materialCategory === "hardware"
                        ? t("rfq.distributionCategory.hardware")
                        : row.materialCategory || "-"
                }}
              </template>
            </el-table-column>
            <el-table-column :label="t('rfq.lineItems.brand')" width="100">
              <template #default="{ row }">
                {{ row.brand || "-" }}
              </template>
            </el-table-column>
            <el-table-column :label="t('rfq.items.itemName')" prop="itemName" min-width="150" />
            <el-table-column :label="t('rfq.lineItems.specifications')" min-width="150">
              <template #default="{ row }">
                {{ row.specifications || "-" }}
              </template>
            </el-table-column>
            <el-table-column :label="t('rfq.items.quantity')" width="120">
              <template #default="{ row }"> {{ row.quantity }} {{ row.unit }} </template>
            </el-table-column>
            <el-table-column :label="t('rfq.items.estimatedPrice')" width="140">
              <template #default="{ row }">
                {{ formatLineItemTotal(row) }}
              </template>
            </el-table-column>
          </el-table>

          <h3 style="margin: 32px 0 16px 0">{{ t("rfq.create.rfqDetails") }}</h3>
          <el-descriptions :column="2" border class="review-section">
            <el-descriptions-item :label="t('rfq.form.title')">
              {{ formData.title }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.form.rfqType')">
              <el-tag :type="formData.rfqType === 'short_term' ? 'success' : 'warning'">
                {{ t(`rfq.rfqType.${formData.rfqType}`) }}
              </el-tag>
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.form.deliveryPeriod')">
              {{ formData.deliveryPeriod }} {{ t("common.days") }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.form.budgetAmount')">
              {{ formData.budgetAmount || "-" }} {{ formData.currency }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.form.validUntil')">
              {{ formatDateTime(formData.validUntil) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.form.requestingParty')">
              {{ formData.requestingParty || "-" }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.form.requestingDepartment')">
              {{ formData.requestingDepartment || "-" }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.form.requirementDate')">
              {{ formData.requirementDate || "-" }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.invitation.selectedCount')" :span="2">
              {{ formData.supplierIds?.length || 0 }} {{ t("rfq.invitation.internalSuppliers") }}
              <span v-if="formData.externalEmails && formData.externalEmails.length > 0">
                + {{ formData.externalEmails.length }} {{ t("rfq.invitation.externalSuppliers") }}
              </span>
            </el-descriptions-item>
            <el-descriptions-item
              v-if="formData.supplierExceptionNote"
              :label="t('rfq.form.supplierExceptionNote')"
              :span="2"
            >
              <el-text type="warning">{{ formData.supplierExceptionNote }}</el-text>
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.form.description')" :span="2">
              {{ formData.description || "-" }}
            </el-descriptions-item>
            <el-descriptions-item
              v-if="formData.detailedParameters"
              :label="t('rfq.form.detailedParameters')"
              :span="2"
            >
              <pre
                style="margin: 0; white-space: pre-wrap; word-break: break-word; font-size: 13px"
                >{{ formData.detailedParameters }}</pre
              >
            </el-descriptions-item>
          </el-descriptions>
        </div>
      </div>

      <!-- Navigation Buttons -->
      <div class="step-actions">
        <el-button v-if="currentStep > 0" @click="prevStep">
          {{ t("common.previous") }}
        </el-button>
        <div style="flex: 1"></div>
        <el-button v-if="currentStep < 3" type="primary" @click="nextStep">
          {{ t("common.next") }}
        </el-button>
        <el-button v-if="currentStep === 3" type="success" :loading="submitting" @click="submitRfq">
          {{ t("rfq.create.submit") }}
        </el-button>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, defineAsyncComponent } from "vue";
import { useRouter, useRoute } from "vue-router";
import { useI18n } from "vue-i18n";
import type { UploadFile } from "element-plus";
import PageHeader from "@/components/layout/PageHeader.vue";
import { Grid, Edit, User, Check, Upload } from "@element-plus/icons-vue";
import type { RfqLineItem } from "@/components/RfqLineItemsEditor.vue";
import { createRfqWorkflow, importRfqFromExcel } from "@/api/rfq";
import { markItemAsConverted } from "@/api/requisitions";
import type { RfqMaterialType, RfqType } from "@/types";
import { useNotification, useFileUpload } from "@/composables";

const { t } = useI18n();
const router = useRouter();
const route = useRoute();
const notification = useNotification();
const fileUploader = useFileUpload();

const RfqForm = defineAsyncComponent(() => import("@/components/RfqForm.vue"));
const RfqSupplierInvitation = defineAsyncComponent(
  () => import("@/components/RfqSupplierInvitation.vue"),
);
const RfqLineItemsEditor = defineAsyncComponent(
  () => import("@/components/RfqLineItemsEditor.vue"),
);

const currentStep = ref(0);
const submitting = ref(false);
const rfqFormRef = ref();
const fromRequisitionId = ref<number | null>(null);
const fromRequisitionItemId = ref<number | null>(null);
const prefillNotice = ref<{ id: number } | null>(null);
const prefillWarnings = ref<string[]>([]);
const excelUploadRef = ref();
const importedFromExcel = ref(false);
const importedPrNumber = ref<string>('');

const formData = reactive<any>({
  // Material category type (IDM/DM)
  materialCategoryType: "IDM",
  // Line items mode
  lineItems: [] as RfqLineItem[],
  // Basic info
  title: "",
  description: "",
  rfqType: "short_term" as RfqType,
  deliveryPeriod: 15,
  projectLocation: "HZ",
  budgetAmount: undefined,
  currency: "CNY",
  validUntil: "",
  requiredDocuments: [],
  evaluationCriteria: {},
  supplierIds: [],
  externalEmails: [],
  minSupplierCount: 3,
  supplierExceptionNote: undefined,
  requestingParty: undefined,
  requestingDepartment: undefined,
  requirementDate: undefined,
  detailedParameters: undefined,
});

const totalSupplierCount = computed(() => {
  return (formData.supplierIds?.length || 0) + (formData.externalEmails?.length || 0);
});

function handleFormUpdate(update: Record<string, unknown>) {
  Object.assign(formData, update);
}

async function nextStep() {
  if (currentStep.value === 0) {
    // Step 1: Validate material category type selection
    if (!formData.materialCategoryType) {
      notification.warning("请选择物料大类");
      return;
    }
    if (formData.materialCategoryType === "DM") {
      notification.warning("DM物料功能即将上线，请选择IDM物料");
      return;
    }
  } else if (currentStep.value === 1) {
    // Step 2: Validate RFQ details AND line items
    const isValid = await rfqFormRef.value?.validate?.();
    if (!isValid) {
      notification.warning(t("rfq.validation.formIncomplete"));
      return;
    }

    // Validate line items
    if (!formData.lineItems || formData.lineItems.length === 0) {
      notification.warning("请至少添加一个需求行");
      return;
    }

    // Validate each line item's required fields
    for (let i = 0; i < formData.lineItems.length; i++) {
      const item = formData.lineItems[i];
      if (!item.materialCategory || !item.itemName || !item.quantity || !item.unit) {
        notification.warning(`第${i + 1}行：请填写完整信息（物料类别、物料名称、数量、单位）`);
        return;
      }
    }
  } else if (currentStep.value === 2) {
    const minCount = formData.minSupplierCount || 3;
    const hasException = Boolean(
      formData.supplierExceptionNote && formData.supplierExceptionNote.trim(),
    );
    if (totalSupplierCount.value < minCount && !hasException) {
      notification.warning(t("rfq.validation.minSupplierOrException", { count: minCount }));
      return;
    }
  }

  if (currentStep.value < 3) {
    currentStep.value += 1;
  }
}

function prevStep() {
  if (currentStep.value > 0) {
    currentStep.value -= 1;
  }
}

async function submitRfq() {
  submitting.value = true;
  try {
    // Use new workflow API
    const payload = {
      materialCategoryType: formData.materialCategoryType,
      title: formData.title,
      description: formData.description,
      rfqType: formData.rfqType,
      deliveryPeriod: formData.deliveryPeriod,
      projectLocation: formData.projectLocation,
      budgetAmount: formData.budgetAmount,
      currency: formData.currency,
      validUntil: formData.validUntil,
      requestingParty: formData.requestingParty,
      requestingDepartment: formData.requestingDepartment,
      requirementDate: formData.requirementDate,
      lineItems: formData.lineItems,
      supplierIds: formData.supplierIds,
      externalEmails: formData.externalEmails,
      minSupplierCount: formData.minSupplierCount,
      supplierExceptionNote: formData.supplierExceptionNote,
      requiredDocuments: formData.requiredDocuments,
      evaluationCriteria: formData.evaluationCriteria,
    };

    const rfq = await createRfqWorkflow(payload);

    if (fromRequisitionId.value && fromRequisitionItemId.value) {
      try {
        await markItemAsConverted(
          fromRequisitionId.value,
          fromRequisitionItemId.value,
          rfq.id,
        );
      } catch (error) {
        console.error("Failed to mark requisition item as converted:", error);
      }
    }

    notification.success(t("rfq.create.success"));
    router.push(`/rfq/${rfq.id}`);
  } catch (error: any) {
    notification.error(error?.message || t("rfq.create.error"));
  } finally {
    submitting.value = false;
  }
}

function goBack() {
  router.push("/rfq");
}

function getCategoryLabel(category: string | undefined) {
  if (!category) return "-";
  return t(`rfq.distributionCategory.${category}`);
}

function getSubcategoryLabel(subcategory: string | undefined) {
  if (!subcategory) return "-";
  return t(`rfq.distributionSubcategory.${subcategory}`);
}

function formatDateTime(dateString: string) {
  if (!dateString) return "-";
  try {
    return new Date(dateString).toLocaleString();
  } catch {
    return dateString;
  }
}

function formatLineItemTotal(item: RfqLineItem): string {
  if (item.quantity && item.estimatedUnitPrice) {
    return `${(item.quantity * item.estimatedUnitPrice).toFixed(2)} ${item.currency || "CNY"}`;
  }
  return "-";
}

function openSourceRequisition(id: number) {
  router.push({ name: "requisition-detail", params: { id } });
}

function mapRequisitionTypeToDistribution(itemType: string, itemSubtype?: string) {
  switch (itemType) {
    case "equipment": {
      if (itemSubtype === "non-standard" || itemSubtype === "non_standard") {
        return { category: "equipment", subcategory: "non_standard" };
      }
      if (itemSubtype === "fixtures" || itemSubtype === "molds" || itemSubtype === "blades") {
        return {
          category: "equipment",
          subcategory: itemSubtype as "fixtures" | "molds" | "blades",
        };
      }
      return { category: "equipment", subcategory: "standard" };
    }
    case "fixtures":
      return { category: "equipment", subcategory: "fixtures" };
    case "molds":
      return { category: "equipment", subcategory: "molds" };
    case "blades":
      return { category: "equipment", subcategory: "blades" };
    case "hardware":
      return { category: "auxiliary_materials", subcategory: "hardware" };
    case "consumables": {
      switch (itemSubtype) {
        case "labor_protection":
          return { category: "auxiliary_materials", subcategory: "labor_protection" };
        case "cleaning":
        case "production":
          return { category: "auxiliary_materials", subcategory: "production_supplies" };
        case "office":
          return { category: "auxiliary_materials", subcategory: "office_supplies" };
        case "accessories":
          return { category: "auxiliary_materials", subcategory: "accessories" };
        case "hardware":
          return { category: "auxiliary_materials", subcategory: "hardware" };
        case "other":
        default:
          return { category: "auxiliary_materials", subcategory: "others" };
      }
    }
    default:
      return { category: "auxiliary_materials", subcategory: "others" };
  }
}

function applyRequisitionPrefill(query: Record<string, unknown>) {
  const rawId = query.fromRequisition;
  const parsedId =
    typeof rawId === "number"
      ? rawId
      : typeof rawId === "string" && rawId.trim().length
        ? Number(rawId)
        : NaN;

  const rawItemId = query.fromRequisitionItem;
  const parsedItemId =
    typeof rawItemId === "number"
      ? rawItemId
      : typeof rawItemId === "string" && rawItemId.trim().length
        ? Number(rawItemId)
        : NaN;

  if (!rawId || Number.isNaN(parsedId)) {
    return;
  }

  fromRequisitionId.value = parsedId;
  if (!Number.isNaN(parsedItemId)) {
    fromRequisitionItemId.value = parsedItemId;
  }
  prefillNotice.value = { id: parsedId };

  const warnings: string[] = [];
  const validItemTypes = ["equipment", "consumables", "fixtures", "molds", "blades", "hardware"];
  let itemType = typeof query.itemType === "string" ? query.itemType : "";
  if (!validItemTypes.includes(itemType)) {
    if (itemType) {
      warnings.push(
        `Unrecognized item type "${itemType}" from requisition; defaulting to "equipment".`,
      );
    }
    itemType = "equipment";
  }

  let itemSubtype = typeof query.itemSubtype === "string" ? query.itemSubtype : undefined;
  if (itemType === "equipment") {
    const validEquipmentSubtypes = [
      "standard",
      "non-standard",
      "non_standard",
      "fixtures",
      "molds",
      "blades",
    ];
    if (itemSubtype && !validEquipmentSubtypes.includes(itemSubtype)) {
      warnings.push(`Unrecognized equipment subtype "${itemSubtype}"; using "standard".`);
      itemSubtype = "standard";
    }
  } else if (itemType === "consumables") {
    const validConsumableSubtypes = [
      "labor_protection",
      "cleaning",
      "office",
      "accessories",
      "production",
      "other",
    ];
    if (!itemSubtype || !validConsumableSubtypes.includes(itemSubtype)) {
      if (itemSubtype) {
        warnings.push(`Unrecognized consumable subtype "${itemSubtype}"; using "other".`);
      }
      itemSubtype = "other";
    }
  } else {
    itemSubtype = undefined;
  }

  const distribution = mapRequisitionTypeToDistribution(itemType, itemSubtype);
  formData.distributionCategory = distribution.category;
  formData.distributionSubcategory = distribution.subcategory;

  const title = typeof query.title === "string" ? query.title.trim() : "";
  if (title) {
    formData.title = title;
  }

  const description = typeof query.description === "string" ? query.description.trim() : "";
  const specifications =
    typeof query.specifications === "string" ? query.specifications.trim() : "";
  const quantity = typeof query.quantity === "string" ? query.quantity.trim() : "";
  const unit = typeof query.unit === "string" ? query.unit.trim() : "";
  const notes = typeof query.notes === "string" ? query.notes.trim() : "";

  if (description) {
    formData.description = description;
  }

  let detailedParameters = description;
  if (specifications) {
    detailedParameters += `\n\n${t("requisition.form.fields.specifications.label")}:\n${specifications}`;
  }
  if (quantity && unit) {
    detailedParameters += `\n\n${t("requisition.detail.fields.quantity")}: ${quantity} ${unit}`;
  }
  if (notes) {
    detailedParameters += `\n\n${t("requisition.form.fields.notes.label")}:\n${notes}`;
  }
  formData.detailedParameters = detailedParameters ? detailedParameters : undefined;

  const rawBudget =
    typeof query.budgetAmount === "string"
      ? query.budgetAmount.trim()
      : String(query.budgetAmount ?? "").trim();
  if (rawBudget) {
    const parsedBudget = Number(rawBudget);
    if (Number.isFinite(parsedBudget)) {
      formData.budgetAmount = parsedBudget;
    } else {
      warnings.push(`Unable to parse budget amount "${rawBudget}"; leaving blank.`);
    }
  }

  const currency = typeof query.currency === "string" ? query.currency.trim() : "";
  if (currency) {
    formData.currency = currency;
  }

  const requestingDepartment =
    typeof query.requestingDepartment === "string" ? query.requestingDepartment.trim() : "";
  if (requestingDepartment) {
    formData.requestingDepartment = requestingDepartment;
  }

  const requirementDate =
    typeof query.requirementDate === "string" ? query.requirementDate.trim() : "";
  if (requirementDate) {
    formData.requirementDate = requirementDate;
  }

  prefillWarnings.value = warnings;
  currentStep.value = Math.max(currentStep.value, 1);

  notification.success(t("rfq.create.prefilledFromRequisition"), undefined, {
    duration: 3000,
  });
}

/**
 * 处理Excel文件选择
 */
async function handleExcelFileSelect(file: UploadFile) {
  if (!file.raw) return;

  // 文件大小验证（最大10MB）
  if (file.size && file.size > 10 * 1024 * 1024) {
    notification.error(t("rfq.import.fileSizeError"));
    return;
  }

  // 文件类型验证
  const validTypes = [
    'application/vnd.ms-excel',
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    'application/vnd.ms-excel.sheet.macroEnabled.12'
  ];
  if (!validTypes.includes(file.raw.type) && !file.name.match(/\.(xlsx?|xlsm)$/i)) {
    notification.error(t("rfq.import.fileTypeError"));
    return;
  }

  // 检查是否需要覆盖确认
  const hasFormData = formData.title || formData.lineItems.length > 0;
  if (hasFormData) {
    try {
      await notification.confirm(
        t("rfq.import.confirmOverwrite"),
        t("common.confirm"),
        { type: "warning" }
      );
    } catch {
      // 用户取消
      excelUploadRef.value?.clearFiles();
      return;
    }
  }

  try {
    submitting.value = true;

    // 上传文件到后端解析
    const result = await fileUploader.upload(file.raw, {
      request: async (formData) => {
        formData.append("sheetName", "PRBuyer");
        formData.append("headerRow", "15");
        return importRfqFromExcel(formData);
      },
      errorMessage: t("rfq.import.error"),
    });

    // 填充表单数据
    formData.title = result.title || '';
    formData.description = result.description || '';
    formData.requestingDepartment = result.requestingDepartment || '';
    formData.requirementDate = result.requirementDate || '';
    formData.currency = result.currency || 'CNY';
    formData.budgetAmount = result.budgetAmount || undefined;

    // 转换requirements为lineItems
    formData.lineItems = result.requirements.map((req, index) => ({
      lineNumber: index + 1,
      materialCategory: 'auxiliary_materials', // 默认辅料
      itemName: req.itemName,
      itemCode: req.itemCode || undefined,
      quantity: req.quantity,
      unit: req.unit,
      estimatedUnitPrice: req.estimatedUnitPrice || undefined,
      currency: result.currency,
      remarks: req.notes || undefined,
    } as RfqLineItem));

    // 设置导入标记
    importedFromExcel.value = true;
    importedPrNumber.value = result.prNumber;

    // 显示推荐供应商提示
    if (result.recommendedSuppliers && result.recommendedSuppliers.length > 0) {
      notification.info(
        t("rfq.import.recommendedSuppliers", {
          count: result.recommendedSuppliers.length,
          names: result.recommendedSuppliers.slice(0, 3).join(", "),
        }),
        undefined,
        { duration: 5000 },
      );
    }

    // 跳转到第2步（详情填写）
    currentStep.value = 1;

    notification.success(t("rfq.import.success", { count: result.requirements.length }), undefined, {
      duration: 3000,
    });
  } catch (error: any) {
    console.error('Excel导入失败:', error);
    notification.error(error.message || t("rfq.import.error"));
  } finally {
    submitting.value = false;
    // 清除上传组件状态
    excelUploadRef.value?.clearFiles();
  }
}

/**
 * 检查表单是否有数据
 */
function hasFormData(): boolean {
  return formData.title !== '' || formData.lineItems.length > 0;
}

onMounted(() => {
  applyRequisitionPrefill(route.query as Record<string, unknown>);
});
</script>

<style scoped>
.rfq-create-view {
  padding: 24px;
  max-width: 1200px;
  margin: 0 auto;
}



.wizard-card {
  padding: 32px;
}

.step-content {
  min-height: 500px;
  padding: 32px 0;
}

.step-panel h2 {
  font-size: 20px;
  font-weight: 600;
  color: #303133;
  margin: 0 0 8px 0;
}

.step-description {
  font-size: 14px;
  color: #606266;
  margin: 0 0 24px 0;
}

.review-section {
  margin-top: 24px;
}

.step-actions {
  display: flex;
  justify-content: space-between;
  margin-top: 32px;
  padding-top: 24px;
  border-top: 1px solid #dcdfe6;
}

.prefill-alert {
  margin-bottom: 16px;
}

.prefill-alert.warning {
  margin-bottom: 12px;
}

.prefill-alert-body {
  display: flex;
  align-items: center;
  gap: 12px;
}

.material-type-selection {
  margin-top: 32px;
}

.material-type-selection :deep(.el-radio-group) {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 24px;
  width: 100%;
}

.material-type-card {
  cursor: pointer;
  transition: all 0.3s ease;
  border: 2px solid transparent;
}

.material-type-card.selected {
  border-color: #409eff;
  box-shadow: 0 2px 12px 0 rgba(64, 158, 255, 0.3);
}

.material-type-card :deep(.el-card__body) {
  padding: 24px;
}

.material-type-card :deep(.el-radio) {
  width: 100%;
  height: auto;
  white-space: normal;
}

.material-type-card :deep(.el-radio__label) {
  width: 100%;
  padding-left: 12px;
}

.material-type-content h3 {
  margin: 0 0 8px 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.material-type-content p {
  margin: 0 0 8px 0;
  font-size: 14px;
  color: #606266;
  line-height: 1.5;
}

.rfq-details-upper {
  margin-bottom: 24px;
}

.rfq-details-lower {
  margin-top: 24px;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  margin: 0 0 16px 0;
}

@media (max-width: 768px) {
  .rfq-create-view {
    padding: 16px;
  }

  .material-type-selection :deep(.el-radio-group) {
    grid-template-columns: 1fr;
  }
}
</style>

