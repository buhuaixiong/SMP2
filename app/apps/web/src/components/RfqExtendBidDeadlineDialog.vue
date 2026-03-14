<template>
  <el-dialog
    v-model="dialogVisible"
    :title="t('rfq.rounds.extendDialogTitle')"
    width="560px"
    :close-on-click-modal="false"
  >
    <div class="dialog-body">
      <el-alert
        type="info"
        :closable="false"
        :title="t('rfq.rounds.extendDialogHint')"
        class="dialog-alert"
      />

      <el-descriptions v-if="currentRound" :column="2" border class="round-summary">
        <el-descriptions-item :label="t('rfq.rounds.roundNumber')">
          {{ t('rfq.rounds.roundLabel', { number: currentRound.roundNumber }) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.deadline')">
          {{ formatDateTime(currentRound.bidDeadline) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.pendingCount')">
          {{ currentRound.pendingCount ?? 0 }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.status')">
          <el-tag type="success">{{ translateStatus(currentRound.status) }}</el-tag>
        </el-descriptions-item>
      </el-descriptions>

      <el-form label-position="top">
        <el-form-item :label="t('rfq.rounds.newDeadline')" required>
          <el-date-picker
            v-model="deadlineValue"
            type="datetime"
            value-format="YYYY-MM-DDTHH:mm:ss[Z]"
            :placeholder="t('rfq.rounds.newDeadlinePlaceholder')"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item :label="t('rfq.rounds.reason')">
          <el-input
            v-model="reason"
            type="textarea"
            :rows="4"
            :placeholder="t('rfq.rounds.reasonPlaceholder')"
          />
        </el-form-item>
      </el-form>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="dialogVisible = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ t('common.confirm') }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import { ElMessage } from "element-plus";
import { useI18n } from "vue-i18n";
import { extendRfqBidDeadline } from "@/api/rfq";
import type { RfqBidRoundSummary } from "@/types";
import { useNotification } from "@/composables";
import { extractErrorMessage } from "@/utils/errorHandling";

const props = withDefaults(
  defineProps<{
    visible: boolean;
    rfqId?: number | null;
    currentRound?: RfqBidRoundSummary | null;
    formatDateTime: (value?: string | null) => string;
  }>(),
  {
    rfqId: null,
    currentRound: null,
  },
);

const emit = defineEmits<{
  (event: "update:visible", value: boolean): void;
  (event: "success"): void;
}>();

const { t } = useI18n();
const notification = useNotification();
const dialogVisible = ref(props.visible);
const submitting = ref(false);
const deadlineValue = ref("");
const reason = ref("");

watch(
  () => props.visible,
  (value) => {
    dialogVisible.value = value;
    if (value) {
      deadlineValue.value = props.currentRound?.bidDeadline ?? "";
      reason.value = props.currentRound?.extensionReason ?? "";
    }
  },
  { immediate: true },
);

watch(dialogVisible, (value) => emit("update:visible", value));

function translateStatus(status?: string | null) {
  const key = String(status ?? "").trim();
  return key ? t(`rfq.status.${key}`) : "-";
}

async function handleSubmit() {
  if (!props.rfqId) {
    return;
  }

  if (!deadlineValue.value) {
    ElMessage.warning(t("rfq.rounds.newDeadlineRequired"));
    return;
  }

  submitting.value = true;
  try {
    await extendRfqBidDeadline(props.rfqId, {
      newDeadline: deadlineValue.value,
      reason: reason.value.trim() || null,
    });
    notification.success(t("rfq.rounds.extendSuccess"));
    emit("success");
    dialogVisible.value = false;
  } catch (error) {
    notification.error(extractErrorMessage(error));
  } finally {
    submitting.value = false;
  }
}
</script>

<style scoped>
.dialog-alert {
  margin-bottom: 16px;
}

.round-summary {
  margin-bottom: 16px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
</style>
