<template>
  <div class="field-changes">
    <el-table
      :data="changes"
      border
      style="width: 100%"
      size="small"
    >
      <el-table-column
        prop="fieldLabel"
        :label="$t('audit.field')"
        width="150"
      >
        <template #default="{ row }">
          <div class="field-label">
            <el-icon v-if="row.changeType === 'added'" color="#67C23A">
              <Plus />
            </el-icon>
            <el-icon v-else-if="row.changeType === 'removed'" color="#F56C6C">
              <Minus />
            </el-icon>
            <el-icon v-else color="#409EFF">
              <Edit />
            </el-icon>
            <span>{{ row.fieldLabel || row.field }}</span>
          </div>
        </template>
      </el-table-column>

      <el-table-column
        prop="oldValue"
        :label="$t('audit.oldValue')"
        min-width="200"
      >
        <template #default="{ row }">
          <div class="value-cell old-value">
            <span v-if="row.changeType === 'added'" class="empty-value">
              {{ $t('audit.empty') }}
            </span>
            <span v-else>
              {{ formatValue(row.oldValue) }}
            </span>
          </div>
        </template>
      </el-table-column>

      <el-table-column width="60" align="center">
        <template #default>
          <el-icon color="#909399"><Right /></el-icon>
        </template>
      </el-table-column>

      <el-table-column
        prop="newValue"
        :label="$t('audit.newValue')"
        min-width="200"
      >
        <template #default="{ row }">
          <div class="value-cell new-value">
            <span v-if="row.changeType === 'removed'" class="empty-value">
              {{ $t('audit.empty') }}
            </span>
            <span v-else :class="getValueClass(row.changeType)">
              {{ formatValue(row.newValue) }}
            </span>
          </div>
        </template>
      </el-table-column>

      <el-table-column
        prop="changeType"
        :label="$t('audit.changeType')"
        width="100"
        align="center"
      >
        <template #default="{ row }">
          <el-tag
            :type="getChangeTypeTagType(row.changeType)"
            size="small"
          >
            {{ $t(`audit.changeTypes.${row.changeType}`) }}
          </el-tag>
        </template>
      </el-table-column>
    </el-table>

    <!-- Array Changes (if applicable) -->
    <div v-if="hasArrayChanges" class="array-changes">
      <h4>{{ $t('audit.relatedItemsChanges') }}</h4>

      <div v-if="arrayChanges.added && arrayChanges.added.length > 0" class="array-section">
        <h5>
          <el-icon color="#67C23A"><Plus /></el-icon>
          {{ $t('audit.itemsAdded', { count: arrayChanges.added.length }) }}
        </h5>
        <el-tag
          v-for="item in arrayChanges.added"
          :key="item.id"
          type="success"
          size="small"
          class="item-tag"
        >
          {{ formatArrayItem(item) }}
        </el-tag>
      </div>

      <div v-if="arrayChanges.modified && arrayChanges.modified.length > 0" class="array-section">
        <h5>
          <el-icon color="#409EFF"><Edit /></el-icon>
          {{ $t('audit.itemsModified', { count: arrayChanges.modified.length }) }}
        </h5>
        <div
          v-for="item in arrayChanges.modified"
          :key="item.id"
          class="modified-item"
        >
          <el-tag type="primary" size="small">
            {{ formatArrayItem(item) }}
          </el-tag>
          <FieldChanges
            v-if="item.changes"
            :changes="item.changes"
            compact
          />
        </div>
      </div>

      <div v-if="arrayChanges.removed && arrayChanges.removed.length > 0" class="array-section">
        <h5>
          <el-icon color="#F56C6C"><Minus /></el-icon>
          {{ $t('audit.itemsRemoved', { count: arrayChanges.removed.length }) }}
        </h5>
        <el-tag
          v-for="item in arrayChanges.removed"
          :key="item.id"
          type="danger"
          size="small"
          class="item-tag"
        >
          {{ formatArrayItem(item) }}
        </el-tag>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { Plus, Minus, Edit, Right } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps({
  changes: {
    type: Array,
    required: true
  },
  arrayChanges: {
    type: Object,
    default: null
  },
  compact: {
    type: Boolean,
    default: false
  }
})

const hasArrayChanges = computed(() => {
  return props.arrayChanges &&
    (props.arrayChanges.added?.length > 0 ||
     props.arrayChanges.modified?.length > 0 ||
     props.arrayChanges.removed?.length > 0)
})

const formatValue = (value) => {
  if (value === null || value === undefined) {
    return '-'
  }

  if (typeof value === 'boolean') {
    return value ? t('common.yes') : t('common.no')
  }

  if (typeof value === 'object') {
    return JSON.stringify(value)
  }

  if (typeof value === 'string' && value.length > 100 && !props.compact) {
    return value.substring(0, 97) + '...'
  }

  return String(value)
}

const formatArrayItem = (item) => {
  const itemData = item.item || item
  // Try to get a meaningful label from the item
  return itemData.name || itemData.title || itemData.itemName || `ID: ${item.id}`
}

const getValueClass = (changeType) => {
  return {
    'value-added': changeType === 'added',
    'value-modified': changeType === 'modified',
    'value-removed': changeType === 'removed'
  }
}

const getChangeTypeTagType = (changeType) => {
  const typeMap = {
    added: 'success',
    modified: 'primary',
    removed: 'danger'
  }
  return typeMap[changeType] || 'info'
}
</script>

<style scoped lang="scss">
.field-changes {
  .field-label {
    display: flex;
    align-items: center;
    gap: 6px;
    font-weight: 500;
  }

  .value-cell {
    padding: 4px 0;
    word-break: break-word;

    .empty-value {
      color: #909399;
      font-style: italic;
    }

    .value-added {
      color: #67C23A;
      font-weight: 500;
    }

    .value-modified {
      color: #409EFF;
      font-weight: 500;
    }

    .value-removed {
      color: #F56C6C;
      font-weight: 500;
      text-decoration: line-through;
    }
  }

  .old-value {
    background-color: #fef0f0;
  }

  .new-value {
    background-color: #f0f9ff;
  }
}

.array-changes {
  margin-top: 24px;
  padding: 16px;
  background-color: #f5f7fa;
  border-radius: 4px;

  > h4 {
    margin: 0 0 16px 0;
    font-size: 14px;
    font-weight: 500;
    color: #303133;
  }

  .array-section {
    margin-bottom: 16px;

    &:last-child {
      margin-bottom: 0;
    }

    h5 {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0 0 8px 0;
      font-size: 13px;
      font-weight: 500;
      color: #606266;
    }

    .item-tag {
      margin-right: 8px;
      margin-bottom: 8px;
    }

    .modified-item {
      margin-bottom: 12px;
      padding: 8px;
      background-color: white;
      border-radius: 4px;

      > .el-tag {
        margin-bottom: 8px;
      }
    }
  }
}
</style>
