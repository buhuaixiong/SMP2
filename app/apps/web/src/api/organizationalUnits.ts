import { get, post, put, del } from "./http";
import type { ApiResponse } from "@/types";

// ============================================================
// Types (will be moved to @/types/index.ts later)
// ============================================================

export interface OrganizationalUnit {
  id: number;
  code: string;
  name: string;
  type: "general" | "department" | "division" | "procurement";
  parentId: number | null;
  level: number;
  path: string | null;
  description?: string;
  adminIds: string[];
  category?: string;
  region?: string;
  isActive: number;
  createdAt: string;
  createdBy?: string;
  updatedAt: string;
  updatedBy?: string;
  deletedAt?: string;
  deletedBy?: string;

  // Enriched fields
  memberCount?: number;
  supplierCount?: number;
  contractCount?: number;
  childCount?: number;
  children?: OrganizationalUnit[];
}

export interface OrganizationalUnitDetail extends OrganizationalUnit {
  members: OrgUnitMember[];
  suppliers: OrgUnitSupplier[];
  contracts: OrgUnitContract[];
  ancestors: OrganizationalUnit[];
  descendantCount: number;
}

export interface OrgUnitMember {
  id: number;
  unitId: number;
  userId: string;
  userName?: string;
  userRole?: string;
  role: string; // 'member' | 'lead' | 'admin'
  joinedAt: string;
  assignedBy?: string;
  notes?: string;
}

export interface OrgUnitSupplier {
  id: number;
  unitId: number;
  supplierId: number;
  companyName?: string;
  companyId?: string;
  category?: string;
  region?: string;
  status?: string;
  assignedAt: string;
  assignedBy?: string;
  isPrimary: number;
  notes?: string;
}

export interface OrgUnitContract {
  id: number;
  unitId: number;
  contractId: number;
  title?: string;
  agreementNumber?: string;
  status?: string;
  effectiveFrom?: string;
  effectiveTo?: string;
  assignedAt: string;
  assignedBy?: string;
  notes?: string;
}

export interface DeletionCheck {
  canDelete: boolean;
  message?: string;
  error?: string;
  blockers: {
    members: number;
    suppliers: number;
    contracts: number;
    childUnits: number;
    affectedUnits: Array<{ id: number; name: string; level: number }>;
  };
}

export interface DeleteOrgUnitResult {
  deletedIds: number[];
}

export interface CreateOrgUnitPayload {
  code: string;
  name: string;
  type?: "general" | "department" | "division" | "procurement";
  parentId?: number | null;
  description?: string;
  category?: string;
  region?: string;
  adminId?: string | null;
  adminIds?: string[];
}

export interface UpdateOrgUnitPayload {
  name?: string;
  type?: "general" | "department" | "division" | "procurement";
  description?: string;
  category?: string;
  region?: string;
  isActive?: boolean;
  adminId?: string | null;
  adminIds?: string[];
}

export interface AddMembersPayload {
  userIds: string[];
  role?: string;
  notes?: string;
}

export interface AddSuppliersPayload {
  supplierIds: number[];
  isPrimary?: boolean;
  notes?: string;
}

export interface MoveUnitPayload {
  newParentId: number | null;
}

export interface ListOrgUnitsParams {
  type?: string;
  isActive?: boolean | string;
  format?: "tree" | "flat";
  parentId?: number | string;
}

// ============================================================
// API Functions
// ============================================================

/**
 * List all organizational units
 */
export async function listOrgUnits(
  params?: ListOrgUnitsParams,
): Promise<ApiResponse<OrganizationalUnit[]>> {
  return get<ApiResponse<OrganizationalUnit[]>>("/org-units", { params });
}

/**
 * Get single organizational unit with full details
 */
export async function getOrgUnit(id: number): Promise<ApiResponse<OrganizationalUnitDetail>> {
  return get<ApiResponse<OrganizationalUnitDetail>>(`/org-units/${id}`);
}

/**
 * Create new organizational unit
 */
export async function createOrgUnit(
  payload: CreateOrgUnitPayload,
): Promise<ApiResponse<OrganizationalUnit>> {
  return post<ApiResponse<OrganizationalUnit>>("/org-units", payload);
}

/**
 * Update organizational unit
 */
export async function updateOrgUnit(
  id: number,
  payload: UpdateOrgUnitPayload,
): Promise<ApiResponse<OrganizationalUnit>> {
  return put<ApiResponse<OrganizationalUnit>>(`/org-units/${id}`, payload);
}

/**
 * Delete (soft delete) organizational unit
 */
export async function deleteOrgUnit(id: number): Promise<ApiResponse<DeleteOrgUnitResult>> {
  return del<ApiResponse<DeleteOrgUnitResult>>(`/org-units/${id}`);
}

/**
 * Check if unit can be safely deleted
 */
export async function checkDeletion(id: number): Promise<ApiResponse<DeletionCheck>> {
  return get<ApiResponse<DeletionCheck>>(`/org-units/${id}/cascade-check`);
}

/**
 * Move unit to a new parent
 */
export async function moveUnit(
  id: number,
  payload: MoveUnitPayload,
): Promise<ApiResponse<OrganizationalUnit>> {
  return post<ApiResponse<OrganizationalUnit>>(`/org-units/${id}/move`, payload);
}

/**
 * Get unit members
 */
export async function getMembers(unitId: number): Promise<ApiResponse<OrgUnitMember[]>> {
  return get<ApiResponse<OrgUnitMember[]>>(`/org-units/${unitId}/members`);
}

/**
 * Add users to unit
 */
export async function addMembers(
  unitId: number,
  payload: AddMembersPayload,
): Promise<ApiResponse<{ addedCount: number }>> {
  return post<ApiResponse<{ addedCount: number }>>(`/org-units/${unitId}/members`, payload);
}

/**
 * Remove user from unit
 */
export async function removeMember(unitId: number, userId: string): Promise<void> {
  return del<void>(`/org-units/${unitId}/members/${userId}`);
}

/**
 * Get unit suppliers
 */
export async function getSuppliers(unitId: number): Promise<ApiResponse<OrgUnitSupplier[]>> {
  return get<ApiResponse<OrgUnitSupplier[]>>(`/org-units/${unitId}/suppliers`);
}

/**
 * Add suppliers to unit
 */
export async function addSuppliers(
  unitId: number,
  payload: AddSuppliersPayload,
): Promise<ApiResponse<{ addedCount: number }>> {
  return post<ApiResponse<{ addedCount: number }>>(`/org-units/${unitId}/suppliers`, payload);
}

/**
 * Remove supplier from unit
 */
export async function removeSupplier(unitId: number, supplierId: number): Promise<void> {
  return del<void>(`/org-units/${unitId}/suppliers/${supplierId}`);
}
