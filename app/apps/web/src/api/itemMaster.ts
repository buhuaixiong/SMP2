import { apiFetch } from "./http";

export interface ItemMasterRecord {
  id: number;
  fac: string;
  itemNumber: string;
  vendor: string;
  sourcingName: string | null;
  ownerUserId: string | null;
  ownerUsernameSnapshot: string | null;
  itemDescription: string | null;
  unit: string | null;
  moq: number | null;
  spq: number | null;
  currency: string | null;
  priceBreak1: number | null;
  exchangeRate: number | null;
  vendorName: string | null;
  terms: string | null;
  termsDesc: string | null;
  company: string | null;
  class: string | null;
  updatedAt: string | null;
  lastImportBatchId: number | null;
}

export interface ItemMasterImportBatch {
  id: number;
  fileName: string;
  sheetScope: string;
  status: string;
  startedAt: string;
  finishedAt: string | null;
  importedByUserId: string;
  importedByName: string | null;
  insertedCount: number;
  updatedCount: number;
  warningCount: number;
  errorCount: number;
}

export interface PaginationPayload {
  page: number;
  limit: number;
  total: number;
  totalPages: number;
}

export interface ItemMasterListResponse {
  data: ItemMasterRecord[];
  pagination: PaginationPayload;
}

export interface ItemMasterBatchListResponse {
  data: ItemMasterImportBatch[];
  pagination: PaginationPayload;
}

export interface ItemMasterImportResponse {
  batchId: number;
  status: string;
  insertedCount: number;
  updatedCount: number;
  warningCount: number;
  errorCount: number;
  warnings: string[];
  errors: string[];
  fatalMessage?: string | null;
}

export interface ItemMasterListParams {
  fac?: string;
  itemNumber?: string;
  vendor?: string;
  sourcingName?: string;
  unassignedOnly?: boolean;
  page?: number;
  limit?: number;
}

const toQuery = (params: Record<string, unknown>): string => {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value == null || value === "") {
      return;
    }
    query.set(key, String(value));
  });

  const encoded = query.toString();
  return encoded ? `?${encoded}` : "";
};

export const importItemMaster = async (
  file: File,
  sheets: string[] = [],
): Promise<ItemMasterImportResponse> => {
  const form = new FormData();
  form.append("file", file);
  sheets
    .map((sheet) => sheet.trim())
    .filter((sheet) => sheet.length > 0)
    .forEach((sheet) => form.append("sheets", sheet));

  return apiFetch<ItemMasterImportResponse>("/item-master/import", {
    method: "POST",
    body: form,
    timeout: 0,
  });
};

export const listItemMasterRecords = async (
  params: ItemMasterListParams = {},
): Promise<ItemMasterListResponse> => {
  return apiFetch<ItemMasterListResponse>(`/item-master${toQuery(params as Record<string, unknown>)}`);
};

export const listItemMasterImportBatches = async (
  page = 1,
  limit = 20,
): Promise<ItemMasterBatchListResponse> => {
  return apiFetch<ItemMasterBatchListResponse>(`/item-master/import-batches${toQuery({ page, limit })}`);
};

export const getItemMasterImportBatch = async (id: number): Promise<{ data: unknown }> => {
  return apiFetch<{ data: unknown }>(`/item-master/import-batches/${id}`);
};
