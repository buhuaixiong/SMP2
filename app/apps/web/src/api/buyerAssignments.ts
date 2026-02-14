import { apiFetch } from "./http";

export interface BuyerAssignment {
  id: number;
  buyerId: string;
  supplierId: number;
  createdAt: string;
  createdBy: string;
  companyName?: string;
  companyId?: string;
  category?: string;
  region?: string;
  status?: string;
}

export interface Buyer {
  id: string;
  name: string;
  role: string;
}

export interface AssignByTagPayload {
  buyerId: string;
  tagIds: number[];
}

export interface AssignByTagResponse {
  message: string;
  data: {
    assignedCount: number;
    totalSuppliers: number;
    supplierIds: number[];
  };
}

/**
 * Assign suppliers to buyer by tags
 */
export async function assignSuppliersByTag(
  payload: AssignByTagPayload,
): Promise<AssignByTagResponse> {
  return apiFetch<AssignByTagResponse>("/buyer-assignments/by-tag", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

/**
 * Get assigned suppliers for a buyer
 */
export async function getAssignedSuppliers(buyerId?: string): Promise<{ data: BuyerAssignment[] }> {
  const params = buyerId ? { buyerId } : {};
  return apiFetch<{ data: BuyerAssignment[] }>("/buyer-assignments/suppliers", { params });
}

/**
 * Remove a buyer-supplier assignment
 */
export async function removeAssignment(assignmentId: number): Promise<void> {
  return apiFetch<void>(`/buyer-assignments/${assignmentId}`, {
    method: "DELETE",
    parseData: false,
  });
}

/**
 * Get list of buyers (purchasers and procurement managers)
 */
export async function getBuyers(): Promise<{ data: Buyer[] }> {
  return apiFetch<{ data: Buyer[] }>("/buyer-assignments/buyers");
}
