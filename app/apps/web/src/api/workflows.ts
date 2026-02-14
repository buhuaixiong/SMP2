import { apiFetch } from "./http";
import type { WorkflowInstance } from "@/types";

export interface WorkflowFilters {
  type?: string;
  entityType?: string;
  status?: string;
}

export interface WorkflowPayload {
  workflowType: string;
  entityType: string;
  entityId: string;
  status?: string;
  currentStep?: string;
  createdBy?: string;
  actorId?: string;
  actorName?: string;
  steps?: Array<{
    stepOrder?: number;
    name?: string;
    assignee?: string;
    status?: string;
    dueAt?: string;
    notes?: string;
  }>;
}

export const listWorkflows = (filters: WorkflowFilters = {}) => {
  const params = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      params.append(key, String(value));
    }
  });
  const query = params.toString();
  return apiFetch<WorkflowInstance[]>(`/workflows${query ? `?${query}` : ""}`);
};

export const getWorkflow = (id: number) => apiFetch<WorkflowInstance>(`/workflows/${id}`);

export const createWorkflow = (payload: WorkflowPayload) =>
  apiFetch<WorkflowInstance>("/workflows", {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const updateWorkflow = (
  id: number,
  payload: Pick<WorkflowPayload, "status" | "currentStep" | "actorId" | "actorName">,
) =>
  apiFetch<WorkflowInstance>(`/workflows/${id}`, {
    method: "PATCH",
    body: JSON.stringify(payload),
  });

export const updateWorkflowStep = (
  workflowId: number,
  stepId: number,
  payload: {
    status?: string;
    assignee?: string | null;
    notes?: string | null;
    completedAt?: string | null;
    dueAt?: string | null;
    actorId?: string;
    actorName?: string;
  },
) =>
  apiFetch<WorkflowInstance>(`/workflows/${workflowId}/steps/${stepId}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });
