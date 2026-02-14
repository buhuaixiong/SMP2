import { apiFetch } from "./http";
import type {
  User,
  BuyerSummary,
  BuyerAssignment,
  SupplierSummary,
  CreateUserPayload,
} from "@/types";

interface BuyerAssignmentResponseMeta {
  grantTypes?: Array<{ key: string; label: string }>;
  buyerId?: string;
  buyerName?: string;
  total?: number;
}

export interface RoleDefinition {
  role: string;
  label: string;
  permissions: string[];
}

type BuyerAssignmentUpdatePayload = Pick<
  BuyerAssignment,
  "supplierId" | "contractAlert" | "profileAccess"
>;

const unwrap = <T>(payload: { data: T } | T): T => {
  if (payload && typeof payload === "object" && "data" in payload) {
    return (payload as { data: T }).data;
  }
  return payload as T;
};

export const listRoles = async () => {
  const response = await apiFetch<{ data: RoleDefinition[] }>("/permissions/roles");
  return unwrap(response);
};

export const listUsers = async (scope: "internal" | "all" = "internal") => {
  const params = scope ? `?scope=${encodeURIComponent(scope)}` : "";
  const response = await apiFetch<{ data: User[] }>(`/permissions/users${params}`);
  return unwrap(response);
};

export const createUser = async (payload: CreateUserPayload) => {
  const response = await apiFetch<{ data: User }>("/permissions/users", {
    method: "POST",
    body: payload,
  });
  return unwrap(response);
};

export const updateUserRole = async (
  id: string,
  payload: { role: string; actorId?: string; actorName?: string },
) => {
  const response = await apiFetch<{ data: User }>(
    `/permissions/users/${encodeURIComponent(id)}/role`,
    {
      method: "PUT",
      body: payload,
    },
  );
  return unwrap(response);
};

export const deleteUser = async (id: string) => {
  const response = await apiFetch<{ message: string }>(
    `/permissions/users/${encodeURIComponent(id)}`,
    {
      method: "DELETE",
    },
  );
  return response;
};

export const resetUserPassword = async (id: string, password: string) => {
  const response = await apiFetch<{ message: string }>(
    `/permissions/users/${encodeURIComponent(id)}/password`,
    {
      method: "PUT",
      body: { password },
    },
  );
  return response;
};

export const updateUserEmail = async (id: string, email: string | null) => {
  const response = await apiFetch<{ data: User }>(
    `/permissions/users/${encodeURIComponent(id)}`,
    {
      method: "PUT",
      body: { email },
    },
  );
  return unwrap(response);
};

export const listBuyers = async () => {
  const response = await apiFetch<{ data: BuyerSummary[] }>("/permissions/buyers");
  return unwrap(response);
};

export const getBuyerAssignments = async (buyerId: string) => {
  const response = await apiFetch<{ data: BuyerAssignment[]; meta?: BuyerAssignmentResponseMeta }>(
    `/permissions/buyers/${encodeURIComponent(buyerId)}/assignments`,
  );
  const data = unwrap(response);
  const meta =
    response && typeof response === "object" && "meta" in response
      ? (response as { meta?: BuyerAssignmentResponseMeta }).meta
      : undefined;
  return { data, meta };
};

export const updateBuyerAssignments = async (
  buyerId: string,
  assignments: BuyerAssignmentUpdatePayload[],
) => {
  const response = await apiFetch<{ data: BuyerAssignment[]; meta?: BuyerAssignmentResponseMeta }>(
    `/permissions/buyers/${encodeURIComponent(buyerId)}/assignments`,
    {
      method: "POST",
      body: { assignments },
    },
  );
  const data = unwrap(response);
  const meta =
    response && typeof response === "object" && "meta" in response
      ? (response as { meta?: BuyerAssignmentResponseMeta }).meta
      : undefined;
  return { data, meta };
};

export const searchAssignableSuppliers = async (
  query: string,
  limit = 20,
): Promise<SupplierSummary[]> => {
  const params = new URLSearchParams({ limit: String(limit) });
  if (query) {
    params.set("q", query);
  }
  const response = await apiFetch<unknown>(`/permissions/suppliers/search?${params.toString()}`);
  const unwrapped = unwrap(response as { data: unknown } | unknown);
  const list = Array.isArray(unwrapped)
    ? unwrapped
    : Array.isArray((unwrapped as { data?: unknown }).data)
      ? ((unwrapped as { data?: unknown }).data as unknown[])
      : [];

  const getRecord = (value: unknown): Record<string, unknown> | null =>
    value && typeof value === "object" ? (value as Record<string, unknown>) : null;

  const pickValue = (record: Record<string, unknown>, keys: string[]): unknown => {
    for (const key of keys) {
      if (key in record) return record[key];
    }
    return undefined;
  };

  const toNumber = (value: unknown): number | null => {
    if (typeof value === "number" && Number.isFinite(value)) return value;
    if (typeof value === "string" && value.trim()) {
      const parsed = Number(value);
      return Number.isFinite(parsed) ? parsed : null;
    }
    return null;
  };

  const toString = (value: unknown): string =>
    typeof value === "string" ? value : value != null ? String(value) : "";

  const toOptionalString = (value: unknown): string | null => {
    if (value == null) return null;
    const str = toString(value);
    return str.length ? str : null;
  };

  const mapped: SupplierSummary[] = [];
  list.forEach((item) => {
    const record = getRecord(item);
    if (!record) return;
    const idValue = pickValue(record, ["id", "Id", "supplierId", "SupplierId", "supplier_id"]);
    const id = toNumber(idValue);
    if (id === null) return;
    const supplierCode = toOptionalString(
      pickValue(record, ["supplierCode", "SupplierCode", "supplier_code"]),
    );
    const entry: SupplierSummary = {
      id,
      companyName: toString(pickValue(record, ["companyName", "CompanyName", "company_name"])),
      companyId: toString(pickValue(record, ["companyId", "CompanyId", "company_id"])),
    };
    if (supplierCode !== null) {
      entry.supplierCode = supplierCode;
    }
    mapped.push(entry);
  });

  return mapped;
};

// User lifecycle management
export const freezeUser = async (id: string, reason?: string) => {
  const response = await apiFetch<{ message: string }>(
    `/permissions/users/${encodeURIComponent(id)}/freeze`,
    {
      method: "POST",
      body: { reason },
    },
  );
  return response;
};

export const unfreezeUser = async (id: string) => {
  const response = await apiFetch<{ message: string }>(
    `/permissions/users/${encodeURIComponent(id)}/unfreeze`,
    {
      method: "POST",
    },
  );
  return response;
};

export const softDeleteUser = async (id: string) => {
  const response = await apiFetch<{ message: string }>(
    `/permissions/users/${encodeURIComponent(id)}`,
    {
      method: "DELETE",
    },
  );
  return response;
};
