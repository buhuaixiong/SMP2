<template>
  <div class="approval-workflow">
    <!-- RFQ基本信息 -->
    <el-card class="rfq-info-card">
      <template #header>
        <h3>{{ t("rfq.approval.rfqInfo") }}</h3>
      </template>
      <el-descriptions :column="2" border>
        <el-descriptions-item :label="t('rfq.form.title')">
          {{ rfq?.title }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.rfqType')">
          <el-tag :type="rfq?.rfqType === 'short_term' ? 'success' : 'warning'">
            {{ t(`rfq.rfqType.${rfq?.rfqType}`) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.approval.selectedSupplier')">
          {{ selectedQuote?.supplierName || "-" }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.quote.grandTotal')">
          <span class="highlight-amount">
            {{ formatPrice(selectedQuote?.totalAmount, selectedQuote?.currency) }}
          </span>
        </el-descriptions-item>
        <el-descriptions-item :label="t('rfq.form.description')" :span="2">
          {{ rfq?.description || "-" }}
        </el-descriptions-item>
      </el-descriptions>
    </el-card>

    <!-- 需求明细 -->
    <el-card class="line-items-card">
      <template #header>
        <h3>{{ t("rfq.lineItems.title") }}</h3>
      </template>
      <el-table :data="rfq?.lineItems" border stripe>
        <el-table-column type="index" :label="t('rfq.items.lineNumber')" width="70" />
        <el-table-column :label="t('rfq.lineItems.materialCategory')" width="120">
          <template #default="{ row }">
            {{ getCategoryLabel(row.materialCategory) }}
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.items.itemName')" min-width="150">
          <template #default="{ row }">
            <div>
              <div>{{ row.itemName }}</div>
              <div v-if="row.specifications" class="item-spec">{{ row.specifications }}</div>
            </div>
          </template>
        </el-table-column>
        <el-table-column :label="t('rfq.items.quantity')" width="120">
          <template #default="{ row }"> {{ row.quantity }} {{ row.unit }} </template>
        </el-table-column>
        <el-table-column :label="t('rfq.approval.quotePrice')" width="140">
          <template #default="{ row }">
            {{ getQuotePrice(row.id) }}
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 价格对比截图 (采购经理和总监都可见) -->
    <el-card v-if="priceComparisons.length > 0" class="price-comparison-card">
      <template #header>
        <h3>{{ t("rfq.approval.priceComparison") }}</h3>
      </template>
      <el-row :gutter="24">
        <el-col v-for="comparison in priceComparisons" :key="comparison.id" :span="8">
          <div class="comparison-item">
            <div class="platform-name">{{ getPlatformName(comparison.platform) }}</div>
            <div v-if="comparison.productUrl" class="product-url">
              <a :href="comparison.productUrl" target="_blank">{{ t("rfq.review.productUrl") }}</a>
            </div>
            <div v-if="comparison.platformPrice" class="platform-price">
              {{ t("rfq.review.platformPrice") }}: {{ formatPrice(comparison.platformPrice) }}
            </div>
            <div class="screenshot">
              <el-image
                :src="comparison.filePath ? resolveApiUrl(comparison.filePath) : ''"
                :preview-src-list="comparison.filePath ? [resolveApiUrl(comparison.filePath)] : []"
                fit="cover"
                style="width: 100%; height: 200px"
              >
                <template #error>
                  <div class="image-error">{{ t("common.imageLoadError") }}</div>
                </template>
              </el-image>
            </div>
          </div>
        </el-col>
      </el-row>
    </el-card>

    <!-- 审批流程 -->
    <el-card class="approval-steps-card">
      <template #header>
        <h3>{{ t("rfq.approval.approvalProcess") }}</h3>
      </template>

      <el-timeline>
        <el-timeline-item
          v-for="approval in approvals"
          :key="approval.id"
          :type="getTimelineType(approval.status)"
          :icon="getTimelineIcon(approval.status)"
        >
          <div class="approval-step">
            <div class="step-header">
              <h4>{{ approval.stepName }}</h4>
              <el-tag :type="getStatusType(approval.status)">
                {{ t(`rfq.approval.status.${approval.status}`) }}
              </el-tag>
            </div>

            <div class="step-info">
              <div v-if="approval.approverName">
                {{ t("rfq.approval.approver") }}: {{ approval.approverName }}
              </div>
              <div v-if="approval.decidedAt">
                {{ t("rfq.approval.decidedAt") }}: {{ formatDateTime(approval.decidedAt) }}
              </div>
              <div v-if="approval.decision" class="decision-text">
                {{ approval.decision }}
              </div>
            </div>

            <!-- 评论区 -->
            <div v-if="approval.comments && approval.comments.length > 0" class="comments-section">
              <h5>{{ t("rfq.approval.comments") }}</h5>
              <div v-for="comment in approval.comments" :key="comment.id" class="comment-item">
                <div class="comment-header">
                  <span class="author">{{ comment.authorName }}</span>
                  <span class="time">{{ formatDateTime(comment.createdAt) }}</span>
                </div>
                <div class="comment-content">{{ comment.content }}</div>
              </div>
            </div>

            <!-- 操作区 (仅当前审批人可见) -->
            <div v-if="canApprove(approval)" class="approval-actions">
              <el-divider />

              <el-form label-position="top">
                <el-form-item :label="t('rfq.approval.decision')">
                  <el-input
                    v-model="decisionText"
                    type="textarea"
                    :rows="3"
                    :placeholder="t('rfq.approval.decisionPlaceholder')"
                    maxlength="500"
                    show-word-limit
                  />
                </el-form-item>
              </el-form>

              <div class="action-buttons">
                <el-button type="danger" @click="handleReject(approval.id)">
                  {{ t("rfq.approval.reject") }}
                </el-button>
                <el-button type="success" @click="handleApprove(approval.id)">
                  {{ t("rfq.approval.approve") }}
                </el-button>
              </div>
            </div>

            <!-- 添加评论 -->
            <div v-if="approval.status === 'pending'" class="add-comment">
              <el-divider />
              <el-input
                v-model="newComment"
                type="textarea"
                :rows="2"
                :placeholder="t('rfq.approval.addCommentPlaceholder')"
              />
              <el-button
                type="primary"
                size="small"
                style="margin-top: 8px"
                @click="handleAddComment(approval.id)"
              >
                {{ t("rfq.approval.addComment") }}
              </el-button>
            </div>

            <!-- 邀请采购员评论 (仅经理和总监) -->
            <div v-if="canInvitePurchaser(approval)" class="invite-purchaser">
              <el-button type="primary" size="small" link @click="showInviteDialog(approval.id)">
                {{ t("rfq.approval.invitePurchaser") }}
              </el-button>
            </div>
          </div>
        </el-timeline-item>
      </el-timeline>
    </el-card>

    <!-- 邀请采购员对话框 -->
    <el-dialog
      v-model="inviteDialogVisible"
      :title="t('rfq.approval.invitePurchaser')"
      width="500px"
    >
      <el-form label-position="top">
        <el-form-item :label="t('rfq.approval.selectPurchasers')">
          <el-select
            v-model="selectedPurchasers"
            multiple
            :placeholder="t('rfq.approval.selectPurchasersPlaceholder')"
            style="width: 100%"
          >
            <el-option
              v-for="purchaser in purchasers"
              :key="purchaser.id"
              :label="purchaser.name"
              :value="purchaser.id"
            />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="inviteDialogVisible = false">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" @click="handleInvitePurchasers">
          {{ t("common.confirm") }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";

import { Check, Close, Clock } from "@element-plus/icons-vue";
import { useAuthStore } from "@/stores/auth";
import { apiFetch } from "@/api/http";
import { resolveApiUrl } from "@/utils/apiBaseUrl";


import { useNotification } from "@/composables";

const notification = useNotification();
const props = defineProps<{
  rfqId: number;
  rfq: any;
  selectedQuote: any;
  approvals: any[];
  priceComparisons: any[];
}>();

const emit = defineEmits<{
  (e: "refresh"): void;
}>();

const { t } = useI18n();
const authStore = useAuthStore();

const decisionText = ref("");
const newComment = ref("");
const inviteDialogVisible = ref(false);
const selectedPurchasers = ref<number[]>([]);
const currentApprovalId = ref<number | null>(null);
const purchasers = ref<any[]>([]);

onMounted(() => {
  loadPurchasers();
});

async function loadPurchasers() {
  try {
    const data = await apiFetch<{ data?: any[] }>("/users", {
      params: { role: "purchaser" },
    });
    purchasers.value = data.data || [];
  } catch (error) {
    console.error("Failed to load purchasers:", error);
  }
}

function canApprove(approval: any): boolean {
  if (approval.status !== "pending") return false;
  return approval.approverRole === authStore.user?.role;
}

function canInvitePurchaser(approval: any): boolean {
  if (approval.status !== "pending") return false;
  const role = authStore.user?.role;
  return role === "procurement_director";
}

function getCategoryLabel(category: string): string {
  if (category === "equipment") return t("rfq.distributionCategory.equipment");
  if (category === "consumables") return t("rfq.lineItems.consumables");
  if (category === "hardware") return t("rfq.distributionCategory.hardware");
  return category;
}

function getQuotePrice(lineItemId: number): string {
  if (!props.selectedQuote?.lineItems) return "-";
  const quoteLineItem = props.selectedQuote.lineItems.find(
    (item: any) => item.rfqLineItemId === lineItemId,
  );
  return quoteLineItem ? formatPrice(quoteLineItem.unitPrice) : "-";
}

function getPlatformName(platform: string): string {
  const names: Record<string, string> = {
    "1688": "1688",
    zkh: "震坤行",
    jd: "京东",
  };
  return names[platform] || platform;
}

function formatPrice(amount?: number, currency = "CNY"): string {
  if (amount === undefined || amount === null) return "-";
  return `${amount.toFixed(2)} ${currency}`;
}

function formatDateTime(dateString?: string): string {
  if (!dateString) return "-";
  try {
    return new Date(dateString).toLocaleString();
  } catch {
    return dateString;
  }
}

function getTimelineType(status: string): string {
  if (status === "approved") return "success";
  if (status === "rejected") return "danger";
  return "primary";
}

function getTimelineIcon(status: string) {
  if (status === "approved") return Check;
  if (status === "rejected") return Close;
  return Clock;
}

function getStatusType(status: string): string {
  if (status === "approved") return "success";
  if (status === "rejected") return "danger";
  return "warning";
}

async function handleApprove(approvalId: number) {
  try {
    await notification.confirm(t("rfq.approval.confirmApprove"), t("common.confirm"), {
      confirmButtonText: t("common.confirm"),
      cancelButtonText: t("common.cancel"),
      type: "success",
    });

    await apiFetch(`/rfq-workflow/${props.rfqId}/approvals/${approvalId}/approve`, {
      method: "POST",
      body: { decision: decisionText.value },
    });

    notification.success(t("rfq.approval.approveSuccess"));
    decisionText.value = "";
    emit("refresh");
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error?.message || t("rfq.approval.approveError"));
    }
  }
}

async function handleReject(approvalId: number) {
  try {
    await notification.confirm(t("rfq.approval.confirmReject"), t("common.confirm"), {
      confirmButtonText: t("common.confirm"),
      cancelButtonText: t("common.cancel"),
      type: "warning",
    });

    await apiFetch(`/rfq-workflow/${props.rfqId}/approvals/${approvalId}/reject`, {
      method: "POST",
      body: { decision: decisionText.value },
    });

    notification.success(t("rfq.approval.rejectSuccess"));
    decisionText.value = "";
    emit("refresh");
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error?.message || t("rfq.approval.rejectError"));
    }
  }
}

async function handleAddComment(approvalId: number) {
  if (!newComment.value.trim()) {
    notification.warning(t("rfq.approval.commentRequired"));
    return;
  }

  try {
    await apiFetch(`/rfq-workflow/${props.rfqId}/approvals/${approvalId}/comments`, {
      method: "POST",
      body: { content: newComment.value },
    });

    notification.success(t("rfq.approval.commentAdded"));
    newComment.value = "";
    emit("refresh");
  } catch (error: any) {
    notification.error(error?.message || t("rfq.approval.commentError"));
  }
}

function showInviteDialog(approvalId: number) {
  currentApprovalId.value = approvalId;
  inviteDialogVisible.value = true;
}

async function handleInvitePurchasers() {
  if (selectedPurchasers.value.length === 0) {
    notification.warning(t("rfq.approval.selectPurchasersFirst"));
    return;
  }

  try {
    await apiFetch(`/rfq-workflow/${props.rfqId}/approvals/${currentApprovalId.value}/invite`, {
      method: "POST",
      body: { purchaserIds: selectedPurchasers.value },
    });

    notification.success(t("rfq.approval.inviteSuccess"));
    inviteDialogVisible.value = false;
    selectedPurchasers.value = [];
  } catch (error: any) {
    notification.error(error?.message || t("rfq.approval.inviteError"));
  }
}




</script>

<style scoped>
.approval-workflow {
  max-width: 1400px;
  margin: 0 auto;
}

.rfq-info-card,
.line-items-card,
.price-comparison-card,
.approval-steps-card {
  margin-bottom: 24px;
}

.highlight-amount {
  font-size: 18px;
  font-weight: 600;
  color: #67c23a;
}

.item-spec {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}

.comparison-item {
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  padding: 16px;
  background-color: #fafafa;
}

.platform-name {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  margin-bottom: 8px;
}

.product-url {
  font-size: 13px;
  margin-bottom: 4px;
}

.product-url a {
  color: #409eff;
  text-decoration: none;
}

.product-url a:hover {
  text-decoration: underline;
}

.platform-price {
  font-size: 14px;
  color: #606266;
  margin-bottom: 12px;
}

.screenshot {
  margin-top: 12px;
}

.image-error {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  background-color: #f5f7fa;
  color: #909399;
}

.approval-step {
  padding: 16px 0;
}

.step-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.step-header h4 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.step-info {
  font-size: 14px;
  color: #606266;
  line-height: 1.6;
}

.decision-text {
  margin-top: 8px;
  padding: 8px 12px;
  background-color: #f5f7fa;
  border-radius: 4px;
  font-style: italic;
}

.comments-section {
  margin-top: 16px;
  padding: 12px;
  background-color: #f9f9f9;
  border-radius: 4px;
}

.comments-section h5 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.comment-item {
  margin-bottom: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid #ebeef5;
}

.comment-item:last-child {
  margin-bottom: 0;
  padding-bottom: 0;
  border-bottom: none;
}

.comment-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 8px;
}

.author {
  font-weight: 500;
  color: #303133;
}

.time {
  font-size: 12px;
  color: #909399;
}

.comment-content {
  font-size: 14px;
  color: #606266;
  line-height: 1.6;
}

.approval-actions {
  margin-top: 16px;
}

.action-buttons {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.add-comment {
  margin-top: 16px;
}

.invite-purchaser {
  margin-top: 12px;
}
</style>
