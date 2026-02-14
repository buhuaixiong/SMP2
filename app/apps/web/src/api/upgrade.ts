import { apiFetch } from "./http";
import type {
  UpgradeApplicationDocument,
  UpgradeApplicationInfo,
  UpgradeRequirement,
  UpgradeStatus,
  UpgradeWorkflowSummary,
} from "@/types";

type UpgradeRequirementApiResponse =
  | UpgradeRequirement[]
  | {
      requiredDocuments?: UpgradeRequirement[];
      workflow?: unknown;
    };

export interface UploadUpgradeFilePayload {
  supplierId: number;
  requirementCode: string;
  file: File;
  status?: string;
  validFrom?: string | null;
  validTo?: string | null;
}

export interface UploadedUpgradeDocument {
  id: number;
  applicationId: number;
  requirementCode: string;
  requirementName: string;
  fileId: number;
  uploadedAt: string;
  uploadedBy: string | null;
  status: string | null;
  notes: string | null;
}

export const fetchUpgradeRequirements = async (): Promise<UpgradeRequirement[]> => {
  const response = await apiFetch<{ data: UpgradeRequirementApiResponse }>(
    "/temp-suppliers/upgrade-requirements",
  );
  const data = response.data;
  if (Array.isArray(data)) {
    return data;
  }
  if (data && typeof data === "object") {
    return data.requiredDocuments ?? [];
  }
  return [];
};

export const fetchUpgradeStatus = async (supplierId: number) => {
  const response = await apiFetch<{ data: UpgradeStatus }>(
    `/temp-suppliers/${supplierId}/upgrade-status`,
  );
  return response.data;
};

export interface SubmitUpgradeApplicationPayload {
  documents?: Array<{
    requirementCode: string;
    fileId: number;
    notes?: string | null;
  }>;
}

export type SubmitUpgradeApplicationResult =
  | { applicationId: number }
  | { application: UpgradeApplicationInfo; workflow?: UpgradeWorkflowSummary | null };

export const submitUpgradeApplication = async (
  supplierId: number,
  payload: SubmitUpgradeApplicationPayload = {},
) => {
  const response = await apiFetch<{ data: SubmitUpgradeApplicationResult }>(
    `/temp-suppliers/${supplierId}/upgrade-application`,
    {
      method: "POST",
      body: payload,
    },
  );
  return response.data;
};

export const uploadUpgradeFile = async (payload: UploadUpgradeFilePayload) => {
  const form = new FormData();
  form.append("file", payload.file);
  form.append("requirementCode", payload.requirementCode);
  if (payload.validFrom) {
    form.append("validFrom", payload.validFrom);
  }
  if (payload.validTo) {
    form.append("validTo", payload.validTo);
  }
  if (payload.status) {
    form.append("status", payload.status);
  }

  const response = await apiFetch<{ data: UpgradeApplicationDocument | UploadedUpgradeDocument }>(
    `/temp-suppliers/${payload.supplierId}/upgrade-application-documents`,
    {
      method: "POST",
      body: form,
      parseData: true,
    },
  );
  return response.data;
};

export const deleteUpgradeDocument = async (supplierId: number, documentId: number) => {
  const response = await apiFetch<{ data?: { id: number } }>(
    `/temp-suppliers/${supplierId}/upgrade-application-documents/${documentId}`,
    {
      method: "DELETE",
    },
  );
  return response.data;
};

export interface UpgradeDecisionPayload {
  decision: "approved" | "rejected";
  comments?: string | null;
}

export const submitUpgradeDecision = (
  applicationId: number,
  stepKey: string,
  payload: UpgradeDecisionPayload,
) =>
  apiFetch(`/temp-suppliers/upgrade-applications/${applicationId}/steps/${stepKey}/decision`, {
    method: "POST",
    body: payload,
  });

export interface PendingUpgradeApplication {
  id: number;
  supplierId: number;
  supplierName: string;
  status: string;
  currentStep: string;
  submittedAt: string;
  submittedBy: string;
  dueAt: string;
  documentCompleteness: number;
  documents: Array<{
    id: number;
    requirementCode: string;
    requirementName: string;
    fileId: number | null;
    file: UpgradeApplicationDocument["file"];
  }>;
}

export const fetchPendingUpgradeApplications = async () => {
  const response = await apiFetch<{ data: PendingUpgradeApplication[] }>(
    "/temp-suppliers/upgrade-applications/pending",
  );
  return response.data;
};

export const fetchApprovedUpgradeApplications = async () => {
  const response = await apiFetch<{ data: PendingUpgradeApplication[] }>(
    "/temp-suppliers/upgrade-applications/approved",
  );
  return response.data;
};
