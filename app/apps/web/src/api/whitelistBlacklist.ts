import { apiFetch } from "./http";

/**
 * Whitelist & Blacklist API Module
 * Handles document exemptions and registration blocks for procurement director
 */

// ============================================================
// Types
// ============================================================

export interface WhitelistExemption {
  id: number;
  supplier_id: number;
  supplier_name?: string;
  supplier_code?: string;
  supplier_stage?: string;
  document_type: string;
  exempted_by: string;
  exempted_by_name?: string;
  exempted_at: string;
  reason: string;
  expires_at?: string | null;
  is_active: number;
}

export interface BlacklistEntry {
  id: number;
  blacklist_type: "credit_code" | "email";
  blacklist_value: string;
  reason: string;
  severity: "critical" | "high" | "medium";
  added_by: string;
  added_by_name?: string;
  added_at: string;
  expires_at?: string | null;
  is_active: number;
  notes?: string | null;
}

export interface AddWhitelistExemptionPayload {
  supplier_id: number;
  document_type:
    | "quality_assurance_agreement"
    | "quality_compensation_agreement"
    | "quality_kpi_targets"
    | "incoming_packaging_transport_agreement";
  reason: string;
  expires_at?: string | null;
}

export interface AddBlacklistEntryPayload {
  blacklist_type: "credit_code" | "email";
  blacklist_value: string;
  reason: string;
  severity?: "critical" | "high" | "medium";
  expires_at?: string | null;
  notes?: string | null;
}

export interface BlacklistValidationResult {
  is_blacklisted: boolean;
  reason?: string | null;
  severity?: string | null;
  blacklist_type?: string | null;
}

// ============================================================
// Supplier Search for Whitelist Selector
// ============================================================

export interface SupplierSearchResult {
  id: number;
  companyId: string;
  companyName: string;
  stage: string;
  category?: string;
  contactPerson?: string;
}

export interface SupplierSearchResponse {
  suppliers: SupplierSearchResult[];
  total: number;
}

// ============================================================
// Whitelist API Functions
// ============================================================

/**
 * Get all whitelist exemptions
 */
export async function fetchWhitelistExemptions(params?: {
  supplier_id?: number;
  document_type?: string;
  is_active?: boolean;
}): Promise<{ exemptions: WhitelistExemption[] }> {
  return await apiFetch("/whitelist/exemptions", {
    method: "GET",
    params,
  });
}

/**
 * Add a document exemption for a supplier
 */
export async function addWhitelistExemption(
  payload: AddWhitelistExemptionPayload,
): Promise<{ message: string; exemption_id: number }> {
  return await apiFetch("/whitelist/exemptions", {
    method: "POST",
    body: payload,
  });
}

/**
 * Delete a whitelist exemption
 */
export async function deleteWhitelistExemption(id: number): Promise<{ message: string }> {
  return await apiFetch(`/whitelist/exemptions/${id}`, {
    method: "DELETE",
  });
}

/**
 * Deactivate a whitelist exemption
 */
export async function deactivateWhitelistExemption(id: number): Promise<{ message: string }> {
  return await apiFetch(`/whitelist/exemptions/${id}/deactivate`, {
    method: "PATCH",
  });
}

// ============================================================
// Blacklist API Functions
// ============================================================

/**
 * Get all blacklist entries
 */
export async function fetchBlacklistEntries(params?: {
  blacklist_type?: "credit_code" | "email";
  severity?: "critical" | "high" | "medium";
  is_active?: boolean;
}): Promise<{ entries: BlacklistEntry[] }> {
  return await apiFetch("/blacklist/entries", {
    method: "GET",
    params,
  });
}

/**
 * Add a new blacklist entry
 */
export async function addBlacklistEntry(
  payload: AddBlacklistEntryPayload,
): Promise<{ message: string; entry_id: number }> {
  return await apiFetch("/blacklist/entries", {
    method: "POST",
    body: payload,
  });
}

/**
 * Delete a blacklist entry
 */
export async function deleteBlacklistEntry(id: number): Promise<{ message: string }> {
  return await apiFetch(`/blacklist/entries/${id}`, {
    method: "DELETE",
  });
}

/**
 * Deactivate a blacklist entry
 */
export async function deactivateBlacklistEntry(id: number): Promise<{ message: string }> {
  return await apiFetch(`/blacklist/entries/${id}/deactivate`, {
    method: "PATCH",
  });
}

/**
 * Validate if a credit code or email is blacklisted
 * (Public endpoint - no auth required)
 */
export async function validateBlacklist(params: {
  credit_code?: string;
  email?: string;
}): Promise<BlacklistValidationResult> {
  return await apiFetch("/blacklist/validate", {
    method: "POST",
    body: params,
  });
}

// ============================================================
// Supplier Search API Functions
// ============================================================

/**
 * Search suppliers for whitelist selector
 */
export async function searchSuppliers(params?: {
  q?: string;
  stage?: string;
  limit?: number;
  offset?: number;
}): Promise<SupplierSearchResponse> {
  return await apiFetch("/whitelist/suppliers/search", {
    method: "GET",
    params,
  });
}
