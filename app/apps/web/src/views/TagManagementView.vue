<template>
  <div class="tag-management-view">
    <PageHeader
      :title="t('tagManagement.title')"
      :subtitle="t('tagManagement.subtitle')"
    >
      <template #actions>
        <el-button type="primary" @click="showCreateDialog = true">
          <el-icon><Plus /></el-icon>
          {{ t("tagManagement.createTag") }}
        </el-button>
      </template>
    </PageHeader>

    <!-- Search Bar -->
    <div class="search-bar">
      <el-input
        v-model="searchQuery"
        :placeholder="t('tagManagement.searchPlaceholder')"
        clearable
        style="max-width: 400px"
      >
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
      </el-input>
    </div>

    <!-- Tags Grid -->
    <el-card v-loading="loading" class="tags-card">
      <div v-if="filteredTags.length === 0" class="empty-state">
        <el-empty :description="t('tagManagement.empty')">
          <el-button type="primary" @click="showCreateDialog = true">
            {{ t("tagManagement.createFirstTag") }}
          </el-button>
        </el-empty>
      </div>

      <div v-else class="tags-grid">
        <div
          v-for="tag in filteredTags"
          :key="tag.id"
          class="tag-card"
          :style="{ borderColor: tag.color || '#409EFF' }"
        >
          <div class="tag-card-header">
            <div class="tag-info">
              <div class="tag-color-dot" :style="{ backgroundColor: tag.color || '#409EFF' }"></div>
              <h3>{{ tag.name }}</h3>
            </div>
            <el-dropdown trigger="click">
              <el-icon class="tag-menu-icon"><MoreFilled /></el-icon>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item @click="editTag(tag)">
                    <el-icon><Edit /></el-icon>
                    {{ t("tagManagement.editTag") }}
                  </el-dropdown-item>
                  <el-dropdown-item @click="openDeleteConfirm(tag)" divided>
                    <el-icon><Delete /></el-icon>
                    {{ t("tagManagement.deleteTag") }}
                  </el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </div>

          <p class="tag-description">
            {{ tag.description || t("tagManagement.noDescription") }}
          </p>

          <div class="tag-stats">
            <el-tag size="small" type="info">
              {{ t("tagManagement.usedBySuppliers", { count: getTagUsageCount(tag.name) }) }}
            </el-tag>
          </div>

          <div class="tag-actions">
            <el-button size="small" @click="openSupplierManagement(tag)">
              <el-icon><UserFilled /></el-icon>
              {{ t("tagManagement.manageSuppliers") }}
            </el-button>
          </div>
        </div>
      </div>
    </el-card>

    <!-- Create/Edit Tag Dialog -->
    <el-dialog
      v-model="showCreateDialog"
      :title="editingTag ? t('tagManagement.editTag') : t('tagManagement.createTag')"
      width="500px"
    >
      <el-form :model="tagForm" :rules="tagRules" ref="tagFormRef" label-width="100px">
        <el-form-item :label="t('tagManagement.tagName')" prop="name">
          <el-input v-model="tagForm.name" :placeholder="t('tagManagement.placeholders.tagName')" />
        </el-form-item>
        <el-form-item :label="t('tagManagement.description')">
          <el-input
            v-model="tagForm.description"
            type="textarea"
            :rows="3"
            :placeholder="t('tagManagement.placeholders.description')"
          />
        </el-form-item>
        <el-form-item :label="t('tagManagement.tagColor')">
          <el-color-picker v-model="tagForm.color" show-alpha :predefine="predefineColors" />
          <span style="margin-left: 10px; color: #909399; font-size: 12px">
            {{ t("tagManagement.colorHelper") }}
          </span>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="cancelDialog">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" :loading="submitting" @click="submitTag">
          {{ editingTag ? t("tagManagement.actions.update") : t("tagManagement.actions.create") }}
        </el-button>
      </template>
    </el-dialog>

    <!-- Manage Suppliers Dialog -->
    <el-dialog
      v-model="showSupplierManagementDialog"
      :title="t('tagManagement.manageSuppliersForTag', { tagName: managingTag?.name || '' })"
      width="800px"
    >
      <div v-loading="loadingSuppliers" class="supplier-management-content">
        <!-- Current Suppliers -->
        <div class="supplier-section">
          <h3>{{ t("tagManagement.currentSuppliers") }}</h3>
          <div v-if="tagSuppliers.length === 0" class="empty-message">
            {{ t("tagManagement.noSuppliersWithTag") }}
          </div>
          <div v-else class="supplier-list">
            <div v-for="supplier in tagSuppliers" :key="supplier.id" class="supplier-item">
              <div class="supplier-info">
                <span class="supplier-name">{{ supplier.companyName }}</span>
                <span class="supplier-details"
                  >{{ supplier.category }} | {{ supplier.region }}</span
                >
              </div>
              <el-button
                size="small"
                type="danger"
                text
                @click="removeSupplierFromTag(supplier.id)"
              >
                <el-icon><RemoveFilled /></el-icon>
                {{ t("common.remove") }}
              </el-button>
            </div>
          </div>
        </div>

        <!-- Add Suppliers -->
        <el-divider />
        <div class="supplier-section">
          <h3>{{ t("tagManagement.addSuppliers") }}</h3>
          <el-select
            v-model="selectedSuppliersToAdd"
            multiple
            filterable
            :placeholder="t('tagManagement.searchSuppliersPlaceholder')"
            style="width: 100%"
          >
            <el-option
              v-for="supplier in availableSuppliersToAdd"
              :key="supplier.id"
              :label="`${supplier.companyName} (${supplier.category})`"
              :value="supplier.id"
            />
          </el-select>
          <el-button
            type="primary"
            :disabled="selectedSuppliersToAdd.length === 0"
            :loading="addingSuppliers"
            @click="addSuppliersToTag"
            style="margin-top: 12px; width: 100%"
          >
            {{ t("tagManagement.addSelectedSuppliers", { count: selectedSuppliersToAdd.length }) }}
          </el-button>
        </div>
      </div>
      <template #footer>
        <el-button @click="closeSupplierManagement">{{ t("common.close") }}</el-button>
      </template>
    </el-dialog>

    <!-- Delete Confirmation Dialog -->
    <el-dialog
      v-model="showDeleteDialog"
      :title="t('tagManagement.confirmDeleteTitle')"
      width="450px"
      :close-on-click-modal="false"
    >
      <div style="margin-bottom: 20px;">
        <p style="color: #606266; margin-bottom: 12px;">
          {{
            tagToDelete && getTagUsageCount(tagToDelete.name) > 0
              ? t("tagManagement.confirmDeleteInUse", { count: getTagUsageCount(tagToDelete.name) })
              : t("tagManagement.confirmDeleteWithName", { name: tagToDelete?.name })
          }}
        </p>
        <el-alert
          v-if="tagToDelete && getTagUsageCount(tagToDelete.name) > 0"
          type="warning"
          :closable="false"
          show-icon
        >
          {{ t("tagManagement.deleteWarning") }}
        </el-alert>
      </div>
      <template #footer>
        <el-button @click="showDeleteDialog = false">{{ t("common.cancel") }}</el-button>
        <ConfirmButton
          type="danger"
          :text="t('common.delete')"
          :confirm-text="t('tagManagement.confirmDeleteAction')"
          @confirm="confirmDelete"
        />
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { type FormInstance, type FormRules } from "element-plus";
import PageHeader from "@/components/layout/PageHeader.vue";
import ConfirmButton from "@/components/common/ConfirmButton.vue";
import {
  Plus,
  Search,
  Edit,
  Delete,
  MoreFilled,
  UserFilled,
  RemoveFilled,
} from "@element-plus/icons-vue";
import { useSupplierStore } from "@/stores/supplier";
import { storeToRefs } from "pinia";
import * as suppliersApi from "@/api/suppliers";
import type { SupplierTag, Supplier } from "@/types";


import { useNotification } from "@/composables";
const notification = useNotification();

defineOptions({ name: "TagManagementView" });

const { t } = useI18n();

const supplierStore = useSupplierStore();
const supplierRefs = storeToRefs(supplierStore);
const suppliers = supplierRefs.suppliers;
const storeTags = supplierRefs.availableTags;

// State
const loading = ref(false);
const searchQuery = ref("");
const showCreateDialog = ref(false);
const submitting = ref(false);
const editingTag = ref<SupplierTag | null>(null);

const tagFormRef = ref<FormInstance>();
const tagForm = ref({
  name: "",
  description: "",
  color: "#409EFF",
});

// Supplier management state
const showSupplierManagementDialog = ref(false);
const managingTag = ref<SupplierTag | null>(null);
const tagSuppliers = ref<Supplier[]>([]);
const loadingSuppliers = ref(false);
const selectedSuppliersToAdd = ref<number[]>([]);
const addingSuppliers = ref(false);

const tagRules = computed<FormRules>(() => ({
  name: [
    { required: true, message: t("tagManagement.validation.nameRequired"), trigger: "blur" },
    { min: 2, max: 50, message: t("tagManagement.validation.nameLength"), trigger: "blur" },
  ],
}));

const asTagArray = (source: unknown): SupplierTag[] | null => {
  if (!source) return null;
  if (Array.isArray(source)) {
    return source as SupplierTag[];
  }
  if (typeof source === "object" && "value" in source) {
    const value = (source as { value: unknown }).value;
    return Array.isArray(value) ? (value as SupplierTag[]) : null;
  }
  return null;
};

const getStoreField = (store: unknown, key: "availableTags" | "tags"): unknown => {
  if (!store || typeof store !== "object") return undefined;
  if (!(key in store)) return undefined;
  return (store as Record<string, unknown>)[key];
};

const availableTagsList = computed<SupplierTag[]>(() => {
  const fromRefs = asTagArray(storeTags);
  if (fromRefs) return fromRefs;

  const fromStore = asTagArray(getStoreField(supplierStore, "availableTags"));
  if (fromStore) return fromStore;

  const legacy = asTagArray(getStoreField(supplierStore, "tags"));
  return legacy ?? [];
});

const predefineColors = [
  "#409EFF", // Primary Blue
  "#67C23A", // Success Green
  "#E6A23C", // Warning Orange
  "#F56C6C", // Danger Red
  "#909399", // Info Gray
  "#ff4500", // Orange Red
  "#ff8c00", // Dark Orange
  "#ffd700", // Gold
  "#90ee90", // Light Green
  "#00ced1", // Dark Turquoise
  "#9370db", // Medium Purple
  "#ff69b4", // Hot Pink
];

// Computed
const filteredTags = computed(() => {
  const tags = availableTagsList.value;
  if (!searchQuery.value) return tags;

  const query = searchQuery.value.toLowerCase();
  return tags.filter(
    (tag) =>
      tag.name.toLowerCase().includes(query) ||
      (tag.description && tag.description.toLowerCase().includes(query)),
  );
});

const availableSuppliersToAdd = computed(() => {
  const allSuppliers = suppliers.value || [];
  const currentSupplierIds = new Set(tagSuppliers.value.map((s) => s.id));
  return allSuppliers.filter((s) => !currentSupplierIds.has(s.id));
});

// Methods
const getTagUsageCount = (tagName: string) => {
  const allSuppliers = suppliers.value || [];
  return allSuppliers.filter(
    (supplier) =>
      Array.isArray(supplier.tags) &&
      supplier.tags.some(
        (tag: any) =>
          (typeof tag === "string" && tag === tagName) ||
          (typeof tag === "object" && tag?.name === tagName),
      ),
  ).length;
};

const editTag = (tag: SupplierTag) => {
  editingTag.value = tag;
  tagForm.value = {
    name: tag.name,
    description: tag.description || "",
    color: tag.color || "#409EFF",
  };
  showCreateDialog.value = true;
};

const tagToDelete = ref<SupplierTag | null>(null);
const showDeleteDialog = ref(false);

const openDeleteConfirm = (tag: SupplierTag) => {
  tagToDelete.value = tag;
  showDeleteDialog.value = true;
};

const confirmDelete = async () => {
  if (!tagToDelete.value) return;

  try {
    await suppliersApi.deleteTag(tagToDelete.value.id);
    await supplierStore.ensureTags();
    notification.success(t("tagManagement.notifications.deleteSuccess"));
    showDeleteDialog.value = false;
    tagToDelete.value = null;
  } catch (error) {
    console.error(t("tagManagement.notifications.deleteFailure"), error);
    notification.error(t("tagManagement.notifications.deleteFailure"));
  }
};

const cancelDialog = () => {
  showCreateDialog.value = false;
  editingTag.value = null;
  tagFormRef.value?.resetFields();
  tagForm.value = {
    name: "",
    description: "",
    color: "#409EFF",
  };
};

const submitTag = async () => {
  if (!tagFormRef.value) return;

  await tagFormRef.value.validate(async (valid) => {
    if (!valid) return;

    submitting.value = true;
    try {
      if (editingTag.value) {
        const response = await suppliersApi.updateTag(editingTag.value.id, {
          name: tagForm.value.name,
          description: tagForm.value.description || undefined,
          color: tagForm.value.color || undefined,
        });
        // Response is { data: SupplierTag }, unwrap it
        const updatedTag = response.data;
        notification.success(t("tagManagement.notifications.updateSuccess"));
      } else {
        const response = await suppliersApi.createTag({
          name: tagForm.value.name,
          description: tagForm.value.description || undefined,
          color: tagForm.value.color || undefined,
        });
        // Response is { data: SupplierTag }, unwrap it
        const createdTag = response.data;
        notification.success(t("tagManagement.notifications.createSuccess"));
      }

      await supplierStore.ensureTags();
      cancelDialog();
    } catch (error: unknown) {
      console.error(t("tagManagement.notifications.saveFailure"), error);
      const fallbackMessage = t("tagManagement.notifications.saveFailure");
      const apiMessage = (error as any)?.response?.data?.message;
      notification.error(apiMessage || fallbackMessage);
    } finally {
      submitting.value = false;
    }
  });
};

// Supplier Management Methods
const openSupplierManagement = async (tag: SupplierTag) => {
  managingTag.value = tag;
  showSupplierManagementDialog.value = true;
  selectedSuppliersToAdd.value = [];

  loadingSuppliers.value = true;
  try {
    const response = await suppliersApi.getSuppliersByTag(tag.id);
    tagSuppliers.value = response.data || [];
  } catch (error) {
    console.error("Failed to load suppliers for tag", error);
    notification.error(t("tagManagement.notifications.loadSuppliersFailure"));
    tagSuppliers.value = [];
  } finally {
    loadingSuppliers.value = false;
  }
};

const closeSupplierManagement = () => {
  showSupplierManagementDialog.value = false;
  managingTag.value = null;
  tagSuppliers.value = [];
  selectedSuppliersToAdd.value = [];
};

const addSuppliersToTag = async () => {
  if (!managingTag.value || selectedSuppliersToAdd.value.length === 0) return;

  addingSuppliers.value = true;
  try {
    const response = await suppliersApi.batchAssignTag(
      managingTag.value.id,
      selectedSuppliersToAdd.value,
    );

    notification.success(
      t("tagManagement.notifications.addSuppliersSuccess", {
        count: response.data.added,
        tagName: managingTag.value.name,
      }),
    );

    // Refresh supplier list
    const updatedResponse = await suppliersApi.getSuppliersByTag(managingTag.value.id);
    tagSuppliers.value = updatedResponse.data || [];
    selectedSuppliersToAdd.value = [];

    // Refresh suppliers in store
    await supplierStore.fetchSuppliers();
  } catch (error) {
    console.error("Failed to add suppliers to tag", error);
    notification.error(t("tagManagement.notifications.addSuppliersFailure"));
  } finally {
    addingSuppliers.value = false;
  }
};

const removeSupplierFromTag = async (supplierId: number) => {
  if (!managingTag.value) return;

  try {
    await suppliersApi.batchRemoveTag(managingTag.value.id, [supplierId]);

    // Remove from local list
    tagSuppliers.value = tagSuppliers.value.filter((s) => s.id !== supplierId);

    notification.success(
      t("tagManagement.notifications.removeSupplierSuccess", {
        tagName: managingTag.value.name,
      }),
    );

    // Refresh suppliers in store
    await supplierStore.fetchSuppliers();
  } catch (error) {
    console.error("Failed to remove supplier from tag", error);
    notification.error(t("tagManagement.notifications.removeSupplierFailure"));
  }
};

// Lifecycle
onMounted(async () => {
  loading.value = true;
  try {
    await Promise.all([supplierStore.ensureTags(), supplierStore.fetchSuppliers()]);
  } catch (error) {
    console.error(t("tagManagement.notifications.loadFailure"), error);
    notification.error(t("tagManagement.notifications.loadFailure"));
  } finally {
    loading.value = false;
  }
});




</script>

<style scoped>
.tag-management-view {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}




.search-bar {
  margin-bottom: 20px;
}

.tags-card {
  min-height: 400px;
}

.empty-state {
  padding: 60px 20px;
  text-align: center;
}

.tags-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 20px;
}

.tag-card {
  border: 2px solid;
  border-radius: 8px;
  padding: 16px;
  background: #fff;
  transition: all 0.3s ease;
  position: relative;
}

.tag-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  transform: translateY(-2px);
}

.tag-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}

.tag-info {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
}

.tag-color-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  flex-shrink: 0;
}

.tag-card h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
  word-break: break-word;
}

.tag-menu-icon {
  cursor: pointer;
  color: #909399;
  font-size: 18px;
  padding: 4px;
}

.tag-menu-icon:hover {
  color: #606266;
}

.tag-description {
  margin: 0 0 12px 0;
  color: #606266;
  font-size: 14px;
  line-height: 1.5;
  min-height: 42px;
}

.tag-stats {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 12px;
}

.tag-actions {
  margin-top: 8px;
}

.tag-actions .el-button {
  width: 100%;
}

.supplier-management-content {
  min-height: 300px;
}

.supplier-section h3 {
  margin: 0 0 12px 0;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.empty-message {
  padding: 20px;
  text-align: center;
  color: #909399;
  font-size: 14px;
}

.supplier-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 300px;
  overflow-y: auto;
}

.supplier-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px;
  border: 1px solid #e4e7ed;
  border-radius: 4px;
  transition: all 0.2s;
}

.supplier-item:hover {
  background-color: #f5f7fa;
  border-color: #409eff;
}

.supplier-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.supplier-name {
  font-weight: 600;
  color: #303133;
  font-size: 14px;
}

.supplier-details {
  font-size: 12px;
  color: #909399;
}
</style>
