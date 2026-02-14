import { RegistryManager } from "@/core/registry";
import { ServiceManager, type ServiceDefinition, type ServiceManagerOptions } from "@/core/services";

export type MockServiceFactory<T = unknown> = ServiceDefinition<T> | (() => ServiceDefinition<T> | Partial<ServiceDefinition<T>>);

function normalizeDefinition<T>(name: string, factory: MockServiceFactory<T>): ServiceDefinition<T> {
  if (typeof factory === "function") {
    const result = factory();
    if ("setup" in result) {
      return {
        name: result.name ?? name,
        dependencies: result.dependencies ?? [],
        optional: result.optional,
        setup: result.setup as ServiceDefinition<T>["setup"],
        teardown: result.teardown,
        description: result.description,
      };
    }
    return {
      name,
      dependencies: [],
      setup: result.setup as ServiceDefinition<T>["setup"],
      teardown: result.teardown,
    };
  }
  return {
    ...factory,
    name: factory.name ?? name,
  };
}

export function createMockServiceManager(
  services: Record<string, MockServiceFactory> = {},
  options: Omit<ServiceManagerOptions, "registry"> = {},
) {
  const registryManager = new RegistryManager();
  const registry = registryManager.category<ServiceDefinition>("services");
  let sequence = 10;
  Object.entries(services).forEach(([name, factory]) => {
    const definition = normalizeDefinition(name, factory);
    registry.add(definition.name, definition, { sequence });
    sequence += 10;
  });
  return new ServiceManager({ ...options, registry: registryManager });
}

export async function startMockServices(
  services: Record<string, MockServiceFactory> = {},
  options: Omit<ServiceManagerOptions, "registry"> = {},
) {
  const manager = createMockServiceManager(services, options);
  await manager.startAll();
  return manager;
}

export type { ServiceManager } from "@/core/services";
