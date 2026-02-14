import { inject, type App } from "vue";
import { registry as defaultRegistry } from "../registry";
import { ServiceManager } from "./ServiceManager";
import type { ServiceManagerOptions } from "./types";

export const SERVICE_MANAGER_SYMBOL = Symbol("service-manager");

export type InstallServiceManagerOptions = Omit<ServiceManagerOptions, "app">;

export function createServiceManager(options: InstallServiceManagerOptions = {}): ServiceManager {
  return new ServiceManager({
    registry: options.registry ?? defaultRegistry,
    category: options.category,
    config: options.config,
    logger: options.logger,
  });
}

export function installServiceManager(app: App, options: InstallServiceManagerOptions = {}): ServiceManager {
  const manager = createServiceManager(options);
  manager.setApp(app);
  app.provide(SERVICE_MANAGER_SYMBOL, manager);
  return manager;
}

export function useServiceManager(): ServiceManager {
  const manager = inject<ServiceManager>(SERVICE_MANAGER_SYMBOL);
  if (!manager) {
    throw new Error("ServiceManager is not installed in the current Vue application");
  }
  return manager;
}

export * from "./ServiceManager";
export * from "./types";
