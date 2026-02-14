﻿﻿<template>
  <el-card class="approval-workflow-card" shadow="never" v-if="approvals.length > 0">
    <template #header>
      <div class="card-header">
        <span class="card-title">{{ t("rfq.approval.workflowTitle") }}</span>
        <el-tag :type="getApprovalStatusType(overallStatus)" size="large">
          {{ t(`rfq.approval.status.${overallStatus}`) }}
        </el-tag>
      </div>
    </template>

    <el-timeline>
      <el-timeline-item
        v-for="approval in approvals"
        :key="approval.id"
        :type="getTimelineType(approval.status)"
        :icon="getTimelineIcon(approval.status)"
        :size="approval.status === 'pending' ? 'large' : 'normal'"
      >
        <div class="approval-step">
          <div class="step-header">
            <h4>{{ approval.step_name || approval.stepName }}</h4>
            <el-tag :type="getStepStatusType(approval.status)" size="small">
              {{ t(`rfq.approval.stepStatus.${approval.status}`) }}
            </el-tag>
          </div>

          <div class="step-details">
            <div class="step-info">
              <span class="label">{{ t("rfq.approval.approverRole") }}:</span>
              <span class="value">{{ t(`common.roles.${approval.approver_role || approval.approverRole}`) }}</span>
            </div>

            <div v-if="approval.approver_name || approval.approverName" class="step-info">
              <span class="label">{{ t("rfq.approval.approver") }}:</span>
              <span class="value">{{ approval.approver_name || approval.approverName }}</span>
            </div>

            <div v-if="approval.decided_at || approval.decidedAt" class="step-info">
              <span class="label">{{ t("rfq.approval.decidedAt") }}:</span>
              <span class="value">{{ formatDateTime(approval.decided_at || approval.decidedAt) }}</span>
            </div>

            <div v-if="approval.comments" class="step-info">
              <span class="label">{{ t("rfq.approval.comments") }}:</span>
              <span class="value">{{ approval.comments }}</span>
            </div>

            <div v-if="approval.rejection_reason || approval.rejectionReason" class="step-info">
              <span class="label">{{ t("rfq.approval.rejectionReason") }}:</span>
              <span class="value text-danger">{{ approval.rejection_reason || approval.rejectionReason }}</span>
            </div>
          </div>

          <!-- Comments Section -->
          <div v-if="Array.isArray(approval.comments) && approval.comments.length > 0" class="comments-section">
            <el-divider content-position="left">
              <span style="font-size: 13px; color: #606266;">{{ t("rfq.approval.comments") }}</span>
            </el-divider>
            <div v-for="comment in (approval.comments as Comment[])" :key="comment.id" class="comment-item">
              <div class="comment-header">
                <span class="comment-author">{{ comment.author_name || comment.authorName }}</span>
                <span class="comment-time">{{ formatDateTime(comment.created_at || comment.createdAt) }}</span>
              </div>
              <div class="comment-content">{{ comment.content }}</div>
            </div>
          </div>

          <!-- Add Comment Section -->
          <div class="add-comment-section">
            <el-input
              v-model="commentInputs[approval.id]"
              type="textarea"
              :rows="2"
              :placeholder="t('rfq.approval.addCommentPlaceholder')"
              style="margin-bottom: 8px;"
            />
            <div style="display: flex; gap: 8px; align-items: center;">
              <el-button
                type="primary"
                size="small"
                :disabled="!commentInputs[approval.id] || !commentInputs[approval.id].trim()"
                @click="handleAddComment(approval)"
              >
                {{ t("rfq.approval.addComment") }}
              </el-button>
              <el-button
                v-if="canInvitePurchasers(approval)"
                type="info"
                size="small"
                @click="handleInvitePurchaser(approval)"
              >
                {{ t("rfq.approval.invitePurchaser") }}
              </el-button>
            </div>
          </div>

          <!-- Action buttons for pending approval -->
          <div
            v-if="approval.status === 'pending' && (approval.approver_role || approval.approverRole) && canApprove((approval.approver_role || approval.approverRole)!)"
            class="step-actions"
          >
            <el-button type="success" size="small" @click="handleApprove(approval)">
              {{ t("rfq.approval.approve") }}
            </el-button>
            <el-button type="danger" size="small" @click="handleReject(approval)">
              {{ t("rfq.approval.reject") }}
            </el-button>
          </div>
        </div>
      </el-timeline-item>
    </el-timeline>

    <!-- Reject Dialog -->
    <el-dialog
      v-model="showRejectDialog"
      :title="t('rfq.approval.rejectTitle')"
      width="500px"
    >
      <el-form :model="rejectForm" label-width="120px">
        <el-form-item :label="t('rfq.approval.rejectionReason')" required>
          <el-input
            v-model="rejectForm.reason"
            type="textarea"
            :rows="4"
            :placeholder="t('rfq.approval.rejectionReasonPlaceholder')"
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showRejectDialog = false">{{ t("common.cancel") }}</el-button>
        <el-button type="danger" :loading="submitting" @click="confirmReject">
          {{ t("rfq.approval.confirmReject") }}
        </el-button>
      </template>
    </el-dialog>

    <!-- Approve Dialog -->
    <el-dialog
      v-model="showApproveDialog"
      :title="t('rfq.approval.approveTitle')"
      width="500px"
    >
      <el-form :model="approveForm" label-width="120px">
        <el-form-item :label="t('rfq.approval.comments')">
          <el-input
            v-model="approveForm.comments"
            type="textarea"
            :rows="4"
            :placeholder="t('rfq.approval.commentsPlaceholder')"
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showApproveDialog = false">{{ t("common.cancel") }}</el-button>
        <el-button type="success" :loading="submitting" @click="confirmApprove">
          {{ t("rfq.approval.confirmApprove") }}
        </el-button>
      </template>
    </el-dialog>

    <!-- Invite Purchaser Dialog -->
    <el-dialog
      v-model="showInvitePurchaserDialog"
      :title="t('rfq.approval.invitePurchaser')"
      width="600px"
    >
      <el-form :model="inviteForm" label-width="120px">
        <el-form-item :label="t('rfq.approval.selectPurchasers')">
          <el-select
            v-model="inviteForm.purchaserIds"
            multiple
            :placeholder="t('rfq.approval.selectPurchasersPlaceholder')"
            style="width: 100%;"
            filterable
          >
            <el-option
              v-for="purchaser in availablePurchasers"
              :key="purchaser.id"
              :label="`${purchaser.name} (${purchaser.role || 'purchaser'})`"
              :value="purchaser.id"
            />
          </el-select>
          <div v-if="availablePurchasers.length === 0" style="margin-top: 8px; color: #909399; font-size: 12px;">
            {{ t("common.none") }} - 娌℃湁鍙敤鐨勯噰璐憳
          </div>
        </el-form-item>
        <el-form-item :label="t('rfq.approval.comments')">
          <el-input
            v-model="inviteForm.message"
            type="textarea"
            :rows="3"
            :placeholder="t('rfq.approval.addCommentPlaceholder')"
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showInvitePurchaserDialog = false">{{ t("common.cancel") }}</el-button>
        <el-button
          type="primary"
          :loading="submitting"
          :disabled="!inviteForm.purchaserIds || inviteForm.purchaserIds.length === 0"
          @click="confirmInvitePurchaser"
        >
          {{ t("common.confirm") }}
        </el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { ref, computed, reactive, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { Check, Close, Clock } from "@element-plus/icons-vue";
import {
  approveRfqWorkflow,
  rejectRfqWorkflow,
  addRfqApprovalComment,
  invitePurchaserToComment,
  type RfqApprovalComment,
  type RfqApprovalStep,
} from "@/api/rfq";
import { useAuthStore } from "@/stores/auth";
import { apiFetch } from "@/api/http";
import { useNotification, useApprovalWorkflow } from "@/composables";

const { t } = useI18n();
const authStore = useAuthStore();
const notification = useNotification();
const approvalWorkflow = useApprovalWorkflow("rfq", {
  approveApi: (rfqId, approvalId, payload) => approveRfqWorkflow(rfqId, approvalId, payload ?? {}),
  rejectApi: (rfqId, approvalId, payload) => rejectRfqWorkflow(rfqId, approvalId, (payload ?? { reason: '' }) as { reason: string }),
  commentApi: addRfqApprovalComment,
  inviteApi: invitePurchaserToComment,
});

type Comment = RfqApprovalComment;

type Approval = RfqApprovalStep;

const props = defineProps<{
  rfqId: number;
  approvals: Approval[];
}>();

const emit = defineEmits<{
  refresh: [];
}>();

const showApproveDialog = ref(false);
const showRejectDialog = ref(false);
const showInvitePurchaserDialog = ref(false);
const submitting = ref(false);
const currentApproval = ref<Approval | null>(null);

// Track comment inputs for each approval step
const commentInputs = reactive<Record<number, string>>({});

// Available purchasers for invitation
const availablePurchasers = ref<Array<{ id: string | number; name: string; role?: string }>>([]);

const approveForm = ref({
  comments: "",
});

const rejectForm = ref({
  reason: "",
});

const inviteForm = ref({
  purchaserIds: [] as (string | number)[],
  message: "",
});

const overallStatus = computed(() => {
  if (props.approvals.length === 0) return "none";
  if (props.approvals.some((a) => a.status === "rejected")) return "rejected";
  if (props.approvals.every((a) => a.status === "approved")) return "approved";
  return "pending";
});

function canApprove(role: string): boolean {
  const userRole = authStore.user?.role;
  if (!userRole) return false;
  return userRole === role;
}

function canInvitePurchasers(approval: Approval): boolean {
  const userRole = authStore.user?.role;
  if (!userRole) return false;
  // Only procurement directors can invite purchasers
  return userRole === "procurement_director";
}

function getApprovalStatusType(status: string): string {
  const typeMap: Record<string, string> = {
    pending: "warning",
    approved: "success",
    rejected: "danger",
    none: "info",
  };
  return typeMap[status] || "info";
}

function getStepStatusType(status: string): string {
  const typeMap: Record<string, string> = {
    pending: "warning",
    approved: "success",
    rejected: "danger",
  };
  return typeMap[status] || "info";
}

function getTimelineType(status: string): string {
  const typeMap: Record<string, string> = {
    approved: "success",
    rejected: "danger",
    pending: "warning",
  };
  return typeMap[status] || "info";
}

function getTimelineIcon(status: string) {
  const iconMap: Record<string, any> = {
    approved: Check,
    rejected: Close,
    pending: Clock,
  };
  return iconMap[status] || Clock;
}

function handleApprove(approval: Approval) {
  currentApproval.value = approval;
  approveForm.value.comments = "";
  showApproveDialog.value = true;
}

function handleReject(approval: Approval) {
  currentApproval.value = approval;
  rejectForm.value.reason = "";
  showRejectDialog.value = true;
}

async function confirmApprove() {
  if (!currentApproval.value) return;

  submitting.value = true;
  try {
    await approvalWorkflow.approve(props.rfqId, currentApproval.value.id, {
      comments: approveForm.value.comments,
    });
    showApproveDialog.value = false;
    emit("refresh");
  } catch (error: any) {
    // handled within workflow
  } finally {
    submitting.value = false;
  }
}

async function confirmReject() {
  if (!currentApproval.value) return;

  if (!rejectForm.value.reason.trim()) {
    notification.warning(t("rfq.approval.rejectionReasonRequired"));
    return;
  }

  submitting.value = true;
  try {
    await approvalWorkflow.reject(props.rfqId, currentApproval.value.id, {
      reason: rejectForm.value.reason,
    });
    showRejectDialog.value = false;
    emit("refresh");
  } catch (error: any) {
    // handled within workflow
  } finally {
    submitting.value = false;
  }
}

async function handleAddComment(approval: Approval) {
  const content = commentInputs[approval.id];

  if (!content || !content.trim()) {
    notification.warning(t("rfq.approval.commentRequired"));
    return;
  }

  try {
    await approvalWorkflow.addComment(props.rfqId, approval.id, content.trim());

    // Clear the input
    commentInputs[approval.id] = "";

    // Refresh to get the new comment
    emit("refresh");
  } catch (error: any) {
    notification.error(error.message || t("rfq.approval.commentError"));
  }
}

function handleInvitePurchaser(approval: Approval) {
  currentApproval.value = approval;
  inviteForm.value.purchaserIds = [];
  inviteForm.value.message = "";
  showInvitePurchaserDialog.value = true;
}

async function confirmInvitePurchaser() {
  if (!currentApproval.value) return;

  if (!inviteForm.value.purchaserIds || inviteForm.value.purchaserIds.length === 0) {
    notification.warning(t("rfq.approval.selectPurchasersFirst"));
    return;
  }

  submitting.value = true;
  try {
    await approvalWorkflow.invitePurchasers(props.rfqId, currentApproval.value.id, {
      purchaserIds: inviteForm.value.purchaserIds,
      message: inviteForm.value.message,
    });
    showInvitePurchaserDialog.value = false;
    emit("refresh");
  } catch (error: any) {
    notification.error(error.message || t("rfq.approval.inviteError"));
  } finally {
    submitting.value = false;
  }
}

async function loadPurchasers() {
  try {
    const response = await apiFetch<{ data: Array<{ id: string | number; name: string; role: string }> }>("/users", {
      params: { role: "purchaser" },
    });
    console.log("=== Loaded purchasers ===");
    console.log("Count:", response.data?.length || 0);
    console.log("Data:", response.data);
    console.log("========================");

    if (!response.data || response.data.length === 0) {
      notification.warning("娌℃湁鎵惧埌鍙敤鐨勯噰璐憳");
    }

    availablePurchasers.value = response.data || [];
  } catch (error) {
    console.error("Failed to load purchasers:", error);
    notification.error("鍔犺浇閲囪喘鍛樺垪琛ㄥけ璐? " + (error as any).message);
  }
}

onMounted(() => {
  loadPurchasers();
});

function formatDateTime(dateStr: string | null | undefined): string {
  if (!dateStr) return "-";
  const date = new Date(dateStr);
  if (Number.isNaN(date.getTime())) return "-";
  return date.toLocaleString();
}
</script>

<style scoped>
.approval-workflow-card {
  margin-top: 20px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-title {
  font-size: 16px;
  font-weight: 600;
}

.approval-step {
  padding: 10px 0;
}

.step-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.step-header h4 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.step-details {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 12px;
}

.step-info {
  display: flex;
  gap: 8px;
  font-size: 14px;
}

.step-info .label {
  color: #606266;
  font-weight: 500;
  min-width: 100px;
}

.step-info .value {
  color: #303133;
}

.step-info .text-danger {
  color: #f56c6c;
}

.step-actions {
  margin-top: 12px;
  display: flex;
  gap: 12px;
}

.comments-section {
  margin-top: 16px;
}

.comment-item {
  background-color: #f5f7fa;
  border-radius: 4px;
  padding: 10px 12px;
  margin-bottom: 8px;
}

.comment-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}

.comment-author {
  font-weight: 600;
  font-size: 13px;
  color: #409eff;
}

.comment-time {
  font-size: 12px;
  color: #909399;
}

.comment-content {
  font-size: 14px;
  color: #303133;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-word;
}

.add-comment-section {
  margin-top: 12px;
  padding: 12px;
  background-color: #fafafa;
  border-radius: 4px;
  border: 1px dashed #dcdfe6;
}
</style>


