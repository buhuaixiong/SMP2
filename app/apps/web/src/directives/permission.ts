import type { ObjectDirective, DirectiveBinding } from "vue";
import type { PermissionService } from "@/services/permission";
import { SERVICE_MANAGER_SYMBOL, type ServiceManager } from "@/core/services";

type BindingValue = string | string[] | { value?: string | string[]; any?: boolean };

const getService = (binding: DirectiveBinding): PermissionService | null => {
  const instance = binding.instance;
  if (!instance || typeof instance !== 'object' || !('appContext' in instance)) {
    return null;
  }
  const manager = instance.appContext?.provides?.[SERVICE_MANAGER_SYMBOL as symbol] as
    | ServiceManager
    | undefined;
  if (!manager || !manager.has("permission") || !manager.isStarted("permission")) {
    return null;
  }
  try {
    return manager.get<PermissionService>("permission");
  } catch {
    return null;
  }
};

const toArray = (value: BindingValue): string[] => {
  if (!value) {
    return [];
  }
  if (typeof value === "string") {
    return value.split(",").map((item) => item.trim()).filter(Boolean);
  }
  if (Array.isArray(value)) {
    return value.map((item) => String(item));
  }
  if (typeof value === "object" && Array.isArray(value.value)) {
    return value.value.map((item) => String(item));
  }
  if (typeof value === "object" && typeof value.value === "string") {
    return value.value.split(",").map((item) => item.trim()).filter(Boolean);
  }
  return [];
};

const toggleElement = (el: HTMLElement, allowed: boolean) => {
  if (!allowed) {
    el.dataset.permissionDisplay = el.style.display || "";
    el.style.display = "none";
    el.setAttribute("aria-hidden", "true");
  } else {
    const defaultDisplay = el.dataset.permissionDisplay ?? "";
    el.style.display = defaultDisplay;
    el.removeAttribute("aria-hidden");
  }
};

const createPermissionDirective = (checker: (service: PermissionService, value: BindingValue) => boolean) => {
  const directive: ObjectDirective = {
    mounted(el, binding) {
      const service = getService(binding);
      if (!service) {
        toggleElement(el as HTMLElement, false);
        return;
      }
      toggleElement(el as HTMLElement, checker(service, binding.value));
    },
    updated(el, binding) {
      const service = getService(binding);
      if (!service) {
        toggleElement(el as HTMLElement, false);
        return;
      }
      toggleElement(el as HTMLElement, checker(service, binding.value));
    },
  };
  return directive;
};

export const permissionDirective = createPermissionDirective((service, value) => {
  const permissions = toArray(value);
  if (!permissions.length) {
    return true;
  }
  if (typeof value === "object" && value && "any" in value && value.any) {
    return service.hasAny(...permissions);
  }
  return service.hasAll(...permissions);
});

export const roleDirective = createPermissionDirective((service, value) => {
  const roles = toArray(value);
  if (!roles.length) {
    return true;
  }
  return service.hasRole(...roles);
});
