import { apiFetch, BASE_URL } from "./http";

export interface FieldChange {
  field: string;
  fieldLabel: string;
  oldValue: unknown;
  newValue: unknown;
  changeType: 'added' | 'modified' | 'removed';
}

export interface AuditLogChanges {
  fieldChanges?: FieldChange[];
  oldData?: Record<string, unknown>;
  newData?: Record<string, unknown>;
  added?: unknown[];
  modified?: unknown[];
  removed?: unknown[];
}

export interface AuditLogEntry {
  id: number;
  actorId: string | null;
  actorName: string | null;
  entityType: string | null;
  entityId: string | null;
  action: string | null;
  changes: AuditLogChanges | Record<string, unknown> | null;
  summary: string | null;
  ipAddress: string | null;
  isSensitive: number;
  immutable: number;
  hashChainValue: string | null;
  createdAt: string;
}

export interface AuditLogQuery {
  entityType?: string;
  entityId?: string;
  actorId?: string;
  ipAddress?: string;
  keyword?: string;
  startDate?: string;
  endDate?: string;
  isSensitive?: boolean | string;
  page?: number;
  limit?: number;
  includeDiagnostics?: boolean;
}

export interface AuditLogResponse {
  data: AuditLogEntry[];
  page: number;
  pageSize: number;
  total?: number;
  totalPages?: number;
  queryTime?: number;
  querySource?: string;
  performanceWarning?: string;
  appliedFilters?: string[];
  diagnostics?: {
    aggregator?: AggregatorDiagnostics;
  };
}

export interface AuditArchiveMetadata {
  id: number;
  auditLogId: number;
  archiveFilePath: string;
  fileHash: string;
  archiveDate: string;
  verifiedAt: string | null;
  verificationStatus: string | null;
}

export interface AuditArchiveStats {
  totalArchived: number;
  totalVerified: number;
  totalFailed: number;
  oldestArchive: string | null;
  newestArchive: string | null;
}

export interface VerificationResult {
  valid: boolean;
  message: string;
  details?: Record<string, unknown>;
  brokenChains?: Array<{
    logId: number;
    expectedHash: string;
    actualHash: string;
    createdAt: string;
  }>;
  verifiedCount?: number;
}

export interface AggregatorFailureInfo {
  backend: string;
  message?: string;
  at: string;
}

export interface AggregatorQuerySnapshot {
  backend: string;
  status: string;
  durationMs: number;
  filters: string[];
  at: string;
  error?: { message?: string };
  note?: string;
}

export interface AggregatorDiagnostics {
  enabled: boolean;
  backends: {
    elastic: boolean;
    loki: boolean;
  };
  pushes: {
    total: number;
    success: number;
    failure: number;
    lastSuccessAt: string | null;
    lastFailure: AggregatorFailureInfo | null;
    backends: Record<
      string,
      {
        total: number;
        success: number;
        failure: number;
        lastSuccessAt?: string | null;
        lastFailure?: AggregatorFailureInfo | null;
      }
    >;
  };
  queries: {
    total: number;
    success: number;
    failure: number;
    skipped: number;
    last: AggregatorQuerySnapshot | null;
    backends: Record<
      string,
      {
        total: number;
        success: number;
        failure: number;
        skipped?: number;
        lastSuccess?: AggregatorQuerySnapshot | null;
        lastFailure?: AggregatorQuerySnapshot | null;
      }
    >;
  };
}

const unwrap = <T>(payload: { data: T } | T): T => {
  if (payload && typeof payload === "object" && "data" in payload) {
    return (payload as { data: T }).data;
  }
  return payload as T;
};

export const listAuditLogs = async (query: AuditLogQuery = {}): Promise<AuditLogResponse> => {
  const params = new URLSearchParams();
  if (query.entityType) params.set("entityType", query.entityType);
  if (query.entityId) params.set("entityId", query.entityId);
  if (query.actorId) params.set("actorId", query.actorId);
  if (query.ipAddress) params.set("ipAddress", query.ipAddress);
  if (query.keyword) params.set("keyword", query.keyword);
  if (query.startDate) params.set("startDate", query.startDate);
  if (query.endDate) params.set("endDate", query.endDate);
  if (query.isSensitive !== undefined) params.set("isSensitive", String(query.isSensitive));
  if (query.page) params.set("page", String(query.page));
  if (query.limit) params.set("limit", String(query.limit));
  if (query.includeDiagnostics) params.set("includeDiagnostics", "true");

  const queryString = params.toString();
  const response = await apiFetch<AuditLogResponse>(
    `/audit${queryString ? `?${queryString}` : ""}`,
  );

  return response as AuditLogResponse;
};

export const getAuditLog = async (id: number): Promise<AuditLogEntry> => {
  const response = await apiFetch<{ data: AuditLogEntry }>(`/audit/${id}`);
  return unwrap(response);
};

export const getAuditAggregatorDiagnostics = async (): Promise<AggregatorDiagnostics> => {
  const response = await apiFetch<{ data: AggregatorDiagnostics }>("/audit/diagnostics/aggregator");
  return unwrap(response);
};

export const deleteAuditLog = async (id: number): Promise<{ message: string; id: number }> => {
  const response = await apiFetch<{ message: string; id: number }>(`/audit/${id}`, {
    method: "DELETE",
  });
  return response;
};

// Archive management APIs
export const getArchiveStats = async (): Promise<AuditArchiveStats> => {
  const response = await apiFetch<{ data: AuditArchiveStats }>("/audit-archive/stats");
  return unwrap(response);
};

export const verifyArchivedLog = async (id: number): Promise<VerificationResult> => {
  const response = await apiFetch<{ data: VerificationResult }>(`/audit-archive/verify/${id}`, {
    method: "POST",
  });
  return unwrap(response);
};

export const verifyHashChain = async (
  startId?: number,
  endId?: number,
): Promise<VerificationResult> => {
  const params = new URLSearchParams();
  if (startId) params.set("startId", String(startId));
  if (endId) params.set("endId", String(endId));

  const queryString = params.toString();
  const response = await apiFetch<{ data: VerificationResult }>(
    `/audit-archive/verify-chain${queryString ? `?${queryString}` : ""}`,
    { method: "POST" },
  );
  return unwrap(response);
};

export const getArchiveMetadata = async (id: number): Promise<AuditArchiveMetadata> => {
  const response = await apiFetch<{ data: AuditArchiveMetadata }>(`/audit-archive/metadata/${id}`);
  return unwrap(response);
};

export const exportAuditLogs = async (filters: {
  startDate?: string;
  endDate?: string;
  isSensitive?: boolean;
  entityType?: string;
  entityId?: string;
}): Promise<Blob> => {
  const params = new URLSearchParams();
  if (filters.startDate) params.set("startDate", filters.startDate);
  if (filters.endDate) params.set("endDate", filters.endDate);
  if (filters.isSensitive !== undefined) params.set("isSensitive", String(filters.isSensitive));
  if (filters.entityType) params.set("entityType", filters.entityType);
  if (filters.entityId) params.set("entityId", filters.entityId);

  const queryString = params.toString();
  const response = await fetch(
    `${BASE_URL}/audit-archive/export${queryString ? `?${queryString}` : ""}`,
    {
      headers: {
        Authorization: `Bearer ${localStorage.getItem("token")}`,
      },
    },
  );

  if (!response.ok) {
    throw new Error("Failed to export audit logs");
  }

  return response.blob();
};

// Convenience function for Vue components
export const getAuditLogs = listAuditLogs;
