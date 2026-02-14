<template>
  <div class="org-units-view">
    <PageHeader
      title="组织单元管理"
      subtitle="维护企业组织结构，支持最多 5 级层级体系"
    >
      <template #actions>
        <el-button type="primary" :disabled="!canCreateRoot" @click="startCreateRoot">
          <el-icon><Plus /></el-icon>
          新建根节点
        </el-button>
      </template>
    </PageHeader>

    <div class="filters-bar">
      <el-input
        v-model="searchQuery"
        placeholder="搜索组织单元名称或编码"
        clearable
        style="max-width: 360px"
      >
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
      </el-input>

      <div class="filter-group">
        <el-select v-model="filters.type" placeholder="类型" clearable style="width: 160px">
          <el-option label="全部类型" value="" />
          <el-option label="常规" value="general" />
          <el-option label="部门" value="department" />
          <el-option label="分部" value="division" />
          <el-option label="采购" value="procurement" />
        </el-select>

        <el-radio-group v-model="viewMode">
          <el-radio-button value="tree">树形</el-radio-button>
          <el-radio-button value="table">平铺</el-radio-button>
        </el-radio-group>
      </div>
    </div>

    <el-card v-loading="loading" class="units-card">
      <el-tree
        v-if="viewMode === 'tree'"
        :data="treeData"
        node-key="id"
        :props="{ label: 'name', children: 'children' }"
        default-expand-all
        :expand-on-click-node="false"
      >
        <template #default="{ data }">
          <div class="tree-node">
            <span class="node-info">
              <strong>{{ data.name }}</strong>
              <el-tag size="small" :type="getTypeTagType(data.type)">
                {{ getTypeLabel(data.type) }}
              </el-tag>
              <el-tag size="small">L{{ data.level }}</el-tag>
              <el-tag v-if="data.memberCount" size="small"> {{ data.memberCount }} 成员 </el-tag>
            </span>
            <span class="node-actions">
              <el-button link type="primary" size="small" @click="manageUnit(data)">
                管理
              </el-button>
              <el-button
                link
                type="primary"
                size="small"
                :disabled="!canEditUnit(data)"
                @click="editUnit(data)"
              >
                编辑
              </el-button>
              <el-button
                link
                type="primary"
                size="small"
                :disabled="!canMoveUnit(data)"
                @click="openMoveDialog(data)"
              >
                移动
              </el-button>
              <el-button
                link
                type="warning"
                size="small"
                :disabled="!canAddChild(data)"
                @click="addChildUnit(data)"
              >
                创建子节点
              </el-button>
              <el-button
                link
                type="danger"
                size="small"
                :disabled="!canEditUnit(data)"
                @click="confirmDelete(data)"
              >
                删除
              </el-button>
            </span>
          </div>
        </template>
      </el-tree>

      <el-table v-else :data="tableUnits" stripe @row-click="manageUnit">
        <el-table-column prop="code" label="编码" width="140" />
        <el-table-column prop="name" label="名称" min-width="220" />
        <el-table-column prop="type" label="类型" width="120">
          <template #default="{ row }">
            <el-tag :type="getTypeTagType(row.type)" size="small">
              {{ getTypeLabel(row.type) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="level" label="层级" width="80" align="center" />
        <el-table-column label="成员数" width="100" align="center">
          <template #default="{ row }">
            {{ row.memberCount ?? 0 }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="300" fixed="right">
          <template #default="{ row }">
            <el-space wrap>
              <el-button link type="primary" size="small" @click.stop="manageUnit(row)">
                管理
              </el-button>
              <el-button
                link
                type="primary"
                size="small"
                :disabled="!canEditUnit(row)"
                @click.stop="editUnit(row)"
              >
                编辑
              </el-button>
              <el-button
                link
                type="primary"
                size="small"
                :disabled="!canMoveUnit(row)"
                @click.stop="openMoveDialog(row)"
              >
                移动
              </el-button>
              <el-button
                link
                type="warning"
                size="small"
                :disabled="!canAddChild(row)"
                @click.stop="addChildUnit(row)"
              >
                创建子节点
              </el-button>
              <el-button
                link
                type="danger"
                size="small"
                :disabled="!canEditUnit(row)"
                @click.stop="confirmDelete(row)"
              >
                删除
              </el-button>
            </el-space>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog
      v-model="showCreateDialog"
      :title="editingUnit ? '编辑组织单元' : '创建组织单元'"
      width="520px"
      destroy-on-close
    >
      <el-form ref="unitFormRef" :model="unitForm" :rules="unitRules" label-width="96px">
        <el-form-item label="编码" prop="code">
          <el-input
            v-model="unitForm.code"
            placeholder="唯一编码，例如 HQ、CN-BJ"
            :disabled="!!editingUnit"
          />
        </el-form-item>
        <el-form-item label="名称" prop="name">
          <el-input v-model="unitForm.name" placeholder="组织单元名称" />
        </el-form-item>
        <el-form-item label="类型">
          <el-select v-model="unitForm.type" style="width: 100%">
            <el-option label="常规" value="general" />
            <el-option label="部门" value="department" />
            <el-option label="分部" value="division" />
            <el-option label="采购" value="procurement" />
          </el-select>
        </el-form-item>
        <el-form-item label="上级节点">
          <el-cascader
            v-model="unitForm.parentId"
            :options="parentOptions"
            :props="cascaderProps"
            clearable
            placeholder="留空创建根节点"
            :disabled="!!editingUnit"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="unitForm.description" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item v-if="isSystemAdmin" label="节点管理员">
          <el-select
            v-model="unitForm.adminId"
            filterable
            clearable
            placeholder="可选，留空则暂不指定"
            :loading="userOptionsLoading"
            style="width: 100%"
            @visible-change="handleAdminSelectVisible"
          >
            <el-option
              v-for="option in userOptions"
              :key="option.id"
              :label="formatUserOption(option)"
              :value="option.id"
            />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="cancelUnitDialog">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitUnit">
          {{ editingUnit ? "保存" : "创建" }}
        </el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="showManageDialog"
      :title="currentUnit ? `管理：${currentUnit.name}` : '管理组织单元'"
      width="920px"
      destroy-on-close
    >
      <section class="admin-panel">
        <div class="admin-info">
          <span class="label">节点管理员：</span>
          <span class="value">{{ adminDisplay }}</span>
        </div>
        <div v-if="isSystemAdmin" class="admin-editor">
          <el-select
            v-model="adminSelection"
            size="small"
            filterable
            clearable
            placeholder="选择管理员"
            :loading="userOptionsLoading"
            style="width: 260px"
            @visible-change="handleAdminSelectVisible"
          >
            <el-option
              v-for="option in userOptions"
              :key="option.id"
              :label="formatUserOption(option)"
              :value="option.id"
            />
          </el-select>
          <el-button
            type="primary"
            size="small"
            :loading="savingAdmin"
            :disabled="adminSelectionChanged"
            @click="saveAdminAssignment"
          >
            保存
          </el-button>
        </div>
      </section>

      <el-tabs v-model="activeTab">
        <el-tab-pane label="成员" name="members">
          <div class="manage-section">
            <div class="section-header">
              <h3>成员列表</h3>
              <el-button
                v-if="canManageCurrentUnit"
                size="small"
                type="primary"
                @click="openAddMembersDialog"
              >
                添加成员
              </el-button>
            </div>
            <el-table :data="members" v-loading="loadingMembers" stripe>
              <el-table-column prop="userName" label="姓名" />
              <el-table-column prop="userRole" label="平台角色" width="160" />
              <el-table-column prop="role" label="组内角色" width="120">
                <template #default="{ row }">
                  <el-tag
                    size="small"
                    :type="
                      row.role === 'admin' ? 'danger' : row.role === 'lead' ? 'warning' : 'info'
                    "
                  >
                    {{ row.role }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column label="操作" width="120" align="center">
                <template #default="{ row }">
                  <el-button
                    v-if="canManageCurrentUnit"
                    link
                    type="danger"
                    size="small"
                    @click="removeMember(row.userId)"
                  >
                    移除
                  </el-button>
                </template>
              </el-table-column>
            </el-table>
          </div>
        </el-tab-pane>
        <el-tab-pane label="供应商" name="suppliers">
          <div class="manage-section">
            <div class="section-header">
              <h3>供应商列表</h3>
              <el-button size="small" type="primary" @click="showAddSuppliersDialog = true">
                添加供应商
              </el-button>
            </div>
            <el-table :data="suppliers" v-loading="loadingSuppliers" stripe>
              <el-table-column prop="companyName" label="供应商名称" />
              <el-table-column prop="companyId" label="编号" width="160" />
              <el-table-column prop="status" label="状态" width="120" />
              <el-table-column label="操作" width="120" align="center">
                <template #default="{ row }">
                  <el-button
                    link
                    type="danger"
                    size="small"
                    @click="removeSupplier(row.supplierId)"
                  >
                    移除
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
      title="添加成员"
      width="520px"
      @close="closeAddMembersDialog"
    >
      <el-form label-width="80px">
        <el-form-item label="成员" required>
          <el-select
            v-model="addMemberForm.userIds"
            multiple
            filterable
            clearable
            placeholder="请选择要添加的成员"
            :loading="userOptionsLoading"
            style="width: 100%"
            @visible-change="handleAdminSelectVisible"
          >
            <el-option
              v-for="option in selectableMemberOptions"
              :key="option.id"
              :label="formatUserOption(option)"
              :value="option.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="组内角色">
          <el-select v-model="addMemberForm.role" style="width: 100%">
            <el-option label="成员" value="member" />
            <el-option label="负责人" value="lead" />
            <el-option label="管理员" value="admin" />
          </el-select>
        </el-form-item>
        <el-form-item label="备注">
          <el-input
            v-model="addMemberForm.notes"
            type="textarea"
            :rows="3"
            placeholder="可选备注"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="closeAddMembersDialog">取消</el-button>
        <el-button
          type="primary"
          :loading="addMemberSubmitting"
          :disabled="addMemberForm.userIds.length === 0"
          @click="confirmAddMembers"
        >
          添加
        </el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showAddSuppliersDialog" title="添加供应商" width="480px">
      <p>此演示版本未实现供应商选择，请在集成环境中接入供应商选择器。</p>
      <template #footer>
        <el-button @click="showAddSuppliersDialog = false">知道了</el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="showMoveDialog"
      :title="moveTargetUnit ? `移动节点：${moveTargetUnit.name}` : '移动节点'"
      width="420px"
      @close="closeMoveDialog"
    >
      <el-form label-width="96px">
        <el-form-item label="新的上级节点">
          <el-tree-select
            v-model="moveParentId"
            :data="moveParentOptions"
            :props="treeSelectProps"
            check-strictly
            clearable
            placeholder="选择新的上级节点（留空移动为根节点）"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="closeMoveDialog">取消</el-button>
        <el-button type="primary" :loading="moveSubmitting" @click="confirmMove"> 确认 </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { computed, nextTick, onMounted, ref, watch } from "vue";
import { type FormInstance, type FormRules } from "element-plus";
import { Plus, Search } from "@element-plus/icons-vue";
import PageHeader from "@/components/layout/PageHeader.vue";
import { storeToRefs } from "pinia";
import { useOrganizationalUnitsStore } from "@/stores/organizationalUnits";
import { useAuthStore } from "@/stores/auth";
import type { AddMembersPayload, OrganizationalUnit } from "@/api/organizationalUnits";
import { listUsers, type User as ApiUser } from "@/api/users";


import { useNotification } from "@/composables";

const notification = useNotification();
interface UserOption {
  id: string;
  name: string;
  role: string;
}

interface UnitForm {
  code: string;
  name: string;
  type: OrganizationalUnit["type"];
  parentId: number | null;
  description: string;
  adminId: string | null;
}

const ROLE_LABELS: Record<string, string> = {
  admin: "系统管理员",
  purchaser: "采购员",
  procurement_manager: "采购经理",
  procurement_director: "采购总监",
  finance_accountant: "财务会计",
  finance_director: "财务总监",
  quality_manager: "SQE",
  temp_supplier: "临时供应商",
  formal_supplier: "正式供应商",
};

const EXCLUDED_USER_ROLES = new Set(["temp_supplier", "formal_supplier", "supplier"]);

const cascaderProps = { value: "id", label: "name", children: "children", checkStrictly: true };
const treeSelectProps = { value: "id", label: "name", children: "children", disabled: "disabled" };

const authStore = useAuthStore();
const orgUnitsStore = useOrganizationalUnitsStore();
const { units, currentUnit, members, suppliers, loading, loadingMembers, loadingSuppliers } =
  storeToRefs(orgUnitsStore);

const searchQuery = ref("");
const filters = ref({ type: "" });
const viewMode = ref<"tree" | "table">("tree");

const showCreateDialog = ref(false);
const showManageDialog = ref(false);
const showAddMembersDialog = ref(false);
const showAddSuppliersDialog = ref(false);
const showMoveDialog = ref(false);

const activeTab = ref("members");
const submitting = ref(false);
const moveSubmitting = ref(false);

const editingUnit = ref<OrganizationalUnit | null>(null);
const moveTargetUnit = ref<OrganizationalUnit | null>(null);
const moveParentId = ref<number | null>(null);

const unitFormRef = ref<FormInstance>();

const createEmptyUnitForm = (): UnitForm => ({
  code: "",
  name: "",
  type: "general",
  parentId: null,
  description: "",
  adminId: null,
});

const unitForm = ref<UnitForm>(createEmptyUnitForm());

const unitRules = computed<FormRules>(() => ({
  code: [{ required: true, message: "编码不能为空", trigger: "blur" }],
  name: [{ required: true, message: "名称不能为空", trigger: "blur" }],
}));

const userOptions = ref<UserOption[]>([]);
const userOptionsLoading = ref(false);
const userOptionsLoaded = ref(false);

const adminSelection = ref<string | null>(null);
const savingAdmin = ref(false);

const addMemberSubmitting = ref(false);
const addMemberForm = ref<{ userIds: string[]; role: AddMembersPayload["role"]; notes: string }>({
  userIds: [],
  role: "member",
  notes: "",
});

const canManage = computed(() => {
  const user = authStore.user;
  if (!user) return false;
  if (user.role === "admin") return true;
  return user.permissions?.includes("admin.org_units.manage") ?? false;
});

const isSystemAdmin = computed(() => authStore.user?.role === "admin");

const userAdminUnitIds = computed(() => {
  const ids = new Set<number>();
  for (const unit of authStore.user?.adminUnits ?? []) {
    ids.add(Number(unit.id));
  }
  return ids;
});

function isUserUnitAdmin(unit: OrganizationalUnit | null): boolean {
  if (!unit) return false;
  const adminIds = userAdminUnitIds.value;
  if (adminIds.has(unit.id)) {
    return true;
  }
  if (unit.path) {
    return unit.path
      .split("/")
      .filter(Boolean)
      .map((value) => Number(value))
      .some((id) => adminIds.has(id));
  }
  return false;
}

const canManageCurrentUnit = computed(() => canManage.value || isUserUnitAdmin(currentUnit.value));
const canCreateRoot = computed(() => isSystemAdmin.value || canManage.value);

const currentAdminId = computed(() => currentUnit.value?.adminIds?.[0] ?? null);

const adminDisplay = computed(() => {
  const adminId = currentAdminId.value;
  if (!adminId) {
    return "未指定";
  }
  const option = userOptions.value.find((item) => item.id === adminId);
  if (option) {
    return formatUserOption(option);
  }
  return adminId;
});

const filteredUnits = computed(() => {
  let result = units.value.slice();

  if (filters.value.type) {
    result = result.filter((item) => item.type === filters.value.type);
  }

  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase();
    result = result.filter(
      (item) => item.name.toLowerCase().includes(query) || item.code.toLowerCase().includes(query),
    );
  }

  return result;
});

const tableUnits = computed(() => filteredUnits.value);

const treeData = computed(() => {
  const buildTree = (parentId: number | null): OrganizationalUnit[] => {
    return filteredUnits.value
      .filter((unit) => unit.parentId === parentId)
      .map((unit) => ({
        ...unit,
        children: buildTree(unit.id),
      }));
  };
  return buildTree(null);
});

const parentOptions = computed(() => treeData.value);

const moveParentOptions = computed(() => {
  const options = treeData.value;
  if (!moveTargetUnit.value) {
    return options;
  }
  const blockedIds = new Set<number>([moveTargetUnit.value.id]);
  if (moveTargetUnit.value.path) {
    moveTargetUnit.value.path
      .split("/")
      .filter(Boolean)
      .map((segment) => Number(segment))
      .forEach((id) => blockedIds.add(id));
  }
  for (const unit of units.value) {
    if (!unit.path) continue;
    const segments = unit.path
      .split("/")
      .filter(Boolean)
      .map((segment) => Number(segment));
    if (segments.includes(moveTargetUnit.value.id)) {
      blockedIds.add(unit.id);
    }
  }

  const clone = (nodes: OrganizationalUnit[]): any[] =>
    nodes.map((node) => ({
      ...node,
      disabled: blockedIds.has(node.id),
      children: node.children ? clone(node.children as OrganizationalUnit[]) : undefined,
    }));

  return clone(options);
});

const memberIdSet = computed(() => new Set(members.value.map((member) => String(member.userId))));

const selectableMemberOptions = computed(() =>
  userOptions.value.filter((option) => !memberIdSet.value.has(option.id)),
);

function formatRole(role: string): string {
  return ROLE_LABELS[role] ?? role;
}

function formatUserOption(option: UserOption): string {
  return `${option.name}（${formatRole(option.role)} · ${option.id}）`;
}

function ensureOptionPresence(id: string | null) {
  if (!id) {
    return;
  }
  if (!userOptions.value.some((item) => item.id === id)) {
    userOptions.value.push({ id, name: id, role: "unknown" });
  }
}

async function ensureUserOptions(force = false) {
  if (userOptionsLoaded.value && !force) {
    return;
  }
  userOptionsLoading.value = true;
  try {
    const users = await listUsers();
    const filtered = users
      .filter((user: ApiUser) => !EXCLUDED_USER_ROLES.has(String(user.role)))
      .map((user: ApiUser) => ({
        id: String(user.id ?? ""),
        name: user.name ?? String(user.id ?? ""),
        role: String(user.role ?? "unknown"),
      }))
      .sort((a, b) => a.name.localeCompare(b.name, "zh-CN"));
    userOptions.value = filtered;
    userOptionsLoaded.value = true;
  } catch (error) {
    console.error("Failed to load users for selection", error);
    notification.error((error as any)?.message ?? "加载用户列表失败");
  } finally {
    userOptionsLoading.value = false;
  }
}

function canEditUnit(unit: OrganizationalUnit): boolean {
  return canManage.value || isUserUnitAdmin(unit);
}

function canMoveUnit(unit: OrganizationalUnit): boolean {
  return canEditUnit(unit);
}

function canAddChild(unit: OrganizationalUnit): boolean {
  if (unit.level >= 5) {
    return false;
  }
  return canEditUnit(unit);
}

function canManageUnit(unit: OrganizationalUnit): boolean {
  return canManage.value || isUserUnitAdmin(unit);
}

function getTypeLabel(type: string): string {
  const map: Record<string, string> = {
    general: "常规",
    department: "部门",
    division: "分部",
    procurement: "采购",
  };
  return map[type] ?? type;
}

function getTypeTagType(type: string): string {
  const map: Record<string, string> = {
    general: "info",
    department: "success",
    division: "warning",
    procurement: "danger",
  };
  return map[type] ?? "info";
}

function resetUnitForm() {
  unitForm.value = createEmptyUnitForm();
}

function resetAddMemberForm() {
  addMemberForm.value = { userIds: [], role: "member", notes: "" };
}

function cancelUnitDialog() {
  showCreateDialog.value = false;
  editingUnit.value = null;
  resetUnitForm();
  unitFormRef.value?.resetFields();
}

async function startCreateRoot() {
  if (!canCreateRoot.value) {
    notification.warning("当前账号无权创建根节点");
    return;
  }
  editingUnit.value = null;
  resetUnitForm();
  if (isSystemAdmin.value) {
    await ensureUserOptions();
  }
  showCreateDialog.value = true;
  nextTick(() => unitFormRef.value?.clearValidate());
}

async function addChildUnit(parent: OrganizationalUnit) {
  if (!canManageUnit(parent)) {
    notification.warning("当前账号无权在该节点下创建子节点");
    return;
  }
  editingUnit.value = null;
  resetUnitForm();
  unitForm.value.parentId = parent.id;
  if (isSystemAdmin.value) {
    await ensureUserOptions();
  }
  showCreateDialog.value = true;
  nextTick(() => unitFormRef.value?.clearValidate());
}

async function editUnit(unit: OrganizationalUnit) {
  if (!canEditUnit(unit)) {
    notification.warning("当前账号无权编辑该节点");
    return;
  }
  editingUnit.value = unit;
  unitForm.value = {
    code: unit.code,
    name: unit.name,
    type: unit.type,
    parentId: unit.parentId ?? null,
    description: unit.description ?? "",
    adminId: currentAdminId.value,
  };
  if (isSystemAdmin.value) {
    await ensureUserOptions();
    ensureOptionPresence(unitForm.value.adminId);
  }
  showCreateDialog.value = true;
  nextTick(() => unitFormRef.value?.clearValidate());
}

async function submitUnit() {
  if (!unitFormRef.value) {
    return;
  }
  await unitFormRef.value.validate(async (valid) => {
    if (!valid) {
      return;
    }
    submitting.value = true;
    try {
      if (editingUnit.value) {
        await orgUnitsStore.updateUnit(editingUnit.value.id, {
          name: unitForm.value.name,
          type: unitForm.value.type,
          description: unitForm.value.description,
          adminId: isSystemAdmin.value ? (unitForm.value.adminId ?? null) : undefined,
        });
        notification.success("更新成功");
      } else {
        await orgUnitsStore.createUnit({
          code: unitForm.value.code,
          name: unitForm.value.name,
          type: unitForm.value.type,
          parentId: unitForm.value.parentId,
          description: unitForm.value.description,
          adminId: isSystemAdmin.value ? (unitForm.value.adminId ?? null) : undefined,
        });
        notification.success("创建成功");
      }
      cancelUnitDialog();
    } catch (error: any) {
      notification.error(error?.response?.data?.message ?? "操作失败");
    } finally {
      submitting.value = false;
    }
  });
}

async function manageUnit(unit: OrganizationalUnit) {
  try {
    await orgUnitsStore.fetchUnit(unit.id);
    activeTab.value = "members";
    if (isSystemAdmin.value) {
      await ensureUserOptions();
      ensureOptionPresence(currentAdminId.value);
    }
    adminSelection.value = currentAdminId.value;
    showManageDialog.value = true;
  } catch (error) {
    notification.error("加载组织单元失败");
  }
}

async function confirmDelete(unit: OrganizationalUnit) {
  if (!canEditUnit(unit)) {
    notification.warning("当前账号无权删除该节点");
    return;
  }
  try {
    const check = await orgUnitsStore.checkDeletion(unit.id);
    if (!check.canDelete) {
      notification.alert(check.message ?? "该节点存在依赖，无法删除", "无法删除", {
        type: "warning",
      });
      return;
    }
    await notification.confirm(`确认删除组织单元 “${unit.name}” 吗？此操作不可撤销。`, "确认删除", {
      type: "warning",
    });
    await orgUnitsStore.deleteUnit(unit.id);
    notification.success("删除成功");
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error?.response?.data?.message ?? "删除失败");
    }
  }
}

async function openMoveDialog(unit: OrganizationalUnit) {
  if (!canMoveUnit(unit)) {
    notification.warning("当前账号无权移动该节点");
    return;
  }
  moveTargetUnit.value = unit;
  moveParentId.value = unit.parentId ?? null;
  showMoveDialog.value = true;
}

function closeMoveDialog() {
  showMoveDialog.value = false;
  moveTargetUnit.value = null;
  moveParentId.value = null;
  moveSubmitting.value = false;
}

async function confirmMove() {
  if (!moveTargetUnit.value) {
    return;
  }
  moveSubmitting.value = true;
  try {
    await orgUnitsStore.moveUnit(moveTargetUnit.value.id, moveParentId.value ?? null);
    notification.success("移动成功");
    closeMoveDialog();
  } catch (error: any) {
    notification.error(error?.response?.data?.message ?? "移动失败");
  } finally {
    moveSubmitting.value = false;
  }
}

async function openAddMembersDialog() {
  if (!canManageCurrentUnit.value) {
    notification.warning("当前账号无权维护该节点成员");
    return;
  }
  await ensureUserOptions();
  ensureOptionPresence(currentAdminId.value);
  resetAddMemberForm();
  showAddMembersDialog.value = true;
}

function closeAddMembersDialog() {
  showAddMembersDialog.value = false;
  resetAddMemberForm();
}

async function confirmAddMembers() {
  if (!currentUnit.value || addMemberForm.value.userIds.length === 0) {
    return;
  }
  addMemberSubmitting.value = true;
  try {
    const payload: AddMembersPayload = {
      userIds: [...addMemberForm.value.userIds],
      role: addMemberForm.value.role,
    };
    const note = addMemberForm.value.notes.trim();
    if (note) {
      payload.notes = note;
    }
    await orgUnitsStore.addMembers(currentUnit.value.id, payload);
    notification.success("添加成员成功");
    closeAddMembersDialog();
  } catch (error) {
    notification.error((error as any)?.response?.data?.message ?? "添加失败");
  } finally {
    addMemberSubmitting.value = false;
  }
}

async function removeMember(userId: string) {
  if (!currentUnit.value) {
    return;
  }
  try {
    await notification.confirm("确认移除该成员吗？", "提示", { type: "warning" });
    await orgUnitsStore.removeMember(currentUnit.value.id, userId);
    notification.success("移除成功");
  } catch (error) {
    if (error !== "cancel") {
      notification.error("移除失败");
    }
  }
}

async function removeSupplier(supplierId: number) {
  if (!currentUnit.value) {
    return;
  }
  try {
    await notification.confirm("确认移除该供应商吗？", "提示", { type: "warning" });
    await orgUnitsStore.removeSupplier(currentUnit.value.id, supplierId);
    notification.success("移除成功");
  } catch (error) {
    if (error !== "cancel") {
      notification.error("移除失败");
    }
  }
}

const adminSelectionChanged = computed(() => adminSelection.value === currentAdminId.value);

async function saveAdminAssignment() {
  if (!currentUnit.value || !isSystemAdmin.value) {
    return;
  }
  savingAdmin.value = true;
  try {
    await orgUnitsStore.updateUnit(currentUnit.value.id, {
      adminId: adminSelection.value ?? null,
    });
    notification.success("管理员已更新");
    await orgUnitsStore.fetchUnit(currentUnit.value.id);
    ensureOptionPresence(currentAdminId.value);
    adminSelection.value = currentAdminId.value;
  } catch (error) {
    notification.error((error as any)?.response?.data?.message ?? "更新失败");
  } finally {
    savingAdmin.value = false;
  }
}

function handleAdminSelectVisible(visible: boolean) {
  if (visible && isSystemAdmin.value) {
    ensureUserOptions(true).then(() => ensureOptionPresence(adminSelection.value));
  }
}

watch(currentUnit, (unit) => {
  if (unit) {
    adminSelection.value = unit.adminIds?.[0] ?? null;
  } else {
    adminSelection.value = null;
  }
});

watch(showManageDialog, (visible) => {
  if (!visible) {
    adminSelection.value = null;
  }
});

onMounted(async () => {
  await orgUnitsStore.fetchUnits();
});




</script>

<style scoped>
.org-units-view {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.org-units-view :deep(.page-header) {
  margin-bottom: 24px;
  color: #fff;
}

.org-units-view :deep(.page-header__actions) {
  display: inline-flex;
  gap: 12px;
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
  margin-left: auto;
}

.units-card {
  margin-bottom: 24px;
}

.tree-node {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-right: 8px;
}

.tree-node .node-info {
  display: flex;
  gap: 8px;
  align-items: center;
}

.tree-node .node-actions {
  display: flex;
  gap: 6px;
}

.manage-section {
  padding: 12px 0;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.section-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
}

.admin-panel {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 0 4px;
  gap: 16px;
}

.admin-info .label {
  color: #606266;
}

.admin-info .value {
  font-weight: 500;
}

.admin-editor {
  display: flex;
  align-items: center;
  gap: 8px;
}
</style>
