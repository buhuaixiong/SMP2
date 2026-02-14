<template>
  <div class="file-info" :class="{ empty: !file }">
    <template v-if="file">
      <div class="file-header">
        <span class="label">{{ label }}</span>
        <slot name="actions" :file="file" />
      </div>
      <a
        class="file-link"
        :href="file.downloadUrl"
        target="_blank"
        rel="noopener"
      >
        {{ file.originalName }}
      </a>
      <p class="file-meta">
        <span>{{ updatedAtLabel }} {{ formatDate(file.uploadedAt) }}</span>
        <span v-if="file.uploadedBy">
          &bull;
          <template v-if="uploadedByLabel">{{ uploadedByLabel }} </template>{{ file.uploadedBy }}
        </span>
        <span v-if="file.fileSize" class="size">
          &bull; {{ formatSize(file.fileSize) }}
        </span>
      </p>
    </template>
    <p v-else class="empty-text">{{ emptyText }}</p>
  </div>
</template>

<script setup lang="ts">
import { toRef, toRefs } from "vue";

import type { TemplateFileRecord } from "@/types";
import { formatDateTime, formatFileSize } from "@/utils/formatting";

defineOptions({ name: "TemplateFileInfo" });

const props = withDefaults(
  defineProps<{
    file?: TemplateFileRecord | null;
    label?: string;
    emptyText?: string;
    updatedAtLabel?: string;
    uploadedByLabel?: string;
    formatDateFn?: (value: string | null | undefined) => string;
    formatSizeFn?: (value: number | null | undefined) => string;
  }>(),
  {
    file: null,
    label: "",
    emptyText: "-",
    updatedAtLabel: "",
    uploadedByLabel: "",
    formatDateFn: undefined,
    formatSizeFn: undefined,
  },
);

const { label, emptyText, updatedAtLabel, uploadedByLabel } = toRefs(props);
const file = toRef(props, "file");

const formatDate = (value: string | null | undefined) =>
  (props.formatDateFn ?? formatDateTime)(value);

const formatSize = (value: number | null | undefined) => {
  if (value == null) {
    return "-";
  }
  return (props.formatSizeFn ?? formatFileSize)(value);
};
</script>

<style scoped>
.file-info {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 14px;
  color: #4a4f64;
}

.file-info.empty {
  color: #a0a3b8;
}

.file-header {
  display: inline-flex;
  gap: 8px;
  align-items: center;
  color: #7f84a1;
  font-size: 12px;
}

.file-link {
  color: #2f3aa8;
  text-decoration: none;
  font-weight: 500;
}

.file-link:hover {
  text-decoration: underline;
}

.file-meta {
  margin: 0;
  display: inline-flex;
  gap: 6px;
  color: #777d99;
  font-size: 13px;
}

.size {
  color: #7f84a1;
}

.empty-text {
  margin: 0;
  font-size: 14px;
}
</style>
