import { createApp, type Component, type ComponentPublicInstance, type Plugin } from "vue";
import type { MockServiceFactory, ServiceManager } from "../setup/mockServices";
import { startMockServices } from "../setup/mockServices";
import { SERVICE_MANAGER_SYMBOL } from "@/core/services";

export interface SetupTestServicesOptions {
  services?: Record<string, MockServiceFactory>;
}

export async function setupTestServices(options: SetupTestServicesOptions = {}) {
  return startMockServices(options.services ?? {});
}

export interface MountWithServicesOptions {
  services?: Record<string, MockServiceFactory>;
  props?: Record<string, unknown>;
  plugins?: Array<[Plugin<any[]>, any?]>;
  provide?: Record<PropertyKey, unknown>;
  mount?: (component: Component, options?: Record<string, unknown>) => unknown;
  mountOptions?: Record<string, unknown>;
}

export interface MountedComponent<T extends ComponentPublicInstance = ComponentPublicInstance> {
  app: ReturnType<typeof createApp>;
  container: HTMLElement;
  manager: ServiceManager;
  vm: T;
  unmount(): void;
}

export async function mountWithServices<T extends ComponentPublicInstance = ComponentPublicInstance>(
  component: Component,
  options: MountWithServicesOptions = {},
): Promise<MountedComponent<T>> {
  const manager = await setupTestServices({ services: options.services });

  if (options.mount) {
    return options.mount(component, {
      ...(options.mountOptions ?? {}),
      global: {
        provide: {
          [SERVICE_MANAGER_SYMBOL]: manager,
          ...(options.provide ?? {}),
        },
        ...(options.mountOptions?.global ?? {}),
      },
    }) as MountedComponent<T>;
  }

  const container = document.createElement("div");
  document.body.appendChild(container);
  const app = createApp(component, options.props);
  (options.plugins ?? []).forEach(([plugin, pluginOptions]) => {
    app.use(plugin, pluginOptions);
  });
  Object.entries(options.provide ?? {}).forEach(([key, value]) => {
    app.provide(key, value);
  });
  app.provide(SERVICE_MANAGER_SYMBOL, manager);
  const vm = app.mount(container) as T;

  return {
    app,
    container,
    manager,
    vm,
    unmount() {
      app.unmount();
      if (container.parentNode) {
        container.parentNode.removeChild(container);
      }
    },
  };
}
