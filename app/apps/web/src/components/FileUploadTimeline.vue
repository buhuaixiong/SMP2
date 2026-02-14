<template>
  <el-timeline>
    <el-timeline-item
      v-for="step in timelineSteps"
      :key="step.step"
      :type="step.type"
      :color="step.color"
      :hollow="step.hollow"
    >
      <div class="timeline-card" :class="step.status">
        <div class="timeline-header">
          <h4>{{ step.label }}</h4>
          <el-tag :type="step.tagType" size="small">{{ step.statusText }}</el-tag>
        </div>

        <div v-if="step.approvalRecord" class="timeline-content">
          <p v-if="step.approvalRecord.approverName">
            <strong>{{ t("fileUpload.timeline.approver") }}:</strong> {{ step.approvalRecord.approverName }}
          </p>
          <p v-if="step.approvalRecord.createdAt">
            <strong>{{ t("fileUpload.timeline.approvedAt") }}:</strong> {{ formatTime(step.approvalRecord.createdAt) }}
          </p>
          <p v-if="step.approvalRecord.decision">
            <strong>{{ t("fileUpload.timeline.decision") }}:</strong>
            <el-tag
              :type="step.approvalRecord.decision === 'approved' ? 'success' : 'danger'"
              size="small"
            >
              {{ getDecisionLabel(step.approvalRecord.decision) }}
            </el-tag>
          </p>
          <p v-if="step.approvalRecord.comments" class="comments">
            <strong>{{ t("fileUpload.timeline.comments") }}:</strong> {{ step.approvalRecord.comments }}
          </p>
        </div>

        <div v-else-if="step.status === 'pending'" class="timeline-content">
          <p style="color: #e6a23c">{{ t("fileUpload.timeline.pendingMessage") }}</p>
        </div>

        <div v-else class="timeline-content">
          <p style="color: #909399">{{ t("fileUpload.timeline.futureMessage") }}</p>
        </div>
      </div>
    </el-timeline-item>
  </el-timeline>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { FileApprovalRecord, WorkflowStep } from "@/api/fileUploads";
import dayjs from "dayjs";

const props = defineProps<{
  workflow: WorkflowStep[];
  currentStep: string;
  status: string;
  approvalHistory: FileApprovalRecord[];
}>();

const { t } = useI18n();

interface TimelineStep {
  step: string;
  label: string;
  status: "completed" | "pending" | "rejected" | "future";
  statusText: string;
  type: "primary" | "success" | "warning" | "danger" | "info";
  tagType: "primary" | "success" | "warning" | "danger" | "info";
  color: string;
  hollow: boolean;
  approvalRecord?: FileApprovalRecord;
}

const getDecisionLabel = (decision: string | null) => {
  if (decision === "approved") {
    return t("fileUpload.timeline.decisionLabels.approved");
  }
  return t("fileUpload.timeline.decisionLabels.rejected");
};

const timelineSteps = computed<TimelineStep[]>(() => {
  const steps: TimelineStep[] = [];
  const approvalMap = new Map(props.approvalHistory.map((record) => [record.step, record]));

  let foundCurrent = false;
  const isRejected = props.status === "rejected";
  const isCompleted = props.status === "approved";

  props.workflow.forEach((workflowStep) => {
    const approvalRecord = approvalMap.get(workflowStep.step);
    const isCurrent = workflowStep.step === props.currentStep && !isCompleted;
    const isPast = !isCurrent && !foundCurrent && !isRejected;

    let status: TimelineStep["status"];
    let statusText: string;
    let type: TimelineStep["type"];
    let tagType: TimelineStep["tagType"];
    let color: string;
    let hollow: boolean;

    if (approvalRecord) {
      if (approvalRecord.decision === "approved") {
        status = "completed";
        statusText = t("fileUpload.timeline.status.approved");
        type = "success";
        tagType = "success";
        color = "#67c23a";
        hollow = false;
      } else {
        status = "rejected";
        statusText = t("fileUpload.timeline.status.rejected");
        type = "danger";
        tagType = "danger";
        color = "#f56c6c";
        hollow = false;
      }
    } else if (isCurrent) {
      status = "pending";
      statusText = t("fileUpload.timeline.status.pending");
      type = "warning";
      tagType = "warning";
      color = "#e6a23c";
      hollow = false;
      foundCurrent = true;
    } else if (isPast && isCompleted) {
      status = "completed";
      statusText = t("fileUpload.timeline.status.approved");
      type = "success";
      tagType = "success";
      color = "#67c23a";
      hollow = false;
    } else {
      status = "future";
      statusText = t("fileUpload.timeline.status.future");
      type = "info";
      tagType = "info";
      color = "#909399";
      hollow = true;
    }

    steps.push({
      step: workflowStep.step,
      label: workflowStep.label,
      status,
      statusText,
      type,
      tagType,
      color,
      hollow,
      approvalRecord,
    });

    if (isCurrent && !isRejected) {
      foundCurrent = true;
    }
  });

  return steps;
});

const formatTime = (dateStr: string): string => {
  return dayjs(dateStr).format("YYYY-MM-DD HH:mm");
};
</script>

<style scoped>
.timeline-card {
  padding: 16px;
  border-radius: 8px;
  border: 1px solid #e4e7ed;
  background: #ffffff;
  transition: all 0.3s;
}

.timeline-card:hover {
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.timeline-card.completed {
  background: #f0f9ff;
  border-color: #67c23a;
}

.timeline-card.pending {
  background: #fdf6ec;
  border-color: #e6a23c;
}

.timeline-card.rejected {
  background: #fef0f0;
  border-color: #f56c6c;
}

.timeline-card.future {
  background: #fafafa;
  border-color: #dcdfe6;
}

.timeline-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.timeline-header h4 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.timeline-content {
  font-size: 14px;
  color: #606266;
  line-height: 1.6;
}

.timeline-content p {
  margin: 4px 0;
}

.timeline-content .comments {
  padding: 8px;
  background: #f5f7fa;
  border-radius: 4px;
  margin-top: 8px;
  font-size: 13px;
  color: #606266;
}
</style>
