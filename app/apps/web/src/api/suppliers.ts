// src/api/suppliers.ts (已修正)

import { apiFetch } from "./http";
import type {
  Supplier,
  SupplierTag,
  ApprovalRecord,
  SupplierDocument,
  Contract,
  SupplierRating,
  RatingsSummary,
  SupplierStage,
  SupplierCompanyType,
  SupplierImportResponse,
  SupplierCompletionStatus,
} from "@/types";
import { buildQueryParams, normalizePaginatedResponse } from "@/utils/pagination";

export interface SupplierFilters {
  id?: number;
  status?: string;
  category?: string;
  region?: string;
  importance?: string;
  tag?: string;
  q?: string;
  query?: string;
  stage?: string;
  completionStatus?: SupplierCompletionStatus;
  missingDocument?: string | string[];
  missingProfile?: string | string[];
  incomplete?: boolean;
  limit?: number;
  offset?: number;
  forRfq?: boolean;
}

export interface PaginatedSupplierResponse {
  data: Supplier[];
  pagination: {
    total: number;
    limit: number;
    offset: number;
    hasMore: boolean;
  };
}

export interface SupplierPayload {
  companyName: string;
  companyId: string;
  contactPerson: string;
  contactPhone: string;
  contactEmail: string;
  category: string;
  address: string;
  stage?: SupplierStage | null;
  bankAccount?: string | null;
  paymentTerms?: string | null;
  creditRating?: string | null;
  serviceCategory?: string | null;
  region?: string | null;
  importance?: string | null;
  complianceStatus?: string | null;
  complianceNotes?: string | null;
  complianceOwner?: string | null;
  complianceReviewedAt?: string | null;
  financialContact?: string | null;
  paymentCurrency?: string | null;
  englishName?: string | null;
  chineseName?: string | null;
  companyType?: SupplierCompanyType | null;
  companyTypeOther?: string | null;
  authorizedCapital?: string | null;
  issuedCapital?: string | null;
  directors?: string | null;
  owners?: string | null;
  registeredOffice?: string | null;
  businessRegistrationNumber?: string | null;
  businessAddress?: string | null;
  businessPhone?: string | null;
  faxNumber?: string | null;
  salesContactName?: string | null;
  salesContactEmail?: string | null;
  salesContactPhone?: string | null;
  financeContactName?: string | null;
  financeContactEmail?: string | null;
  financeContactPhone?: string | null;
  customerServiceContactName?: string | null;
  customerServiceContactEmail?: string | null;
  customerServiceContactPhone?: string | null;
  businessNature?: string | null;
  operatingCurrency?: string | null;
  deliveryLocation?: string | null;
  shipCode?: string | null;
  productOrigin?: string | null;
  productsForEci?: string | null;
  establishedYear?: string | null;
  employeeCount?: string | null;
  qualityCertifications?: string | null;
  invoiceType?: string | null;
  paymentTermsDays?: string | null;
  paymentMethods?: string[];
  paymentMethodsOther?: string | null;
  bankName?: string | null;
  bankAddress?: string | null;
  swiftCode?: string | null;
  notes?: string | null;
  tags?: string[];
  actorId?: string;
  actorName?: string;
}

export const listSuppliers = async (filters: SupplierFilters = {}) => {
  const query = buildQueryParams(filters as Record<string, unknown>);
  const response = await apiFetch<Record<string, unknown>>(`/suppliers${query ? `?${query}` : ""}`);
  return normalizePaginatedResponse<Supplier>(response, {
    defaultLimit: filters.limit,
    defaultOffset: filters.offset,
  });
};

export const getSupplier = (id: number) => apiFetch<Supplier>(`/suppliers/${id}`);

export const createSupplier = (payload: SupplierPayload) =>
  apiFetch<Supplier>("/suppliers", {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const updateSupplier = (id: number, payload: Partial<SupplierPayload>) =>
  apiFetch<Supplier>(`/suppliers/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

export const deleteSupplier = (id: number) =>
  apiFetch<void>(`/suppliers/${id}`, {
    method: "DELETE",
    parseData: false,
  });

export const updateSupplierStatus = (
  id: number,
  payload: {
    status: string;
    currentApprover?: string | null;
    notes?: string | null;
    actorId?: string;
    actorName?: string;
  },
) =>
  apiFetch<Supplier>(`/suppliers/${id}/status`, {
    method: "PATCH",
    body: JSON.stringify(payload),
  });

export const approveSupplier = (
  id: number,
  payload: {
    decision: "approved" | "rejected";
    approver: string;
    comments?: string;
    actorId?: string;
  },
) =>
  apiFetch<Supplier>(`/suppliers/${id}/approve`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

export const setSupplierTags = (
  id: number,
  tags: string[],
  actor?: { actorId?: string; actorName?: string },
) =>
  apiFetch<SupplierTag[]>(`/suppliers/${id}/tags`, {
    method: "PUT",
    body: JSON.stringify({ tags, ...actor }),
  });

export const listTags = () => apiFetch<{ data: SupplierTag[] }>("/suppliers/tags");

export const createTag = (payload: { name: string; description?: string; color?: string }) =>
  apiFetch<{ data: SupplierTag }>("/suppliers/tags", {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const deleteTag = (id: number) =>
  apiFetch<void>(`/suppliers/tags/${id}`, {
    method: "DELETE",
    parseData: false,
  });

export const updateTag = (
  id: number,
  payload: { name: string; description?: string; color?: string },
) =>
  apiFetch<{ data: SupplierTag }>(`/suppliers/tags/${id}`, {
    method: "PUT",
    body: payload,
  });

export const getSupplierStats = () => apiFetch<SupplierStats>("/suppliers/stats");

// --- 修正后的函数 ---
export const importSuppliersFromExcel = async (file: File): Promise<SupplierImportResponse> => {
  const form = new FormData();
  form.append("file", file);

  // 移除了手动处理 Token 的逻辑，完全依赖 http.ts 中的拦截器
  const result = await apiFetch<{ data: SupplierImportResponse }>("/suppliers/import", {
    method: "POST",
    body: form,
    timeout: 0,
  });

  return result.data;
};
// --- 修正结束 ---

export interface SupplierStats {
  totalSuppliers: number;
  completedSuppliers: number;
  mostlyCompleteSuppliers: number;
  pendingSuppliers: number;
  completionRate: number;
  averageCompletion: number;
  completionBuckets: Record<SupplierCompletionStatus, number>;
  requirementCatalog: {
    documents: Array<{ type: string; label: string; category: string | null }>;
    profileFields: Array<{ key: string; label: string }>;
  };
}

export interface SupplierDetail extends Supplier {
  documents: SupplierDocument[];
  contracts: Contract[];
  ratingsSummary: RatingsSummary;
  latestRating: SupplierRating | null;
  approvalHistory: ApprovalRecord[];
}

export interface HistoryEvent {
  id?: number;
  timestamp: string;
  action: string;
  entityType?: string;
  entityId?: string;
  actor?: string;
  actorName?: string;
  changes?: Record<string, unknown>;
}

export interface SupplierHistoryResponse {
  data: HistoryEvent[];
  pagination: {
    total: number;
    limit: number;
    offset: number;
    hasMore: boolean;
  };
}

export const getSupplierHistory = (
  supplierId: number,
  params?: { limit?: number; offset?: number },
) => {
  const query = buildQueryParams(params || {});
  return apiFetch<SupplierHistoryResponse>(
    `/suppliers/${supplierId}/history${query ? `?${query}` : ""}`,
  );
};

export interface ApprovalPreviewStep {
  key: string;
  title: string;
  role: string;
  slaDays: number;
  eta: string;
  status: string;
  description?: string;
}

export interface ApprovalRiskFlag {
  key: string;
  level: "info" | "warning" | "critical";
  message: string;
}

export interface ApprovalPreview {
  template: string;
  templateLabel: string;
  estimatedWorkingDays: number;
  steps: ApprovalPreviewStep[];
  riskFlags: ApprovalRiskFlag[];
  categoryAChanges: string[];
  requiresTempAccount: boolean;
  changeSummary: Record<string, unknown>;
  generatedAt: string;
}

export interface ApprovalPreviewRequest {
  payload: Record<string, unknown> | object;
  changedFields?: string[];
  supplierId?: number | null;
  startDate?: string | null;
  baselineVersion?: number | null;
  context?: Record<string, unknown>;
}

export interface TempAccountRequest {
  currency?: string;
  expiresInDays?: number;
  expiresAt?: string;
  forceReissue?: boolean;
  password?: string;
}

export interface TempAccountResponse {
  supplierId: number;
  username: string;
  password: string;
  status: string;
  currency: string;
  sequenceNumber: number;
  expiresAt: string;
  issuedAt: string;
  forceReissue: boolean;
}

export interface FinalizeSupplierPayload {
  supplierCode: string;
  password?: string;
  stage?: string;
}

export interface FinalizeSupplierResponse {
  supplierId: number;
  supplierCode: string;
  defaultPassword: string;
  stage: string;
  finalizedAt: string;
}

export const previewApprovalFlow = (payload: ApprovalPreviewRequest) =>
  apiFetch<{ data: ApprovalPreview }>("/suppliers/preview-approval", {
    method: "POST",
    body: JSON.stringify(payload),
  }).then((response) => response.data);

export const issueTempAccount = (supplierId: number, payload: TempAccountRequest = {}) =>
  apiFetch<{ data: TempAccountResponse }>(`/suppliers/${supplierId}/temp-accounts`, {
    method: "POST",
    body: JSON.stringify(payload),
  }).then((response) => response.data);

export const finalizeSupplierCode = (
  supplierId: number,
  payload: FinalizeSupplierPayload,
) =>
  apiFetch<{ data: FinalizeSupplierResponse }>(`/suppliers/${supplierId}/finalize-code`, {
    method: "POST",
    body: JSON.stringify(payload),
  }).then((response) => response.data);

// Batch tag assignment operations
export const batchAssignTag = (tagId: number, supplierIds: number[]) =>
  apiFetch<{
    message: string;
    data: { added: number; skipped: number };
  }>(`/suppliers/tags/${tagId}/batch-assign`, {
    method: "POST",
    body: JSON.stringify({ supplierIds }),
  });

export const batchRemoveTag = (tagId: number, supplierIds: number[]) =>
  apiFetch<{
    message: string;
    data: { removed: number };
  }>(`/suppliers/tags/${tagId}/batch-remove`, {
    method: "POST",
    body: JSON.stringify({ supplierIds }),
  });

export const getSuppliersByTag = (tagId: number) =>
  apiFetch<{ data: Supplier[] }>(`/suppliers/tags/${tagId}/suppliers`);

export async function fetchSuppliers(filters: SupplierFilters = {}): Promise<Supplier[]> {
  const response = await listSuppliers(filters);
  const list = (response.data ?? []) as Supplier[];
  return list.filter((supplier) => supplier && supplier.id != null);
}
