<template>
  <el-card class="rfq-bid-round-history-card" shadow="never">
    <template #header>
      <div class="card-header-flex">
        <div>
          <div class="card-title">{{ t("rfq.rounds.currentRoundTitle") }}</div>
          <div v-if="currentRound" class="card-subtitle">
            {{ t("rfq.rounds.defaultComparisonLatestOnly") }}
          </div>
        </div>
        <div class="header-actions">
          <el-button
            v-if="showExtendAction"
            type="warning"
            plain
            :disabled="extendActionDisabled"
            @click="$emit('extend-deadline')"
          >
            {{ t("rfq.rounds.extendDeadline") }}
          </el-button>
          <el-button
            v-if="showStartNextAction"
            type="primary"
            :disabled="startNextActionDisabled"
            @click="$emit('start-next-round')"
          >
            {{ t("rfq.rounds.startNextRound") }}
          </el-button>
          <el-button v-if="showHistoryAction" type="primary" plain @click="$emit('view-history')">
            {{ t("rfq.rounds.viewHistory") }}
          </el-button>
        </div>
      </div>
    </template>

    <el-empty v-if="!currentRound" :description="t('rfq.rounds.noRounds')" :image-size="72" />

    <template v-else>
      <el-descriptions :column="4" border>
        <el-descriptions-item :label="t('rfq.rounds.roundNumber')">
          <el-tag :type="currentRound.isLatest ? 'primary' : 'info'">
            {{ t('rfq.rounds.roundLabel', { number: currentRound.roundNumber }) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.status')">
          <el-tag :type="statusTagType(currentRound.status)">
            {{ translateStatus(currentRound.status) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.deadline')">
          {{ formatDateTime(currentRound.bidDeadline) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.totalRounds')">
          {{ rounds.length }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.invitedCount')">
          {{ currentRound.invitedCount ?? 0 }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.submittedCount')">
          {{ currentRound.submittedCount ?? 0 }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.pendingCount')">
          {{ currentRound.pendingCount ?? 0 }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.scope')">
          {{ t('rfq.rounds.latestRoundOnly') }}
        </el-descriptions-item>
      </el-descriptions>

      <div v-if="rounds.length > 1" class="round-tags">
        <el-tag
          v-for="round in rounds"
          :key="round.id"
          :type="round.id === currentRound.id ? 'primary' : 'info'"
          effect="plain"
        >
          {{ t('rfq.rounds.roundLabel', { number: round.roundNumber }) }}
        </el-tag>
      </div>
    </template>
  </el-card>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import type { RfqBidRoundSummary } from "@/types";

withDefaults(
  defineProps<{
    currentRound?: RfqBidRoundSummary | null;
    rounds?: RfqBidRoundSummary[];
    showHistoryAction?: boolean;
    showExtendAction?: boolean;
    showStartNextAction?: boolean;
    extendActionDisabled?: boolean;
    startNextActionDisabled?: boolean;
    formatDateTime: (value?: string | null) => string;
  }>(),
  {
    currentRound: null,
    rounds: () => [],
    showHistoryAction: false,
    showExtendAction: false,
    showStartNextAction: false,
    extendActionDisabled: false,
    startNextActionDisabled: false,
  },
);

defineEmits<{
  (event: "view-history"): void;
  (event: "extend-deadline"): void;
  (event: "start-next-round"): void;
}>();

const { t } = useI18n();

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
.card-header-flex {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.header-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.card-title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.card-subtitle {
  margin-top: 4px;
  color: #909399;
  font-size: 13px;
}

.round-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 16px;
}

@media (max-width: 768px) {
  .card-header-flex {
    flex-direction: column;
    align-items: stretch;
  }

  .header-actions {
    justify-content: flex-start;
  }
}
</style>
