<template>
  <div class="pr-fill-form">
    <el-card>
      <template #header>
        <h3>{{ t("rfq.pr.fillPR") }}</h3>
      </template>

      <!-- RFQ信息摘要 -->
      <el-alert
        :title="t('rfq.pr.instruction')"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 24px"
      />

      <el-descriptions :column="2" border style="margin-bottom: 24px">
        <el-descriptions-item :label="t('rfq.form.title')">
          {{ rfq?.title }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.approval.selectedSupplier')">
          {{ selectedQuote?.supplierName }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.quote.grandTotal')">
          <span class="highlight-amount">
            {{ formatPrice(selectedQuote?.totalAmount, selectedQuote?.currency) }}
          </span>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.pr.approvalStatus')">
          <el-tag type="success">{{ t("rfq.approval.status.approved") }}</el-tag>
        </el-descriptions-item>
      </el-descriptions>

      <!-- PR填写表单 -->
      <el-form ref="formRef" :model="formData" :rules="rules" label-position="top"><el-form-item :label="t('rfq.pr.notes')">
          <el-input
            v-model="formData.notes"
            type="textarea"
            :rows="4"
            :placeholder="t('rfq.pr.notesPlaceholder')"
            maxlength="500"
            show-word-limit
          />
        </el-form-item>

        <div class="form-actions">
          <el-button @click="handleCancel">{{ t("common.cancel") }}</el-button>
          <el-button type="primary" :loading="submitting" @click="handleSubmit">
            {{ t("rfq.pr.submitPR") }}
          </el-button>
        </div>
      </el-form>
    </el-card>
  </div>
</template>

<script setup lang="ts">




import { ref, reactive } from "vue";
import { useI18n } from "vue-i18n";

import type { FormInstance, FormRules } from "element-plus";
import { fillPr } from "@/api/rfq";


import { useNotification } from "@/composables";

const notification = useNotification();
const props = defineProps<{
  rfqId: number;
  rfq: any;
  selectedQuote: any;
}>();

const emit = defineEmits<{
  (e: "submitted"): void;
  (e: "cancel"): void;
}>();

const { t } = useI18n();
const formRef = ref<FormInstance>();
const submitting = ref(false);
const departmentUsers = ref<any[]>([]);

const formData = reactive({
  prNumber: "",
  prDate: new Date().toISOString().split("T")[0],
  notes: "",
});

const rules: FormRules = {
  prNumber: [{ required: true, message: t("rfq.pr.prNumberRequired"), trigger: "blur" }],
  prDate: [{ required: true, message: t("rfq.pr.prDateRequired"), trigger: "change" }],
};


function formatPrice(amount?: number, currency = "CNY"): string {
  if (amount === undefined || amount === null) return "-";
  return `${amount.toFixed(2)} ${currency}`;
}

async function handleSubmit() {
  if (!formRef.value) return;

  try {
    await formRef.value.validate();

    await notification.confirm(
      t("rfq.pr.confirmSubmit", { prNumber: formData.prNumber }),
      t("common.confirm"),
      {
        confirmButtonText: t("common.confirm"),
        cancelButtonText: t("common.cancel"),
        type: "warning",
      },
    );

    submitting.value = true;

    await fillPr(props.rfqId, {
      prNumber: formData.prNumber,
      prDate: formData.prDate,
      notes: formData.notes,
    });

    notification.success(t("rfq.pr.submitSuccess"));
    emit("submitted");
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error?.message || t("rfq.pr.submitError"));
    }
  } finally {
    submitting.value = false;
  }
}

function handleCancel() {
  emit("cancel");
}




</script>

<style scoped>
.pr-fill-form {
  max-width: 1000px;
  margin: 0 auto;
}

.highlight-amount {
  font-size: 18px;
  font-weight: 600;
  color: #67c23a;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 24px;
  padding-top: 24px;
  border-top: 1px solid #dcdfe6;
}
</style>
