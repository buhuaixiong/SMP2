import { defineStore } from "pinia";
import { ref } from "vue";
import {
  listRoles,
  listUsers,
  updateUserRole,
  createUser,
  deleteUser,
  resetUserPassword,
  updateUserEmail,
  listBuyers,
  getBuyerAssignments,
  updateBuyerAssignments,
  searchAssignableSuppliers,
  freezeUser as apiFreezeUser,
  unfreezeUser as apiUnfreezeUser,
  softDeleteUser as apiSoftDeleteUser,
  type RoleDefinition,
} from "../api/permissions";
import type {
  User,
  BuyerSummary,
  BuyerAssignment,
  SupplierSummary,
  CreateUserPayload,
} from "../types";

export const usePermissionStore = defineStore("permission", () => {
  const roles = ref<RoleDefinition[]>([]);
  const users = ref<User[]>([]);
  const buyers = ref<BuyerSummary[]>([]);
  const buyerAssignments = ref<Record<string, BuyerAssignment[]>>({});
  const supplierSearchResults = ref<SupplierSummary[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const fetchRoles = async () => {
    loading.value = true;
    error.value = null;
    try {
      roles.value = await listRoles();
      return roles.value;
    } catch (err) {
      error.value = err instanceof Error ? err.message : "Failed to load roles";
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const fetchUsers = async () => {
    loading.value = true;
    error.value = null;
    try {
      users.value = await listUsers();
      return users.value;
    } catch (err) {
      error.value = err instanceof Error ? err.message : "Failed to load users";
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const createAccount = async (payload: CreateUserPayload) => {
    const created = await createUser(payload);
    const index = users.value.findIndex((user) => user.id === created.id);
    if (index >= 0) {
      users.value.splice(index, 1, created);
    } else {
      users.value.push(created);
    }
    if (created.role === "purchaser") {
      await fetchBuyers();
    }
    return created;
  };

  const changeUserRole = async (
    id: string,
    role: string,
    actor?: { actorId?: string; actorName?: string },
  ) => {
    const updated = await updateUserRole(id, { role, ...actor });
    const index = users.value.findIndex((user) => user.id === id);
    if (index >= 0) {
      users.value.splice(index, 1, updated);
    }
    if (updated.role === "purchaser") {
      await fetchBuyers();
    } else {
      const buyerIndex = buyers.value.findIndex((buyer) => buyer.id === id);
      if (buyerIndex >= 0) {
        buyers.value.splice(buyerIndex, 1);
        delete buyerAssignments.value[id];
      }
    }
    return updated;
  };

  const removeUser = async (id: string) => {
    await deleteUser(id);
    users.value = users.value.filter((user) => user.id !== id);
    const buyerIndex = buyers.value.findIndex((buyer) => buyer.id === id);
    if (buyerIndex >= 0) {
      buyers.value.splice(buyerIndex, 1);
      delete buyerAssignments.value[id];
    }
  };

  const changeUserPassword = async (id: string, password: string) => {
    await resetUserPassword(id, password);
  };

  const changeUserEmail = async (id: string, email: string | null) => {
    const updated = await updateUserEmail(id, email);
    const index = users.value.findIndex((user) => user.id === id);
    if (index >= 0) {
      users.value.splice(index, 1, updated);
    }
    return updated;
  };

  const fetchBuyers = async () => {
    const list = await listBuyers();
    buyers.value = list.map((item) => ({
      ...item,
      assignmentCount: item.assignmentCount ?? 0,
      contractAlertCount: item.contractAlertCount ?? 0,
      profileAccessCount: item.profileAccessCount ?? 0,
    }));
    return buyers.value;
  };

  const fetchBuyerAssignments = async (buyerId: string) => {
    const { data, meta } = await getBuyerAssignments(buyerId);
    buyerAssignments.value = {
      ...buyerAssignments.value,
      [buyerId]: data,
    };
    const summary = buyers.value.find((buyer) => buyer.id === buyerId);
    if (summary) {
      summary.assignmentCount = data.length;
      summary.contractAlertCount = data.filter((item) => item.contractAlert).length;
      summary.profileAccessCount = data.filter((item) => item.profileAccess).length;
      if (meta?.buyerName && !summary.name) {
        summary.name = meta.buyerName;
      }
    }
    return data;
  };

  const saveBuyerAssignments = async (buyerId: string, assignments: BuyerAssignment[]) => {
    const payload = assignments.map((item) => ({
      supplierId: item.supplierId,
      contractAlert: item.contractAlert,
      profileAccess: item.profileAccess,
    }));
    const { data } = await updateBuyerAssignments(buyerId, payload);
    buyerAssignments.value = {
      ...buyerAssignments.value,
      [buyerId]: data,
    };
    const summary = buyers.value.find((buyer) => buyer.id === buyerId);
    if (summary) {
      summary.assignmentCount = data.length;
      summary.contractAlertCount = data.filter((item) => item.contractAlert).length;
      summary.profileAccessCount = data.filter((item) => item.profileAccess).length;
    }
    return data;
  };

  const searchSuppliers = async (keyword: string): Promise<SupplierSummary[]> => {
    const results = await searchAssignableSuppliers(keyword);
    supplierSearchResults.value = results;
    return results;
  };

  // User lifecycle management
  const freezeUser = async (id: string, reason?: string) => {
    await apiFreezeUser(id, reason);
  };

  const unfreezeUser = async (id: string) => {
    await apiUnfreezeUser(id);
  };

  const deleteUser = async (id: string) => {
    await apiSoftDeleteUser(id);
  };

  return {
    roles,
    users,
    buyers,
    buyerAssignments,
    supplierSearchResults,
    loading,
    error,
    fetchRoles,
    fetchUsers,
    createAccount,
    changeUserRole,
    removeUser,
    changeUserPassword,
    changeUserEmail,
    fetchBuyers,
    fetchBuyerAssignments,
    saveBuyerAssignments,
    searchSuppliers,
    freezeUser,
    unfreezeUser,
    deleteUser,
  };
});

