<template>
  <section class="supplier-list">
    <header class="list-toolbar">
      <div class="results-count">
        <span v-if="totalItems > 0">
          {{ t("table.showing", { start: pageStart, end: pageEnd, total: totalItems }) }}
        </span>
        <span v-else>
          {{ t("table.noMatch") }}
        </span>
        <span v-if="selectedSupplierIds.length > 0" class="selection-count">
          · {{ selectedSupplierIds.length }} selected
        </span>
      </div>
      <div v-if="totalItems" class="pagination-bar">
        <label>
          {{ t("table.perPage") }}
          <select :value="pageSize" @change="onPageSize">
            <option v-for="option in pageSizeOptions" :key="option" :value="option">
              {{ option }}
            </option>
          </select>
        </label>
        <div class="pager-buttons">
          <button type="button" :disabled="!canGoPrevious" @click="$emit('previous-page')">
            {{ t("common.previous") }}
          </button>
          <span>{{ t("table.page", { current: currentPage, total: totalPages }) }}</span>
          <button type="button" :disabled="!canGoNext" @click="$emit('next-page')">
            {{ t("common.next") }}
          </button>
        </div>
      </div>
    </header>

    <div v-if="loading" class="panel">{{ t("table.loading") }}</div>
    <div v-else-if="!suppliers.length" class="panel empty-state">{{ t("table.noData") }}</div>
    <div v-else class="panel">
      <table class="supplier-table">
        <thead>
          <tr>
            <th class="checkbox-column">
              <input
                type="checkbox"
                :checked="isAllSelected"
                :indeterminate="isSomeSelected"
                @change="toggleSelectAll"
                title="Select all on this page"
              />
            </th>
            <th>{{ t("table.columns.company") }}</th>
            <th>{{ t("table.columns.category") }}</th>
            <th>{{ t("table.columns.region") }}</th>
            <th>{{ t("table.columns.importance") }}</th>
            <th>{{ t("table.columns.completion") }}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <template v-for="supplier in suppliers" :key="supplier.id">
            <tr
              :class="{
                active: supplier.id === selectedSupplierId,
                selected: isSupplierSelected(supplier.id),
              }"
            >
              <td class="checkbox-column">
                <input
                  type="checkbox"
                  :checked="isSupplierSelected(supplier.id)"
                  @change="toggleSupplierSelection(supplier.id)"
                />
              </td>
              <td>
                <div class="company-cell">
                  <span class="company-name">{{ supplier.companyName }}</span>
                  <span class="company-meta">{{ supplier.companyId }}</span>
                  <span class="company-meta">
                    {{ supplier.contactPerson }} · {{ supplier.contactEmail }}
                  </span>
                </div>
              </td>
              <td>{{ supplier.category || "–" }}</td>
              <td>{{ supplier.region || "–" }}</td>
              <td>{{ supplier.importance || "–" }}</td>
              <td class="completion-cell">
                <div class="completion-summary" :title="completionTooltip(supplier)">
                  <span class="completion-score"
                    >{{ Math.round(supplier.completionScore ?? 0) }}%</span
                  >
                  <span class="completion-pill" :class="completionPillClass(supplier)">
                    {{ completionLabel(supplier) }}
                  </span>
                </div>
              </td>
              <td class="actions-cell">
                <button class="icon-btn" type="button" @click="$emit('toggle-expand', supplier.id)">
                  {{
                    expandedSupplierId === supplier.id
                      ? t("table.hideDetails")
                      : t("table.showDetails")
                  }}
                </button>
                <button class="link-btn" type="button" @click="$emit('select', supplier.id)">
                  {{ t("common.view") }}
                </button>
              </td>
            </tr>
            <tr v-if="expandedSupplierId === supplier.id" class="detail-row">
              <td colspan="7">
                <slot name="details" :supplier="supplier" />
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useSupplierCompletion } from "@/composables/useSupplierCompletion";
import type { Supplier } from "@/types";

const { t } = useI18n();

const props = defineProps<{
  suppliers: Supplier[];
  loading: boolean;
  pageStart: number;
  pageEnd: number;
  totalItems: number;
  pageSize: number;
  pageSizeOptions: number[];
  currentPage: number;
  totalPages: number;
  canGoPrevious: boolean;
  canGoNext: boolean;
  selectedSupplierId: number | null;
  expandedSupplierId: number | null;
  selectedSupplierIds?: number[];
}>();

const emit = defineEmits<{
  (event: "update:pageSize", value: number): void;
  (event: "previous-page"): void;
  (event: "next-page"): void;
  (event: "select", id: number): void;
  (event: "toggle-expand", id: number): void;
  (event: "update:selectedSupplierIds", ids: number[]): void;
}>();

const { completionLabel, completionPillClass, completionTooltip } = useSupplierCompletion();

const selectedSupplierIds = computed(() => props.selectedSupplierIds || []);

const isSupplierSelected = (supplierId: number) => {
  return selectedSupplierIds.value.includes(supplierId);
};

const isAllSelected = computed(() => {
  if (props.suppliers.length === 0) return false;
  return props.suppliers.every((s) => isSupplierSelected(s.id));
});

const isSomeSelected = computed(() => {
  const selected = props.suppliers.filter((s) => isSupplierSelected(s.id)).length;
  return selected > 0 && selected < props.suppliers.length;
});

const toggleSupplierSelection = (supplierId: number) => {
  const newSelection = isSupplierSelected(supplierId)
    ? selectedSupplierIds.value.filter((id) => id !== supplierId)
    : [...selectedSupplierIds.value, supplierId];
  emit("update:selectedSupplierIds", newSelection);
};

const toggleSelectAll = () => {
  if (isAllSelected.value) {
    // Deselect all on this page
    const pageIds = props.suppliers.map((s) => s.id);
    const newSelection = selectedSupplierIds.value.filter((id) => !pageIds.includes(id));
    emit("update:selectedSupplierIds", newSelection);
  } else {
    // Select all on this page
    const pageIds = props.suppliers.map((s) => s.id);
    const newSelection = [...new Set([...selectedSupplierIds.value, ...pageIds])];
    emit("update:selectedSupplierIds", newSelection);
  }
};

const onPageSize = (event: Event) => {
  const value = Number((event.target as HTMLSelectElement).value);
  emit("update:pageSize", value);
};
</script>

<style scoped>
.supplier-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.list-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.results-count {
  font-size: 0.95rem;
  color: #374151;
}

.pagination-bar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.pagination-bar select {
  padding: 0.35rem 0.6rem;
  border-radius: 6px;
  border: 1px solid #d1d5db;
}

.pager-buttons {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.pager-buttons button {
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 0.4rem 0.8rem;
  background: #ffffff;
  cursor: pointer;
}

.pager-buttons button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.panel {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  background: #ffffff;
  padding: 1rem;
}

.empty-state {
  color: #6b7280;
  text-align: center;
}

.supplier-table {
  width: 100%;
  border-collapse: collapse;
}

.supplier-table thead {
  background: #f9fafb;
}

.supplier-table th,
.supplier-table td {
  padding: 0.75rem;
  text-align: left;
  border-bottom: 1px solid #e5e7eb;
}

.supplier-table tr.active {
  background: #f5f5ff;
}

.supplier-table tr.selected {
  background: #eff6ff;
}

.supplier-table tr.selected.active {
  background: #dbeafe;
}

.checkbox-column {
  width: 40px;
  text-align: center;
}

.checkbox-column input[type="checkbox"] {
  cursor: pointer;
  width: 16px;
  height: 16px;
}

.selection-count {
  color: #2563eb;
  font-weight: 600;
}

.company-cell {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.company-name {
  font-weight: 600;
  color: #111827;
}

.company-meta {
  font-size: 0.85rem;
  color: #6b7280;
}

.completion-cell {
  min-width: 160px;
}

.completion-summary {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
}

.completion-score {
  font-weight: 600;
}

.completion-pill {
  display: inline-flex;
  align-items: center;
  padding: 0.15rem 0.6rem;
  border-radius: 999px;
  font-size: 0.8rem;
  text-transform: capitalize;
}

.completion-pill.pill-complete {
  background: #dcfce7;
  color: #166534;
}

.completion-pill.pill-mostly {
  background: #fef08a;
  color: #92400e;
}

.completion-pill.pill-attention {
  background: #fee2e2;
  color: #b91c1c;
}

.actions-cell {
  display: flex;
  gap: 0.5rem;
  justify-content: flex-end;
}

.icon-btn {
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: #ffffff;
  padding: 0.4rem 0.8rem;
  cursor: pointer;
}

.icon-btn:hover {
  background: #f3f4f6;
}

.link-btn {
  background: none;
  border: none;
  color: #4f46e5;
  cursor: pointer;
}

.detail-row td {
  background: #f9fafb;
}
</style>
