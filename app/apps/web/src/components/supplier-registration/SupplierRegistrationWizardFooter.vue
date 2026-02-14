<template>
  <div class="wizard-footer">
    <div class="footer-left">
      <el-button
        class="ghost-button"
        :loading="draftSaving"
        :disabled="draftSaving || draftLoading || loading"
        @click="handleSaveDraft"
      >
        {{
          translateOr(
            "supplierRegistration.actions.saveDraft",
            "Save Draft",
          )
        }}
      </el-button>
      <span v-if="draftExpiresAt" class="draft-meta">
        {{
          translateOr("supplierRegistration.draft.expiresAt", "Expires")
        }}:
        {{ formatDateTime(draftExpiresAt) }}
      </span>
      <el-button
        v-if="!isFirstStep"
        @click="goPreviousStep"
        :disabled="loading || previewLoading"
      >
        {{ translateOr("common.previous", "Previous") }}
      </el-button>
    </div>
    <div class="footer-right">
      <el-button
        v-if="isLastStep"
        :loading="previewLoading"
        class="ghost-button"
        @click="handlePreview"
      >
        {{
          translateOr(
            "supplierRegistration.actions.previewApproval",
            "Preview Approval Flow",
          )
        }}
      </el-button>
      <el-button
        type="primary"
        :loading="loading"
        @click="isLastStep ? handleSubmit() : goNextStep()"
      >
        {{
          isLastStep
            ? translateOr(
                "supplierRegistration.actions.submit",
                "Submit Registration",
              )
            : translateOr("common.next", "Next")
        }}
      </el-button>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  translateOr: (key: string, fallback: string) => string;
  isFirstStep: boolean;
  isLastStep: boolean;
  loading: boolean;
  previewLoading: boolean;
  draftSaving: boolean;
  draftLoading: boolean;
  draftExpiresAt: string | null;
  formatDateTime: (value: string) => string;
  handleSaveDraft: () => void;
  goPreviousStep: () => void;
  goNextStep: () => void;
  handlePreview: () => void;
  handleSubmit: () => void;
}>();
</script>
