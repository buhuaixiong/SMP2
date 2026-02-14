<template>
  <div class="whitelist-blacklist-container">
    <el-tabs v-model="activeTab" class="tabs">
      <!-- Whitelist Tab -->
      <el-tab-pane :label="t('whitelistBlacklist.tabs.whitelist')" name="whitelist">
        <div class="tab-content">
          <div class="header-actions">
            <h2>{{ t("whitelistBlacklist.whitelist.title") }}</h2>
            <el-button type="primary" @click="showAddWhitelistDialog">
              {{ t("whitelistBlacklist.whitelist.addButton") }}
            </el-button>
          </div>

          <el-table :data="whitelistData" border stripe v-loading="whitelistLoading">
            <el-table-column
              prop="supplier_code"
              :label="t('whitelistBlacklist.whitelist.table.supplierCode')"
              width="120"
            />
            <el-table-column
              prop="supplier_name"
              :label="t('whitelistBlacklist.whitelist.table.supplierName')"
              min-width="180"
            />
            <el-table-column
              prop="document_type"
              :label="t('whitelistBlacklist.whitelist.table.documentType')"
              width="150"
            >
              <template #default="{ row }">
                <el-tag>{{ getDocumentTypeLabel(row.document_type) }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column
              prop="reason"
              :label="t('whitelistBlacklist.whitelist.table.reason')"
              min-width="200"
              show-overflow-tooltip
            />
            <el-table-column
              prop="exempted_by_name"
              :label="t('whitelistBlacklist.whitelist.table.exemptedBy')"
              width="100"
            />
            <el-table-column
              prop="exempted_at"
              :label="t('whitelistBlacklist.whitelist.table.exemptedAt')"
              width="180"
            >
              <template #default="{ row }">
                {{ formatDate(row.exempted_at) }}
              </template>
            </el-table-column>
            <el-table-column
              prop="expires_at"
              :label="t('whitelistBlacklist.whitelist.table.expiresAt')"
              width="180"
            >
              <template #default="{ row }">
                {{
                  row.expires_at
                    ? formatDate(row.expires_at)
                    : t("whitelistBlacklist.whitelist.table.neverExpires")
                }}
              </template>
            </el-table-column>
            <el-table-column
              prop="is_active"
              :label="t('whitelistBlacklist.whitelist.table.status')"
              width="80"
            >
              <template #default="{ row }">
                <el-tag :type="row.is_active ? 'success' : 'info'">
                  {{
                    row.is_active
                      ? t("whitelistBlacklist.whitelist.table.active")
                      : t("whitelistBlacklist.whitelist.table.inactive")
                  }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column
              :label="t('whitelistBlacklist.whitelist.table.actions')"
              width="150"
              fixed="right"
            >
              <template #default="{ row }">
                <el-button
                  v-if="row.is_active"
                  link
                  type="warning"
                  size="small"
                  @click="handleDeactivateWhitelist(row.id)"
                >
                  {{ t("whitelistBlacklist.whitelist.table.deactivate") }}
                </el-button>
                <el-button link type="danger" size="small" @click="handleDeleteWhitelist(row.id)">
                  {{ t("whitelistBlacklist.whitelist.table.delete") }}
                </el-button>
              </template>
            </el-table-column>
          </el-table>
        </div>
      </el-tab-pane>

      <!-- Blacklist Tab -->
      <el-tab-pane :label="t('whitelistBlacklist.tabs.blacklist')" name="blacklist">
        <div class="tab-content">
          <div class="header-actions">
            <h2>{{ t("whitelistBlacklist.blacklist.title") }}</h2>
            <el-button type="primary" @click="showAddBlacklistDialog">
              {{ t("whitelistBlacklist.blacklist.addButton") }}
            </el-button>
          </div>

          <el-table :data="blacklistData" border stripe v-loading="blacklistLoading">
            <el-table-column
              prop="blacklist_type"
              :label="t('whitelistBlacklist.blacklist.table.blacklistType')"
              width="120"
            >
              <template #default="{ row }">
                <el-tag :type="row.blacklist_type === 'credit_code' ? 'warning' : 'info'">
                  {{
                    row.blacklist_type === "credit_code"
                      ? t("whitelistBlacklist.blacklist.table.creditCode")
                      : t("whitelistBlacklist.blacklist.table.email")
                  }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column
              prop="blacklist_value"
              :label="t('whitelistBlacklist.blacklist.table.blacklistValue')"
              min-width="200"
            />
            <el-table-column
              prop="severity"
              :label="t('whitelistBlacklist.blacklist.table.severity')"
              width="100"
            >
              <template #default="{ row }">
                <el-tag :type="getSeverityType(row.severity)">
                  {{ getSeverityLabel(row.severity) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column
              prop="reason"
              :label="t('whitelistBlacklist.blacklist.table.reason')"
              min-width="200"
              show-overflow-tooltip
            />
            <el-table-column
              prop="notes"
              :label="t('whitelistBlacklist.blacklist.table.notes')"
              min-width="150"
              show-overflow-tooltip
            />
            <el-table-column
              prop="added_by_name"
              :label="t('whitelistBlacklist.blacklist.table.addedBy')"
              width="100"
            />
            <el-table-column
              prop="added_at"
              :label="t('whitelistBlacklist.blacklist.table.addedAt')"
              width="180"
            >
              <template #default="{ row }">
                {{ formatDate(row.added_at) }}
              </template>
            </el-table-column>
            <el-table-column
              prop="expires_at"
              :label="t('whitelistBlacklist.blacklist.table.expiresAt')"
              width="180"
            >
              <template #default="{ row }">
                {{
                  row.expires_at
                    ? formatDate(row.expires_at)
                    : t("whitelistBlacklist.blacklist.table.neverExpires")
                }}
              </template>
            </el-table-column>
            <el-table-column
              prop="is_active"
              :label="t('whitelistBlacklist.blacklist.table.status')"
              width="80"
            >
              <template #default="{ row }">
                <el-tag :type="row.is_active ? 'danger' : 'info'">
                  {{
                    row.is_active
                      ? t("whitelistBlacklist.blacklist.table.active")
                      : t("whitelistBlacklist.blacklist.table.inactive")
                  }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column
              :label="t('whitelistBlacklist.blacklist.table.actions')"
              width="150"
              fixed="right"
            >
              <template #default="{ row }">
                <el-button
                  v-if="row.is_active"
                  link
                  type="warning"
                  size="small"
                  @click="handleDeactivateBlacklist(row.id)"
                >
                  {{ t("whitelistBlacklist.blacklist.table.deactivate") }}
                </el-button>
                <el-button link type="danger" size="small" @click="handleDeleteBlacklist(row.id)">
                  {{ t("whitelistBlacklist.blacklist.table.delete") }}
                </el-button>
              </template>
            </el-table-column>
          </el-table>
        </div>
      </el-tab-pane>
    </el-tabs>

    <!-- Supplier Selector Dialog -->
    <el-dialog
      v-model="supplierSelectorVisible"
      :title="t('whitelistBlacklist.whitelist.supplierSelector.title')"
      width="800px"
    >
      <div class="supplier-selector">
        <el-input
          v-model="supplierSearchKeyword"
          :placeholder="t('whitelistBlacklist.whitelist.supplierSelector.searchPlaceholder')"
          clearable
          @keyup.enter="handleSupplierSearch"
          style="margin-bottom: 16px"
        >
          <template #append>
            <el-button :icon="Search" @click="handleSupplierSearch">
              {{ t('whitelistBlacklist.whitelist.supplierSelector.search') }}
            </el-button>
          </template>
        </el-input>

        <el-table
          :data="supplierSearchResults"
          v-loading="supplierSearchLoading"
          border
          stripe
          highlight-current-row
          @row-click="handleSupplierSelect"
          style="cursor: pointer"
        >
          <el-table-column
            prop="companyId"
            :label="t('whitelistBlacklist.whitelist.supplierSelector.supplierCode')"
            width="150"
          />
          <el-table-column
            prop="companyName"
            :label="t('whitelistBlacklist.whitelist.supplierSelector.supplierName')"
            min-width="200"
          />
          <el-table-column
            prop="stage"
            :label="t('whitelistBlacklist.whitelist.supplierSelector.stage')"
            width="120"
          >
            <template #default="{ row }">
              <el-tag :type="row.stage === 'formal_supplier' ? 'success' : 'warning'">
                {{ getStageLabel(row.stage) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column
            prop="category"
            :label="t('whitelistBlacklist.whitelist.supplierSelector.category')"
            width="120"
            show-overflow-tooltip
          />
        </el-table>

        <el-pagination
          v-model:current-page="supplierSearchPage"
          :page-size="supplierSearchPageSize"
          :total="supplierSearchTotal"
          layout="total, prev, pager, next"
          @current-change="handleSupplierPageChange"
          style="margin-top: 16px; text-align: right"
        />
      </div>

      <template #footer>
        <el-button @click="supplierSelectorVisible = false">
          {{ t('whitelistBlacklist.whitelist.supplierSelector.cancel') }}
        </el-button>
      </template>
    </el-dialog>

    <!-- Add Whitelist Dialog -->
    <el-dialog
      v-model="whitelistDialogVisible"
      :title="t('whitelistBlacklist.whitelist.dialog.title')"
      width="600px"
    >
      <el-form
        :model="whitelistForm"
        :rules="whitelistRules"
        ref="whitelistFormRef"
        label-width="120px"
      >
        <el-form-item
          :label="t('whitelistBlacklist.whitelist.dialog.supplier')"
          prop="supplier_id"
          required
        >
          <div style="display: flex; align-items: center; width: 100%">
            <el-input
              :value="
                whitelistForm.supplier_name
                  ? `${whitelistForm.supplier_name} (${whitelistForm.supplier_code})`
                  : ''
              "
              :placeholder="t('whitelistBlacklist.whitelist.dialog.selectSupplier')"
              readonly
              style="flex: 1"
            />
            <el-button
              type="primary"
              @click="openSupplierSelector"
              style="margin-left: 8px"
            >
              {{ t('whitelistBlacklist.whitelist.dialog.chooseSupplier') }}
            </el-button>
          </div>
        </el-form-item>
        <el-form-item
          :label="t('whitelistBlacklist.whitelist.dialog.documentType')"
          prop="document_type"
        >
          <el-select
            v-model="whitelistForm.document_type"
            :placeholder="t('whitelistBlacklist.whitelist.dialog.selectDocumentType')"
            style="width: 100%"
          >
            <el-option
              :label="t('whitelistBlacklist.whitelist.documentTypes.quality_assurance_agreement')"
              value="quality_assurance_agreement"
            />
            <el-option
              :label="t('whitelistBlacklist.whitelist.documentTypes.quality_compensation_agreement')"
              value="quality_compensation_agreement"
            />
            <el-option
              :label="t('whitelistBlacklist.whitelist.documentTypes.quality_kpi_targets')"
              value="quality_kpi_targets"
            />
            <el-option
              :label="t('whitelistBlacklist.whitelist.documentTypes.incoming_packaging_transport_agreement')"
              value="incoming_packaging_transport_agreement"
            />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('whitelistBlacklist.whitelist.dialog.reason')" prop="reason">
          <el-input
            v-model="whitelistForm.reason"
            type="textarea"
            :rows="3"
            :placeholder="t('whitelistBlacklist.whitelist.dialog.reasonPlaceholder')"
          />
        </el-form-item>
        <el-form-item :label="t('whitelistBlacklist.whitelist.dialog.expiresAt')">
          <el-date-picker
            v-model="whitelistForm.expires_at"
            type="datetime"
            :placeholder="t('whitelistBlacklist.whitelist.dialog.selectExpirationDate')"
            format="YYYY-MM-DD HH:mm:ss"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="whitelistDialogVisible = false">{{
          t("whitelistBlacklist.whitelist.dialog.cancel")
        }}</el-button>
        <el-button type="primary" @click="submitWhitelistForm" :loading="submittingWhitelist">
          {{ t("whitelistBlacklist.whitelist.dialog.submit") }}
        </el-button>
      </template>
    </el-dialog>

    <!-- Add Blacklist Dialog -->
    <el-dialog
      v-model="blacklistDialogVisible"
      :title="t('whitelistBlacklist.blacklist.dialog.title')"
      width="600px"
    >
      <el-form
        :model="blacklistForm"
        :rules="blacklistRules"
        ref="blacklistFormRef"
        label-width="120px"
      >
        <el-form-item
          :label="t('whitelistBlacklist.blacklist.dialog.blacklistType')"
          prop="blacklist_type"
        >
          <el-select
            v-model="blacklistForm.blacklist_type"
            :placeholder="t('whitelistBlacklist.blacklist.dialog.selectBlacklistType')"
          >
            <el-option
              :label="t('whitelistBlacklist.blacklist.dialog.creditCode')"
              value="credit_code"
            />
            <el-option :label="t('whitelistBlacklist.blacklist.dialog.email')" value="email" />
          </el-select>
        </el-form-item>
        <el-form-item
          :label="t('whitelistBlacklist.blacklist.dialog.blacklistValue')"
          prop="blacklist_value"
        >
          <el-input
            v-model="blacklistForm.blacklist_value"
            :placeholder="getBlacklistValuePlaceholder()"
          />
        </el-form-item>
        <el-form-item :label="t('whitelistBlacklist.blacklist.dialog.severity')" prop="severity">
          <el-select
            v-model="blacklistForm.severity"
            :placeholder="t('whitelistBlacklist.blacklist.dialog.selectSeverity')"
          >
            <el-option
              :label="t('whitelistBlacklist.blacklist.dialog.critical')"
              value="critical"
            />
            <el-option :label="t('whitelistBlacklist.blacklist.dialog.high')" value="high" />
            <el-option :label="t('whitelistBlacklist.blacklist.dialog.medium')" value="medium" />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('whitelistBlacklist.blacklist.dialog.reason')" prop="reason">
          <el-input
            v-model="blacklistForm.reason"
            type="textarea"
            :rows="3"
            :placeholder="t('whitelistBlacklist.blacklist.dialog.reasonPlaceholder')"
          />
        </el-form-item>
        <el-form-item :label="t('whitelistBlacklist.blacklist.dialog.notes')">
          <el-input
            v-model="blacklistForm.notes"
            type="textarea"
            :rows="2"
            :placeholder="t('whitelistBlacklist.blacklist.dialog.notesPlaceholder')"
          />
        </el-form-item>
        <el-form-item :label="t('whitelistBlacklist.blacklist.dialog.expiresAt')">
          <el-date-picker
            v-model="blacklistForm.expires_at"
            type="datetime"
            :placeholder="t('whitelistBlacklist.blacklist.dialog.selectExpirationDate')"
            format="YYYY-MM-DD HH:mm:ss"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="blacklistDialogVisible = false">{{
          t("whitelistBlacklist.blacklist.dialog.cancel")
        }}</el-button>
        <el-button type="primary" @click="submitBlacklistForm" :loading="submittingBlacklist">
          {{ t("whitelistBlacklist.blacklist.dialog.submit") }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">



import { ref, onMounted, computed } from "vue";
import { useI18n } from "vue-i18n";
import { type FormInstance, type FormRules } from "element-plus";
import { Search } from "@element-plus/icons-vue";
import {
  fetchWhitelistExemptions,
  addWhitelistExemption,
  deleteWhitelistExemption,
  deactivateWhitelistExemption,
  fetchBlacklistEntries,
  addBlacklistEntry,
  deleteBlacklistEntry,
  deactivateBlacklistEntry,
  searchSuppliers,
  type WhitelistExemption,
  type BlacklistEntry,
  type AddWhitelistExemptionPayload,
  type AddBlacklistEntryPayload,
  type SupplierSearchResult,
} from "@/api/whitelistBlacklist";

import { useNotification } from "@/composables";

defineOptions({ name: "SupplierWhitelistBlacklistView" });

const { t } = useI18n();
const notification = useNotification();

// ============================================================
// State
// ============================================================

const activeTab = ref("whitelist");

// Whitelist State
const whitelistData = ref<WhitelistExemption[]>([]);
const whitelistLoading = ref(false);
const whitelistDialogVisible = ref(false);
const submittingWhitelist = ref(false);
const whitelistFormRef = ref<FormInstance>();
const whitelistForm = ref<AddWhitelistExemptionPayload & {
  supplier_code?: string;
  supplier_name?: string;
}>({
  supplier_id: 0,
  document_type: "quality_assurance_agreement",
  reason: "",
  expires_at: null,
  supplier_code: "",
  supplier_name: "",
});

// Supplier Selector State
const supplierSelectorVisible = ref(false);
const supplierSearchKeyword = ref("");
const supplierSearchResults = ref<SupplierSearchResult[]>([]);
const supplierSearchLoading = ref(false);
const supplierSearchTotal = ref(0);
const supplierSearchPage = ref(1);
const supplierSearchPageSize = ref(10);
const selectedSupplier = ref<SupplierSearchResult | null>(null);

const whitelistRules: FormRules = {
  supplier_id: [
    {
      required: true,
      message: t("whitelistBlacklist.whitelist.validation.supplierIdRequired"),
      trigger: "blur",
    },
  ],
  document_type: [
    {
      required: true,
      message: t("whitelistBlacklist.whitelist.validation.documentTypeRequired"),
      trigger: "change",
    },
  ],
  reason: [
    {
      required: true,
      message: t("whitelistBlacklist.whitelist.validation.reasonRequired"),
      trigger: "blur",
    },
  ],
};

// Blacklist State
const blacklistData = ref<BlacklistEntry[]>([]);
const blacklistLoading = ref(false);
const blacklistDialogVisible = ref(false);
const submittingBlacklist = ref(false);
const blacklistFormRef = ref<FormInstance>();
const blacklistForm = ref<AddBlacklistEntryPayload>({
  blacklist_type: "credit_code",
  blacklist_value: "",
  reason: "",
  severity: "high",
  expires_at: null,
  notes: null,
});

const blacklistRules: FormRules = {
  blacklist_type: [
    {
      required: true,
      message: t("whitelistBlacklist.blacklist.validation.blacklistTypeRequired"),
      trigger: "change",
    },
  ],
  blacklist_value: [
    {
      required: true,
      message: t("whitelistBlacklist.blacklist.validation.blacklistValueRequired"),
      trigger: "blur",
    },
  ],
  reason: [
    {
      required: true,
      message: t("whitelistBlacklist.blacklist.validation.reasonRequired"),
      trigger: "blur",
    },
  ],
  severity: [
    {
      required: true,
      message: t("whitelistBlacklist.blacklist.validation.severityRequired"),
      trigger: "change",
    },
  ],
};

// ============================================================
// Lifecycle
// ============================================================

onMounted(() => {
  loadWhitelistData();
  loadBlacklistData();
});

// ============================================================
// Whitelist Methods
// ============================================================

const loadWhitelistData = async () => {
  whitelistLoading.value = true;
  try {
    const res = await fetchWhitelistExemptions();
    whitelistData.value = res.exemptions;
  } catch (error: any) {
    notification.error(error.message || t("whitelistBlacklist.whitelist.messages.loadFailed"));
  } finally {
    whitelistLoading.value = false;
  }
};

const showAddWhitelistDialog = () => {
  whitelistForm.value = {
    supplier_id: 0,
    document_type: "quality_assurance_agreement",
    reason: "",
    expires_at: null,
    supplier_code: "",
    supplier_name: "",
  };
  selectedSupplier.value = null;
  whitelistDialogVisible.value = true;
};

// Supplier Selector Methods
const openSupplierSelector = () => {
  supplierSelectorVisible.value = true;
  supplierSearchKeyword.value = "";
  supplierSearchPage.value = 1;
  loadSuppliers();
};

const loadSuppliers = async () => {
  supplierSearchLoading.value = true;
  try {
    const res = await searchSuppliers({
      q: supplierSearchKeyword.value,
      limit: supplierSearchPageSize.value,
      offset: (supplierSearchPage.value - 1) * supplierSearchPageSize.value,
    });
    supplierSearchResults.value = res.suppliers;
    supplierSearchTotal.value = res.total;
  } catch (error: any) {
    notification.error(error.message || "加载供应商列表失败");
  } finally {
    supplierSearchLoading.value = false;
  }
};

const handleSupplierSearch = () => {
  supplierSearchPage.value = 1;
  loadSuppliers();
};

const handleSupplierPageChange = (page: number) => {
  supplierSearchPage.value = page;
  loadSuppliers();
};

const handleSupplierSelect = (supplier: SupplierSearchResult) => {
  selectedSupplier.value = supplier;
  whitelistForm.value.supplier_id = supplier.id;
  whitelistForm.value.supplier_code = supplier.companyId;
  whitelistForm.value.supplier_name = supplier.companyName;
  supplierSelectorVisible.value = false;
  notification.success(`已选择供应商: ${supplier.companyName} (${supplier.companyId})`);
};

const getStageLabel = (stage: string) => {
  const stageMap: Record<string, string> = {
    potential: "潜在供应商",
    temp_supplier: "临时供应商",
    formal_supplier: "正式供应商",
    suspended: "已暂停",
    rejected: "已拒绝",
  };
  return stageMap[stage] || stage;
};

const submitWhitelistForm = async () => {
  if (!whitelistFormRef.value) return;

  await whitelistFormRef.value.validate(async (valid) => {
    if (!valid) return;

    submittingWhitelist.value = true;
    try {
      const payload = {
        ...whitelistForm.value,
        expires_at: whitelistForm.value.expires_at
          ? new Date(whitelistForm.value.expires_at).toISOString()
          : null,
      };
      await addWhitelistExemption(payload);
      notification.success(t("whitelistBlacklist.whitelist.messages.addSuccess"));
      whitelistDialogVisible.value = false;
      loadWhitelistData();
    } catch (error: any) {
      notification.error(error.message || t("whitelistBlacklist.whitelist.messages.addFailed"));
    } finally {
      submittingWhitelist.value = false;
    }
  });
};

const handleDeactivateWhitelist = async (id: number) => {
  try {
    await notification.confirm(
      t("whitelistBlacklist.whitelist.messages.deactivateConfirm"),
      t("whitelistBlacklist.whitelist.messages.confirmAction"),
      {
        confirmButtonText: t("whitelistBlacklist.whitelist.messages.confirm"),
        cancelButtonText: t("whitelistBlacklist.whitelist.messages.cancel"),
        type: "warning",
      },
    );

    await deactivateWhitelistExemption(id);
    notification.success(t("whitelistBlacklist.whitelist.messages.deactivateSuccess"));
    loadWhitelistData();
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error.message || t("whitelistBlacklist.whitelist.messages.deactivateFailed"));
    }
  }
};

const handleDeleteWhitelist = async (id: number) => {
  try {
    await notification.confirm(
      t("whitelistBlacklist.whitelist.messages.deleteConfirm"),
      t("whitelistBlacklist.whitelist.messages.confirmDelete"),
      {
        confirmButtonText: t("whitelistBlacklist.whitelist.messages.confirm"),
        cancelButtonText: t("whitelistBlacklist.whitelist.messages.cancel"),
        type: "warning",
      },
    );

    await deleteWhitelistExemption(id);
    notification.success(t("whitelistBlacklist.whitelist.messages.deleteSuccess"));
    loadWhitelistData();
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error.message || t("whitelistBlacklist.whitelist.messages.deleteFailed"));
    }
  }
};

// ============================================================
// Blacklist Methods
// ============================================================

const loadBlacklistData = async () => {
  blacklistLoading.value = true;
  try {
    const res = await fetchBlacklistEntries();
    blacklistData.value = res.entries;
  } catch (error: any) {
    notification.error(error.message || t("whitelistBlacklist.blacklist.messages.loadFailed"));
  } finally {
    blacklistLoading.value = false;
  }
};

const showAddBlacklistDialog = () => {
  blacklistForm.value = {
    blacklist_type: "credit_code",
    blacklist_value: "",
    reason: "",
    severity: "high",
    expires_at: null,
    notes: null,
  };
  blacklistDialogVisible.value = true;
};

const submitBlacklistForm = async () => {
  if (!blacklistFormRef.value) return;

  await blacklistFormRef.value.validate(async (valid) => {
    if (!valid) return;

    submittingBlacklist.value = true;
    try {
      const payload = {
        ...blacklistForm.value,
        expires_at: blacklistForm.value.expires_at
          ? new Date(blacklistForm.value.expires_at).toISOString()
          : null,
      };
      await addBlacklistEntry(payload);
      notification.success(t("whitelistBlacklist.blacklist.messages.addSuccess"));
      blacklistDialogVisible.value = false;
      loadBlacklistData();
    } catch (error: any) {
      notification.error(error.message || t("whitelistBlacklist.blacklist.messages.addFailed"));
    } finally {
      submittingBlacklist.value = false;
    }
  });
};

const handleDeactivateBlacklist = async (id: number) => {
  try {
    await notification.confirm(
      t("whitelistBlacklist.blacklist.messages.deactivateConfirm"),
      t("whitelistBlacklist.blacklist.messages.confirmAction"),
      {
        confirmButtonText: t("whitelistBlacklist.blacklist.messages.confirm"),
        cancelButtonText: t("whitelistBlacklist.blacklist.messages.cancel"),
        type: "warning",
      },
    );

    await deactivateBlacklistEntry(id);
    notification.success(t("whitelistBlacklist.blacklist.messages.deactivateSuccess"));
    loadBlacklistData();
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error.message || t("whitelistBlacklist.blacklist.messages.deactivateFailed"));
    }
  }
};

const handleDeleteBlacklist = async (id: number) => {
  try {
    await notification.confirm(
      t("whitelistBlacklist.blacklist.messages.deleteConfirm"),
      t("whitelistBlacklist.blacklist.messages.confirmDelete"),
      {
        confirmButtonText: t("whitelistBlacklist.blacklist.messages.confirm"),
        cancelButtonText: t("whitelistBlacklist.blacklist.messages.cancel"),
        type: "warning",
      },
    );

    await deleteBlacklistEntry(id);
    notification.success(t("whitelistBlacklist.blacklist.messages.deleteSuccess"));
    loadBlacklistData();
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error.message || t("whitelistBlacklist.blacklist.messages.deleteFailed"));
    }
  }
};

// ============================================================
// Helper Methods
// ============================================================

const getDocumentTypeLabel = (type: string) => {
  const typeKey = `whitelistBlacklist.whitelist.documentTypes.${type}`;
  const translated = t(typeKey);
  return translated !== typeKey ? translated : type;
};

const getSeverityType = (severity: string) => {
  const types: Record<string, "danger" | "warning" | "info"> = {
    critical: "danger",
    high: "warning",
    medium: "info",
  };
  return types[severity] || "info";
};

const getSeverityLabel = (severity: string) => {
  const severityKey = `whitelistBlacklist.blacklist.severityLevels.${severity}`;
  const translated = t(severityKey);
  return translated !== severityKey ? translated : severity;
};

const getBlacklistValuePlaceholder = () => {
  return blacklistForm.value.blacklist_type === "credit_code"
    ? t("whitelistBlacklist.blacklist.placeholders.creditCode")
    : t("whitelistBlacklist.blacklist.placeholders.email");
};

const formatDate = (dateString: string) => {
  if (!dateString) return "-";
  return new Date(dateString).toLocaleString("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
};



</script>

<style scoped>
.whitelist-blacklist-container {
  padding: 20px;
  background: #f5f7fa;
  min-height: calc(100vh - 120px);
}

.tabs {
  background: white;
  border-radius: 8px;
  padding: 20px;
}

.tab-content {
  margin-top: 20px;
}

.header-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.header-actions h2 {
  margin: 0;
  font-size: 18px;
  color: #303133;
}

.supplier-selector {
  min-height: 400px;
}

.supplier-selector :deep(.el-table__row) {
  cursor: pointer;
}

.supplier-selector :deep(.el-table__row:hover) {
  background-color: #f5f7fa;
}
</style>
