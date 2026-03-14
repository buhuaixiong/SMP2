<template>
  <el-dialog
    v-model="dialogVisible"
    :title="t('rfq.rounds.historyDialogTitle')"
    width="1200px"
    top="5vh"
    :close-on-click-modal="false"
    class="rfq-supplier-comparison-history-dialog"
  >
    <div v-loading="loading">
      <el-alert
        v-if="history?.rounds?.length"
        type="info"
        :closable="false"
        :title="t('rfq.rounds.historyDialogHint')"
        class="history-alert"
      />

      <el-empty
        v-if="!loading && (!history?.rounds || history.rounds.length === 0)"
        :description="t('rfq.rounds.noHistoryData')"
        :image-size="96"
      />

      <el-tabs v-else v-model="activeRoundTab" class="history-tabs">
        <el-tab-pane
          v-for="round in history?.rounds ?? []"
          :key="String(round.summary?.id ?? round.summary?.roundNumber ?? '')"
          :name="String(round.summary?.id ?? round.summary?.roundNumber ?? '')"
          :label="roundLabel(round.summary)"
        >
          <el-descriptions v-if="round.summary" :column="4" border class="history-summary">
            <el-descriptions-item :label="t('rfq.rounds.status')">
              <el-tag :type="statusTagType(round.summary.status)">
                {{ translateStatus(round.summary.status) }}
              </el-tag>
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.rounds.deadline')">
              {{ formatDateTime(round.summary.bidDeadline) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.rounds.invitedCount')">
              {{ round.summary.invitedCount ?? 0 }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('rfq.rounds.submittedCount')">
              {{ round.summary.submittedCount ?? 0 }}
            </el-descriptions-item>
          </el-descriptions>

          <RfqRoundComparisonTable :line-items="normalizedLineItems" :quotes="round.quotes ?? []" />
        </el-tab-pane>
      </el-tabs>
    </div>
  </el-dialog>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import RfqRoundComparisonTable from "@/components/RfqRoundComparisonTable.vue";
import type { RfqItem, RfqSupplierComparisonHistory } from "@/types";

const props = withDefaults(
  defineProps<{
    visible: boolean;
    loading?: boolean;
    history?: RfqSupplierComparisonHistory | null;
    lineItems: RfqItem[];
    formatDateTime: (value?: string | null) => string;
  }>(),
  {
    loading: false,
    history: null,
  },
);

const emit = defineEmits<{
  (event: "update:visible", value: boolean): void;
}>();

const { t } = useI18n();
const dialogVisible = ref(props.visible);
const activeRoundTab = ref("");
const normalizedLineItems = computed(() =>
  (props.lineItems ?? []).map((item, index) => ({
    ...item,
    id: Number(item.id ?? index + 1),
  })),
);

watch(
  () => props.visible,
  (value) => {
    dialogVisible.value = value;
  },
);

watch(dialogVisible, (value) => {
  emit("update:visible", value);
});

watch(
  () => props.history,
  (value) => {
    activeRoundTab.value = String(value?.rounds?.[0]?.summary?.id ?? "");
  },
  { immediate: true },
);

function roundLabel(summary?: RfqSupplierComparisonHistory["rounds"][number]["summary"] | null) {
  return summary
    ? t("rfq.rounds.roundLabel", { number: summary.roundNumber })
    : t("rfq.rounds.unknownRound");
}

function statusTagType(status?: string | null) {
  switch (String(status ?? "").toLowerCase()) {
    case "published":
      return "success";
    case "closed":
      return "info";
    case "cancelled":
      return "danger";
    case "draft":
      return "warning";
    default:
      return "info";
  }
}

function translateStatus(status?: string | null) {
  const key = String(status ?? "").trim();
  return key ? t(`rfq.status.${key}`) : "-";
}
</script>

<style scoped>
.history-alert {
  margin-bottom: 16px;
}

.history-tabs {
  margin-top: 8px;
}

.history-summary {
  margin-bottom: 16px;
}
</style>
