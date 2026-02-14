<template>
  <div class="approval-history-timeline" v-loading="loading">
    <el-timeline v-if="history.length > 0">
      <el-timeline-item
        v-for="item in history"
        :key="item.id"
        :timestamp="formatDateTime(item.createdAt)"
        :type="getTimelineType(item.decision)"
        :icon="getTimelineIcon(item.decision)"
        placement="top"
      >
        <el-card>
          <div class="history-item">
            <div class="item-header">
              <div class="step-info">
                <el-tag :type="getDecisionType(item.decision)" size="small">
                  {{ $t(`rfq.lineItemWorkflow.decisions.${item.decision}`) }}
                </el-tag>
                <span class="step-name">
                  {{ $t(`rfq.lineItemWorkflow.steps.${item.step}`) }}
                </span>
              </div>
              <div class="approver-info">
                <span class="approver-name">{{ item.approverName }}</span>
                <span class="approver-role">
                  ({{ $t(`roles.${item.approverRole}`) }})
                </span>
              </div>
            </div>

            <div v-if="item.comments" class="comments">
              <strong>{{ $t('common.comments') }}:</strong>
              <p>{{ item.comments }}</p>
            </div>

            <div v-if="item.newQuoteId && item.previousQuoteId" class="quote-change">
              <el-alert
                :title="$t('rfq.lineItemWorkflow.quoteChanged')"
                type="warning"
                :closable="false"
                show-icon
              >
                <div class="change-details">
                  <div>{{ $t('rfq.lineItemWorkflow.previousQuote') }}: #{{ item.previousQuoteId }}</div>
                  <div>{{ $t('rfq.lineItemWorkflow.newQuote') }}: #{{ item.newQuoteId }}</div>
                  <div v-if="item.changeReason">
                    {{ $t('common.reason') }}: {{ item.changeReason }}
                  </div>
                </div>
              </el-alert>
            </div>
          </div>
        </el-card>
      </el-timeline-item>
    </el-timeline>

    <el-empty
      v-else
      :description="$t('rfq.lineItemWorkflow.noHistory')"
      :image-size="100"
    />
  </div>
</template>

<script setup lang="ts">




import { ref, onMounted } from 'vue'
import { Check, Close, CircleCheck, CircleClose } from '@element-plus/icons-vue'

import { useI18n } from 'vue-i18n'
import { getApprovalHistory, type LineItemApproval } from '@/api/lineItemWorkflow'


import { useNotification } from "@/composables";

const notification = useNotification();
const props = defineProps<{
  rfqId: number
  lineItemId: number
}>()

const { t } = useI18n()

const loading = ref(false)
const history = ref<LineItemApproval[]>([])

async function loadHistory() {
  loading.value = true
  try {
    history.value = await getApprovalHistory(props.rfqId, props.lineItemId)
  } catch (error: any) {
    notification.error(error.message || t('common.loadFailed'))
  } finally {
    loading.value = false
  }
}

function getTimelineType(decision: string) {
  const typeMap: Record<string, any> = {
    approved: 'success',
    rejected: 'danger',
    confirmed: 'success',
    refused: 'danger',
    submitted: 'primary',
  }
  return typeMap[decision] || 'info'
}

function getTimelineIcon(decision: string) {
  const iconMap: Record<string, any> = {
    approved: CircleCheck,
    rejected: CircleClose,
    confirmed: Check,
    refused: Close,
  }
  return iconMap[decision] || undefined
}

function getDecisionType(decision: string) {
  const typeMap: Record<string, any> = {
    approved: 'success',
    rejected: 'danger',
    confirmed: 'success',
    refused: 'danger',
    submitted: 'primary',
  }
  return typeMap[decision] || 'info'
}

function formatDateTime(dateString: string | null) {
  if (!dateString) return '-'
  const date = new Date(dateString)
  return date.toLocaleString()
}

onMounted(() => {
  loadHistory()
})




</script>

<style scoped lang="scss">
.approval-history-timeline {
  .history-item {
    .item-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;

      .step-info {
        display: flex;
        align-items: center;
        gap: 8px;

        .step-name {
          font-weight: 600;
          font-size: 14px;
        }
      }

      .approver-info {
        font-size: 13px;
        color: #606266;

        .approver-name {
          font-weight: 500;
          color: #303133;
        }

        .approver-role {
          margin-left: 4px;
        }
      }
    }

    .comments {
      margin-top: 12px;
      padding: 8px 12px;
      background-color: #f5f7fa;
      border-radius: 4px;

      strong {
        font-size: 13px;
        color: #606266;
      }

      p {
        margin: 4px 0 0;
        font-size: 13px;
        color: #303133;
        line-height: 1.5;
      }
    }

    .quote-change {
      margin-top: 12px;

      .change-details {
        margin-top: 8px;
        font-size: 13px;
        line-height: 1.6;
      }
    }
  }
}
</style>
