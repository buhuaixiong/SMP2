<template>
  <div class="rfq-items-manager">
    <div class="manager-header">
      <h3>{{ t("rfq.items.title") }}</h3>
      <el-button type="primary" :icon="Plus" @click="addItem">
        {{ t("rfq.items.addItem") }}
      </el-button>
    </div>

    <el-alert
      v-if="items.length === 0"
      :title="t('rfq.items.emptyHint')"
      type="info"
      :closable="false"
      show-icon
      style="margin-bottom: 16px"
    />

    <!-- 物料行表格 -->
    <el-table v-else :data="items" border stripe style="width: 100%">
      <el-table-column type="index" :label="t('rfq.items.lineNumber')" width="70" align="center" />

      <el-table-column :label="t('rfq.materialType.label')" width="110">
        <template #default="{ row }">
          <el-select v-model="row.materialType" size="small">
            <el-option :label="t('rfq.materialType.idm')" value="IDM" />
            <el-option :label="t('rfq.materialType.dm')" value="DM" />
          </el-select>
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.distributionForm.category')" width="140">
        <template #default="{ row, $index }">
          <el-select
            v-model="row.distributionCategory"
            size="small"
            @change="onCategoryChange($index)"
          >
            <el-option :label="t('rfq.distributionCategory.equipment')" value="equipment" />
            <el-option
              :label="t('rfq.distributionCategory.auxiliaryMaterials')"
              value="auxiliary_materials"
            />
            <el-option :label="t('rfq.distributionCategory.fixtures')" value="fixtures" />
            <el-option :label="t('rfq.distributionCategory.molds')" value="molds" />
            <el-option :label="t('rfq.distributionCategory.blades')" value="blades" />
            <el-option :label="t('rfq.distributionCategory.hardware')" value="hardware" />
          </el-select>
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.distributionForm.subcategory')" width="140">
        <template #default="{ row }">
          <el-select v-model="row.distributionSubcategory" size="small">
            <el-option
              v-for="sub in getSubcategories(row.distributionCategory)"
              :key="sub.value"
              :label="sub.label"
              :value="sub.value"
            />
          </el-select>
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.items.itemName')" min-width="180">
        <template #default="{ row }">
          <el-input
            v-model="row.itemName"
            size="small"
            :placeholder="t('rfq.items.itemNamePlaceholder')"
            required
          />
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.items.specifications')" min-width="200">
        <template #default="{ row }">
          <el-input
            v-model="row.specifications"
            size="small"
            :placeholder="t('rfq.items.specificationsPlaceholder')"
          />
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.items.quantity')" width="110">
        <template #default="{ row }">
          <el-input-number
            v-model="row.quantity"
            size="small"
            :min="0"
            :precision="2"
            style="width: 100%"
          />
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.items.unit')" width="100">
        <template #default="{ row }">
          <el-input v-model="row.unit" size="small" :placeholder="t('rfq.items.unitPlaceholder')" />
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.items.estimatedPrice')" width="120">
        <template #default="{ row }">
          <el-input-number
            v-model="row.estimatedUnitPrice"
            size="small"
            :min="0"
            :precision="2"
            style="width: 100%"
            @change="updateTotal(row)"
          />
        </template>
      </el-table-column>

      <el-table-column :label="t('rfq.items.totalAmount')" width="120">
        <template #default="{ row }">
          <span class="total-amount">{{ formatTotal(row) }}</span>
        </template>
      </el-table-column>

      <el-table-column :label="t('common.actions')" width="150" fixed="right">
        <template #default="{ $index }">
          <el-button type="primary" :icon="Edit" size="small" link @click="editItem($index)">
            {{ t("common.details") }}
          </el-button>
          <el-button type="danger" :icon="Delete" size="small" link @click="deleteItem($index)">
            {{ t("common.delete") }}
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 总计行 -->
    <div v-if="items.length > 0" class="summary-row">
      <span class="summary-label">{{ t("rfq.items.grandTotal") }}:</span>
      <span class="summary-value">{{ grandTotal }} {{ items[0]?.currency || "CNY" }}</span>
    </div>

    <!-- 物料详情对话框 -->
    <el-dialog v-model="dialogVisible" :title="t('rfq.items.itemDetails')" width="700px">
      <el-form v-if="editingItem" :model="editingItem" label-width="120px">
        <el-form-item :label="t('rfq.items.itemCode')">
          <el-input v-model="editingItem.itemCode" />
        </el-form-item>
        <el-form-item :label="t('rfq.items.detailedParameters')">
          <el-input v-model="editingItem.detailedParameters" type="textarea" :rows="4" />
        </el-form-item>
        <el-form-item :label="t('common.notes')">
          <el-input v-model="editingItem.notes" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item :label="t('common.currency')">
          <el-select v-model="editingItem.currency">
            <el-option label="CNY" value="CNY" />
            <el-option label="USD" value="USD" />
            <el-option label="EUR" value="EUR" />
            <el-option label="JPY" value="JPY" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" @click="saveItem">{{ t("common.save") }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import { useI18n } from "vue-i18n";
import { Plus, Edit, Delete } from "@element-plus/icons-vue";
import type { RfqItem } from "@/types";

const { t } = useI18n();

const props = defineProps<{
  modelValue: RfqItem[];
}>();

const emit = defineEmits<{
  "update:modelValue": [value: RfqItem[]];
}>();

const items = computed({
  get: () => props.modelValue,
  set: (value) => emit("update:modelValue", value),
});

const dialogVisible = ref(false);
const editingIndex = ref(-1);
const editingItem = ref<RfqItem | null>(null);

const grandTotal = computed(() => {
  const total = items.value.reduce((sum, item) => {
    const itemTotal =
      item.quantity && item.estimatedUnitPrice ? item.quantity * item.estimatedUnitPrice : 0;
    return sum + itemTotal;
  }, 0);
  return total.toFixed(2);
});

function addItem() {
  const newItem: RfqItem = {
    lineNumber: items.value.length + 1,
    materialType: "IDM",
    distributionCategory: "",
    distributionSubcategory: "",
    itemName: "",
    quantity: 1,
    unit: "件",
    currency: "CNY",
  };
  items.value = [...items.value, newItem];
}

function deleteItem(index: number) {
  items.value = items.value.filter((_, i) => i !== index);
  // 重新编号
  items.value.forEach((item, i) => (item.lineNumber = i + 1));
}

function editItem(index: number) {
  editingIndex.value = index;
  editingItem.value = { ...items.value[index] };
  dialogVisible.value = true;
}

function saveItem() {
  if (editingIndex.value >= 0 && editingItem.value) {
    const updated = [...items.value];
    updated[editingIndex.value] = editingItem.value;
    items.value = updated;
  }
  dialogVisible.value = false;
}

function updateTotal(item: RfqItem) {
  if (item.quantity && item.estimatedUnitPrice) {
    item.estimatedTotalAmount = item.quantity * item.estimatedUnitPrice;
  }
}

function formatTotal(item: RfqItem): string {
  if (item.quantity && item.estimatedUnitPrice) {
    return (item.quantity * item.estimatedUnitPrice).toFixed(2);
  }
  return "-";
}

function getSubcategories(category: string) {
  const directMapping: Record<string, string> = {
    fixtures: "fixtures",
    molds: "molds",
    blades: "blades",
    hardware: "hardware",
  };

  // 直接类别（无子类别选择）
  if (directMapping[category]) {
    return [{ label: t(`rfq.distributionCategory.${category}`), value: directMapping[category] }];
  }

  if (category === "equipment") {
    return [
      { label: t("rfq.distributionSubcategory.standard"), value: "standard" },
      { label: t("rfq.distributionSubcategory.nonStandard"), value: "non_standard" },
    ];
  }

  if (category === "auxiliary_materials") {
    return [
      { label: t("rfq.distributionSubcategory.laborProtection"), value: "labor_protection" },
      { label: t("rfq.distributionSubcategory.officeSupplies"), value: "office_supplies" },
      { label: t("rfq.distributionSubcategory.productionSupplies"), value: "production_supplies" },
      { label: t("rfq.distributionSubcategory.accessories"), value: "accessories" },
      { label: t("rfq.distributionSubcategory.others"), value: "others" },
    ];
  }

  return [];
}

function onCategoryChange(index: number) {
  const item = items.value[index];
  const directMapping: Record<string, string> = {
    fixtures: "fixtures",
    molds: "molds",
    blades: "blades",
    hardware: "hardware",
  };

  if (directMapping[item.distributionCategory]) {
    item.distributionSubcategory = directMapping[item.distributionCategory];
  } else {
    item.distributionSubcategory = "";
  }
}
</script>

<style scoped>
.rfq-items-manager {
  background: #f5f7fa;
  border-radius: 12px;
  padding: 24px;
}

.manager-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.manager-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.total-amount {
  font-weight: 600;
  color: #409eff;
}

.summary-row {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  margin-top: 16px;
  padding: 12px 16px;
  background: white;
  border: 1px solid #dcdfe6;
  border-radius: 8px;
}

.summary-label {
  font-size: 16px;
  font-weight: 600;
  color: #606266;
  margin-right: 12px;
}

.summary-value {
  font-size: 20px;
  font-weight: 700;
  color: #409eff;
}

:deep(.el-table) {
  background: white;
  border-radius: 8px;
}

:deep(.el-input-number .el-input__inner) {
  text-align: left;
}
</style>
