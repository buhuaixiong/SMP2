import { computed, ref } from "vue";
import { useService } from "@/core/hooks";
import type { PermissionService } from "@/services";

export function usePermission() {
  const permissionService = useService<PermissionService>("permission");
  const role = ref(permissionService.getRole());
  const permissions = ref(permissionService.getPermissions());

  const sync = () => {
    role.value = permissionService.getRole();
    permissions.value = permissionService.getPermissions();
  };

  const refresh = async (force = true) => {
    const updated = await permissionService.refresh(force);
    permissions.value = updated;
    role.value = permissionService.getRole();
    return updated;
  };

  return {
    role: computed(() => role.value),
    permissions: computed(() => permissions.value),
    has: (permission: string) => permissionService.has(permission),
    hasAny: (...permissionList: string[]) => permissionService.hasAny(...permissionList),
    hasAll: (...permissionList: string[]) => permissionService.hasAll(...permissionList),
    hasRole: (...roles: string[]) => permissionService.hasRole(...roles),
    refresh,
    sync,
  };
}
