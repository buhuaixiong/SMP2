<template>
  <div class="purchasing-groups-view">
    <PageHeader
      :title="t('purchasingGroups.title')"
      :subtitle="t('purchasingGroups.subtitle')"
    >
      <template #actions>
        <el-button v-if="canManageGroups" type="primary" @click="showCreateDialog = true">
          <el-icon><Plus /></el-icon>
          {{ t("purchasingGroups.createGroup") }}
        </el-button>
      </template>
    </PageHeader>

    <div class="filters-bar">
      <el-input
        v-model="searchQuery"
        :placeholder="t('purchasingGroups.filters.search')"
        clearable
        style="max-width: 400px"
      >
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
      </el-input>

      <div class="filter-group">
        <el-select
          v-model="filters.category"
          :placeholder="t('purchasingGroups.filters.category')"
          clearable
          style="width: 160px"
        >
          <el-option :label="t('purchasingGroups.filters.allCategories')" value="" />
          <el-option v-for="cat in categoryOptions" :key="cat" :label="cat" :value="cat" />
        </el-select>

        <el-select
          v-model="filters.region"
          :placeholder="t('purchasingGroups.filters.region')"
          clearable
          style="width: 160px"
        >
          <el-option :label="t('purchasingGroups.filters.allRegions')" value="" />
          <el-option
            v-for="region in regionOptions"
            :key="region"
            :label="region"
            :value="region"
          />
        </el-select>

        <el-select
          v-model="filters.isActive"
          :placeholder="t('purchasingGroups.filters.status')"
          clearable
          style="width: 140px"
        >
          <el-option :label="t('purchasingGroups.filters.allStatus')" value="" />
          <el-option :label="t('purchasingGroups.status.active')" value="true" />
          <el-option :label="t('purchasingGroups.status.inactive')" value="false" />
        </el-select>
      </div>
    </div>

    <el-card v-loading="loading" class="groups-card">
      <el-table :data="filteredGroups" stripe @row-click="handleRowClick">
        <el-table-column
          prop="code"
          :label="t('purchasingGroups.table.columns.code')"
          width="120"
        />
        <el-table-column
          prop="name"
          :label="t('purchasingGroups.table.columns.name')"
          min-width="200"
        />
        <el-table-column
          prop="category"
          :label="t('purchasingGroups.table.columns.category')"
          width="140"
        />
        <el-table-column
          prop="region"
          :label="t('purchasingGroups.table.columns.region')"
          width="120"
        />
        <el-table-column
          :label="t('purchasingGroups.table.columns.members')"
          width="100"
          align="center"
        >
          <template #default="{ row }">
            <el-tag size="small">{{ row.memberCount || 0 }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column
          :label="t('purchasingGroups.table.columns.suppliers')"
          width="100"
          align="center"
        >
          <template #default="{ row }">
            <el-tag size="small" type="success">{{ row.supplierCount || 0 }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column
          :label="t('purchasingGroups.table.columns.status')"
          width="100"
          align="center"
        >
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
              {{
                row.isActive
                  ? t("purchasingGroups.status.active")
                  : t("purchasingGroups.status.inactive")
              }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column
          :label="t('purchasingGroups.table.columns.actions')"
          width="180"
          align="center"
          fixed="right"
        >
          <template #default="{ row }">
            <el-button
              v-if="canManageGroups"
              link
              type="primary"
              size="small"
              @click.stop="editGroup(row)"
            >
              {{ t("common.edit") }}
            </el-button>
            <el-button
              v-if="canManageGroup(row.id)"
              link
              type="primary"
              size="small"
              @click.stop="manageGroup(row)"
            >
              {{ t("purchasingGroups.actions.manage") }}
            </el-button>
            <span
              v-else
              class="no-access-text"
            >
              {{ t("purchasingGroups.labels.noAccess") }}
            </span>
            <el-button
              v-if="canManageGroups"
              link
              type="danger"
              size="small"
              @click.stop="confirmDelete(row)"
            >
              {{ t("common.delete") }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog
      v-model="showCreateDialog"
      :title="
        editingGroup
          ? t('purchasingGroups.dialogs.editTitle')
          : t('purchasingGroups.dialogs.createTitle')
      "
      width="600px"
    >
      <el-form :model="groupForm" :rules="groupRules" ref="groupFormRef" label-width="120px">
        <el-form-item :label="t('purchasingGroups.fields.code.label')" prop="code">
          <el-input
            v-model="groupForm.code"
            :placeholder="t('purchasingGroups.fields.code.placeholder')"
            :disabled="!!editingGroup"
          />
        </el-form-item>
        <el-form-item :label="t('purchasingGroups.fields.name.label')" prop="name">
          <el-input
            v-model="groupForm.name"
            :placeholder="t('purchasingGroups.fields.name.placeholder')"
          />
        </el-form-item>
        <el-form-item :label="t('purchasingGroups.fields.description.label')">
          <el-input
            v-model="groupForm.description"
            type="textarea"
            :rows="3"
            :placeholder="t('purchasingGroups.fields.description.placeholder')"
          />
        </el-form-item>
        <el-form-item :label="t('purchasingGroups.fields.category.label')">
          <el-input
            v-model="groupForm.category"
            :placeholder="t('purchasingGroups.fields.category.placeholder')"
          />
        </el-form-item>
        <el-form-item :label="t('purchasingGroups.fields.region.label')">
          <el-input
            v-model="groupForm.region"
            :placeholder="t('purchasingGroups.fields.region.placeholder')"
          />
        </el-form-item>
        <el-form-item v-if="editingGroup" :label="t('purchasingGroups.fields.isActive.label')">
          <el-switch v-model="groupForm.isActive" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="cancelGroupDialog">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" :loading="submitting" @click="submitGroup">
          {{
            editingGroup
              ? t("purchasingGroups.actions.update")
              : t("purchasingGroups.actions.create")
          }}
        </el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="showManageDialog"
      :title="t('purchasingGroups.dialogs.manageTitle', { name: currentGroup?.name ?? '' })"
      width="900px"
      destroy-on-close
    >
      <el-tabs v-model="activeTab">
        <el-tab-pane :label="t('purchasingGroups.tabs.members')" name="members">
          <div class="manage-section">
            <div class="section-header">
              <h3>{{ t("purchasingGroups.sections.members") }}</h3>
              <el-button size="small" type="primary" @click="showAddMembersDialog = true">
                {{ t("purchasingGroups.actions.addMembers") }}
              </el-button>
            </div>
            <el-table :data="members" v-loading="loadingMembers" stripe>
              <el-table-column
                prop="buyerName"
                :label="t('purchasingGroups.tableMembers.buyerName')"
              />
              <el-table-column
                prop="buyerRole"
                :label="t('purchasingGroups.tableMembers.buyerRole')"
                width="180"
              />
              <el-table-column
                prop="role"
                :label="t('purchasingGroups.tableMembers.groupRole')"
                width="120"
              >
                <template #default="{ row }">
                  <el-tag size="small" :type="row.role === 'lead' ? 'warning' : ''">
                    {{
                      row.role === "lead"
                        ? t("purchasingGroups.groupRole.leader")
                        : t("purchasingGroups.groupRole.member")
                    }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column
                :label="t('purchasingGroups.table.columns.actions')"
                width="100"
                align="center"
              >
                <template #default="{ row }">
                  <el-button link type="danger" size="small" @click="removeMember(row.buyerId)">
                    {{ t("common.remove") }}
                  </el-button>
                </template>
              </el-table-column>
            </el-table>
          </div>
        </el-tab-pane>

        <el-tab-pane :label="t('purchasingGroups.tabs.suppliers')" name="suppliers">
          <div class="manage-section">
            <div class="section-header">
              <h3>{{ t("purchasingGroups.sections.suppliers") }}</h3>
              <el-button v-if="canManageSuppliers" size="small" type="primary" @click="showAddSuppliersDialog = true">
                {{ t("purchasingGroups.actions.addSuppliers") }}
              </el-button>
            </div>
            <el-alert
              v-if="!canManageSuppliers && canAssignForCurrentGroup"
              type="info"
              :title="t('purchasingGroups.hints.managerControlsSuppliers')"
              show-icon
              class="assignment-hint"
            />
            <el-table :data="suppliers" v-loading="loadingSuppliers" stripe>
              <el-table-column
                prop="companyName"
                :label="t('purchasingGroups.tableSuppliers.companyName')"
              />
              <el-table-column
                prop="companyId"
                :label="t('purchasingGroups.tableSuppliers.companyId')"
                width="150"
              />
              <el-table-column
                prop="category"
                :label="t('purchasingGroups.table.columns.category')"
                width="140"
              />
              <el-table-column
                prop="region"
                :label="t('purchasingGroups.table.columns.region')"
                width="120"
              />
              <el-table-column
                :label="t('purchasingGroups.table.columns.actions')"
                width="100"
                align="center"
              >
                <template #default="{ row }">
                  <el-button
                    link
                    type="danger"
                    size="small"
                    @click="removeSupplier(row.supplierId)" v-if="canManageSuppliers"
                  >
                    {{ t("common.remove") }}
                  </el-button>
                </template>
              </el-table-column>
            </el-table>
          </div>
        </el-tab-pane>

        <el-tab-pane
          v-if="canAssignForCurrentGroup"
          :label="t('purchasingGroups.tabs.assignments')"
          name="assignments"
        >
          <div class="manage-section">
            <div class="section-header">
              <h3>{{ t('purchasingGroups.sections.assignments') }}</h3>
            </div>

            <el-form class="assignment-form" :model="assignForm" label-width="140px">
              <el-form-item :label="t('purchasingGroups.fields.assignmentBuyer.label')">
                <el-select
                  v-model="assignForm.buyerId"
                  filterable
                  clearable
                  :placeholder="t('purchasingGroups.fields.assignmentBuyer.placeholder')"
                >
                  <el-option
                    v-for="option in memberOptions"
                    :key="option.value"
                    :label="option.label"
                    :value="option.value"
                  />
                </el-select>
              </el-form-item>

              <el-form-item :label="t('purchasingGroups.fields.assignmentSuppliers.label')">
                <el-select
                  v-model="assignForm.supplierIds"
                  multiple
                  filterable
                  :placeholder="t('purchasingGroups.fields.assignmentSuppliers.placeholder')"
                >
                  <el-option
                    v-for="option in assignmentSupplierOptions"
                    :key="option.value"
                    :label="option.label"
                    :value="option.value"
                  />
                </el-select>
              </el-form-item>

              <div class="assignment-actions">
                <el-button
                  type="primary"
                  :loading="loadingAssignments"
                  @click="submitAssignment"
                >
                  {{ t('purchasingGroups.actions.assignSuppliers') }}
                </el-button>
              </div>
            </el-form>

            <el-table
              :data="buyerAssignments"
              v-loading="loadingAssignments"
              stripe
              class="assignment-table"
            >
              <el-table-column
                prop="buyerName"
                :label="t('purchasingGroups.tableAssignments.buyer')"
              >
                <template #default="{ row }">
                  {{ row.buyerName || row.buyerId }}
                </template>
              </el-table-column>
              <el-table-column
                prop="companyName"
                :label="t('purchasingGroups.tableAssignments.supplier')"
              />
              <el-table-column
                prop="companyId"
                :label="t('purchasingGroups.tableAssignments.supplierCode')"
                width="160"
              />
              <el-table-column
                prop="createdAt"
                :label="t('purchasingGroups.tableAssignments.assignedAt')"
                width="200"
              >
                <template #default="{ row }">
                  {{ formatTimestamp(row.createdAt) }}
                </template>
              </el-table-column>
              <el-table-column
                v-if="canAssignForCurrentGroup"
                :label="t('purchasingGroups.table.columns.actions')"
                width="120"
                align="center"
              >
                <template #default="{ row }">
                  <el-button link type="danger" size="small" @click="removeAssignment(row.id)">
                    {{ t('common.remove') }}
                  </el-button>
                </template>
              </el-table-column>
            </el-table>
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-dialog>

    <el-dialog
      v-model="showAddMembersDialog"
      :title="t('purchasingGroups.dialogs.addMembersTitle')"
      width="600px"
    >
      <el-form :model="addMembersForm" label-width="100px">
        <el-form-item :label="t('purchasingGroups.fields.buyers.label')">
          <el-select
            v-model="addMembersForm.buyerIds"
            multiple
            filterable
            :placeholder="t('purchasingGroups.fields.buyers.placeholder')"
            style="width: 100%"
          >
            <el-option
              v-for="buyer in availableBuyers"
              :key="buyer.id"
              :label="`${buyer.name} (${buyer.role})`"
              :value="buyer.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('purchasingGroups.fields.role')">
          <el-radio-group v-model="addMembersForm.role">
          <el-radio value="member">{{ t("purchasingGroups.groupRole.member") }}</el-radio>
          <el-radio value="lead">{{ t("purchasingGroups.groupRole.leader") }}</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item :label="t('purchasingGroups.fields.notes')">
          <el-input v-model="addMembersForm.notes" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showAddMembersDialog = false">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" :loading="submitting" @click="submitAddMembers">
          {{ t("purchasingGroups.actions.addMembers") }}
        </el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="showAddSuppliersDialog"
      :title="t('purchasingGroups.dialogs.addSuppliersTitle')"
      width="700px"
    >
      <el-form :model="addSuppliersForm" label-width="100px">
        <el-form-item :label="t('purchasingGroups.fields.suppliers.label')">
          <el-select
            v-model="addSuppliersForm.supplierIds"
            multiple
            filterable
            remote
            :remote-method="handleSupplierSearch"
            :loading="supplierSearchLoading"
            :reserve-keyword="true"
            :placeholder="t('purchasingGroups.fields.suppliers.placeholder')"
            style="width: 100%"
            @visible-change="onSupplierDropdownVisibleChange"
          >
            <el-option
              v-for="supplier in supplierOptions"
              :key="supplier.id"
              :label="getSupplierOptionLabel(supplier)"
              :value="supplier.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('purchasingGroups.fields.primary.label')">
          <el-switch v-model="addSuppliersForm.isPrimary" />
          <span style="margin-left: 10px; color: #909399; font-size: 12px">
            {{ t("purchasingGroups.fields.primary.hint") }}
          </span>
        </el-form-item>
        <el-form-item :label="t('purchasingGroups.fields.notes')">
          <el-input v-model="addSuppliersForm.notes" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showAddSuppliersDialog = false">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" :loading="submitting" @click="submitAddSuppliers">
          {{ t("purchasingGroups.actions.addSuppliers") }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted, watch } from "vue";
import { useI18n } from "vue-i18n";
import { type FormInstance, type FormRules } from "element-plus";
import { Plus, Search } from "@element-plus/icons-vue";
import PageHeader from "@/components/layout/PageHeader.vue";
import { useAuthStore } from "@/stores/auth";
import { usePurchasingGroupsStore } from "@/stores/purchasingGroups";
import { storeToRefs } from "pinia";
import * as usersApi from "@/api/users";
import { listSuppliers } from "@/api/suppliers";
import type { PurchasingGroup, PurchasingGroupDetail, Supplier } from "@/types";


import { useNotification } from "@/composables";

const notification = useNotification();
defineOptions({ name: "PurchasingGroupsView" });

const { t } = useI18n();
const authStore = useAuthStore();
const groupsStore = usePurchasingGroupsStore();

const {
  groups,
  loading,
  members,
  suppliers,
  buyerAssignments,
  loadingMembers,
  loadingSuppliers,
  loadingAssignments,
} = storeToRefs(groupsStore);

const searchQuery = ref("");
const filters = ref({
  category: "",
  region: "",
  isActive: "",
});

const showCreateDialog = ref(false);
const showManageDialog = ref(false);
const showAddMembersDialog = ref(false);
const showAddSuppliersDialog = ref(false);
const activeTab = ref("members");
const submitting = ref(false);

const editingGroup = ref<PurchasingGroup | null>(null);
const currentGroup = ref<PurchasingGroupDetail | null>(null);

const groupFormRef = ref<FormInstance>();
const groupForm = ref({
  code: "",
  name: "",
  description: "",
  category: "",
  region: "",
  isActive: true,
});

const groupRules = computed<FormRules>(() => ({
  code: [
    { required: true, message: t("purchasingGroups.validation.codeRequired"), trigger: "blur" },
  ],
  name: [
    { required: true, message: t("purchasingGroups.validation.nameRequired"), trigger: "blur" },
  ],
}));

const addMembersForm = ref({
  buyerIds: [] as string[],
  role: "member",
  notes: "",
});

const addSuppliersForm = ref({
  supplierIds: [] as number[],
  isPrimary: false,
  notes: "",
});

const assignForm = ref({
  buyerId: "",
  supplierIds: [] as number[],
});

const resetAssignmentForm = () => {
  assignForm.value = { buyerId: "", supplierIds: [] };
};

const availableBuyers = ref<any[]>([]);

const canManageGroups = computed(
  () => authStore.hasPermission?.("admin.purchasing_groups.manage") ?? false,
);

const leaderGroupIdSet = computed(() => {
  const set = new Set<number>();
  (authStore.user?.purchasingGroups ?? []).forEach((group: any) => {
    const id = Number(group?.id);
    const role = String(group?.memberRole ?? "").toLowerCase();
    if (Number.isFinite(id) && (role === "lead" || role === "leader")) {
      set.add(id);
    }
  });
  return set;
});

const isLeaderForGroupId = (groupId?: number | null) => {
  if (groupId === null || groupId === undefined) {
    return false;
  }
  return leaderGroupIdSet.value.has(Number(groupId));
};

const canManageSuppliers = computed(() => canManageGroups.value);

const canManageGroup = (groupId?: number | null) => {
  if (groupId === null || groupId === undefined) {
    return canManageGroups.value;
  }
  return canManageGroups.value || isLeaderForGroupId(groupId);
};

const canAssignForCurrentGroup = computed(() => canManageGroup(currentGroup.value?.id));

const SUPPLIER_SEARCH_LIMIT = 20;

type SupplierOption = Pick<
  Supplier,
  "id" | "companyName" | "companyId" | "englishName" | "chineseName"
>;

const supplierOptions = ref<SupplierOption[]>([]);
const supplierSearchLoading = ref(false);
const supplierSearchTerm = ref("");
let supplierSearchRequestId = 0;

const buildSupplierOption = (supplier: Supplier): SupplierOption => ({
  id: supplier.id,
  companyName: supplier.companyName ?? null,
  companyId: supplier.companyId ?? null,
  englishName: supplier.englishName ?? null,
  chineseName: supplier.chineseName ?? null,
});

const getSupplierOptionLabel = (option: SupplierOption) => {
  const primaryName =
    option.companyName?.trim() ||
    option.englishName?.trim() ||
    option.chineseName?.trim() ||
    `#${option.id}`;
  const code = option.companyId?.trim();
  return code ? `${primaryName} (${code})` : primaryName;
};

const mergeSupplierOptions = (incoming: SupplierOption[], selectedIds: Set<number>) => {
  const map = new Map<number, SupplierOption>();
  supplierOptions.value
    .filter((option) => selectedIds.has(option.id))
    .forEach((option) => map.set(option.id, option));
  incoming.forEach((option) => map.set(option.id, option));
  supplierOptions.value = Array.from(map.values());
};

const handleSupplierSearch = async (query: string) => {
  supplierSearchTerm.value = query;
  const trimmedQuery = query.trim();
  const requestId = ++supplierSearchRequestId;
  supplierSearchLoading.value = true;
  try {
    const response = await listSuppliers({
      q: trimmedQuery || undefined,
      limit: SUPPLIER_SEARCH_LIMIT,
      offset: 0,
    });
    const results = ((response.data ?? []) as Supplier[]).map((supplier) =>
      buildSupplierOption(supplier),
    );
    if (!showAddSuppliersDialog.value || requestId !== supplierSearchRequestId) {
      return;
    }
    const selectedIds = new Set(addSuppliersForm.value.supplierIds);
    mergeSupplierOptions(results, selectedIds);
  } catch (error) {
    if (requestId === supplierSearchRequestId) {
      console.error("Failed to search suppliers for purchasing group", error);
      notification.error(t("purchasingGroups.errors.loadSuppliersFailure"));
    }
  } finally {
    if (requestId === supplierSearchRequestId) {
      supplierSearchLoading.value = false;
    }
  }
};

const onSupplierDropdownVisibleChange = async (visible: boolean) => {
  if (visible) {
    await handleSupplierSearch(supplierSearchTerm.value);
  }
};

watch(showAddSuppliersDialog, (visible) => {
  if (!visible) {
    const selectedIds = new Set(addSuppliersForm.value.supplierIds);
    supplierOptions.value = supplierOptions.value.filter((option) => selectedIds.has(option.id));
    supplierSearchTerm.value = "";
    supplierSearchLoading.value = false;
  }
});

watch(members, (newMembers) => {
  const validBuyerIds = new Set(newMembers.map((member) => member.buyerId));
  if (assignForm.value.buyerId && !validBuyerIds.has(assignForm.value.buyerId)) {
    assignForm.value.buyerId = "";
  }
});

watch(suppliers, (newSuppliers) => {
  const validSupplierIds = new Set(newSuppliers.map((supplier) => supplier.supplierId));
  assignForm.value.supplierIds = assignForm.value.supplierIds.filter((id) => validSupplierIds.has(id));
});

watch(showManageDialog, (visible) => {
  if (!visible) {
    resetAssignmentForm();
  }
});

watch(activeTab, async (tab) => {
  if (tab === "assignments" && currentGroup.value && canAssignForCurrentGroup.value) {
    await groupsStore.fetchBuyerAssignments(currentGroup.value.id);
  }
});

const filteredGroups = computed(() => {
  let result = groups.value;

  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase();
    result = result.filter(
      (g) =>
        g.name.toLowerCase().includes(query) ||
        g.code.toLowerCase().includes(query) ||
        (g.description && g.description.toLowerCase().includes(query)),
    );
  }

  if (filters.value.category) {
    result = result.filter((g) => g.category === filters.value.category);
  }

  if (filters.value.region) {
    result = result.filter((g) => g.region === filters.value.region);
  }

  if (filters.value.isActive !== "") {
    const isActive = filters.value.isActive === "true";
    result = result.filter((g) => g.isActive === (isActive ? 1 : 0));
  }

  return result;
});

const categoryOptions = computed(() =>
  Array.from(new Set(groups.value.map((g) => g.category).filter(Boolean))),
);

const regionOptions = computed(() =>
  Array.from(new Set(groups.value.map((g) => g.region).filter(Boolean))),
);

const memberOptions = computed(() =>
  members.value.map((member) => ({
    value: member.buyerId,
    label: member.buyerName || member.buyerId,
  })),
);

const assignmentSupplierOptions = computed(() =>
  suppliers.value.map((supplier) => ({
    value: supplier.supplierId,
    label: supplier.companyName || supplier.companyId || `#${supplier.supplierId}`,
  })),
);

const formatTimestamp = (value?: string | null) => {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
};

const handleRowClick = (row: PurchasingGroup) => {
  manageGroup(row);
};

const editGroup = (group: PurchasingGroup) => {
  editingGroup.value = group;
  groupForm.value = {
    code: group.code,
    name: group.name,
    description: group.description || "",
    category: group.category || "",
    region: group.region || "",
    isActive: group.isActive === 1,
  };
  showCreateDialog.value = true;
};

const manageGroup = async (group: PurchasingGroup) => {
  if (!canManageGroup(group.id)) {
    notification.warning(t("purchasingGroups.errors.manageAccessDenied"));
    return;
  }

  try {
    const detail = await groupsStore.fetchGroup(group.id);
    currentGroup.value = detail;
    resetAssignmentForm();

    const cacheState = groupsStore.hydrateGroupAssociations(group.id);
    if (!cacheState.hasMembers || !cacheState.hasSuppliers) {
      await Promise.all([
        cacheState.hasMembers ? Promise.resolve() : groupsStore.fetchMembers(group.id),
        cacheState.hasSuppliers ? Promise.resolve() : groupsStore.fetchSuppliers(group.id),
      ]);
    }

    if (canAssignForCurrentGroup.value) {
      await groupsStore.fetchBuyerAssignments(group.id);
      activeTab.value = "assignments";
    } else {
      buyerAssignments.value = [];
      activeTab.value = "members";
    }

    showManageDialog.value = true;
  } catch (error: any) {
    console.error(t("purchasingGroups.errors.loadFailure"), error);
    if (error?.response?.status === 403 || error?.response?.status === 404) {
      notification.warning(t("purchasingGroups.errors.manageAccessDenied"));
    } else {
      notification.error(t("purchasingGroups.errors.loadFailure"));
    }
  }
};

const confirmDelete = (group: PurchasingGroup) => {
  notification.confirm(
    t("purchasingGroups.confirmDeleteDialog.message", { name: group.name }),
    t("purchasingGroups.confirmDeleteDialog.title"),
    {
      confirmButtonText: t("purchasingGroups.confirmDeleteDialog.confirm"),
      cancelButtonText: t("purchasingGroups.confirmDeleteDialog.cancel"),
      type: "warning",
    },
  )
    .then(async () => {
      try {
        await groupsStore.deleteGroup(group.id);
        notification.success(t("purchasingGroups.notifications.deleteSuccess"));
      } catch (error) {
        console.error(t("purchasingGroups.errors.deleteFailure"), error);
        notification.error(t("purchasingGroups.errors.deleteFailure"));
      }
    })
    .catch(() => {});
};

const cancelGroupDialog = () => {
  showCreateDialog.value = false;
  editingGroup.value = null;
  groupFormRef.value?.resetFields();
};

const submitGroup = async () => {
  if (!groupFormRef.value) return;

  await groupFormRef.value.validate(async (valid) => {
    if (!valid) return;

    submitting.value = true;
    try {
      if (editingGroup.value) {
        await groupsStore.updateGroup(editingGroup.value.id, {
          name: groupForm.value.name,
          description: groupForm.value.description || undefined,
          category: groupForm.value.category || undefined,
          region: groupForm.value.region || undefined,
          isActive: groupForm.value.isActive,
        });
        notification.success(t("purchasingGroups.notifications.updateSuccess"));
      } else {
        await groupsStore.createGroup({
          code: groupForm.value.code,
          name: groupForm.value.name,
          description: groupForm.value.description || undefined,
          category: groupForm.value.category || undefined,
          region: groupForm.value.region || undefined,
        });
        notification.success(t("purchasingGroups.notifications.createSuccess"));
      }
      cancelGroupDialog();
    } catch (error: any) {
      console.error(t("purchasingGroups.errors.saveFailure"), error);
      const fallback = t("purchasingGroups.errors.saveFailure");
      notification.error(error?.response?.data?.message ?? fallback);
    } finally {
      submitting.value = false;
    }
  });
};

const submitAddMembers = async () => {
  if (!currentGroup.value || !addMembersForm.value.buyerIds.length) {
    notification.warning(t("purchasingGroups.validation.selectBuyer"));
    return;
  }

  submitting.value = true;
  try {
    await groupsStore.addMembers(currentGroup.value.id, {
      buyerIds: addMembersForm.value.buyerIds,
      role: addMembersForm.value.role,
      notes: addMembersForm.value.notes || undefined,
    });
    notification.success(t("purchasingGroups.notifications.addMembersSuccess"));
    showAddMembersDialog.value = false;
    addMembersForm.value = { buyerIds: [], role: "member", notes: "" };
  } catch (error) {
    console.error(t("purchasingGroups.errors.addMembersFailure"), error);
    notification.error(t("purchasingGroups.errors.addMembersFailure"));
  } finally {
    submitting.value = false;
  }
};

const submitAddSuppliers = async () => {
  if (!canManageSuppliers.value) {
    return;
  }

  if (!currentGroup.value || !addSuppliersForm.value.supplierIds.length) {
    notification.warning(t("purchasingGroups.validation.selectSupplier"));
    return;
  }

  submitting.value = true;
  try {
    await groupsStore.addSuppliers(currentGroup.value.id, {
      supplierIds: addSuppliersForm.value.supplierIds,
      isPrimary: addSuppliersForm.value.isPrimary,
      notes: addSuppliersForm.value.notes || undefined,
    });
    notification.success(t("purchasingGroups.notifications.addSuppliersSuccess"));
    showAddSuppliersDialog.value = false;
    addSuppliersForm.value = { supplierIds: [], isPrimary: false, notes: "" };
  } catch (error) {
    console.error(t("purchasingGroups.errors.addSuppliersFailure"), error);
    notification.error(t("purchasingGroups.errors.addSuppliersFailure"));
  } finally {
    submitting.value = false;
  }
};

const submitAssignment = async () => {
  if (!currentGroup.value) {
    return;
  }
  if (!assignForm.value.buyerId) {
    notification.warning(t("purchasingGroups.validation.selectAssignmentBuyer"));
    return;
  }
  if (!assignForm.value.supplierIds.length) {
    notification.warning(t("purchasingGroups.validation.selectAssignmentSupplier"));
    return;
  }

  try {
    const result = await groupsStore.assignSuppliersToBuyer(currentGroup.value.id, {
      buyerId: assignForm.value.buyerId,
      supplierIds: [...assignForm.value.supplierIds],
    });
    const assignedCount = (result as { assignedCount?: number } | undefined)?.assignedCount
      ?? assignForm.value.supplierIds.length;
    notification.success(
      t("purchasingGroups.notifications.assignSuppliersSuccess", { count: assignedCount }),
    );
    resetAssignmentForm();
  } catch (error) {
    console.error(t("purchasingGroups.errors.assignSuppliersFailure"), error);
    notification.error(t("purchasingGroups.errors.assignSuppliersFailure"));
  }
};

const removeMember = async (buyerId: string) => {
  if (!currentGroup.value) return;

  try {
    await notification.confirm(t("purchasingGroups.removeMemberConfirm"), t("common.confirm"), {
      confirmButtonText: t("common.remove"),
      cancelButtonText: t("common.cancel"),
      type: "warning",
    });
    await groupsStore.removeMember(currentGroup.value.id, buyerId);
    notification.success(t("purchasingGroups.notifications.removeMemberSuccess"));
  } catch (error: any) {
    if (error !== "cancel") {
      console.error(t("purchasingGroups.errors.removeMemberFailure"), error);
      notification.error(t("purchasingGroups.errors.removeMemberFailure"));
    }
  }
};

const removeSupplier = async (supplierId: number) => {
  if (!canManageSuppliers.value || !currentGroup.value) return;

  try {
    await notification.confirm(t("purchasingGroups.removeSupplierConfirm"), t("common.confirm"), {
      confirmButtonText: t("common.remove"),
      cancelButtonText: t("common.cancel"),
      type: "warning",
    });
    await groupsStore.removeSupplier(currentGroup.value.id, supplierId);
    notification.success(t("purchasingGroups.notifications.removeSupplierSuccess"));
  } catch (error: any) {
    if (error !== "cancel") {
      console.error(t("purchasingGroups.errors.removeSupplierFailure"), error);
      notification.error(t("purchasingGroups.errors.removeSupplierFailure"));
    }
  }
};

const removeAssignment = async (assignmentId: number) => {
  if (!currentGroup.value) {
    return;
  }

  try {
    await notification.confirm(
      t("purchasingGroups.removeAssignmentConfirm"),
      t("common.confirm"),
      {
        confirmButtonText: t("common.remove"),
        cancelButtonText: t("common.cancel"),
        type: "warning",
      },
    );
    await groupsStore.removeBuyerAssignment(currentGroup.value.id, assignmentId);
    notification.success(t("purchasingGroups.notifications.removeAssignmentSuccess"));
  } catch (error: any) {
    if (error !== "cancel") {
      console.error(t("purchasingGroups.errors.removeAssignmentFailure"), error);
      notification.error(t("purchasingGroups.errors.removeAssignmentFailure"));
    }
  }
};

const fetchBuyers = async () => {
  try {
    const response = await usersApi.listUsers();
    availableBuyers.value = response.filter((user: any) => user.role === "purchaser");
  } catch (error) {
    console.error(t("purchasingGroups.errors.loadBuyersFailure"), error);
  }
};

onMounted(async () => {
  await Promise.all([groupsStore.fetchGroups(), fetchBuyers()]);
  const manageableGroupIds = groups.value
    .filter((group) => canManageGroup(group.id))
    .map((group) => group.id);
  if (manageableGroupIds.length > 0) {
    void groupsStore.prefetchGroupAssociations(manageableGroupIds);
  }
});




</script>

<style scoped>
.purchasing-groups-view {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}




.filters-bar {
  display: flex;
  gap: 12px;
  margin-bottom: 20px;
  align-items: center;
}

.filter-group {
  display: flex;
  gap: 12px;
  flex: 1;
  justify-content: flex-end;
}

.groups-card {
  margin-bottom: 24px;
}

.manage-section {
  padding: 16px 0;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.section-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.assignment-hint {
  margin-bottom: 16px;
}

.assignment-form {
  margin-bottom: 16px;
}

.assignment-actions {
  margin-bottom: 16px;
}

.no-access-text {
  color: #c0c4cc;
  font-size: 12px;
}
</style>

