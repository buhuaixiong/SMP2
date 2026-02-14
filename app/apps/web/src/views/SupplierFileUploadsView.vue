<template>
  <div class="file-uploads-page">
    <PageHeader
      title="文件上传管理"
      subtitle="上传文件并查看审批状态"
    >
      <template #actions>
        <div class="header-actions">
          <el-button @click="fetchUploads">
            <el-icon><Refresh /></el-icon>
            刷新
          </el-button>
          <el-button type="primary" @click="showUploadDialog = true">
            <el-icon><Upload /></el-icon>
            上传文件
          </el-button>
        </div>
      </template>
    </PageHeader>

    <!-- Statistics Cards -->
    <div class="stats-cards">
      <el-card class="stat-card">
        <div class="stat-content">
          <div class="stat-icon" style="background: #e6f7ff">
            <el-icon :size="24" color="#1890ff"><Document /></el-icon>
          </div>
          <div class="stat-info">
            <p class="stat-label">总上传数</p>
            <p class="stat-value">{{ totalCount }}</p>
          </div>
        </div>
      </el-card>

      <el-card class="stat-card">
        <div class="stat-content">
          <div class="stat-icon" style="background: #fff7e6">
            <el-icon :size="24" color="#fa8c16"><Clock /></el-icon>
          </div>
          <div class="stat-info">
            <p class="stat-label">审批中</p>
            <p class="stat-value">{{ pendingCount }}</p>
          </div>
        </div>
      </el-card>

      <el-card class="stat-card">
        <div class="stat-content">
          <div class="stat-icon" style="background: #f6ffed">
            <el-icon :size="24" color="#52c41a"><CircleCheck /></el-icon>
          </div>
          <div class="stat-info">
            <p class="stat-label">已批准</p>
            <p class="stat-value">{{ approvedCount }}</p>
          </div>
        </div>
      </el-card>

      <el-card class="stat-card">
        <div class="stat-content">
          <div class="stat-icon" style="background: #fff1f0">
            <el-icon :size="24" color="#ff4d4f"><CircleClose /></el-icon>
          </div>
          <div class="stat-info">
            <p class="stat-label">已拒绝</p>
            <p class="stat-value">{{ rejectedCount }}</p>
          </div>
        </div>
      </el-card>
    </div>

    <!-- File Upload List -->
    <el-card>
      <template #header>
        <span>上传记录</span>
      </template>

      <el-table v-loading="loading" :data="uploads" stripe style="width: 100%">
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
        <el-table-column prop="status" label="状态" width="130">
          <template #default="{ row }">
            <el-tag :type="getStatusTagType(row.status)">
              {{ getStatusLabel(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="currentStep" label="当前步骤" width="130">
          <template #default="{ row }">
            {{ getCurrentStepLabel(row.currentStep, row.status) }}
          </template>
        </el-table-column>
        <el-table-column prop="validTo" label="过期日期" width="120">
          <template #default="{ row }">
            <span v-if="row.validTo" :style="getExpiryStyle(row.validTo, row.status)">
              {{ row.validTo }}
            </span>
            <span v-else style="color: #909399">—</span>
          </template>
        </el-table-column>
        <el-table-column prop="submittedAt" label="提交时间" width="160">
          <template #default="{ row }">
            {{ formatTime(row.submittedAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="120" fixed="right">
          <template #default="{ row }">
            <el-button type="primary" link @click="viewDetails(row.id)"> 查看详情 </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- Upload Dialog -->
    <SupplierFileUploadDialog
      v-if="showUploadDialog"
      v-model:visible="showUploadDialog"
      :supplier-id="supplierId"
      @success="handleUploadSuccess"
    />

    <!-- Details Dialog -->
    <el-dialog
      v-model="showDetailsDialog"
      title="文件上传详情"
      width="900px"
      :destroy-on-close="true"
    >
      <div v-if="selectedUploadDetails" v-loading="loadingDetails">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="文件ID">
            {{ selectedUploadDetails.id }}
          </el-descriptions-item>
          <el-descriptions-item label="文件名">
            {{ selectedUploadDetails.fileName || "未命名" }}
          </el-descriptions-item>
          <el-descriptions-item label="文件说明" :span="2">
            {{ selectedUploadDetails.fileDescription || "—" }}
          </el-descriptions-item>
          <el-descriptions-item label="生效日期">
            {{ selectedUploadDetails.validFrom || "—" }}
          </el-descriptions-item>
          <el-descriptions-item label="过期日期">
            <span
              v-if="selectedUploadDetails.validTo"
              :style="getExpiryStyle(selectedUploadDetails.validTo, selectedUploadDetails.status)"
            >
              {{ selectedUploadDetails.validTo }}
            </span>
            <span v-else>—</span>
          </el-descriptions-item>
          <el-descriptions-item label="当前状态">
            <el-tag :type="getStatusTagType(selectedUploadDetails.status)">
              {{ getStatusLabel(selectedUploadDetails.status) }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="提交时间">
            {{ formatTime(selectedUploadDetails.submittedAt) }}
          </el-descriptions-item>
        </el-descriptions>

        <!-- File Preview Section -->
        <el-divider content-position="left">
          <span style="display: flex; align-items: center; gap: 8px;">
            文件预览
            <el-button type="primary" size="small" @click="downloadFile">
              <el-icon><Download /></el-icon>
              下载文件
            </el-button>
          </span>
        </el-divider>

        <div class="file-preview-container">
          <!-- PDF Preview -->
          <div v-if="isPDF(selectedUploadDetails.fileName)" class="pdf-preview">
            <iframe
              :src="getFilePreviewUrl(selectedUploadDetails.id)"
              style="width: 100%; height: 600px; border: 1px solid #dcdfe6; border-radius: 4px;"
              frameborder="0"
            ></iframe>
          </div>

          <!-- Image Preview -->
          <div v-else-if="isImage(selectedUploadDetails.fileName)" class="image-preview">
            <el-image
              :src="getFilePreviewUrl(selectedUploadDetails.id)"
              :preview-src-list="[getFilePreviewUrl(selectedUploadDetails.id)]"
              fit="contain"
              style="max-width: 100%; max-height: 600px;"
            >
              <template #error>
                <div class="image-error">
                  <el-icon :size="48"><Picture /></el-icon>
                  <p>图片加载失败</p>
                </div>
              </template>
            </el-image>
          </div>

          <!-- Other File Types -->
          <div v-else class="no-preview">
            <el-icon :size="64" color="#909399"><Document /></el-icon>
            <p style="margin-top: 16px; color: #606266;">
              此文件类型不支持在线预览，请下载后查看
            </p>
            <el-button type="primary" @click="downloadFile" style="margin-top: 12px;">
              <el-icon><Download /></el-icon>
              下载文件
            </el-button>
          </div>
        </div>

        <el-divider content-position="left">审批进度</el-divider>

        <FileUploadTimeline
          :workflow="selectedUploadDetails.workflow"
          :current-step="selectedUploadDetails.currentStep"
          :status="selectedUploadDetails.status"
          :approval-history="selectedUploadDetails.approvalHistory"
        />
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted, defineAsyncComponent } from "vue";

import PageHeader from "@/components/layout/PageHeader.vue";
import {
  Upload,
  Refresh,
  Document,
  Clock,
  CircleCheck,
  CircleClose,
  Download,
  Picture,
} from "@element-plus/icons-vue";
import dayjs from "dayjs";
import { useAuthStore } from "@/stores/auth";
import { getSupplierFileUploads, getFileUploadDetails } from "@/api/fileUploads";
import type { FileUpload, FileUploadDetails } from "@/api/fileUploads";
import { resolveApiUrl } from "@/utils/apiBaseUrl";
import { openFileInNewTab } from "@/utils/fileDownload";


import { useNotification } from "@/composables";
const notification = useNotification();

const SupplierFileUploadDialog = defineAsyncComponent(
  () => import("@/components/SupplierFileUploadDialog.vue"),
);
const FileUploadTimeline = defineAsyncComponent(
  () => import("@/components/FileUploadTimeline.vue"),
);

const authStore = useAuthStore();
const loading = ref(false);
const loadingDetails = ref(false);
const showUploadDialog = ref(false);
const showDetailsDialog = ref(false);

const uploads = ref<FileUpload[]>([]);
const selectedUploadDetails = ref<FileUploadDetails | null>(null);

const supplierId = computed(() => {
  return authStore.user?.supplierId || 0;
});

const totalCount = computed(() => uploads.value.length);

const pendingCount = computed(() => {
  return uploads.value.filter((u) => u.status.startsWith("pending_")).length;
});

const approvedCount = computed(() => {
  return uploads.value.filter((u) => u.status === "approved").length;
});

const rejectedCount = computed(() => {
  return uploads.value.filter((u) => u.status === "rejected").length;
});

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

const getCurrentStepLabel = (currentStep: string, status: string): string => {
  if (status === "approved") return "已完成";
  if (status === "rejected") return "已终止";

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

const getExpiryStyle = (validTo: string, status: string): string => {
  if (status !== "approved" || !validTo) {
    return "";
  }

  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const expiryDate = new Date(validTo);
  expiryDate.setHours(0, 0, 0, 0);

  const diffDays = Math.ceil((expiryDate.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));

  if (diffDays < 0) {
    // Expired
    return "color: #f56c6c; font-weight: bold;";
  } else if (diffDays <= 7) {
    // Expiring soon (within 7 days)
    return "color: #e6a23c; font-weight: bold;";
  } else if (diffDays <= 30) {
    // Expiring within 30 days
    return "color: #409eff;";
  }

  return "";
};

const fetchUploads = async () => {
  if (!supplierId.value) {
    notification.warning("无法获取供应商信息");
    return;
  }

  loading.value = true;
  try {
    uploads.value = await getSupplierFileUploads(supplierId.value);
  } catch (error: any) {
    console.error("Failed to fetch file uploads:", error);
    notification.error(error?.message || "获取上传记录失败");
  } finally {
    loading.value = false;
  }
};

const viewDetails = async (uploadId: number) => {
  loadingDetails.value = true;
  showDetailsDialog.value = true;
  try {
    selectedUploadDetails.value = await getFileUploadDetails(uploadId);
  } catch (error: any) {
    console.error("Failed to fetch upload details:", error);
    notification.error(error?.message || "获取详情失败");
    showDetailsDialog.value = false;
  } finally {
    loadingDetails.value = false;
  }
};

const handleUploadSuccess = () => {
  showUploadDialog.value = false;
  fetchUploads();
};

// File preview and download functions
const getFilePreviewUrl = (fileId: number): string => {
  const url = resolveApiUrl(`/api/files/download/${fileId}`);
  return url;
};

const isPDF = (fileName: string | null | undefined): boolean => {
  if (!fileName) return false;
  return fileName.toLowerCase().endsWith(".pdf");
};

const isImage = (fileName: string | null | undefined): boolean => {
  if (!fileName) return false;
  const ext = fileName.toLowerCase();
  return ext.endsWith(".jpg") || ext.endsWith(".jpeg") || ext.endsWith(".png");
};

const downloadFile = async () => {
  if (!selectedUploadDetails.value) return;
  const url = getFilePreviewUrl(selectedUploadDetails.value.id);
  try {
    await openFileInNewTab(url);
  } catch (error: any) {
    notification.error(error?.message || "文件下载失败");
  }
};

onMounted(() => {
  fetchUploads();
});




</script>

<style scoped>
.file-uploads-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px;
}




.header-actions {
  display: flex;
  gap: 12px;
}

.stats-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
}

.stat-card {
  cursor: default;
}

.stat-content {
  display: flex;
  align-items: center;
  gap: 16px;
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.stat-info {
  flex: 1;
}

.stat-label {
  margin: 0 0 4px 0;
  font-size: 14px;
  color: #909399;
}

.stat-value {
  margin: 0;
  font-size: 24px;
  font-weight: 600;
  color: #303133;
}

.file-preview-container {
  margin: 16px 0;
  min-height: 200px;
}

.no-preview {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  background: #f5f7fa;
  border-radius: 4px;
}

.image-preview {
  display: flex;
  justify-content: center;
  align-items: center;
  background: #f5f7fa;
  border-radius: 4px;
  padding: 20px;
}

.image-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #909399;
  padding: 40px;
}

.image-error p {
  margin-top: 12px;
  font-size: 14px;
}
</style>
