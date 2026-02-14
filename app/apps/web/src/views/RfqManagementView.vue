﻿<template>
  <div class="rfq-management-view">
    <PageHeader :title="t('rfq.management.title')">
      <template #actions>
        <el-button
          v-if="!isSupplier"
          type="primary"
          :icon="Plus"
          @click="navigateToCreate"
        >
          {{ t("rfq.management.createRfq") }}
        </el-button>
      </template>
    </PageHeader>

    <template v-if="isSupplier">
      <el-card class="supplier-card" shadow="never" v-loading="supplierInvitationsLoading">
        <template #header>
          <div class="supplier-header">
            <div>
              <h2>{{ t("rfq.management.supplierInvitations.title") }}</h2>
              <p class="supplier-subtitle">{{ t("rfq.management.supplierInvitations.subtitle") }}</p>
            </div>
            <el-tag v-if="supplierPendingCount" type="warning" size="small">
              {{ t("rfq.management.supplierInvitations.pending", { count: supplierPendingCount }) }}
            </el-tag>
          </div>
        </template>

        <el-alert
          v-if="supplierInvitationsError"
          class="supplier-alert"
          :title="supplierInvitationsError"
          type="error"
          show-icon
          :closable="false"
        />

        <el-table
          v-else-if="supplierInvitationsSorted.length"
          :data="supplierInvitationsSorted"
          style="width: 100%"
        >
          <el-table-column
            prop="title"
            :label="t('rfq.management.supplierInvitations.columns.title')"
            min-width="220"
          >
            <template #default="{ row }">
              <el-link type="primary" @click="viewRfq(row.id)">
                {{ row.title }}
              </el-link>
            </template>
          </el-table-column>

          <el-table-column
            prop="rfqStatus"
            :label="t('rfq.management.supplierInvitations.columns.status')"
            width="150"
          >
            <template #default="{ row }">
              <el-tag :type="getStatusType(row.rfqStatus || '')" size="small">
                {{ translateRfqStatus(row.rfqStatus) }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column
            prop="quoteStatus"
            :label="t('rfq.management.supplierInvitations.columns.quoteStatus')"
            width="150"
          >
            <template #default="{ row }">
              {{ translateQuoteStatus(row.quoteStatus) }}
            </template>
          </el-table-column>

          <el-table-column
            prop="validUntil"
            :label="t('rfq.management.supplierInvitations.columns.validUntil')"
            width="180"
          >
            <template #default="{ row }">
              {{ formatDateTime(row.validUntil) }}
            </template>
          </el-table-column>

          <el-table-column
            prop="daysRemaining"
            :label="t('rfq.management.supplierInvitations.columns.daysRemaining')"
            width="140"
          >
            <template #default="{ row }">
              {{ formatDaysRemaining(row.daysRemaining) }}
            </template>
          </el-table-column>

          <el-table-column
            :label="t('rfq.management.supplierInvitations.columns.needsResponse')"
            width="160"
            align="center"
          >
            <template #default="{ row }">
              <el-tag :type="invitationTagType(row)" size="small">
                <span v-if="row.needsResponse">
                  {{ t('rfq.management.supplierInvitations.needsResponseTag') }}
                </span>
                <span v-else-if="row.denialReason">
                  {{ t('rfq.management.supplierInvitations.expiredTag') }}
                </span>
                <span v-else>
                  {{ t('rfq.management.supplierInvitations.responseSubmittedTag') }}
                </span>
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column
            :label="t('rfq.management.supplierInvitations.columns.actions')"
            width="140"
            align="center"
          >
            <template #default="{ row }">
              <el-button type="primary" link size="small" @click="viewRfq(row.id)">
                {{ t('rfq.management.supplierInvitations.actions.view') }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-empty
          v-else
          :description="t('rfq.management.supplierInvitations.empty')"
        />
      </el-card>
    </template>

    <template v-else>
      <!-- Material Type Tabs -->
      <el-tabs v-model="activeTab" class="material-tabs" @tab-change="handleTabChange">
        <el-tab-pane :label="t('rfq.materialType.idm')" name="IDM">
          <template #label>
            <span class="tab-label">
              <el-icon><DocumentChecked /></el-icon>
              {{ t("rfq.materialType.idm") }}
            </span>
          </template>
        </el-tab-pane>

        <el-tab-pane :label="t('rfq.materialType.dm')" name="DM" disabled>
          <template #label>
            <span class="tab-label">
              <el-icon><Document /></el-icon>
              {{ t("rfq.materialType.dm") }}
              <el-tag type="info" size="small" style="margin-left: 8px">
                {{ t("common.comingSoon") }}
              </el-tag>
            </span>
          </template>
        </el-tab-pane>
      </el-tabs>

      <el-card class="pending-rfq-line-items-card" shadow="never" v-loading="pendingRfqLineItemsLoading">
        <template #header>
          <div class="pending-header">
            <div>
              <h2>{{ t("rfq.management.pendingLineItems.title") }}</h2>
              <p class="pending-subtitle">{{ t("rfq.management.pendingLineItems.subtitle") }}</p>
            </div>
            <el-tag v-if="pendingRfqLineItemCount" type="warning" size="small">
              {{ pendingRfqLineItemCount }} {{ t("rfq.management.pendingLineItems.itemsCount") }}
            </el-tag>
          </div>
        </template>

        <el-alert
          v-if="pendingRfqLineItemCount"
          class="pending-alert"
          :title="t('rfq.management.pendingLineItems.alert', { count: pendingRfqLineItemCount })"
          type="warning"
          show-icon
          :closable="false"
        />

        <el-alert
          v-else-if="pendingRfqLineItemsError"
          class="pending-alert"
          :title="pendingRfqLineItemsError"
          type="error"
          show-icon
          :closable="false"
        />

        <el-table
          v-if="pendingRfqLineItems.length"
          :data="pendingRfqLineItems"
          style="width: 100%"
          class="pending-table"
        >
          <el-table-column
            prop="rfqTitle"
            :label="t('rfq.management.pendingLineItems.columns.rfqTitle')"
            min-width="180"
          >
            <template #default="{ row }">
              <el-link type="primary" @click="viewRfqLineItem(row.rfqId, row.id)">
                {{ row.rfqTitle }}
              </el-link>
            </template>
          </el-table-column>

          <el-table-column
            prop="itemName"
            :label="t('rfq.management.pendingLineItems.columns.itemName')"
            min-width="200"
          />

          <el-table-column
            prop="requestingDepartment"
            :label="t('rfq.management.pendingLineItems.columns.department')"
            width="120"
          />

          <el-table-column
            prop="quantity"
            :label="t('rfq.management.pendingLineItems.columns.quantity')"
            width="120"
          >
            <template #default="{ row }">
              {{ row.quantity }} {{ row.unit || "" }}
            </template>
          </el-table-column>

          <el-table-column
            prop="status"
            :label="t('rfq.management.pendingLineItems.columns.status')"
            width="150"
          >
            <template #default="{ row }">
              <el-tag :type="getLineItemStatusType(row.status)" size="small">
                {{ translateLineItemStatus(row.status, row.selectedQuoteId) }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column
            prop="supplierName"
            :label="t('rfq.management.pendingLineItems.columns.supplier')"
            width="150"
          >
            <template #default="{ row }">
              {{ row.supplierName || '-' }}
            </template>
          </el-table-column>

          <el-table-column
            prop="updatedAt"
            :label="t('rfq.management.pendingLineItems.columns.updatedAt')"
            width="180"
          >
            <template #default="{ row }">
              {{ formatDateTime(row.updatedAt) }}
            </template>
          </el-table-column>

          <el-table-column
            :label="t('rfq.management.pendingLineItems.columns.actions')"
            width="150"
            align="center"
          >
            <template #default="{ row }">
              <el-button type="primary" size="small" link @click="viewRfqLineItem(row.rfqId, row.id)">
                {{ t("rfq.management.pendingLineItems.actions.handle") }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-empty
          v-else-if="!pendingRfqLineItemsLoading"
          :description="t('rfq.management.pendingLineItems.empty')"
        />
      </el-card>

      <!-- Filters -->
      <el-card class="filter-card" shadow="never">
        <el-form :inline="true" :model="filters">
          <el-form-item :label="t('rfq.filter.status')">
            <el-select v-model="filters.status" :placeholder="t('common.all')" clearable>
              <el-option :label="t('rfq.status.draft')" value="draft" />
              <el-option :label="t('rfq.status.published')" value="published" />
              <el-option :label="t('rfq.status.inProgress')" value="in_progress" />
              <el-option :label="t('rfq.status.closed')" value="closed" />
              <el-option :label="t('rfq.status.cancelled')" value="cancelled" />
            </el-select>
          </el-form-item>

          <el-form-item :label="t('rfq.filter.category')">
            <el-select
              v-model="filters.distributionCategory"
              :placeholder="t('common.all')"
              clearable
            >
              <el-option :label="t('rfq.distributionCategory.equipment')" value="equipment" />
              <el-option
                :label="t('rfq.distributionCategory.auxiliaryMaterials')"
                value="auxiliary_materials"
              />
            </el-select>
          </el-form-item>

          <el-form-item :label="t('rfq.filter.rfqType')">
            <el-select v-model="filters.rfqType" :placeholder="t('common.all')" clearable>
              <el-option :label="t('rfq.rfqType.shortTerm')" value="short_term" />
              <el-option :label="t('rfq.rfqType.longTerm')" value="long_term" />
            </el-select>
          </el-form-item>

          <el-form-item>
            <el-button type="primary" :icon="Search" @click="loadRfqs">
              {{ t("common.search") }}
            </el-button>
            <el-button :icon="RefreshLeft" @click="resetFilters">
              {{ t("common.reset") }}
            </el-button>
          </el-form-item>
        </el-form>
      </el-card>

      <!-- RFQ List -->
      <el-card class="list-card" shadow="never" v-loading="loading">
        <el-table :data="rfqList" style="width: 100%">
          <el-table-column prop="id" label="ID" width="80" />
          <el-table-column prop="title" :label="t('rfq.form.title')" min-width="200" />

          <el-table-column :label="t('rfq.distributionForm.category')" width="150">
            <template #default="{ row }">
              {{ getCategoryLabel(row.distributionCategory) }}
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.distributionForm.subcategory')" width="150">
            <template #default="{ row }">
              {{ getSubcategoryLabel(row.distributionSubcategory) }}
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.form.rfqType')" width="120">
            <template #default="{ row }">
              <el-tag :type="row.rfqType === 'short_term' ? 'success' : 'warning'" size="small">
                {{ t(`rfq.rfqType.${row.rfqType}`) }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column :label="t('rfq.status.label')" width="120">
            <template #default="{ row }">
              <el-tag :type="getStatusType(row.status)" size="small">
                {{ t(`rfq.status.${row.status}`) }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column
            prop="validUntil"
            :label="t('rfq.form.validUntil')"
            width="160"
          >
            <template #default="{ row }">
              {{ formatDateTime(row.validUntil) }}
            </template>
          </el-table-column>

          <el-table-column
            prop="createdAt"
            :label="t('common.createdAt')"
            width="160"
          >
            <template #default="{ row }">
              {{ formatDateTime(row.createdAt) }}
            </template>
          </el-table-column>

          <el-table-column :label="t('common.actions')" width="200" align="center">
            <template #default="{ row }">
              <el-button type="primary" size="small" link @click="viewRfq(row.id)">
                {{ t("common.view") }}
              </el-button>
              <el-button
                v-if="canEditRfq"
                type="warning"
                size="small"
                link
                @click="editRfq(row.id, row.status)"
              >
                {{ t("common.edit") }}
              </el-button>
              <el-button
                v-if="canEditRfq"
                type="danger"
                size="small"
                link
                @click="deleteRfq(row)"
              >
                {{ t("common.delete") }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-card>

      <div class="pagination-container">
        <el-pagination
          background
          layout="prev, pager, next, sizes"
          :total="pagination.total"
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.limit"
          :page-sizes="[10, 20, 50, 100]"
          @current-change="handleCurrentChange"
          @size-change="handleSizeChange"
        />
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, reactive, onMounted, watch } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { Plus, Search, RefreshLeft, DocumentChecked, Document } from "@element-plus/icons-vue";
import PageHeader from "@/components/layout/PageHeader.vue";
import type { Rfq, SupplierRfqInvitationSummary } from "@/types";
import {
  fetchRfqs,
  type RfqListParams,
  listSupplierRfqInvitations,
  fetchPendingRfqLineItems,
  type PendingRfqLineItem,
} from "@/api/rfq";
import { useAuthStore } from "@/stores/auth";
import { useNotification, usePermission, useTableActions } from "@/composables";
import { useService } from "@/core/hooks";
import type { HttpService } from "@/services";

const { t } = useI18n();
const router = useRouter();
const authStore = useAuthStore();
const notification = useNotification();
const permission = usePermission();
const tableActions = useTableActions<Rfq>();
const httpService = useService<HttpService>("http");

const isSupplier = computed(() => permission.hasRole("supplier") || !!authStore.user?.supplierId);

// Check if user can edit/delete RFQs (only purchaser can)
const canEditRfq = computed(() => {
  const role = authStore.user?.role;
  return role === 'purchaser' || role === 'admin';
});

const activeTab = ref("IDM");
const loading = ref(false);
const rfqList = ref<Rfq[]>([]);

const filters = reactive<RfqListParams>({
  materialType: "IDM",
  status: undefined,
  distributionCategory: undefined,
  rfqType: undefined,
});

const pagination = reactive({
  page: 1,
  limit: 20,
  total: 0,
  totalPages: 0,
});

const pendingRfqLineItems = ref<PendingRfqLineItem[]>([]);
const pendingRfqLineItemsLoading = ref(false);
const pendingRfqLineItemsError = ref<string | null>(null);

const pendingRfqLineItemCount = computed(() => pendingRfqLineItems.value.length);

const supplierInvitations = ref<SupplierRfqInvitationSummary[]>([]);
const supplierInvitationsLoading = ref(false);
const supplierInvitationsError = ref<string | null>(null);
const supplierInvitationsSorted = computed(() =>
  [...supplierInvitations.value].sort((a, b) => Number(b.needsResponse) - Number(a.needsResponse))
);
const supplierPendingCount = computed(() => supplierInvitations.value.filter((inv) => inv.needsResponse).length);

onMounted(() => {
  if (isSupplier.value) {
    loadSupplierInvitations();
  } else {
    loadProcurementData();
  }
});

watch(isSupplier, (value) => {
  if (value) {
    clearProcurementData();
    loadSupplierInvitations();
  } else {
    clearSupplierData();
    loadProcurementData();
  }
});

function loadProcurementData() {
  loadRfqs();
  loadPendingRfqLineItems();
}

function clearProcurementData() {
  rfqList.value = [];
  pagination.page = 1;
  pagination.total = 0;
  pagination.totalPages = 0;
  pendingRfqLineItems.value = [];
  pendingRfqLineItemsError.value = null;
}

function clearSupplierData() {
  supplierInvitations.value = [];
  supplierInvitationsError.value = null;
}

async function loadRfqs() {
  if (isSupplier.value) return;
  loading.value = true;
  try {
    const response = await fetchRfqs({
      ...filters,
      page: pagination.page,
      limit: pagination.limit,
    });
    rfqList.value = response.data;
    pagination.total = response.pagination.total;
    pagination.totalPages = response.pagination.totalPages;
  } catch (error) {
    notification.error(t("rfq.management.loadError"));
  } finally {
    loading.value = false;
  }
}

async function loadPendingRfqLineItems() {
  if (isSupplier.value) return;
  pendingRfqLineItemsLoading.value = true;
  pendingRfqLineItemsError.value = null;
  try {
    const response = await fetchPendingRfqLineItems({ status: 'pending' });
    pendingRfqLineItems.value = response.data;
  } catch (error: any) {
    console.error(t("rfq.management.pendingLineItems.loadError"), error);
    pendingRfqLineItemsError.value =
      error?.message || t("rfq.management.pendingLineItems.loadError");
  } finally {
    pendingRfqLineItemsLoading.value = false;
  }
}

async function loadSupplierInvitations() {
  supplierInvitationsLoading.value = true;
  supplierInvitationsError.value = null;
  try {
    supplierInvitations.value = await listSupplierRfqInvitations();
  } catch (error: any) {
    supplierInvitationsError.value = error?.message || t("rfq.management.loadError");
  } finally {
    supplierInvitationsLoading.value = false;
  }
}

function handleTabChange(tab: string) {
  if (isSupplier.value) return;
  filters.materialType = tab;
  resetFilters();
}

function resetFilters() {
  if (isSupplier.value) return;
  filters.status = undefined;
  filters.distributionCategory = undefined;
  filters.rfqType = undefined;
  pagination.page = 1;
  loadRfqs();
}

function handleSizeChange() {
  if (isSupplier.value) return;
  pagination.page = 1;
  loadRfqs();
}

function handleCurrentChange() {
  if (isSupplier.value) return;
  loadRfqs();
}

function navigateToCreate() {
  if (isSupplier.value) return;
  router.push("/rfq/create");
}

function viewRfq(id: number) {
  router.push(`/rfq/${id}`);
}

function editRfq(id: number, status: string) {
  if (isSupplier.value) return;

  // 只有草稿状态可编辑
  if (status !== 'draft') {
    notification.warning(t('rfq.management.cannotEditPublished'));
    return;
  }

  router.push(`/rfq/${id}/edit`);
}

async function deleteRfq(rfq: Rfq) {
  if (isSupplier.value) return;

  // 只有草稿状态可删除
  if (rfq.status !== 'draft') {
    notification.warning(t('rfq.management.cannotDeletePublished'));
    return;
  }

  try {
    await notification.confirm(t("rfq.management.deleteConfirm"), t("common.warning"));
  } catch {
    return;
  }

  await tableActions.runAction(
    async () => {
        await httpService.delete(`/api/rfq-workflow/${rfq.id}`, { silent: true });
    },
    {
      rows: [rfq],
      successMessage: t("rfq.management.deleteSuccess"),
      errorMessage: t("common.operationFailed"),
    },
  );
  await loadRfqs();
}

function getCategoryLabel(category: string | null): string {
  if (!category) return "-";
  return t(`rfq.distributionCategory.${category}`);
}

function getSubcategoryLabel(subcategory: string | null): string {
  if (!subcategory) return "-";
  return t(`rfq.distributionSubcategory.${subcategory}`);
}

function getStatusType(status: string): "success" | "info" | "warning" | "danger" {
  const statusMap: Record<string, "success" | "info" | "warning" | "danger"> = {
    draft: "info",
    published: "warning",
    in_progress: "info",
    closed: "success",
    cancelled: "danger",
  };
  return statusMap[status] || "info";
}

function translateRfqStatus(status: string | null | undefined): string {
  if (!status) return "-";
  const key = `rfq.status.${status}`;
  const translated = t(key);
  return translated === key ? status : translated;
}

function translateQuoteStatus(status: string | null | undefined): string {
  if (!status) return "-";
  let key = `rfq.quoteStatus.${status}`;
  let translated = t(key);
  if (translated === key) {
    key = `rfq.quotes.statuses.${status}`;
    translated = t(key);
  }
  if (translated === key) {
    key = `rfq.quote.statuses.${status}`;
    translated = t(key);
  }
  return translated === key ? status : translated;
}

function invitationTagType(invitation: SupplierRfqInvitationSummary): "success" | "info" | "warning" {
  if (invitation.needsResponse) {
    return "warning";
  }
  if (invitation.denialReason) {
    return "info";
  }
  return "success";
}

function formatDateTime(dateString: string | null): string {
  if (!dateString) return "-";
  return new Date(dateString).toLocaleString();
}

function formatDaysRemaining(value: number | null): string {
  if (value === null || value === undefined) {
    return "-";
  }
  return String(value);
}

function getLineItemStatusType(status: string): "success" | "info" | "warning" | "danger" {
  const statusMap: Record<string, "success" | "info" | "warning" | "danger"> = {
    draft: "info",
    pending_director: "warning",
    pending_po: "warning",
    completed: "success",
    rejected: "danger",
  };
  return statusMap[status] || "info";
}

function translateLineItemStatus(status: string, selectedQuoteId: number | null): string {
  if (status === 'draft' && selectedQuoteId) {
    const key = 'rfq.lineItemStatus.rejectedPendingResubmit';
    const translated = t(key);
    return translated === key ? '需重新处理' : translated;
  }

  const key = `rfq.lineItemStatus.${status}`;
  const translated = t(key);
  return translated === key ? status : translated;
}

function viewRfqLineItem(rfqId: number, lineItemId: number) {
  router.push({
    path: `/rfq/${rfqId}`,
    query: { lineItemId: lineItemId.toString() }
  });
}
</script>

<style scoped>
.rfq-management-view {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}



.material-tabs {
  margin-bottom: 24px;
}

.tab-label {
  display: flex;
  align-items: center;
  gap: 6px;
}

.pending-rfq-line-items-card {
  margin-bottom: 16px;
}

.pending-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.pending-subtitle {
  margin: 0;
  color: #6b7280;
  font-size: 0.9rem;
}

.pending-alert {
  margin-bottom: 12px;
}

.filter-card {
  margin-bottom: 16px;
}

.list-card {
  min-height: 400px;
}

.pagination-container {
  margin-top: 24px;
  display: flex;
  justify-content: flex-end;
}

.supplier-card {
  margin-bottom: 16px;
}

.supplier-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.supplier-subtitle {
  margin: 0;
  color: #6b7280;
  font-size: 0.9rem;
}

.supplier-alert {
  margin-bottom: 12px;
}
</style>


