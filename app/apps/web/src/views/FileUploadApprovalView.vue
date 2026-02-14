<template>
  <div class="approval-page">
    <PageHeader
      title="文件上传审批"
      subtitle="审批供应商上传的文件"
    >
      <template #actions>
        <div class="header-actions">
          <el-button @click="refreshAll">
            <el-icon><Refresh /></el-icon>
            刷新
          </el-button>
        </div>
      </template>
    </PageHeader>

    <div class="approval-stack">
      <el-card v-for="section in approvalSections" :key="section.key">
        <template #header>
          <div class="section-header">
            <span>{{ section.title }} ({{ section.items.length }})</span>
            <el-tag v-if="section.items.length > 0 && section.canApprove" type="warning">
              {{ section.items.length }} 个待处理
            </el-tag>
          </div>
        </template>

        <el-empty
          v-if="!section.loading && section.items.length === 0"
          :description="section.emptyText"
        />

        <el-table v-else v-loading="section.loading" :data="section.items" stripe style="width: 100%">
          <el-table-column prop="id" label="ID" width="80" />
          <el-table-column prop="fileName" label="文件名" min-width="200">
            <template #default="{ row }">
              <div style="display: flex; align-items: center; gap: 8px">
                <el-icon><Document /></el-icon>
                <span>{{ row.fileName || "未命名文件" }}</span>
              </div>
            </template>
          </el-table-column>
          <el-table-column prop="fileDescription" label="文件说明" min-width="150">
            <template #default="{ row }">
              <span>{{ row.fileDescription || "—" }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="supplierId" label="供应商ID" width="110" />
          <el-table-column prop="currentStep" label="当前步骤" width="130">
            <template #default="{ row }">
              {{ getCurrentStepLabel(row.currentStep) }}
            </template>
          </el-table-column>
          <el-table-column prop="validTo" label="过期日期" width="120">
            <template #default="{ row }">
              <span v-if="row.validTo">{{ row.validTo }}</span>
              <span v-else style="color: #909399">—</span>
            </template>
          </el-table-column>
          <el-table-column prop="submittedAt" label="提交时间" width="160">
            <template #default="{ row }">
              {{ formatTime(row.submittedAt) }}
            </template>
          </el-table-column>
          <el-table-column label="操作" width="240" fixed="right">
            <template #default="{ row }">
              <el-button
                v-if="section.canApprove"
                type="primary"
                link
                @click="openApprovalDialog(row.id)"
              >
                审批
              </el-button>
              <el-button
                v-else
                type="primary"
                link
                @click="openApprovalDialog(row.id, true)"
              >
                详情
              </el-button>
              <el-button
                v-if="row.status === 'approved' && row.validTo"
                type="warning"
                link
                @click="handleSendReminder(row.id)"
              >
                发送提醒
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-card>
    </div>

    <!-- Approval Dialog -->
    <el-dialog v-model="showApprovalDialog" title="文件审批" width="900px" :destroy-on-close="true">
      <div v-if="selectedUploadDetails" v-loading="loadingDetails">
        <!-- File Information -->
        <el-alert title="文件信息" type="info" :closable="false" style="margin-bottom: 20px">
          <el-descriptions :column="2" border size="small">
            <el-descriptions-item label="文件ID">
              {{ selectedUploadDetails.id }}
            </el-descriptions-item>
            <el-descriptions-item label="文件名">
              {{ selectedUploadDetails.fileName || "未命名" }}
            </el-descriptions-item>
            <el-descriptions-item label="文件说明" :span="2">
              {{ selectedUploadDetails.fileDescription || "—" }}
            </el-descriptions-item>
            <el-descriptions-item label="供应商ID">
              {{ selectedUploadDetails.supplierId }}
            </el-descriptions-item>
            <el-descriptions-item label="供应商名称">
              {{ selectedUploadDetails.supplier?.companyName || "—" }}
            </el-descriptions-item>
            <el-descriptions-item label="提交时间">
              {{ formatTime(selectedUploadDetails.submittedAt) }}
            </el-descriptions-item>
            <el-descriptions-item label="当前状态">
              <el-tag :type="getStatusTagType(selectedUploadDetails.status)">
                {{ getStatusLabel(selectedUploadDetails.status) }}
              </el-tag>
            </el-descriptions-item>
          </el-descriptions>
        </el-alert>

        <!-- Download Link -->
        <div v-if="selectedUploadDetails.fileId" style="margin-bottom: 20px">
          <el-button type="primary" @click="downloadFile(selectedUploadDetails.fileId)">
            <el-icon><Download /></el-icon>
            下载文件
          </el-button>
        </div>

        <!-- Approval Timeline -->
        <el-divider content-position="left">审批进度</el-divider>
        <FileUploadTimeline
          :workflow="selectedUploadDetails.workflow"
          :current-step="selectedUploadDetails.currentStep"
          :status="selectedUploadDetails.status"
          :approval-history="selectedUploadDetails.approvalHistory"
        />

        <!-- Approval Decision Form -->
        <template v-if="!detailReadOnly">
          <el-divider content-position="left">审批决定</el-divider>
          <el-form :model="approvalForm" label-width="100px">
            <el-form-item label="审批决定" required>
              <el-radio-group v-model="approvalForm.decision">
                <el-radio value="approved">批准</el-radio>
                <el-radio value="rejected">拒绝</el-radio>
              </el-radio-group>
            </el-form-item>

            <el-form-item label="审批意见">
              <el-input
                v-model="approvalForm.comments"
                type="textarea"
                :rows="4"
                placeholder="请输入审批意见(可选)"
                maxlength="500"
                show-word-limit
              />
            </el-form-item>
          </el-form>
        </template>
      </div>

      <template #footer>
        <span class="dialog-footer">
          <el-button @click="showApprovalDialog = false">取消</el-button>
          <el-button
            v-if="!detailReadOnly"
            type="primary"
            :loading="submitting"
            :disabled="!approvalForm.decision"
            @click="handleSubmitApproval"
          >
            提交审批
          </el-button>
        </span>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, reactive, onMounted, defineAsyncComponent, computed } from "vue";

import { Refresh, Document, Download } from "@element-plus/icons-vue";
import dayjs from "dayjs";
import {
  getMyPendingFileApprovals,
  getMyApprovedFileApprovals,
  getFileUploadDetails,
  approveFileUpload,
  sendFileReminder,
} from "@/api/fileUploads";
import type { FileUpload, FileUploadDetails } from "@/api/fileUploads";
import PageHeader from "@/components/layout/PageHeader.vue";
import { resolveApiUrl } from "@/utils/apiBaseUrl";


import { useNotification } from "@/composables";
const notification = useNotification();

const FileUploadTimeline = defineAsyncComponent(
  () => import("@/components/FileUploadTimeline.vue"),
);

const loading = ref(false);
const approvedLoading = ref(false);
const loadingDetails = ref(false);
const submitting = ref(false);
const showApprovalDialog = ref(false);

const pendingApprovals = ref<FileUpload[]>([]);
const approvedApprovals = ref<FileUpload[]>([]);
const selectedUploadDetails = ref<FileUploadDetails | null>(null);
const detailReadOnly = ref(false);

const approvalForm = reactive({
  decision: "" as "approved" | "rejected" | "",
  comments: "",
});

const approvalSections = computed(() => [
  {
    key: "pending",
    title: "待审批文件",
    items: pendingApprovals.value,
    loading: loading.value,
    emptyText: "暂无待审批文件",
    canApprove: true,
  },
  {
    key: "approved",
    title: "已审批文件",
    items: approvedApprovals.value,
    loading: approvedLoading.value,
    emptyText: "暂无已审批文件",
    canApprove: false,
  },
]);

const getStatusLabel = (status: string): string => {
  const statusMap: Record<string, string> = {
    pending_purchaser: "待采购员审批",
    pending_quality_manager: "待品质审批",
    pending_procurement_manager: "待采购经理审批",
    pending_procurement_director: "待采购总监审批",
    pending_finance_director: "待财务总监审批",
    approved: "已批准",
    rejected: "已拒绝",
  };
  return statusMap[status] || status;
};

const getStatusTagType = (status: string): "success" | "warning" | "danger" | "info" => {
  if (status === "approved") return "success";
  if (status === "rejected") return "danger";
  if (status.startsWith("pending_")) return "warning";
  return "info";
};

const getCurrentStepLabel = (currentStep: string): string => {
  const stepLabels: Record<string, string> = {
    purchaser: "采购员审批",
    quality_manager: "品质审批",
    procurement_manager: "采购经理审批",
    procurement_director: "采购总监审批",
    finance_director: "财务总监审批",
  };
  return stepLabels[currentStep] || currentStep;
};

const formatTime = (dateStr: string): string => {
  return dayjs(dateStr).format("YYYY-MM-DD HH:mm");
};

const fetchPendingApprovals = async () => {
  loading.value = true;
  try {
    pendingApprovals.value = await getMyPendingFileApprovals();
  } catch (error: any) {
    console.error("Failed to fetch pending approvals:", error);
    notification.error(error?.message || "获取待审批列表失败");
  } finally {
    loading.value = false;
  }
};

const fetchApprovedApprovals = async () => {
  approvedLoading.value = true;
  try {
    approvedApprovals.value = await getMyApprovedFileApprovals();
  } catch (error: any) {
    console.error("Failed to fetch approved approvals:", error);
    notification.error(error?.message || "获取已审批列表失败");
  } finally {
    approvedLoading.value = false;
  }
};

const refreshAll = async () => {
  await Promise.all([fetchPendingApprovals(), fetchApprovedApprovals()]);
};

const openApprovalDialog = async (uploadId: number, readOnly = false) => {
  loadingDetails.value = true;
  showApprovalDialog.value = true;
  detailReadOnly.value = readOnly;
  approvalForm.decision = "";
  approvalForm.comments = "";

  try {
    selectedUploadDetails.value = await getFileUploadDetails(uploadId);
  } catch (error: any) {
    console.error("Failed to fetch upload details:", error);
    notification.error(error?.message || "获取详情失败");
    showApprovalDialog.value = false;
  } finally {
    loadingDetails.value = false;
  }
};

const downloadFile = (fileId: number) => {
  const downloadUrl = resolveApiUrl(`/api/files/download/${fileId}`);
  window.open(downloadUrl, "_blank");
};

const handleSubmitApproval = async () => {
  if (!selectedUploadDetails.value) return;

  if (!approvalForm.decision) {
    notification.warning("请选择审批决定");
    return;
  }

  try {
    await notification.confirm(
      `确定${approvalForm.decision === "approved" ? "批准" : "拒绝"}此文件上传申请吗?`,
      "确认审批",
      {
        type: "warning",
        confirmButtonText: "确定",
        cancelButtonText: "取消",
      },
    );
  } catch {
    return;
  }

  submitting.value = true;
  try {
    const result = await approveFileUpload(selectedUploadDetails.value.id, {
      decision: approvalForm.decision,
      comments: approvalForm.comments || undefined,
    });

    notification.success(result.message || "审批提交成功");
    showApprovalDialog.value = false;
    refreshAll();
  } catch (error: any) {
    console.error("Failed to submit approval:", error);
    notification.error(error?.message || "审批提交失败");
  } finally {
    submitting.value = false;
  }
};

const handleSendReminder = async (uploadId: number) => {
  try {
    await notification.confirm("确定要向供应商发送文件过期提醒邮件吗?", "确认发送", {
      type: "warning",
      confirmButtonText: "确定",
      cancelButtonText: "取消",
    });
  } catch {
    return;
  }

  try {
    const result = await sendFileReminder(uploadId);
    notification.success(result.message || "提醒邮件已发送");
  } catch (error: any) {
    console.error("Failed to send reminder:", error);
    notification.error(error?.message || "发送提醒邮件失败");
  }
};

onMounted(() => {
  refreshAll();
});




</script>

<style scoped>
.approval-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px;
}

.approval-stack {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}




.header-actions {
  display: flex;
  gap: 12px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
</style>
