import { ref } from "vue";
import { useService } from "@/core/hooks";
import type { AuditService, HttpService } from "@/services";
import { useNotification } from "./useNotification";

export interface ApprovalTimelineEntry {
  id: number | string;
  status: string;
  [key: string]: unknown;
}

export interface ApprovalActionPayload {
  comments?: string;
  reason?: string;
  [key: string]: unknown;
}

export interface UseApprovalWorkflowOptions {
  approveApi?: (entityId: number, approvalId: number, payload?: ApprovalActionPayload) => Promise<unknown>;
  rejectApi?: (entityId: number, approvalId: number, payload?: ApprovalActionPayload) => Promise<unknown>;
  requestChangesApi?: (entityId: number, approvalId: number, payload: ApprovalActionPayload) => Promise<unknown>;
  commentApi?: (entityId: number, approvalId: number, payload: { content: string }) => Promise<unknown>;
  inviteApi?: (
    entityId: number,
    approvalId: number,
    payload: { purchaserIds: (string | number)[]; message?: string },
  ) => Promise<unknown>;
  historyApi?: (entityId: number) => Promise<ApprovalTimelineEntry[]>;
  messages?: Partial<Record<string, string>>;
}

const defaultMessages = {
  approveSuccess: "审批通过",
  approveError: "审批失败",
  rejectSuccess: "已驳回",
  rejectError: "驳回失败",
  requestChangesSuccess: "已提交变更请求",
  requestChangesError: "提交变更请求失败",
  commentSuccess: "评论已添加",
  commentError: "添加评论失败",
  inviteSuccess: "已邀请采购员",
  inviteError: "邀请失败",
  emptyInvites: "请选择至少一名采购员",
};

export function useApprovalWorkflow(entityType: string, options: UseApprovalWorkflowOptions = {}) {
  const http = useService<HttpService>("http");
  const audit = useService<AuditService>("audit");
  const notification = useNotification();

  const loading = ref(false);
  const history = ref<ApprovalTimelineEntry[]>([]);
  const lastError = ref<string | null>(null);

  const messageOf = (key: keyof typeof defaultMessages) =>
    options.messages?.[key] ?? defaultMessages[key];

  const buildPath = (entityId: number, approvalId: number, action: string) =>
    `/api/${entityType}/${entityId}/approvals/${approvalId}/${action}`;

  const runWithFeedback = async <T>(action: () => Promise<T>, successMessage?: string, errorMessage?: string) => {
    loading.value = true;
    lastError.value = null;
    try {
      const result = await action();
      if (successMessage) {
        notification.success(successMessage);
      }
      return result;
    } catch (error: any) {
      const message = error?.message || errorMessage;
      lastError.value = message ?? null;
      notification.error(message ?? "操作失败");
      throw error;
    } finally {
      loading.value = false;
    }
  };

  const approve = async (entityId: number, approvalId: number, payload?: ApprovalActionPayload) => {
    await runWithFeedback(
      () =>
        options.approveApi
          ? options.approveApi(entityId, approvalId, payload)
          : http.post(buildPath(entityId, approvalId, "approve"), payload ?? {}, { silent: true }),
      messageOf("approveSuccess"),
      messageOf("approveError"),
    );
    audit.logUpdate(entityType, {
      entityId,
      action: "approve",
      approvalId,
      payload,
    });
  };

  const reject = async (entityId: number, approvalId: number, payload: ApprovalActionPayload) => {
    await runWithFeedback(
      () =>
        options.rejectApi
          ? options.rejectApi(entityId, approvalId, payload)
          : http.post(buildPath(entityId, approvalId, "reject"), payload, { silent: true }),
      messageOf("rejectSuccess"),
      messageOf("rejectError"),
    );
    audit.logUpdate(entityType, {
      entityId,
      action: "reject",
      approvalId,
      payload,
    });
  };

  const requestChanges = async (entityId: number, approvalId: number, payload: ApprovalActionPayload) => {
    await runWithFeedback(
      () =>
        options.requestChangesApi
          ? options.requestChangesApi(entityId, approvalId, payload)
          : http.post(buildPath(entityId, approvalId, "request-changes"), payload, { silent: true }),
      messageOf("requestChangesSuccess"),
      messageOf("requestChangesError"),
    );
    audit.logUpdate(entityType, {
      entityId,
      action: "update",
      approvalId,
      payload,
    });
  };

  const addComment = async (entityId: number, approvalId: number, content: string) => {
    await runWithFeedback(
      () =>
        options.commentApi
          ? options.commentApi(entityId, approvalId, { content })
          : http.post(buildPath(entityId, approvalId, "comments"), { content }, { silent: true }),
      messageOf("commentSuccess"),
      messageOf("commentError"),
    );
  };

  const invitePurchasers = async (
    entityId: number,
    approvalId: number,
    payload: { purchaserIds: (string | number)[]; message?: string },
  ) => {
    if (!payload.purchaserIds?.length) {
      notification.warning(messageOf("emptyInvites"));
      return;
    }
    await runWithFeedback(
      () =>
        options.inviteApi
          ? options.inviteApi(entityId, approvalId, payload)
          : http.post(buildPath(entityId, approvalId, "invite-purchasers"), payload, { silent: true }),
      messageOf("inviteSuccess"),
      messageOf("inviteError"),
    );
  };

  const fetchHistory = async (entityId: number) => {
    loading.value = true;
    try {
      const data = options.historyApi
        ? await options.historyApi(entityId)
        : await http.get(`/api/${entityType}/${entityId}/approvals`);
      history.value = Array.isArray(data) ? data : [];
      return history.value;
    } catch (error: any) {
      lastError.value = error?.message ?? null;
      notification.error(error?.message || "加载审批历史失败");
      throw error;
    } finally {
      loading.value = false;
    }
  };

  return {
    loading,
    history,
    lastError,
    approve,
    reject,
    requestChanges,
    addComment,
    invitePurchasers,
    fetchHistory,
  };
}
