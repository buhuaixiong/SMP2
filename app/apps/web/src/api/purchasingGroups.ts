import { get, post, put, del } from "./http";
import type {
  ApiResponse,
  PurchasingGroup,
  PurchasingGroupDetail,
  PurchasingGroupMember,
  PurchasingGroupSupplier,
  BuyerAssignment,
  WorkloadAnalysis,
  CreatePurchasingGroupPayload,
  UpdatePurchasingGroupPayload,
  AddGroupMembersPayload,
  AddGroupSuppliersPayload,
} from "@/types";

export interface ListGroupsParams {
  category?: string;
  region?: string;
  isActive?: boolean | string;
  q?: string;
}

/**
 * List all purchasing groups
 */
export async function listGroups(
  params?: ListGroupsParams,
): Promise<ApiResponse<PurchasingGroup[]>> {
  return get<ApiResponse<PurchasingGroup[]>>("/purchasing-groups", { params });
}

/**
 * Get single purchasing group with members and suppliers
 */
export async function getGroup(id: number): Promise<ApiResponse<PurchasingGroupDetail>> {
  return get<ApiResponse<PurchasingGroupDetail>>(`/purchasing-groups/${id}`);
}

/**
 * Create new purchasing group
 */
export async function createGroup(
  payload: CreatePurchasingGroupPayload,
): Promise<ApiResponse<PurchasingGroup>> {
  return post<ApiResponse<PurchasingGroup>>("/purchasing-groups", payload);
}

/**
 * Update purchasing group
 */
export async function updateGroup(
  id: number,
  payload: UpdatePurchasingGroupPayload,
): Promise<ApiResponse<PurchasingGroup>> {
  return put<ApiResponse<PurchasingGroup>>(`/purchasing-groups/${id}`, payload);
}

/**
 * Delete (soft delete) purchasing group
 */
export async function deleteGroup(id: number): Promise<void> {
  return del<void>(`/purchasing-groups/${id}`);
}

/**
 * Get group members (buyers)
 */
export async function getMembers(groupId: number): Promise<ApiResponse<PurchasingGroupMember[]>> {
  return get<ApiResponse<PurchasingGroupMember[]>>(`/purchasing-groups/${groupId}/members`);
}

/**
 * Add buyers to group
 */
export async function addMembers(
  groupId: number,
  payload: AddGroupMembersPayload,
): Promise<ApiResponse<{ addedCount: number }>> {
  return post<ApiResponse<{ addedCount: number }>>(
    `/purchasing-groups/${groupId}/members`,
    payload,
  );
}

/**
 * Remove buyer from group
 */
export async function removeMember(groupId: number, buyerId: string): Promise<void> {
  return del<void>(`/purchasing-groups/${groupId}/members/${buyerId}`);
}

/**
 * Get group suppliers
 */
export async function getSuppliers(
  groupId: number,
): Promise<ApiResponse<PurchasingGroupSupplier[]>> {
  return get<ApiResponse<PurchasingGroupSupplier[]>>(`/purchasing-groups/${groupId}/suppliers`);
}

/**
 * Add suppliers to group
 */
export async function addSuppliers(
  groupId: number,
  payload: AddGroupSuppliersPayload,
): Promise<ApiResponse<{ addedCount: number }>> {
  return post<ApiResponse<{ addedCount: number }>>(
    `/purchasing-groups/${groupId}/suppliers`,
    payload,
  );
}

/**
 * Remove supplier from group
 */
export async function removeSupplier(groupId: number, supplierId: number): Promise<void> {
  return del<void>(`/purchasing-groups/${groupId}/suppliers/${supplierId}`);
}

/**
 * Get workload analysis for all buyers
 */
export async function getWorkloadAnalysis(): Promise<ApiResponse<WorkloadAnalysis>> {
  return get<ApiResponse<WorkloadAnalysis>>("/purchasing-groups/analytics/workload");
}

/**
 * Manually rebuild the buyer-supplier access cache
 */
export async function rebuildCache(): Promise<ApiResponse<{ message: string }>> {
  return post<ApiResponse<{ message: string }>>("/purchasing-groups/admin/rebuild-cache");
}

/**
 * Get buyer-supplier assignments within a purchasing group
 */
export async function getBuyerAssignments(
  groupId: number,
): Promise<ApiResponse<BuyerAssignment[]>> {
  return get<ApiResponse<BuyerAssignment[]>>(`/purchasing-groups/${groupId}/buyer-assignments`);
}

/**
 * Assign suppliers to a buyer within the purchasing group
 */
export async function assignToBuyer(
  groupId: number,
  payload: { buyerId: string; supplierIds: number[] },
): Promise<ApiResponse<{ assignedCount: number }>> {
  return post<ApiResponse<{ assignedCount: number }>>(
    `/purchasing-groups/${groupId}/assign-to-buyer`,
    payload,
  );
}

/**
 * Remove a buyer-supplier assignment
 */
export async function removeBuyerAssignment(
  groupId: number,
  assignmentId: number,
): Promise<void> {
  return del<void>(`/purchasing-groups/${groupId}/buyer-assignments/${assignmentId}`);
}
