import { defineStore } from "pinia";
import { computed, ref } from "vue";
import {
  getLockdownStatus,
  getFullLockdownStatus,
  activateLockdown as apiActivateLockdown,
  deactivateLockdown as apiDeactivateLockdown,
  getLockdownHistory as apiGetLockdownHistory,
  type LockdownStatus,
  type LockdownFullStatus,
  type ActivateLockdownParams,
  type LockdownHistoryEntry,
} from "@/api/system";

type LockdownUpdatePayload = {
  isActive?: boolean;
  announcement?: string | null;
  activatedAt?: string | null;
  reason?: string | null;
  activatedBy?: string | null;
};

type ApplyStatusOptions = {
  forceAnnouncement?: boolean;
};

export const useLockdownStore = defineStore("lockdown", () => {
  const isActive = ref(false);
  const announcement = ref<string | null>(null);
  const activatedAt = ref<string | null>(null);
  const reason = ref<string | null>(null);
  const activatedBy = ref<string | null>(null);
  const loading = ref(false);
  const history = ref<LockdownHistoryEntry[]>([]);
  const historyLoading = ref(false);
  const lastServerMessage = ref<string | null>(null);
  const announcementAcknowledged = ref(false);

  let pollInterval: number | null = null;
  let lastKnownActive = false;

  const applyStatus = (status: LockdownUpdatePayload, options: ApplyStatusOptions = {}) => {
    const hasIsActive = typeof status.isActive === "boolean";
    const active = hasIsActive ? Boolean(status.isActive) : isActive.value;
    const becameActive = hasIsActive && !lastKnownActive && active;
    const becameInactive = hasIsActive && lastKnownActive && !active;

    if (hasIsActive) {
      isActive.value = active;
      lastKnownActive = active;
    }

    if (Object.prototype.hasOwnProperty.call(status, "announcement")) {
      announcement.value = status.announcement ?? null;
    }
    if (Object.prototype.hasOwnProperty.call(status, "activatedAt")) {
      activatedAt.value = status.activatedAt ?? null;
    }
    if (Object.prototype.hasOwnProperty.call(status, "reason")) {
      reason.value = status.reason ?? null;
    }
    if (Object.prototype.hasOwnProperty.call(status, "activatedBy")) {
      activatedBy.value = status.activatedBy ?? null;
    }

    if (becameInactive) {
      announcementAcknowledged.value = false;
      lastServerMessage.value = null;
      announcement.value = null;
      activatedAt.value = null;
      reason.value = null;
      activatedBy.value = null;
    }

    if (becameActive || options.forceAnnouncement) {
      announcementAcknowledged.value = false;
    }
  };

  /**
   * Check current lockdown status (public endpoint)
   */
  const checkStatus = async () => {
    try {
      const status: LockdownStatus = await getLockdownStatus();
      applyStatus({
        isActive: status.isActive,
        announcement: status.announcement ?? null,
        activatedAt: status.activatedAt ?? null,
      });
    } catch (error) {
      console.error("[Lockdown Store] Error checking status:", error);
    }
  };

  /**
   * Get full lockdown status including internal details (admin only)
   */
  const checkFullStatus = async () => {
    loading.value = true;
    try {
      const status: LockdownFullStatus = await getFullLockdownStatus();
      applyStatus({
        isActive: status.isActive,
        announcement: status.announcement ?? null,
        activatedAt: status.activatedAt ?? null,
        reason: status.reason ?? null,
        activatedBy: status.activatedBy ?? null,
      });
    } catch (error) {
      console.error("[Lockdown Store] Error checking full status:", error);
      throw error;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Activate emergency lockdown (admin only)
   */
  const activate = async (params: ActivateLockdownParams) => {
    loading.value = true;
    try {
      const result = await apiActivateLockdown(params);
      applyStatus(
        {
          isActive: result.isActive,
          announcement: result.announcement ?? null,
          activatedAt: result.activatedAt ?? null,
          reason: result.reason ?? null,
          activatedBy: result.activatedBy ?? null,
        },
        { forceAnnouncement: true },
      );
      lastServerMessage.value = null;
      return result;
    } catch (error) {
      console.error("[Lockdown Store] Error activating lockdown:", error);
      throw error;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Deactivate emergency lockdown (admin only)
   */
  const deactivate = async () => {
    loading.value = true;
    try {
      const result = await apiDeactivateLockdown();
      applyStatus({
        isActive: result.isActive,
        announcement: result.announcement ?? null,
        activatedAt: result.activatedAt ?? null,
        reason: null,
        activatedBy: null,
      });
      return result;
    } catch (error) {
      console.error("[Lockdown Store] Error deactivating lockdown:", error);
      throw error;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Load lockdown history (admin only)
   */
  const loadHistory = async (limit: number = 50) => {
    historyLoading.value = true;
    try {
      history.value = await apiGetLockdownHistory(limit);
    } catch (error) {
      console.error("[Lockdown Store] Error loading history:", error);
      throw error;
    } finally {
      historyLoading.value = false;
    }
  };

  /**
   * Start polling lockdown status every 10 seconds
   */
  const startPolling = () => {
    if (pollInterval) {
      return; // Already polling
    }

    // Check immediately
    checkStatus();

    // Poll every 10 seconds
    pollInterval = window.setInterval(() => {
      checkStatus();
    }, 10000);
  };

  /**
   * Stop polling lockdown status
   */
  const stopPolling = () => {
    if (pollInterval) {
      clearInterval(pollInterval);
      pollInterval = null;
    }
  };

  /**
   * Reset store state
   */
  const reset = () => {
    stopPolling();
    history.value = [];
    loading.value = false;
    historyLoading.value = false;
    lastServerMessage.value = null;
    announcementAcknowledged.value = false;
    lastKnownActive = false;
    applyStatus({
      isActive: false,
      announcement: null,
      activatedAt: null,
      reason: null,
      activatedBy: null,
    });
  };

  /**
   * Sync store state using the payload returned from a 503 lockdown response
   */
  const syncFromLockdownResponse = (payload?: {
    message?: string | null;
    lockdown?: Partial<LockdownFullStatus>;
  }) => {
    const details = payload?.lockdown ?? {};
    applyStatus(
      {
        isActive: typeof details.isActive === "boolean" ? details.isActive : true,
        announcement: details.announcement ?? null,
        activatedAt: details.activatedAt ?? null,
      },
      { forceAnnouncement: true },
    );

    lastServerMessage.value = payload?.message ?? null;
  };

  const shouldShowAnnouncement = computed(() => isActive.value && !announcementAcknowledged.value);

  const acknowledgeAnnouncement = () => {
    announcementAcknowledged.value = true;
  };

  return {
    // State
    isActive,
    announcement,
    activatedAt,
    reason,
    activatedBy,
    loading,
    history,
    historyLoading,
    lastServerMessage,
    shouldShowAnnouncement,

    // Actions
    checkStatus,
    checkFullStatus,
    activate,
    deactivate,
    loadHistory,
    startPolling,
    stopPolling,
    reset,
    syncFromLockdownResponse,
    acknowledgeAnnouncement,
  };
});
