<template>
  <div class="history-panel">
    <p v-if="loading" class="history-loading">{{ loadingText }}</p>
    <ul v-else class="history-list">
      <li v-for="record in records" :key="record.id">
        <button
          type="button"
          class="history-link"
          @click="downloadRecord(record.downloadUrl, record.originalName)"
        >
          {{ record.originalName }}
        </button>
        <span>{{ formatDate(record.uploadedAt) }}</span>
        <span v-if="record.uploadedBy">{{ record.uploadedBy }}</span>
        <span v-if="record.fileSize" class="size">{{ formatSize(record.fileSize) }}</span>
        <span v-if="record.isActive" class="badge">{{ activeLabel }}</span>
      </li>
      <li v-if="!records.length">{{ emptyText }}</li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { toRefs } from "vue";

import type { TemplateFileRecord } from "@/types";
import { formatDateTime, formatFileSize } from "@/utils/formatting";
import { downloadFile } from "@/utils/fileDownload";

defineOptions({ name: "TemplateHistoryList" });

const props = withDefaults(
  defineProps<{
    records: TemplateFileRecord[];
    loading?: boolean;
    loadingText?: string;
    emptyText?: string;
    activeLabel?: string;
  }>(),
  {
    records: () => [],
    loading: false,
    loadingText: "Loading…",
    emptyText: "No history available",
    activeLabel: "Active",
  },
);

const formatDate = (value: string | null | undefined) => formatDateTime(value);
const formatSize = (value: number | null | undefined) =>
  value ? formatFileSize(value) : "-";

const downloadRecord = async (url?: string | null, name?: string | null) => {
  if (!url) {
    return;
  }

  try {
    await downloadFile(url, name || "template-file");
  } catch (error) {
    console.error("[TemplateHistoryList] Failed to download file", error);
  }
};

const { loadingText, emptyText, activeLabel, loading, records } = toRefs(props);
</script>

<style scoped>
.history-panel {
  background: #f7f8ff;
  border-radius: 12px;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.history-loading {
  margin: 0;
  color: #6a6f8f;
  font-size: 13px;
}

.history-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 13px;
}

.history-list li {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: baseline;
  color: #4a4f64;
}

.history-link {
  background: transparent;
  border: none;
  padding: 0;
  color: #2f3aa8;
  text-decoration: none;
  cursor: pointer;
  font: inherit;
}

.history-link:hover {
  text-decoration: underline;
}

.size {
  color: #7f84a1;
}

.badge {
  background: #e6f7ed;
  color: #1f8a4d;
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 11px;
}
</style>
