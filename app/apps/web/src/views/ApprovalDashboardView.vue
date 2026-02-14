<template>
  <div class="approval-dashboard-container">
    <el-card class="header-card">
      <div class="header-content">
        <div>
          <h2>{{ t("approvalDashboard.title") }}</h2>
          <p class="subtitle">{{ t("approvalDashboard.subtitle") }}</p>
        </div>
        <el-button
          type="primary"
          :icon="Refresh"
          @click="refreshAll"
          :loading="loading || approvedLoading"
        >
          {{ t("common.refresh") }}
        </el-button>
      </div>
    </el-card>

    <el-card class="stats-card" v-if="statistics">
      <div class="stats-grid">
        <div class="stat-item">
          <div class="stat-value">{{ statistics.total }}</div>
          <div class="stat-label">{{ t("approvalDashboard.stats.total") }}</div>
        </div>
        <div class="stat-item">
          <div class="stat-value urgent">{{ statistics.urgent }}</div>
          <div class="stat-label">{{ t("approvalDashboard.stats.urgent") }}</div>
        </div>
        <div class="stat-item">
          <div class="stat-value">{{ statistics.thisWeek }}</div>
          <div class="stat-label">{{ t("approvalDashboard.stats.thisWeek") }}</div>
        </div>
        <div class="stat-item">
          <div class="stat-value">{{ formatPercentage(statistics.avgCompleteness) }}</div>
          <div class="stat-label">{{ t("approvalDashboard.stats.avgCompleteness") }}</div>
        </div>
      </div>
    </el-card>

    <el-card class="filter-card">
      <el-form :inline="true" :model="filters" class="filter-form">
        <el-form-item :label="t('approvalDashboard.filters.status')">
          <el-select v-model="filters.status" clearable @change="loadApplications">
            <el-option
              v-for="status in statusOptions"
              :key="status.value"
              :label="t(status.label)"
              :value="status.value"
            />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('approvalDashboard.filters.urgency')">
          <el-select v-model="filters.urgency" clearable @change="loadApplications">
            <el-option :label="t('approvalDashboard.urgency.urgent')" value="urgent" />
            <el-option :label="t('approvalDashboard.urgency.normal')" value="normal" />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('approvalDashboard.filters.search')">
          <el-input
            v-model="filters.search"
            :placeholder="t('approvalDashboard.searchPlaceholder')"
            clearable
            @input="debounceSearch"
          />
        </el-form-item>
      </el-form>
    </el-card>

    <el-card
      v-for="section in approvalSections"
      :key="section.key"
      class="applications-card"
      v-loading="section.loading"
    >
      <template #header>
        <div class="section-header">
          <span>{{ section.title }} ({{ section.items.length }})</span>
        </div>
      </template>

      <div v-if="!section.items.length" class="empty-state">
        <el-empty :description="section.emptyText" />
      </div>

      <div v-else class="applications-list">
        <div
          v-for="app in section.items"
          :key="app.id"
          class="application-item"
          :class="{ urgent: isUrgent(app.dueAt) }"
        >
          <div class="app-header">
            <div class="app-info">
              <div class="app-title">
                <span class="supplier-name">{{ app.supplierName }}</span>
                <el-tag :type="getStatusType(app.status)" size="small">
                  {{ t(`approvalDashboard.statuses.${app.status}`) }}
                </el-tag>
                <el-tag v-if="isUrgent(app.dueAt)" type="danger" size="small">
                  {{ t("approvalDashboard.urgent") }}
                </el-tag>
              </div>
              <div class="app-meta">
                <span>
                  <el-icon><User /></el-icon>
                  {{ app.submittedBy }}
                </span>
                <span>
                  <el-icon><Clock /></el-icon>
                  {{ formatDate(app.submittedAt) }}
                </span>
                <span v-if="app.dueAt">
                  <el-icon><Calendar /></el-icon>
                  {{ t("approvalDashboard.dueDate") }}: {{ formatDate(app.dueAt) }}
                  <span v-if="getRemainingDays(app.dueAt) !== null" class="remaining-days">
                    ({{ getRemainingDays(app.dueAt) }}{{ t("common.daysRemaining") }})
                  </span>
                </span>
              </div>
            </div>
            <div class="app-actions">
              <el-button type="primary" size="small" @click="viewApplication(app, section.key)">
                {{ t("approvalDashboard.viewDetails") }}
              </el-button>
            </div>
          </div>

          <div class="app-body">
            <div class="progress-section">
              <div class="progress-label">
                <span
                  >{{ t("approvalDashboard.currentStep") }}:
                  {{ t(`upgradeManagement.steps.${app.currentStep}`) }}</span
                >
                <span class="completeness">{{ formatPercentage(app.documentCompleteness) }}</span>
              </div>
              <el-progress
                :percentage="app.documentCompleteness"
                :color="getCompletenessColor(app.documentCompleteness)"
              />
            </div>

            <div class="documents-preview">
              <div class="section-title">{{ t("approvalDashboard.documents") }}</div>
              <div class="document-tags">
                <el-tag
                  v-for="doc in app.documents"
                  :key="doc.id"
                  :type="doc.file ? 'success' : 'info'"
                  size="small"
                  class="doc-tag"
                >
                  {{ doc.requirementName }}
                </el-tag>
              </div>
            </div>
          </div>
        </div>
      </div>
    </el-card>

    <el-dialog
      v-model="detailDialogVisible"
      :title="t('approvalDashboard.applicationDetails')"
      width="80%"
      :close-on-click-modal="false"
    >
      <div v-if="selectedApplication" class="detail-content" v-loading="detailLoading">
        <el-descriptions :column="2" border>
          <el-descriptions-item :label="t('approvalDashboard.supplierName')">
            {{ selectedApplication.supplierName }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('approvalDashboard.applicationId')">
            #{{ selectedApplication.id }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('approvalDashboard.status')">
            <el-tag :type="getStatusType(selectedApplication.status)">
              {{ t(`approvalDashboard.statuses.${selectedApplication.status}`) }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item :label="t('approvalDashboard.currentStep')">
            {{ t(`upgradeManagement.steps.${selectedApplication.currentStep}`) }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('approvalDashboard.submittedBy')">
            {{ selectedApplication.submittedBy }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('approvalDashboard.submittedAt')">
            {{ formatDateTime(selectedApplication.submittedAt) }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('approvalDashboard.dueDate')">
            <span :class="{ 'text-danger': isUrgent(selectedApplication.dueAt) }">
              {{ formatDate(selectedApplication.dueAt) }}
              <span v-if="getRemainingDays(selectedApplication.dueAt) !== null">
                ({{ getRemainingDays(selectedApplication.dueAt) }}{{ t("common.daysRemaining") }})
              </span>
            </span>
          </el-descriptions-item>
          <el-descriptions-item :label="t('approvalDashboard.completeness')">
            {{ formatPercentage(selectedApplication.documentCompleteness) }}
          </el-descriptions-item>
        </el-descriptions>

        <el-divider />

        <div class="documents-section">
          <h3>{{ t("approvalDashboard.uploadedDocuments") }}</h3>
          <el-table :data="selectedApplication.documents" border stripe>
            <el-table-column
              :label="t('approvalDashboard.table.documentName')"
              prop="requirementName"
              min-width="150"
            />
            <el-table-column :label="t('approvalDashboard.table.status')" width="100">
              <template #default="{ row }">
                <el-tag v-if="row.file" type="success" size="small">
                  {{ t("approvalDashboard.uploaded") }}
                </el-tag>
                <el-tag v-else type="info" size="small">
                  {{ t("approvalDashboard.notUploaded") }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column :label="t('approvalDashboard.table.validityPeriod')" width="200">
              <template #default="{ row }">
                <span v-if="row.file && row.file.validFrom && row.file.validTo">
                  {{ formatDate(row.file.validFrom) }} ~ {{ formatDate(row.file.validTo) }}
                </span>
                <span v-else>-</span>
              </template>
            </el-table-column>
            <el-table-column :label="t('approvalDashboard.table.uploadedAt')" width="180">
              <template #default="{ row }">
                {{ row.file ? formatDateTime(row.file.uploadTime) : "-" }}
              </template>
            </el-table-column>
            <el-table-column :label="t('common.actions')" width="120" fixed="right">
              <template #default="{ row }">
                <el-button
                  v-if="row.file"
                  type="primary"
                  size="small"
                  link
                  @click="downloadDocument(row.file)"
                >
                  {{ t("common.download") }}
                </el-button>
                <span v-else>-</span>
              </template>
            </el-table-column>
          </el-table>
        </div>

        <el-divider />

        <div class="approval-history-section">
          <h3>{{ t("approvalDashboard.approvalHistory") }}</h3>
          <el-timeline v-if="approvalHistory.length">
            <el-timeline-item
              v-for="review in approvalHistory"
              :key="review.id"
              :timestamp="formatDateTime(review.decidedAt)"
              :type="review.decision === 'approved' ? 'success' : 'danger'"
            >
              <div class="timeline-content">
                <div class="timeline-header">
                  <el-tag
                    :type="review.decision === 'approved' ? 'success' : 'danger'"
                    size="small"
                  >
                    {{ t(`approvalDashboard.decisions.${review.decision}`) }}
                  </el-tag>
                  <span class="step-name">{{ review.stepName }}</span>
                </div>
                <div class="timeline-body">
                  <div class="reviewer">{{ review.decidedByName }}</div>
                  <div v-if="review.comments" class="comments">{{ review.comments }}</div>
                </div>
              </div>
            </el-timeline-item>
          </el-timeline>
          <el-empty v-else :description="t('approvalDashboard.noHistory')" />
        </div>

        <el-divider />

        <div
          class="decision-section"
          v-if="detailSource === 'pending' && canApprove(selectedApplication)"
        >
          <h3>{{ t("approvalDashboard.makeDecision") }}</h3>
          <el-form :model="decisionForm" label-width="100px">
            <el-form-item :label="t('approvalDashboard.decision')">
              <el-radio-group v-model="decisionForm.decision">
                <el-radio value="approved">{{ t("approvalDashboard.approve") }}</el-radio>
                <el-radio value="rejected">{{ t("approvalDashboard.reject") }}</el-radio>
              </el-radio-group>
            </el-form-item>
            <el-form-item :label="t('approvalDashboard.comments')">
              <el-input
                v-model="decisionForm.comments"
                type="textarea"
                :rows="4"
                :placeholder="t('approvalDashboard.commentsPlaceholder')"
              />
            </el-form-item>
            <el-form-item>
              <el-button
                type="primary"
                @click="submitDecision"
                :loading="submitting"
                :disabled="!decisionForm.decision"
              >
                {{ t("approvalDashboard.submitDecision") }}
              </el-button>
              <el-button @click="detailDialogVisible = false">
                {{ t("common.cancel") }}
              </el-button>
            </el-form-item>
          </el-form>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

import { Refresh, User, Clock, Calendar } from "@element-plus/icons-vue";
import {
  fetchPendingUpgradeApplications,
  fetchApprovedUpgradeApplications,
  submitUpgradeDecision,
} from "@/api/upgrade";
import type { PendingUpgradeApplication } from "@/api/upgrade";
import { useAuthStore } from "@/stores/auth";
import { useUpgradeStore } from "@/stores/upgrade";
import { resolveApiUrl } from "@/utils/apiBaseUrl";
import { openFileInNewTab } from "@/utils/fileDownload";


import { useNotification } from "@/composables";

const notification = useNotification();
const { t } = useI18n();
const authStore = useAuthStore();
const upgradeStore = useUpgradeStore();

type ApprovalSectionKey = "pending" | "approved";

type ApprovalSection = {
  key: ApprovalSectionKey;
  title: string;
  items: PendingUpgradeApplication[];
  loading: boolean;
  emptyText: string;
};

const loading = ref(false);
const approvedLoading = ref(false);
const detailLoading = ref(false);
const submitting = ref(false);
const applications = ref<PendingUpgradeApplication[]>([]);
const approvedApplications = ref<PendingUpgradeApplication[]>([]);
const detailDialogVisible = ref(false);
const selectedApplication = ref<PendingUpgradeApplication | null>(null);
const approvalHistory = ref<any[]>([]);
const detailSource = ref<ApprovalSectionKey>("pending");

const filters = ref({
  status: "",
  urgency: "",
  search: "",
});

const decisionForm = ref({
  decision: "" as "approved" | "rejected" | "",
  comments: "",
});

let pollingInterval: number | null = null;

const statusOptions = [
  {
    value: "pending_procurement_review",
    label: "approvalDashboard.statuses.pending_procurement_review",
  },
  { value: "pending_quality_review", label: "approvalDashboard.statuses.pending_quality_review" },
  {
    value: "pending_procurement_manager_review",
    label: "approvalDashboard.statuses.pending_procurement_manager_review",
  },
  {
    value: "pending_procurement_director_review",
    label: "approvalDashboard.statuses.pending_procurement_director_review",
  },
  {
    value: "pending_finance_director_review",
    label: "approvalDashboard.statuses.pending_finance_director_review",
  },
];

const statistics = computed(() => {
  if (!applications.value.length) return null;

  const total = applications.value.length;
  const urgent = applications.value.filter((app) => isUrgent(app.dueAt)).length;

  const oneWeekAgo = new Date();
  oneWeekAgo.setDate(oneWeekAgo.getDate() - 7);
  const thisWeek = applications.value.filter(
    (app) => new Date(app.submittedAt) >= oneWeekAgo,
  ).length;

  const totalCompleteness = applications.value.reduce(
    (sum, app) => sum + app.documentCompleteness,
    0,
  );
  const avgCompleteness = totalCompleteness / total;

  return { total, urgent, thisWeek, avgCompleteness };
});

const filterApplications = (
  list: PendingUpgradeApplication[],
  includeStatusFilter: boolean,
): PendingUpgradeApplication[] => {
  let result = [...list];

  if (includeStatusFilter && filters.value.status) {
    result = result.filter((app) => app.status === filters.value.status);
  }

  if (filters.value.urgency === "urgent") {
    result = result.filter((app) => isUrgent(app.dueAt));
  } else if (filters.value.urgency === "normal") {
    result = result.filter((app) => !isUrgent(app.dueAt));
  }

  if (filters.value.search) {
    const search = filters.value.search.toLowerCase();
    result = result.filter(
      (app) =>
        app.supplierName.toLowerCase().includes(search) ||
        app.submittedBy.toLowerCase().includes(search),
    );
  }

  return result.sort((a, b) => {
    const urgentA = isUrgent(a.dueAt);
    const urgentB = isUrgent(b.dueAt);
    if (urgentA !== urgentB) return urgentA ? -1 : 1;
    return new Date(b.submittedAt).getTime() - new Date(a.submittedAt).getTime();
  });
};

const filteredPendingApplications = computed(() =>
  filterApplications(applications.value, true),
);

const filteredApprovedApplications = computed(() =>
  filterApplications(approvedApplications.value, false),
);

const approvalSections = computed<ApprovalSection[]>(() => [
  {
    key: "pending",
    title: t("approvalDashboard.sections.pending"),
    items: filteredPendingApplications.value,
    loading: loading.value,
    emptyText: t("approvalDashboard.noApplications"),
  },
  {
    key: "approved",
    title: t("approvalDashboard.sections.approved"),
    items: filteredApprovedApplications.value,
    loading: approvedLoading.value,
    emptyText: t("approvalDashboard.noApprovedApplications"),
  },
]);

const loadApplications = async () => {
  loading.value = true;
  try {
    const data = await fetchPendingUpgradeApplications();
    applications.value = data;
  } catch (error: any) {
    notification.error(error.message || t("approvalDashboard.loadError"));
  } finally {
    loading.value = false;
  }
};

const loadApprovedApplications = async () => {
  approvedLoading.value = true;
  try {
    const data = await fetchApprovedUpgradeApplications();
    approvedApplications.value = data;
  } catch (error: any) {
    notification.error(error.message || t("approvalDashboard.loadError"));
  } finally {
    approvedLoading.value = false;
  }
};

const refreshAll = async () => {
  await Promise.all([loadApplications(), loadApprovedApplications()]);
};

const viewApplication = async (
  app: PendingUpgradeApplication,
  source: ApprovalSectionKey = "pending",
) => {
  selectedApplication.value = app;
  detailDialogVisible.value = true;
  detailSource.value = source;
  detailLoading.value = true;

  try {
    const status = await upgradeStore.loadStatus(app.supplierId);
    approvalHistory.value = status.reviews || [];
  } catch (error: any) {
    notification.error(error.message || t("approvalDashboard.loadDetailError"));
  } finally {
    detailLoading.value = false;
  }
};

const canApprove = (app: PendingUpgradeApplication): boolean => {
  if (!authStore.user) return false;

  const stepPermissionMap: Record<string, string> = {
    pending_procurement_review: "supplier.upgrade.init",
    pending_quality_review: "supplier.upgrade.approve.quality",
    pending_procurement_manager_review: "supplier.upgrade.approve.manager",
    pending_procurement_director_review: "supplier.upgrade.approve.director",
    pending_finance_director_review: "supplier.upgrade.approve.finance",
  };

  const requiredPermission = stepPermissionMap[app.status];
  return requiredPermission ? authStore.user.permissions?.includes(requiredPermission) : false;
};

const getStepKey = (status: string): string => {
  const stepMap: Record<string, string> = {
    pending_procurement_review: "procurement_review",
    pending_quality_review: "quality_review",
    pending_procurement_manager_review: "procurement_manager_review",
    pending_procurement_director_review: "procurement_director_review",
    pending_finance_director_review: "finance_director_review",
  };
  return stepMap[status] || "";
};

const submitDecision = async () => {
  if (!selectedApplication.value || !decisionForm.value.decision) return;

  await notification.confirm(
    t("approvalDashboard.confirmDecision", {
      decision: t(`approvalDashboard.decisions.${decisionForm.value.decision}`),
    }),
    t("common.confirm"),
    { type: "warning" },
  );

  submitting.value = true;
  try {
    const stepKey = getStepKey(selectedApplication.value.status);
    await submitUpgradeDecision(selectedApplication.value.id, stepKey, {
      decision: decisionForm.value.decision,
      comments: decisionForm.value.comments || null,
    });

    notification.success(t("approvalDashboard.decisionSuccess"));
    detailDialogVisible.value = false;
    decisionForm.value = { decision: "", comments: "" };
    await refreshAll();
  } catch (error: any) {
    notification.error(error.message || t("approvalDashboard.decisionError"));
  } finally {
    submitting.value = false;
  }
};

const buildDownloadUrl = (fileId: number) => {
  const url = resolveApiUrl(`/api/files/download/${fileId}`);
  return url;
};

const downloadDocument = async (file: any) => {
  if (!file?.id) {
    return;
  }
  const url = buildDownloadUrl(file.id);
  try {
    await openFileInNewTab(url);
  } catch (error: any) {
    notification.error(error?.message || t("approvalDashboard.messages.fileNotFound"));
  }
};

const isUrgent = (dueDate: string): boolean => {
  if (!dueDate) return false;
  const due = new Date(dueDate);
  const now = new Date();
  const diffDays = (due.getTime() - now.getTime()) / (1000 * 60 * 60 * 24);
  return diffDays <= 3 && diffDays >= 0;
};

const getRemainingDays = (dueDate: string): number | null => {
  if (!dueDate) return null;
  const due = new Date(dueDate);
  const now = new Date();
  const diffDays = Math.ceil((due.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
  return diffDays;
};

const formatDate = (dateStr: string): string => {
  if (!dateStr) return "-";
  return new Date(dateStr).toLocaleDateString();
};

const formatDateTime = (dateStr: string): string => {
  if (!dateStr) return "-";
  return new Date(dateStr).toLocaleString();
};

const formatPercentage = (value: number): string => {
  return `${Math.round(value)}%`;
};

const getStatusType = (status: string): "success" | "warning" | "danger" | "info" => {
  if (status === "approved") return "success";
  if (status.startsWith("pending")) return "warning";
  if (status === "rejected" || status === "returned") return "danger";
  return "info";
};

const getCompletenessColor = (percentage: number): string => {
  if (percentage >= 100) return "#67c23a";
  if (percentage >= 80) return "#e6a23c";
  return "#f56c6c";
};

let searchTimeout: number | null = null;
const debounceSearch = () => {
  if (searchTimeout) clearTimeout(searchTimeout);
  searchTimeout = window.setTimeout(() => {
    loadApplications();
  }, 500);
};

const startPolling = () => {
  pollingInterval = window.setInterval(() => {
    refreshAll();
  }, 30000); // Poll every 30 seconds
};

const stopPolling = () => {
  if (pollingInterval) {
    clearInterval(pollingInterval);
    pollingInterval = null;
  }
};

onMounted(() => {
  refreshAll();
  startPolling();
});

onUnmounted(() => {
  stopPolling();
  if (searchTimeout) clearTimeout(searchTimeout);
});




</script>

<style scoped>
.approval-dashboard-container {
  padding: 20px;
  max-width: 1400px;
  margin: 0 auto;
}

.header-card {
  margin-bottom: 20px;
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-content h2 {
  margin: 0 0 8px 0;
  font-size: 24px;
  color: #303133;
}

.subtitle {
  margin: 0;
  color: #909399;
  font-size: 14px;
}

.stats-card {
  margin-bottom: 20px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 24px;
}

.stat-item {
  text-align: center;
  padding: 16px;
  background: #f5f7fa;
  border-radius: 8px;
}

.stat-value {
  font-size: 32px;
  font-weight: bold;
  color: #409eff;
  margin-bottom: 8px;
}

.stat-value.urgent {
  color: #f56c6c;
}

.stat-label {
  color: #606266;
  font-size: 14px;
}

.filter-card {
  margin-bottom: 20px;
}

.filter-form {
  margin-bottom: 0;
}

.applications-card {
  min-height: 400px;
  margin-bottom: 20px;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.empty-state {
  padding: 60px 0;
}

.applications-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.application-item {
  border: 1px solid #dcdfe6;
  border-radius: 8px;
  padding: 20px;
  transition: all 0.3s;
  background: #fff;
}

.application-item:hover {
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.application-item.urgent {
  border-color: #f56c6c;
  background: #fef0f0;
}

.app-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 16px;
}

.app-info {
  flex: 1;
}

.app-title {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}

.supplier-name {
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.app-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  color: #606266;
  font-size: 14px;
}

.app-meta span {
  display: flex;
  align-items: center;
  gap: 4px;
}

.remaining-days {
  color: #f56c6c;
  font-weight: 600;
}

.app-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.progress-section {
  padding: 16px;
  background: #f5f7fa;
  border-radius: 6px;
}

.progress-label {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
  font-size: 14px;
  color: #606266;
}

.completeness {
  font-weight: 600;
  color: #409eff;
}

.documents-preview {
  padding: 16px;
  background: #f5f7fa;
  border-radius: 6px;
}

.section-title {
  font-weight: 600;
  margin-bottom: 12px;
  color: #303133;
}

.document-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.doc-tag {
  margin: 0;
}

.detail-content {
  max-height: 70vh;
  overflow-y: auto;
}

.documents-section,
.approval-history-section,
.decision-section {
  margin-top: 24px;
}

.documents-section h3,
.approval-history-section h3,
.decision-section h3 {
  margin: 0 0 16px 0;
  font-size: 16px;
  color: #303133;
}

.timeline-content {
  padding: 8px 0;
}

.timeline-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}

.step-name {
  font-weight: 600;
  color: #303133;
}

.timeline-body {
  color: #606266;
  font-size: 14px;
}

.reviewer {
  margin-bottom: 4px;
}

.comments {
  padding: 8px;
  background: #f5f7fa;
  border-radius: 4px;
  margin-top: 8px;
}

.text-danger {
  color: #f56c6c;
}

@media (max-width: 768px) {
  .approval-dashboard-container {
    padding: 12px;
  }

  .app-header {
    flex-direction: column;
    gap: 12px;
  }

  .app-actions {
    width: 100%;
  }

  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
