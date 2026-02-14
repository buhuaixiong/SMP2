import { apiFetch } from "./http";
import type {
  Contract,
  ContractReminderItem,
  ContractReminderSettings,
  ContractReminderSummary,
  ContractVersion,
} from "@/types";

export interface ContractFilters {
  supplierId?: number | string;
  status?: string;
  expiresBefore?: string;
  q?: string;
}

export interface ContractPayload {
  supplierId: number;
  title: string;
  agreementNumber: string;
  amount?: number | null;
  currency?: string | null;
  status?: string | null;
  paymentCycle?: string | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  autoRenew?: boolean;
  isMandatory?: boolean;
  notes?: string | null;
  createdBy?: string | null;
  actorId?: string;
  actorName?: string;
}

export const listContracts = (filters: ContractFilters = {}) => {
  const params = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      params.append(key, String(value));
    }
  });
  const query = params.toString();
  return apiFetch<Contract[]>(`/contracts${query ? `?${query}` : ""}`);
};

export const getContract = (id: number) => apiFetch<Contract>(`/contracts/${id}`);

export const createContract = (payload: ContractPayload) =>
  apiFetch<Contract>("/contracts", {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const updateContract = (id: number, payload: Partial<ContractPayload>) =>
  apiFetch<Contract>(`/contracts/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

export const deleteContract = (id: number) =>
  apiFetch<void>(`/contracts/${id}`, {
    method: "DELETE",
    parseData: false,
  });

export const listContractVersions = (id: number) =>
  apiFetch<ContractVersion[]>(`/contracts/${id}/versions`);

export const uploadContractVersion = (
  contractId: number,
  payload: {
    file: File;
    changeLog?: string;
    createdBy?: string;
    actorId?: string;
    actorName?: string;
  },
) => {
  const form = new FormData();
  form.append("file", payload.file);
  if (payload.changeLog) form.append("changeLog", payload.changeLog);
  if (payload.createdBy) form.append("createdBy", payload.createdBy);
  if (payload.actorId) form.append("actorId", payload.actorId);
  if (payload.actorName) form.append("actorName", payload.actorName);

  return apiFetch<ContractVersion[]>(`/contracts/${contractId}/versions`, {
    method: "POST",
    body: form,
    parseData: true,
  });
};

export const deleteContractVersion = (contractId: number, versionId: number) =>
  apiFetch<void>(`/contracts/${contractId}/versions/${versionId}`, {
    method: "DELETE",
    parseData: false,
  });

export const getContractReminderSummary = () =>
  apiFetch<ContractReminderSummary>("/contracts/reminders/summary");

export const listContractReminders = (params: { bucket?: string; limit?: number } = {}) => {
  const search = new URLSearchParams();
  if (params.bucket) {
    search.append("bucket", params.bucket);
  }
  if (typeof params.limit === "number") {
    search.append("limit", String(params.limit));
  }
  const query = search.toString();
  return apiFetch<{
    settings: ContractReminderSettings;
    total: number;
    items: ContractReminderItem[];
  }>(`/contracts/reminders${query ? `?${query}` : ""}`);
};

export const getContractReminderSettings = () =>
  apiFetch<{ settings: ContractReminderSettings; defaults: ContractReminderSettings }>(
    "/contracts/reminders/settings",
  );

export const updateContractReminderSettings = (payload: Partial<ContractReminderSettings>) =>
  apiFetch<ContractReminderSettings>("/contracts/reminders/settings", {
    method: "PUT",
    body: JSON.stringify(payload),
  });
