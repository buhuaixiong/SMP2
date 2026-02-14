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
          鎵归噺涓婁紶蹇呭鏂囦欢
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
        <span>宸查€夋嫨 {{ selectedSupplierIds.length }} 涓緵搴斿晢</span>
      </div>
      <div class="batch-actions">
        <el-button type="primary" size="small" @click="showBatchTagDialog = true">
          <el-icon><PriceTag /></el-icon>
          鎵归噺娣诲姞鏍囩
        </el-button>
        <el-button type="danger" size="small" plain @click="showBatchRemoveTagDialog = true">
          <el-icon><RemoveFilled /></el-icon>
          鎵归噺绉婚櫎鏍囩
        </el-button>
        <el-button v-if="isAdmin" type="success" size="small" @click="navigateToBulkDocumentImport">
          <el-icon><UploadFilled /></el-icon>
          鎵归噺涓婁紶鏂囨。
        </el-button>
        <el-button size="small" @click="clearSelection">
          娓呴櫎閫夋嫨
        </el-button>
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
            <!-- 浠呴噰璐憳/绠＄悊鍛樺彲瑙佺殑绠＄悊鎸夐挳 -->
            <template v-if="!isSupplierUser">
              <el-button size="small" type="warning" @click="sendProfileReminderEmail">
                <el-icon><Message /></el-icon>
                鎻愰啋瀹屽杽璧勬枡
              </el-button>
              <el-button size="small" type="danger" @click="sendExpiryReminderEmail">
                <el-icon><Bell /></el-icon>
                鎻愰啋鏂囦欢杩囨湡
              </el-button>
            </template>

            <!-- 浠呬緵搴斿晢鍙鐨勫揩鎹锋搷浣?-->
            <template v-else>
              <el-button size="small" type="primary" @click="navigateToChangeRequest">
                <el-icon><EditPen /></el-icon>
                鐢宠鍙樻洿璧勬枡
              </el-button>
              <el-button v-if="isTempSupplier" size="small" type="success" @click="navigateToUpgrade">
                <el-icon><TrendCharts /></el-icon>
                杞负姝ｅ紡渚涘簲鍟?              </el-button>
              <el-button size="small" @click="navigateToFileUpload">
                <el-icon><UploadFilled /></el-icon>
                涓婁紶/鏇存柊鏂囦欢
              </el-button>
            </template>

            <el-button size="small" @click="closeDetail">鍏抽棴</el-button>
          </div>
        </header>

        <div class="detail-content">
          <!-- 浠呬緵搴斿晢鍙鐨勭姸鎬佹彁绀?-->
          <el-alert
            v-if="isSupplierUser"
            type="info"
            :closable="false"
            show-icon
            class="supplier-notice"
          >
            <template #title>
              {{ isTempSupplier ? '涓存椂渚涘簲鍟嗚鏄? : '姝ｅ紡渚涘簲鍟嗚鏄? }}
            </template>
            <ul>
              <li v-if="isTempSupplier">鎮ㄥ綋鍓嶆槸涓存椂渚涘簲鍟嗭紝鏌愪簺鍔熻兘鍙楅檺</li>
              <li>濡傞渶淇敼璧勬枡锛岃閫氳繃"鐢宠鍙樻洿璧勬枡"鎻愪氦</li>
              <li v-if="isTempSupplier">瀹屾垚鎵€鏈夋枃妗ｄ笂浼犲悗鍙敵璇疯浆涓烘寮忎緵搴斿晢</li>
              <li>璧勬枡瀹屾暣搴? {{ Math.round(selectedSupplier.profileCompletion || 0) }}%</li>
            </ul>
          </el-alert>

          <!-- Profile Information Section -->
          <section class="detail-section">
            <h3 class="section-title">渚涘簲鍟嗚祫鏂?/h3>

            <div class="info-grid">
              <!-- Company Name -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.companyName, 'empty': !selectedSupplier.companyName }">
                <label>鍏徃鍚嶇О</label>
                <span v-if="selectedSupplier.companyName">{{ selectedSupplier.companyName }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Company ID -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.companyId, 'empty': !selectedSupplier.companyId }">
                <label>鍏徃ID/娉ㄥ唽鍙?/label>
                <span v-if="selectedSupplier.companyId">{{ selectedSupplier.companyId }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Contact Person -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.contactPerson, 'empty': !selectedSupplier.contactPerson }">
                <label>鑱旂郴浜?/label>
                <span v-if="selectedSupplier.contactPerson">{{ selectedSupplier.contactPerson }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Contact Phone -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.contactPhone, 'empty': !selectedSupplier.contactPhone }">
                <label>鑱旂郴鐢佃瘽</label>
                <span v-if="selectedSupplier.contactPhone">{{ selectedSupplier.contactPhone }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Contact Email -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.contactEmail, 'empty': !selectedSupplier.contactEmail }">
                <label>鑱旂郴閭</label>
                <span v-if="selectedSupplier.contactEmail">{{ selectedSupplier.contactEmail }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Category -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.category, 'empty': !selectedSupplier.category }">
                <label>涓氬姟绫诲埆</label>
                <span v-if="selectedSupplier.category">{{ selectedSupplier.category }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Address -->
              <div class="info-item full-width" :class="{ 'filled': selectedSupplier.address, 'empty': !selectedSupplier.address }">
                <label>閫氳鍦板潃</label>
                <span v-if="selectedSupplier.address">{{ selectedSupplier.address }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Business Registration Number -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.businessRegistrationNumber, 'empty': !selectedSupplier.businessRegistrationNumber }">
                <label>钀ヤ笟鎵х収鍙?/label>
                <span v-if="selectedSupplier.businessRegistrationNumber">{{ selectedSupplier.businessRegistrationNumber }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Payment Terms -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.paymentTerms, 'empty': !selectedSupplier.paymentTerms }">
                <label>浠樻鏉′欢</label>
                <span v-if="selectedSupplier.paymentTerms">{{ selectedSupplier.paymentTerms }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Payment Currency -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.paymentCurrency, 'empty': !selectedSupplier.paymentCurrency }">
                <label>浠樻甯佺</label>
                <span v-if="selectedSupplier.paymentCurrency">{{ selectedSupplier.paymentCurrency }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Bank Account -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.bankAccount, 'empty': !selectedSupplier.bankAccount }">
                <label>閾惰璐﹀彿</label>
                <span v-if="selectedSupplier.bankAccount">{{ selectedSupplier.bankAccount }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>

              <!-- Region -->
              <div class="info-item" :class="{ 'filled': selectedSupplier.region, 'empty': !selectedSupplier.region }">
                <label>鎵€灞炲湴鍖?/label>
                <span v-if="selectedSupplier.region">{{ selectedSupplier.region }}</span>
                <span v-else class="empty-text">鏈～鍐?/span>
              </div>
            </div>
          </section>

          <!-- Documents Section -->
          <section class="detail-section">
            <h3 class="section-title">鏂囦欢璧勬枡</h3>

            <div class="documents-list">
              <!-- Required Documents Status -->
              <div class="doc-requirement">
                <h4>蹇呭鏂囦欢</h4>
                <div class="required-docs">
                  <!-- Quality Compensation Agreement -->
                  <div class="doc-status" :class="hasDocument('quality_compensation_agreement') ? 'uploaded' : 'missing'">
                    <div class="doc-name">
                      <el-icon v-if="hasDocument('quality_compensation_agreement')"><CircleCheckFilled /></el-icon>
                      <el-icon v-else><CircleCloseFilled /></el-icon>
                      <span>璐ㄩ噺璧斿伩鍗忚</span>
                    </div>
                    <div v-if="getDocument('quality_compensation_agreement')" class="doc-info">
                      <span class="validity">鏈夋晥鏈? {{ formatDate(getDocument('quality_compensation_agreement').validFrom) }} - {{ formatDate(getDocument('quality_compensation_agreement').expiresAt) }}</span>
                    </div>
                    <div v-else class="doc-info">
                      <span class="missing-text">鏈笂浼?/span>
                    </div>
                  </div>

                  <!-- Incoming Packaging Transportation Agreement -->
                  <div class="doc-status" :class="hasDocument('packaging_transport_agreement') ? 'uploaded' : 'missing'">
                    <div class="doc-name">
                      <el-icon v-if="hasDocument('packaging_transport_agreement')"><CircleCheckFilled /></el-icon>
                      <el-icon v-else><CircleCloseFilled /></el-icon>
                      <span>鏉ユ枡鍖呰杩愯緭鍗忚</span>
                    </div>
                    <div v-if="getDocument('packaging_transport_agreement')" class="doc-info">
                      <span class="validity">鏈夋晥鏈? {{ formatDate(getDocument('packaging_transport_agreement').validFrom) }} - {{ formatDate(getDocument('packaging_transport_agreement').expiresAt) }}</span>
                    </div>
                    <div v-else class="doc-info">
                      <span class="missing-text">鏈笂浼?/span>
                    </div>
                  </div>

                  <!-- Quality Assurance Agreement -->
                  <div class="doc-status" :class="hasDocument('quality_assurance_agreement') ? 'uploaded' : 'missing'">
                    <div class="doc-name">
                      <el-icon v-if="hasDocument('quality_assurance_agreement')"><CircleCheckFilled /></el-icon>
                      <el-icon v-else><CircleCloseFilled /></el-icon>
                      <span>璐ㄩ噺淇濊瘉鍗忚</span>
                    </div>
                    <div v-if="getDocument('quality_assurance_agreement')" class="doc-info">
                      <span class="validity">鏈夋晥鏈? {{ formatDate(getDocument('quality_assurance_agreement').validFrom) }} - {{ formatDate(getDocument('quality_assurance_agreement').expiresAt) }}</span>
                    </div>
                    <div v-else class="doc-info">
                      <span class="missing-text">鏈笂浼?/span>
                    </div>
                  </div>

                  <!-- Quality KPI Annual Target -->
                  <div class="doc-status" :class="hasDocument('quality_kpi_annual_target') ? 'uploaded' : 'missing'">
                    <div class="doc-name">
                      <el-icon v-if="hasDocument('quality_kpi_annual_target')"><CircleCheckFilled /></el-icon>
                      <el-icon v-else><CircleCloseFilled /></el-icon>
                      <span>璐ㄩ噺KPI骞村害鐩爣</span>
                    </div>
                    <div v-if="getDocument('quality_kpi_annual_target')" class="doc-info">
                      <span class="validity">鏈夋晥鏈? {{ formatDate(getDocument('quality_kpi_annual_target').validFrom) }} - {{ formatDate(getDocument('quality_kpi_annual_target').expiresAt) }}</span>
                    </div>
                    <div v-else class="doc-info">
                      <span class="missing-text">鏈笂浼?/span>
                    </div>
                  </div>

                  <!-- Supplier Manual Template -->
                  <div class="doc-status" :class="hasDocument('supplier_manual_template') ? 'uploaded' : 'missing'">
                    <div class="doc-name">
                      <el-icon v-if="hasDocument('supplier_manual_template')"><CircleCheckFilled /></el-icon>
                      <el-icon v-else><CircleCloseFilled /></el-icon>
                      <span>渚涘簲鍟嗘墜鍐岃寖鏈?/span>
                    </div>
                    <div v-if="getDocument('supplier_manual_template')" class="doc-info">
                      <span class="validity">鏈夋晥鏈? {{ formatDate(getDocument('supplier_manual_template').validFrom) }} - {{ formatDate(getDocument('supplier_manual_template').expiresAt) }}</span>
                    </div>
                    <div v-else class="doc-info">
                      <span class="missing-text">鏈笂浼?/span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- All Uploaded Documents -->
              <div v-if="selectedSupplier.documents && selectedSupplier.documents.length > 0" class="all-documents">
                <h4>鎵€鏈夊凡涓婁紶鏂囦欢</h4>
                <div class="doc-list">
                  <div v-for="doc in selectedSupplier.documents" :key="doc.id" class="doc-item">
                    <div class="doc-header">
                      <span class="doc-type">{{ getDocTypeName(doc.docType) }}</span>
                      <span class="doc-filename">{{ doc.filename }}</span>
                    </div>
                    <div class="doc-dates">
                      <span v-if="doc.validFrom" class="valid-from">鐢熸晥鏃ユ湡: {{ formatDate(doc.validFrom) }}</span>
                      <span v-if="doc.expiresAt" class="expires-at" :class="{ 'expired': isExpired(doc), 'expiring': isExpiringSoon(doc) }">
                        鍒版湡鏃ユ湡: {{ formatDate(doc.expiresAt) }}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </section>
        </div>
      </aside>
    </div>

    <!-- Batch Add Tags Dialog -->
    <el-dialog v-model="showBatchTagDialog" title="鎵归噺娣诲姞鏍囩" width="500px">
      <div class="batch-tag-dialog">
        <el-alert
          title="鎻愮ず"
          type="info"
          :closable="false"
          style="margin-bottom: 16px"
        >
          宸查€夋嫨 {{ selectedSupplierIds.length }} 涓緵搴斿晢銆傞€夋嫨鏍囩鍚庡皢鎵归噺娣诲姞鍒拌繖浜涗緵搴斿晢銆?        </el-alert>

        <el-select
          v-model="selectedTagsToAdd"
          multiple
          placeholder="璇烽€夋嫨瑕佹坊鍔犵殑鏍囩"
          style="width: 100%"
          filterable
        >
          <el-option
            v-for="tag in availableTags"
            :key="tag.id"
            :label="tag.name"
            :value="tag.id"
          >
            <span :style="{ color: tag.color || '#409EFF' }">鈼?{{ tag.name }}</span>
          </el-option>
        </el-select>
      </div>

      <template #footer>
        <el-button @click="showBatchTagDialog = false">鍙栨秷</el-button>
        <el-button
          type="primary"
          :loading="batchTagLoading"
          :disabled="selectedTagsToAdd.length === 0"
          @click="handleBatchAddTags"
        >
          纭畾娣诲姞
        </el-button>
      </template>
    </el-dialog>

    <!-- Batch Remove Tags Dialog -->
    <el-dialog v-model="showBatchRemoveTagDialog" title="鎵归噺绉婚櫎鏍囩" width="500px">
      <div class="batch-tag-dialog">
        <el-alert
          title="鎻愮ず"
          type="warning"
          :closable="false"
          style="margin-bottom: 16px"
        >
          宸查€夋嫨 {{ selectedSupplierIds.length }} 涓緵搴斿晢銆傞€夋嫨鏍囩鍚庡皢浠庤繖浜涗緵搴斿晢涓Щ闄ゃ€?        </el-alert>

        <el-select
          v-model="selectedTagsToRemove"
          multiple
          placeholder="璇烽€夋嫨瑕佺Щ闄ょ殑鏍囩"
          style="width: 100%"
          filterable
        >
          <el-option
            v-for="tag in availableTags"
            :key="tag.id"
            :label="tag.name"
            :value="tag.id"
          >
            <span :style="{ color: tag.color || '#409EFF' }">鈼?{{ tag.name }}</span>
          </el-option>
        </el-select>
      </div>

      <template #footer>
        <el-button @click="showBatchRemoveTagDialog = false">鍙栨秷</el-button>
        <el-button
          type="danger"
          :loading="batchTagLoading"
          :disabled="selectedTagsToRemove.length === 0"
          @click="handleBatchRemoveTags"
        >
          纭畾绉婚櫎
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useRouter } from "vue-router";
import { ElMessage } from "element-plus";
import { useI18n } from "vue-i18n";
import { Check, PriceTag, RemoveFilled, CircleCheckFilled, CircleCloseFilled, Message, Bell, UploadFilled, EditPen, TrendCharts } from "@element-plus/icons-vue";

import DirectoryHeader from "@/components/supplier-directory/DirectoryHeader.vue";
import AdvancedFiltersPanel from "@/components/supplier-directory/AdvancedFiltersPanel.vue";
import ActiveFilterChips, {
  type ActiveChip,
} from "@/components/supplier-directory/ActiveFilterChips.vue";
import SummaryCards from "@/components/supplier-directory/SummaryCards.vue";
import SupplierTable from "@/components/supplier-directory/SupplierTable.vue";

import { useSupplierDirectoryFilters } from "@/composables/useSupplierDirectoryFilters";
import { DEFAULT_PAGE_SIZES } from "@/composables/usePagination";

import { useSupplierStore } from "@/stores/supplier";
import { useAuthStore } from "@/stores/auth";

import * as suppliersApi from "@/api/suppliers";
import { apiFetch } from "@/api/http";
import type { Supplier } from "@/types";
import { SupplierStage, SupplierStatus } from "@/types";


const { t } = useI18n();
const router = useRouter();
const supplierStore = useSupplierStore();
const authStore = useAuthStore();

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
      const status =
        supplier.completionStatus ?? supplier.complianceSummary?.completionCategory;
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
  pageSizeValue.value > 0
    ? Math.max(1, Math.ceil(totalSuppliers.value / pageSizeValue.value))
    : 1,
);

const pageStart = computed(() =>
  totalSuppliers.value === 0 ? 0 : pagination.value.offset + 1,
);

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
  if (key === "q") {
    filters.q = "";
    return;
  }
  if (key === "status") {
    filters.status = "";
    return;
  }
  if (key === "stage") {
    filters.stage = "";
    return;
  }
  if (key === "category") {
    filters.category = "";
    return;
  }
  if (key === "region") {
    filters.region = "";
    return;
  }
  if (key === "importance") {
    filters.importance = "";
    return;
  }
  if (key === "tag") {
    filters.tag = "";
    return;
  }
  if (key === "completionStatus") {
    filters.completionStatus = "";
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

const isTempSupplier = computed(() => authStore.user?.role === 'temp_supplier');

// Navigation methods for supplier quick actions
const navigateToChangeRequest = () => {
  router.push('/supplier/change-requests');
};

const navigateToUpgrade = () => {
  router.push('/supplier/upgrade');
};

const navigateToFileUpload = () => {
  router.push('/supplier/file-uploads');
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
  supplierStore.selectSupplier(null);
};

const hasDocument = (docType: string) => {
  if (!selectedSupplier.value?.documents) return false;
  return selectedSupplier.value.documents.some((doc: any) => doc.docType === docType);
};

const getDocument = (docType: string) => {
  if (!selectedSupplier.value?.documents) return null;
  return selectedSupplier.value.documents.find((doc: any) => doc.docType === docType);
};

const formatDate = (date: string | null | undefined) => {
  if (!date) return 'N/A';
  const d = new Date(date);
  return d.toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' });
};

const isExpired = (doc: any) => {
  if (!doc.expiresAt) return false;
  return new Date(doc.expiresAt) < new Date();
};

const isExpiringSoon = (doc: any) => {
  if (!doc.expiresAt) return false;
  const expiryDate = new Date(doc.expiresAt);
  const today = new Date();
  const daysUntilExpiry = Math.floor((expiryDate.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
  return daysUntilExpiry > 0 && daysUntilExpiry <= 30;
};

const getDocTypeName = (docType: string) => {
  const typeMap: Record<string, string> = {
    'business_license': '钀ヤ笟鎵х収',
    'tax_certificate': '绋庡姟鐧昏璇?,
    'bank_information': '閾惰璧勬枡',
    'quality_certificate': '璐ㄩ噺璁よ瘉',
    'quality_compensation_agreement': '璐ㄩ噺璧斿伩鍗忚',
    'packaging_transport_agreement': '鏉ユ枡鍖呰杩愯緭鍗忚',
    'quality_assurance_agreement': '璐ㄩ噺淇濊瘉鍗忚',
    'quality_kpi_annual_target': '璐ㄩ噺KPI骞村害鐩爣',
    'supplier_manual_template': '渚涘簲鍟嗘墜鍐岃寖鏈?,
    'other': '鍏朵粬鏂囦欢'
  };
  return typeMap[docType] || docType;
};

// Email Reminder Functions
const sendProfileReminderEmail = async () => {
  if (!selectedSupplier.value) return;

  // Check if supplier has contact email
  if (!selectedSupplier.value.contactEmail) {
    ElMessage.error('璇ヤ緵搴斿晢鏈～鍐欒仈绯婚偖绠憋紝鏃犳硶鍙戦€佹彁閱掗偖浠?);
    return;
  }

  try {
    // Check for missing profile fields and documents
    const missingFields: string[] = [];
    const requiredFields = [
      { key: 'companyName', label: '鍏徃鍚嶇О' },
      { key: 'companyId', label: '鍏徃ID/娉ㄥ唽鍙? },
      { key: 'contactPerson', label: '鑱旂郴浜? },
      { key: 'contactPhone', label: '鑱旂郴鐢佃瘽' },
      { key: 'contactEmail', label: '鑱旂郴閭' },
      { key: 'category', label: '涓氬姟绫诲埆' },
      { key: 'address', label: '閫氳鍦板潃' },
      { key: 'businessRegistrationNumber', label: '钀ヤ笟鎵х収鍙? },
      { key: 'paymentTerms', label: '浠樻鏉′欢' },
      { key: 'paymentCurrency', label: '浠樻甯佺' },
      { key: 'bankAccount', label: '閾惰璐﹀彿' },
      { key: 'region', label: '鎵€灞炲湴鍖? }
    ];

    requiredFields.forEach(field => {
      if (!selectedSupplier.value![field.key as keyof typeof selectedSupplier.value]) {
        missingFields.push(field.label);
      }
    });

    const requiredDocs = [
      { type: 'quality_compensation_agreement', label: '璐ㄩ噺璧斿伩鍗忚' },
      { type: 'packaging_transport_agreement', label: '鏉ユ枡鍖呰杩愯緭鍗忚' },
      { type: 'quality_assurance_agreement', label: '璐ㄩ噺淇濊瘉鍗忚' },
      { type: 'quality_kpi_annual_target', label: '璐ㄩ噺KPI骞村害鐩爣' },
      { type: 'supplier_manual_template', label: '渚涘簲鍟嗘墜鍐岃寖鏈? }
    ];

    const missingDocs: string[] = [];
    requiredDocs.forEach(doc => {
      if (!hasDocument(doc.type)) {
        missingDocs.push(doc.label);
      }
    });

    if (missingFields.length === 0 && missingDocs.length === 0) {
      ElMessage.info('璇ヤ緵搴斿晢璧勬枡宸插畬鏁达紝鏃犻渶鎻愰啋');
      return;
    }

    // Show loading message
    const loadingMessage = ElMessage({
      message: `姝ｅ湪鍙戦€佹彁閱掗偖浠惰嚦 ${selectedSupplier.value.contactEmail}...`,
      type: 'info',
      duration: 0,
      icon: Message
    });

    // Send reminder email via API
    const response = await apiFetch<{ success: boolean; message?: string; emailSent?: boolean }>(`/suppliers/${selectedSupplier.value.id}/send-profile-reminder`, {
      method: 'POST',
      body: JSON.stringify({
        recipientEmail: selectedSupplier.value.contactEmail,
        recipientName: selectedSupplier.value.contactPerson || selectedSupplier.value.companyName,
        companyName: selectedSupplier.value.companyName,
        missingFields,
        missingDocuments: missingDocs
      })
    });

    loadingMessage.close();

    if (response.success || response.emailSent) {
      ElMessage.success({
        message: `鎻愰啋閭欢宸叉垚鍔熷彂閫佽嚦 ${selectedSupplier.value.contactEmail}`,
        duration: 5000,
        showClose: true
      });
    } else {
      ElMessage.warning({
        message: response.message || '閭欢鍙戦€佺姸鎬佹湭鐭ワ紝璇锋鏌ラ偖浠舵湇鍔″櫒閰嶇疆',
        duration: 5000,
        showClose: true
      });
    }
  } catch (error: any) {
    console.error('Failed to send profile reminder:', error);

    // Parse error message
    let errorMessage = '鍙戦€佹彁閱掗偖浠跺け璐?;
    if (error.message) {
      if (error.message.includes('Email service not configured')) {
        errorMessage = '閭欢鏈嶅姟鏈厤缃紝璇疯仈绯荤郴缁熺鐞嗗憳璁剧疆SMTP鏈嶅姟鍣?;
      } else if (error.message.includes('Invalid email')) {
        errorMessage = '渚涘簲鍟嗛偖绠卞湴鍧€鏃犳晥锛岃鍏堟洿鏂拌仈绯婚偖绠?;
      } else if (error.message.includes('Network')) {
        errorMessage = '缃戠粶杩炴帴澶辫触锛岃妫€鏌ョ綉缁滃悗閲嶈瘯';
      } else {
        errorMessage = `鍙戦€佸け璐ワ細${error.message}`;
      }
    }

    ElMessage.error({
      message: errorMessage,
      duration: 5000,
      showClose: true
    });
  }
};

const sendExpiryReminderEmail = async () => {
  if (!selectedSupplier.value?.documents) return;

  // Check if supplier has contact email
  if (!selectedSupplier.value.contactEmail) {
    ElMessage.error('璇ヤ緵搴斿晢鏈～鍐欒仈绯婚偖绠憋紝鏃犳硶鍙戦€佹彁閱掗偖浠?);
    return;
  }

  try {
    // Find documents that are expired or expiring soon
    const expiringDocs: Array<{ name: string; expiryDate: string; status: string; filename?: string }> = [];

    selectedSupplier.value.documents.forEach((doc: any) => {
      if (doc.expiresAt) {
        if (isExpired(doc)) {
          expiringDocs.push({
            name: getDocTypeName(doc.docType),
            filename: doc.filename,
            expiryDate: formatDate(doc.expiresAt),
            status: '宸茶繃鏈?
          });
        } else if (isExpiringSoon(doc)) {
          expiringDocs.push({
            name: getDocTypeName(doc.docType),
            filename: doc.filename,
            expiryDate: formatDate(doc.expiresAt),
            status: '鍗冲皢杩囨湡'
          });
        }
      }
    });

    if (expiringDocs.length === 0) {
      ElMessage.info('璇ヤ緵搴斿晢鏆傛棤鍗冲皢杩囨湡鎴栧凡杩囨湡鐨勬枃浠?);
      return;
    }

    // Show loading message
    const loadingMessage = ElMessage({
      message: `姝ｅ湪鍙戦€佹枃浠惰繃鏈熸彁閱掗偖浠惰嚦 ${selectedSupplier.value.contactEmail}...`,
      type: 'info',
      duration: 0,
      icon: Bell
    });

    // Send expiry reminder email via API
    const response = await apiFetch<{ success: boolean; message?: string; emailSent?: boolean }>(`/suppliers/${selectedSupplier.value.id}/send-expiry-reminder`, {
      method: 'POST',
      body: JSON.stringify({
        recipientEmail: selectedSupplier.value.contactEmail,
        recipientName: selectedSupplier.value.contactPerson || selectedSupplier.value.companyName,
        companyName: selectedSupplier.value.companyName,
        expiringDocuments: expiringDocs
      })
    });

    loadingMessage.close();

    if (response.success || response.emailSent) {
      const expiredCount = expiringDocs.filter(doc => doc.status === '宸茶繃鏈?).length;
      const expiringCount = expiringDocs.filter(doc => doc.status === '鍗冲皢杩囨湡').length;

      let detailMessage = `鏂囦欢杩囨湡鎻愰啋閭欢宸叉垚鍔熷彂閫佽嚦 ${selectedSupplier.value.contactEmail}\n`;
      if (expiredCount > 0) detailMessage += `宸茶繃鏈熸枃浠讹細${expiredCount}涓猏n`;
      if (expiringCount > 0) detailMessage += `鍗冲皢杩囨湡鏂囦欢锛?{expiringCount}涓猔;

      ElMessage.success({
        message: detailMessage,
        duration: 5000,
        showClose: true,
        dangerouslyUseHTMLString: false
      });
    } else {
      ElMessage.warning({
        message: response.message || '閭欢鍙戦€佺姸鎬佹湭鐭ワ紝璇锋鏌ラ偖浠舵湇鍔″櫒閰嶇疆',
        duration: 5000,
        showClose: true
      });
    }
  } catch (error: any) {
    console.error('Failed to send expiry reminder:', error);

    // Parse error message
    let errorMessage = '鍙戦€佹彁閱掗偖浠跺け璐?;
    if (error.message) {
      if (error.message.includes('Email service not configured')) {
        errorMessage = '閭欢鏈嶅姟鏈厤缃紝璇疯仈绯荤郴缁熺鐞嗗憳璁剧疆SMTP鏈嶅姟鍣?;
      } else if (error.message.includes('Invalid email')) {
        errorMessage = '渚涘簲鍟嗛偖绠卞湴鍧€鏃犳晥锛岃鍏堟洿鏂拌仈绯婚偖绠?;
      } else if (error.message.includes('Network')) {
        errorMessage = '缃戠粶杩炴帴澶辫触锛岃妫€鏌ョ綉缁滃悗閲嶈瘯';
      } else {
        errorMessage = `鍙戦€佸け璐ワ細${error.message}`;
      }
    }

    ElMessage.error({
      message: errorMessage,
      duration: 5000,
      showClose: true
    });
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
      ElMessage.success(
        `鎴愬姛涓?${selectedSupplierIds.value.length} 涓緵搴斿晢娣诲姞浜?${successCount} 涓爣绛綻
      );
      await supplierStore.fetchSuppliers();
      selectedTagsToAdd.value = [];
      showBatchTagDialog.value = false;
      clearSelection();
    }

    if (errorCount > 0) {
      ElMessage.warning(`${errorCount} 涓爣绛炬坊鍔犲け璐);
    }
  } catch (error) {
    console.error("Batch add tags error:", error);
    ElMessage.error("鎵归噺娣诲姞鏍囩澶辫触");
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
      ElMessage.success(
        `鎴愬姛浠?${selectedSupplierIds.value.length} 涓緵搴斿晢涓Щ闄や簡 ${successCount} 涓爣绛綻
      );
      await supplierStore.fetchSuppliers();
      selectedTagsToRemove.value = [];
      showBatchRemoveTagDialog.value = false;
      clearSelection();
    }

    if (errorCount > 0) {
      ElMessage.warning(`${errorCount} 涓爣绛剧Щ闄ゅけ璐);
    }
  } catch (error) {
    console.error("Batch remove tags error:", error);
    ElMessage.error("鎵归噺绉婚櫎鏍囩澶辫触");
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
</style>

