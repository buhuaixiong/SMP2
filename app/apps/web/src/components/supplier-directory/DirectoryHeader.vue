<template>
  <div class="directory-header">
    <div class="title-row">
      <slot name="title">
        <h1 class="view-title">Supplier management</h1>
      </slot>
      <div class="primary-actions">
        <slot name="primary-actions" />
      </div>
    </div>

    <div class="search-row">
      <label class="search-field">
        <span class="sr-only">Search suppliers</span>
        <input
          :value="search"
          type="search"
          placeholder="Search by company, contact, or registration"
          @input="onSearch"
        />
      </label>

      <label class="status-field">
        <span>Status</span>
        <select :value="status" @change="onStatus">
          <option value="">All</option>
          <option v-for="option in statusOptions" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
      </label>

      <button class="advanced-toggle" type="button" @click="$emit('toggle-advanced')">
        Advanced filters
        <span v-if="advancedCount > 0" class="badge">{{ advancedCount }}</span>
      </button>

      <div class="quick-filter-icons" role="group" aria-label="Quick filters">
        <button
          class="icon-filter"
          :class="{ active: needsAttentionActive }"
          type="button"
          title="Needs attention"
          @click="$emit('toggle-needs-attention')"
        >
          ⚠️
        </button>
        <button
          v-for="doc in documentOptions"
          :key="doc.type"
          class="icon-filter"
          :class="{ active: selectedMissingDocuments.includes(doc.type) }"
          type="button"
          :title="`Missing ${doc.label}`"
          @click="$emit('toggle-missing-document', doc.type)"
        >
          📄
        </button>
        <button
          v-if="canClearQuickFilters"
          class="icon-filter clear"
          type="button"
          title="Clear quick filters"
          @click="$emit('clear-quick-filters')"
        >
          ✖️
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
interface StatusOption {
  value: string;
  label: string;
}

interface DocumentOption {
  type: string;
  label: string;
}

const props = withDefaults(
  defineProps<{
    search: string;
    status: string;
    statusOptions: StatusOption[];
    needsAttentionActive?: boolean;
    selectedMissingDocuments?: string[];
    documentOptions?: DocumentOption[];
    advancedCount?: number;
    canClearQuickFilters?: boolean;
  }>(),
  {
    needsAttentionActive: false,
    selectedMissingDocuments: () => [],
    documentOptions: () => [],
    advancedCount: 0,
    canClearQuickFilters: false,
  },
);

const emit = defineEmits<{
  (event: "update:search", value: string): void;
  (event: "update:status", value: string): void;
  (event: "toggle-advanced"): void;
  (event: "toggle-needs-attention"): void;
  (event: "toggle-missing-document", code: string): void;
  (event: "clear-quick-filters"): void;
}>();

const onSearch = (event: Event) => {
  const target = event.target as HTMLInputElement;
  emit("update:search", target.value);
};

const onStatus = (event: Event) => {
  const target = event.target as HTMLSelectElement;
  emit("update:status", target.value);
};
</script>

<style scoped>
.directory-header {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.title-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.view-title {
  margin: 0;
  font-size: 1.8rem;
  font-weight: 600;
}

.search-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
}

.search-field {
  flex: 1 1 260px;
}

.search-field input {
  width: 100%;
  padding: 0.55rem 0.75rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font-size: 0.95rem;
}

.status-field {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  font-size: 0.85rem;
  color: #374151;
}

.status-field select {
  min-width: 160px;
  padding: 0.5rem 0.75rem;
  border: 1px solid #d1d5db;
  border-radius: 8px;
}

.advanced-toggle {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  border: 1px solid #d1d5db;
  background: #ffffff;
  color: #374151;
  border-radius: 8px;
  padding: 0.5rem 0.85rem;
  cursor: pointer;
  transition: background 0.2s ease;
}

.advanced-toggle:hover {
  background: #f9fafb;
}

.badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 1.5rem;
  height: 1.5rem;
  padding: 0 0.5rem;
  background: #4f46e5;
  color: #ffffff;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 600;
}

.quick-filter-icons {
  display: inline-flex;
  gap: 0.5rem;
  align-items: center;
}

.icon-filter {
  width: 2.25rem;
  height: 2.25rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #d1d5db;
  border-radius: 0.75rem;
  background: #ffffff;
  cursor: pointer;
  transition:
    background 0.2s ease,
    border-color 0.2s ease;
}

.icon-filter:hover {
  background: #f3f4f6;
}

.icon-filter.active {
  background: #eef2ff;
  border-color: #6366f1;
}

.icon-filter.clear {
  border-color: #f87171;
  color: #b91c1c;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
</style>
