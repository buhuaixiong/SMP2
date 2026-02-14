<template>
  <div class="profile-history-timeline">
    <div class="timeline-header">
      <h3>Profile Change History</h3>
      <div class="timeline-controls">
        <el-select
          v-model="filterType"
          placeholder="Filter by type"
          size="small"
          clearable
          style="width: 150px"
        >
          <el-option label="All Changes" value="" />
          <el-option label="Profile Updates" value="profile" />
          <el-option label="Documents" value="document" />
          <el-option label="Status Changes" value="status" />
          <el-option label="Approvals" value="approval" />
        </el-select>

        <el-date-picker
          v-model="dateRange"
          type="daterange"
          size="small"
          range-separator="to"
          start-placeholder="Start date"
          end-placeholder="End date"
          :clearable="true"
          style="width: 260px"
          @change="handleDateChange"
        />

        <el-button
          size="small"
          :icon="showFilters ? 'arrow-up' : 'arrow-down'"
          @click="showFilters = !showFilters"
        >
          {{ showFilters ? "Hide" : "Show" }} Filters
        </el-button>
      </div>
    </div>

    <!-- Advanced Filters -->
    <transition name="slide-down">
      <div v-if="showFilters" class="advanced-filters">
        <el-form :inline="true" size="small">
          <el-form-item label="Changed By">
            <el-input v-model="filterUser" placeholder="User name" clearable style="width: 160px" />
          </el-form-item>

          <el-form-item label="Field">
            <el-input
              v-model="filterField"
              placeholder="Field name"
              clearable
              style="width: 160px"
            />
          </el-form-item>

          <el-form-item>
            <el-button type="primary" @click="applyFilters">Apply</el-button>
            <el-button @click="clearFilters">Clear</el-button>
          </el-form-item>
        </el-form>
      </div>
    </transition>

    <!-- Loading State -->
    <div v-if="effectiveLoading" class="timeline-loading">
      <el-skeleton :rows="5" animated />
    </div>

    <!-- Timeline -->
    <div v-else-if="filteredHistory.length > 0" class="timeline-container">
      <el-timeline>
        <el-timeline-item
          v-for="(event, index) in filteredHistory"
          :key="index"
          :timestamp="formatTimestamp(event.timestamp)"
          :type="getEventType(event.action)"
          :icon="getEventIcon(event.action)"
          :color="getEventColor(event.action)"
          placement="top"
        >
          <el-card class="timeline-card" :class="getEventClass(event.action)">
            <div class="event-header">
              <div class="event-title">
                <el-icon :size="18">
                  <component :is="getEventIconComponent(event.action)" />
                </el-icon>
                <span class="event-action">{{ formatAction(event.action) }}</span>
              </div>

              <el-tag :type="getActionTagType(event.action)" size="small">
                {{ event.action }}
              </el-tag>
            </div>

            <!-- Event Details -->
            <div class="event-details">
              <div v-if="event.actor" class="event-actor">
                <el-icon><user /></el-icon>
                <span>{{ event.actorName || event.actor }}</span>
              </div>

              <!-- Profile Field Changes -->
              <div v-if="event.changes && event.action === 'update'" class="field-changes">
                <div
                  v-for="(change, fieldKey) in event.changes"
                  :key="fieldKey"
                  class="field-change"
                >
                  <div class="field-name">{{ formatFieldName(fieldKey) }}</div>
                  <div class="field-values">
                    <div class="old-value">
                      <span class="label">Before:</span>
                      <span class="value">{{ formatValue(change.old) }}</span>
                    </div>
                    <el-icon class="arrow-icon"><right /></el-icon>
                    <div class="new-value">
                      <span class="label">After:</span>
                      <span class="value">{{ formatValue(change.new) }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Status Changes -->
              <div v-if="event.action === 'status_change'" class="status-change">
                <div class="status-flow">
                  <el-tag :type="getStatusType(event.oldStatus)">
                    {{ formatStatus(event.oldStatus) }}
                  </el-tag>
                  <el-icon class="flow-arrow"><right /></el-icon>
                  <el-tag :type="getStatusType(event.newStatus)">
                    {{ formatStatus(event.newStatus) }}
                  </el-tag>
                </div>
                <div v-if="event.notes" class="status-notes">
                  <el-icon><document /></el-icon>
                  <span>{{ event.notes }}</span>
                </div>
              </div>

              <!-- Document Events -->
              <div v-if="event.entityType === 'document'" class="document-event">
                <div class="document-info">
                  <el-icon><document /></el-icon>
                  <span class="doc-name">{{ event.documentName || "Document" }}</span>
                  <el-tag v-if="event.docType" size="small" type="info">
                    {{ event.docType }}
                  </el-tag>
                </div>
                <div v-if="event.notes" class="doc-notes">{{ event.notes }}</div>
              </div>

              <!-- Approval Events -->
              <div v-if="event.action === 'approval'" class="approval-event">
                <div class="approval-decision" :class="event.decision">
                  <el-icon v-if="event.decision === 'approved'"><circle-check /></el-icon>
                  <el-icon v-else><circle-close /></el-icon>
                  <span>{{ event.decision === "approved" ? "Approved" : "Rejected" }}</span>
                </div>
                <div v-if="event.comments" class="approval-comments">
                  <el-icon><chat-line-square /></el-icon>
                  <span>{{ event.comments }}</span>
                </div>
              </div>

              <!-- Generic Changes -->
              <div v-if="event.changes && event.action !== 'update'" class="generic-changes">
                <pre class="changes-json">{{ JSON.stringify(event.changes, null, 2) }}</pre>
              </div>

              <!-- Event Notes -->
              <div v-if="event.notes && event.action !== 'status_change'" class="event-notes">
                <el-icon><warning /></el-icon>
                <span>{{ event.notes }}</span>
              </div>
            </div>

            <!-- Expand/Collapse for Complex Changes -->
            <div
              v-if="hasComplexChanges(event)"
              class="expand-toggle"
              @click="toggleExpanded(index)"
            >
              <el-icon :class="{ 'is-expanded': expandedEvents.has(index) }">
                <arrow-down />
              </el-icon>
              <span>{{ expandedEvents.has(index) ? "Show Less" : "Show More" }}</span>
            </div>
          </el-card>
        </el-timeline-item>
      </el-timeline>

      <!-- Load More -->
      <div v-if="effectiveHasMore" class="load-more">
        <el-button :loading="loadingMore" @click="loadMore"> Load More History </el-button>
      </div>
    </div>

    <!-- Empty State -->
    <div v-else class="empty-state">
      <el-empty description="No history records found">
        <template #image>
          <el-icon :size="80" color="#C0C4CC"><clock /></el-icon>
        </template>
      </el-empty>
    </div>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, watch, onMounted } from "vue";
import {
  User,
  Document,
  Right,
  Warning,
  CircleCheck,
  CircleClose,
  ChatLineSquare,
  ArrowDown,
  Clock,
  Edit,
  Upload,
  Download,
  Delete,
  Check,
  Close,
  Refresh,
} from "@element-plus/icons-vue";

import { getSupplierHistory, type HistoryEvent as APIHistoryEvent } from "@/api/suppliers";


import { useNotification } from "@/composables";
const notification = useNotification();

interface HistoryEvent {
  id?: number;
  timestamp: string;
  action: string;
  entityType?: string;
  entityId?: string;
  actor?: string;
  actorName?: string;
  changes?: Record<string, any>;
  oldStatus?: string;
  newStatus?: string;
  notes?: string;
  documentName?: string;
  docType?: string;
  decision?: "approved" | "rejected";
  comments?: string;
}

interface Props {
  supplierId: number;
  history?: HistoryEvent[];
  loading?: boolean;
  hasMore?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  history: () => [],
  loading: false,
  hasMore: false,
});

const emit = defineEmits<{
  (e: "load-more"): void;
  (e: "refresh"): void;
}>();

// Internal state for self-managed data fetching
const internalHistory = ref<HistoryEvent[]>([]);
const internalLoading = ref(false);
const internalHasMore = ref(false);
const currentOffset = ref(0);
const pageLimit = 50;

// Determine if we're using external or internal data
const isExternalData = computed(() => props.history && props.history.length > 0);
const effectiveHistory = computed(() =>
  isExternalData.value ? props.history : internalHistory.value,
);
const effectiveLoading = computed(() =>
  isExternalData.value ? props.loading : internalLoading.value,
);
const effectiveHasMore = computed(() =>
  isExternalData.value ? props.hasMore : internalHasMore.value,
);

// Fetch history from API
const fetchHistory = async (append = false) => {
  if (isExternalData.value) return; // Don't fetch if external data is provided
  if (!props.supplierId || props.supplierId <= 0) {
    console.warn("ProfileHistoryTimeline: Invalid supplier ID", props.supplierId);
    return;
  }

  try {
    internalLoading.value = true;
    const response = await getSupplierHistory(props.supplierId, {
      limit: pageLimit,
      offset: append ? currentOffset.value : 0,
    });

    if (append) {
      internalHistory.value = [...internalHistory.value, ...response.data];
    } else {
      internalHistory.value = response.data;
      currentOffset.value = 0;
    }

    currentOffset.value = response.pagination.offset + response.data.length;
    internalHasMore.value = response.pagination.hasMore;
  } catch (error) {
    console.error("Failed to fetch supplier history:", error);
    notification.error("Failed to load history");
  } finally {
    internalLoading.value = false;
  }
};

const filterType = ref("");
const dateRange = ref<[Date, Date] | null>(null);
const filterUser = ref("");
const filterField = ref("");
const showFilters = ref(false);
const expandedEvents = ref(new Set<number>());
const loadingMore = ref(false);

const filteredHistory = computed(() => {
  let filtered = [...effectiveHistory.value];

  // Filter by type
  if (filterType.value) {
    filtered = filtered.filter((event) => {
      switch (filterType.value) {
        case "profile":
          return event.action === "update" || event.action === "create";
        case "document":
          return event.entityType === "document" || event.action.includes("document");
        case "status":
          return event.action === "status_change";
        case "approval":
          return event.action === "approval";
        default:
          return true;
      }
    });
  }

  // Filter by date range
  if (dateRange.value) {
    const [start, end] = dateRange.value;
    filtered = filtered.filter((event) => {
      if (!event.timestamp) return false;
      const eventDate = new Date(event.timestamp);
      if (Number.isNaN(eventDate.getTime())) return false;
      return eventDate >= start && eventDate <= end;
    });
  }

  // Filter by user
  if (filterUser.value) {
    const userLower = filterUser.value.toLowerCase();
    filtered = filtered.filter((event) => {
      const actorName = event.actorName ? event.actorName.toLowerCase() : "";
      const actor = event.actor ? event.actor.toLowerCase() : "";
      return actorName.includes(userLower) || actor.includes(userLower);
    });
  }

  return filtered;
});

const formatTimestamp = (timestamp?: string | null): string => {
  if (!timestamp) {
    return "Unknown time";
  }
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return "Unknown time";
  }
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return "Just now";
  if (diffMins < 60) return `${diffMins} minute${diffMins > 1 ? "s" : ""} ago`;
  if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? "s" : ""} ago`;
  if (diffDays < 7) return `${diffDays} day${diffDays > 1 ? "s" : ""} ago`;

  return date.toLocaleString();
};

const formatAction = (action: string): string => {
  const actionMap: Record<string, string> = {
    create: "Profile Created",
    update: "Profile Updated",
    status_change: "Status Changed",
    approval: "Approval Decision",
    document_upload: "Document Uploaded",
    document_delete: "Document Deleted",
    document_renew: "Document Renewed",
    tag_add: "Tag Added",
    tag_remove: "Tag Removed",
  };
  return actionMap[action] || action.replace(/_/g, " ").replace(/\b\w/g, (l) => l.toUpperCase());
};

const formatFieldName = (field: string): string => {
  const fieldMap: Record<string, string> = {
    companyName: "Company Name",
    companyId: "Company ID",
    contactPerson: "Contact Person",
    contactPhone: "Contact Phone",
    contactEmail: "Contact Email",
    bankAccount: "Bank Account",
    paymentTerms: "Payment Terms",
    businessRegistrationNumber: "Business Registration Number",
  };
  return (
    fieldMap[field] || field.replace(/([A-Z])/g, " $1").replace(/^./, (str) => str.toUpperCase())
  );
};

const formatValue = (value: any): string => {
  if (value === null || value === undefined) return "N/A";
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
};

const formatStatus = (status?: string | null): string => {
  if (!status) {
    return "Unknown";
  }
  return status.replace(/_/g, " ").replace(/\b\w/g, (l) => l.toUpperCase());
};

const getEventType = (action: string): "primary" | "success" | "warning" | "danger" | "info" => {
  if (action === "create") return "success";
  if (action === "update") return "primary";
  if (action === "status_change") return "warning";
  if (action === "approval") return "success";
  if (action.includes("delete")) return "danger";
  return "info";
};

const getEventIcon = (action: string): string => {
  const iconMap: Record<string, string> = {
    create: "plus",
    update: "edit",
    status_change: "refresh",
    approval: "check",
    document_upload: "upload",
    document_delete: "delete",
    document_renew: "refresh",
  };
  return iconMap[action] || "circle";
};

const getEventColor = (action: string): string => {
  if (action === "create") return "#67C23A";
  if (action === "update") return "#409EFF";
  if (action === "status_change") return "#E6A23C";
  if (action === "approval") return "#67C23A";
  if (action.includes("delete")) return "#F56C6C";
  return "#909399";
};

const getEventClass = (action: string): string => {
  return `event-${action.replace(/_/g, "-")}`;
};

const getEventIconComponent = (action: string) => {
  const componentMap: Record<string, any> = {
    create: Check,
    update: Edit,
    status_change: Refresh,
    approval: CircleCheck,
    document_upload: Upload,
    document_delete: Delete,
    document_download: Download,
    document_renew: Refresh,
  };
  return componentMap[action] || Edit;
};

const getActionTagType = (action: string): "success" | "info" | "warning" | "danger" => {
  if (action === "create" || action === "approval") return "success";
  if (action === "update") return "info";
  if (action === "status_change") return "warning";
  if (action.includes("delete")) return "danger";
  return "info";
};

const getStatusType = (status?: string | null): "success" | "info" | "warning" | "danger" => {
  if (!status) return "info";
  if (status.includes("approved") || status.includes("qualified")) return "success";
  if (status.includes("pending")) return "warning";
  if (status.includes("reject") || status.includes("suspend")) return "danger";
  return "info";
};

const hasComplexChanges = (event: HistoryEvent): boolean => {
  if (!event.changes) return false;
  const changeCount = Object.keys(event.changes).length;
  return changeCount > 3;
};

const toggleExpanded = (index: number) => {
  if (expandedEvents.value.has(index)) {
    expandedEvents.value.delete(index);
  } else {
    expandedEvents.value.add(index);
  }
};

const handleDateChange = (value: [Date, Date] | null) => {
  // Trigger filter update
  applyFilters();
};

const applyFilters = () => {
  // Filters are computed, so this is mainly for user feedback
  notification.success("Filters applied");
};

const clearFilters = () => {
  filterType.value = "";
  dateRange.value = null;
  filterUser.value = "";
  filterField.value = "";
  notification.info("Filters cleared");
};

const loadMore = async () => {
  if (isExternalData.value) {
    loadingMore.value = true;
    try {
      emit("load-more");
    } finally {
      loadingMore.value = false;
    }
  } else {
    await fetchHistory(true);
  }
};

onMounted(() => {
  // Fetch history if not provided externally
  if (!isExternalData.value) {
    fetchHistory();
  }
});




</script>

<style scoped>
.profile-history-timeline {
  background: white;
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.timeline-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
  flex-wrap: wrap;
  gap: 16px;
}

.timeline-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.timeline-controls {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.advanced-filters {
  padding: 16px;
  background: #f5f7fa;
  border-radius: 8px;
  margin-bottom: 24px;
}

.slide-down-enter-active,
.slide-down-leave-active {
  transition: all 0.3s ease;
  max-height: 200px;
  overflow: hidden;
}

.slide-down-enter-from,
.slide-down-leave-to {
  max-height: 0;
  opacity: 0;
}

.timeline-loading {
  padding: 40px 20px;
}

.timeline-container {
  margin-top: 24px;
}

.timeline-card {
  margin-top: 8px;
  border-radius: 8px;
  transition: all 0.3s;
}

.timeline-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.12);
  transform: translateY(-2px);
}

.event-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.event-title {
  display: flex;
  align-items: center;
  gap: 8px;
}

.event-action {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.event-details {
  display: flex;
  flex-direction: column;
  gap: 12px;
  color: #606266;
}

.event-actor {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: #909399;
}

.field-changes {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.field-change {
  padding: 12px;
  background: #f5f7fa;
  border-radius: 6px;
  border-left: 3px solid #409eff;
}

.field-name {
  font-weight: 500;
  color: #303133;
  margin-bottom: 8px;
  font-size: 14px;
}

.field-values {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 13px;
}

.old-value,
.new-value {
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 1;
}

.old-value .label,
.new-value .label {
  font-size: 11px;
  color: #909399;
  text-transform: uppercase;
}

.old-value .value {
  color: #f56c6c;
  text-decoration: line-through;
}

.new-value .value {
  color: #67c23a;
  font-weight: 500;
}

.arrow-icon {
  color: #c0c4cc;
  flex-shrink: 0;
}

.status-change {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.status-flow {
  display: flex;
  align-items: center;
  gap: 12px;
}

.flow-arrow {
  color: #909399;
}

.status-notes {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  padding: 8px;
  background: #fdf6ec;
  border-radius: 4px;
  font-size: 13px;
}

.document-event {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.document-info {
  display: flex;
  align-items: center;
  gap: 8px;
}

.doc-name {
  font-weight: 500;
  color: #303133;
}

.doc-notes {
  font-size: 13px;
  color: #606266;
  padding-left: 28px;
}

.approval-event {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.approval-decision {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 15px;
  font-weight: 600;
}

.approval-decision.approved {
  color: #67c23a;
}

.approval-decision.rejected {
  color: #f56c6c;
}

.approval-comments {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  padding: 8px;
  background: #f0f9ff;
  border-radius: 4px;
  border-left: 3px solid #409eff;
  font-size: 13px;
}

.generic-changes {
  padding: 12px;
  background: #f5f7fa;
  border-radius: 6px;
  overflow-x: auto;
}

.changes-json {
  font-family: "Courier New", monospace;
  font-size: 12px;
  margin: 0;
  color: #606266;
}

.event-notes {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  padding: 8px;
  background: #fef0f0;
  border-radius: 4px;
  font-size: 13px;
  color: #f56c6c;
}

.expand-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid #ebeef5;
  cursor: pointer;
  color: #409eff;
  font-size: 13px;
  transition: color 0.3s;
}

.expand-toggle:hover {
  color: #66b1ff;
}

.expand-toggle .el-icon {
  transition: transform 0.3s;
}

.expand-toggle .el-icon.is-expanded {
  transform: rotate(180deg);
}

.load-more {
  display: flex;
  justify-content: center;
  margin-top: 24px;
  padding-top: 24px;
  border-top: 1px solid #ebeef5;
}

.empty-state {
  padding: 60px 20px;
  text-align: center;
}

@media (max-width: 768px) {
  .profile-history-timeline {
    padding: 16px;
  }

  .timeline-header {
    flex-direction: column;
    align-items: stretch;
  }

  .timeline-controls {
    flex-direction: column;
  }

  .field-values {
    flex-direction: column;
    align-items: stretch;
  }

  .arrow-icon {
    transform: rotate(90deg);
    align-self: center;
  }
}
</style>
