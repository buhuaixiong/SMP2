import { apiFetch } from "@/api/http";
import type { ServiceDefinition } from "@/core/services";
import { useAuthStore } from "@/stores/auth";

const PERMISSION_TTL = 5 * 60 * 1000;

export interface PermissionService {
  has(permission: string): boolean;
  hasAny(...permissions: string[]): boolean;
  hasAll(...permissions: string[]): boolean;
  hasRole(...roles: string[]): boolean;
  getRole(): string | null;
  getPermissions(): string[];
  refresh(force?: boolean): Promise<string[]>;
}

export const permissionService: ServiceDefinition<PermissionService> = {
  name: "permission",
  dependencies: ["http"],
  setup() {
    const authStore = useAuthStore();
    const permissionSet = new Set(authStore.user?.permissions ?? []);
    let role = authStore.user?.role ?? null;
    let lastSyncedAt = 0;

    const syncFromStore = () => {
      permissionSet.clear();
      (authStore.user?.permissions ?? []).forEach((permission) => permissionSet.add(permission));
      role = authStore.user?.role ?? null;
      lastSyncedAt = Date.now();
      return Array.from(permissionSet);
    };

    const ensureFresh = async (force?: boolean) => {
      const now = Date.now();
      if (!force && now - lastSyncedAt < PERMISSION_TTL && permissionSet.size > 0) {
        return Array.from(permissionSet);
      }
      const response = await apiFetch<{ user?: { permissions?: string[]; role?: string } }>("/auth/me");
      const permissions = response?.user?.permissions ?? [];
      permissionSet.clear();
      permissions.forEach((permission) => permissionSet.add(permission));
      role = response?.user?.role ?? role;
      lastSyncedAt = Date.now();
      return Array.from(permissionSet);
    };

    const has = (permission: string) => permissionSet.has(permission);

    const hasAny = (...permissions: string[]) => {
      if (permissions.length === 0) {
        return true;
      }
      return permissions.some((permission) => permissionSet.has(permission));
    };

    const hasAll = (...permissions: string[]) => {
      if (permissions.length === 0) {
        return true;
      }
      return permissions.every((permission) => permissionSet.has(permission));
    };

    const hasRole = (...roles: string[]) => {
      if (roles.length === 0) {
        return true;
      }
      return roles.some((candidate) => candidate === role);
    };

    return {
      has,
      hasAny,
      hasAll,
      hasRole,
      getRole: () => role,
      getPermissions: () => Array.from(permissionSet),
      refresh: (force?: boolean) => ensureFresh(force),
    };
  },
};
