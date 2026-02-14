import { defineStore } from "pinia";
import { computed, ref } from "vue";
import type {
  BuyerAssignment,
  PurchasingGroup,
  PurchasingGroupDetail,
  PurchasingGroupMember,
  PurchasingGroupSupplier,
  WorkloadAnalysis,
  CreatePurchasingGroupPayload,
  UpdatePurchasingGroupPayload,
  AddGroupMembersPayload,
  AddGroupSuppliersPayload,
} from "@/types";
import * as api from "@/api/purchasingGroups";

type PurchasingGroupBuyerAssignment = BuyerAssignment;

type FetchGroupAssociationsOptions = {
  updateState?: boolean;
};

// Utility to unwrap API response
const unwrap = <T>(payload: T | { data: T }): T => {
  if (payload && typeof payload === "object" && "data" in payload) {
    return (payload as { data: T }).data;
  }
  return payload as T;
};

// Helper to group items by a key
const groupByKey = <T>(
  items: T[],
  getKey: (item: T) => string,
  defaultLabel = "Uncategorized",
): Map<string, T[]> => {
  const result = new Map<string, T[]>();
  items.forEach((item) => {
    const key = getKey(item) || defaultLabel;
    if (!result.has(key)) {
      result.set(key, []);
    }
    result.get(key)!.push(item);
  });
  return result;
};

export const usePurchasingGroupsStore = defineStore("purchasingGroups", () => {
  // State
  const groups = ref<PurchasingGroup[]>([]);
  const currentGroup = ref<PurchasingGroupDetail | null>(null);
  const members = ref<PurchasingGroupMember[]>([]);
  const suppliers = ref<PurchasingGroupSupplier[]>([]);
  const membersByGroup = ref<Record<number, PurchasingGroupMember[]>>({});
  const suppliersByGroup = ref<Record<number, PurchasingGroupSupplier[]>>({});
  const buyerAssignments = ref<PurchasingGroupBuyerAssignment[]>([]);
  const workloadAnalysis = ref<WorkloadAnalysis | null>(null);

  const loading = ref(false);
  const loadingMembers = ref(false);
  const loadingSuppliers = ref(false);
  const loadingAssignments = ref(false);
  const loadingWorkload = ref(false);

  // Computed getters
  const activeGroups = computed(() => groups.value.filter((g) => g.isActive === 1));

  const groupsByCategory = computed(() =>
    groupByKey(groups.value, (g) => g.category || "", "Uncategorized"),
  );

  const groupsByRegion = computed(() =>
    groupByKey(groups.value, (g) => g.region || "", "Unspecified"),
  );

  // Actions
  async function fetchGroups(params?: api.ListGroupsParams) {
    loading.value = true;
    try {
      const response = await api.listGroups(params);
      groups.value = unwrap(response);
      return groups.value;
    } finally {
      loading.value = false;
    }
  }

  async function fetchGroup(id: number) {
    loading.value = true;
    try {
      const response = await api.getGroup(id);
      currentGroup.value = unwrap(response);
      members.value = currentGroup.value.members || [];
      suppliers.value = currentGroup.value.suppliers || [];
      return currentGroup.value;
    } finally {
      loading.value = false;
    }
  }

  async function createGroup(payload: CreatePurchasingGroupPayload) {
    loading.value = true;
    try {
      const response = await api.createGroup(payload);
      const created = unwrap(response);
      groups.value.unshift(created);
      return created;
    } finally {
      loading.value = false;
    }
  }

  async function updateGroup(id: number, payload: UpdatePurchasingGroupPayload) {
    loading.value = true;
    try {
      const response = await api.updateGroup(id, payload);
      const updated = unwrap(response);
      const index = groups.value.findIndex((g) => g.id === id);
      if (index !== -1) {
        groups.value[index] = updated;
      }
      if (currentGroup.value && currentGroup.value.id === id) {
        currentGroup.value = { ...currentGroup.value, ...updated };
      }
      return updated;
    } finally {
      loading.value = false;
    }
  }

  async function deleteGroup(id: number) {
    loading.value = true;
    try {
      await api.deleteGroup(id);
      groups.value = groups.value.filter((g) => g.id !== id);
      if (currentGroup.value && currentGroup.value.id === id) {
        currentGroup.value = null;
        members.value = [];
        suppliers.value = [];
      }
    } finally {
      loading.value = false;
    }
  }

  async function fetchMembers(groupId: number, options: FetchGroupAssociationsOptions = {}) {
    const shouldUpdateState = options.updateState !== false;
    if (shouldUpdateState) {
      loadingMembers.value = true;
    }
    try {
      const response = await api.getMembers(groupId);
      const list = unwrap(response);
      membersByGroup.value[groupId] = list;
      if (shouldUpdateState) {
        members.value = list;
      }
      return list;
    } finally {
      if (shouldUpdateState) {
        loadingMembers.value = false;
      }
    }
  }

  async function addMembers(groupId: number, payload: AddGroupMembersPayload) {
    loadingMembers.value = true;
    try {
      await api.addMembers(groupId, payload);
      // Refresh members list
      await fetchMembers(groupId);
      // Update group member count
      const groupIndex = groups.value.findIndex((g) => g.id === groupId);
      if (groupIndex !== -1) {
        groups.value[groupIndex].memberCount = members.value.length;
      }
    } finally {
      loadingMembers.value = false;
    }
  }

  async function removeMember(groupId: number, buyerId: string) {
    loadingMembers.value = true;
    try {
      await api.removeMember(groupId, buyerId);
      members.value = members.value.filter((m) => m.buyerId !== buyerId);
      membersByGroup.value[groupId] = members.value;
      // Update group member count
      const groupIndex = groups.value.findIndex((g) => g.id === groupId);
      if (groupIndex !== -1) {
        groups.value[groupIndex].memberCount = members.value.length;
      }
    } finally {
      loadingMembers.value = false;
    }
  }

  async function fetchSuppliers(groupId: number, options: FetchGroupAssociationsOptions = {}) {
    const shouldUpdateState = options.updateState !== false;
    if (shouldUpdateState) {
      loadingSuppliers.value = true;
    }
    try {
      const response = await api.getSuppliers(groupId);
      const list = unwrap(response);
      suppliersByGroup.value[groupId] = list;
      if (shouldUpdateState) {
        suppliers.value = list;
      }
      return list;
    } finally {
      if (shouldUpdateState) {
        loadingSuppliers.value = false;
      }
    }
  }

  function hydrateGroupAssociations(groupId: number) {
    const hasMembers = Object.prototype.hasOwnProperty.call(membersByGroup.value, groupId);
    const hasSuppliers = Object.prototype.hasOwnProperty.call(suppliersByGroup.value, groupId);
    members.value = hasMembers ? membersByGroup.value[groupId] : [];
    suppliers.value = hasSuppliers ? suppliersByGroup.value[groupId] : [];
    return { hasMembers, hasSuppliers };
  }

  async function prefetchGroupAssociations(groupIds: number[]) {
    const uniqueIds = Array.from(
      new Set(groupIds.filter((id) => Number.isFinite(id) && id > 0)),
    );
    await Promise.all(
      uniqueIds.map(async (groupId) => {
        try {
          await Promise.all([
            fetchMembers(groupId, { updateState: false }),
            fetchSuppliers(groupId, { updateState: false }),
          ]);
        } catch (error) {
          console.error("Failed to prefetch purchasing group associations", { groupId, error });
        }
      }),
    );
  }

  async function addSuppliers(groupId: number, payload: AddGroupSuppliersPayload) {
    loadingSuppliers.value = true;
    try {
      await api.addSuppliers(groupId, payload);
      // Refresh suppliers list
      await fetchSuppliers(groupId);
      // Update group supplier count
      const groupIndex = groups.value.findIndex((g) => g.id === groupId);
      if (groupIndex !== -1) {
        groups.value[groupIndex].supplierCount = suppliers.value.length;
      }
    } finally {
      loadingSuppliers.value = false;
    }
  }

  async function fetchBuyerAssignments(groupId: number) {
    loadingAssignments.value = true;
    try {
      const response = await api.getBuyerAssignments(groupId);
      buyerAssignments.value = unwrap(response);
      return buyerAssignments.value;
    } finally {
      loadingAssignments.value = false;
    }
  }

  async function assignSuppliersToBuyer(
    groupId: number,
    payload: { buyerId: string; supplierIds: number[] },
  ) {
    loadingAssignments.value = true;
    try {
      const response = await api.assignToBuyer(groupId, payload);
      const result = unwrap(response);
      await fetchBuyerAssignments(groupId);
      return result;
    } finally {
      loadingAssignments.value = false;
    }
  }

  async function removeBuyerAssignment(groupId: number, assignmentId: number) {
    loadingAssignments.value = true;
    try {
      await api.removeBuyerAssignment(groupId, assignmentId);
      buyerAssignments.value = buyerAssignments.value.filter((assignment) => assignment.id !== assignmentId);
    } finally {
      loadingAssignments.value = false;
    }
  }

  async function removeSupplier(groupId: number, supplierId: number) {
    loadingSuppliers.value = true;
    try {
      await api.removeSupplier(groupId, supplierId);
      suppliers.value = suppliers.value.filter((s) => s.supplierId !== supplierId);
      suppliersByGroup.value[groupId] = suppliers.value;
      // Update group supplier count
      const groupIndex = groups.value.findIndex((g) => g.id === groupId);
      if (groupIndex !== -1) {
        groups.value[groupIndex].supplierCount = suppliers.value.length;
      }
    } finally {
      loadingSuppliers.value = false;
    }
  }

  async function fetchWorkloadAnalysis() {
    loadingWorkload.value = true;
    try {
      const response = await api.getWorkloadAnalysis();
      workloadAnalysis.value = unwrap(response);
      return workloadAnalysis.value;
    } finally {
      loadingWorkload.value = false;
    }
  }

  async function rebuildCache() {
    loading.value = true;
    try {
      await api.rebuildCache();
    } finally {
      loading.value = false;
    }
  }

  function clearCurrentGroup() {
    currentGroup.value = null;
    members.value = [];
    suppliers.value = [];
    buyerAssignments.value = [];
  }

  return {
    // State
    groups,
    currentGroup,
    members,
    suppliers,
    buyerAssignments,
    workloadAnalysis,
    loading,
    loadingMembers,
    loadingSuppliers,
    loadingAssignments,
    loadingWorkload,

    // Computed
    activeGroups,
    groupsByCategory,
    groupsByRegion,

    // Actions
    fetchGroups,
    fetchGroup,
    createGroup,
    updateGroup,
    deleteGroup,
    fetchMembers,
    addMembers,
    removeMember,
    fetchSuppliers,
    hydrateGroupAssociations,
    prefetchGroupAssociations,
    addSuppliers,
    removeSupplier,
    fetchBuyerAssignments,
    assignSuppliersToBuyer,
    removeBuyerAssignment,
    fetchWorkloadAnalysis,
    rebuildCache,
    clearCurrentGroup,
  };
});

