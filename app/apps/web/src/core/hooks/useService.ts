import { computed } from "vue";
import { useServiceManager } from "../services";

function assertServiceName(name: string): asserts name is string {
  if (!name) {
    throw new Error("[useService] service name is required");
  }
}

export function useService<T>(serviceName: string): T {
  assertServiceName(serviceName);
  const manager = useServiceManager();
  if (!manager.has(serviceName)) {
    throw new Error(`[useService] Service "${serviceName}" is not registered`);
  }
  if (!manager.isStarted(serviceName)) {
    throw new Error(`[useService] Service "${serviceName}" has not been started`);
  }
  return manager.get<T>(serviceName);
}

export function useServices<T extends readonly string[]>(serviceNames: T) {
  if (!Array.isArray(serviceNames) || serviceNames.length === 0) {
    throw new Error("[useService] serviceNames should be a non-empty array");
  }
  const manager = useServiceManager();
  return computed(() => {
    return serviceNames.reduce((acc, name) => {
      if (!manager.has(name)) {
        throw new Error(`[useService] Service "${name}" is not registered`);
      }
      acc[name as T[number]] = manager.get(name);
      return acc;
    }, {} as Record<T[number], any>);
  }).value;
}
