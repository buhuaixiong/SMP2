<template>
  <div class="directory-page">
    <DirectoryHeader
      :search="filters.q"
      :status="filters.status"
      :status-options="statusOptions"
      :needs-attention-active="filters.completionStatus === 'needs_attention'"
      :selected-missing-documents="missingDocumentFilter"
      :document-options="documentRequirementOptions"
      :advanced-count="activeAdvancedCount"
      :can-clear-quick-filters="canClearQuickFilters"
      @update:search="(value) => (filters.q = value)"
      @update:status="(value) => (filters.status = value)"
      @toggle-advanced="toggleAdvancedFilters"
      @toggle-needs-attention="toggleNeedsAttentionFilter"
      @toggle-missing-document="toggleMissingDocumentFilter"
      @clear-quick-filters="clearQuickFilters"
    >
      <template #title>
        <h1 class="view-title">{{ pageTitle }}</h1>
      </template>
      <template v-if="isAdmin" #primary-actions>
        <el-button type="primary" plain @click="navigateToBulkDocumentImport">
          <el-icon><UploadFilled /></el-icon>
          批量上传必备文件
        </el-button>
      </template>
    </DirectoryHeader>

    <ActiveFilterChips
      :chips="activeFilterChips"
      :show-clear="activeFilterChips.length > 0"
      @remove="removeActiveFilter"
      @clear="resetFilters"
    />

    <SummaryCards
      v-if="!isSupplierUser"
      :total-count="totalSuppliers"
      :needs-attention-count="needsAttentionCount"
      :pending-approvals-count="pendingApprovalsCount"
      :high-priority-count="highPriorityCount"
      :needs-attention-active="filters.completionStatus === 'needs_attention'"
      :pending-approvals-active="isPendingFilterActive"
      :high-priority-active="filters.importance === 'High'"
      @filter-needs-attention="toggleNeedsAttentionFilter"
      @filter-pending="togglePendingApprovalsFilter"
      @filter-high-priority="toggleHighPriorityFilter"
    />

    <AdvancedFiltersPanel
      v-if="showAdvancedFilters"
      :stage="filters.stage"
      :category="filters.category"
      :region="filters.region"
      :importance="filters.importance"
      :tag="filters.tag"
      :stage-options="stageOptions"
      :category-options="categoryOptions"
      :region-options="regionOptions"
      :importance-options="importanceOptions"
      :tag-options="tagOptions"
      @update:stage="(value) => (filters.stage = value)"
      @update:category="(value) => (filters.category = value)"
      @update:region="(value) => (filters.region = value)"
      @update:importance="(value) => (filters.importance = value)"
      @update:tag="(value) => (filters.tag = value)"
    />

    <!-- Batch Operations Toolbar -->
    <div v-if="selectedSupplierIds.length > 0 && !isSupplierUser" class="batch-operations-bar">
      <div class="batch-info">
        <el-icon><Check /></el-icon>
        <span>已选择 {{ selectedSupplierIds.length }} 个供应商</span>
      </div>
      <div class="batch-actions">
        <el-button type="primary" size="small" @click="showBatchTagDialog = true">
          <el-icon><PriceTag /></el-icon>
          批量添加标签
        </el-button>
        <el-button type="danger" size="small" plain @click="showBatchRemoveTagDialog = true">
          <el-icon><RemoveFilled /></el-icon>
          批量移除标签
        </el-button>
        <el-button v-if="isAdmin" type="success" size="small" @click="navigateToBulkDocumentImport">
          <el-icon><UploadFilled /></el-icon>
          批量上传文件
        </el-button>
        <el-button size="small" @click="clearSelection"> 清除选择 </el-button>
      </div>
    </div>

    <div class="content-layout" :class="{ 'supplier-view': isSupplierUser }">
      <SupplierTable
        v-if="!isSupplierUser"
        :suppliers="paginatedSuppliers"
        :loading="loading"
        :page-start="pageStart"
        :page-end="pageEnd"
        :total-items="totalSuppliers"
        :page-size="pageSizeValue"
        :page-size-options="pageSizeOptions"
        :current-page="currentPageValue"
        :total-pages="totalPages"
        :can-go-previous="canGoPrevious"
        :can-go-next="canGoNext"
        :selected-supplier-id="selectedSupplierId"
        :expanded-supplier-id="expandedSupplierId"
        :selected-supplier-ids="selectedSupplierIds"
        @update:pageSize="setPageSize"
        @previous-page="goToPreviousPage"
        @next-page="goToNextPage"
        @select="selectSupplier"
        @toggle-expand="toggleExpandedSupplier"
        @update:selectedSupplierIds="updateSelectedSupplierIds"
      >
        <template #details="{ supplier }">
          <div class="detail-card">
            <div class="detail-grid">
              <div>
                <h4>{{ t("directory.table.contact") }}</h4>
                <p>
                  <strong>{{ supplier.contactPerson }}</strong
                  ><br />
                  {{ supplier.contactEmail }}<br />
                  {{ supplier.contactPhone || t("directory.table.noPhone") }}
                </p>
              </div>
              <div>
                <h4>{{ t("directory.table.profile") }}</h4>
                <p>{{ t("directory.table.stage") }}: {{ stageLabel(supplier.stage) }}</p>
                <p>{{ t("common.status") }}: {{ statusLabel(supplier.status) }}</p>
                <p>
                  {{ t("directory.table.importance") }}:
                  {{ supplier.importance || t("directory.table.notSet") }}
                </p>
              </div>
              <div>
                <h4>{{ t("directory.table.completion") }}</h4>
                <p>
                  {{
                    t("directory.fields.profileCompletion", {
                      percent: Math.round(supplier.profileCompletion ?? 0),
                    })
                  }}
                </p>
                <p>
                  {{
                    t("directory.fields.documentCompletion", {
                      percent: Math.round(supplier.documentCompletion ?? 0),
                    })
                  }}
                </p>
              </div>
            </div>
          </div>
        </template>
      </SupplierTable>

      <!-- Supplier Detail Panel -->
      <aside v-if="selectedSupplier" class="detail-pane">
        <header class="detail-header">
          <h2>{{ selectedSupplier.companyName }}</h2>
          <div class="header-actions">
            <!-- 采购员/仅管理员可见的管理按钮 -->
            <template v-if="!isSupplierUser">
              <el-button size="small" type="warning" @click="sendProfileReminderEmail">
                <el-icon><Message /></el-icon>
                提醒完善资料
              </el-button>
              <el-button size="small" type="danger" @click="sendExpiryReminderEmail">
                <el-icon><Bell /></el-icon>
                提醒文件到期
              </el-button>
            </template>

            <!-- 供应商可见的快捷操作 -->
            <template v-else>
              <el-button size="small" type="primary" @click="navigateToChangeRequest">
                <el-icon><EditPen /></el-icon>
                申请变更资料
              </el-button>
              <el-button
                v-if="isTempSupplier"
                size="small"
                type="success"
                @click="navigateToUpgrade"
              >
                <el-icon><TrendCharts /></el-icon>
                转为正式供应商
              </el-button>
              <el-button size="small" @click="navigateToFileUpload">
                <el-icon><UploadFilled /></el-icon>
                上传/更新文件
              </el-button>
            </template>

            <el-button size="small" @click="closeDetail">关闭</el-button>
          </div>
        </header>

        <div class="detail-content">
          <!-- 供应商可见的状态提示 -->
          <el-alert
            v-if="isSupplierUser"
            type="info"
            :closable="false"
            show-icon
            class="supplier-notice"
          >
            <template #title>
              {{ isTempSupplier ? "临时供应商说明：" : "正式供应商说明：" }}
            </template>
            <ul>
              <li v-if="isTempSupplier">您当前是临时供应商，部分功能受限</li>
              <li>如需修改资料，请通过"申请变更资料"提交</li>
              <li v-if="isTempSupplier">完成全部文件上传后可申请转为正式供应商</li>
              <li>资料完整度：{{ Math.round(selectedSupplier.profileCompletion || 0) }}%</li>
            </ul>
          </el-alert>

          <!-- Profile Information Section -->
          <section class="detail-section">
            <h3 class="section-title">供应商资料</h3>

            <div class="info-grid">
              <!-- Company Name -->
              <div
                class="info-item"
                :class="{
                  filled: selectedSupplier.companyName,
                  empty: !selectedSupplier.companyName,
                }"
              >
                <label>公司名称</label>
                <span v-if="selectedSupplier.companyName">{{ selectedSupplier.companyName }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Company ID -->
              <div
                class="info-item"
                :class="{ filled: selectedSupplier.companyId, empty: !selectedSupplier.companyId }"
              >
                <label>公司ID / 注册号</label>
                <span v-if="selectedSupplier.companyId">{{ selectedSupplier.companyId }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Contact Person -->
              <div
                class="info-item"
                :class="{
                  filled: selectedSupplier.contactPerson,
                  empty: !selectedSupplier.contactPerson,
                }"
              >
                <label>联系人</label>
                <span v-if="selectedSupplier.contactPerson">{{
                  selectedSupplier.contactPerson
                }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Contact Phone -->
              <div
                class="info-item"
                :class="{
                  filled: selectedSupplier.contactPhone,
                  empty: !selectedSupplier.contactPhone,
                }"
              >
                <label>联系电话</label>
                <span v-if="selectedSupplier.contactPhone">{{
                  selectedSupplier.contactPhone
                }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Contact Email -->
              <div
                class="info-item"
                :class="{
                  filled: selectedSupplier.contactEmail,
                  empty: !selectedSupplier.contactEmail,
                }"
              >
                <label>联系邮箱</label>
                <span v-if="selectedSupplier.contactEmail">{{
                  selectedSupplier.contactEmail
                }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Category -->
              <div
                class="info-item"
                :class="{ filled: selectedSupplier.category, empty: !selectedSupplier.category }"
              >
                <label>业务类别</label>
                <span v-if="selectedSupplier.category">{{ selectedSupplier.category }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Address -->
              <div
                class="info-item full-width"
                :class="{ filled: selectedSupplier.address, empty: !selectedSupplier.address }"
              >
                <label>通讯地址</label>
                <span v-if="selectedSupplier.address">{{ selectedSupplier.address }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Business Registration Number -->
              <div
                class="info-item"
                :class="{
                  filled: selectedSupplier.businessRegistrationNumber,
                  empty: !selectedSupplier.businessRegistrationNumber,
                }"
              >
                <label>营业执照编号</label>
                <span v-if="selectedSupplier.businessRegistrationNumber">{{
                  selectedSupplier.businessRegistrationNumber
                }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Payment Terms -->
              <div
                class="info-item"
                :class="{
                  filled: selectedSupplier.paymentTerms,
                  empty: !selectedSupplier.paymentTerms,
                }"
              >
                <label>付款条款</label>
                <span v-if="selectedSupplier.paymentTerms">{{
                  selectedSupplier.paymentTerms
                }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Payment Currency -->
              <div
                class="info-item"
                :class="{
                  filled: selectedSupplier.paymentCurrency,
                  empty: !selectedSupplier.paymentCurrency,
                }"
              >
                <label>付款币种</label>
                <span v-if="selectedSupplier.paymentCurrency">{{
                  selectedSupplier.paymentCurrency
                }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Bank Account -->
              <div
                class="info-item"
                :class="{
                  filled: selectedSupplier.bankAccount,
                  empty: !selectedSupplier.bankAccount,
                }"
              >
                <label>银行账号</label>
                <span v-if="selectedSupplier.bankAccount">{{ selectedSupplier.bankAccount }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>

              <!-- Region -->
              <div
                class="info-item"
                :class="{ filled: selectedSupplier.region, empty: !selectedSupplier.region }"
              >
                <label>所属地区</label>
                <span v-if="selectedSupplier.region">{{ selectedSupplier.region }}</span>
                <span v-else class="empty-text">未填写</span>
              </div>
            </div>
          </section>

          <!-- Documents Section -->
          <section class="detail-section">
            <h3 class="section-title">文件资料</h3>
            <SupplierDocumentsPanel :documents="selectedSupplier.documents" />
          </section>

          <!-- Approval History Section -->
          <section v-if="mergedApprovals.length > 0" class="detail-section">
            <h3 class="section-title">{{ t('supplier.approvalHistory.title') }}</h3>

            <el-timeline class="approval-timeline">
              <el-timeline-item
                v-for="item in mergedApprovals"
                :key="item.key"
                :timestamp="formatDate(item.decidedAt)"
                :type="item.tagType"
                placement="top"
              >
                <div class="timeline-item-content">
                  <div class="timeline-header">
                    <el-tag
                      size="small"
                      :class="`source-tag ${item.source}-tag`"
                    >
                      {{ t(`supplier.approvalHistory.sourceLabels.${item.source}`) }}
                    </el-tag>
                    <span class="timeline-title">{{ item.title }}</span>
                    <el-tag size="small" :type="item.tagType">{{ item.statusLabel }}</el-tag>
                  </div>
                  <p class="timeline-actor">{{ t('supplier.approvalHistory.handler') }}: {{ item.actor || "未知" }}</p>
                  <p v-if="item.remark" class="timeline-remark">
                    {{ t('supplier.approvalHistory.remark') }}: {{ item.remark }}
                  </p>
                </div>
              </el-timeline-item>
            </el-timeline>
          </section>
        </div>
      </aside>
    </div>

    <!-- Batch Add Tags Dialog -->
    <el-dialog v-model="showBatchTagDialog" title="批量添加标签" width="500px">
      <div class="batch-tag-dialog">
        <el-alert title="提示" type="info" :closable="false" style="margin-bottom: 16px">
          已选择 {{ selectedSupplierIds.length }} 个供应商，将为这些供应商批量添加所选标签。
        </el-alert>

        <el-select
          v-model="selectedTagsToAdd"
          multiple
          placeholder="请选择要添加的标签"
          style="width: 100%"
          filterable
        >
          <el-option v-for="tag in availableTags" :key="tag.id" :label="tag.name" :value="tag.id">
            <span :style="{ color: tag.color || '#409EFF' }">• {{ tag.name }}</span>
          </el-option>
        </el-select>
      </div>

      <template #footer>
        <el-button @click="showBatchTagDialog = false">取消</el-button>
        <el-button
          type="primary"
          :loading="batchTagLoading"
          :disabled="selectedTagsToAdd.length === 0"
          @click="handleBatchAddTags"
        >
          确认添加
        </el-button>
      </template>
    </el-dialog>

    <!-- Batch Remove Tags Dialog -->
    <el-dialog v-model="showBatchRemoveTagDialog" title="批量移除标签" width="500px">
      <div class="batch-tag-dialog">
        <el-alert title="提示" type="warning" :closable="false" style="margin-bottom: 16px">
          已选择 {{ selectedSupplierIds.length }} 个供应商，将从这些供应商中批量移除所选标签。
        </el-alert>

        <el-select
          v-model="selectedTagsToRemove"
          multiple
          placeholder="请选择要移除的标签"
          style="width: 100%"
          filterable
        >
          <el-option v-for="tag in availableTags" :key="tag.id" :label="tag.name" :value="tag.id">
            <span :style="{ color: tag.color || '#409EFF' }">• {{ tag.name }}</span>
          </el-option>
        </el-select>
      </div>

      <template #footer>
        <el-button @click="showBatchRemoveTagDialog = false">取消</el-button>
        <el-button
          type="danger"
          :loading="batchTagLoading"
          :disabled="selectedTagsToRemove.length === 0"
          @click="handleBatchRemoveTags"
        >
          确认移除
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch, defineAsyncComponent } from "vue";
import { storeToRefs } from "pinia";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import {
  Check,
  PriceTag,
  RemoveFilled,
  Message,
  Bell,
  UploadFilled,
  EditPen,
  TrendCharts,
} from "@element-plus/icons-vue";

import DirectoryHeader from "@/components/supplier-directory/DirectoryHeader.vue";
import ActiveFilterChips, {
  type ActiveChip,
} from "@/components/supplier-directory/ActiveFilterChips.vue";
import SummaryCards from "@/components/supplier-directory/SummaryCards.vue";

import { useSupplierDocuments, useSupplierEmailReminder } from "@/composables";
import { useNotification } from "@/composables";
import { useSupplierDirectoryFilters } from "@/composables/useSupplierDirectoryFilters";
import { DEFAULT_PAGE_SIZES } from "@/composables/usePagination";

import { useSupplierStore } from "@/stores/supplier";
import { useAuthStore } from "@/stores/auth";

import * as suppliersApi from "@/api/suppliers";
import type { Supplier } from "@/types";
import { SupplierStage, SupplierStatus } from "@/types";

const { t } = useI18n();
const router = useRouter();
const supplierStore = useSupplierStore();
const authStore = useAuthStore();
const notification = useNotification();

const AdvancedFiltersPanel = defineAsyncComponent(
  () => import("@/components/supplier-directory/AdvancedFiltersPanel.vue"),
);
const SupplierTable = defineAsyncComponent(
  () => import("@/components/supplier-directory/SupplierTable.vue"),
);
const SupplierDocumentsPanel = defineAsyncComponent(
  () => import("@/components/supplier-directory/SupplierDocumentsPanel.vue"),
);

// 导入文档工具 composable
const {
  REQUIRED_DOCUMENT_TYPES,
  getDocTypeName,
  hasDocument,
  getDocument,
  formatDate,
  isExpired,
  isExpiringSoon,
  checkProfileCompleteness,
  getMissingRequiredDocLabels,
  REQUIRED_PROFILE_FIELDS,
} = useSupplierDocuments();

const { sendProfileReminder, sendExpiryReminder } = useSupplierEmailReminder();

const {
  filters,
  showAdvancedFilters: showAdvancedFiltersRef,
  toggleAdvancedFilters: toggleAdvancedFiltersRef,
  resetFilters,
  missingDocumentFilter,
  toggleMissingDocumentFilter,
  toggleNeedsAttentionFilter,
  clearQuickFilters,
} = useSupplierDirectoryFilters();

const {
  suppliers,
  loading,
  documentRequirementOptions,
  selectedSupplierId,
  selectedSupplier,
  availableTags,
  pagination,
  pageSize,
  currentPage,
} = storeToRefs(supplierStore);

const selectedSupplierIds = ref<number[]>([]);
const showBatchTagDialog = ref(false);
const showBatchRemoveTagDialog = ref(false);
const selectedTagsToAdd = ref<number[]>([]);
const selectedTagsToRemove = ref<number[]>([]);
const batchTagLoading = ref(false);
const filtersInitialized = ref(false);
const pageSizeOptions = DEFAULT_PAGE_SIZES;
const pageSizeValue = computed(() => pageSize.value);
const currentPageValue = computed(() => currentPage.value);

const filteredSuppliers = computed(() => {
  const query = filters.q.trim().toLowerCase();
  const missingDocs = new Set(missingDocumentFilter.value);

  return suppliers.value.filter((supplier) => {
    const matchesQuery =
      !query ||
      [
        supplier.companyName,
        supplier.companyId,
        supplier.contactPerson,
        supplier.contactEmail,
      ].some((value) => typeof value === "string" && value.toLowerCase().includes(query));

    if (!matchesQuery) return false;
    if (filters.status && supplier.status !== filters.status) return false;
    if (filters.stage && (supplier.stage ?? SupplierStage.TEMPORARY) !== filters.stage) {
      return false;
    }
    if (filters.category && supplier.category !== filters.category) return false;
    if (filters.region && supplier.region !== filters.region) return false;
    if (filters.importance && supplier.importance !== filters.importance) return false;
    if (filters.tag) {
      const tags = Array.isArray(supplier.tags) ? supplier.tags : [];
      const matchesTag = tags.some((tag: any) => {
        if (!tag) return false;
        if (typeof tag === "string") {
          return tag.toLowerCase() === filters.tag.toLowerCase();
        }
        return typeof tag.name === "string" && tag.name.toLowerCase() === filters.tag.toLowerCase();
      });
      if (!matchesTag) return false;
    }

    if (filters.completionStatus) {
      const status = supplier.completionStatus ?? supplier.complianceSummary?.completionCategory;
      if (status !== filters.completionStatus) return false;
    }

    if (missingDocs.size) {
      const summary = supplier.complianceSummary;
      const missing = summary?.missingDocumentTypes ?? [];
      const hasDoc = missing.some((item) => item && missingDocs.has(item.type));
      if (!hasDoc) return false;
    }

    return true;
  });
});

const totalSuppliers = computed(() => pagination.value.total ?? filteredSuppliers.value.length);

const totalPages = computed(() =>
  pageSizeValue.value > 0 ? Math.max(1, Math.ceil(totalSuppliers.value / pageSizeValue.value)) : 1,
);

const pageStart = computed(() => (totalSuppliers.value === 0 ? 0 : pagination.value.offset + 1));

const pageEnd = computed(() =>
  totalSuppliers.value === 0
    ? 0
    : Math.min(pagination.value.offset + suppliers.value.length, totalSuppliers.value),
);

const canGoPrevious = computed(() => pagination.value.offset > 0);
const canGoNext = computed(
  () => pagination.value.offset + pagination.value.limit < totalSuppliers.value,
);

const paginatedSuppliers = computed(() => filteredSuppliers.value);

const buildFilterPayload = (): suppliersApi.SupplierFilters => {
  const payload: suppliersApi.SupplierFilters = {};

  if (filters.q.trim()) payload.q = filters.q.trim();
  if (filters.status) payload.status = filters.status;
  if (filters.stage) payload.stage = filters.stage;
  if (filters.category) payload.category = filters.category;
  if (filters.region) payload.region = filters.region;
  if (filters.importance) payload.importance = filters.importance;
  if (filters.tag) payload.tag = filters.tag;
  if (filters.missingDocument.length > 0) {
    payload.missingDocument = [...filters.missingDocument];
  }

  return payload;
};

const setPageSize = async (size: number | string) => {
  const numeric = Number(size);
  if (!Number.isFinite(numeric) || numeric <= 0) {
    return;
  }

  const sanitized = Math.floor(numeric);
  await supplierStore.fetchSuppliers(buildFilterPayload(), {
    page: 1,
    pageSize: sanitized,
    force: true,
  });
};

const goToPreviousPage = async () => {
  if (!canGoPrevious.value) return;
  const target = Math.max(1, currentPageValue.value - 1);
  await supplierStore.fetchSuppliers(buildFilterPayload(), {
    page: target,
    pageSize: pageSizeValue.value,
    force: true,
  });
};

const goToNextPage = async () => {
  if (!canGoNext.value) return;
  const target = currentPageValue.value + 1;
  await supplierStore.fetchSuppliers(buildFilterPayload(), {
    page: target,
    pageSize: pageSizeValue.value,
    force: true,
  });
};

const expandedSupplierId = ref<number | null>(null);

const stageOptions = [
  { value: "", label: t("directory.stageOptions.all") },
  { value: SupplierStage.TEMPORARY, label: t("directory.stageOptions.temporary") },
  { value: SupplierStage.OFFICIAL, label: t("directory.stageOptions.official") },
];

const statusOptions = [
  { value: "", label: t("directory.statusOptions.all") },
  { value: SupplierStatus.POTENTIAL, label: t("supplier.status.potential") },
  { value: SupplierStatus.UNDER_REVIEW, label: t("supplier.status.underReview") },
  { value: SupplierStatus.PENDING_INFO, label: t("supplier.status.pendingPurchaser") },
  { value: SupplierStatus.PENDING_PURCHASER, label: t("supplier.status.pendingPurchaser") },
  { value: SupplierStatus.PENDING_QUALITY_REVIEW, label: t("supplier.status.pendingPurchaser") },
  {
    value: SupplierStatus.PENDING_PURCHASE_MANAGER,
    label: t("supplier.status.pendingPurchaseManager"),
  },
  {
    value: SupplierStatus.PENDING_PURCHASE_DIRECTOR,
    label: t("supplier.status.pendingFinanceManager"),
  },
  {
    value: SupplierStatus.PENDING_FINANCE_MANAGER,
    label: t("supplier.status.pendingFinanceManager"),
  },
  { value: SupplierStatus.APPROVED, label: t("supplier.status.approved") },
  { value: SupplierStatus.QUALIFIED, label: t("supplier.status.qualified") },
  { value: SupplierStatus.DISQUALIFIED, label: t("supplier.status.disqualified") },
  { value: SupplierStatus.SUSPENDED, label: t("supplier.status.suspended") },
  { value: SupplierStatus.TERMINATED, label: t("supplier.status.terminated") },
  { value: SupplierStatus.REJECTED, label: t("supplier.status.rejected") },
];

const categoryOptions = computed(() =>
  Array.from(
    new Set(suppliers.value.map((supplier) => supplier.category).filter(isNonEmptyString)),
  ).sort(),
);
const regionOptions = computed(() =>
  Array.from(
    new Set(suppliers.value.map((supplier) => supplier.region).filter(isNonEmptyString)),
  ).sort(),
);
const importanceOptions = computed(() =>
  Array.from(
    new Set(suppliers.value.map((supplier) => supplier.importance).filter(isNonEmptyString)),
  ).sort(),
);
const tagOptions = computed(() =>
  Array.from(
    new Set(
      suppliers.value
        .flatMap((supplier) => (Array.isArray(supplier.tags) ? supplier.tags : []))
        .map((tag: any) => (typeof tag === "string" ? tag : tag?.name))
        .filter(isNonEmptyString),
    ),
  ).sort(),
);

const activeAdvancedCount = computed(() => {
  let count = 0;
  if (filters.stage) count += 1;
  if (filters.category) count += 1;
  if (filters.region) count += 1;
  if (filters.importance) count += 1;
  if (filters.tag) count += 1;
  return count;
});

const canClearQuickFilters = computed(() =>
  Boolean(filters.completionStatus || missingDocumentFilter.value.length),
);

// Merge approval history from different sources
interface TimelineItem {
  key: string;
  source: string;
  title: string;
  statusLabel: string;
  actor: string;
  decidedAt: string;
  remark: string;
  tagType: string;
}

const mergedApprovals = computed<TimelineItem[]>(() => {
  if (!selectedSupplier.value) return [];

  const items: TimelineItem[] = [];

  // Add status approvals
  selectedSupplier.value.approvalHistory?.forEach((approval: any) => {
    items.push({
      key: `status_${approval.id || approval.date}`,
      source: approval.source || 'status_approval',
      title: approval.step || '',
      statusLabel: approval.result === 'approved' ? t('common.approved') : t('common.rejected'),
      actor: approval.decidedByName || approval.approver || '',
      decidedAt: approval.decidedAt || approval.date || '',
      remark: approval.comments || '',
      tagType: approval.result === 'approved' ? 'success' : 'danger',
    });
  });

  // Add file approvals
  selectedSupplier.value.fileApprovals?.forEach((approval: any) => {
    const fileName = approval.fileName ? ` - ${approval.fileName}` : '';
    items.push({
      key: `file_${approval.id}`,
      source: approval.source || 'file_upload',
      title: `${approval.stepName || ''}${fileName}`,
      statusLabel: approval.decision === 'approved' ? t('common.approved') : t('common.rejected'),
      actor: approval.decidedByName || '',
      decidedAt: approval.decidedAt || '',
      remark: approval.comments || '',
      tagType: approval.decision === 'approved' ? 'success' : 'danger',
    });
  });

  // Sort by timestamp (newest first)
  return items.sort((a, b) => {
    const timeA = new Date(a.decidedAt || 0).getTime();
    const timeB = new Date(b.decidedAt || 0).getTime();
    return timeB - timeA;
  });
});

const activeFilterChips = computed<ActiveChip[]>(() => {
  const chips: ActiveChip[] = [];
  if (filters.q.trim()) {
    chips.push({ key: "q", label: t("directory.filterChips.search", { value: filters.q.trim() }) });
  }
  if (filters.status) {
    chips.push({
      key: "status",
      label: t("directory.filterChips.status", { value: statusLabel(filters.status) }),
    });
  }
  if (filters.stage) {
    chips.push({
      key: "stage",
      label: t("directory.filterChips.stage", { value: stageLabel(filters.stage) }),
    });
  }
  if (filters.category) {
    chips.push({
      key: "category",
      label: t("directory.filterChips.category", { value: filters.category }),
    });
  }
  if (filters.region) {
    chips.push({
      key: "region",
      label: t("directory.filterChips.region", { value: filters.region }),
    });
  }
  if (filters.importance) {
    chips.push({
      key: "importance",
      label: t("directory.filterChips.importance", { value: filters.importance }),
    });
  }
  if (filters.tag) {
    chips.push({ key: "tag", label: t("directory.filterChips.tag", { value: filters.tag }) });
  }
  if (filters.completionStatus) {
    chips.push({
      key: "completionStatus",
      label: t("directory.filterChips.progress", {
        value: completionLabelForValue(filters.completionStatus),
      }),
    });
  }
  missingDocumentFilter.value.forEach((code) => {
    const match = documentRequirementOptions.value.find((item) => item.type === code);
    chips.push({
      key: `missing:${code}`,
      label: t("directory.filterChips.missing", { value: match?.label ?? code }),
    });
  });
  return chips;
});

const removeActiveFilter = (key: string) => {
  // 简单过滤器字段映射
  const simpleFilters: Record<string, string> = {
    q: "q",
    status: "status",
    stage: "stage",
    category: "category",
    region: "region",
    importance: "importance",
    tag: "tag",
    completionStatus: "completionStatus",
  };

  if (key in simpleFilters) {
    (filters as Record<string, unknown>)[simpleFilters[key]] = "";
    return;
  }

  if (key.startsWith("missing:")) {
    const code = key.split(":")[1];
    filters.missingDocument = filters.missingDocument.filter((item) => item !== code);
  }
};

const selectSupplier = async (id: number) => {
  await supplierStore.selectSupplier(id);
};

const toggleExpandedSupplier = (id: number) => {
  expandedSupplierId.value = expandedSupplierId.value === id ? null : id;
};

const statusLabel = (status?: string | null) => {
  if (!status) return t("directory.table.unknown");
  const option = statusOptions.find((item) => item.value === status);
  return option?.label ?? status;
};

const statusClass = (status?: string | null) => {
  switch (status) {
    case SupplierStatus.APPROVED:
    case SupplierStatus.QUALIFIED:
      return "status-positive";
    case SupplierStatus.DISQUALIFIED:
    case SupplierStatus.REJECTED:
    case SupplierStatus.TERMINATED:
      return "status-negative";
    default:
      return "status-neutral";
  }
};

const stageLabel = (stage?: string | null) => {
  if (!stage) return t("directory.stageOptions.temporary");
  return stage === SupplierStage.OFFICIAL
    ? t("directory.stageOptions.official")
    : t("directory.stageOptions.temporary");
};

const completionLabelForValue = (value: string) => {
  switch (value) {
    case "needs_attention":
      return t("supplier.filters.needsAttention");
    case "mostly_complete":
      return t("supplier.filters.mostlyComplete");
    case "complete":
      return t("supplier.filters.complete");
    default:
      return value;
  }
};

const missingRequirementLabels = (supplier: Supplier) => {
  const labels = new Set<string>();
  const push = (value?: string | null) => {
    if (!value) return;
    const trimmed = value.trim();
    if (trimmed) labels.add(trimmed);
  };

  if (Array.isArray(supplier.missingRequirements)) {
    supplier.missingRequirements.forEach((item) => {
      if (!item) return;
      if (typeof item === "string") {
        push(item);
        return;
      }
      push(item.label);
      push((item as { key?: string }).key);
    });
  }

  supplier.complianceSummary?.missingItems?.forEach((item) => push(item?.label ?? item?.key));
  supplier.complianceSummary?.missingDocumentTypes?.forEach((item) =>
    push(item?.label ?? item?.type),
  );
  supplier.complianceSummary?.missingProfileFields?.forEach((item) =>
    push(item?.label ?? item?.key),
  );

  return Array.from(labels);
};

const isAdmin = computed(() => authStore.user?.role === "admin");

const isSupplierUser = computed(() => {
  const user = authStore.user;
  if (!user) {
    return false;
  }
  if (user.supplierId == null) {
    return false;
  }
  const staffRoles = new Set([
    "admin",
    "purchaser",
    "procurement_manager",
    "procurement_director",
    "finance_accountant",
    "finance_director",
  ]);
  if (staffRoles.has(user.role)) {
    return false;
  }
  const permissions = new Set(user.permissions || []);
  return !permissions.has("supplier.segment.manage");
});

const isTempSupplier = computed(() => authStore.user?.role === "temp_supplier");

// Navigation methods for supplier quick actions
const navigateToChangeRequest = () => {
  router.push("/supplier/change-requests");
};

const navigateToUpgrade = () => {
  router.push("/supplier/upgrade");
};

const navigateToFileUpload = () => {
  router.push("/supplier/file-uploads");
};

const pageTitle = computed(() =>
  isSupplierUser.value ? t("directory.myProfileTitle") : t("directory.pageTitle"),
);

const showAdvancedFilters = computed(() => !isSupplierUser.value && showAdvancedFiltersRef.value);

const toggleAdvancedFilters = () => {
  if (isSupplierUser.value) return;
  toggleAdvancedFiltersRef();
};

const needsAttentionCount = computed(
  () =>
    filteredSuppliers.value.filter((supplier) => {
      const status = supplier.completionStatus ?? supplier.complianceSummary?.completionCategory;
      return status === "needs_attention";
    }).length,
);

const pendingStatuses = new Set<string>([
  SupplierStatus.POTENTIAL,
  SupplierStatus.UNDER_REVIEW,
  SupplierStatus.PENDING_INFO,
  SupplierStatus.PENDING_PURCHASER,
  SupplierStatus.PENDING_QUALITY_REVIEW,
  SupplierStatus.PENDING_PURCHASE_MANAGER,
  SupplierStatus.PENDING_PURCHASE_DIRECTOR,
  SupplierStatus.PENDING_FINANCE_MANAGER,
]);

const pendingApprovalsCount = computed(
  () => filteredSuppliers.value.filter((supplier) => pendingStatuses.has(supplier.status)).length,
);

const highPriorityCount = computed(
  () => filteredSuppliers.value.filter((supplier) => supplier.importance === "High").length,
);

const isPendingFilterActive = computed(() =>
  Boolean(filters.status && pendingStatuses.has(filters.status)),
);

const togglePendingApprovalsFilter = () => {
  if (isPendingFilterActive.value) {
    filters.status = "";
  } else {
    filters.status = SupplierStatus.PENDING_PURCHASER;
  }
};

const toggleHighPriorityFilter = () => {
  if (filters.importance === "High") {
    filters.importance = "";
  } else {
    filters.importance = "High";
  }
};

const handleRefresh = async () => {
  await supplierStore.fetchSuppliers(buildFilterPayload(), {
    page: currentPageValue.value,
    pageSize: pageSizeValue.value,
    force: true,
  });
  if (selectedSupplierId.value) {
    await supplierStore.selectSupplier(selectedSupplierId.value);
  }
};

// Detail Panel Functions
const closeDetail = () => {
  supplierStore.clearSelection();
};

const toRecord = (value: unknown): Record<string, unknown> => {
  if (!value || typeof value !== "object") return {};
  return value as Record<string, unknown>;
};

// Email Reminder Functions
const sendProfileReminderEmail = async () => {
  if (!selectedSupplier.value) return;

  // Check if supplier has contact email
  if (!selectedSupplier.value.contactEmail) {
    notification.error("该供应商未填写联系邮箱，无法发送提醒邮件");
    return;
  }

  // Check for missing profile fields and documents
  const missingFields = checkProfileCompleteness(
    toRecord(selectedSupplier.value),
    REQUIRED_PROFILE_FIELDS,
  );
  const missingDocs = getMissingRequiredDocLabels(selectedSupplier.value?.documents);

  if (missingFields.length === 0 && missingDocs.length === 0) {
    notification.info("该供应商资料已完整，无需提醒");
    return;
  }

  const opened = sendProfileReminder(
    {
      companyName: selectedSupplier.value.companyName,
      contactPerson: selectedSupplier.value.contactPerson,
      contactEmail: selectedSupplier.value.contactEmail,
    },
    missingFields,
    missingDocs,
    () => {
      notification.info(`已打开邮件客户端，请发送提醒邮件至 ${selectedSupplier.value?.contactEmail}`, undefined, {
        duration: 5000,
        showClose: true,
      });
    },
  );

  if (!opened) {
    notification.error("无法打开邮件客户端，请检查浏览器设置", undefined, { duration: 5000, showClose: true });
  }
};

const sendExpiryReminderEmail = async () => {
  if (!selectedSupplier.value?.documents) return;

  // Check if supplier has contact email
  if (!selectedSupplier.value.contactEmail) {
    notification.error("该供应商未填写联系邮箱，无法发送提醒邮件");
    return;
  }

  // Find documents that are expired or expiring soon
  const expiringDocs = selectedSupplier.value.documents
    .filter((doc) => isExpired(doc) || isExpiringSoon(doc))
    .map((doc) => ({
      name: getDocTypeName(doc.docType || ""),
      filename: doc.filename,
      expiryDate: formatDate(doc.expiresAt),
      status: isExpired(doc) ? "已过期" : "即将过期",
    }));

  if (expiringDocs.length === 0) {
    notification.info("该供应商暂无即将过期或已过期的文件");
    return;
  }

  const opened = sendExpiryReminder(
    {
      companyName: selectedSupplier.value.companyName,
      contactPerson: selectedSupplier.value.contactPerson,
      contactEmail: selectedSupplier.value.contactEmail,
    },
    expiringDocs,
    () => {
      notification.info(`已打开邮件客户端，请发送提醒邮件至 ${selectedSupplier.value?.contactEmail}`, undefined, {
        duration: 5000,
        showClose: true,
      });
    },
  );

  if (!opened) {
    notification.error("无法打开邮件客户端，请检查浏览器设置", undefined, { duration: 5000, showClose: true });
  }
};

// Batch Tag Operations
const updateSelectedSupplierIds = (ids: number[]) => {
  selectedSupplierIds.value = ids;
};

const clearSelection = () => {
  selectedSupplierIds.value = [];
};

const navigateToBulkDocumentImport = () => {
  router.push({
    name: "admin-bulk-document-import",
    state: { preSelectedSupplierIds: selectedSupplierIds.value },
  });
};

const handleBatchAddTags = async () => {
  if (selectedTagsToAdd.value.length === 0 || selectedSupplierIds.value.length === 0) {
    return;
  }

  batchTagLoading.value = true;
  let successCount = 0;
  let errorCount = 0;

  try {
    for (const tagId of selectedTagsToAdd.value) {
      try {
        await suppliersApi.batchAssignTag(tagId, selectedSupplierIds.value);
        successCount++;
      } catch (error) {
        console.error(`Failed to assign tag ${tagId}:`, error);
        errorCount++;
      }
    }

    if (successCount > 0) {
      notification.success(
        `成功为 ${selectedSupplierIds.value.length} 个供应商添加了 ${successCount} 个标签`,
      );
      await supplierStore.fetchSuppliers();
      selectedTagsToAdd.value = [];
      showBatchTagDialog.value = false;
      clearSelection();
    }

    if (errorCount > 0) {
      notification.warning(`${errorCount} 个标签添加失败`);
    }
  } catch (error) {
    console.error("Batch add tags error:", error);
    notification.error("批量添加标签失败");
  } finally {
    batchTagLoading.value = false;
  }
};

const handleBatchRemoveTags = async () => {
  if (selectedTagsToRemove.value.length === 0 || selectedSupplierIds.value.length === 0) {
    return;
  }

  batchTagLoading.value = true;
  let successCount = 0;
  let errorCount = 0;

  try {
    for (const tagId of selectedTagsToRemove.value) {
      try {
        await suppliersApi.batchRemoveTag(tagId, selectedSupplierIds.value);
        successCount++;
      } catch (error) {
        console.error(`Failed to remove tag ${tagId}:`, error);
        errorCount++;
      }
    }

    if (successCount > 0) {
      notification.success(
        `成功从 ${selectedSupplierIds.value.length} 个供应商中移除了 ${successCount} 个标签`,
      );
      await supplierStore.fetchSuppliers();
      selectedTagsToRemove.value = [];
      showBatchRemoveTagDialog.value = false;
      clearSelection();
    }

    if (errorCount > 0) {
      notification.warning(`${errorCount} 个标签移除失败`);
    }
  } catch (error) {
    console.error("Batch remove tags error:", error);
    notification.error("批量移除标签失败");
  } finally {
    batchTagLoading.value = false;
  }
};

watch(
  () => ({
    q: filters.q,
    status: filters.status,
    stage: filters.stage,
    category: filters.category,
    region: filters.region,
    importance: filters.importance,
    tag: filters.tag,
    completionStatus: filters.completionStatus,
    missingDocument: [...filters.missingDocument],
  }),
  async () => {
    if (isSupplierUser.value || !filtersInitialized.value) {
      return;
    }

    await supplierStore.fetchSuppliers(buildFilterPayload(), {
      page: 1,
      pageSize: pageSizeValue.value,
      force: true,
    });
  },
  { deep: true },
);

watch(filteredSuppliers, () => {
  if (!filteredSuppliers.value.some((supplier) => supplier.id === expandedSupplierId.value)) {
    expandedSupplierId.value = null;
  }
});

onMounted(async () => {
  await supplierStore.fetchSuppliers(buildFilterPayload(), {
    page: 1,
    pageSize: pageSizeValue.value,
    force: true,
  });
  filtersInitialized.value = true;
  await supplierStore.ensureTags();

  // For supplier users, select their own supplier profile
  if (isSupplierUser.value && suppliers.value.length > 0) {
    await supplierStore.selectSupplier(suppliers.value[0].id);
  }
  // For staff users, select the first supplier if none selected
  else if (!isSupplierUser.value && suppliers.value.length && selectedSupplierId.value == null) {
    await supplierStore.selectSupplier(suppliers.value[0].id);
  }
});

const isNonEmptyString = (value: unknown): value is string =>
  typeof value === "string" && value.trim().length > 0;
</script>

<style scoped>
.directory-page {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  padding: 1.5rem;
}

.view-title {
  margin: 0;
  font-size: 1.75rem;
  font-weight: 600;
}

.content-layout {
  display: grid;
  grid-template-columns: 1fr 600px;
  gap: 1.5rem;
}

/* Detail Panel Styles */
.detail-pane {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  background: #ffffff;
  display: flex;
  flex-direction: column;
  height: calc(100vh - 200px);
  overflow: hidden;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem;
  border-bottom: 1px solid #e5e7eb;
}

.detail-header h2 {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 600;
  color: #111827;
}

.header-actions {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.detail-content {
  flex: 1;
  overflow-y: auto;
  padding: 1.25rem;
}

/* Supplier Notice */
.supplier-notice {
  margin-bottom: 20px;
}

.supplier-notice ul {
  margin: 8px 0 0 20px;
  padding: 0;
}

.supplier-notice li {
  margin: 4px 0;
}

.detail-section {
  margin-bottom: 2rem;
}

.section-title {
  font-size: 1.1rem;
  font-weight: 600;
  color: #111827;
  margin: 0 0 1rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 2px solid #e5e7eb;
}

/* Info Grid */
.info-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.info-item {
  padding: 0.75rem;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
}

.info-item.filled {
  background: #f0fdf4;
  border-color: #86efac;
}

.info-item.empty {
  background: #fef2f2;
  border-color: #fca5a5;
}

.info-item.full-width {
  grid-column: 1 / -1;
}

.info-item label {
  display: block;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  color: #6b7280;
  margin-bottom: 0.25rem;
}

.info-item span {
  display: block;
  font-size: 0.9rem;
  color: #111827;
}

.info-item .empty-text {
  color: #9ca3af;
  font-style: italic;
}

/* Documents Section */
.documents-list {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.doc-requirement h4 {
  font-size: 1rem;
  font-weight: 600;
  color: #374151;
  margin: 0 0 0.75rem 0;
}

.required-docs {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.doc-status {
  padding: 0.75rem;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
}

.doc-status.uploaded {
  background: #f0fdf4;
  border-color: #86efac;
}

.doc-status.missing {
  background: #fef2f2;
  border-color: #fca5a5;
}

.doc-name {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
  color: #111827;
  margin-bottom: 0.5rem;
}

.doc-name .el-icon {
  font-size: 1.25rem;
}

.doc-status.uploaded .doc-name .el-icon {
  color: #22c55e;
}

.doc-status.missing .doc-name .el-icon {
  color: #ef4444;
}

.doc-info {
  font-size: 0.85rem;
  color: #6b7280;
}

.doc-info .validity {
  color: #059669;
}

.doc-info .missing-text {
  color: #dc2626;
}

/* All Documents List */
.all-documents h4 {
  font-size: 1rem;
  font-weight: 600;
  color: #374151;
  margin: 0 0 0.75rem 0;
}

.doc-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.doc-item {
  padding: 0.75rem;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
  background: #f9fafb;
}

.doc-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.doc-type {
  display: inline-block;
  padding: 0.25rem 0.5rem;
  background: #dbeafe;
  color: #1e40af;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
}

.doc-filename {
  font-size: 0.9rem;
  color: #111827;
  font-weight: 500;
}

.doc-dates {
  display: flex;
  gap: 1rem;
  font-size: 0.85rem;
}

.valid-from {
  color: #059669;
}

.expires-at {
  color: #6b7280;
}

.expires-at.expired {
  color: #dc2626;
  font-weight: 600;
}

.expires-at.expiring {
  color: #f59e0b;
  font-weight: 600;
}

.detail-list {
  display: grid;
  gap: 0.75rem;
  margin-top: 1rem;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  padding: 0.25rem 0.65rem;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 600;
}

.status-positive {
  background: #dcfce7;
  color: #166534;
}

.status-neutral {
  background: #e0f2fe;
  color: #0c4a6e;
}

.status-negative {
  background: #fee2e2;
  color: #b91c1c;
}

.detail-list dt {
  font-size: 0.8rem;
  text-transform: uppercase;
  color: #6b7280;
  margin: 0 0 0.15rem;
}

.detail-list dd {
  margin: 0;
  font-size: 0.95rem;
  color: #1f2937;
}

.detail-card {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  background: #f9fafb;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 1rem;
}

.detail-card h4 {
  margin: 0 0 0.35rem;
  font-size: 0.95rem;
  font-weight: 600;
  color: #111827;
}

.detail-card p {
  margin: 0;
  color: #4b5563;
  font-size: 0.9rem;
}

/* Batch Operations Bar */
.batch-operations-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-radius: 8px;
  color: white;
  box-shadow: 0 4px 6px rgba(102, 126, 234, 0.2);
}

.batch-info {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  font-size: 14px;
}

.batch-actions {
  display: flex;
  gap: 8px;
}

.batch-tag-dialog {
  padding: 16px 0;
}

@media (max-width: 960px) {
  .batch-operations-bar {
    flex-direction: column;
    gap: 12px;
    align-items: stretch;
  }

  .batch-actions {
    justify-content: stretch;
  }

  .batch-actions button {
    flex: 1;
  }
}

/* Approval History Styles */
.approval-timeline {
  padding: 16px 0;
}

.timeline-item-content {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.timeline-header {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.timeline-title {
  font-weight: 600;
  color: #303133;
  flex: 1;
}

.timeline-actor {
  margin: 0;
  font-size: 14px;
  color: #606266;
}

.timeline-remark {
  margin: 0;
  font-size: 14px;
  color: #909399;
  font-style: italic;
}

/* Source tag colors */
.source-tag {
  margin-right: 0 !important;
  border: none !important;
  font-weight: 500;
}

/* Status approval tag - Green theme */
.status_approval-tag {
  background-color: #e8f5e9 !important;
  color: #2e7d32 !important;
}

.status_approval-tag :deep(.el-icon) {
  color: #2e7d32;
}

/* File upload tag - Blue theme */
.file_upload-tag {
  background-color: #e3f2fd !important;
  color: #1976d2 !important;
}

.file_upload-tag :deep(.el-icon) {
  color: #1976d2;
}

/* Upgrade application tag - Purple theme */
.upgrade_application-tag {
  background-color: #f3e5f5 !important;
  color: #7b1fa2 !important;
}

.upgrade_application-tag :deep(.el-icon) {
  color: #7b1fa2;
}
</style>





