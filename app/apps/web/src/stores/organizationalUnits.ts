import { defineStore } from "pinia";
import { ref } from "vue";
import * as orgUnitApi from "@/api/organizationalUnits";
import type {
  OrganizationalUnit,
  OrganizationalUnitDetail,
  OrgUnitMember,
  OrgUnitSupplier,
} from "@/api/organizationalUnits";

export const useOrganizationalUnitsStore = defineStore("organizationalUnits", () => {
  // State
  const units = ref<OrganizationalUnit[]>([]);
  const currentUnit = ref<OrganizationalUnitDetail | null>(null);
  const members = ref<OrgUnitMember[]>([]);
  const suppliers = ref<OrgUnitSupplier[]>([]);
  const loading = ref(false);
  const loadingMembers = ref(false);
  const loadingSuppliers = ref(false);

  // Actions

  /**
   * Fetch all organizational units
   */
  async function fetchUnits(params?: {
    type?: string;
    isActive?: boolean | string;
    format?: "tree" | "flat";
    parentId?: number | string;
  }) {
    loading.value = true;
    try {
      const response = await orgUnitApi.listOrgUnits(params);
      units.value = response.data;
      return response.data;
    } finally {
      loading.value = false;
    }
  }

  /**
   * Fetch single organizational unit with details
   */
  async function fetchUnit(id: number) {
    loading.value = true;
    try {
      const response = await orgUnitApi.getOrgUnit(id);
      currentUnit.value = response.data;

      // Also populate members and suppliers arrays
      members.value = response.data.members || [];
      suppliers.value = response.data.suppliers || [];

      return response.data;
    } finally {
      loading.value = false;
    }
  }

  /**
   * Create new organizational unit
   */
  async function createUnit(payload: orgUnitApi.CreateOrgUnitPayload): Promise<OrganizationalUnit> {
    const response = await orgUnitApi.createOrgUnit(payload);

    // Refresh the units list
    await fetchUnits();

    return response.data;
  }

  /**
   * Update organizational unit
   */
  async function updateUnit(
    id: number,
    payload: orgUnitApi.UpdateOrgUnitPayload,
  ): Promise<OrganizationalUnit> {
    const response = await orgUnitApi.updateOrgUnit(id, payload);

    // Refresh the units list
    await fetchUnits();

    // If this was the current unit, refresh it
    if (currentUnit.value && currentUnit.value.id === id) {
      await fetchUnit(id);
    }

    return response.data;
  }

  /**
   * Delete organizational unit
   */
  async function deleteUnit(id: number): Promise<void> {
    const response = await orgUnitApi.deleteOrgUnit(id);
    const deletedIds = new Set(response?.data?.deletedIds ?? [id]);

    if (deletedIds.size > 0) {
      units.value = units.value.filter((unit) => !deletedIds.has(unit.id));
    }

    if (currentUnit.value && deletedIds.has(currentUnit.value.id)) {
      currentUnit.value = null;
      members.value = [];
      suppliers.value = [];
    }

    await fetchUnits();
  }

  /**
   * Check if unit can be deleted
   */
  async function checkDeletion(id: number) {
    const response = await orgUnitApi.checkDeletion(id);
    return response.data;
  }

  /**
   * Move unit to a new parent
   */
  async function moveUnit(id: number, newParentId: number | null): Promise<OrganizationalUnit> {
    const response = await orgUnitApi.moveUnit(id, { newParentId });

    // Refresh the units list (tree structure changed)
    await fetchUnits();

    return response.data;
  }

  /**
   * Fetch unit members
   */
  async function fetchMembers(unitId: number) {
    loadingMembers.value = true;
    try {
      const response = await orgUnitApi.getMembers(unitId);
      members.value = response.data;

      // Update current unit if it's loaded
      if (currentUnit.value && currentUnit.value.id === unitId) {
        currentUnit.value.members = response.data;
      }

      return response.data;
    } finally {
      loadingMembers.value = false;
    }
  }

  /**
   * Add members to unit
   */
  async function addMembers(unitId: number, payload: orgUnitApi.AddMembersPayload) {
    const response = await orgUnitApi.addMembers(unitId, payload);

    // Refresh members list
    await fetchMembers(unitId);

    return response.data;
  }

  /**
   * Remove member from unit
   */
  async function removeMember(unitId: number, userId: string): Promise<void> {
    await orgUnitApi.removeMember(unitId, userId);

    // Update local state
    members.value = members.value.filter((m) => m.userId !== userId);

    // Update current unit if it's loaded
    if (currentUnit.value && currentUnit.value.id === unitId) {
      currentUnit.value.members = members.value;
    }
  }

  /**
   * Fetch unit suppliers
   */
  async function fetchSuppliers(unitId: number) {
    loadingSuppliers.value = true;
    try {
      const response = await orgUnitApi.getSuppliers(unitId);
      suppliers.value = response.data;

      // Update current unit if it's loaded
      if (currentUnit.value && currentUnit.value.id === unitId) {
        currentUnit.value.suppliers = response.data;
      }

      return response.data;
    } finally {
      loadingSuppliers.value = false;
    }
  }

  /**
   * Add suppliers to unit
   */
  async function addSuppliers(unitId: number, payload: orgUnitApi.AddSuppliersPayload) {
    const response = await orgUnitApi.addSuppliers(unitId, payload);

    // Refresh suppliers list
    await fetchSuppliers(unitId);

    return response.data;
  }

  /**
   * Remove supplier from unit
   */
  async function removeSupplier(unitId: number, supplierId: number): Promise<void> {
    await orgUnitApi.removeSupplier(unitId, supplierId);

    // Update local state
    suppliers.value = suppliers.value.filter((s) => s.supplierId !== supplierId);

    // Update current unit if it's loaded
    if (currentUnit.value && currentUnit.value.id === unitId) {
      currentUnit.value.suppliers = suppliers.value;
    }
  }

  return {
    // State
    units,
    currentUnit,
    members,
    suppliers,
    loading,
    loadingMembers,
    loadingSuppliers,

    // Actions
    fetchUnits,
    fetchUnit,
    createUnit,
    updateUnit,
    deleteUnit,
    checkDeletion,
    moveUnit,
    fetchMembers,
    addMembers,
    removeMember,
    fetchSuppliers,
    addSuppliers,
    removeSupplier,
  };
});
