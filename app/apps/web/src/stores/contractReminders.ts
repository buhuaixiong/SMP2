import { defineStore } from "pinia";
import { ref, computed } from "vue";

import {
  getContractReminderSummary,
  listContractReminders,
  getContractReminderSettings,
  updateContractReminderSettings,
} from "@/api/contracts";

import type {
  ContractReminderSummary,
  ContractReminderItem,
  ContractReminderSettings,
} from "@/types";

interface ReminderListResponse {
  settings: ContractReminderSettings;
  total: number;
  items: ContractReminderItem[];
}

export const useContractReminderStore = defineStore("contract-reminders", () => {
  const summary = ref<ContractReminderSummary | null>(null);
  const reminders = ref<ContractReminderItem[]>([]);
  const reminderTotal = ref(0);
  const settings = ref<ContractReminderSettings | null>(null);
  const defaults = ref<ContractReminderSettings | null>(null);

  const loadingSummary = ref(false);
  const loadingReminders = ref(false);
  const loadingSettings = ref(false);

  const reminderBuckets = computed(() => summary.value?.buckets ?? []);
  const expiredSummary = computed(
    () => summary.value?.expired ?? { contractCount: 0, supplierCount: 0 },
  );

  const fetchSummary = async (force = false) => {
    if (!force && summary.value) {
      return summary.value;
    }
    loadingSummary.value = true;
    try {
      const result = await getContractReminderSummary();
      summary.value = result;
      return result;
    } finally {
      loadingSummary.value = false;
    }
  };

  const fetchReminders = async (params: { bucket?: string; limit?: number } = {}) => {
    loadingReminders.value = true;
    try {
      const response: ReminderListResponse = await listContractReminders(params);
      settings.value = response.settings;
      reminders.value = response.items;
      reminderTotal.value = response.total;
      return response;
    } finally {
      loadingReminders.value = false;
    }
  };

  const fetchSettings = async (force = false) => {
    if (!force && settings.value && defaults.value) {
      return { settings: settings.value, defaults: defaults.value };
    }
    loadingSettings.value = true;
    try {
      const response = await getContractReminderSettings();
      settings.value = response.settings;
      defaults.value = response.defaults;
      return response;
    } finally {
      loadingSettings.value = false;
    }
  };

  const saveSettings = async (payload: Partial<ContractReminderSettings>) => {
    loadingSettings.value = true;
    try {
      const updated = await updateContractReminderSettings(payload);
      settings.value = updated;
      return updated;
    } finally {
      loadingSettings.value = false;
    }
  };

  return {
    summary,
    reminders,
    reminderTotal,
    settings,
    defaults,
    loadingSummary,
    loadingReminders,
    loadingSettings,
    reminderBuckets,
    expiredSummary,
    fetchSummary,
    fetchReminders,
    fetchSettings,
    saveSettings,
  };
});
