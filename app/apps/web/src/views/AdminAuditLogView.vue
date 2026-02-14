<template>
  <div class="audit-log-viewer">
    <PageHeader
      :title="t('adminAuditLog.title')"
      :subtitle="t('adminAuditLog.subtitle')"
    >
      <template #actions>
        <div class="header-actions">
          <button class="link-btn" type="button" @click="refresh">
            {{ t("common.refresh") }}
          </button>
          <button v-if="hasFilters" class="link-btn" type="button" @click="clearFilters">
            {{ t("adminAuditLog.actions.clearFilters") }}
          </button>
        </div>
      </template>
    </PageHeader>

    <section class="panel filters-panel">
      <h2>{{ t("adminAuditLog.filters.advancedTitle") }}</h2>
      <div class="filter-grid">
        <label>
          {{ t("adminAuditLog.filters.actorId") }}
          <input
            v-model="filters.actorId"
            type="text"
            :placeholder="t('adminAuditLog.filters.placeholders.actorId')"
            @input="debouncedApplyFilters"
          />
        </label>
        <label>
          {{ t("adminAuditLog.filters.ipAddress") }}
          <input
            v-model="filters.ipAddress"
            type="text"
            :placeholder="t('adminAuditLog.filters.placeholders.ipAddress')"
            @input="debouncedApplyFilters"
          />
        </label>
        <label>
          {{ t("adminAuditLog.filters.entityType") }}
          <select v-model="filters.entityType" @change="applyFilters">
            <option value="">{{ t("adminAuditLog.filters.options.all") }}</option>
            <option value="user">{{ t("adminAuditLog.filters.options.user") }}</option>
            <option value="user_role">{{ t("adminAuditLog.filters.options.userRole") }}</option>
            <option value="supplier">{{ t("adminAuditLog.filters.options.supplier") }}</option>
            <option value="supplier_status">{{ t("adminAuditLog.filters.options.supplierStatus") }}</option>
            <option value="supplier_document">{{ t("adminAuditLog.filters.options.supplierDocument") }}</option>
            <option value="contract">{{ t("adminAuditLog.filters.options.contract") }}</option>
            <option value="rating">{{ t("adminAuditLog.filters.options.rating") }}</option>
            <option value="buyer_assignment">{{ t("adminAuditLog.filters.options.buyerAssignment") }}</option>
            <option value="audit_log">{{ t("adminAuditLog.filters.options.auditLog") }}</option>
          </select>
        </label>
        <label>
          {{ t("adminAuditLog.filters.keyword") }}
          <input
            v-model="filters.keyword"
            type="text"
            :placeholder="t('adminAuditLog.filters.placeholders.keyword')"
            @input="debouncedApplyFilters"
          />
        </label>
        <label>
          {{ t("adminAuditLog.filters.startDate") }}
          <input v-model="filters.startDate" type="datetime-local" @change="applyFilters" />
        </label>
        <label>
          {{ t("adminAuditLog.filters.endDate") }}
          <input v-model="filters.endDate" type="datetime-local" @change="applyFilters" />
        </label>
        <label>
          {{ t("adminAuditLog.filters.isSensitive") }}
          <select v-model="filters.isSensitive" @change="applyFilters">
            <option value="">{{ t("adminAuditLog.filters.options.sensitiveAll") }}</option>
            <option value="true">{{ t("adminAuditLog.filters.options.sensitiveOnly") }}</option>
            <option value="false">{{ t("adminAuditLog.filters.options.nonSensitive") }}</option>
          </select>
        </label>
        <label>
          {{ t("adminAuditLog.filters.entityId") }}
          <input
            v-model="filters.entityId"
            type="text"
            :placeholder="t('adminAuditLog.filters.placeholders.entityId')"
            @input="debouncedApplyFilters"
          />
        </label>
      </div>
      <div class="filter-actions">
        <button class="btn-secondary" type="button" @click="clearFilters">{{ t("adminAuditLog.actions.clearFilters") }}</button>
        <button class="btn-primary" type="button" @click="exportLogs">{{ t("adminAuditLog.actions.exportLogs") }}</button>
        <button class="btn-primary" type="button" @click="verifyChain">{{ t("adminAuditLog.actions.verifyChain") }}</button>
        <button
          class="btn-secondary"
          type="button"
          :disabled="diagnosticsLoading"
          @click="refreshDiagnostics"
        >
          {{ diagnosticsLoading ? t("adminAuditLog.actions.refreshDiagnosticsLoading") : t("adminAuditLog.actions.refreshDiagnostics") }}
        </button>
      </div>
      <div v-if="queryTime !== null" class="query-stats">
        {{ t("adminAuditLog.stats.queryTime", { time: queryTime }) }}
        <span v-if="totalRecords !== undefined">
          | {{ t("adminAuditLog.stats.totalRecords", { total: totalRecords }) }}</span
        >
        <span v-if="querySourceLabel">
          | {{ t("adminAuditLog.stats.dataSource", { source: querySourceLabel }) }}</span
        >
        <span v-if="appliedFiltersDisplay"> | {{ t("adminAuditLog.stats.appliedFilters", { filters: appliedFiltersDisplay }) }}</span>
        <span v-if="performanceWarning" class="query-warning">
          | {{ t("adminAuditLog.stats.performanceWarning", { warning: performanceWarning }) }}
        </span>
      </div>
      <div v-if="aggregatorDiagnostics" class="query-diagnostics">
        <span>
          {{ t("adminAuditLog.diagnostics.label", {
            status: aggregatorDiagnostics.enabled
              ? t("adminAuditLog.diagnostics.status.enabled")
              : t("adminAuditLog.diagnostics.status.disabled")
          }) }}
        </span>
        <span v-if="aggregatorDiagnostics.queries.last">
          | {{ t("adminAuditLog.diagnostics.latestQuery", {
            backend: aggregatorDiagnostics.queries.last.backend,
            status: aggregatorDiagnostics.queries.last.status,
            duration: aggregatorDiagnostics.queries.last.durationMs
          }) }}
        </span>
        <span v-if="aggregatorDiagnostics.queries.last?.error">
          | {{ t("adminAuditLog.diagnostics.error", {
            message: aggregatorDiagnostics.queries.last.error.message
          }) }}
        </span>
      </div>
    </section>
    <section class="panel">
      <div class="section-header">
        <h2>{{ t("adminAuditLog.list.title") }}</h2>
        <span v-if="loading" class="muted">{{ t("common.loading") }}</span>
      </div>

      <div v-if="loading && !logs.length" class="placeholder">
        {{ t("adminAuditLog.list.loadingHint") }}
      </div>
      <div v-else-if="!logs.length" class="placeholder">{{ t("adminAuditLog.list.empty") }}</div>
      <div v-else class="log-list">
        <article
          v-for="entry in logs"
          :key="entry.id"
          class="log-entry"
          :class="[`type-${entry.entityType}`, { sensitive: entry.isSensitive }]"
        >
          <header class="entry-header">
            <div class="entry-meta">
              <span class="entry-id">#{{ entry.id }}</span>
              <span v-if="entry.isSensitive" class="sensitive-badge" :title="t('adminAuditLog.list.sensitiveTooltip')">
                馃敀 {{ t("adminAuditLog.list.sensitiveBadge") }}
              </span>
              <span class="entry-type">{{ formatEntityType(entry.entityType) }}</span>
              <span v-if="entry.entityId" class="entry-entity">{{ entry.entityId }}</span>
              <span class="entry-action">{{ formatAction(entry.action) }}</span>
              <span v-if="entry.ipAddress" class="entry-ip" :title="t('adminAuditLog.list.ipTooltip')">
                馃搷 {{ entry.ipAddress }}
              </span>
            </div>
            <time class="entry-time" :datetime="entry.createdAt">
              {{ formatDateTime(entry.createdAt) }}
            </time>
          </header>

          <div class="entry-body">
            <div class="entry-actor">
              <strong>{{
                entry.actorName || entry.actorId || t("adminAuditLog.systemActor")
              }}</strong>
              <span v-if="entry.actorId && entry.actorId !== entry.actorName" class="muted">
                ({{ entry.actorId }})
              </span>
            </div>

            <div v-if="entry.changes" class="entry-changes">
              <button type="button" class="toggle-changes" @click="toggleChanges(entry.id)">
                {{
                  expandedEntries.has(entry.id)
                    ? t("adminAuditLog.list.toggle.hide")
                    : t("adminAuditLog.list.toggle.show")
                }}
              </button>
              <pre v-if="expandedEntries.has(entry.id)" class="changes-detail">{{
                JSON.stringify(entry.changes, null, 2)
              }}</pre>
            </div>

            <div v-if="entry.isSensitive && entry.hashChainValue" class="entry-hash">
              <span class="hash-label">{{ t("adminAuditLog.list.hashLabel") }}</span>
              <code class="hash-value">{{ entry.hashChainValue.substring(0, 16) }}...</code>
              <button type="button" class="btn-verify" @click="verifyLog(entry.id)">{{ t("adminAuditLog.list.verify") }}</button>
            </div>
          </div>
        </article>
      </div>

      <footer v-if="logs.length" class="pagination-footer">
        <button type="button" :disabled="currentPage === 1" @click="goToPage(currentPage - 1)">
          {{ t("common.previous") }}
        </button>
        <span class="page-info">{{
          t("adminAuditLog.list.pagination.page", { page: currentPage })
        }}</span>
        <button type="button" :disabled="logs.length < pageSize" @click="goToPage(currentPage + 1)">
          {{ t("common.next") }}
        </button>
      </footer>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, nextTick } from "vue";
import { useI18n } from "vue-i18n";
import PageHeader from "@/components/layout/PageHeader.vue";
import { getActiveIntlLocale } from "@/i18n";
import { useNotification } from "@/composables";
import {
  listAuditLogs,
  type AuditLogEntry,
  type AuditLogQuery,
  type AggregatorDiagnostics,
} from "@/api/audit";

const notification = useNotification();
const { t } = useI18n();

const logs = ref<AuditLogEntry[]>([]);
const loading = ref(false);
const currentPage = ref(1);
const pageSize = ref(50);
const expandedEntries = ref(new Set<number>());
const queryTime = ref<number | null>(null);
const totalRecords = ref<number | undefined>(undefined);
const querySource = ref<string | null>(null);
const performanceWarning = ref<string | null>(null);
const appliedFilters = ref<string[]>([]);
const aggregatorDiagnostics = ref<AggregatorDiagnostics | null>(null);
const diagnosticsLoading = ref(false);
const isMounted = ref(false);

const ensureDomReady = async () => {
  if (!isMounted.value || typeof document === "undefined" || typeof window === "undefined") {
    return false;
  }
  await nextTick();
  return true;
};

const filters = reactive<AuditLogQuery>({
  entityType: "",
  entityId: "",
  actorId: "",
  ipAddress: "",
  keyword: "",
  startDate: "",
  endDate: "",
  isSensitive: "",
});

const hasFilters = computed(() =>
  Boolean(
    filters.entityType ||
      filters.entityId ||
      filters.actorId ||
      filters.ipAddress ||
      filters.keyword ||
      filters.startDate ||
      filters.endDate ||
      filters.isSensitive,
  ),
);

const querySourceLabel = computed(() => {
  if (!querySource.value) {
    return null;
  }
  const sourceKey = querySource.value.toLowerCase();
  const supported = ["elasticsearch", "loki", "aggregator", "sqlite"];
  if (supported.includes(sourceKey)) {
    return t(`adminAuditLog.querySources.${sourceKey}`);
  }
  return querySource.value;
});

const appliedFiltersDisplay = computed(() =>
  appliedFilters.value.length ? appliedFilters.value.join(", ") : "",
);

// Debounce timer for input fields
let debounceTimer: ReturnType<typeof setTimeout> | null = null;
const debouncedApplyFilters = () => {
  if (debounceTimer) {
    clearTimeout(debounceTimer);
  }
  debounceTimer = setTimeout(() => {
    applyFilters();
  }, 500);
};

const entityTypeKeyMap: Record<string, string> = {
  user: "user",
  user_role: "userRole",
  supplier: "supplier",
  supplier_status: "supplierStatus",
  supplier_document: "supplierDocument",
  contract: "contract",
  rating: "rating",
  buyer_assignment: "buyerAssignment",
};

const formatEntityType = (type: string | null) => {
  if (!type) {
    return t("adminAuditLog.format.unknown");
  }
  const mapped = entityTypeKeyMap[type];
  if (mapped) {
    return t(`adminAuditLog.filters.options.${mapped}`);
  }
  return type
    .split("_")
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(" ");
};

const formatAction = (action: string | null) => {
  if (!action) {
    return t("adminAuditLog.format.unknown");
  }
  return action
    .split("_")
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(" ");
};

const formatDateTime = (isoString: string) => {
  try {
    const date = new Date(isoString);
    return new Intl.DateTimeFormat(getActiveIntlLocale(), {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    }).format(date);
  } catch {
    return isoString;
  }
};

const toggleChanges = (entryId: number) => {
  if (expandedEntries.value.has(entryId)) {
    expandedEntries.value.delete(entryId);
  } else {
    expandedEntries.value.add(entryId);
  }
};

const fetchLogs = async () => {
  loading.value = true;
  try {
    const query: AuditLogQuery = {
      page: currentPage.value,
      limit: pageSize.value,
    };
    if (filters.entityType) query.entityType = filters.entityType;
    if (filters.entityId) query.entityId = filters.entityId;
    if (filters.actorId) query.actorId = filters.actorId;
    if (filters.ipAddress) query.ipAddress = filters.ipAddress;
    if (filters.keyword) query.keyword = filters.keyword;
    if (filters.startDate) query.startDate = filters.startDate;
    if (filters.endDate) query.endDate = filters.endDate;
    if (filters.isSensitive) query.isSensitive = filters.isSensitive;
    query.includeDiagnostics = true;

    querySource.value = null;
    performanceWarning.value = null;
    appliedFilters.value = [];

    const response = await listAuditLogs(query);
    logs.value = response.data;
    queryTime.value = response.queryTime ?? null;
    totalRecords.value = response.total;
    querySource.value = response.querySource ?? null;
    performanceWarning.value = response.performanceWarning ?? null;
    appliedFilters.value = Array.isArray(response.appliedFilters) ? response.appliedFilters : [];
    aggregatorDiagnostics.value = response.diagnostics?.aggregator ?? null;
  } catch (error) {
    console.error(t("adminAuditLog.messages.loadFailed"), error);
    const message = error instanceof Error ? error.message : t("adminAuditLog.messages.loadFailed");
    notification.error(message);
  } finally {
    loading.value = false;
  }
};

const refreshDiagnostics = async () => {
  diagnosticsLoading.value = true;
  try {
    const { getAuditAggregatorDiagnostics } = await import("@/api/audit");
    aggregatorDiagnostics.value = await getAuditAggregatorDiagnostics();
  } catch (error) {
    console.error("Failed to fetch aggregator diagnostics", error);
    const message = error instanceof Error ? error.message : t("adminAuditLog.messages.diagnosticsFailed");
    notification.error(message);
  } finally {
    diagnosticsLoading.value = false;
  }
};

const applyFilters = () => {
  currentPage.value = 1;
  expandedEntries.value.clear();
  fetchLogs();
};

const clearFilters = () => {
  filters.entityType = "";
  filters.entityId = "";
  filters.actorId = "";
  filters.ipAddress = "";
  filters.keyword = "";
  filters.startDate = "";
  filters.endDate = "";
  filters.isSensitive = "";
  applyFilters();
};

const goToPage = (page: number) => {
  currentPage.value = page;
  expandedEntries.value.clear();
  fetchLogs();
};

const refresh = () => {
  expandedEntries.value.clear();
  fetchLogs();
};

const verifyLog = async (id: number) => {
  try {
    const { verifyArchivedLog } = await import("@/api/audit");
    const result = await verifyArchivedLog(id);
    if (result.valid) {
      notification.success(t("adminAuditLog.messages.verifyLogSuccess", { id, message: result.message }));
    } else {
      notification.warning(t("adminAuditLog.messages.verifyLogFailure", { id, message: result.message }));
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    notification.error(t("adminAuditLog.messages.verifyLogError", { message }));
  }
};

const verifyChain = async () => {
  try {
    await notification.confirm(
      t("adminAuditLog.messages.verifyChainConfirm"),
      t("adminAuditLog.messages.verifyChainTitle"),
      {
        confirmButtonText: t("common.confirm"),
        cancelButtonText: t("common.cancel"),
        type: "info",
      },
    );

    const { verifyHashChain } = await import("@/api/audit");
    const result = await verifyHashChain();

    if (result.valid) {
      notification.success({
        message: t("adminAuditLog.messages.chainSuccess", { message: result.message }),
        duration: 5000,
      });
    } else {
      notification.error({
        message: t("adminAuditLog.messages.chainFailure", { message: result.message }),
        duration: 5000,
      });
      if (result.brokenChains && result.brokenChains.length > 0) {
        console.error("Broken hash chains detected", result.brokenChains);
      }
    }
  } catch (error) {
    if (error === "cancel") {
      return;
    }
    const message = error instanceof Error ? error.message : String(error);
    notification.error(t("adminAuditLog.messages.chainError", { message }));
  }
};

const exportLogs = async () => {
  try {
    await notification.confirm(
      t("adminAuditLog.messages.exportConfirm"),
      t("adminAuditLog.messages.exportTitle"),
      {
        confirmButtonText: t("common.confirm"),
        cancelButtonText: t("common.cancel"),
        type: "info",
      },
    );

    const { exportAuditLogs } = await import("@/api/audit");
    const exportFilters = {
      startDate: filters.startDate || undefined,
      endDate: filters.endDate || undefined,
      isSensitive: filters.isSensitive ? filters.isSensitive === "true" : undefined,
      entityType: filters.entityType || undefined,
      entityId: filters.entityId || undefined,
    };

    const blob = await exportAuditLogs(exportFilters);
    if (!(await ensureDomReady())) {
      return;
    }

    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `audit-logs-export-${new Date().toISOString().split("T")[0]}.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);

    notification.success(t("adminAuditLog.messages.exportSuccess"));
  } catch (error) {
    if (error === "cancel") {
      return;
    }
    const message = error instanceof Error ? error.message : String(error);
    notification.error(t("adminAuditLog.messages.exportFailed", { message }));
  }
};

onMounted(() => {
  isMounted.value = true;
  fetchLogs();
});

onUnmounted(() => {
  isMounted.value = false;
});



</script>

<style scoped>
.audit-log-viewer {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}


.header-actions {
  display: flex;
  gap: 12px;
}



.panel {
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 20px;
  background: #ffffff;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.filters-panel h2 {
  font-size: 16px;
  margin: 0;
  font-weight: 600;
}

.filter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
}

.filter-grid label {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 14px;
  color: #374151;
}

.filter-grid input,
.filter-grid select {
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 8px 10px;
  font-size: 14px;
}

.filter-actions {
  display: flex;
  gap: 12px;
  margin-top: 8px;
}

.btn-primary,
.btn-secondary {
  padding: 8px 16px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  border: none;
  transition: all 0.2s ease;
}

.btn-primary {
  background: #4f46e5;
  color: white;
}

.btn-primary:hover {
  background: #4338ca;
}

.btn-secondary {
  background: #f3f4f6;
  color: #374151;
  border: 1px solid #d1d5db;
}

.btn-secondary:hover {
  background: #e5e7eb;
}

.query-stats {
  padding: 8px 12px;
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  border-radius: 6px;
  font-size: 13px;
  color: #166534;
}

.query-stats strong {
  font-weight: 600;
  color: #15803d;
}

.query-warning {
  color: #b45309;
  font-weight: 600;
}

.query-diagnostics {
  margin-top: 6px;
  font-size: 13px;
  color: #4b5563;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.section-header h2 {
  font-size: 16px;
  margin: 0;
  font-weight: 600;
}

.placeholder {
  color: #6b7280;
  font-size: 14px;
  padding: 40px 20px;
  text-align: center;
}

.log-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.log-entry {
  border: 1px solid #e5e7eb;
  border-left: 3px solid #9ca3af;
  border-radius: 8px;
  padding: 14px 16px;
  background: #f9fafb;
  transition:
    background 0.2s ease,
    border-color 0.2s ease;
}

.log-entry:hover {
  background: #ffffff;
}

.log-entry.type-user,
.log-entry.type-user_role {
  border-left-color: #3b82f6;
}

.log-entry.type-supplier,
.log-entry.type-supplier_status {
  border-left-color: #10b981;
}

.log-entry.type-contract {
  border-left-color: #f59e0b;
}

.log-entry.type-buyer_assignment {
  border-left-color: #8b5cf6;
}

.log-entry.sensitive {
  border-left-color: #dc2626;
  border-left-width: 4px;
  background: #fef2f2;
}

.log-entry.sensitive:hover {
  background: #fee2e2;
}

.sensitive-badge {
  background: #dc2626;
  color: white;
  padding: 3px 10px;
  border-radius: 999px;
  font-weight: 600;
  font-size: 11px;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.entry-ip {
  background: #dbeafe;
  color: #1e40af;
  padding: 2px 8px;
  border-radius: 999px;
  font-weight: 500;
  font-size: 11px;
  font-family: monospace;
}

.entry-hash {
  margin-top: 8px;
  padding: 8px 12px;
  background: #f3f4f6;
  border-radius: 6px;
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
}

.hash-label {
  color: #6b7280;
  font-weight: 600;
}

.hash-value {
  font-family: monospace;
  background: #1f2937;
  color: #10b981;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
}

.btn-verify {
  margin-left: auto;
  padding: 4px 12px;
  background: #10b981;
  color: white;
  border: none;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s ease;
}

.btn-verify:hover {
  background: #059669;
}

.entry-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  margin-bottom: 10px;
}

.entry-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  font-size: 13px;
}

.entry-id {
  font-weight: 600;
  color: #6b7280;
}

.entry-type {
  background: #eef2ff;
  color: #4f46e5;
  padding: 2px 8px;
  border-radius: 999px;
  font-weight: 600;
  font-size: 12px;
}

.entry-entity {
  background: #fff7ed;
  color: #ea580c;
  padding: 2px 8px;
  border-radius: 999px;
  font-weight: 500;
  font-size: 12px;
}

.entry-action {
  color: #4b5563;
  font-weight: 500;
}

.entry-time {
  font-size: 12px;
  color: #9ca3af;
  white-space: nowrap;
}

.entry-body {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.entry-actor {
  font-size: 14px;
  color: #1f2937;
}

.entry-actor strong {
  font-weight: 600;
}

.entry-changes {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.toggle-changes {
  align-self: flex-start;
  border: none;
  background: transparent;
  color: #4f46e5;
  font-weight: 600;
  font-size: 13px;
  cursor: pointer;
  padding: 0;
}

.changes-detail {
  background: #1f2937;
  color: #f9fafb;
  padding: 12px;
  border-radius: 6px;
  font-size: 12px;
  font-family: "Courier New", monospace;
  overflow-x: auto;
  margin: 0;
}

.pagination-footer {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 16px;
  padding-top: 12px;
  border-top: 1px solid #e5e7eb;
}

.pagination-footer button {
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 6px 14px;
  background: #ffffff;
  cursor: pointer;
  font-weight: 500;
}

.pagination-footer button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.page-info {
  font-size: 14px;
  color: #6b7280;
}

.link-btn {
  border: none;
  background: transparent;
  color: #4f46e5;
  font-weight: 600;
  cursor: pointer;
  padding: 0;
}

.muted {
  color: #6b7280;
}
</style>
