<template>
  <div class="approval-view">
    <PageHeader
      title="供应商资料变更审批"
      subtitle="审批供应商提交的资料修改申请"
    />

    <div class="content-wrapper">
      <div class="approval-stack">
        <el-card
          v-for="section in approvalSections"
          :key="section.key"
          class="approval-card"
          shadow="never"
        >
          <template #header>
            <div class="card-header">
              <span>{{ section.title }} ({{ section.items.length }})</span>
              <el-button type="primary" size="small" @click="section.refresh">
                <el-icon><Refresh /></el-icon>
                刷新
              </el-button>
            </div>
          </template>

          <el-table v-loading="section.loading" :data="section.items" stripe>
            <el-table-column prop="id" label="申请编号" width="100" />

            <el-table-column label="供应商信息" width="250">
              <template #default="{ row }">
                <div class="supplier-info">
                  <div class="supplier-name">{{ row.companyName }}</div>
                  <div class="supplier-code">代码: {{ row.companyId }}</div>
                </div>
              </template>
            </el-table-column>

            <el-table-column label="供应商阶段" width="100">
              <template #default="{ row }">
                <el-tag :type="row.stage === 'official' ? 'success' : 'info'" size="small">
                  {{ row.stage === "official" ? "正式" : "临时" }}
                </el-tag>
              </template>
            </el-table-column>

            <el-table-column label="风险等级" width="100">
              <template #default="{ row }">
                <el-tag
                  :type="
                    row.riskLevel === 'high'
                      ? 'danger'
                      : row.riskLevel === 'medium'
                        ? 'warning'
                        : 'info'
                  "
                  size="small"
                >
                  {{ getRiskLevelText(row.riskLevel) }}
                </el-tag>
              </template>
            </el-table-column>

            <el-table-column label="修改字段" width="120">
              <template #default="{ row }">
                <el-tag size="small">{{ getChangedFieldsCount(row.payload) }} 个字段</el-tag>
              </template>
            </el-table-column>

            <el-table-column prop="submittedAt" label="提交时间" width="180">
              <template #default="{ row }">
                {{ formatDateTime(row.submittedAt) }}
              </template>
            </el-table-column>

            <el-table-column label="操作" width="200" fixed="right">
              <template #default="{ row }">
                <el-button
                  v-if="section.canApprove"
                  type="primary"
                  size="small"
                  @click="handleApprove(row)"
                >
                  审批
                </el-button>
                <el-button type="info" size="small" link @click="viewDetails(row)"> 详情 </el-button>
              </template>
            </el-table-column>
          </el-table>

          <div v-if="section.items.length === 0 && !section.loading" class="empty-state">
            <el-empty :description="section.emptyText" />
          </div>
        </el-card>
      </div>
    </div>

    <!-- 审批对话框 -->
    <el-dialog
      v-model="approvalDialogVisible"
      title="审批变更申请"
      width="900px"
      destroy-on-close
      :before-close="handleCloseApprovalDialog"
    >
      <div v-if="selectedRequest" class="approval-content">
        <!-- 供应商信息 -->
        <el-alert type="info" :closable="false" style="margin-bottom: 20px">
          <template #title>
            <div style="font-size: 15px; font-weight: 600">
              {{ selectedRequest.companyName }} ({{ selectedRequest.companyId }})
            </div>
          </template>
          <div style="margin-top: 8px; font-size: 13px">
            提交时间: {{ formatDateTime(selectedRequest.submittedAt) }} | 风险等级:
            <el-tag :type="getRiskTagType(selectedRequest.riskLevel)" size="small">
              {{ getRiskLevelText(selectedRequest.riskLevel) }}
            </el-tag>
          </div>
        </el-alert>

        <!-- 变更内容对比 -->
        <el-divider content-position="left">变更内容</el-divider>
        <el-table :data="getChangedFieldsArray(selectedRequest.payload)" border>
          <el-table-column prop="label" label="字段名称" width="150" />
          <el-table-column label="申请修改为">
            <template #default="{ row }">
              <span style="color: #409eff; font-weight: 600">{{ row.newValue || "-" }}</span>
            </template>
          </el-table-column>
        </el-table>

        <!-- 审批历史 -->
        <el-divider content-position="left">审批进度</el-divider>
        <ChangeRequestTimeline
          v-if="requestDetails"
          :workflow="requestDetails.workflow"
          :approval-history="requestDetails.approvalHistory"
          :current-step="selectedRequest.currentStep"
          :status="selectedRequest.status"
        />

        <!-- 审批决定 -->
        <el-divider content-position="left">您的审批决定</el-divider>
        <el-form :model="approvalForm" label-width="100px">
          <el-form-item label="审批决定" required>
            <el-radio-group v-model="approvalForm.decision">
              <el-radio value="approved">
                <el-icon color="#67c23a"><CircleCheck /></el-icon>
                批准
              </el-radio>
              <el-radio value="rejected">
                <el-icon color="#f56c6c"><CircleClose /></el-icon>
                拒绝
              </el-radio>
            </el-radio-group>
          </el-form-item>

          <el-form-item label="审批意见">
            <el-input
              v-model="approvalForm.comments"
              type="textarea"
              :rows="4"
              :placeholder="
                approvalForm.decision === 'approved' ? '请填写批准意见(可选)' : '请说明拒绝理由'
              "
            />
          </el-form-item>
        </el-form>
      </div>

      <template #footer>
        <span class="dialog-footer">
          <el-button @click="handleCloseApprovalDialog">取消</el-button>
          <el-button
            type="primary"
            :loading="submitting"
            :disabled="!approvalForm.decision"
            @click="submitApproval"
          >
            提交审批
          </el-button>
        </span>
      </template>
    </el-dialog>

    <!-- 详情对话框 -->
    <el-dialog v-model="detailsDialogVisible" title="变更申请详情" width="800px" destroy-on-close>
      <div v-if="selectedRequest" class="details-content">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="申请编号">{{ selectedRequest.id }}</el-descriptions-item>
          <el-descriptions-item label="供应商">
            {{ selectedRequest.companyName }}
          </el-descriptions-item>
          <el-descriptions-item label="供应商代码">
            {{ selectedRequest.companyId }}
          </el-descriptions-item>
          <el-descriptions-item label="风险等级">
            <el-tag :type="getRiskTagType(selectedRequest.riskLevel)" size="small">
              {{ getRiskLevelText(selectedRequest.riskLevel) }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="提交时间">
            {{ formatDateTime(selectedRequest.submittedAt) }}
          </el-descriptions-item>
          <el-descriptions-item label="当前状态">
            {{ getStatusText(selectedRequest.status) }}
          </el-descriptions-item>
        </el-descriptions>

        <el-divider content-position="left">变更内容</el-divider>
        <el-table :data="getChangedFieldsArray(selectedRequest.payload)" border>
          <el-table-column prop="label" label="字段" width="150" />
          <el-table-column prop="newValue" label="新值" />
        </el-table>

        <el-divider content-position="left">审批进度</el-divider>
        <ChangeRequestTimeline
          v-if="requestDetails"
          :workflow="requestDetails.workflow"
          :approval-history="requestDetails.approvalHistory"
          :current-step="selectedRequest.currentStep"
          :status="selectedRequest.status"
        />
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, reactive, onMounted, defineAsyncComponent, computed } from "vue";
import { Refresh, CircleCheck, CircleClose } from "@element-plus/icons-vue";
import PageHeader from "@/components/layout/PageHeader.vue";
import {
  getMyPendingApprovals,
  getMyApprovedApprovals,
  getChangeRequestDetails,
  approveChangeRequest,
} from "@/api/changeRequests";
import type { ChangeRequest, ChangeRequestDetails } from "@/api/changeRequests";
import { useNotification } from "@/composables";

const notification = useNotification();

const ChangeRequestTimeline = defineAsyncComponent(
  () => import("@/components/ChangeRequestTimeline.vue"),
);

const loading = ref(false);
const pendingRequests = ref<ChangeRequest[]>([]);
const approvedLoading = ref(false);
const approvedRequests = ref<ChangeRequest[]>([]);
const approvalDialogVisible = ref(false);
const detailsDialogVisible = ref(false);
const selectedRequest = ref<ChangeRequest | null>(null);
const requestDetails = ref<ChangeRequestDetails | null>(null);
const submitting = ref(false);

const approvalForm = reactive({
  decision: "",
  comments: "",
});

// 字段标签映射
const fieldLabels: Record<string, string> = {
  companyName: "公司名称",
  companyId: "供应商代码",
  contactPerson: "主联系人",
  contactPhone: "联系电话",
  contactEmail: "联系邮箱",
  category: "供应商类别",
  address: "注册地址",
  businessRegistrationNumber: "营业执照号",
  paymentTerms: "付款条款",
  paymentCurrency: "付款币种",
  bankAccount: "银行账户",
  region: "地区",
};

// 加载待审批列表
const loadPendingRequests = async () => {
  try {
    loading.value = true;
    pendingRequests.value = await getMyPendingApprovals();
  } catch (error: any) {
    console.error("加载待审批列表失败:", error);
    notification.error(error.message || "加载失败");
  } finally {
    loading.value = false;
  }
};

// 加载已审批列表
const loadApprovedRequests = async () => {
  try {
    approvedLoading.value = true;
    approvedRequests.value = await getMyApprovedApprovals();
  } catch (error: any) {
    console.error("加载已审批列表失败:", error);
    notification.error(error.message || "加载失败");
  } finally {
    approvedLoading.value = false;
  }
};

const approvalSections = computed(() => [
  {
    key: "pending",
    title: "待我审批",
    items: pendingRequests.value,
    loading: loading.value,
    emptyText: "暂无待审批的变更申请",
    refresh: loadPendingRequests,
    canApprove: true,
  },
  {
    key: "approved",
    title: "已审批项",
    items: approvedRequests.value,
    loading: approvedLoading.value,
    emptyText: "暂无已审批的变更申请",
    refresh: loadApprovedRequests,
    canApprove: false,
  },
]);

// 打开审批对话框
const handleApprove = async (request: ChangeRequest) => {
  selectedRequest.value = request;
  approvalForm.decision = "";
  approvalForm.comments = "";
  approvalDialogVisible.value = true;
  requestDetails.value = null;

  try {
    requestDetails.value = await getChangeRequestDetails(request.id);
  } catch (error: any) {
    console.error("加载详情失败:", error);
  }
};

// 查看详情
const viewDetails = async (request: ChangeRequest) => {
  selectedRequest.value = request;
  detailsDialogVisible.value = true;
  requestDetails.value = null;

  try {
    requestDetails.value = await getChangeRequestDetails(request.id);
  } catch (error: any) {
    console.error("加载详情失败:", error);
  }
};

// 提交审批
const submitApproval = async () => {
  if (!selectedRequest.value) return;

  if (!approvalForm.decision) {
    notification.warning("请选择审批决定");
    return;
  }

  if (approvalForm.decision === "rejected" && !approvalForm.comments.trim()) {
    notification.warning("拒绝时请填写拒绝理由");
    return;
  }

  try {
    await notification.confirm(
      `确认${approvalForm.decision === "approved" ? "批准" : "拒绝"}此变更申请?`,
      "确认操作",
      {
        confirmButtonText: "确认",
        cancelButtonText: "取消",
        type: approvalForm.decision === "approved" ? "success" : "warning",
      },
    );

    submitting.value = true;

    const result = await approveChangeRequest(
      selectedRequest.value.id,
      approvalForm.decision as "approved" | "rejected",
      approvalForm.comments,
    );

    notification.success(result.message || "审批提交成功");

    approvalDialogVisible.value = false;
    await Promise.all([loadPendingRequests(), loadApprovedRequests()]);
  } catch (error: any) {
    if (error !== "cancel") {
      console.error("审批失败:", error);
      notification.error(error.message || "审批失败");
    }
  } finally {
    submitting.value = false;
  }
};

// 关闭审批对话框
const handleCloseApprovalDialog = () => {
  if (approvalForm.decision && !submitting.value) {
    notification.confirm("您有未提交的审批决定,确定关闭?", "提示", {
      confirmButtonText: "确定",
      cancelButtonText: "取消",
    })
      .then(() => {
        approvalDialogVisible.value = false;
      })
      .catch(() => {});
  } else {
    approvalDialogVisible.value = false;
  }
};

// 辅助函数
const getRiskLevelText = (level?: string) => {
  const map: Record<string, string> = {
    high: "高风险",
    medium: "中风险",
    low: "低风险",
  };
  return map[level || "low"] || "未知";
};

const getRiskTagType = (level?: string) => {
  if (level === "high") return "danger";
  if (level === "medium") return "warning";
  return "info";
};

const getStatusText = (status: string) => {
  if (status === "approved") return "已批准";
  if (status === "rejected") return "已拒绝";
  if (status.startsWith("pending_")) return "审批中";
  return status;
};

const getChangedFieldsCount = (payload: any) => {
  return Object.keys(payload || {}).length;
};

const getChangedFieldsArray = (payload: any) => {
  if (!payload) return [];
  return Object.entries(payload).map(([key, value]) => ({
    key,
    label: fieldLabels[key] || key,
    newValue: value,
  }));
};

const formatDateTime = (dateStr: string) => {
  if (!dateStr) return "-";
  return new Date(dateStr).toLocaleString("zh-CN");
};

onMounted(() => {
  loadPendingRequests();
  loadApprovedRequests();
});




</script>

<style scoped>
.approval-view {
  padding: 24px;
  background: #f5f7fa;
  min-height: calc(100vh - 60px);
}




.content-wrapper {
  max-width: 1400px;
  margin: 0 auto;
}

.approval-stack {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.approval-card {
  border-radius: 8px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-weight: 600;
}

.supplier-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.supplier-name {
  font-weight: 600;
  color: #303133;
}

.supplier-code {
  font-size: 12px;
  color: #909399;
}

.empty-state {
  padding: 40px 20px;
}

.approval-content,
.details-content {
  padding: 0 8px;
}

:deep(.el-radio) {
  display: flex;
  align-items: center;
  margin-right: 30px;
}

:deep(.el-radio__label) {
  display: flex;
  align-items: center;
  gap: 6px;
}

:deep(.el-divider__text) {
  font-weight: 600;
  font-size: 15px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
</style>
