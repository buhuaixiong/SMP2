import { defineStore } from "pinia";
import { ref } from "vue";

import {
  fetchUpgradeRequirements,
  fetchUpgradeStatus,
  submitUpgradeApplication,
  uploadUpgradeFile,
  submitUpgradeDecision,
  type UploadUpgradeFilePayload,
  type UpgradeDecisionPayload,
} from "@/api/upgrade";
import type { UpgradeRequirement, UpgradeStatus } from "@/types";

interface RequirementUploadResult {
  fileId: number;
}

export const useUpgradeStore = defineStore("upgrade", () => {
  const requirements = ref<UpgradeRequirement[]>([]);
  const status = ref<UpgradeStatus | null>(null);
  const loading = ref(false);
  const submitting = ref(false);
  const actionLoading = ref(false);

  const syncRequirements = (incoming?: UpgradeRequirement[]) => {
    if (!requirements.value.length && !incoming) {
      return;
    }
    const base = requirements.value.length ? requirements.value : (incoming ?? []);
    const incomingMap = new Map((incoming ?? base).map((item) => [item.code, item]));
    requirements.value = base.map((item) => {
      const match = incomingMap.get(item.code);
      return {
        ...(match ?? item),
        fulfilled: match?.fulfilled ?? item.fulfilled ?? false,
        documentId: match?.documentId ?? item.documentId ?? null,
        template: match?.template ?? item.template ?? null,
      };
    });
  };

  const loadRequirements = async () => {
    const data = await fetchUpgradeRequirements();
    requirements.value = data;
    return data;
  };

  const loadStatus = async (supplierId: number) => {
    loading.value = true;
    try {
      const data = await fetchUpgradeStatus(supplierId);
      status.value = data;
      syncRequirements(data.requirements);
      return data;
    } finally {
      loading.value = false;
    }
  };

  const submitApplication = async (
    supplierId: number,
    documents: Array<{ requirementCode: string; fileId: number; notes?: string | null }>,
  ) => {
    submitting.value = true;
    try {
      await submitUpgradeApplication(supplierId, { documents });
      await loadStatus(supplierId);
    } finally {
      submitting.value = false;
    }
  };

  const uploadRequirementFile = async (
    payload: UploadUpgradeFilePayload,
  ): Promise<RequirementUploadResult> => {
    const file = await uploadUpgradeFile(payload);
    syncRequirements();
    return { fileId: file.id };
  };

  const decide = async (
    applicationId: number,
    stepKey: string,
    payload: UpgradeDecisionPayload,
    supplierId?: number,
  ) => {
    actionLoading.value = true;
    try {
      await submitUpgradeDecision(applicationId, stepKey, payload);
      if (supplierId) {
        await loadStatus(supplierId);
      } else if (status.value?.supplierId) {
        await loadStatus(status.value.supplierId);
      }
    } finally {
      actionLoading.value = false;
    }
  };

  const reset = () => {
    requirements.value = [];
    status.value = null;
  };

  return {
    requirements,
    status,
    loading,
    submitting,
    actionLoading,
    loadRequirements,
    loadStatus,
    submitApplication,
    uploadRequirementFile,
    decide,
    reset,
  };
});
