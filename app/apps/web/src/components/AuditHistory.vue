<template>
  <div class="audit-history">
    <div class="audit-header">
      <h3>{{ $t('audit.history') }}</h3>
      <el-button
        v-if="showExport"
        size="small"
        @click="exportHistory"
      >
        {{ $t('audit.export') }}
      </el-button>
    </div>

    <el-timeline v-if="logs && logs.length > 0" class="audit-timeline">
      <el-timeline-item
        v-for="log in logs"
        :key="log.id"
        :timestamp="formatTimestamp(log.createdAt)"
        placement="top"
        :color="getActionColor(log.action)"
      >
        <el-card class="audit-card">
          <div class="audit-card-header">
            <div class="audit-meta">
              <span class="audit-actor">
                <el-icon><User /></el-icon>
                {{ log.actorName || $t('audit.system') }}
              </span>
              <span class="audit-action">
                <el-tag :type="getActionType(log.action)" size="small">
                  {{ $t(`audit.actions.${log.action}`, log.action) }}
                </el-tag>
              </span>
              <span v-if="log.ipAddress" class="audit-ip">
                <el-icon><Location /></el-icon>
                {{ log.ipAddress }}
              </span>
            </div>
          </div>

          <!-- Field-Level Summary -->
          <div v-if="log.summary" class="audit-summary">
            <el-icon class="summary-icon"><EditPen /></el-icon>
            <span>{{ log.summary }}</span>
          </div>

          <!-- Field Changes Detail -->
          <div
            v-if="log.changes && log.changes.fieldChanges"
            class="field-changes"
          >
            <el-collapse v-model="activeChanges[log.id]">
              <el-collapse-item
                :title="$t('audit.viewDetails')"
                :name="log.id"
              >
                <FieldChanges :changes="log.changes.fieldChanges" />
              </el-collapse-item>
            </el-collapse>
          </div>

          <!-- Legacy Changes (for backward compatibility) -->
          <div
            v-else-if="log.changes && !log.changes.fieldChanges"
            class="legacy-changes"
          >
            <el-button
              text
              size="small"
              @click="showRawChanges(log)"
            >
              {{ $t('audit.viewRawChanges') }}
            </el-button>
          </div>
        </el-card>
      </el-timeline-item>
    </el-timeline>

    <el-empty
      v-else-if="!loading"
      :description="$t('audit.noHistory')"
    />

    <div v-if="loading" class="loading-container">
      <el-skeleton :rows="5" animated />
    </div>

    <!-- Pagination -->
    <el-pagination
      v-if="pagination && pagination.total > pagination.limit"
      v-model:current-page="currentPage"
      v-model:page-size="pageSize"
      :page-sizes="[10, 20, 50, 100]"
      :total="pagination.total"
      layout="total, sizes, prev, pager, next"
      @current-change="handlePageChange"
      @size-change="handleSizeChange"
    />

    <!-- Raw Changes Dialog -->
    <el-dialog
      v-model="rawChangesVisible"
      :title="$t('audit.rawChanges')"
      width="600px"
    >
      <pre class="raw-changes-content">{{ formatJson(selectedLog?.changes) }}</pre>
    </el-dialog>
  </div>
</template>

<script setup>




import { ref, reactive, watch, onMounted, nextTick } from 'vue'
import { User, Location, EditPen } from '@element-plus/icons-vue'
import FieldChanges from './FieldChanges.vue'
import { getAuditLogs, exportAuditLogs } from '@/api/audit'

import { useI18n } from 'vue-i18n'


import { useNotification } from "@/composables";

const notification = useNotification();
const { t } = useI18n()

const props = defineProps({
  entityType: {
    type: String,
    default: null
  },
  entityId: {
    type: [String, Number],
    default: null
  },
  showExport: {
    type: Boolean,
    default: false
  },
  autoLoad: {
    type: Boolean,
    default: true
  }
})

const emit = defineEmits(['loaded', 'error'])

const logs = ref([])
const loading = ref(false)
const activeChanges = reactive({})
const currentPage = ref(1)
const pageSize = ref(20)
const pagination = ref(null)
const rawChangesVisible = ref(false)
const selectedLog = ref(null)

const ensureDomReady = async () => {
  if (typeof document === "undefined" || typeof window === "undefined") {
    return false
  }
  await nextTick()
  return true
}

const loadLogs = async () => {
  try {
    loading.value = true

    const params = {
      page: currentPage.value,
      limit: pageSize.value
    }

    if (props.entityType) params.entityType = props.entityType
    if (props.entityId) params.entityId = props.entityId

    const response = await getAuditLogs(params)

    if (response.success) {
      logs.value = response.data || []
      pagination.value = response.pagination
      emit('loaded', logs.value)
    } else {
      throw new Error(response.message || 'Failed to load audit logs')
    }
  } catch (error) {
    console.error('Error loading audit logs:', error)
    notification.error(t('audit.loadError'))
    emit('error', error)
  } finally {
    loading.value = false
  }
}

const formatTimestamp = (timestamp) => {
  if (!timestamp) return ''
  const date = new Date(timestamp)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

const getActionColor = (action) => {
  const colorMap = {
    create: '#67C23A',
    update: '#409EFF',
    delete: '#F56C6C',
    publish: '#E6A23C',
    approve: '#67C23A',
    reject: '#F56C6C'
  }
  return colorMap[action] || '#909399'
}

const getActionType = (action) => {
  const typeMap = {
    create: 'success',
    update: 'primary',
    delete: 'danger',
    publish: 'warning',
    approve: 'success',
    reject: 'danger'
  }
  return typeMap[action] || 'info'
}

const showRawChanges = (log) => {
  selectedLog.value = log
  rawChangesVisible.value = true
}

const formatJson = (obj) => {
  return JSON.stringify(obj, null, 2)
}

const handlePageChange = (page) => {
  currentPage.value = page
  loadLogs()
}

const handleSizeChange = (size) => {
  pageSize.value = size
  currentPage.value = 1
  loadLogs()
}

const exportHistory = async () => {
  try {
    const exportFilters = {
      entityType: props.entityType || undefined,
      entityId:
        props.entityId !== null && props.entityId !== undefined && String(props.entityId).length
          ? String(props.entityId)
          : undefined,
    }
    const blob = await exportAuditLogs(exportFilters)
    if (!(await ensureDomReady())) {
      return
    }

    const url = window.URL.createObjectURL(blob)
    const link = document.createElement("a")
    link.href = url
    link.download = `audit-logs-export-${new Date().toISOString().split("T")[0]}.json`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)

    notification.success(t("common.operationSuccess"))
  } catch (error) {
    console.error("Audit export failed:", error)
    notification.error(t("common.operationFailed"))
  }
}

// Watch for prop changes
watch(() => [props.entityType, props.entityId], () => {
  if (props.autoLoad) {
    currentPage.value = 1
    loadLogs()
  }
}, { immediate: false })

onMounted(() => {
  if (props.autoLoad) {
    loadLogs()
  }
})

// Expose methods for parent component
defineExpose({
  loadLogs,
  refresh: loadLogs
})




</script>

<style scoped lang="scss">
.audit-history {
  padding: 20px;
}

.audit-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;

  h3 {
    margin: 0;
    font-size: 18px;
    font-weight: 500;
  }
}

.audit-timeline {
  margin-top: 20px;
}

.audit-card {
  margin-bottom: 0;

  :deep(.el-card__body) {
    padding: 16px;
  }
}

.audit-card-header {
  margin-bottom: 12px;
}

.audit-meta {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
  align-items: center;
  font-size: 14px;
  color: #606266;

  span {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .audit-actor {
    font-weight: 500;
    color: #303133;
  }
}

.audit-summary {
  display: flex;
  align-items: start;
  gap: 8px;
  padding: 12px;
  background-color: #f5f7fa;
  border-radius: 4px;
  font-size: 14px;
  line-height: 1.6;
  color: #606266;

  .summary-icon {
    margin-top: 2px;
    color: #409eff;
  }
}

.field-changes {
  margin-top: 12px;

  :deep(.el-collapse) {
    border: none;
  }

  :deep(.el-collapse-item__header) {
    background-color: transparent;
    border: none;
    padding-left: 0;
    font-size: 13px;
    color: #409eff;
  }

  :deep(.el-collapse-item__content) {
    padding: 12px 0;
  }
}

.legacy-changes {
  margin-top: 8px;
}

.loading-container {
  padding: 20px 0;
}

.raw-changes-content {
  background-color: #f5f7fa;
  padding: 16px;
  border-radius: 4px;
  max-height: 400px;
  overflow: auto;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 12px;
  line-height: 1.6;
}

:deep(.el-pagination) {
  margin-top: 20px;
  justify-content: center;
}
</style>
