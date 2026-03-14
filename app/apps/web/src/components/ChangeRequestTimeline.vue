<template>
  <div class="change-request-timeline">
    <el-timeline>
      <el-timeline-item
        v-for="(step, index) in timelineSteps"
        :key="step.step"
        :timestamp="step.timestamp"
        :type="step.type"
        :icon="step.icon"
        :color="step.color"
        placement="top"
      >
        <div class="timeline-card" :class="step.status">
          <div class="timeline-header">
            <h4>{{ step.label }}</h4>
            <el-tag :type="step.tagType" size="small">{{ step.statusText }}</el-tag>
          </div>

          <div v-if="step.approverName" class="timeline-content">
            <p><strong>审批人:</strong> {{ step.approverName }}</p>
            <p v-if="step.decision">
              <strong>决定:</strong>
              <el-tag
                :type="step.decision === 'approved' ? 'success' : 'danger'"
                size="small"
                style="margin-left: 8px"
              >
                {{ step.decision === "approved" ? "批准" : "拒绝" }}
              </el-tag>
            </p>
            <p v-if="step.comments" class="comments"><strong>备注:</strong> {{ step.comments }}</p>
          </div>

          <div v-else-if="step.status === 'pending'" class="timeline-content pending-hint">
            <el-icon><Clock /></el-icon>
            <span>等待审批中...</span>
          </div>

          <div v-else-if="step.status === 'future'" class="timeline-content future-hint">
            <span>待前序步骤完成</span>
          </div>
        </div>
      </el-timeline-item>

      <!-- 最终结果 -->
      <el-timeline-item
        v-if="finalStatus"
        :timestamp="finalTimestamp"
        :type="
          finalStatus === 'approved' ? 'success' : finalStatus === 'rejected' ? 'danger' : 'info'
        "
        :icon="finalIcon"
        placement="top"
      >
        <div class="timeline-card final" :class="finalStatus">
          <div class="timeline-header">
            <h4>{{ finalTitle }}</h4>
            <el-tag :type="finalTagType" size="small">{{ finalStatusText }}</el-tag>
          </div>
          <div v-if="finalMessage" class="timeline-content">
            <p>{{ finalMessage }}</p>
          </div>
        </div>
      </el-timeline-item>
    </el-timeline>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { Clock, Check, Close, CircleCheck, CircleClose, Warning } from "@element-plus/icons-vue";
import type { ApprovalRecord, WorkflowStep } from "@/api/changeRequests";

interface Props {
  workflow: WorkflowStep[];
  approvalHistory: ApprovalRecord[];
  currentStep: string;
  status: string;
}

const props = defineProps<Props>();

// 工作流步骤的中文标签映射
const stepLabels: Record<string, string> = {
  purchaser: "采购员审批",
  quality_manager: "品质审批",
  procurement_manager: "采购经理审批",
  procurement_director: "采购总监审批",
  finance_director: "财务总监审批",
};

// 构建时间线步骤
const timelineSteps = computed(() => {
  return props.workflow.map((step, index) => {
    // 查找该步骤的审批记录
    const approval = props.approvalHistory.find((a) => a.step === step.step);

    let status: "completed" | "pending" | "rejected" | "future" = "future";
    let type: "success" | "warning" | "danger" | "info" = "info";
    let icon = Warning;
    let color = "#909399";
    let tagType: "success" | "warning" | "danger" | "info" = "info";
    let statusText = "未开始";

    if (approval) {
      if (approval.decision === "approved") {
        status = "completed";
        type = "success";
        icon = CircleCheck;
        color = "#67c23a";
        tagType = "success";
        statusText = "已批准";
      } else if (approval.decision === "rejected") {
        status = "rejected";
        type = "danger";
        icon = CircleClose;
        color = "#f56c6c";
        tagType = "danger";
        statusText = "已拒绝";
      }
    } else if (step.step === props.currentStep && props.status.includes("pending")) {
      status = "pending";
      type = "warning";
      icon = Clock;
      color = "#e6a23c";
      tagType = "warning";
      statusText = "审批中";
    }

    return {
      step: step.step,
      label: stepLabels[step.step] || step.label,
      status,
      type,
      icon,
      color,
      tagType,
      statusText,
      approverName: approval?.approverName,
      decision: approval?.decision,
      comments: approval?.comments,
      timestamp: approval?.createdAt
        ? new Date(approval.createdAt).toLocaleString("zh-CN")
        : undefined,
    };
  });
});

// 最终状态
const finalStatus = computed(() => {
  if (props.status === "approved") return "approved";
  if (props.status === "rejected") return "rejected";
  return null;
});

const finalTimestamp = computed(() => {
  if (!props.approvalHistory.length) return undefined;
  const lastApproval = props.approvalHistory[props.approvalHistory.length - 1];
  return new Date(lastApproval.createdAt).toLocaleString("zh-CN");
});

const finalIcon = computed(() => {
  if (finalStatus.value === "approved") return Check;
  if (finalStatus.value === "rejected") return Close;
  return undefined;
});

const finalTagType = computed(() => {
  if (finalStatus.value === "approved") return "success";
  if (finalStatus.value === "rejected") return "danger";
  return "info";
});

const finalTitle = computed(() => {
  if (finalStatus.value === "approved") return "审批完成";
  if (finalStatus.value === "rejected") return "审批被拒绝";
  return "流程进行中";
});

const finalStatusText = computed(() => {
  if (finalStatus.value === "approved") return "变更已应用";
  if (finalStatus.value === "rejected") return "流程终止";
  return "进行中";
});

const finalMessage = computed(() => {
  if (finalStatus.value === "approved") {
    return "所有审批步骤已通过,您的资料修改已自动应用。";
  }
  if (finalStatus.value === "rejected") {
    return "审批流程已被拒绝,资料未做任何修改。如需修改,请重新提交申请。";
  }
  return null;
});
</script>

<style scoped>
.change-request-timeline {
  padding: 20px 0;
}

.timeline-card {
  background: #fff;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  padding: 16px;
  margin-top: -6px;
  transition: all 0.3s;
}

.timeline-card:hover {
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.timeline-card.completed {
  border-left: 3px solid #67c23a;
  background: #f0f9ff;
}

.timeline-card.pending {
  border-left: 3px solid #e6a23c;
  background: #fdf6ec;
}

.timeline-card.rejected {
  border-left: 3px solid #f56c6c;
  background: #fef0f0;
}

.timeline-card.future {
  opacity: 0.6;
}

.timeline-card.final {
  font-weight: 500;
}

.timeline-card.final.approved {
  border-left: 3px solid #67c23a;
  background: linear-gradient(135deg, #f0f9ff 0%, #e8f5e9 100%);
}

.timeline-card.final.rejected {
  border-left: 3px solid #f56c6c;
  background: linear-gradient(135deg, #fef0f0 0%, #ffebee 100%);
}

.timeline-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.timeline-header h4 {
  margin: 0;
  font-size: 15px;
  color: #303133;
}

.timeline-content {
  font-size: 13px;
  color: #606266;
  line-height: 1.6;
}

.timeline-content p {
  margin: 6px 0;
}

.timeline-content.pending-hint,
.timeline-content.future-hint {
  display: flex;
  align-items: center;
  gap: 6px;
  color: #909399;
  font-style: italic;
}

.comments {
  padding: 8px 12px;
  background: #f5f7fa;
  border-radius: 4px;
  margin-top: 8px;
}

:deep(.el-timeline-item__timestamp) {
  font-size: 12px;
  color: #909399;
}
</style>
