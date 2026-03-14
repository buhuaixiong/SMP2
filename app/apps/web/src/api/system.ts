import { apiFetch } from "./http";

export interface LockdownStatus {
  isActive: boolean;
  announcement?: string | null;
  activatedAt?: string | null;
}

export interface LockdownFullStatus extends LockdownStatus {
  reason?: string | null;
  activatedBy?: string | null;
}

export interface LockdownHistoryEntry {
  id: number;
  action: "LOCKDOWN_ACTIVATED" | "LOCKDOWN_DEACTIVATED";
  actorId: string;
  actorName: string;
  timestamp: string;
  changes: Record<string, unknown>;
  ipAddress?: string;
}

export interface ActivateLockdownParams {
  reason: string;
  announcement: string;
}

export interface LockdownActionResult {
  success: boolean;
  isActive: boolean;
  activatedAt?: string;
  deactivatedAt?: string;
  activatedBy?: string;
  deactivatedBy?: string;
  reason?: string;
  announcement?: string;
}

/**
 * Get current lockdown status (public endpoint)
 */
export const getLockdownStatus = async (): Promise<LockdownStatus> => {
  const response = await apiFetch<{ success: boolean; data: LockdownStatus }>("/system/lockdown");
  return response.data;
};

/**
 * Get full lockdown status including internal details (admin only)
 */
export const getFullLockdownStatus = async (): Promise<LockdownFullStatus> => {
  const response = await apiFetch<{ success: boolean; data: LockdownFullStatus }>(
    "/system/lockdown/full",
  );
  return response.data;
};

/**
 * Activate emergency lockdown (admin only)
 */
export const activateLockdown = async (
  params: ActivateLockdownParams,
): Promise<LockdownActionResult> => {
  const response = await apiFetch<{ success: boolean; data: LockdownActionResult }>(
    "/system/lockdown/activate",
    {
      method: "POST",
      body: params,
    },
  );
  return response.data;
};

/**
 * Deactivate emergency lockdown (admin only)
 */
export const deactivateLockdown = async (): Promise<LockdownActionResult> => {
  const response = await apiFetch<{ success: boolean; data: LockdownActionResult }>(
    "/system/lockdown/deactivate",
    {
      method: "POST",
    },
  );
  return response.data;
};

/**
 * Get lockdown activation/deactivation history (admin only)
 */
export const getLockdownHistory = async (limit: number = 50): Promise<LockdownHistoryEntry[]> => {
  const response = await apiFetch<{ success: boolean; data: LockdownHistoryEntry[] }>(
    "/system/lockdown/history",
    {
      params: { limit },
    },
  );
  return response.data;
};
