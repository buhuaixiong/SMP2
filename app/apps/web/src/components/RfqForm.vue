<template>
  <el-form
    ref="formRef"
    :model="formData"
    :rules="rules"
    label-width="160px"
    label-position="left"
    class="rfq-form"
  >
    <el-form-item :label="t('rfq.form.title')" prop="title">
      <el-input
        v-model="formData.title"
        :placeholder="t('rfq.form.titlePlaceholder')"
        maxlength="200"
        show-word-limit
      />
    </el-form-item>

    <el-form-item :label="t('rfq.form.description')" prop="description">
      <el-input
        v-model="formData.description"
        type="textarea"
        :rows="4"
        :placeholder="t('rfq.form.descriptionPlaceholder')"
        maxlength="1000"
        show-word-limit
      />
    </el-form-item>

    <!-- IDM Equipment Standard Form enhancements -->
    <el-divider content-position="left">{{ t("rfq.form.requestInfo") }}</el-divider>

    <el-form-item :label="t('rfq.form.requestingParty')" prop="requestingParty">
      <el-input
        v-model="formData.requestingParty"
        :placeholder="t('rfq.form.requestingPartyPlaceholder')"
        maxlength="100"
      />
    </el-form-item>

    <el-form-item :label="t('rfq.form.requestingDepartment')" prop="requestingDepartment">
      <el-input
        v-model="formData.requestingDepartment"
        :placeholder="t('rfq.form.requestingDepartmentPlaceholder')"
        maxlength="100"
      />
    </el-form-item>

    <el-form-item :label="t('rfq.form.requirementDate')" prop="requirementDate">
      <el-date-picker
        v-model="formData.requirementDate"
        type="date"
        :placeholder="t('rfq.form.requirementDatePlaceholder')"
        format="YYYY-MM-DD"
        value-format="YYYY-MM-DD"
      />
    </el-form-item>

    <el-form-item :label="t('rfq.form.detailedParameters')" prop="detailedParameters">
      <el-input
        v-model="formData.detailedParameters"
        type="textarea"
        :rows="6"
        :placeholder="t('rfq.form.detailedParametersPlaceholder')"
        maxlength="2000"
        show-word-limit
      />
    </el-form-item>

    <el-divider />

    <el-form-item :label="t('rfq.form.rfqType')" prop="rfqType">
      <el-radio-group v-model="formData.rfqType">
        <el-radio label="short_term">
          {{ t("rfq.rfqType.shortTerm") }}
          <span class="hint-text">(≤ 30 {{ t("common.days") }})</span>
        </el-radio>
        <el-radio label="long_term">
          {{ t("rfq.rfqType.longTerm") }}
          <span class="hint-text">(> 30 {{ t("common.days") }})</span>
        </el-radio>
      </el-radio-group>
    </el-form-item>

    <el-form-item :label="t('rfq.form.deliveryPeriod')" prop="deliveryPeriod">
      <el-input-number
        v-model="formData.deliveryPeriod"
        :min="1"
        :max="365"
        :placeholder="t('rfq.form.deliveryPeriodPlaceholder')"
      />
      <span class="unit-text">{{ t("common.days") }}</span>
    </el-form-item>

    <el-form-item :label="t('rfq.form.projectLocation')" prop="projectLocation">
      <el-radio-group v-model="formData.projectLocation">
        <el-radio label="HZ">
          {{ t("rfq.projectLocation.HZ") }}
        </el-radio>
        <el-radio label="TH">
          {{ t("rfq.projectLocation.TH") }}
        </el-radio>
      </el-radio-group>
    </el-form-item>

    <el-form-item :label="t('rfq.form.budgetAmount')" prop="budgetAmount">
      <el-input-number
        v-model="formData.budgetAmount"
        :min="0"
        :precision="2"
        :placeholder="t('rfq.form.budgetAmountPlaceholder')"
        style="width: 200px"
      />
      <el-select v-model="formData.currency" style="width: 100px; margin-left: 12px">
        <el-option label="CNY" value="CNY" />
        <el-option label="USD" value="USD" />
        <el-option label="EUR" value="EUR" />
        <el-option label="THB" value="THB" />
      </el-select>
    </el-form-item>

    <el-form-item :label="t('rfq.form.validUntil')" prop="validUntil">
      <el-date-picker
        v-model="formData.validUntil"
        type="datetime"
        :placeholder="t('rfq.form.validUntilPlaceholder')"
        :disabled-date="disabledDate"
        format="YYYY-MM-DD HH:mm"
        value-format="YYYY-MM-DDTHH:mm:ss"
      />
    </el-form-item>

    <el-form-item :label="t('rfq.form.requiredDocuments')">
      <el-select
        v-model="formData.requiredDocuments"
        multiple
        :placeholder="t('rfq.form.requiredDocumentsPlaceholder')"
        style="width: 100%"
      >
        <el-option
          v-for="doc in documentOptions"
          :key="doc.value"
          :label="doc.label"
          :value="doc.value"
        />
      </el-select>
    </el-form-item>

  </el-form>
</template>

<script setup lang="ts">
import { ref, reactive, computed } from "vue";
import { useI18n } from "vue-i18n";
import type { FormInstance, FormRules } from "element-plus";
import type { CreateRfqPayload } from "@/types";

const { t } = useI18n();

interface Props {
  modelValue: Partial<CreateRfqPayload>;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  "update:modelValue": [value: Partial<CreateRfqPayload>];
}>();

const formRef = ref<FormInstance>();

const formData = computed({
  get: () => props.modelValue,
  set: (value) => emit("update:modelValue", value),
});

const documentOptions = [
  { label: t("rfq.documents.technicalSpec"), value: "technical_spec" },
  { label: t("rfq.documents.businessLicense"), value: "business_license" },
];

const rules = reactive<FormRules>({
  title: [
    { required: true, message: t("rfq.validation.titleRequired"), trigger: "blur" },
    { min: 5, max: 200, message: t("rfq.validation.titleLength"), trigger: "blur" },
  ],
  rfqType: [{ required: true, message: t("rfq.validation.rfqTypeRequired"), trigger: "change" }],
  projectLocation: [
    { required: true, message: t("rfq.validation.projectLocationRequired"), trigger: "change" },
  ],
  deliveryPeriod: [
    { required: true, message: t("rfq.validation.deliveryPeriodRequired"), trigger: "blur" },
    {
      type: "number",
      min: 1,
      max: 365,
      message: t("rfq.validation.deliveryPeriodRange"),
      trigger: "blur",
    },
  ],
  validUntil: [
    { required: true, message: t("rfq.validation.validUntilRequired"), trigger: "change" },
  ],
});

function disabledDate(time: Date) {
  return time.getTime() < Date.now() - 86400000; // Disable past dates
}

async function validate(): Promise<boolean> {
  if (!formRef.value) return false;
  try {
    await formRef.value.validate();
    return true;
  } catch {
    return false;
  }
}

function resetFields() {
  formRef.value?.resetFields();
}

defineExpose({
  validate,
  resetFields,
});
</script>

<style scoped>
.rfq-form {
  padding: 24px;
  background: white;
  border-radius: 8px;
}

.hint-text {
  font-size: 12px;
  color: #909399;
  margin-left: 4px;
}

.unit-text {
  margin-left: 12px;
  color: #606266;
  font-size: 14px;
}

:deep(.el-input-number) {
  width: 180px;
}

:deep(.el-form-item__label) {
  font-weight: 500;
}
</style>
