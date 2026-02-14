import { apiFetch } from "./http";

import type { Supplier } from "@/types";

export type ChangeRequestFlow = "required" | "optional";

export interface ChangeRequestFieldChange {
  key: string;
  label: string;
  newValue: unknown;
  category?: ChangeRequestFlow;
}

export interface ChangeRequest {
  id: number;
  supplierId: number;
  changeType: string;
  status: string;
  currentStep: string;
  payload: Record<string, unknown>;
  submittedBy: string;
  submittedAt: string;
  updatedAt: string;
  riskLevel?: string;
  requiresQuality?: number;
  flow?: ChangeRequestFlow;
  workflow?: WorkflowStep[];
  // Enriched fields from supplier data
  companyName?: string;
  companyId?: string;
}

export interface ChangeRequestDetails extends ChangeRequest {
  approvalHistory: ApprovalRecord[];
  supplier: Supplier;
  workflow: WorkflowStep[];
  changedFields: ChangeRequestFieldChange[];
}

export interface ApprovalRecord {
  id: number;
  requestId: number;
  step: string;
  approverId: string;
  approverName: string;
  decision: "approved" | "rejected";
  comments?: string;
  createdAt: string;
}

export interface WorkflowStep {
  step: string;
  label: string;
  permission: string;
  roles: string[];
}

export interface ChangeRequestResult {
  requestId: number;
  status: string;
  currentStep?: string;
  riskLevel?: string;
  flow: ChangeRequestFlow;
  type?: string;
  isChangeRequest?: boolean;
  message?: string;
  changedFields: ChangeRequestFieldChange[];
}

/**
 * 提交变更申请
 */
export async function submitChangeRequest(
  supplierId: number,
  changes: Record<string, unknown>,
): Promise<ChangeRequestResult> {
  const res = await apiFetch<{ message?: string; data: ChangeRequestResult }>("/change-requests", {
    method: "POST",
    body: { supplierId, changes },
  });
  return {
    ...res.data,
    message: res.data.message ?? res.message,
  };
}

/**
 * 获取我的待审批列表
 */
export async function getMyPendingApprovals(params?: {
  limit?: number;
  offset?: number;
}): Promise<ChangeRequest[]> {
  const res = await apiFetch<{ data: ChangeRequest[] }>("/change-requests/pending", { params });
  return res.data;
}

/**
 * 获取我已审批的列表
 */
export async function getMyApprovedApprovals(params?: {
  limit?: number;
  offset?: number;
}): Promise<ChangeRequest[]> {
  const res = await apiFetch<{ data: ChangeRequest[] }>("/change-requests/approved", { params });
  return res.data;
}

/**
 * 获取变更请求详情
 */
export async function getChangeRequestDetails(requestId: number): Promise<ChangeRequestDetails> {
  const res = await apiFetch<{ data: ChangeRequestDetails }>(`/change-requests/${requestId}`);
  return res.data;
}

/**
 * 审批变更请求
 */
export async function approveChangeRequest(
  requestId: number,
  decision: "approved" | "rejected",
  comments?: string,
): Promise<{ status: string; message: string }> {
  const res = await apiFetch<{ message: string; data: { status: string } }>(
    `/change-requests/${requestId}/approve`,
    {
      method: "PUT",
      body: { decision, comments },
    },
  );
  return { status: res.data.status, message: res.message };
}

/**
 * 获取供应商的变更请求列表
 */
export async function getSupplierChangeRequests(
  supplierId: number,
  params?: {
    status?: string;
    limit?: number;
    offset?: number;
  },
): Promise<ChangeRequest[]> {
  const res = await apiFetch<{ data: ChangeRequest[] }>(`/change-requests/supplier/${supplierId}`, {
    params,
  });
  return res.data;
}
