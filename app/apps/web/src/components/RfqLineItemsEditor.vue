<template>
  <div class="rfq-line-items-editor">
    <div class="editor-header">
      <h3>{{ t("rfq.lineItems.title") }}</h3>
      <el-button type="primary" :icon="Plus" @click="addLineItem">
        {{ t("rfq.lineItems.addItem") }}
      </el-button>
    </div>

    <el-alert
      v-if="lineItems.length === 0"
      :title="t('rfq.lineItems.emptyHint')"
      type="info"
      :closable="false"
      show-icon
      style="margin-bottom: 16px"
    />

    <el-table v-else :data="lineItems" border stripe style="width: 100%" max-height="500px">
      <el-table-column type="index" :label="t('rfq.items.lineNumber')" width="70" align="center" />

      <el-table-column :label="t('rfq.lineItems.materialCategory')" width="120">
        <template #default="{ row }">
          <el-select
            v-model="row.materialCategory"
            :placeholder="t('rfq.lineItems.selectCategory')"
            size="small"
            @change="onLineItemChange"
          >
            <el-option value="equipment" :label="t('rfq.distributionCategory.equipment')" />
            <el-option value="consumables" :label="t('rfq.lineItems.consumables')" />
            <el-option value="hardware" :label="t('rfq.distributionCategory.hardware')" />
            <el-option value="other" :label="t('common.other')" />
          </el-select>
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.lineItems.brand')" width="120">
        <template #default="{ row }">
          <el-input
            v-model="row.brand"
            :placeholder="t('rfq.lineItems.brandPlaceholder')"
            size="small"
            @input="onLineItemChange"
          />
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.items.itemName')" min-width="150">
        <template #default="{ row }">
          <el-input
            v-model="row.itemName"
            :placeholder="t('rfq.lineItems.itemNameRequired')"
            size="small"
            @input="onLineItemChange"
          />
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.lineItems.specifications')" min-width="150">
        <template #default="{ row }">
          <el-input
            v-model="row.specifications"
            :placeholder="t('rfq.lineItems.specificationsPlaceholder')"
            size="small"
            @input="onLineItemChange"
          />
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.items.quantity')" width="100">
        <template #default="{ row }">
          <el-input-number
            v-model="row.quantity"
            :min="0.01"
            :precision="2"
            size="small"
            controls-position="right"
            style="width: 100%"
            @change="onLineItemChange"
          />
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.lineItems.unit')" width="100">
        <template #default="{ row }">
          <el-select
            v-model="row.unit"
            :placeholder="t('rfq.lineItems.selectUnit')"
            size="small"
            filterable
            allow-create
            @change="onLineItemChange"
          >
            <el-option value="件" label="件" />
            <el-option value="个" label="个" />
            <el-option value="台" label="台" />
            <el-option value="套" label="套" />
            <el-option value="箱" label="箱" />
            <el-option value="包" label="包" />
            <el-option value="kg" label="kg" />
            <el-option value="g" label="g" />
            <el-option value="L" label="L" />
            <el-option value="m" label="m" />
            <el-option value="m²" label="m²" />
            <el-option value="m³" label="m³" />
          </el-select>
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.items.estimatedPrice')" width="120">
        <template #default="{ row }">
          <el-input-number
            v-model="row.estimatedUnitPrice"
            :min="0"
            :precision="2"
            size="small"
            controls-position="right"
            style="width: 100%"
            @change="onLineItemChange"
          />
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.lineItems.currency')" width="100">
        <template #default="{ row }">
          <el-select v-model="row.currency" size="small" @change="onLineItemChange">
            <el-option value="CNY" label="CNY" />
            <el-option value="USD" label="USD" />
            <el-option value="EUR" label="EUR" />
            <el-option value="JPY" label="JPY" />
          </el-select>
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.lineItems.notes')" min-width="120">
        <template #default="{ row }">
          <el-input
            v-model="row.notes"
            :placeholder="t('rfq.lineItems.notesPlaceholder')"
            size="small"
            @input="onLineItemChange"
          />
        </template>
      </el-table-column>

      <el-table-column :label="t('common.actions')" width="80" fixed="right" align="center">
        <template #default="{ $index }">
          <el-button
            type="danger"
            :icon="Delete"
            size="small"
            link
            @click="removeLineItem($index)"
          />
        </template>
      </el-table-column>
    </el-table>

    <div v-if="lineItems.length > 0" class="summary-bar">
      <span>{{ t("rfq.lineItems.totalItems") }}: {{ lineItems.length }}</span>
      <span>{{ t("rfq.lineItems.estimatedTotal") }}: {{ formatTotal() }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { Plus, Delete } from "@element-plus/icons-vue";

export interface RfqLineItem {
  lineNumber?: number;
  materialCategory: string;
  brand?: string;
  itemName: string;
  specifications?: string;
  quantity: number;
  unit: string;
  estimatedUnitPrice?: number;
  currency?: string;
  parameters?: string;
  notes?: string;
}

const props = defineProps<{
  modelValue: RfqLineItem[];
}>();

const emit = defineEmits<{
  (e: "update:modelValue", value: RfqLineItem[]): void;
}>();

const { t } = useI18n();

const lineItems = computed({
  get: () => props.modelValue || [],
  set: (value) => emit("update:modelValue", value),
});

function addLineItem() {
  const newItem: RfqLineItem = {
    lineNumber: lineItems.value.length + 1,
    materialCategory: "",
    brand: "",
    itemName: "",
    specifications: "",
    quantity: 1,
    unit: "件",
    estimatedUnitPrice: undefined,
    currency: "CNY",
    parameters: "",
    notes: "",
  };

  lineItems.value = [...lineItems.value, newItem];
}

function removeLineItem(index: number) {
  const updatedItems = [...lineItems.value];
  updatedItems.splice(index, 1);

  // Renumber line items
  updatedItems.forEach((item, idx) => {
    item.lineNumber = idx + 1;
  });

  lineItems.value = updatedItems;
}

function onLineItemChange() {
  // Trigger reactivity by creating a new array
  lineItems.value = [...lineItems.value];
}

function formatTotal(): string {
  let total = 0;
  let currency = "CNY";

  lineItems.value.forEach((item) => {
    if (item.quantity && item.estimatedUnitPrice) {
      total += item.quantity * item.estimatedUnitPrice;
      if (item.currency) {
        currency = item.currency;
      }
    }
  });

  return total > 0 ? `${total.toFixed(2)} ${currency}` : "-";
}
</script>

<style scoped>
.rfq-line-items-editor {
  width: 100%;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.editor-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.summary-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 16px;
  padding: 12px 16px;
  background-color: #f5f7fa;
  border-radius: 4px;
  font-size: 14px;
  font-weight: 500;
  color: #606266;
}

:deep(.el-input-number) {
  width: 100%;
}

:deep(.el-input__inner) {
  padding: 0 8px;
}

:deep(.el-table .cell) {
  padding: 4px 8px;
}
</style>
