<template>
  <div class="preview-panel">
    <div class="preview-actions">
      <el-button size="small" :loading="previewLoading" @click="requestPreview">
        {{
          translateOr(
            "supplierRegistration.actions.previewApproval",
            "Preview Approval Flow",
          )
        }}
      </el-button>
    </div>

    <el-alert
      v-if="previewError"
      type="error"
      :closable="false"
      class="preview-alert"
    >
      {{ previewError }}
    </el-alert>

    <div v-if="approvalPreview" class="preview-result">
      <h3>{{ approvalPreview.templateLabel }}</h3>
      <p class="preview-meta">
        {{
          translateOr(
            "supplierRegistration.preview.estimatedTimeline",
            "Estimated completion",
          )
        }}:
        <strong>{{ approvalPreview.estimatedWorkingDays }}</strong>
        {{
          translateOr(
            "supplierRegistration.preview.workingDays",
            "working days",
          )
        }}
      </p>

      <ul class="preview-steps">
        <li v-for="step in approvalPreview.steps" :key="step.key">
          <div class="step-name">{{ step.title }}</div>
          <div class="step-role">{{ step.role }}</div>
          <div class="step-eta">{{ formatEta(step.eta) }}</div>
        </li>
      </ul>

      <div v-if="approvalPreview.riskFlags.length" class="preview-risks">
        <h4>
          {{
            translateOr(
              "supplierRegistration.preview.riskFlags",
              "Attention points",
            )
          }}
        </h4>
        <ul>
          <li
            v-for="flag in approvalPreview.riskFlags"
            :key="flag.key"
            :class="['risk-flag', flag.level]"
          >
            {{ flag.message }}
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ApprovalPreview } from "@/api/suppliers";

defineProps<{
  translateOr: (key: string, fallback: string) => string;
  previewLoading: boolean;
  previewError: string | null;
  approvalPreview: ApprovalPreview | null;
  formatEta: (iso: string) => string;
  requestPreview: () => void;
}>();
</script>
