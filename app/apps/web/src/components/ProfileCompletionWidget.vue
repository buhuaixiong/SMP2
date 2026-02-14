<template>
  <div class="profile-completion-widget">
    <div class="widget-header">
      <h3>Profile Completion</h3>
      <el-button
        v-if="completionData.overallScore < 100"
        type="primary"
        size="small"
        @click="openWizard"
      >
        Complete Profile
      </el-button>
    </div>

    <!-- Main Progress Circle -->
    <div class="progress-circle-container">
      <el-progress
        type="circle"
        :percentage="completionData.overallScore"
        :width="160"
        :stroke-width="12"
        :color="getProgressColor(completionData.overallScore)"
        :status="completionData.overallScore === 100 ? 'success' : undefined"
      >
        <template #default="{ percentage }">
          <div class="progress-content">
            <div class="percentage">{{ percentage }}%</div>
            <div class="status-text">{{ getStatusText(percentage) }}</div>
          </div>
        </template>
      </el-progress>

      <!-- Celebration Effect -->
      <transition name="celebration">
        <div v-if="showCelebration" class="celebration-overlay">
          <el-icon :size="64" color="#67C23A"><circle-check /></el-icon>
          <p>Profile Complete!</p>
        </div>
      </transition>
    </div>

    <!-- Segmented Progress Bars -->
    <div class="progress-segments">
      <div class="segment-item">
        <div class="segment-header">
          <span class="segment-label">
            <el-icon><user /></el-icon>
            Profile Information
          </span>
          <span class="segment-score">{{ completionData.profileScore }}%</span>
        </div>
        <el-progress
          :percentage="completionData.profileScore"
          :color="getProgressColor(completionData.profileScore)"
          :show-text="false"
        />
        <div class="segment-detail">
          {{ completedProfileFields }}/{{ totalProfileFields }} fields completed
        </div>
      </div>

      <div class="segment-item">
        <div class="segment-header">
          <span class="segment-label">
            <el-icon><document /></el-icon>
            Required Documents
          </span>
          <span class="segment-score">{{ completionData.documentScore }}%</span>
        </div>
        <el-progress
          :percentage="completionData.documentScore"
          :color="getProgressColor(completionData.documentScore)"
          :show-text="false"
        />
        <div class="segment-detail">
          {{ completedDocuments }}/{{ totalDocuments }} documents uploaded
        </div>
      </div>
    </div>

    <!-- Missing Items Checklist -->
    <div
      v-if="completionData.missingItems && completionData.missingItems.length > 0"
      class="missing-items"
    >
      <div class="missing-header" @click="expandMissing = !expandMissing">
        <span class="missing-title">
          <el-icon><warning /></el-icon>
          {{ completionData.missingItems.length }} item(s) missing
        </span>
        <el-icon class="expand-icon" :class="{ 'is-expanded': expandMissing }">
          <arrow-down />
        </el-icon>
      </div>

      <transition name="expand">
        <div v-show="expandMissing" class="missing-list">
          <div
            v-for="(item, index) in completionData.missingItems"
            :key="index"
            class="missing-item"
            @click="handleItemClick(item)"
          >
            <el-icon class="item-icon" :color="item.type === 'profile' ? '#E6A23C' : '#F56C6C'">
              <circle-close />
            </el-icon>
            <div class="item-content">
              <span class="item-label">{{ item.label }}</span>
              <span class="item-type">{{
                item.type === "profile" ? "Profile Field" : "Document"
              }}</span>
            </div>
            <el-icon class="item-arrow">
              <arrow-right />
            </el-icon>
          </div>
        </div>
      </transition>
    </div>

    <!-- Completion Tips -->
    <el-alert
      v-if="completionData.overallScore < 80"
      type="warning"
      :closable="false"
      show-icon
      class="completion-tip"
    >
      <template #title> Why complete your profile? </template>
      <ul class="tip-list">
        <li>Increase visibility to procurement teams</li>
        <li>Speed up approval processes</li>
        <li>Unlock premium supplier features</li>
        <li>Build trust with complete documentation</li>
      </ul>
    </el-alert>

    <!-- Milestone Badges -->
    <div v-if="completionData.overallScore > 0" class="milestone-badges">
      <div class="badge" :class="{ achieved: completionData.overallScore >= 25 }">
        <el-icon><star /></el-icon>
        <span>Started</span>
      </div>
      <div class="badge" :class="{ achieved: completionData.overallScore >= 50 }">
        <el-icon><star /></el-icon>
        <span>Halfway</span>
      </div>
      <div class="badge" :class="{ achieved: completionData.overallScore >= 80 }">
        <el-icon><star /></el-icon>
        <span>Almost Done</span>
      </div>
      <div class="badge" :class="{ achieved: completionData.overallScore === 100 }">
        <el-icon><trophy /></el-icon>
        <span>Complete</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from "vue";
import {
  User,
  Document,
  Warning,
  ArrowDown,
  ArrowRight,
  CircleClose,
  CircleCheck,
  Star,
  Trophy,
} from "@element-plus/icons-vue";
import type { ComplianceSummary } from "@/types";

interface Props {
  completionData: ComplianceSummary;
  loading?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
});

const emit = defineEmits<{
  (e: "navigate-to-field", field: string): void;
  (e: "navigate-to-documents"): void;
  (e: "open-wizard"): void;
}>();

const expandMissing = ref(false);
const showCelebration = ref(false);
const previousScore = ref(0);

// Required fields configuration (matches backend supplierCompleteness.js)
const REQUIRED_PROFILE_FIELDS = [
  "companyName",
  "companyId",
  "contactPerson",
  "contactPhone",
  "contactEmail",
  "category",
  "address",
  "businessRegistrationNumber",
  "paymentTerms",
  "paymentCurrency",
  "bankAccount",
  "region",
];

const REQUIRED_DOCUMENTS = [
  { type: "business_license", label: "Business License" },
  { type: "tax_certificate", label: "Tax Registration Certificate" },
  { type: "bank_information", label: "Bank Account Information" },
];

const totalProfileFields = computed(() => REQUIRED_PROFILE_FIELDS.length);
const totalDocuments = computed(() => REQUIRED_DOCUMENTS.length);

const completedProfileFields = computed(() => {
  const missingProfileCount =
    props.completionData.missingItems?.filter((item) => item.type === "profile").length || 0;
  return totalProfileFields.value - missingProfileCount;
});

const completedDocuments = computed(() => {
  const missingDocCount =
    props.completionData.missingItems?.filter((item) => item.type === "document").length || 0;
  return totalDocuments.value - missingDocCount;
});

const getProgressColor = (percentage: number) => {
  if (percentage >= 90) return "#67C23A"; // Green
  if (percentage >= 60) return "#E6A23C"; // Yellow
  return "#F56C6C"; // Red
};

const getStatusText = (percentage: number) => {
  if (percentage === 100) return "Complete";
  if (percentage >= 80) return "Almost There";
  if (percentage >= 50) return "Good Progress";
  if (percentage >= 25) return "Getting Started";
  return "Just Started";
};

const handleItemClick = (item: { type: string; key: string; label: string }) => {
  if (item.type === "profile") {
    emit("navigate-to-field", item.key);
  } else if (item.type === "document") {
    emit("navigate-to-documents");
  }
};

const openWizard = () => {
  emit("open-wizard");
};

// Watch for completion milestones
watch(
  () => props.completionData.overallScore,
  (newScore, oldScore) => {
    // Trigger celebration when reaching 100%
    if (newScore === 100 && oldScore < 100) {
      showCelebration.value = true;
      setTimeout(() => {
        showCelebration.value = false;
      }, 3000);
    }

    // Check for milestone achievements
    const milestones = [25, 50, 80, 100];
    milestones.forEach((milestone) => {
      if (oldScore < milestone && newScore >= milestone) {
        // Could trigger notification or animation here
        console.log(`Milestone achieved: ${milestone}%`);
      }
    });

    previousScore.value = oldScore;
  },
  { immediate: false },
);
</script>

<style scoped>
.profile-completion-widget {
  background: white;
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.widget-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.widget-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.progress-circle-container {
  display: flex;
  justify-content: center;
  margin: 32px 0;
  position: relative;
}

.progress-content {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.percentage {
  font-size: 32px;
  font-weight: 700;
  color: #303133;
  line-height: 1;
}

.status-text {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}

.celebration-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.95);
  border-radius: 50%;
  animation: pulse 0.6s ease-out;
}

.celebration-overlay p {
  margin-top: 8px;
  font-size: 16px;
  font-weight: 600;
  color: #67c23a;
}

@keyframes pulse {
  0% {
    transform: scale(0.8);
    opacity: 0;
  }
  50% {
    transform: scale(1.1);
  }
  100% {
    transform: scale(1);
    opacity: 1;
  }
}

.celebration-enter-active,
.celebration-leave-active {
  transition: all 0.3s ease;
}

.celebration-enter-from,
.celebration-leave-to {
  opacity: 0;
  transform: scale(0.8);
}

.progress-segments {
  display: flex;
  flex-direction: column;
  gap: 20px;
  margin-bottom: 24px;
}

.segment-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.segment-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.segment-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: 500;
  color: #606266;
}

.segment-score {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.segment-detail {
  font-size: 12px;
  color: #909399;
  margin-top: -4px;
}

.missing-items {
  border: 1px solid #ebeef5;
  border-radius: 8px;
  overflow: hidden;
  margin-bottom: 16px;
}

.missing-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: #fdf6ec;
  cursor: pointer;
  transition: background 0.3s;
}

.missing-header:hover {
  background: #fbf0e0;
}

.missing-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  color: #e6a23c;
}

.expand-icon {
  transition: transform 0.3s;
}

.expand-icon.is-expanded {
  transform: rotate(180deg);
}

.missing-list {
  border-top: 1px solid #ebeef5;
}

.expand-enter-active,
.expand-leave-active {
  transition: all 0.3s ease;
  max-height: 500px;
  overflow: hidden;
}

.expand-enter-from,
.expand-leave-to {
  max-height: 0;
  opacity: 0;
}

.missing-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  cursor: pointer;
  transition: background 0.2s;
  border-bottom: 1px solid #f2f6fc;
}

.missing-item:last-child {
  border-bottom: none;
}

.missing-item:hover {
  background: #f5f7fa;
}

.item-icon {
  flex-shrink: 0;
}

.item-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.item-label {
  font-size: 14px;
  color: #303133;
}

.item-type {
  font-size: 12px;
  color: #909399;
}

.item-arrow {
  flex-shrink: 0;
  color: #c0c4cc;
}

.completion-tip {
  margin-bottom: 16px;
}

.tip-list {
  margin: 8px 0 0;
  padding-left: 20px;
  font-size: 13px;
  color: #606266;
}

.tip-list li {
  margin: 4px 0;
}

.milestone-badges {
  display: flex;
  justify-content: space-around;
  gap: 8px;
  padding-top: 16px;
  border-top: 1px solid #ebeef5;
}

.badge {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 12px;
  border-radius: 8px;
  background: #f5f7fa;
  transition: all 0.3s;
  opacity: 0.5;
  flex: 1;
}

.badge.achieved {
  opacity: 1;
  background: linear-gradient(135deg, #fff7e6 0%, #fff2d9 100%);
  box-shadow: 0 2px 8px rgba(230, 162, 60, 0.2);
  transform: scale(1.05);
}

.badge .el-icon {
  font-size: 24px;
  color: #e6a23c;
}

.badge.achieved .el-icon {
  color: #f59a23;
  animation: bounce 0.6s ease;
}

.badge span {
  font-size: 11px;
  font-weight: 500;
  color: #606266;
  text-align: center;
}

.badge.achieved span {
  color: #e6a23c;
  font-weight: 600;
}

@keyframes bounce {
  0%,
  100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-8px);
  }
}

@media (max-width: 768px) {
  .profile-completion-widget {
    padding: 16px;
  }

  .progress-circle-container {
    margin: 24px 0;
  }

  .milestone-badges {
    flex-wrap: wrap;
  }

  .badge {
    min-width: calc(50% - 4px);
  }
}
</style>
