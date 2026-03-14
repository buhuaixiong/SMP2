<template>
  <div class="item-master-page">
    <PageHeader
      title="Item Master"
      subtitle="Import, query, and track material master records."
    />

    <section v-if="canImport" class="panel">
      <header class="panel-header">
        <h3>Import Excel</h3>
      </header>
      <div class="import-row">
        <input type="file" accept=".xlsx,.xlsm" @change="onFileChange" />
        <el-input
          v-model="sheetInput"
          placeholder="Optional sheets, comma-separated (e.g. HZ,TH)"
          clearable
        />
        <el-button type="primary" :loading="importing" :disabled="!fileToImport" @click="submitImport">
          Import
        </el-button>
      </div>
      <p class="help">
        If sheet list is empty, all worksheets are imported by default.
      </p>
    </section>

    <section class="panel">
      <header class="panel-header">
        <h3>Records</h3>
      </header>
      <div class="filters">
        <el-input v-model="filters.fac" placeholder="Fac" clearable />
        <el-input v-model="filters.itemNumber" placeholder="Item Number" clearable />
        <el-input v-model="filters.vendor" placeholder="Vendor" clearable />
        <el-input v-model="filters.sourcingName" placeholder="Sourcing Name" clearable />
        <el-checkbox v-model="filters.unassignedOnly">Unassigned only</el-checkbox>
        <el-button type="primary" :loading="loadingRecords" @click="searchRecords">Search</el-button>
        <el-button @click="resetFilters">Reset</el-button>
      </div>

      <el-table :data="records" border stripe v-loading="loadingRecords">
        <el-table-column prop="fac" label="Fac" min-width="100" />
        <el-table-column prop="itemNumber" label="Item Number" min-width="140" />
        <el-table-column prop="vendor" label="Vendor" min-width="120" />
        <el-table-column prop="sourcingName" label="Sourcing Name" min-width="150" />
        <el-table-column prop="ownerUserId" label="Owner User ID" min-width="150" />
        <el-table-column prop="itemDescription" label="Description" min-width="180" />
        <el-table-column prop="currency" label="Currency" width="100" />
        <el-table-column prop="priceBreak1" label="Price" width="110" />
        <el-table-column prop="updatedAt" label="Updated At" min-width="180" />
      </el-table>

      <div class="pager">
        <el-pagination
          background
          layout="prev, pager, next, total"
          :current-page="recordPage"
          :page-size="recordLimit"
          :total="recordTotal"
          @current-change="changeRecordPage"
        />
      </div>
    </section>

    <section v-if="canViewAll" class="panel">
      <header class="panel-header">
        <h3>Import Batches</h3>
      </header>
      <el-table :data="batches" border stripe v-loading="loadingBatches">
        <el-table-column prop="id" label="Batch ID" width="100" />
        <el-table-column prop="fileName" label="File Name" min-width="220" />
        <el-table-column prop="sheetScope" label="Sheets" min-width="130" />
        <el-table-column prop="status" label="Status" width="130" />
        <el-table-column prop="insertedCount" label="Inserted" width="100" />
        <el-table-column prop="updatedCount" label="Updated" width="100" />
        <el-table-column prop="warningCount" label="Warnings" width="100" />
        <el-table-column prop="errorCount" label="Errors" width="90" />
        <el-table-column prop="startedAt" label="Started At" min-width="180" />
        <el-table-column prop="finishedAt" label="Finished At" min-width="180" />
      </el-table>
      <div class="pager">
        <el-pagination
          background
          layout="prev, pager, next, total"
          :current-page="batchPage"
          :page-size="batchLimit"
          :total="batchTotal"
          @current-change="changeBatchPage"
        />
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import PageHeader from "@/components/layout/PageHeader.vue";
import { useNotification } from "@/composables";
import { useAuthStore } from "@/stores/auth";
import {
  importItemMaster,
  listItemMasterImportBatches,
  listItemMasterRecords,
  type ItemMasterImportBatch,
  type ItemMasterRecord,
} from "@/api/itemMaster";

defineOptions({ name: "ItemMasterView" });

const notification = useNotification();
const authStore = useAuthStore();

const canImport = computed(() => authStore.hasPermission("item_master.import.manage"));
const canViewAll = computed(() => authStore.hasPermission("item_master.view.all"));

const fileToImport = ref<File | null>(null);
const sheetInput = ref("");
const importing = ref(false);

const filters = reactive({
  fac: "",
  itemNumber: "",
  vendor: "",
  sourcingName: "",
  unassignedOnly: false,
});

const records = ref<ItemMasterRecord[]>([]);
const loadingRecords = ref(false);
const recordPage = ref(1);
const recordLimit = ref(20);
const recordTotal = ref(0);

const batches = ref<ItemMasterImportBatch[]>([]);
const loadingBatches = ref(false);
const batchPage = ref(1);
const batchLimit = ref(20);
const batchTotal = ref(0);

const onFileChange = (event: Event) => {
  const input = event.target as HTMLInputElement;
  fileToImport.value = input.files?.[0] ?? null;
  input.value = "";
};

const loadRecords = async () => {
  loadingRecords.value = true;
  try {
    const response = await listItemMasterRecords({
      fac: filters.fac.trim() || undefined,
      itemNumber: filters.itemNumber.trim() || undefined,
      vendor: filters.vendor.trim() || undefined,
      sourcingName: filters.sourcingName.trim() || undefined,
      unassignedOnly: filters.unassignedOnly || undefined,
      page: recordPage.value,
      limit: recordLimit.value,
    });
    records.value = response.data ?? [];
    recordTotal.value = response.pagination?.total ?? 0;
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : "Failed to load item master records.";
    notification.error(message);
  } finally {
    loadingRecords.value = false;
  }
};

const loadBatches = async () => {
  if (!canViewAll.value) {
    return;
  }
  loadingBatches.value = true;
  try {
    const response = await listItemMasterImportBatches(batchPage.value, batchLimit.value);
    batches.value = response.data ?? [];
    batchTotal.value = response.pagination?.total ?? 0;
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : "Failed to load import batches.";
    notification.error(message);
  } finally {
    loadingBatches.value = false;
  }
};

const submitImport = async () => {
  if (!fileToImport.value) {
    notification.warning("Please choose an Excel file first.");
    return;
  }

  importing.value = true;
  try {
    const sheets = sheetInput.value
      .split(",")
      .map((value) => value.trim())
      .filter((value) => value.length > 0);
    const result = await importItemMaster(fileToImport.value, sheets);
    const summary = `Inserted ${result.insertedCount}, updated ${result.updatedCount}, warnings ${result.warningCount}, errors ${result.errorCount}.`;
    if (result.status === "failed") {
      notification.error(result.fatalMessage || `Import failed. ${summary}`);
    } else {
      notification.success(`Import finished. ${summary}`);
    }

    fileToImport.value = null;
    sheetInput.value = "";
    await loadRecords();
    await loadBatches();
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : "Failed to import item master.";
    notification.error(message);
  } finally {
    importing.value = false;
  }
};

const searchRecords = async () => {
  recordPage.value = 1;
  await loadRecords();
};

const resetFilters = async () => {
  filters.fac = "";
  filters.itemNumber = "";
  filters.vendor = "";
  filters.sourcingName = "";
  filters.unassignedOnly = false;
  recordPage.value = 1;
  await loadRecords();
};

const changeRecordPage = async (page: number) => {
  recordPage.value = page;
  await loadRecords();
};

const changeBatchPage = async (page: number) => {
  batchPage.value = page;
  await loadBatches();
};

onMounted(async () => {
  await loadRecords();
  await loadBatches();
});
</script>

<style scoped>
.item-master-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 24px;
}

.panel {
  background: #fff;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 16px;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.panel-header h3 {
  margin: 0;
  font-size: 18px;
}

.import-row {
  display: grid;
  grid-template-columns: minmax(180px, auto) 1fr auto;
  gap: 12px;
  align-items: center;
}

.filters {
  display: grid;
  grid-template-columns: repeat(4, minmax(120px, 1fr)) auto auto auto;
  gap: 12px;
  margin-bottom: 12px;
}

.pager {
  margin-top: 12px;
  display: flex;
  justify-content: flex-end;
}

.help {
  margin-top: 8px;
  margin-bottom: 0;
  color: #6b7280;
  font-size: 13px;
}

@media (max-width: 1200px) {
  .filters {
    grid-template-columns: repeat(2, minmax(120px, 1fr));
  }

  .import-row {
    grid-template-columns: 1fr;
  }
}
</style>
