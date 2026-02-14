<template>
  <div class="upgrade-page" v-loading="loading">
    <PageHeader
      :title="t('upgradeManagement.title')"
      :subtitle="t('upgradeManagement.subtitle')"
    >
      <template #actions>
        <div class="header-actions">
          <el-tag v-if="statusLabel" :type="statusTagType">{{ statusLabel }}</el-tag>
          <el-button size="small" @click="refreshStatus">
            {{ t("upgradeManagement.refresh") }}
          </el-button>
          <el-button
            v-if="canSubmit"
            size="small"
            type="primary"
            :loading="submitting"
            :disabled="isSubmitDisabled"
            @click="submitUpgrade"
          >
            {{ t("upgradeManagement.submitApplication") }}
          </el-button>
        </div>
      </template>
    </PageHeader>

    <!-- Visual Progress Steps - 5 Steps -->
    <el-card v-if="showProgressSteps" class="progress-card" shadow="never">
      <el-steps :active="currentStepIndex" finish-status="success" align-center>
        <el-step
          :title="t('upgradeManagement.steps.submitApplication')"
          :description="t('upgradeManagement.steps.uploadFiles')"
        >
          <template #icon>
            <el-icon><Upload /></el-icon>
          </template>
        </el-step>
        <el-step
          :title="t('upgradeManagement.steps.purchaserReview')"
          :description="getStepDescription('procurement_review')"
        >
          <template #icon>
            <el-icon><User /></el-icon>
          </template>
        </el-step>
        <el-step
          :title="t('upgradeManagement.steps.qualityReview')"
          :description="getStepDescription('quality_review')"
        >
          <template #icon>
            <el-icon><Check /></el-icon>
          </template>
        </el-step>
        <el-step
          :title="t('upgradeManagement.steps.procurementManagerReview')"
          :description="getStepDescription('procurement_manager_review')"
        >
          <template #icon>
            <el-icon><UserFilled /></el-icon>
          </template>
        </el-step>
        <el-step
          :title="t('upgradeManagement.steps.procurementDirectorReview')"
          :description="getStepDescription('procurement_director_review')"
        >
          <template #icon>
            <el-icon><Star /></el-icon>
          </template>
        </el-step>
        <el-step
          :title="t('upgradeManagement.steps.financeDirectorReview')"
          :description="getStepDescription('finance_director_review')"
        >
          <template #icon>
            <el-icon><Money /></el-icon>
          </template>
        </el-step>
        <el-step
          :title="t('upgradeManagement.steps.complete')"
          :description="t('upgradeManagement.steps.upgradeToFormal')"
        >
          <template #icon>
            <el-icon><CircleCheck /></el-icon>
          </template>
        </el-step>
      </el-steps>
    </el-card>

    <!-- Deadline Alert -->
    <el-alert
      v-if="deadlineInfo"
      :title="deadlineInfo.title"
      :type="deadlineInfo.type"
      :closable="false"
      show-icon
      class="deadline-alert"
    >
      <template #default>
        <p>{{ deadlineInfo.message }}</p>
        <p v-if="state.application?.dueAt" class="deadline-date">
          {{ t("upgradeManagement.deadline.expectedCompletion") }}:
          {{ formatDate(state.application.dueAt) }}
        </p>
      </template>
    </el-alert>

    <el-card class="info-card" shadow="never">
      <template #header>
        <span>{{ t("upgradeManagement.requirements.title") }}</span>
      </template>
      <el-alert
        :title="t('upgradeManagement.requirements.description')"
        type="info"
        :closable="false"
        show-icon
      />
      <el-alert
        v-if="fallbackWarning"
        :title="fallbackWarning"
        type="warning"
        :closable="false"
        show-icon
        class="mt-12"
      />
    </el-card>

    <el-card class="upload-card" shadow="never">
      <template #header>
        <div class="card-header-content">
          <span>{{ t("upgradeManagement.upload.title") }}</span>
          <el-progress
            v-if="uploadProgress < 100"
            :percentage="uploadProgress"
            :stroke-width="8"
            :color="uploadProgress === 100 ? '#67c23a' : '#409eff'"
          >
            <template #default="{ percentage }">
              <span class="progress-text">{{ percentage }}%</span>
            </template>
          </el-progress>
          <el-tag v-else type="success">
            <el-icon><CircleCheckFilled /></el-icon>
            {{ t("upgradeManagement.requirements.completed") }}
          </el-tag>
        </div>
      </template>

      <el-alert
        v-if="!hasSupplierContext"
        type="warning"
        show-icon
        :closable="false"
        :title="t('upgradeManagement.upload.noSupplier')"
        class="mb-20"
      />

      <!-- Document Types List -->
      <div class="document-types-section">
        <h3>{{ t("upgradeManagement.documentTypes.title") }}</h3>
        <el-table :data="requirementRows" border stripe>
          <el-table-column type="index" width="50" />
          <el-table-column
            :label="t('upgradeManagement.documentTypes.name')"
            prop="name"
            min-width="200"
          >
            <template #default="{ row }">
              <div class="doc-name-cell">
                <span>{{ row.name }}</span>
                <el-tag v-if="row.required" type="danger" size="small" class="ml-2">
                  {{ t("upgradeManagement.upload.required") }}
                </el-tag>
              </div>
            </template>
          </el-table-column>
          <el-table-column
            :label="t('upgradeManagement.documentTypes.description')"
            prop="description"
            min-width="250"
          />
          <el-table-column
            :label="t('upgradeManagement.documentTypes.status')"
            width="120"
            align="center"
          >
            <template #default="{ row }">
              <el-tag v-if="row.fulfilled" type="success" size="small">
                <el-icon><Check /></el-icon> {{ t("upgradeManagement.upload.uploaded") }}
              </el-tag>
              <el-tag v-else type="info" size="small">
                {{ t("upgradeManagement.upload.notUploaded") }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column
            :label="t('upgradeManagement.documentTypes.template')"
            width="150"
            align="center"
          >
            <template #default="{ row }">
              <el-button
                v-if="row.templateUrl"
                size="small"
                type="primary"
                link
                @click="openTemplate(row.templateUrl)"
              >
                <el-icon><Download /></el-icon> {{ t("common.download") }}
              </el-button>
              <span v-else class="text-muted">{{ t("upgradeManagement.upload.noTemplate") }}</span>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- Upload Section -->
      <div v-if="canEdit" class="upload-section">
        <h3>{{ t("upgradeManagement.upload.title") }}</h3>
        <el-form :model="uploadForm" label-width="140px">
          <el-form-item :label="t('upgradeManagement.upload.documentType')" required>
            <el-select
              v-model="uploadForm.documentType"
              :placeholder="t('upgradeManagement.upload.selectDocumentType')"
              style="width: 100%"
              @change="onDocumentTypeChange"
            >
              <el-option
                v-for="row in requirementRows"
                :key="row.code"
                :label="row.name"
                :value="row.code"
              >
                <div class="select-option-content">
                  <span>{{ row.name }}</span>
                  <el-tag v-if="row.required" type="danger" size="small">
                    {{ t("upgradeManagement.upload.required") }}
                  </el-tag>
                  <el-tag v-if="row.fulfilled" type="success" size="small">
                    {{ t("upgradeManagement.upload.uploaded") }}
                  </el-tag>
                </div>
              </el-option>
            </el-select>
          </el-form-item>

          <el-form-item :label="t('upgradeManagement.upload.validFrom')" required>
            <el-date-picker
              v-model="uploadForm.validFrom"
              type="date"
              :placeholder="t('upgradeManagement.upload.selectDate')"
              :disabled-date="disabledDate"
              style="width: 100%"
            />
          </el-form-item>

          <el-form-item :label="t('upgradeManagement.upload.validTo')" required>
            <el-date-picker
              v-model="uploadForm.validTo"
              type="date"
              :placeholder="t('upgradeManagement.upload.selectDate')"
              :disabled-date="(time: Date) => disabledValidToDate(time, uploadForm)"
              style="width: 100%"
            />
          </el-form-item>

          <el-form-item :label="t('upgradeManagement.upload.file')" required>
            <el-upload
              ref="uploadRef"
              class="upload-demo"
              :auto-upload="false"
              :limit="1"
              :on-change="handleFileChange"
              :before-upload="beforeUpload"
              accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png"
            >
              <template #trigger>
                <el-button type="primary">
                  <el-icon><Upload /></el-icon>
                  {{ t("upgradeManagement.upload.chooseFile") }}
                </el-button>
              </template>
              <template #tip>
                <div class="el-upload__tip">
                  {{ t("upgradeManagement.upload.fileTypeHint") }}
                </div>
              </template>
            </el-upload>
          </el-form-item>

          <el-form-item>
            <el-button
              type="primary"
              :loading="uploading"
              :disabled="!canUploadFile"
              @click="handleUploadFile"
            >
              <el-icon><UploadFilled /></el-icon>
              {{ t("upgradeManagement.upload.uploadFile") }}
            </el-button>
            <el-button @click="resetUploadForm">
              {{ t("common.reset") }}
            </el-button>
          </el-form-item>
        </el-form>
      </div>

      <!-- Uploaded Documents Table -->
      <div v-if="uploadedDocuments.length" class="uploaded-documents-section">
        <h3>{{ t("upgradeManagement.upload.uploadedDocuments") }}</h3>
        <el-table :data="uploadedDocuments" border stripe>
          <el-table-column type="index" width="50" />
          <el-table-column
            :label="t('upgradeManagement.documentTypes.type')"
            prop="requirementName"
            min-width="180"
          />
          <el-table-column :label="t('upgradeManagement.documentTypes.fileName')" min-width="200">
            <template #default="{ row }">
              <el-link
                v-if="row.file"
                type="primary"
                @click="openPreview(buildDownloadUrl(row.file.id))"
              >
                {{ row.file.originalName }}
              </el-link>
            </template>
          </el-table-column>
          <el-table-column :label="t('upgradeManagement.documentTypes.validityPeriod')" width="220">
            <template #default="{ row }">
              <span v-if="row.file && row.file.validFrom && row.file.validTo">
                {{ formatDate(row.file.validFrom) }} ~ {{ formatDate(row.file.validTo) }}
              </span>
            </template>
          </el-table-column>
          <el-table-column :label="t('upgradeManagement.documentTypes.uploadedAt')" width="180">
            <template #default="{ row }">
              {{ row.file ? formatDate(row.file.uploadTime) : "-" }}
            </template>
          </el-table-column>
          <el-table-column :label="t('common.actions')" width="120" align="center">
            <template #default="{ row }">
              <el-button
                v-if="canEdit && row.file"
                size="small"
                type="danger"
                link
                @click="handleDeleteDocument(row)"
              >
                {{ t("common.delete") }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- Old Document Cards Grid - REMOVED, KEEPING STRUCTURE FOR REFERENCE IF NEEDED -->
      <div v-if="false" class="document-cards-grid">
        <div
          v-for="row in requirementRows"
          :key="row.code"
          class="document-card"
          :class="{
            fulfilled: row.fulfilled,
            required: row.required,
            pending: hasPending(row),
          }"
        >
          <!-- Card Header -->
          <div class="doc-card-header">
            <div class="doc-title">
              <h4>{{ row.name }}</h4>
              <div class="doc-badges">
                <el-tag v-if="row.required" type="danger" size="small" effect="dark">{{
                  t("upgradeManagement.upload.required")
                }}</el-tag>
                <el-tag v-if="row.fulfilled" type="success" size="small">
                  <el-icon><Check /></el-icon> {{ t("upgradeManagement.upload.uploaded") }}
                </el-tag>
              </div>
            </div>
          </div>

          <!-- Card Body -->
          <div class="doc-card-body">
            <p class="doc-description">{{ row.description }}</p>

            <!-- Validity Period -->
            <div v-if="canEdit" class="validity-dates">
              <div class="date-input-group">
                <label class="date-label required-label"
                  >Valid From: <span class="required-asterisk">*</span></label
                >
                <el-date-picker
                  v-model="row.validFrom"
                  type="date"
                  placeholder="Select valid from date (required)"
                  size="small"
                  :disabled-date="disabledDate"
                  style="width: 100%"
                />
              </div>
              <div class="date-input-group">
                <label class="date-label required-label"
                  >Valid To: <span class="required-asterisk">*</span></label
                >
                <el-date-picker
                  v-model="row.validTo"
                  type="date"
                  placeholder="Select valid to date (required)"
                  size="small"
                  :disabled-date="(time: Date) => disabledValidToDate(time, row)"
                  style="width: 100%"
                />
              </div>
            </div>

            <!-- File Preview -->
            <div v-if="row.fileId" class="file-preview-container">
              <div class="file-preview-box" @click="openPreview(row.fileUrl)">
                <div v-if="row.fileUrl && isImageFile(row.fileName)" class="image-preview">
                  <img :src="row.fileUrl || ''" :alt="row.fileName || row.name" />
                </div>
                <div v-else class="file-icon-preview">
                  <el-icon :size="48" color="#409eff">
                    <Document v-if="isPdfFile(row.fileName)" />
                    <Folder v-else />
                  </el-icon>
                </div>
                <div class="file-name-overlay">
                  <el-icon><View /></el-icon>
                  <span>{{ truncateFileName(row.fileName) }}</span>
                </div>
              </div>
            </div>
            <div v-else class="no-file-placeholder">
              <el-icon :size="32" color="#dcdfe6"><DocumentAdd /></el-icon>
              <span>{{ t("upgradeManagement.upload.notUploaded") }}</span>
            </div>
          </div>

          <!-- Card Footer -->
          <div class="doc-card-footer">
            <el-button
              v-if="row.templateUrl"
              size="small"
              text
              @click="openTemplate(row.templateUrl)"
            >
              <el-icon><Download /></el-icon> {{ t("upgradeManagement.upload.downloadTemplate") }}
            </el-button>

            <div class="action-buttons">
              <el-button
                v-if="canEdit && hasPending(row)"
                size="small"
                type="warning"
                plain
                @click="removePending(row)"
              >
                {{ t("upgradeManagement.upload.revoke") }}
              </el-button>

              <!-- Show tooltip with disabled button when not editable -->
              <el-tooltip v-if="!canEdit" :content="canEditTooltip" placement="top">
                <el-button size="small" disabled>
                  <el-icon><Upload /></el-icon>
                  {{
                    row.fileId
                      ? t("upgradeManagement.upload.reupload")
                      : t("upgradeManagement.upload.uploadFile")
                  }}
                </el-button>
              </el-tooltip>

              <!-- Show upload button when editable -->
              <template v-else>
                <!-- Show disabled button with tooltip when dates are not filled -->
                <el-tooltip
                  v-if="!row.validFrom || !row.validTo"
                  :content="t('upgradeManagement.upload.dateRequiredTooltip')"
                  placement="top"
                >
                  <el-button size="small" :type="row.fileId ? 'default' : 'primary'" disabled>
                    <el-icon><Upload /></el-icon>
                    {{
                      row.fileId
                        ? t("upgradeManagement.upload.reupload")
                        : t("upgradeManagement.upload.uploadFile")
                    }}
                  </el-button>
                </el-tooltip>

                <!-- Show enabled upload button when dates are filled -->
                <el-upload
                  v-else
                  class="inline-upload"
                  :show-file-list="false"
                  :before-upload="beforeUpload"
                  :http-request="createUploadHandler(row)"
                  accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png"
                >
                  <el-button size="small" :type="row.fileId ? 'default' : 'primary'">
                    <el-icon><Upload /></el-icon>
                    {{
                      row.fileId
                        ? t("upgradeManagement.upload.reupload")
                        : t("upgradeManagement.upload.uploadFile")
                    }}
                  </el-button>
                </el-upload>
              </template>
            </div>
          </div>
        </div>
      </div>
    </el-card>

    <el-card v-if="timelineItems.length" class="timeline-card" shadow="never">
      <template #header>
        <span>{{ t("upgradeManagement.workflow.title") }}</span>
      </template>
      <el-timeline>
        <el-timeline-item
          v-for="item in timelineItems"
          :key="item.key"
          :timestamp="item.time"
          :type="item.tagType"
          placement="top"
        >
          <div class="timeline-item">
            <div class="timeline-title">
              <!-- Add source badge with custom colors -->
              <el-tag
                v-if="item.source === 'file_upload'"
                size="small"
                class="source-tag file-upload-tag"
              >
                <el-icon style="vertical-align: middle; margin-right: 4px"><Document /></el-icon>
                文件审批
              </el-tag>
              <el-tag
                v-else-if="item.source === 'upgrade_application'"
                size="small"
                class="source-tag upgrade-app-tag"
              >
                <el-icon style="vertical-align: middle; margin-right: 4px"><Upload /></el-icon>
                升级申请
              </el-tag>
              <span>{{ item.title }}</span>
              <el-tag size="small" :type="item.tagType">{{ item.statusLabel }}</el-tag>
            </div>
            <p>{{ t("upgradeManagement.workflow.handler") }}: {{ item.actor || "—" }}</p>
            <p v-if="item.remark">
              {{ t("upgradeManagement.workflow.remark") }}: {{ item.remark }}
            </p>
          </div>
        </el-timeline-item>
      </el-timeline>
    </el-card>

    <!-- Note: Dialog removed - using direct download instead of preview -->
  </div>
</template>

<script setup lang="ts">




import { computed, reactive, ref, onMounted, onUnmounted, nextTick } from "vue";
import { useRoute } from "vue-router";
import { useI18n } from "vue-i18n";
import dayjs from "dayjs";
import relativeTime from "dayjs/plugin/relativeTime";
import "dayjs/locale/zh-cn";

import type { UploadRawFile, UploadRequestOptions } from "element-plus";
import PageHeader from "@/components/layout/PageHeader.vue";
import {
  Upload,
  User,
  Check,
  UserFilled,
  Money,
  Star,
  CircleCheck,
  CircleCheckFilled,
  UploadFilled,
  Document,
  DocumentAdd,
  Folder,
  Download,
  View,
} from "@element-plus/icons-vue";
import { useAuthStore } from "@/stores/auth";
import {
  fetchUpgradeRequirements,
  fetchUpgradeStatus,
  submitUpgradeApplication,
  deleteUpgradeDocument,
  uploadUpgradeFile,
} from "@/api/upgrade";
import type {
  UpgradeApplicationDocument,
  UpgradeApplicationInfo,
  UpgradeRequirement,
  UpgradeReview,
  UpgradeStatus,
  FileApprovalReview,
} from "@/types";
import { resolveApiUrl } from "@/utils/apiBaseUrl";
import { openFileInNewTab } from "@/utils/fileDownload";
import { useNotification } from "@/composables";
const notification = useNotification();

dayjs.extend(relativeTime);
dayjs.locale("zh-cn");

const { t } = useI18n();

interface RequirementRow {
  code: string;
  name: string;
  description: string;
  required: boolean;
  fulfilled: boolean;
  templateUrl: string | null;
  fileId: number | null;
  fileName: string | null;
  fileUrl: string | null;
  validFrom?: Date | null;
  validTo?: Date | null;
}

interface TimelineItem {
  key: string;
  source?: string;  // Approval source: 'file_upload' | 'upgrade_application'
  title: string;
  statusLabel: string;
  actor: string;
  time: string;
  remark: string;
  tagType: string;
}

function getStatusLabel(status: string): string {
  const labelKey = `upgradeManagement.status.${status}`;
  const label = t(labelKey);
  return label !== labelKey ? label : status;
}

const STATUS_TAG_TYPES: Record<string, string> = {
  not_submitted: "info",
  under_review: "warning",
  pending_procurement_review: "warning",
  pending_quality_review: "warning",
  pending_procurement_manager_review: "warning",
  pending_procurement_director_review: "warning",
  pending_finance_director_review: "warning",
  returned: "danger",
  approved: "success",
  rejected: "danger",
};

function getStepStatusLabel(status: string): string {
  const labelKey = `upgradeManagement.steps.${status}`;
  const label = t(labelKey);
  return label !== labelKey ? label : status;
}

const STEP_STATUS_TAGS: Record<string, string> = {
  pending: "warning",
  approved: "success",
  rejected: "danger",
  cancelled: "info",
};

const SUBMITTABLE_STATUSES = new Set(["not_submitted", "returned", "rejected"]);
const EDITABLE_STATUSES = new Set([
  "not_submitted",
  "returned",
  "rejected",
  "pending_procurement_review",
  "pending_quality_review",
  "pending_procurement_manager_review",
  "pending_procurement_director_review",
  "pending_finance_director_review",
]);

const route = useRoute();
const authStore = useAuthStore();

const loading = ref(false);
const submitting = ref(false);
const fallbackWarning = ref<string | null>(null);
const isMounted = ref(false);

const ensureDomReady = async () => {
  if (!isMounted.value || typeof document === "undefined" || typeof window === "undefined") {
    return false;
  }
  await nextTick();
  return true;
};

const pendingUploads = reactive<Record<string, { fileId: number; fileName: string }>>({});

const state = reactive({
  supplierId: null as number | null,
  status: "not_submitted",
  requirements: [] as UpgradeRequirement[],
  documents: [] as UpgradeApplicationDocument[],
  workflow: null as UpgradeStatus["workflow"],
  reviews: [] as UpgradeReview[],
  fileApprovals: [] as FileApprovalReview[],
  application: null as UpgradeApplicationInfo | null,
});

// Upload form state
const uploading = ref(false);
const uploadRef = ref();
const uploadForm = reactive({
  documentType: "",
  validFrom: null as Date | null,
  validTo: null as Date | null,
  file: null as File | null,
});

const uploadedDocuments = computed(() => state.documents);

const canUploadFile = computed(() => {
  return uploadForm.documentType && uploadForm.validFrom && uploadForm.validTo && uploadForm.file;
});

const statusLabel = computed(() => getStatusLabel(state.status));
const statusTagType = computed(() => STATUS_TAG_TYPES[state.status] || "info");
const hasSupplierContext = computed(() => !!state.supplierId);
const canSubmit = computed(
  () => hasSupplierContext.value && SUBMITTABLE_STATUSES.has(state.status),
);
const canEdit = computed(() => hasSupplierContext.value && EDITABLE_STATUSES.has(state.status));

const canEditTooltip = computed(() => {
  if (!hasSupplierContext.value) {
    return t("upgradeManagement.upload.noSupplier");
  }
  if (!EDITABLE_STATUSES.has(state.status)) {
    return t("upgradeManagement.upload.statusNotEditable");
  }
  return "";
});

const requirementRows = computed<RequirementRow[]>(() => {
  return state.requirements.map((req) => {
    const doc = state.documents.find((item) => item.requirementCode === req.code) ?? null;
    const docFile = doc?.file ?? null;
    const pending = pendingUploads[req.code];
    const fileId = pending?.fileId ?? docFile?.id ?? req.documentId ?? null;
    const fileName = pending?.fileName ?? docFile?.originalName ?? "";

    const validFrom = docFile?.validFrom ? new Date(docFile.validFrom) : null;
    const validTo = docFile?.validTo ? new Date(docFile.validTo) : null;

    return {
      code: req.code,
      name: req.name,
      description: req.description,
      required: !!req.required,
      fulfilled: pending ? true : !!(req.fulfilled || doc || req.documentId),
      templateUrl: req.template?.downloadUrl ? resolveApiUrl(req.template.downloadUrl) : null,
      fileId,
      fileName,
      fileUrl: fileId ? buildDownloadUrl(fileId) : null,
      validFrom,
      validTo,
    };
  });
});

const isSubmitDisabled = computed(() => {
  if (!canSubmit.value) {
    return true;
  }
  // 只要上传了任意1个文档就允许提交
  return requirementRows.value.every((row) => !row.fileId);
});

const timelineItems = computed<TimelineItem[]>(() => {
  const items: TimelineItem[] = [];

  if (state.application) {
    items.push({
      key: "application",
      source: "upgrade_application",
      title: t("upgradeManagement.workflow.supplierSubmitted"),
      statusLabel:
        getStatusLabel(state.application.status) || t("upgradeManagement.workflow.submitted"),
      actor: state.application.submittedBy || "",
      time: formatDate(state.application.submittedAt),
      remark: state.application.rejectionReason || "",
      tagType: STATUS_TAG_TYPES[state.application.status] || "info",
    });
  }

  // Add file upload approval records
  state.fileApprovals.forEach((approval) => {
    const fileName = approval.fileName ? ` - ${approval.fileName}` : "";
    items.push({
      key: `file_approval_${approval.id}`,
      source: approval.source,
      title: `${approval.stepName}${fileName}`,
      statusLabel: approval.decision === "approved" ? t("common.approved") : t("common.rejected"),
      actor: approval.decidedByName || "",
      time: formatDate(approval.decidedAt),
      remark: approval.comments || "",
      tagType: approval.decision === "approved" ? "success" : "danger",
    });
  });

  // Add upgrade application review records
  const reviewsByStep = new Map(state.reviews.map((review) => [review.stepKey, review]));

  state.workflow?.steps?.forEach((step) => {
    const review = reviewsByStep.get(step.key);
    const decision = review?.decision || step.status || "pending";
    items.push({
      key: step.key,
      source: review?.source || "upgrade_application",
      title: step.name,
      statusLabel: getStepStatusLabel(decision),
      actor: review?.decidedByName || step.permission || "",
      time: formatDate(review?.decidedAt || step.completedAt),
      remark: review?.comments || step.notes || "",
      tagType: STEP_STATUS_TAGS[decision] || STEP_STATUS_TAGS.pending,
    });
  });

  // Sort all items by timestamp (ISO format sorts correctly with Date)
  return items.sort((a, b) => {
    const timeA = new Date(a.time || 0).getTime();
    const timeB = new Date(b.time || 0).getTime();
    return timeA - timeB;
  });
});

const showProgressSteps = computed(() => {
  return state.status !== "not_submitted";
});

const currentStepIndex = computed(() => {
  const statusMap: Record<string, number> = {
    not_submitted: 0,
    pending_procurement_review: 1,
    pending_quality_review: 2,
    pending_procurement_manager_review: 3,
    pending_procurement_director_review: 4,
    pending_finance_director_review: 5,
    approved: 6,
    returned: 0,
    rejected: 0,
  };
  return statusMap[state.status] ?? 0;
});

interface DeadlineInfo {
  title: string;
  message: string;
  type: "success" | "info" | "warning" | "error";
}

const deadlineInfo = computed<DeadlineInfo | null>(() => {
  if (
    !state.application?.dueAt ||
    state.status === "not_submitted" ||
    state.status === "approved"
  ) {
    return null;
  }

  const now = dayjs();
  const dueDate = dayjs(state.application.dueAt);
  const hoursRemaining = dueDate.diff(now, "hour");
  const daysRemaining = Math.ceil(hoursRemaining / 24);

  if (hoursRemaining < 0) {
    return {
      title: t("upgradeManagement.deadline.overtime"),
      message: t("upgradeManagement.deadline.overtimeMessage", { days: Math.abs(daysRemaining) }),
      type: "error",
    };
  }

  if (daysRemaining < 1) {
    return {
      title: t("upgradeManagement.deadline.lessThanOneDay"),
      message: t("upgradeManagement.deadline.lessThanOneDayMessage", { hours: hoursRemaining }),
      type: "error",
    };
  }

  if (daysRemaining < 2) {
    return {
      title: t("upgradeManagement.deadline.remaining", { days: daysRemaining }),
      message: t("upgradeManagement.deadline.urgent"),
      type: "warning",
    };
  }

  return {
    title: t("upgradeManagement.deadline.remaining", { days: daysRemaining }),
    message: t("upgradeManagement.deadline.inProgress"),
    type: "info",
  };
});

const uploadProgress = computed(() => {
  const total = requirementRows.value.filter((r) => r.required).length;
  if (total === 0) return 0;
  const uploaded = requirementRows.value.filter((r) => r.required && r.fulfilled).length;
  return Math.round((uploaded / total) * 100);
});

function formatDate(value?: string | null) {
  if (!value) {
    return "";
  }
  return dayjs(value).format("YYYY-MM-DD HH:mm");
}

function buildDownloadUrl(fileId: number) {
  const url = resolveApiUrl(`/api/files/download/${fileId}`);
  return url;
}

function normalizeSupplierId(value: unknown): number | null {
  if (Array.isArray(value)) {
    return normalizeSupplierId(value[0]);
  }
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === "string" && value.trim().length) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
}

async function loadUpgradeStatus() {
  loading.value = true;
  fallbackWarning.value = null;

  const candidate =
    normalizeSupplierId(route.params.id) ??
    normalizeSupplierId(route.query.supplierId) ??
    normalizeSupplierId(authStore.user?.supplierId) ??
    state.supplierId;

  if (!candidate) {
    await loadRequirementDefinitions(t("upgradeManagement.warnings.noSupplierInfo"));
    loading.value = false;
    return;
  }

  state.supplierId = candidate;
  Object.keys(pendingUploads).forEach((key) => Reflect.deleteProperty(pendingUploads, key));

  try {
    const data = await fetchUpgradeStatus(candidate);
    state.status = data.status ?? data.application?.status ?? "not_submitted";
    state.requirements = data.requirements ?? [];
    state.documents = data.documents ?? [];
    state.workflow = data.workflow ?? null;
    state.reviews = data.reviews ?? [];
    state.fileApprovals = data.fileApprovals ?? [];
    state.application = data.application ?? null;
    if (!state.requirements.length) {
      await loadRequirementDefinitions();
    }
  } catch (error) {
    console.error(t("upgradeManagement.warnings.loadStatusFailed"), error);
    await loadRequirementDefinitions(t("upgradeManagement.warnings.loadStatusFailed"));
  } finally {
    loading.value = false;
  }
}

function createUploadHandler(row: RequirementRow) {
  return (options: UploadRequestOptions) => {
    uploadRequirementFile(row, options);
  };
}

async function loadRequirementDefinitions(message?: string) {
  try {
    const defs = await fetchUpgradeRequirements();
    state.status = "not_submitted";
    Object.keys(pendingUploads).forEach((key) => Reflect.deleteProperty(pendingUploads, key));
    state.requirements = defs ?? [];
    state.documents = [];
    state.workflow = null;
    state.reviews = [];
    state.application = null;
    if (message) {
      fallbackWarning.value = message;
    } else if (!state.requirements.length) {
      fallbackWarning.value = t("upgradeManagement.warnings.noRequirements");
    }
  } catch (error) {
    console.error(t("upgradeManagement.warnings.loadRequirementsFailed"), error);
    fallbackWarning.value = message || t("upgradeManagement.warnings.loadRequirementsFailed");
  }
}

async function ensureUpgradeApplication() {
  if (!state.supplierId) {
    notification.error(t("upgradeManagement.warnings.noSupplierInfo"));
    return false;
  }

  if (state.application) {
    return true;
  }

  try {
    await submitUpgradeApplication(state.supplierId);
  } catch (error) {
    console.error(t("upgradeManagement.actions.submitFailed"), error);
  }

  await loadUpgradeStatus();
  if (!state.application) {
    notification.error(t("upgradeManagement.actions.submitFailed"));
    return false;
  }

  return true;
}

async function uploadRequirementFile(row: RequirementRow, options: UploadRequestOptions) {
  if (!state.supplierId) {
    notification.error(t("upgradeManagement.upload.noSupplier"));
    return;
  }

  if (!(await ensureUpgradeApplication())) {
    return;
  }

  if (!row.validFrom || !row.validTo) {
    notification.error({
      message: t("upgradeManagement.upload.dateRequired"),
      duration: 5000,
    });
    return;
  }

  if (new Date(row.validFrom) >= new Date(row.validTo)) {
    notification.error(t("upgradeManagement.upload.dateInvalid"));
    return;
  }

  try {
    const result = await uploadUpgradeFile({
      supplierId: state.supplierId,
      requirementCode: row.code,
      file: options.file as File,
      status: "submitted",
      validFrom: row.validFrom.toISOString(),
      validTo: row.validTo.toISOString(),
    });

    const pendingFileId =
      (result as { fileId?: number }).fileId ??
      (result as UpgradeApplicationDocument).file?.id ??
      result.id;

    pendingUploads[row.code] = {
      fileId: pendingFileId,
      fileName: (options.file as File).name,
    };

    await loadUpgradeStatus();
    notification.success(t("upgradeManagement.upload.uploadSuccess"));
  } catch (error) {
    console.error(t("upgradeManagement.upload.uploadFailed"), error);
    notification.error(t("upgradeManagement.upload.uploadFailed"));
  }
}

function beforeUpload(file: UploadRawFile) {
  const isLt20M = file.size / 1024 / 1024 < 20;
  if (!isLt20M) {
    notification.error(t("upgradeManagement.upload.fileSizeLimit"));
    return false;
  }

  const fileName = file.name.toLowerCase();
  const validExtensions = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png"];
  const hasValidExtension = validExtensions.some((ext) => fileName.endsWith(ext));

  if (!hasValidExtension) {
    notification.error({
      message: t("upgradeManagement.upload.unsupportedFormat", {
        formats: validExtensions.join(", "),
      }),
      duration: 5000,
    });
    return false;
  }

  return true;
}

// New upload form handlers
function onDocumentTypeChange() {
  // Reset validity dates when document type changes
  // uploadForm.validFrom = null;
  // uploadForm.validTo = null;
}

function handleFileChange(file: any) {
  uploadForm.file = file.raw;
}

async function handleUploadFile() {
  if (!canUploadFile.value || !state.supplierId) {
    notification.warning(t("upgradeManagement.upload.pleaseCompleteForm"));
    return;
  }

  try {
    uploading.value = true;

    if (!(await ensureUpgradeApplication())) {
      return;
    }

    const result = await uploadUpgradeFile({
      supplierId: state.supplierId,
      requirementCode: uploadForm.documentType,
      file: uploadForm.file!,
      status: "submitted",
      validFrom: dayjs(uploadForm.validFrom).format("YYYY-MM-DD"),
      validTo: dayjs(uploadForm.validTo).format("YYYY-MM-DD"),
    });

    notification.success(t("upgradeManagement.upload.uploadSuccess"));

    // Refresh status
    await loadUpgradeStatus();

    // Reset form
    resetUploadForm();
  } catch (error: any) {
    console.error("Upload failed:", error);
    notification.error(error.message || t("upgradeManagement.upload.uploadFailed"));
  } finally {
    uploading.value = false;
  }
}

function resetUploadForm() {
  uploadForm.documentType = "";
  uploadForm.validFrom = null;
  uploadForm.validTo = null;
  uploadForm.file = null;
  if (uploadRef.value) {
    uploadRef.value.clearFiles();
  }
}

async function handleDeleteDocument(doc: UpgradeApplicationDocument) {
  await notification.confirm(t("upgradeManagement.upload.confirmDelete"), t("common.confirm"), {
    type: "warning",
  });

  try {
    if (!state.supplierId) {
      notification.error(t("upgradeManagement.upload.noSupplier"));
      return;
    }

    await deleteUpgradeDocument(state.supplierId, doc.id);
    notification.success(t("common.operationSuccess"));
    await loadUpgradeStatus();
  } catch (error: any) {
    notification.error(error.message || t("common.operationFailed"));
  }
}

async function openTemplate(url: string | null) {
  if (!url) {
    notification.warning(t("upgradeManagement.notifications.templateNotProvided"));
    return;
  }

  try {
    let downloadUrl = url;
    if (!url.startsWith("http")) {
      downloadUrl = resolveApiUrl(url);
    }

    const token = localStorage.getItem("token");
    const headers: HeadersInit = {};
    if (token) {
      headers["Authorization"] = `Bearer ${token}`;
    }

    const response = await fetch(downloadUrl, { headers });

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    const blob = await response.blob();

    if (blob.size === 0) {
      throw new Error("Downloaded file is empty");
    }

    let filename = "template";
    const contentDisposition = response.headers.get("Content-Disposition");
    if (contentDisposition) {
      const match = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
      if (match && match[1]) {
        filename = match[1].replace(/['"]/g, "");
      }
    } else {
      const urlParts = downloadUrl.split("/");
      filename = urlParts[urlParts.length - 1];
    }

    if (!(await ensureDomReady())) {
      return;
    }

    const blobUrl = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = blobUrl;
    link.download = decodeURIComponent(filename);
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();

    setTimeout(() => {
      document.body.removeChild(link);
      window.URL.revokeObjectURL(blobUrl);
    }, 100);

    notification.success(t("upgradeManagement.upload.downloadStarted"));
  } catch (error) {
    console.error("[Template Download] Error:", error);
    notification.error(t("upgradeManagement.upload.downloadFailed"));
  }
}

function openPreview(url: string | null) {
  if (!url) {
    notification.warning(t("upgradeManagement.preview.noContent"));
    return;
  }
  // 直接打开新窗口下载文件
  openFileInNewTab(url)
    .then(() => {
      notification.success(t("upgradeManagement.preview.downloadStarted"));
    })
    .catch((error: any) => {
      notification.error(error?.message || t("upgradeManagement.upload.downloadFailed"));
    });
}

function disabledDate(time: Date) {
  return time.getTime() < Date.now() - 86400000;
}

function disabledValidToDate(time: Date, row: { validFrom?: Date | null }) {
  if (row.validFrom) {
    const validFromTime = new Date(row.validFrom).getTime();
    return time.getTime() <= validFromTime;
  }
  return time.getTime() < Date.now() - 86400000;
}

function hasPending(row: RequirementRow) {
  return !!pendingUploads[row.code];
}

function removePending(row: RequirementRow) {
  if (pendingUploads[row.code]) {
    Reflect.deleteProperty(pendingUploads, row.code);
    notification.success(t("upgradeManagement.upload.revoked"));
  }
}

async function submitUpgrade() {
  if (!state.supplierId) {
    notification.error(t("upgradeManagement.warnings.noSupplierInfo"));
    return;
  }

  const uploaded = requirementRows.value.filter((row) => row.fileId);

  // 检查是否有缺失的推荐文档，给出提示但允许继续
  const missing = requirementRows.value.filter((row) => row.required && !row.fileId);
  if (missing.length) {
    const names = missing.map((row) => row.name).join("、");
    notification.info(t('upgradeManagement.actions.missingDocumentsWarning', { names }));
  }

  try {
    await notification.confirm(
      t("upgradeManagement.actions.confirmSubmit"),
      t("upgradeManagement.actions.hint"),
      {
        type: "warning",
        confirmButtonText: t("upgradeManagement.actions.submit"),
        cancelButtonText: t("upgradeManagement.actions.cancel"),
      },
    );
  } catch {
    return;
  }

  submitting.value = true;
  try {
    const documents = uploaded.map((row) => ({ requirementCode: row.code, fileId: row.fileId! }));
    const payload = documents.length ? { documents } : {};

    await submitUpgradeApplication(state.supplierId, payload);
    notification.success(t("upgradeManagement.actions.applicationSubmitted"));
    await loadUpgradeStatus();
  } catch (error) {
    console.error(t("upgradeManagement.actions.submitFailed"), error);
    notification.error(t("upgradeManagement.actions.submitFailed"));
  } finally {
    submitting.value = false;
  }
}

function refreshStatus() {
  loadUpgradeStatus();
}

function getStepDescription(stepKey: string): string {
  const step = state.workflow?.steps?.find((s) => s.key === stepKey);
  if (!step) return "";

  return getStepStatusLabel(step.status);
}

function isImageFile(fileName: string | null): boolean {
  if (!fileName) return false;
  const ext = fileName.toLowerCase().split(".").pop();
  return ["jpg", "jpeg", "png", "gif", "bmp", "webp"].includes(ext || "");
}

function isPdfFile(fileName: string | null): boolean {
  if (!fileName) return false;
  return fileName.toLowerCase().endsWith(".pdf");
}

function truncateFileName(fileName: string | null, maxLength = 30): string {
  if (!fileName) return "";
  if (fileName.length <= maxLength) return fileName;
  const lastDot = fileName.lastIndexOf(".");
  if (lastDot <= 0) {
    const base = fileName.substring(0, Math.max(0, maxLength - 3));
    return `${base}...`;
  }
  const ext = fileName.substring(lastDot + 1);
  const nameWithoutExt = fileName.substring(0, lastDot);
  const maxNameLength = maxLength - ext.length - 1;
  if (maxNameLength <= 3) {
    const base = fileName.substring(0, Math.max(0, maxLength - 3));
    return `${base}...`;
  }
  const truncated = nameWithoutExt.substring(0, maxNameLength - 3) + "...";
  return `${truncated}.${ext}`;
}

let pollInterval: ReturnType<typeof setInterval> | null = null;

function startPolling() {
  if (
    state.status &&
    state.status !== "not_submitted" &&
    state.status !== "approved" &&
    state.status !== "returned"
  ) {
    pollInterval = setInterval(() => {
      loadUpgradeStatus();
    }, 30000);
  }
}

function stopPolling() {
  if (pollInterval) {
    clearInterval(pollInterval);
    pollInterval = null;
  }
}

onMounted(() => {
  isMounted.value = true;
  loadUpgradeStatus().then(() => {
    startPolling();
  });
});

onUnmounted(() => {
  isMounted.value = false;
  stopPolling();
});




</script>

<style scoped>
.upgrade-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px;
}




.header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.progress-card,
.info-card,
.upload-card,
.timeline-card {
  background: #ffffff;
}

.progress-card {
  padding: 20px 0;
}

.deadline-alert {
  margin: 0;
}

.deadline-alert p {
  margin: 4px 0;
}

.deadline-date {
  font-weight: 600;
  color: #303133;
}

.card-header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.progress-text {
  font-size: 14px;
  font-weight: 600;
}

.mb-20 {
  margin-bottom: 20px;
}

.document-cards-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 20px;
  margin-top: 20px;
}

.document-card {
  border: 2px solid #e4e7ed;
  border-radius: 8px;
  overflow: hidden;
  transition: all 0.3s ease;
  background: #ffffff;
  display: flex;
  flex-direction: column;
}

.document-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  transform: translateY(-2px);
}

.document-card.fulfilled {
  border-color: #67c23a;
  background: #f0f9ff;
}

.document-card.required:not(.fulfilled) {
  border-color: #f56c6c;
  background: #fef0f0;
}

.document-card.pending {
  border-color: #e6a23c;
  background: #fdf6ec;
}

.doc-card-header {
  padding: 16px;
  background: linear-gradient(135deg, #f5f7fa 0%, #ffffff 100%);
  border-bottom: 1px solid #e4e7ed;
}

.document-card.fulfilled .doc-card-header {
  background: linear-gradient(135deg, #f0f9ff 0%, #e1f3d8 100%);
}

.document-card.required:not(.fulfilled) .doc-card-header {
  background: linear-gradient(135deg, #fef0f0 0%, #fde2e2 100%);
}

.doc-title {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.doc-title h4 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  line-height: 1.4;
}

.doc-badges {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.doc-card-body {
  padding: 16px;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.doc-description {
  margin: 0;
  font-size: 13px;
  color: #606266;
  line-height: 1.6;
}

.validity-dates {
  display: flex;
  gap: 12px;
  margin: 8px 0;
}

.date-input-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.date-label {
  font-size: 12px;
  color: #606266;
  font-weight: 500;
}

.required-asterisk {
  color: #f56c6c;
  margin-left: 2px;
}

.file-preview-container {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 120px;
}

.file-preview-box {
  position: relative;
  width: 100%;
  height: 120px;
  border-radius: 6px;
  overflow: hidden;
  cursor: pointer;
  transition: all 0.3s ease;
}

.file-preview-box:hover {
  transform: scale(1.02);
}

.file-preview-box:hover .file-name-overlay {
  opacity: 1;
}

.image-preview {
  width: 100%;
  height: 100%;
}

.image-preview img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  border-radius: 6px;
}

.file-icon-preview {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f5f7fa;
  border-radius: 6px;
}

.file-name-overlay {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  background: rgba(0, 0, 0, 0.7);
  color: white;
  padding: 8px 12px;
  font-size: 12px;
  display: flex;
  align-items: center;
  gap: 6px;
  opacity: 0;
  transition: opacity 0.3s ease;
}

.file-name-overlay span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.no-file-placeholder {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 24px;
  background: #fafafa;
  border: 2px dashed #dcdfe6;
  border-radius: 6px;
  color: #909399;
  font-size: 13px;
}

.doc-card-footer {
  padding: 12px 16px;
  background: #fafafa;
  border-top: 1px solid #e4e7ed;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.action-buttons {
  display: flex;
  gap: 8px;
  align-items: center;
}

.inline-upload {
  display: inline-flex;
}

.timeline-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.timeline-title {
  display: flex;
  align-items: center;
  gap: 12px;
  font-weight: 600;
  color: #303133;
}

/* Custom source tag colors */
.source-tag {
  margin-right: 8px !important;
  border: none !important;
  font-weight: 500;
}

/* File upload tag - Blue theme */
.file-upload-tag {
  background-color: #e3f2fd !important;
  color: #1976d2 !important;
}

.file-upload-tag :deep(.el-icon) {
  color: #1976d2;
}

/* Upgrade application tag - Purple theme */
.upgrade-app-tag {
  background-color: #f3e5f5 !important;
  color: #7b1fa2 !important;
}

.upgrade-app-tag :deep(.el-icon) {
  color: #7b1fa2;
}

.preview-frame {
  width: 100%;
  min-height: 480px;
  border: none;
}

.mt-12 {
  margin-top: 12px;
}

@media (max-width: 768px) {
  .document-cards-grid {
    grid-template-columns: 1fr;
  }
}
</style>
