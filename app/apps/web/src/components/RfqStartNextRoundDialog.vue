<template>
  <el-dialog
    v-model="dialogVisible"
    :title="t('rfq.rounds.startNextDialogTitle')"
    width="960px"
    :close-on-click-modal="false"
  >
    <div class="dialog-body">
      <el-alert
        type="warning"
        :closable="false"
        :title="t('rfq.rounds.startNextDialogHint')"
        class="dialog-alert"
      />

      <el-descriptions v-if="currentRound" :column="4" border class="round-summary">
        <el-descriptions-item :label="t('rfq.rounds.roundNumber')">
          {{ t('rfq.rounds.roundLabel', { number: currentRound.roundNumber }) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.status')">
          <el-tag type="info">{{ translateStatus(currentRound.status) }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.deadline')">
          {{ formatDateTime(currentRound.bidDeadline) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.rounds.submittedCount')">
          {{ currentRound.submittedCount ?? 0 }} / {{ currentRound.invitedCount ?? 0 }}
        </el-descriptions-item>
      </el-descriptions>

      <el-form label-position="top" class="form-grid">
        <el-form-item :label="t('rfq.rounds.nextRoundDeadline')" required>
          <el-date-picker
            v-model="deadlineValue"
            type="datetime"
            value-format="YYYY-MM-DDTHH:mm:ss[Z]"
            :placeholder="t('rfq.rounds.nextRoundDeadlinePlaceholder')"
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

      <div class="supplier-section">
        <div class="supplier-section-header">
          <div class="section-title">{{ t('rfq.rounds.reselectSuppliers') }}</div>
          <el-input
            v-model="searchKeyword"
            :placeholder="t('rfq.invitation.searchSuppliers')"
            clearable
            class="supplier-search"
          />
        </div>

        <el-table
          ref="tableRef"
          :data="suppliers"
          row-key="id"
          max-height="360px"
          v-loading="loadingSuppliers"
          @selection-change="handleSelectionChange"
        >
          <el-table-column type="selection" width="55" :reserve-selection="true" />
          <el-table-column prop="companyName" :label="t('supplier.companyName')" min-width="200" />
          <el-table-column prop="companyId" :label="t('supplier.companyId')" width="140" />
          <el-table-column prop="stage" :label="t('supplier.stage')" width="120">
            <template #default="{ row }">
              <el-tag :type="row.stage === 'temporary' ? 'warning' : 'success'" size="small">
                {{ t(`supplier.stages.${row.stage ?? 'null'}`) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="category" :label="t('supplier.category')" width="140" />
        </el-table>

        <div class="selection-summary">
          <el-text type="info">
            {{ t('rfq.rounds.selectedSuppliersCount', { count: selectedSupplierIds.length }) }}
          </el-text>
        </div>
      </div>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="dialogVisible = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ t('rfq.rounds.startNextRound') }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { nextTick, ref, watch } from "vue";
import { ElMessage } from "element-plus";
import { useI18n } from "vue-i18n";
import { startNextRfqBidRound } from "@/api/rfq";
import { listSuppliers } from "@/api/suppliers";
import type { RfqBidRoundSummary, Supplier } from "@/types";
import { useNotification } from "@/composables";
import { extractErrorMessage } from "@/utils/errorHandling";

const props = withDefaults(
  defineProps<{
    visible: boolean;
    rfqId?: number | null;
    currentRound?: RfqBidRoundSummary | null;
    initialSupplierIds?: number[];
    formatDateTime: (value?: string | null) => string;
  }>(),
  {
    rfqId: null,
    currentRound: null,
    initialSupplierIds: () => [],
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
const loadingSuppliers = ref(false);
const deadlineValue = ref("");
const reason = ref("");
const searchKeyword = ref("");
const suppliers = ref<Supplier[]>([]);
const selectedSupplierIds = ref<number[]>([]);
const tableRef = ref<any>();
let searchTimer: ReturnType<typeof setTimeout> | null = null;

watch(
  () => props.visible,
  async (value) => {
    dialogVisible.value = value;
    if (value) {
      deadlineValue.value = "";
      reason.value = "";
      selectedSupplierIds.value = [...props.initialSupplierIds];
      await loadSuppliers();
    }
  },
  { immediate: true },
);

watch(dialogVisible, (value) => emit("update:visible", value));

watch(searchKeyword, (value) => {
  if (!dialogVisible.value) {
    return;
  }
  if (searchTimer) {
    clearTimeout(searchTimer);
  }
  searchTimer = setTimeout(() => {
    void loadSuppliers(value);
  }, 350);
});

async function loadSuppliers(keyword = "") {
  loadingSuppliers.value = true;
  try {
    const response = await listSuppliers({ q: keyword || undefined, limit: 50 });
    suppliers.value = response.data;
    await nextTick();
    restoreSelection();
  } catch (error) {
    notification.error(extractErrorMessage(error));
  } finally {
    loadingSuppliers.value = false;
  }
}

function restoreSelection() {
  if (!tableRef.value) {
    return;
  }
  tableRef.value.clearSelection();
  suppliers.value.forEach((supplier) => {
    if (selectedSupplierIds.value.includes(supplier.id)) {
      tableRef.value?.toggleRowSelection(supplier, true);
    }
  });
}

function handleSelectionChange(selection: Supplier[]) {
  selectedSupplierIds.value = selection.map((item) => item.id);
}

function translateStatus(status?: string | null) {
  const key = String(status ?? "").trim();
  return key ? t(`rfq.status.${key}`) : "-";
}

async function handleSubmit() {
  if (!props.rfqId) {
    return;
  }
  if (!deadlineValue.value) {
    ElMessage.warning(t("rfq.rounds.nextRoundDeadlineRequired"));
    return;
  }
  if (selectedSupplierIds.value.length === 0) {
    ElMessage.warning(t("rfq.rounds.selectSuppliersRequired"));
    return;
  }

  submitting.value = true;
  try {
    await startNextRfqBidRound(props.rfqId, {
      deadline: deadlineValue.value,
      reason: reason.value.trim() || null,
      supplierIds: selectedSupplierIds.value,
    });
    notification.success(t("rfq.rounds.startNextSuccess"));
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
.dialog-alert,
.round-summary {
  margin-bottom: 16px;
}

.supplier-section {
  margin-top: 8px;
}

.supplier-section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  margin-bottom: 12px;
}

.section-title {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.supplier-search {
  max-width: 320px;
}

.selection-summary {
  margin-top: 12px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

@media (max-width: 768px) {
  .supplier-section-header {
    flex-direction: column;
    align-items: stretch;
  }

  .supplier-search {
    max-width: none;
  }
}
</style>
