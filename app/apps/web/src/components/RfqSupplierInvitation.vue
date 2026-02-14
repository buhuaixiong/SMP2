<template>
  <div class="rfq-supplier-invitation">
    <div class="invitation-header">
      <h3>{{ t("rfq.invitation.selectSuppliers") }}</h3>
      <el-button type="primary" :icon="Search" @click="showSupplierDialog = true">
        {{ selectedSupplierIds.length > 0
            ? t("rfq.invitation.addSuppliers") + `(已选择${selectedSupplierIds.length}项)`
            : t("rfq.invitation.addSuppliers")
        }}
      </el-button>
    </div>

    <div v-if="selectedSuppliers.length === 0" class="empty-state">
      <el-empty :description="t('rfq.invitation.noSuppliersSelected')">
        <el-button type="primary" @click="showSupplierDialog = true">
          {{ t("rfq.invitation.selectNow") }}
        </el-button>
      </el-empty>
    </div>

    <div v-else class="selected-suppliers">
      <el-table :data="selectedSuppliers" style="width: 100%">
        <el-table-column prop="companyName" :label="t('supplier.companyName')" min-width="200" />
        <el-table-column prop="companyId" :label="t('supplier.companyId')" width="150" />
        <el-table-column prop="stage" :label="t('supplier.stage')" width="120">
          <template #default="{ row }">
            <el-tag :type="row.stage === 'temporary' ? 'warning' : 'success'" size="small">
              {{ t(`supplier.stages.${row.stage ?? "null"}`) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="category" :label="t('supplier.category')" width="150" />
        <el-table-column :label="t('common.actions')" width="100" align="center">
          <template #default="{ row }">
            <el-button
              type="danger"
              :icon="Delete"
              size="small"
              link
              @click="removeSupplier(row.id)"
            >
              {{ t("common.remove") }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="supplier-summary">
        <el-text type="info">
          {{ t("rfq.invitation.selectedCount", { count: selectedSuppliers.length }) }}
        </el-text>
      </div>
    </div>

    <!-- External Email Invitations Section -->
    <el-divider content-position="left">
      <span style="font-size: 16px; font-weight: 600">{{
        t("rfq.invitation.externalSuppliers")
      }}</span>
    </el-divider>

    <div class="external-invitation-section">
      <div class="external-form">
        <el-row :gutter="12">
          <el-col :span="8">
            <el-input
              v-model="externalEmailForm.email"
              :placeholder="t('rfq.invitation.emailPlaceholder')"
              clearable
            >
              <template #prepend>{{ t("rfq.invitation.email") }}</template>
            </el-input>
          </el-col>
          <el-col :span="6">
            <el-input
              v-model="externalEmailForm.companyName"
              :placeholder="t('rfq.invitation.companyNamePlaceholder')"
              clearable
            >
              <template #prepend>{{ t("rfq.invitation.companyName") }}</template>
            </el-input>
          </el-col>
          <el-col :span="6">
            <el-input
              v-model="externalEmailForm.contactPerson"
              :placeholder="t('rfq.invitation.contactPersonPlaceholder')"
              clearable
            >
              <template #prepend>{{ t("rfq.invitation.contactPerson") }}</template>
            </el-input>
          </el-col>
          <el-col :span="4">
            <el-button type="primary" @click="addExternalEmail" style="width: 100%">
              {{ t("rfq.invitation.addEmail") }}
            </el-button>
          </el-col>
        </el-row>
      </div>

      <div
        v-if="externalEmailList.length > 0"
        class="external-emails-table"
        style="margin-top: 16px"
      >
        <el-table :data="externalEmailList" style="width: 100%">
          <el-table-column prop="email" :label="t('rfq.invitation.email')" min-width="200" />
          <el-table-column
            prop="companyName"
            :label="t('rfq.invitation.companyName')"
            min-width="150"
          >
            <template #default="{ row }">
              <span v-if="row.companyName">{{ row.companyName }}</span>
              <el-text v-else type="info" size="small">-</el-text>
            </template>
          </el-table-column>
          <el-table-column
            prop="contactPerson"
            :label="t('rfq.invitation.contactPerson')"
            min-width="150"
          >
            <template #default="{ row }">
              <span v-if="row.contactPerson">{{ row.contactPerson }}</span>
              <el-text v-else type="info" size="small">-</el-text>
            </template>
          </el-table-column>
          <el-table-column :label="t('common.actions')" width="100" align="center">
            <template #default="{ row }">
              <el-button
                type="danger"
                :icon="Delete"
                size="small"
                link
                @click="removeExternalEmail(row.email)"
              >
                {{ t("common.remove") }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <div class="email-summary">
          <el-text type="info">
            {{ t("rfq.invitation.externalEmailCount", { count: externalEmailList.length }) }}
          </el-text>
        </div>
      </div>

      <div v-else class="external-empty-hint">
        <el-text type="info" size="small">
          {{ t("rfq.invitation.externalEmailHint") }}
        </el-text>
      </div>
    </div>

    <!-- Supplier Selection Dialog -->
    <el-dialog
      v-model="showSupplierDialog"
      :title="t('rfq.invitation.selectSuppliers')"
      width="900px"
      :close-on-click-modal="false"
    >
      <div class="supplier-dialog-content">
        <el-input
          v-model="searchKeyword"
          :placeholder="t('rfq.invitation.searchSuppliers')"
          :prefix-icon="Search"
          clearable
          style="margin-bottom: 16px"
        />

        <el-alert
          v-if="!searchKeyword && allSuppliers.length === 0"
          type="info"
          :closable="false"
          style="margin-bottom: 16px"
        >
          <template #default>
            {{ t("rfq.invitation.searchHint") }}
          </template>
        </el-alert>

        <el-table
          ref="tableRef"
          :data="allSuppliers"
          @selection-change="handleSelectionChange"
          max-height="400px"
          v-loading="isLoadingSuppliers"
          row-key="id"
        >
          <el-table-column type="selection" width="55" :reserve-selection="true" />
          <el-table-column prop="companyName" :label="t('supplier.companyName')" min-width="180" />
          <el-table-column prop="companyId" :label="t('supplier.companyId')" width="120" />
          <el-table-column prop="stage" :label="t('supplier.stage')" width="100">
            <template #default="{ row }">
              <el-tag :type="row.stage === 'temporary' ? 'warning' : 'success'" size="small">
                {{ t(`supplier.stages.${row.stage ?? "null"}`) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="category" :label="t('supplier.category')" width="120" />
          <el-table-column prop="region" :label="t('supplier.region')" width="100" />
        </el-table>

        <div v-if="allSuppliers.length > 0" class="search-result-info">
          <el-text type="info" size="small">
            {{ t("rfq.invitation.searchResultCount", { count: allSuppliers.length }) }}
          </el-text>
        </div>
      </div>

      <template #footer>
        <el-button @click="showSupplierDialog = false">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" @click="confirmSelection">
          {{ t("common.confirm") }} ({{ tempSelection.length }})
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted, watch, nextTick } from "vue";
import { useI18n } from "vue-i18n";
import { Search, Delete } from "@element-plus/icons-vue";

import type { Supplier } from "@/types";
import { listSuppliers } from "@/api/suppliers";


import { useNotification } from "@/composables";

const notification = useNotification();
const { t } = useI18n();

interface Props {
  modelValue: number[];
  externalEmails?: { email: string; companyName?: string; contactPerson?: string }[];
}

const props = defineProps<Props>();
const emit = defineEmits<{
  "update:modelValue": [value: number[]];
  "update:externalEmails": [
    value: { email: string; companyName?: string; contactPerson?: string }[],
  ];
}>();

const showSupplierDialog = ref(false);
const searchKeyword = ref("");
const allSuppliers = ref<Supplier[]>([]);
const tempSelection = ref<Supplier[]>([]);
const tableRef = ref();
const isLoadingSuppliers = ref(false);
const searchDebounceTimer = ref<number | null>(null);

// Cache for selected suppliers - keeps full supplier objects
const selectedSuppliersCache = ref<Map<number, Supplier>>(new Map());

// External email invitation state
const externalEmailForm = ref({
  email: "",
  companyName: "",
  contactPerson: "",
});

const externalEmailList = computed({
  get: () => props.externalEmails || [],
  set: (value) => emit("update:externalEmails", value),
});

const selectedSupplierIds = computed({
  get: () => {
    console.log('[selectedSupplierIds.get] props.modelValue:', props.modelValue);
    return props.modelValue;
  },
  set: (value) => {
    console.log('[selectedSupplierIds.set] Emitting update:modelValue with:', value);
    emit("update:modelValue", value);
    console.log('[selectedSupplierIds.set] After emit, props.modelValue:', props.modelValue);
  },
});

const selectedSuppliers = computed(() => {
  console.log('[selectedSuppliers] Computing...');
  console.log('[selectedSuppliers] selectedSupplierIds:', selectedSupplierIds.value);
  console.log('[selectedSuppliers] Cache size:', selectedSuppliersCache.value.size);
  console.log('[selectedSuppliers] Cache contents:', Array.from(selectedSuppliersCache.value.entries()));

  // Get suppliers from cache based on selected IDs
  const result = selectedSupplierIds.value
    .map((id) => selectedSuppliersCache.value.get(id))
    .filter((s): s is Supplier => s !== undefined);

  console.log('[selectedSuppliers] Result:', result);
  return result;
});

// Watch for search keyword changes and trigger server-side search
watch(searchKeyword, (newValue) => {
  // Only trigger search if dialog is open
  if (!showSupplierDialog.value) {
    return;
  }

  // Clear existing timer
  if (searchDebounceTimer.value !== null) {
    clearTimeout(searchDebounceTimer.value);
  }

  // Debounce search: wait 500ms after user stops typing
  searchDebounceTimer.value = window.setTimeout(() => {
    loadSuppliers(newValue);
  }, 500);
});

// Watch for dialog opening to reset search and reload suppliers
watch(showSupplierDialog, (isOpen) => {
  console.log('[showSupplierDialog watch] isOpen:', isOpen);
  console.log('[showSupplierDialog watch] selectedSupplierIds:', selectedSupplierIds.value);
  console.log('[showSupplierDialog watch] props.modelValue:', props.modelValue);
  console.log('[showSupplierDialog watch] Cache size:', selectedSuppliersCache.value.size);

  if (isOpen) {
    // Reset search keyword and load initial suppliers
    searchKeyword.value = "";
    // Don't clear allSuppliers - let loadSuppliers handle merging
    loadSuppliers();
  } else {
    // Clear timer when dialog closes
    if (searchDebounceTimer.value !== null) {
      clearTimeout(searchDebounceTimer.value);
      searchDebounceTimer.value = null;
    }
    // IMPORTANT: Reset tempSelection when dialog closes
    // This prevents stale selections from affecting the next dialog open
    console.log('[showSupplierDialog watch] Resetting tempSelection');
    tempSelection.value = [];
  }
});

// Watch for dialog opening and pre-select already selected suppliers in the table
watch([showSupplierDialog, allSuppliers], ([isOpen, suppliers]) => {
  if (isOpen && suppliers.length > 0 && tableRef.value) {
    // Use nextTick to ensure table is rendered
    nextTick(() => {
      if (!tableRef.value) return;

      // Clear all selections first
      tableRef.value.clearSelection();

      // Pre-select suppliers that are already in selectedSupplierIds
      suppliers.forEach((supplier: Supplier) => {
        if (selectedSupplierIds.value.includes(supplier.id)) {
          tableRef.value.toggleRowSelection(supplier, true);
        }
      });
    });
  }
});

onMounted(async () => {
  // Load initial suppliers (empty search, limited results)
  await loadSuppliers();
});

// Initialize cache if there are pre-selected supplier IDs (e.g., from editing an existing RFQ)
watch(
  () => props.modelValue,
  async (newIds, oldIds) => {
    // Only run on initial mount or when IDs are set externally
    if (oldIds === undefined && newIds.length > 0) {
      // Check if any selected IDs are not in cache
      const missingIds = newIds.filter((id) => !selectedSuppliersCache.value.has(id));

      if (missingIds.length > 0) {
        // Try to load these suppliers to populate the cache
        // This handles cases where the component is initialized with pre-selected IDs
        await loadSuppliers();
      }
    }
  },
  { immediate: true }
);

async function loadSuppliers(query = "") {
  try {
    isLoadingSuppliers.value = true;

    // Add forRfq parameter to allow purchasers to see all suppliers when creating RFQs
    // Server-side search by companyName or companyId
    const params: any = {
      forRfq: true,
      limit: 100, // Reasonable limit for search results
    };

    // Add search query if provided
    if (query && query.trim()) {
      params.q = query.trim();
    }

    const response = await listSuppliers(params);
    const searchResults = response.data || [];

    // Get currently selected suppliers from the cache (not from allSuppliers!)
    const currentSelectedIds = selectedSupplierIds.value;
    const previouslySelectedSuppliers = currentSelectedIds
      .map((id) => selectedSuppliersCache.value.get(id))
      .filter((s): s is Supplier => s !== undefined);

    // Merge previously selected suppliers with new search results
    const searchResultIds = new Set(searchResults.map((s: Supplier) => s.id));
    const uniquePreviouslySelected = previouslySelectedSuppliers.filter((s) =>
      !searchResultIds.has(s.id)
    );

    // Combine: previously selected first, then new search results
    allSuppliers.value = [...uniquePreviouslySelected, ...searchResults];

    // Update cache with any new supplier data from search results
    searchResults.forEach((supplier: Supplier) => {
      // Only update if this supplier is selected and not already cached
      if (currentSelectedIds.includes(supplier.id) && !selectedSuppliersCache.value.has(supplier.id)) {
        selectedSuppliersCache.value.set(supplier.id, supplier);
      }
    });
  } catch (error) {
    notification.error(t("rfq.invitation.loadSuppliersError"));
  } finally {
    isLoadingSuppliers.value = false;
  }
}

function handleSelectionChange(selection: Supplier[]) {
  tempSelection.value = selection;
}

function confirmSelection() {
  console.log('[confirmSelection] START');
  console.log('[confirmSelection] tempSelection:', tempSelection.value);
  console.log('[confirmSelection] selectedSupplierIds BEFORE:', selectedSupplierIds.value);

  const newIds = tempSelection.value.map((s) => s.id);

  // Update the cache with newly selected suppliers
  tempSelection.value.forEach((supplier) => {
    selectedSuppliersCache.value.set(supplier.id, supplier);
  });

  console.log('[confirmSelection] Cache after update:', Array.from(selectedSuppliersCache.value.entries()));

  const updatedIds = [...new Set([...selectedSupplierIds.value, ...newIds])];
  console.log('[confirmSelection] updatedIds:', updatedIds);

  selectedSupplierIds.value = updatedIds;

  console.log('[confirmSelection] selectedSupplierIds AFTER:', selectedSupplierIds.value);
  console.log('[confirmSelection] props.modelValue:', props.modelValue);

  showSupplierDialog.value = false;
  notification.success(t("rfq.invitation.suppliersAdded", { count: tempSelection.value.length }));
}

function removeSupplier(supplierId: number) {
  selectedSupplierIds.value = selectedSupplierIds.value.filter((id) => id !== supplierId);

  // Remove from cache as well
  selectedSuppliersCache.value.delete(supplierId);

  notification.success(t("rfq.invitation.supplierRemoved"));
}

function addExternalEmail() {
  const email = externalEmailForm.value.email.trim();
  if (!email) {
    notification.warning(t("rfq.invitation.emailRequired"));
    return;
  }

  // Basic email validation
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!emailRegex.test(email)) {
    notification.warning(t("rfq.invitation.emailInvalid"));
    return;
  }

  // Check for duplicates
  if (externalEmailList.value.some((e) => e.email.toLowerCase() === email.toLowerCase())) {
    notification.warning(t("rfq.invitation.emailDuplicate"));
    return;
  }

  externalEmailList.value = [
    ...externalEmailList.value,
    {
      email,
      companyName: externalEmailForm.value.companyName.trim() || undefined,
      contactPerson: externalEmailForm.value.contactPerson.trim() || undefined,
    },
  ];

  // Reset form
  externalEmailForm.value = {
    email: "",
    companyName: "",
    contactPerson: "",
  };

  notification.success(t("rfq.invitation.emailAdded"));
}

function removeExternalEmail(email: string) {
  externalEmailList.value = externalEmailList.value.filter((e) => e.email !== email);
  notification.success(t("rfq.invitation.emailRemoved"));
}




</script>

<style scoped>
.rfq-supplier-invitation {
  padding: 24px;
  background: white;
  border-radius: 8px;
}

.invitation-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.invitation-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.empty-state {
  padding: 40px 0;
}

.selected-suppliers {
  margin-top: 16px;
}

.supplier-summary {
  margin-top: 16px;
  padding: 12px;
  background: #f5f7fa;
  border-radius: 4px;
  text-align: center;
}

.supplier-dialog-content {
  padding: 0 4px;
}

.external-invitation-section {
  margin-top: 24px;
}

.external-form {
  padding: 16px;
  background: #f5f7fa;
  border-radius: 4px;
}

.email-summary {
  margin-top: 16px;
  padding: 12px;
  background: #f5f7fa;
  border-radius: 4px;
  text-align: center;
}

.external-empty-hint {
  padding: 16px;
  text-align: center;
  background: #fafafa;
  border-radius: 4px;
  border: 1px dashed #dcdfe6;
}
</style>

