<template>
  <div class="approval-view">
    <PageHeader :title="t('approvalQueue.title')" :subtitle="t('approvalQueue.subtitle')" />

    <div class="content-wrapper">
      <div class="approval-stack">
        <!-- 待审批列表 -->
        <el-card class="approval-card" shadow="never">
        <template #header>
          <div class="card-header">
            <span>待我审批 ({{ applications.length }})</span>
            <el-button type="primary" size="small" @click="refresh">
              <el-icon><Refresh /></el-icon>
              {{ t("common.refresh") }}
            </el-button>
          </div>
        </template>

        <el-table v-loading="loading" :data="applications" stripe>
          <el-table-column prop="id" label="申请编号" width="100" />

          <el-table-column label="供应商信息" width="250">
            <template #default="{ row }">
              <div class="supplier-info">
                <div class="supplier-name">{{ row.companyName }}</div>
                <div class="supplier-code" v-if="row.supplierCode">
                  代码: {{ row.supplierCode }}
                </div>
              </div>
            </template>
          </el-table-column>

          <el-table-column label="状态" width="140">
            <template #default="{ row }">
              <el-tag :type="getStatusType(row.status)" size="small">
                {{ getStatusText(row.status) }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column label="联系方式" width="200">
            <template #default="{ row }">
              {{ row.contactEmail }}
            </template>
          </el-table-column>

          <el-table-column label="负责采购员" width="180">
            <template #default="{ row }">
              {{ row.procurementEmail || "-" }}
            </template>
          </el-table-column>

          <el-table-column prop="createdAt" label="提交时间" width="180">
            <template #default="{ row }">
              {{ formatDateTime(row.createdAt) }}
            </template>
          </el-table-column>

          <el-table-column label="操作" width="200" fixed="right">
            <template #default="{ row }">
              <el-button type="primary" size="small" @click="openDetails(row)">
                {{ t("approvalQueue.actions.viewDetails") }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <div v-if="applications.length === 0 && !loading" class="empty-state">
          <el-empty :description="t('approvalQueue.noSuppliers')" />
        </div>
      </el-card>

        <!-- Approved list -->
        <el-card class="approval-card" shadow="never">
          <template #header>
            <div class="card-header">
              <span>已审批 ({{ approvedApplications.length }})</span>
              <el-button type="primary" size="small" @click="refreshApproved">
                <el-icon><Refresh /></el-icon>
                {{ t("common.refresh") }}
              </el-button>
            </div>
          </template>

          <el-table v-loading="approvedLoading" :data="approvedApplications" stripe>
            <el-table-column prop="id" label="申请编号" width="100" />

            <el-table-column label="供应商信息" width="250">
              <template #default="{ row }">
                <div class="supplier-info">
                  <div class="supplier-name">{{ row.companyName }}</div>
                  <div class="supplier-code" v-if="row.supplierCode">
                    代码: {{ row.supplierCode }}
                  </div>
                </div>
              </template>
            </el-table-column>

            <el-table-column label="状态" width="140">
              <template #default="{ row }">
                <el-tag :type="getStatusType(row.status)" size="small">
                  {{ getStatusText(row.status) }}
                </el-tag>
              </template>
            </el-table-column>

            <el-table-column label="联系方式" width="200">
              <template #default="{ row }">
                {{ row.contactEmail }}
              </template>
            </el-table-column>

            <el-table-column label="负责采购员" width="180">
              <template #default="{ row }">
                {{ row.procurementEmail || "-" }}
              </template>
            </el-table-column>

            <el-table-column prop="createdAt" label="提交时间" width="180">
              <template #default="{ row }">
                {{ formatDateTime(row.createdAt) }}
              </template>
            </el-table-column>

            <el-table-column label="操作" width="200" fixed="right">
              <template #default="{ row }">
                <el-button type="primary" size="small" @click="openDetails(row, 'approved')">
                  {{ t("approvalQueue.actions.viewDetails") }}
                </el-button>
              </template>
            </el-table-column>
          </el-table>

          <div v-if="approvedApplications.length === 0 && !approvedLoading" class="empty-state">
            <el-empty :description="t('approvalQueue.noSuppliers')" />
          </div>
        </el-card>
      </div>
    </div>

    <!-- Application Detail Drawer -->
    <el-drawer
      v-if="drawerVisible"
      v-model="drawerVisible"
      :title="selectedApplication?.companyName || t('approvalQueue.drawer.title')"
      size="900px"
      destroy-on-close
    >
      <div v-if="detailLoading">
        <el-skeleton :rows="8" animated />
      </div>
      <div v-else-if="!applicationDetail">
        <el-empty :description="t('approvalQueue.errors.noStatus')" />
      </div>
      <div v-else class="drawer-content">
        <!-- 供应商信息概览 -->
        <el-alert type="info" :closable="false" style="margin-bottom: 20px">
          <template #title>
            <div style="font-size: 15px; font-weight: 600">
              {{ applicationDetail.companyName }}
            </div>
          </template>
          <div style="margin-top: 8px; font-size: 13px">
            申请编号: {{ applicationDetail.id }} | 提交时间:
            {{ formatDateTime(applicationDetail.createdAt) }}
          </div>
        </el-alert>

        <!-- 基本信息 -->
        <el-divider content-position="left">基本信息</el-divider>
        <el-descriptions :column="2" border>
          <el-descriptions-item label="申请编号">{{ applicationDetail.id }}</el-descriptions-item>
          <el-descriptions-item label="状态">
            <el-tag :type="getStatusType(applicationDetail.status)" size="small">
              {{ getStatusText(applicationDetail.status) }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="供应商代码" v-if="applicationDetail.supplierCode">
            {{ applicationDetail.supplierCode }}
          </el-descriptions-item>
          <el-descriptions-item label="最后更新">
            {{ formatDateTime(applicationDetail.updatedAt) }}
          </el-descriptions-item>
        </el-descriptions>

        <!-- 公司信息 -->
        <el-divider content-position="left">公司信息</el-divider>
        <el-descriptions :column="2" border>
          <el-descriptions-item label="公司名称（中文）">
            {{ applicationDetail.companyName }}
          </el-descriptions-item>
          <el-descriptions-item label="公司名称（英文）" v-if="applicationDetail.companyNameEnglish">
            {{ applicationDetail.companyNameEnglish }}
          </el-descriptions-item>
          <el-descriptions-item label="英文名称">
            {{ applicationDetail.englishName || "-" }}
          </el-descriptions-item>
          <el-descriptions-item label="供应商分类">
            {{ formatSupplierClassification(applicationDetail.supplierClassification) }}
          </el-descriptions-item>
          <el-descriptions-item label="公司类型" v-if="applicationDetail.companyType">
            {{ applicationDetail.companyType }}
          </el-descriptions-item>
          <el-descriptions-item label="营业执照号">
            {{ applicationDetail.businessRegistrationNumber }}
          </el-descriptions-item>
          <el-descriptions-item label="注册地址" :span="2">
            {{ applicationDetail.registeredOffice }}
          </el-descriptions-item>
          <el-descriptions-item label="经营地址" :span="2">
            {{ applicationDetail.businessAddress }}
          </el-descriptions-item>
          <el-descriptions-item label="公司电话" v-if="applicationDetail.companyPhone">
            {{ applicationDetail.companyPhone }}
          </el-descriptions-item>
          <el-descriptions-item label="公司传真" v-if="applicationDetail.companyFax">
            {{ applicationDetail.companyFax }}
          </el-descriptions-item>
        </el-descriptions>

        <!-- 联系人信息 -->
        <el-divider content-position="left">联系人信息</el-divider>
        <el-descriptions :column="2" border>
          <el-descriptions-item label="联系人">
            {{ applicationDetail.contactName }}
          </el-descriptions-item>
          <el-descriptions-item label="联系邮箱">
            {{ applicationDetail.contactEmail }}
          </el-descriptions-item>
          <el-descriptions-item label="联系电话">
            {{ applicationDetail.contactPhone }}
          </el-descriptions-item>
          <el-descriptions-item label="财务联系人" v-if="applicationDetail.financeContactName">
            {{ applicationDetail.financeContactName }}
          </el-descriptions-item>
          <el-descriptions-item label="财务联系邮箱" v-if="applicationDetail.financeContactEmail">
            {{ applicationDetail.financeContactEmail }}
          </el-descriptions-item>
          <el-descriptions-item label="财务联系电话" v-if="applicationDetail.financeContactPhone">
            {{ applicationDetail.financeContactPhone }}
          </el-descriptions-item>
          <el-descriptions-item label="采购联系邮箱" v-if="applicationDetail.procurementEmail">
            {{ applicationDetail.procurementEmail }}
          </el-descriptions-item>
        </el-descriptions>

        <!-- 业务信息 -->
        <el-divider content-position="left">业务信息</el-divider>
        <el-descriptions :column="2" border>
          <el-descriptions-item label="业务性质" v-if="applicationDetail.businessNature">
            {{ applicationDetail.businessNature }}
          </el-descriptions-item>
          <el-descriptions-item label="结算货币">
            {{ applicationDetail.operatingCurrency }}
          </el-descriptions-item>
          <el-descriptions-item label="交货地点">
            {{ applicationDetail.deliveryLocation }}
          </el-descriptions-item>
          <el-descriptions-item label="Ship Code" v-if="applicationDetail.shipCode">
            {{ applicationDetail.shipCode }}
          </el-descriptions-item>
          <el-descriptions-item label="产品类型" :span="2" v-if="applicationDetail.productTypes">
            {{ applicationDetail.productTypes }}
          </el-descriptions-item>
          <el-descriptions-item label="发票类型" v-if="applicationDetail.invoiceType">
            {{ applicationDetail.invoiceType }}
          </el-descriptions-item>
          <el-descriptions-item label="付款条款" v-if="applicationDetail.paymentTermsDays">
            {{ applicationDetail.paymentTermsDays }} 天
          </el-descriptions-item>
          <el-descriptions-item label="付款方式" :span="2" v-if="applicationDetail.paymentMethods">
            {{ applicationDetail.paymentMethods }}
          </el-descriptions-item>
        </el-descriptions>

        <!-- 银行信息 -->
        <el-divider content-position="left">银行信息</el-divider>
        <el-descriptions :column="2" border>
          <el-descriptions-item label="银行名称">
            {{ applicationDetail.bankName }}
          </el-descriptions-item>
          <el-descriptions-item label="银行地址">
            {{ applicationDetail.bankAddress }}
          </el-descriptions-item>
          <el-descriptions-item label="银行账号" :span="2">
            {{ applicationDetail.bankAccountNumber }}
          </el-descriptions-item>
          <el-descriptions-item label="SWIFT Code" :span="2" v-if="applicationDetail.swiftCode">
            {{ applicationDetail.swiftCode }}
          </el-descriptions-item>
        </el-descriptions>

        <!-- 提交资料 -->
        <el-divider content-position="left">提交资料</el-divider>
        <div class="document-grid">
          <el-card class="document-card" shadow="hover">
            <template #header>
              <div class="document-header">
                <span>营业执照</span>
                <el-tag v-if="applicationDetail.businessLicenseFilePath" type="success" size="small">
                  已上传
                </el-tag>
                <el-tag v-else type="info" size="small">暂无</el-tag>
              </div>
            </template>
            <template v-if="applicationDetail.businessLicenseFilePath">
              <div class="document-meta">
                <div class="document-name">
                  {{ applicationDetail.businessLicenseFileName || "business-license" }}
                </div>
                <div class="document-size" v-if="applicationDetail.businessLicenseFileSize">
                  大小：{{ formatFileSize(applicationDetail.businessLicenseFileSize) }}
                </div>
              </div>
              <div class="document-actions">
                <el-button type="primary" link @click="openDocument(applicationDetail.businessLicenseFilePath)">
                  查看 / 下载
                </el-button>
              </div>
            </template>
            <p v-else class="document-empty">供应商未上传营业执照。</p>
          </el-card>

          <el-card class="document-card" shadow="hover">
            <template #header>
              <div class="document-header">
                <span>银行开户资料</span>
                <el-tag v-if="applicationDetail.bankAccountFilePath" type="success" size="small">
                  已上传
                </el-tag>
                <el-tag v-else type="info" size="small">暂无</el-tag>
              </div>
            </template>
            <template v-if="applicationDetail.bankAccountFilePath">
              <div class="document-meta">
                <div class="document-name">
                  {{ applicationDetail.bankAccountFileName || "bank-account-document" }}
                </div>
                <div class="document-size" v-if="applicationDetail.bankAccountFileSize">
                  大小：{{ formatFileSize(applicationDetail.bankAccountFileSize) }}
                </div>
              </div>
              <div class="document-actions">
                <el-button type="primary" link @click="openDocument(applicationDetail.bankAccountFilePath)">
                  查看 / 下载
                </el-button>
              </div>
            </template>
            <p v-else class="document-empty">供应商未上传银行开户资料。</p>
          </el-card>
        </div>

        <!-- 备注 -->
        <div v-if="applicationDetail.notes">
          <el-divider content-position="left">备注</el-divider>
          <el-alert type="info" :closable="false">
            <div style="white-space: pre-wrap; line-height: 1.6">{{ applicationDetail.notes }}</div>
          </el-alert>
        </div>

        <!-- 绑定 VENDOR CODE（仅财务会计可见） -->
        <div v-if="canBindCode">
          <el-divider content-position="left">绑定 VENDOR CODE</el-divider>
          <el-alert type="warning" :closable="false" show-icon style="margin-bottom: 16px">
            审批流程已完成，请为此供应商绑定 VENDOR CODE
          </el-alert>
          <el-button type="primary" :icon="Link" @click="openBindCodeDialog">
            绑定 VENDOR CODE
          </el-button>
        </div>

        <!-- 审批决定 -->
        <template v-if="!canBindCode">
          <el-divider content-position="left">审批意见与备注</el-divider>
          <el-alert
            v-if="approvalHistoryError"
            type="warning"
            :closable="false"
            show-icon
            style="margin-bottom: 12px"
          >
            审批意见加载失败，请稍后重试。
          </el-alert>
          <el-empty v-else-if="approvalHistory.length === 0" description="暂无审批记录" />
          <el-timeline v-else class="approval-history">
            <el-timeline-item
              v-for="item in approvalHistory"
              :key="approvalHistoryKey(item)"
              :timestamp="formatDateTime(item.occurredAt)"
              :type="getHistoryDecisionTagType(item)"
            >
              <div class="approval-history-entry">
                <div class="history-header">
                  <span class="history-step">{{ formatHistoryStep(item) }}</span>
                  <el-tag
                    v-if="getHistoryDecisionLabel(item)"
                    :type="getHistoryDecisionTagType(item)"
                    size="small"
                  >
                    {{ getHistoryDecisionLabel(item) }}
                  </el-tag>
                </div>
                <div class="history-meta">
                  <span>审批人: {{ item.approver || "-" }}</span>
                </div>
                <div v-if="item.comments" class="history-comment">
                  {{ item.comments }}
                </div>
              </div>
            </el-timeline-item>
          </el-timeline>

          <el-divider content-position="left">您的审批决定</el-divider>
          <el-form label-width="100px">
            <el-form-item label="审批决定" required>
              <el-radio-group v-model="approvalDecision">
                <el-radio value="approved">
                  <el-icon color="#67c23a"><CircleCheck /></el-icon>
                  批准
                </el-radio>
                <el-radio value="rejected">
                  <el-icon color="#f56c6c"><CircleClose /></el-icon>
                  拒绝
                </el-radio>
                <el-radio value="requestInfo">
                  <el-icon color="#e6a23c"><Warning /></el-icon>
                  退回补充资料
                </el-radio>
              </el-radio-group>
            </el-form-item>

            <el-form-item label="审批意见">
              <el-input
                v-model="commentDialog.value"
                type="textarea"
                :rows="4"
                :placeholder="getCommentPlaceholder()"
              />
            </el-form-item>
          </el-form>
        </template>
      </div>

      <template #footer>
        <div class="drawer-footer">
          <el-button @click="drawerVisible = false">取消</el-button>
          <el-button
            type="primary"
            :loading="commentDialog.submitting"
            @click="submitDecision"
            :disabled="detailReadOnly || !selectedApplication || canBindCode"
          >
            提交审批
          </el-button>
        </div>
      </template>
    </el-drawer>

    <!-- 绑定 VENDOR CODE 对话框 -->
    <el-dialog
      v-model="bindCodeDialogVisible"
      title="绑定 VENDOR CODE"
      width="500px"
      :close-on-click-modal="false"
    >
      <el-alert type="info" :closable="false" show-icon style="margin-bottom: 20px">
        请为供应商 <strong>{{ selectedApplication?.companyName }}</strong> 绑定 VENDOR CODE
      </el-alert>
      <el-form label-width="120px">
        <el-form-item label="VENDOR CODE" required>
          <el-input
            v-model="bindCodeInput"
            placeholder="请输入 7 位供应商代码"
            maxlength="7"
            show-word-limit
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="bindCodeDialogVisible = false">取消</el-button>
        <el-button
          type="primary"
          :loading="bindingCode"
          :disabled="!bindCodeInput.trim()"
          @click="handleBindCode"
        >
          确认绑定
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, reactive, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { useAuthStore } from "@/stores/auth";
import { Refresh, CircleCheck, CircleClose, Warning, Link } from "@element-plus/icons-vue";
import PageHeader from "@/components/layout/PageHeader.vue";
import {
  fetchPendingRegistrations,
  fetchApprovedRegistrations,
  bindSupplierCode,
  type ApprovalApplicationListItem,
  type SupplierRegistrationApplicationDetail,
  type SupplierRegistrationStatusHistoryEntry,
} from "@/api/public";
import { getStatusText, getStatusType } from "@/utils/helpers";
import { resolveApiUrl } from "@/utils/apiBaseUrl";
import { openFileInNewTab } from "@/utils/fileDownload";
import { useNotification } from "@/composables";

const notification = useNotification();
const authStore = useAuthStore();

defineOptions({ name: "ApprovalQueueView" });

const { t } = useI18n();

// State
const loading = ref(false);
const applications = ref<ApprovalApplicationListItem[]>([]);
const approvedLoading = ref(false);
const approvedApplications = ref<ApprovalApplicationListItem[]>([]);
const drawerVisible = ref(false);
const selectedApplication = ref<ApprovalApplicationListItem | null>(null);
const applicationDetail = ref<SupplierRegistrationApplicationDetail | null>(null);
const approvalHistory = ref<SupplierRegistrationStatusHistoryEntry[]>([]);
const approvalHistoryError = ref(false);
const detailLoading = ref(false);
const detailReadOnly = ref(false);
const approvalDecision = ref<"approved" | "rejected" | "requestInfo">("approved");

const commentDialog = reactive({
  value: "",
  submitting: false,
});

// 绑定 VENDOR CODE 相关状态
const bindCodeDialogVisible = ref(false);
const bindCodeInput = ref("");
const bindingCode = ref(false);

// 计算属性
const isFinanceAccountant = computed(() => authStore.user?.role === "finance_accountant");
const canBindCode = computed(() =>
  isFinanceAccountant.value &&
  selectedApplication.value?.status === "pending_code_binding"
);

// Methods
const refresh = async () => {
  loading.value = true;
  try {
    applications.value = await fetchPendingRegistrations();
  } catch (error) {
    console.error("Failed to fetch pending registrations", error);
    notification.error(t("errors.general"));
  } finally {
    loading.value = false;
  }
};

const refreshApproved = async () => {
  approvedLoading.value = true;
  try {
    approvedApplications.value = await fetchApprovedRegistrations();
  } catch (error) {
    console.error("Failed to fetch approved registrations", error);
    notification.error(t("errors.general"));
  } finally {
    approvedLoading.value = false;
  }
};

const openDetails = async (app: ApprovalApplicationListItem, source: "pending" | "approved" = "pending") => {
  selectedApplication.value = app;
  drawerVisible.value = true;
  detailReadOnly.value = source === "approved";
  approvalDecision.value = "approved";
  commentDialog.value = "";
  await loadApplicationDetail(app.id);

  console.log("[DEBUG openDetails] app.status:", app.status);
  console.log("[DEBUG openDetails] isFinanceAccountant:", isFinanceAccountant.value);
  console.log("[DEBUG openDetails] role:", authStore.user?.role);

  // 如果是财务会计且状态是 pending_code_binding，自动弹出绑定对话框
  if (isFinanceAccountant.value && app.status?.toLowerCase() === "pending_code_binding") {
    console.log("[DEBUG openDetails] 准备弹出绑定对话框");
    bindCodeInput.value = "";
    bindCodeDialogVisible.value = true;
  }
};

const loadApplicationDetail = async (id: number) => {
  detailLoading.value = true;
  approvalHistory.value = [];
  approvalHistoryError.value = false;
  try {
    const { fetchRegistrationDetail, fetchSupplierRegistrationStatus } = await import("@/api/public");
    const [detailResult, statusResult] = await Promise.allSettled([
      fetchRegistrationDetail(id),
      fetchSupplierRegistrationStatus(id),
    ]);

    if (detailResult.status === "fulfilled") {
      applicationDetail.value = detailResult.value;
    } else {
      throw detailResult.reason;
    }

    if (statusResult.status === "fulfilled") {
      approvalHistory.value = (statusResult.value.history || []).filter(
        (item) => item.type === "approval",
      );
    } else {
      approvalHistoryError.value = true;
      console.error("Failed to fetch approval history", statusResult.reason);
    }
  } catch (error) {
    applicationDetail.value = null;
    console.error("Failed to fetch application detail", error);
    notification.error(t("approvalQueue.errors.loadStatus"));
    approvalHistoryError.value = true;
  } finally {
    detailLoading.value = false;
  }
};

const getCommentPlaceholder = () => {
  if (approvalDecision.value === "approved") {
    return "请填写审批意见（可选）";
  } else if (approvalDecision.value === "rejected") {
    return "请说明驳回原因（必填）";
  } else {
    return "请填写需要供应商补充的内容（必填）";
  }
};

const submitDecision = async () => {
  if (!selectedApplication.value) {
    return;
  }

  // Validate required comment for reject and requestInfo
  if (
    (approvalDecision.value === "rejected" || approvalDecision.value === "requestInfo") &&
    !commentDialog.value.trim()
  ) {
    notification.warning(t("approvalQueue.validation.commentRequired"));
    return;
  }

  try {
    commentDialog.submitting = true;
    const id = selectedApplication.value.id;
    const isApprove = approvalDecision.value === "approved";
    let nextStatus: string | undefined;

    if (isApprove) {
      const { approveRegistration } = await import("@/api/public");
      const result = await approveRegistration(id, {
        comment: commentDialog.value.trim() || undefined,
      });
      notification.success(result.message || t("approvalQueue.notifications.approved"));
      nextStatus = result.nextStatus;
    } else if (approvalDecision.value === "rejected") {
      const { rejectRegistration } = await import("@/api/public");
      const result = await rejectRegistration(id, { reason: commentDialog.value });
      notification.success(result.message || t("approvalQueue.notifications.rejected"));
    } else {
      const { requestRegistrationInfo } = await import("@/api/public");
      const result = await requestRegistrationInfo(id, { message: commentDialog.value });
      notification.success(result.message || t("approvalQueue.notifications.requestedInfo"));
    }

    // 财务会计批准后，检查下一个状态是否为 pending_code_binding
    if (isFinanceAccountant.value && isApprove && nextStatus?.toLowerCase() === "pending_code_binding") {
      drawerVisible.value = false;
      await refresh();

      // 重新设置 selectedApplication 以便绑定对话框显示正确的供应商信息
      const refreshedApp = applications.value.find(a => a.id === id);
      if (refreshedApp) {
        selectedApplication.value = refreshedApp;
      }

      // 打开绑定CODE对话框
      bindCodeInput.value = "";
      bindCodeDialogVisible.value = true;
    } else {
      drawerVisible.value = false;
      selectedApplication.value = null;
      await Promise.all([refresh(), refreshApproved()]);
    }
  } catch (error) {
    console.error("Failed to submit decision", error);
    notification.error(error instanceof Error ? error.message : t("errors.general"));
  } finally {
    commentDialog.submitting = false;
  }
};

const formatDateTime = (dateStr: string | null | undefined) => {
  if (!dateStr) return "-";
  const date = new Date(dateStr);
  return date.toLocaleString();
};

const formatSupplierClassification = (value: string | null | undefined) => {
  if (!value) {
    return "-";
  }
  if (value === "DM") {
    return `DM (${t("rfq.materialType.dm")})`;
  }
  if (value === "IDM") {
    return `IDM (${t("rfq.materialType.idm")})`;
  }
  return value;
};

const formatFileSize = (size: number | null | undefined) => {
  if (!size || Number.isNaN(size)) {
    return "-";
  }
  if (size < 1024) {
    return `${size} B`;
  }
  if (size < 1024 * 1024) {
    return `${(size / 1024).toFixed(1)} KB`;
  }
  return `${(size / (1024 * 1024)).toFixed(1)} MB`;
};

const formatHistoryStep = (item: SupplierRegistrationStatusHistoryEntry) => {
  if (!item.step) return "审批";
  const key = `registrationStatus.history.steps.${item.step}`;
  const translated = t(key);
  return translated === key ? item.step : translated;
};

const getHistoryDecisionLabel = (item: SupplierRegistrationStatusHistoryEntry) => {
  const raw = (item.result || item.status || "").toLowerCase();
  if (!raw) return "";
  if (raw === "approved" || raw === "completed") return "批准";
  if (raw === "rejected" || raw === "refused") return "拒绝";
  if (raw.includes("request") || raw.includes("return")) return "退回补充";
  return raw;
};

const getHistoryDecisionTagType = (
  item: SupplierRegistrationStatusHistoryEntry,
): "success" | "warning" | "danger" | "info" => {
  const raw = (item.result || item.status || "").toLowerCase();
  if (raw === "approved" || raw === "completed") return "success";
  if (raw === "rejected" || raw === "refused") return "danger";
  if (raw.includes("request") || raw.includes("return")) return "warning";
  return "info";
};

const approvalHistoryKey = (item: SupplierRegistrationStatusHistoryEntry) => {
  return `${item.step ?? "approval"}-${item.occurredAt ?? "time"}-${item.approver ?? "unknown"}`;
};

const buildDocumentUrl = (relativePath: string | null | undefined) => {
  if (!relativePath) {
    return null;
  }
  const cleanPath = relativePath.startsWith("/") ? relativePath : `/${relativePath}`;
  const url = resolveApiUrl(`/uploads${cleanPath}`);
  return url;
};

const openDocument = async (relativePath: string | null | undefined) => {
  const url = buildDocumentUrl(relativePath);
  if (!url) {
    notification.warning("未找到该文件，请联系供应商重新上传。");
    return;
  }
  try {
    await openFileInNewTab(url);
  } catch (error: any) {
    notification.error(error?.message || "文件打开失败，请稍后重试。");
  }
};

// 打开绑定 VENDOR CODE 对话框
const openBindCodeDialog = () => {
  if (!selectedApplication.value) return;
  bindCodeInput.value = "";
  bindCodeDialogVisible.value = true;
};

// 绑定 VENDOR CODE
const handleBindCode = async () => {
  if (!selectedApplication.value || !bindCodeInput.value.trim()) {
    notification.warning("请输入 VENDOR CODE");
    return;
  }

  bindingCode.value = true;
  try {
    const result = await bindSupplierCode(selectedApplication.value.id, bindCodeInput.value.trim());
    notification.success(result.message || "VENDOR CODE 绑定成功");
    bindCodeDialogVisible.value = false;
    await refresh();
    await refreshApproved();
  } catch (error: any) {
    notification.error(error.message || "绑定失败");
  } finally {
    bindingCode.value = false;
  }
};

// Lifecycle
onMounted(() => {
  refresh();
  refreshApproved();
});




</script>

<style scoped>
.approval-view {
  padding: 24px;
  background-color: #f5f7fa;
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
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.supplier-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.supplier-name {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.supplier-code {
  font-size: 12px;
  color: #909399;
}

.empty-state {
  padding: 40px 0;
}

.drawer-content {
  padding: 0 4px 20px;
}

.document-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 16px;
  margin-bottom: 20px;
}

.document-card {
  min-height: 160px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.document-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-weight: 600;
}

.document-meta {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 13px;
  color: #606266;
}

.document-name {
  font-weight: 600;
  color: #303133;
}

.document-actions {
  display: flex;
  justify-content: flex-start;
  margin-top: 8px;
}

.document-empty {
  color: #909399;
  font-size: 13px;
  margin: 0;
}

.approval-history {
  margin-bottom: 12px;
}

.approval-history-entry {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.history-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.history-step {
  font-weight: 600;
  color: #303133;
}

.history-meta {
  font-size: 12px;
  color: #909399;
}

.history-comment {
  background: #f5f7fa;
  padding: 8px 12px;
  border-radius: 6px;
  color: #606266;
  font-size: 13px;
  white-space: pre-wrap;
}

.drawer-footer {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
  padding: 12px 16px;
  border-top: 1px solid #e4e7ed;
}

:deep(.el-descriptions__label) {
  font-weight: 600;
  background-color: #fafafa;
}

:deep(.el-divider__text) {
  font-size: 15px;
  font-weight: 600;
  color: #303133;
}

:deep(.el-radio) {
  margin-right: 24px;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
</style>

