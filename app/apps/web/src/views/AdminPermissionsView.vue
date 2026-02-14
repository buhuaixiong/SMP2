<template>
  <div class="admin-permissions">
    <header class="page-header">
      <div>
        <h1>{{ t("adminPermissions.title") }}</h1>
        <p class="muted">{{ t("adminPermissions.subtitle") }}</p>
      </div>
      <div class="header-actions">
        <button class="link-btn" type="button" @click="refresh">
          {{ t("adminPermissions.actions.refresh") }}
        </button>
        <button class="primary-btn" type="button" @click="openCreateDialog">
          + {{ t("adminPermissions.actions.createUser") }}
        </button>
      </div>
    </header>

    <nav class="mode-toggle">
      <button
        type="button"
        :class="{ active: activeTab === 'internal' }"
        @click="activeTab = 'internal'"
      >
        {{ t("adminPermissions.tabs.internal") }}
      </button>
      <button
        type="button"
        :class="{ active: activeTab === 'delegations' }"
        @click="activeTab = 'delegations'"
      >
        {{ t("adminPermissions.tabs.delegations") }}
      </button>
    </nav>

    <section v-if="activeTab === 'internal'" class="panel-stack">
      <section class="panel" v-if="internalRoles.length">
        <h2>{{ t("adminPermissions.internal.availableRoles") }}</h2>
        <ul class="role-list">
          <li v-for="role in internalRoles" :key="role.role">{{ role.label }} ({{ role.role }})</li>
        </ul>
      </section>

      <section class="panel">
        <div class="section-header">
          <h2>{{ t("adminPermissions.internal.users") }}</h2>
          <span v-if="permissionStore.loading" class="muted">{{
            t("adminPermissions.internal.loading")
          }}</span>
        </div>
        <table class="user-table">
          <thead>
            <tr>
              <th>{{ t("adminPermissions.internal.table.userId") }}</th>
              <th>{{ t("adminPermissions.internal.table.name") }}</th>
              <th>{{ t("adminPermissions.internal.table.email") }}</th>
              <th>{{ t("adminPermissions.internal.table.status") }}</th>
              <th>{{ t("adminPermissions.internal.table.role") }}</th>
              <th>{{ t("adminPermissions.internal.table.changeRole") }}</th>
              <th>{{ t("adminPermissions.internal.table.actions") }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in internalUsers" :key="user.id">
              <td>{{ user.id }}</td>
              <td>{{ user.name || t("common.userFallback") }}</td>
              <td class="email-cell">
                <input
                  v-model="emailDrafts[user.id]"
                  type="email"
                  :placeholder="t('adminPermissions.internal.table.emailPlaceholder')"
                  :disabled="emailSavingId === user.id || user.status === 'deleted'"
                  @blur="submitEmailUpdate(user)"
                  @keydown.enter.prevent="submitEmailUpdate(user)"
                />
              </td>
              <td>
                <span
                  class="status-pill"
                  :class="`status-${user.status || 'active'}`"
                >
                  {{ formatUserStatus(user.status) }}
                </span>
              </td>
              <td class="current-role">{{ user.role }}</td>
              <td class="role-cell">
                <select
                  :value="user.role"
                  :disabled="isSupplierRole(user.role)"
                  @change="(event) => updateRole(user, (event.target as HTMLSelectElement).value)"
                >
                  <option v-for="role in roleOptionsForUser(user)" :key="role.role" :value="role.role">
                    {{ role.label }}
                  </option>
                </select>
                <p v-if="isSupplierRole(user.role)" class="micro-text">
                  {{ t("adminPermissions.internal.managedAutomatically") }}
                </p>
              </td>
              <td class="actions-cell">
                <button
                  class="link-btn reset-password-btn"
                  type="button"
                  @click="openPasswordResetDialog(user)"
                  :disabled="resettingPasswordUserId === user.id || user.status === 'deleted'"
                >
                  {{
                    resettingPasswordUserId === user.id
                      ? t("adminPermissions.actions.resetting")
                      : t("adminPermissions.actions.resetPassword")
                  }}
                </button>
                <button
                  v-if="user.status !== 'frozen'"
                  class="link-btn"
                  type="button"
                  :disabled="user.status === 'deleted' || isSelfUser(user)"
                  @click="openFreezeDialog(user)"
                >
                  {{ t("adminPermissions.actions.freeze") }}
                </button>
                <button
                  v-else
                  class="link-btn"
                  type="button"
                  :disabled="unfreezingUserId === user.id"
                  @click="handleUnfreeze(user)"
                >
                  {{
                    unfreezingUserId === user.id
                      ? t("adminPermissions.actions.unfreezing")
                      : t("adminPermissions.actions.unfreeze")
                  }}
                </button>
                <button
                  class="link-btn danger"
                  type="button"
                  :disabled="user.status === 'deleted' || isSelfUser(user)"
                  @click="openDeleteDialog(user)"
                >
                  {{ t("adminPermissions.actions.delete") }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </section>
    </section>

    <section v-else class="panel delegations-panel">
      <div class="section-header">
        <h2>{{ t("adminPermissions.delegations.title") }}</h2>
        <button class="link-btn" type="button" @click="loadBuyers">
          {{ t("adminPermissions.actions.reloadBuyers") }}
        </button>
      </div>
      <div class="buyer-assignment">
        <aside class="buyer-list">
          <p v-if="buyers.length === 0" class="placeholder">
            {{ t("adminPermissions.delegations.noBuyers") }}
          </p>
          <ul v-else>
            <li
              v-for="buyer in buyers"
              :key="buyer.id"
              :class="{ active: buyer.id === selectedBuyerId }"
            >
              <button type="button" @click="selectBuyer(buyer.id)">
                <span class="buyer-name">{{ buyer.name || buyer.id }}</span>
                <span class="buyer-meta">{{ formatBuyerStats(buyer) }}</span>
              </button>
            </li>
          </ul>
        </aside>

        <div v-if="selectedBuyer" class="assignment-detail">
          <header class="assignment-header">
            <div>
              <h3>{{ selectedBuyer.name || selectedBuyer.id }}</h3>
              <p class="muted">
                {{ t("adminPermissions.delegations.instructions") }}
              </p>
            </div>
            <button
              class="primary-btn"
              type="button"
              :disabled="!assignmentDirty || savingAssignments"
              @click="saveAssignments"
            >
              {{
                savingAssignments
                  ? t("adminPermissions.actions.saving")
                  : t("adminPermissions.actions.saveChanges")
              }}
            </button>
          </header>

          <div class="assignment-body">
            <section class="current-assignments">
              <h4>{{ t("adminPermissions.delegations.responsibleSuppliers") }}</h4>
              <p v-if="assignmentsLoading" class="muted">
                {{ t("adminPermissions.delegations.loadingAssignments") }}
              </p>
              <ul v-else-if="editableAssignments.length" class="assignment-list">
                <li v-for="assignment in editableAssignments" :key="assignment.supplierId">
                  <div class="assignment-info">
                    <strong>{{ assignment.companyName }}</strong>
                    <span class="muted">#{{ assignment.companyId }}</span>
                  </div>
                  <div class="assignment-permissions">
                    <label>
                      <input
                        type="checkbox"
                        v-model="assignment.profileAccess"
                        @change="markAssignmentsDirty"
                      />
                      {{ t("adminPermissions.delegations.profileAccess") }}
                    </label>
                    <label>
                      <input
                        type="checkbox"
                        v-model="assignment.contractAlert"
                        @change="markAssignmentsDirty"
                      />
                      {{ t("adminPermissions.delegations.contractAlert") }}
                    </label>
                  </div>
                  <button
                    class="link-btn remove"
                    type="button"
                    @click="removeAssignment(assignment.supplierId)"
                  >
                    {{ t("adminPermissions.actions.remove") }}
                  </button>
                </li>
              </ul>
              <p v-else class="placeholder">
                {{ t("adminPermissions.delegations.noAssignments") }}
              </p>
            </section>

            <section class="supplier-search">
              <h4>{{ t("adminPermissions.delegations.addSuppliers") }}</h4>

              <!-- Add by Tags - Quick Assign -->
              <div class="assign-by-tags-section">
                <h5 style="margin: 0 0 8px; font-size: 0.9rem; color: #6b7280;">
                  📌 按标签批量分配
                </h5>
                <div class="tag-assign-controls">
                  <select
                    v-model="selectedTagsForAssignment"
                    multiple
                    style="flex: 1; min-width: 200px; padding: 8px; border: 1px solid #d1d5db; border-radius: 6px;"
                  >
                    <option v-for="tag in availableTags" :key="tag.id" :value="tag.id">
                      {{ tag.name }} ({{ tag.description || '无描述' }})
                    </option>
                  </select>
                  <button
                    class="primary-btn"
                    type="button"
                    :disabled="selectedTagsForAssignment.length === 0 || assigningByTags"
                    @click="assignSuppliersByTags"
                  >
                    {{ assigningByTags ? '分配中...' : '按标签分配' }}
                  </button>
                </div>
                <p v-if="selectedTagsForAssignment.length > 0" class="micro-text" style="margin-top: 4px; color: #6b7280;">
                  将分配所有包含所选标签的供应商到当前采购员
                </p>
              </div>

              <div style="margin: 16px 0; border-top: 1px solid #e5e7eb;"></div>

              <!-- Search Individual Suppliers -->
              <h5 style="margin: 0 0 8px; font-size: 0.9rem; color: #6b7280;">
                🔍 搜索单个供应商
              </h5>
              <div class="search-controls">
                <input
                  v-model.trim="searchTerm"
                  type="text"
                  :placeholder="t('adminPermissions.delegations.searchPlaceholder')"
                  @keyup.enter="performSupplierSearch"
                />
                <button
                  class="link-btn"
                  type="button"
                  :disabled="searchLoading"
                  @click="performSupplierSearch"
                >
                  {{
                    searchLoading
                      ? t("adminPermissions.actions.searching")
                      : t("adminPermissions.actions.search")
                  }}
                </button>
              </div>
              <ul v-if="searchResults.length" class="search-results">
                <li v-for="supplier in searchResults" :key="supplier.id">
                  <div>
                    <strong>{{ supplier.companyName }}</strong>
                    <span class="muted">#{{ supplier.supplierCode || supplier.companyId }}</span>
                  </div>
                  <button class="primary-btn" type="button" @click="addAssignment(supplier)">
                    {{ t("adminPermissions.actions.add") }}
                  </button>
                </li>
              </ul>
              <p v-else-if="searchApplied" class="placeholder">
                {{ t("adminPermissions.delegations.noResults") }}
              </p>
            </section>
          </div>
        </div>

        <div v-else class="assignment-placeholder">
          {{ t("adminPermissions.delegations.selectPrompt") }}
        </div>
      </div>
    </section>

    <dialog v-if="showCreateDialog" class="modal" open>
      <form class="modal-content" @submit.prevent="submitCreateUser">
        <header class="modal-header">
          <h2>{{ t("adminPermissions.dialogs.createUser.title") }}</h2>
          <button class="link-btn" type="button" @click="closeCreateDialog">
            {{ t("common.close") }}
          </button>
        </header>
        <section class="modal-body">
          <label>
            {{ t("adminPermissions.dialogs.createUser.fields.userId") }}
            <input v-model.trim="createForm.id" type="text" required />
          </label>
          <label>
            {{ t("adminPermissions.dialogs.createUser.fields.name") }}
            <input v-model.trim="createForm.name" type="text" />
          </label>
          <label>
            {{ t("adminPermissions.dialogs.createUser.fields.role") }}
            <select v-model="createForm.role">
              <option v-for="role in assignableRoles" :key="role.role" :value="role.role">{{ role.label }}</option>
            </select>
          </label>
          <label>
            {{ t("adminPermissions.dialogs.createUser.fields.password") }}
            <input v-model.trim="createForm.password" type="password" minlength="6" required />
          </label>
          <label>
            {{ t("adminPermissions.dialogs.createUser.fields.email") }}
            <input v-model.trim="createForm.email" type="email" />
          </label>
          <label>
            {{ t("adminPermissions.dialogs.createUser.fields.supplierId") }}
            <input v-model.trim="createForm.supplierId" type="text" />
          </label>
        </section>
        <footer class="modal-footer">
          <button class="link-btn" type="button" @click="closeCreateDialog">
            {{ t("common.cancel") }}
          </button>
          <button class="primary-btn" type="submit" :disabled="creating">
            {{
              creating
                ? t("adminPermissions.dialogs.createUser.submitting")
                : t("adminPermissions.dialogs.createUser.submit")
            }}
          </button>
        </footer>
      </form>
    </dialog>

    <dialog v-if="showPasswordResetDialog" class="modal" open>
      <form class="modal-content" @submit.prevent="submitPasswordReset">
        <header class="modal-header">
          <h2>{{ t("adminPermissions.dialogs.resetPassword.title") }}</h2>
          <button class="link-btn" type="button" @click="closePasswordResetDialog">
            {{ t("common.close") }}
          </button>
        </header>
        <section class="modal-body">
          <p class="muted">
            {{ t("adminPermissions.dialogs.resetPassword.description") }}
            <strong
              >{{ selectedUser?.id ?? "" }} -
              {{ selectedUser?.name || t("common.userFallback") }}</strong
            >
          </p>
          <label>
            {{ t("adminPermissions.dialogs.resetPassword.fields.password") }}
            <input
              v-model.trim="passwordResetForm.password"
              type="password"
              minlength="6"
              required
              :placeholder="t('adminPermissions.dialogs.resetPassword.fields.passwordPlaceholder')"
            />
          </label>
          <label>
            {{ t("adminPermissions.dialogs.resetPassword.fields.confirm") }}
            <input
              v-model.trim="passwordResetForm.confirmPassword"
              type="password"
              minlength="6"
              required
              :placeholder="t('adminPermissions.dialogs.resetPassword.fields.confirmPlaceholder')"
            />
          </label>
        </section>
        <footer class="modal-footer">
          <button class="link-btn" type="button" @click="closePasswordResetDialog">
            {{ t("common.cancel") }}
          </button>
          <button
            class="primary-btn"
            type="submit"
            :disabled="
              !passwordResetForm.password ||
              passwordResetForm.password !== passwordResetForm.confirmPassword ||
              isResettingPassword
            "
          >
            {{
              isResettingPassword
                ? t("adminPermissions.dialogs.resetPassword.submitting")
                : t("adminPermissions.dialogs.resetPassword.submit")
            }}
          </button>
        </footer>
      </form>
    </dialog>

    <dialog v-if="showFreezeDialog" class="modal" open>
      <form class="modal-content" @submit.prevent="submitFreezeUser">
        <header class="modal-header">
          <h2>{{ t("adminPermissions.dialogs.freezeUser.title") }}</h2>
          <button class="link-btn" type="button" @click="closeFreezeDialog">
            {{ t("common.close") }}
          </button>
        </header>
        <section class="modal-body">
          <p class="muted">
            {{ t("adminPermissions.dialogs.freezeUser.description") }}
          </p>
          <p>
            {{ t("adminPermissions.dialogs.freezeUser.userName") }}
            <strong>{{ freezeTarget?.name || freezeTarget?.id || "" }}</strong>
          </p>
          <label>
            {{ t("adminPermissions.dialogs.freezeUser.fields.reason") }}
            <input
              v-model.trim="freezeReason"
              type="text"
              :placeholder="t('adminPermissions.dialogs.freezeUser.fields.reasonPlaceholder')"
            />
          </label>
        </section>
        <footer class="modal-footer">
          <button class="link-btn" type="button" @click="closeFreezeDialog">
            {{ t("common.cancel") }}
          </button>
          <button class="primary-btn" type="submit" :disabled="freezingUserId !== null">
            {{
              freezingUserId !== null
                ? t("adminPermissions.dialogs.freezeUser.submitting")
                : t("adminPermissions.dialogs.freezeUser.submit")
            }}
          </button>
        </footer>
      </form>
    </dialog>

    <dialog v-if="showDeleteDialog" class="modal" open>
      <form class="modal-content" @submit.prevent="submitDeleteUser">
        <header class="modal-header">
          <h2>{{ t("adminPermissions.dialogs.deleteUser.title") }}</h2>
          <button class="link-btn" type="button" @click="closeDeleteDialog">
            {{ t("common.close") }}
          </button>
        </header>
        <section class="modal-body">
          <p class="muted">
            {{ t("adminPermissions.dialogs.deleteUser.description") }}
          </p>
          <p>
            {{ t("adminPermissions.dialogs.deleteUser.userName") }}
            <strong>{{ deleteTarget?.name || deleteTarget?.id || "" }}</strong>
          </p>
          <p class="warning-text">{{ t("adminPermissions.dialogs.deleteUser.warning") }}</p>
        </section>
        <footer class="modal-footer">
          <button class="link-btn" type="button" @click="closeDeleteDialog">
            {{ t("common.cancel") }}
          </button>
          <button class="primary-btn danger" type="submit" :disabled="deletingUserId !== null">
            {{
              deletingUserId !== null
                ? t("adminPermissions.dialogs.deleteUser.submitting")
                : t("adminPermissions.dialogs.deleteUser.submit")
            }}
          </button>
        </footer>
      </form>
    </dialog>
  </div>
</template>

<script setup lang="ts">




import { computed, onMounted, reactive, ref, watch } from "vue";
import { useI18n } from "vue-i18n";


import { usePermissionStore } from "../stores/permissions";
import { useAuthStore } from "../stores/auth";
import { useSupplierStore } from "../stores/supplier";
import * as buyerAssignmentsApi from "@/api/buyerAssignments";
import type { BuyerAssignment, BuyerSummary, SupplierSummary, User } from "@/types";


import { useNotification } from "@/composables";

const notification = useNotification();
const SUPPLIER_ROLE_KEYS = new Set(["temp_supplier", "formal_supplier", "supplier"]);

const { t } = useI18n();
const permissionStore = usePermissionStore();
const authStore = useAuthStore();
const supplierStore = useSupplierStore();

const activeTab = ref<"internal" | "delegations">("internal");
const roles = computed(() => permissionStore.roles);
const users = computed(() => permissionStore.users);
const internalUsers = computed(() => users.value.filter((user) => !isSupplierRole(user.role)));
const internalRoles = computed(() => roles.value.filter((role) => !SUPPLIER_ROLE_KEYS.has(role.role)));
const assignableRoles = computed(() => roles.value.filter((role) => !SUPPLIER_ROLE_KEYS.has(role.role)));
const buyers = computed(() => permissionStore.buyers);
const availableTags = computed(() => supplierStore.availableTags);

const selectedBuyerId = ref<string | null>(null);
const selectedBuyer = computed(
  () => buyers.value.find((buyer) => buyer.id === selectedBuyerId.value) ?? null,
);

const editableAssignments = ref<BuyerAssignment[]>([]);
const assignmentsLoading = ref(false);
const savingAssignments = ref(false);
const assignmentDirty = ref(false);
const searchTerm = ref("");
const searchLoading = ref(false);
const searchApplied = ref(false);
const searchResults = ref<SupplierSummary[]>([]);

// Assign by tags state
const selectedTagsForAssignment = ref<number[]>([]);
const assigningByTags = ref(false);

const resolveErrorMessage = (error: unknown, fallback: string) =>
  error instanceof Error && error.message ? error.message : fallback;

const showCreateDialog = ref(false);
const creating = ref(false);
const createForm = reactive({
  id: "",
  name: "",
  role: "",
  password: "",
  email: "",
  supplierId: "",
});

const showPasswordResetDialog = ref(false);
const resettingPasswordUserId = ref<string | null>(null);
const isResettingPassword = computed(() => resettingPasswordUserId.value !== null);
const selectedUser = ref<User | null>(null);
const passwordResetForm = reactive({
  password: "",
  confirmPassword: "",
});
const emailDrafts = ref<Record<string, string>>({});
const emailSavingId = ref<string | null>(null);
const showFreezeDialog = ref(false);
const freezeTarget = ref<User | null>(null);
const freezeReason = ref("");
const freezingUserId = ref<string | null>(null);
const unfreezingUserId = ref<string | null>(null);
const showDeleteDialog = ref(false);
const deleteTarget = ref<User | null>(null);
const deletingUserId = ref<string | null>(null);

const isSupplierRole = (role: string) => SUPPLIER_ROLE_KEYS.has(role);
const isSelfUser = (user: User) =>
  authStore.user != null && String(authStore.user.id) === user.id;

const roleOptionsForUser = (user: User) => {
  if (isSupplierRole(user.role)) {
    const roleObj = roles.value.find(r => r.role === user.role);
    return roleObj ? [roleObj] : [];
  }
  return assignableRoles.value;
};

const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const formatUserStatus = (status?: User["status"]) => {
  const key = status ?? "active";
  return t(`adminPermissions.internal.status.${key}`);
};

const formatBuyerStats = (buyer: BuyerSummary) => {
  return t("adminPermissions.delegations.buyerStats", {
    suppliers: buyer.assignmentCount,
    profiles: buyer.profileAccessCount,
    alerts: buyer.contractAlertCount,
  });
};

const refresh = async () => {
  try {
    await Promise.all([
      permissionStore.fetchRoles(),
      permissionStore.fetchUsers(),
      permissionStore.fetchBuyers(),
    ]);
    if (activeTab.value === "delegations" && buyers.value.length > 0 && !selectedBuyerId.value) {
      selectedBuyerId.value = buyers.value[0].id;
    }
  } catch (error) {
    console.error(t("adminPermissions.notifications.loadPermissionsFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.loadPermissionsFailure")),
    );
  }
};

const openCreateDialog = () => {
  showCreateDialog.value = true;
  createForm.id = "";
  createForm.name = "";
  createForm.role = assignableRoles.value[0]?.role ?? "purchaser";
  createForm.password = "";
  createForm.email = "";
  createForm.supplierId = "";
};

const closeCreateDialog = () => {
  showCreateDialog.value = false;
};

const openPasswordResetDialog = (user: User) => {
  selectedUser.value = user;
  showPasswordResetDialog.value = true;
  passwordResetForm.password = "";
  passwordResetForm.confirmPassword = "";
};

const closePasswordResetDialog = () => {
  showPasswordResetDialog.value = false;
  selectedUser.value = null;
  passwordResetForm.password = "";
  passwordResetForm.confirmPassword = "";
};

const submitPasswordReset = async () => {
  if (!selectedUser.value || !passwordResetForm.password) {
    notification.warning(t("adminPermissions.validation.passwordRequired"));
    return;
  }

  if (passwordResetForm.password !== passwordResetForm.confirmPassword) {
    notification.warning(t("adminPermissions.validation.passwordMismatch"));
    return;
  }

  resettingPasswordUserId.value = selectedUser.value.id;
  try {
    await permissionStore.changeUserPassword(selectedUser.value.id, passwordResetForm.password);
    notification.success(t("adminPermissions.notifications.passwordResetSuccess"));
    closePasswordResetDialog();
  } catch (error) {
    console.error(t("adminPermissions.notifications.passwordResetFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.passwordResetFailure")),
    );
  } finally {
    resettingPasswordUserId.value = null;
  }
};

const submitCreateUser = async () => {
  const trimmedId = createForm.id.trim();
  const trimmedPassword = createForm.password.trim();
  const trimmedEmail = createForm.email.trim();
  if (!trimmedId || !trimmedPassword) {
    notification.warning(t("adminPermissions.validation.credentialsRequired"));
    return;
  }
  if (trimmedEmail && !emailRegex.test(trimmedEmail)) {
    notification.warning(t("adminPermissions.validation.invalidEmail"));
    return;
  }
  creating.value = true;
  try {
    await permissionStore.createAccount({
      id: trimmedId,
      username: trimmedId,
      name: createForm.name.trim(),
      role: createForm.role,
      password: trimmedPassword,
      supplierId: createForm.supplierId.trim() || undefined,
      email: trimmedEmail || undefined,
    });
    notification.success(t("adminPermissions.notifications.userCreated"));
    closeCreateDialog();
    await permissionStore.fetchUsers();
  } catch (error) {
    console.error(t("adminPermissions.notifications.userCreateFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.userCreateFailure")),
    );
  } finally {
    creating.value = false;
  }
};

const submitEmailUpdate = async (user: User) => {
  const draft = emailDrafts.value[user.id] ?? "";
  const trimmed = draft.trim();
  const current = user.email ?? "";
  if (trimmed === current) {
    return;
  }
  if (trimmed && !emailRegex.test(trimmed)) {
    notification.warning(t("adminPermissions.validation.invalidEmail"));
    emailDrafts.value[user.id] = current;
    return;
  }
  emailSavingId.value = user.id;
  try {
    await permissionStore.changeUserEmail(user.id, trimmed || null);
    notification.success(t("adminPermissions.notifications.emailUpdateSuccess"));
  } catch (error) {
    console.error(t("adminPermissions.notifications.emailUpdateFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.emailUpdateFailure")),
    );
    emailDrafts.value[user.id] = current;
  } finally {
    emailSavingId.value = null;
  }
};

const openFreezeDialog = (user: User) => {
  if (authStore.user && String(authStore.user.id) === user.id) {
    notification.warning(t("adminPermissions.notifications.cannotFreezeSelf"));
    return;
  }
  freezeTarget.value = user;
  freezeReason.value = "";
  showFreezeDialog.value = true;
};

const closeFreezeDialog = () => {
  showFreezeDialog.value = false;
  freezeTarget.value = null;
  freezeReason.value = "";
};

const submitFreezeUser = async () => {
  if (!freezeTarget.value) {
    return;
  }
  freezingUserId.value = freezeTarget.value.id;
  try {
    await permissionStore.freezeUser(freezeTarget.value.id, freezeReason.value.trim() || undefined);
    notification.success(t("adminPermissions.notifications.userFrozen"));
    closeFreezeDialog();
    await permissionStore.fetchUsers();
  } catch (error) {
    console.error(t("adminPermissions.notifications.userFreezeFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.userFreezeFailure")),
    );
  } finally {
    freezingUserId.value = null;
  }
};

const handleUnfreeze = async (user: User) => {
  unfreezingUserId.value = user.id;
  try {
    await permissionStore.unfreezeUser(user.id);
    notification.success(t("adminPermissions.notifications.userUnfrozen"));
    await permissionStore.fetchUsers();
  } catch (error) {
    console.error(t("adminPermissions.notifications.userUnfreezeFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.userUnfreezeFailure")),
    );
  } finally {
    unfreezingUserId.value = null;
  }
};

const openDeleteDialog = (user: User) => {
  if (authStore.user && String(authStore.user.id) === user.id) {
    notification.warning(t("adminPermissions.notifications.cannotDeleteSelf"));
    return;
  }
  deleteTarget.value = user;
  showDeleteDialog.value = true;
};

const closeDeleteDialog = () => {
  showDeleteDialog.value = false;
  deleteTarget.value = null;
};

const submitDeleteUser = async () => {
  if (!deleteTarget.value) {
    return;
  }
  deletingUserId.value = deleteTarget.value.id;
  try {
    await permissionStore.deleteUser(deleteTarget.value.id);
    notification.success(t("adminPermissions.notifications.userDeleted"));
    closeDeleteDialog();
    await permissionStore.fetchUsers();
  } catch (error) {
    console.error(t("adminPermissions.notifications.userDeleteFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.userDeleteFailure")),
    );
  } finally {
    deletingUserId.value = null;
  }
};

const updateRole = async (user: User, role: string) => {
  if (user.role === role) {
    return;
  }
  if (isSupplierRole(user.role)) {
    notification.info(t("adminPermissions.notifications.supplierRoleManaged"));
    return;
  }
  if (SUPPLIER_ROLE_KEYS.has(role)) {
    notification.warning(t("adminPermissions.notifications.supplierRoleNotAllowed"));
    return;
  }
  try {
    await permissionStore.changeUserRole(user.id, role, {
      actorId: authStore.user ? String(authStore.user.id) : undefined,
      actorName: authStore.user?.name ?? undefined,
    });
    notification.success(t("adminPermissions.notifications.roleUpdated"));
  } catch (error) {
    console.error(t("adminPermissions.notifications.roleUpdateFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.roleUpdateFailure")),
    );
  }
};

const loadBuyers = async () => {
  try {
    await permissionStore.fetchBuyers();
    if (
      buyers.value.length > 0 &&
      (!selectedBuyerId.value || !buyers.value.some((buyer) => buyer.id === selectedBuyerId.value))
    ) {
      selectedBuyerId.value = buyers.value[0].id;
    }
  } catch (error) {
    console.error(t("adminPermissions.notifications.loadBuyersFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.loadBuyersFailure")),
    );
  }
};

const selectBuyer = (buyerId: string) => {
  if (selectedBuyerId.value === buyerId) {
    return;
  }
  selectedBuyerId.value = buyerId;
};

watch(
  () => activeTab.value,
  (tab) => {
    if (tab === "delegations" && buyers.value.length > 0 && !selectedBuyerId.value) {
      selectedBuyerId.value = buyers.value[0].id;
    }
  },
);

watch(
  buyers,
  (list) => {
    if (list.length === 0) {
      selectedBuyerId.value = null;
      editableAssignments.value = [];
      assignmentDirty.value = false;
    } else if (selectedBuyerId.value && !list.some((buyer) => buyer.id === selectedBuyerId.value)) {
      selectedBuyerId.value = list[0].id;
    }
  },
  { immediate: true },
);

watch(
  users,
  (list) => {
    const next = { ...emailDrafts.value };
    list.forEach((user) => {
      next[user.id] = user.email ?? "";
    });
    Object.keys(next).forEach((id) => {
      if (!list.some((user) => user.id === id)) {
        delete next[id];
      }
    });
    emailDrafts.value = next;
  },
  { immediate: true },
);

watch(selectedBuyerId, async (buyerId) => {
  editableAssignments.value = [];
  assignmentDirty.value = false;
  searchResults.value = [];
  searchApplied.value = false;
  if (!buyerId) {
    return;
  }
  assignmentsLoading.value = true;
  try {
    const assignments = await permissionStore.fetchBuyerAssignments(buyerId);
    editableAssignments.value = assignments.map((item) => ({ ...item }));
  } catch (error) {
    console.error(t("adminPermissions.notifications.loadAssignmentsFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.loadAssignmentsFailure")),
    );
  } finally {
    assignmentsLoading.value = false;
  }
});

const removeAssignment = (supplierId: number) => {
  editableAssignments.value = editableAssignments.value.filter(
    (item) => item.supplierId !== supplierId,
  );
  assignmentDirty.value = true;
};

const isSupplierAssigned = (supplierId: number) =>
  editableAssignments.value.some((item) => item.supplierId === supplierId);

const addAssignment = (supplier: SupplierSummary) => {
  if (isSupplierAssigned(supplier.id)) {
    return;
  }
  editableAssignments.value.push({
    supplierId: supplier.id,
    companyName: supplier.companyName,
    companyId: supplier.companyId,
    contractAlert: false,
    profileAccess: false,
  });
  assignmentDirty.value = true;
  searchResults.value = searchResults.value.filter((item) => item.id !== supplier.id);
};

const markAssignmentsDirty = () => {
  assignmentDirty.value = true;
};

const performSupplierSearch = async () => {
  if (!selectedBuyerId.value) {
    notification.warning(t("adminPermissions.notifications.selectBuyerFirst"));
    return;
  }
  const keyword = searchTerm.value.trim();
  searchLoading.value = true;
  searchApplied.value = true;
  try {
    const results = await permissionStore.searchSuppliers(keyword);
    searchResults.value = results.filter((item: SupplierSummary) => !isSupplierAssigned(item.id));
  } catch (error) {
    console.error(t("adminPermissions.notifications.searchSuppliersFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.searchSuppliersFailure")),
    );
  } finally {
    searchLoading.value = false;
  }
};

const saveAssignments = async () => {
  if (!selectedBuyerId.value) {
    return;
  }
  savingAssignments.value = true;
  try {
    const updated = await permissionStore.saveBuyerAssignments(
      selectedBuyerId.value,
      editableAssignments.value,
    );
    editableAssignments.value = updated.map((item) => ({ ...item }));
    assignmentDirty.value = false;
    notification.success(t("adminPermissions.notifications.assignmentsUpdated"));
  } catch (error) {
    console.error(t("adminPermissions.notifications.assignmentsUpdateFailure"), error);
    notification.error(
      resolveErrorMessage(error, t("adminPermissions.notifications.assignmentsUpdateFailure")),
    );
  } finally {
    savingAssignments.value = false;
  }
};

// Assign suppliers by tags
const assignSuppliersByTags = async () => {
  if (!selectedBuyerId.value || selectedTagsForAssignment.value.length === 0) {
    return;
  }

  assigningByTags.value = true;
  try {
    const response = await buyerAssignmentsApi.assignSuppliersByTag({
      buyerId: selectedBuyerId.value,
      tagIds: selectedTagsForAssignment.value,
    });

    notification.success(
      response.message ||
      `成功分配 ${response.data.assignedCount} 个供应商到采购员 ${selectedBuyer.value?.name}`
    );

    // Reload assignments for current buyer
    await selectBuyer(selectedBuyerId.value);

    // Clear selection
    selectedTagsForAssignment.value = [];

  } catch (error) {
    console.error("Error assigning suppliers by tags:", error);
    notification.error(
      resolveErrorMessage(error, "按标签分配供应商失败")
    );
  } finally {
    assigningByTags.value = false;
  }
};

onMounted(async () => {
  await Promise.all([
    refresh(),
    supplierStore.ensureTags()
  ]);
});




</script>

<style scoped>
.admin-permissions {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: center;
}

.header-actions {
  display: flex;
  gap: 12px;
}

.page-header h1 {
  font-size: 22px;
  margin: 0;
}

.page-header p {
  margin: 4px 0 0;
  color: #6b7280;
  font-size: 14px;
}

.mode-toggle {
  display: inline-flex;
  border: 1px solid #e5e7eb;
  border-radius: 999px;
  padding: 4px;
  gap: 4px;
  align-self: flex-start;
}

.mode-toggle button {
  border: none;
  background: transparent;
  padding: 6px 16px;
  border-radius: 999px;
  font-weight: 600;
  color: #4b5563;
  cursor: pointer;
  transition:
    background 0.2s ease,
    color 0.2s ease;
}

.mode-toggle button.active {
  background: #4f46e5;
  color: #ffffff;
}

.panel {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 20px;
  background: #ffffff;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.panel-stack {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.role-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.role-list li {
  background: #eef2ff;
  color: #3730a3;
  padding: 6px 12px;
  border-radius: 999px;
  font-size: 13px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.user-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}

.user-table th,
.user-table td {
  padding: 10px;
  border-bottom: 1px solid #f1f5f9;
  text-align: left;
}

.user-table select {
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 6px 8px;
}

.user-table select:disabled {
  background: #f9fafb;
  color: #9ca3af;
}

.email-cell input {
  width: 100%;
  min-width: 180px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 6px 8px;
}

.email-cell input:disabled {
  background: #f9fafb;
  color: #9ca3af;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  padding: 2px 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;
}

.status-active {
  background: #ecfdf3;
  color: #166534;
}

.status-frozen {
  background: #fef3c7;
  color: #92400e;
}

.status-deleted {
  background: #fee2e2;
  color: #991b1b;
}

.role-cell {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.actions-cell {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.link-btn.danger {
  color: #dc2626;
}

.warning-text {
  color: #b45309;
  font-size: 13px;
}

.micro-text {
  font-size: 12px;
  color: #6b7280;
}

.delegations-panel {
  gap: 20px;
}

.buyer-assignment {
  display: grid;
  grid-template-columns: minmax(220px, 260px) 1fr;
  gap: 20px;
}

.buyer-list {
  border-right: 1px solid #f3f4f6;
  padding-right: 16px;
}

.buyer-list ul {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.buyer-list li button {
  width: 100%;
  text-align: left;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  padding: 10px 12px;
  background: #f9fafb;
  display: flex;
  flex-direction: column;
  gap: 4px;
  cursor: pointer;
  transition:
    border-color 0.2s ease,
    background 0.2s ease;
}

.buyer-list li.active button,
.buyer-list li button:hover {
  border-color: #6366f1;
  background: #eef2ff;
}

.buyer-name {
  font-weight: 600;
  color: #1f2937;
}

.buyer-meta {
  font-size: 12px;
  color: #6b7280;
}

.assignment-detail {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.assignment-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}

.assignment-body {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 20px;
}

.current-assignments,
.supplier-search {
  border: 1px solid #eef2ff;
  border-radius: 10px;
  padding: 16px;
  background: #fafaff;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.assignment-list,
.search-results {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.assignment-list li,
.search-results li {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #ffffff;
}

.assignment-list li {
  justify-content: space-between;
}

.assignment-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.assignment-permissions {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 13px;
}

.assignment-permissions label {
  display: flex;
  align-items: center;
  gap: 6px;
}

.remove {
  color: #ef4444;
}

.search-controls {
  display: flex;
  gap: 10px;
}

.search-controls input {
  flex: 1;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 8px 10px;
  font-size: 14px;
}

.placeholder {
  color: #6b7280;
  font-size: 14px;
}

.assignment-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px dashed #d1d5db;
  border-radius: 10px;
  padding: 40px;
  background: #f9fafb;
  color: #6b7280;
  font-size: 14px;
}

.link-btn {
  border: none;
  background: transparent;
  color: #4f46e5;
  font-weight: 600;
  cursor: pointer;
  padding: 0;
}

.primary-btn {
  background: #4f46e5;
  color: #ffffff;
  border: none;
  border-radius: 999px;
  padding: 8px 16px;
  font-weight: 600;
  cursor: pointer;
  transition:
    background 0.2s ease,
    transform 0.2s ease;
}

.primary-btn:disabled {
  opacity: 0.6;
  cursor: default;
}

.primary-btn:not(:disabled):hover {
  background: #4338ca;
  transform: translateY(-1px);
}

.primary-btn.danger {
  background: #dc2626;
}

.primary-btn.danger:not(:disabled):hover {
  background: #b91c1c;
}

.modal {
  border: none;
  padding: 0;
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 20px 60px rgba(15, 23, 42, 0.25);
}

.modal-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 0;
  min-width: 360px;
}

.modal-header,
.modal-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  background: #f9fafb;
}

.modal-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 0 20px 20px;
}

.modal-body label {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 14px;
  color: #374151;
}

.modal-body input,
.modal-body select {
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 8px 10px;
  font-size: 14px;
}

.modal-footer {
  gap: 12px;
}

.actions-cell {
  display: flex;
  gap: 8px;
  align-items: center;
}

.reset-password-btn {
  font-size: 13px;
  padding: 4px 8px;
  border-radius: 4px;
  transition: background-color 0.2s ease;
}

.reset-password-btn:not(:disabled):hover {
  background-color: #f0f0f0;
}

.reset-password-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Assign by Tags Section */
.assign-by-tags-section {
  background: #f9fafb;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  padding: 12px;
  margin-bottom: 12px;
}

.tag-assign-controls {
  display: flex;
  gap: 8px;
  align-items: flex-start;
}

.tag-assign-controls select {
  max-height: 120px;
}

.tag-assign-controls select option {
  padding: 6px;
}
</style>
