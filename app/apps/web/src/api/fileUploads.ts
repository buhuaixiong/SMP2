import { apiFetch } from "./http";
import type { Supplier } from "@/types";

export interface WorkflowStep {
  step: string;
  label: string;
  permission: string;
  roles: string[];
}

export interface FileUpload {
  id: number;
  supplierId: number;
  fileId: number;
  fileName: string | null;
  fileDescription: string | null;
  status: string;
  currentStep: string;
  submittedBy: string;
  submittedAt: string;
  riskLevel: string;
  validFrom: string | null;
  validTo: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface FileApprovalRecord {
  id: number;
  uploadId: number;
  step: string;
  stepName: string | null;
  approverId: string | null;
  approverName: string | null;
  decision: string;
  comments: string | null;
  createdAt: string;
}

export interface FileUploadDetails extends FileUpload {
  approvalHistory: FileApprovalRecord[];
  supplier: Supplier;
  workflow: WorkflowStep[];
}

export interface UploadFilePayload {
  supplierId: number;
  file: File;
  fileDescription?: string;
  validFrom: string;
  validTo: string;
}

export interface ApproveFilePayload {
  decision: "approved" | "rejected";
  comments?: string;
}

/**
 * Upload file and create approval workflow
 */
export async function uploadFileForApproval(payload: UploadFilePayload) {
  const formData = new FormData();
  formData.append("file", payload.file);
  formData.append("supplierId", String(payload.supplierId));
  if (payload.fileDescription) {
    formData.append("fileDescription", payload.fileDescription);
  }
  formData.append("validFrom", payload.validFrom);
  formData.append("validTo", payload.validTo);

  const res = await apiFetch<{
    message?: string;
    data: {
      uploadId: number;
      status: string;
      currentStep: string;
      message: string;
      workflow: WorkflowStep[];
    };
  }>("/file-uploads", {
    method: "POST",
    body: formData,
    parseData: true,
  });

  return res.data;
}

/**
 * Get current user's pending file approvals
 */
export async function getMyPendingFileApprovals(params?: {
  limit?: number;
  offset?: number;
}): Promise<FileUpload[]> {
  const res = await apiFetch<{ data: FileUpload[] }>("/file-uploads/pending", { params });
  return res.data;
}

/**
 * Get current user's approved file approvals
 */
export async function getMyApprovedFileApprovals(params?: {
  limit?: number;
  offset?: number;
}): Promise<FileUpload[]> {
  const res = await apiFetch<{ data: FileUpload[] }>("/file-uploads/approved", { params });
  return res.data;
}

/**
 * Get file upload details
 */
export async function getFileUploadDetails(uploadId: number): Promise<FileUploadDetails> {
  const res = await apiFetch<{ data: FileUploadDetails }>(`/file-uploads/${uploadId}`);
  return res.data;
}

/**
 * Approve or reject file upload
 */
export async function approveFileUpload(
  uploadId: number,
  payload: ApproveFilePayload,
): Promise<{ status: string; message: string }> {
  const res = await apiFetch<{
    message: string;
    data: { status: string; currentStep?: string };
  }>(`/file-uploads/${uploadId}/approve`, {
    method: "PUT",
    body: payload,
  });
  return {
    status: res.data.status,
    message: res.message,
  };
}

/**
 * Get supplier's file upload history
 */
export async function getSupplierFileUploads(
  supplierId: number,
  params?: {
    limit?: number;
    offset?: number;
  },
): Promise<FileUpload[]> {
  const res = await apiFetch<{ data: FileUpload[] }>(`/file-uploads/supplier/${supplierId}`, {
    params,
  });
  return res.data;
}

/**
 * Get expiring and expired files (purchaser only)
 */
export interface ExpiringFilesResponse {
  expiring: (FileUpload & {
    supplier: { id: number; companyName: string; contactEmail: string } | null;
  })[];
  expired: (FileUpload & {
    supplier: { id: number; companyName: string; contactEmail: string } | null;
  })[];
}

export async function getExpiringFiles(params?: {
  daysThreshold?: number;
}): Promise<ExpiringFilesResponse> {
  const res = await apiFetch<{ data: ExpiringFilesResponse }>("/file-uploads/expiring", { params });
  return res.data;
}

/**
 * Send expiry reminder email for a single file (purchaser only)
 */
export async function sendFileReminder(uploadId: number): Promise<{ message: string }> {
  const res = await apiFetch<{ message: string }>(`/file-uploads/${uploadId}/send-reminder`, {
    method: "POST",
  });
  return { message: res.message };
}

/**
 * Send batch reminder emails for expiring files (purchaser only)
 */
export interface BatchReminderResponse {
  message: string;
  sent: number;
  failed: number;
  errors: Array<{ uploadId: number; reason: string }>;
}

export async function sendBatchFileReminders(params?: {
  daysThreshold?: number;
}): Promise<BatchReminderResponse> {
  const res = await apiFetch<{ message: string; data: BatchReminderResponse }>(
    "/file-uploads/batch-reminder",
    {
      method: "POST",
      body: params,
    },
  );
  return res.data;
}
