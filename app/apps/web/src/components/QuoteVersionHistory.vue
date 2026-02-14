<template>
  <div class="quote-version-history">
    <div class="history-header">
      <h3>{{ t("rfq.quotes.versionHistory") }}</h3>
      <el-text type="info">{{ t("rfq.quotes.totalVersions", { count: versions.length }) }}</el-text>
    </div>

    <div v-if="loading" class="loading-state">
      <el-skeleton :rows="5" animated />
    </div>

    <div v-else-if="versions.length === 0" class="empty-state">
      <el-empty :description="t('rfq.quotes.noVersionHistory')" />
    </div>

    <div v-else class="history-content">
      <el-timeline>
        <el-timeline-item
          v-for="version in versions"
          :key="version.id"
          :timestamp="formatDateTime(version.modifiedAt)"
          placement="top"
          :type="version.version === currentVersion ? 'primary' : 'info'"
        >
          <el-card :class="{ 'current-version': version.version === currentVersion }">
            <template #header>
              <div class="version-header">
                <div>
                  <el-text type="primary" size="large" style="font-weight: 600">
                    {{ t("rfq.quotes.versionNumber", { version: version.version }) }}
                  </el-text>
                  <el-tag
                    v-if="version.version === currentVersion"
                    type="success"
                    size="small"
                    style="margin-left: 8px"
                  >
                    {{ t("rfq.quotes.currentVersion") }}
                  </el-tag>
                </div>
                <el-text type="info" size="small">
                  {{ t("rfq.quotes.ipAddress") }}: {{ version.ipAddress || "-" }}
                </el-text>
              </div>
            </template>

            <el-descriptions :column="2" border size="small">
              <el-descriptions-item :label="t('rfq.quotes.unitPrice')">
                {{ formatCurrency(version.unitPrice, "CNY") }}
              </el-descriptions-item>
              <el-descriptions-item :label="t('rfq.quotes.totalAmount')">
                {{ formatCurrency(version.totalAmount, "CNY") }}
              </el-descriptions-item>
              <el-descriptions-item :label="t('rfq.quotes.brand')">
                {{ version.brand || "-" }}
              </el-descriptions-item>
              <el-descriptions-item :label="t('rfq.quotes.taxStatus')">
                <el-tag
                  :type="version.taxStatus === 'inclusive' ? 'success' : 'warning'"
                  size="small"
                >
                  {{ t(`rfq.taxStatus.${version.taxStatus}`) }}
                </el-tag>
              </el-descriptions-item>
              <el-descriptions-item :label="t('rfq.quotes.deliveryDate')" :span="2">
                {{ formatDate(version.deliveryDate) }}
              </el-descriptions-item>
              <el-descriptions-item :label="t('rfq.quotes.paymentTerms')" :span="2">
                {{ version.paymentTerms || "-" }}
              </el-descriptions-item>
              <el-descriptions-item :label="t('rfq.quotes.parameters')" :span="2">
                <pre v-if="version.parameters" class="text-content">{{ version.parameters }}</pre>
                <span v-else>-</span>
              </el-descriptions-item>
              <el-descriptions-item :label="t('rfq.quotes.optionalConfig')" :span="2">
                <pre v-if="version.optionalConfig" class="text-content">{{
                  version.optionalConfig
                }}</pre>
                <span v-else>-</span>
              </el-descriptions-item>
              <el-descriptions-item :label="t('rfq.quotes.notes')" :span="2">
                {{ version.notes || "-" }}
              </el-descriptions-item>
              <el-descriptions-item
                v-if="version.changeSummary"
                :label="t('rfq.quotes.changeSummary')"
                :span="2"
              >
                <el-text type="warning">{{ version.changeSummary }}</el-text>
              </el-descriptions-item>
            </el-descriptions>

            <!-- Show changes compared to previous version -->
            <div v-if="version.version > 1" class="version-changes" style="margin-top: 12px">
              <el-alert :title="t('rfq.quotes.changesFromPrevious')" type="info" :closable="false">
                <template v-if="getVersionChanges(version).length > 0">
                  <ul style="margin: 8px 0 0 0; padding-left: 20px">
                    <li v-for="(change, idx) in getVersionChanges(version)" :key="idx">
                      {{ change }}
                    </li>
                  </ul>
                </template>
                <template v-else>
                  <el-text type="info" size="small">{{
                    t("rfq.quotes.noChangesDetected")
                  }}</el-text>
                </template>
              </el-alert>
            </div>
          </el-card>
        </el-timeline-item>
      </el-timeline>
    </div>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";

import type { QuoteVersion } from "@/types";
import { fetchQuoteVersions } from "@/api/rfq";


import { useNotification } from "@/composables";

const notification = useNotification();
const { t } = useI18n();

interface Props {
  rfqId: number;
  quoteId: number;
  currentVersion?: number;
}

const props = defineProps<Props>();

const loading = ref(false);
const versions = ref<QuoteVersion[]>([]);

onMounted(async () => {
  await loadVersionHistory();
});

async function loadVersionHistory() {
  loading.value = true;
  try {
    const data = await fetchQuoteVersions(props.rfqId, props.quoteId);
    // Sort by version descending (newest first)
    versions.value = data.sort((a, b) => b.version - a.version);
  } catch (error) {
    console.error("Failed to load quote version history:", error);
    notification.error(t("rfq.quotes.loadVersionsError"));
  } finally {
    loading.value = false;
  }
}

function formatCurrency(amount: number, currency: string = "CNY"): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: currency,
  }).format(amount);
}

function formatDate(dateString: string | null): string {
  if (!dateString) return "-";
  return new Date(dateString).toLocaleDateString();
}

function formatDateTime(dateString: string | null): string {
  if (!dateString) return "-";
  return new Date(dateString).toLocaleString();
}

function getVersionChanges(version: QuoteVersion): string[] {
  const changes: string[] = [];
  const prevVersion = versions.value.find((v) => v.version === version.version - 1);

  if (!prevVersion) return changes;

  // Compare fields and track changes
  if (version.unitPrice !== prevVersion.unitPrice) {
    changes.push(
      t("rfq.quotes.changeField", {
        field: t("rfq.quotes.unitPrice"),
        from: formatCurrency(prevVersion.unitPrice),
        to: formatCurrency(version.unitPrice),
      }),
    );
  }

  if (version.totalAmount !== prevVersion.totalAmount) {
    changes.push(
      t("rfq.quotes.changeField", {
        field: t("rfq.quotes.totalAmount"),
        from: formatCurrency(prevVersion.totalAmount),
        to: formatCurrency(version.totalAmount),
      }),
    );
  }

  if (version.brand !== prevVersion.brand) {
    changes.push(
      t("rfq.quotes.changeField", {
        field: t("rfq.quotes.brand"),
        from: prevVersion.brand || "-",
        to: version.brand || "-",
      }),
    );
  }

  if (version.taxStatus !== prevVersion.taxStatus) {
    changes.push(
      t("rfq.quotes.changeField", {
        field: t("rfq.quotes.taxStatus"),
        from: t(`rfq.taxStatus.${prevVersion.taxStatus}`),
        to: t(`rfq.taxStatus.${version.taxStatus}`),
      }),
    );
  }

  if (version.deliveryDate !== prevVersion.deliveryDate) {
    changes.push(
      t("rfq.quotes.changeField", {
        field: t("rfq.quotes.deliveryDate"),
        from: formatDate(prevVersion.deliveryDate),
        to: formatDate(version.deliveryDate),
      }),
    );
  }

  if (version.paymentTerms !== prevVersion.paymentTerms) {
    changes.push(
      t("rfq.quotes.changeField", {
        field: t("rfq.quotes.paymentTerms"),
        from: prevVersion.paymentTerms || "-",
        to: version.paymentTerms || "-",
      }),
    );
  }

  if (version.parameters !== prevVersion.parameters) {
    changes.push(t("rfq.quotes.fieldChanged", { field: t("rfq.quotes.parameters") }));
  }

  if (version.optionalConfig !== prevVersion.optionalConfig) {
    changes.push(t("rfq.quotes.fieldChanged", { field: t("rfq.quotes.optionalConfig") }));
  }

  if (version.notes !== prevVersion.notes) {
    changes.push(t("rfq.quotes.fieldChanged", { field: t("rfq.quotes.notes") }));
  }

  return changes;
}




</script>

<style scoped>
.quote-version-history {
  padding: 24px;
  background: white;
  border-radius: 8px;
}

.history-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.history-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.loading-state,
.empty-state {
  padding: 40px 0;
}

.history-content {
  margin-top: 16px;
}

.version-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.current-version {
  border: 2px solid #409eff;
  box-shadow: 0 2px 12px 0 rgba(64, 158, 255, 0.2);
}

.text-content {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: inherit;
  font-size: 12px;
  line-height: 1.5;
}

.version-changes {
  margin-top: 16px;
}

.version-changes ul {
  margin: 8px 0 0 0;
  padding-left: 20px;
}

.version-changes li {
  margin: 4px 0;
  font-size: 13px;
}

:deep(.el-timeline-item__timestamp) {
  font-size: 13px;
  color: #909399;
}

:deep(.el-card__header) {
  padding: 16px 20px;
  background-color: #fafafa;
}

:deep(.el-card__body) {
  padding: 20px;
}
</style>
