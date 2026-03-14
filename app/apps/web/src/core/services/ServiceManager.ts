import type { App } from "vue";
import { registry as defaultRegistry, RegistryManager } from "../registry";
import {
  ServiceDefinition,
  ServiceLogger,
  ServiceManagerOptions,
  ServiceName,
  ServiceRecord,
  ServiceStartError,
  ServiceStatus,
} from "./types";

const DEFAULT_CATEGORY = "services";

export class ServiceManager {
  private readonly registry: RegistryManager;
  private readonly category: string;
  private readonly logger: ServiceLogger;
  private readonly config: Record<string, unknown>;
  private app?: App;
  private readonly instances = new Map<ServiceName, unknown>();
  private readonly records = new Map<ServiceName, ServiceRecord>();
  private readonly startOrder: ServiceName[] = [];
  private readonly startStack: ServiceName[] = [];
  private lastErrors: ServiceStartError[] = [];

  constructor(options: ServiceManagerOptions = {}) {
    this.registry = options.registry ?? defaultRegistry;
    this.category = options.category ?? DEFAULT_CATEGORY;
    this.logger = options.logger ?? console;
    this.app = options.app;
    this.config = { ...(options.config ?? {}) };
  }

  setApp(app: App): void {
    this.app = app;
  }

  getApp(): App | undefined {
    return this.app;
  }

  getConfig<T = unknown>(): Record<string, T> {
    return this.config as Record<string, T>;
  }

  async startAll(): Promise<void> {
    const serviceEntries = this.getServiceDefinitions();
    const errors: ServiceStartError[] = [];

    for (const entry of serviceEntries) {
      try {
        await this.start(entry.key);
      } catch (error) {
        const normalized = error instanceof ServiceStartError ? error : this.toStartError(entry.key, error);
        errors.push(normalized);
        this.logger.error?.(`[ServiceManager] Failed to start service "${entry.key}"`, normalized);
      }
    }

    this.lastErrors = errors;
    if (errors.length === 0) {
      this.logger.log?.("[ServiceManager] All services started successfully");
    }
  }

  async start<T = unknown>(name: ServiceName): Promise<T> {
    if (this.instances.has(name)) {
      return this.instances.get(name) as T;
    }

    const definition = this.getDefinition(name);
    if (!definition) {
      throw this.toStartError(name, new Error(`Service "${name}" is not registered`));
    }

    if (this.startStack.includes(name)) {
      const loopPath = [...this.startStack, name].join(" -> ");
      throw this.toStartError(name, new Error(`Circular dependency detected: ${loopPath}`));
    }

    const record = this.ensureRecord(name, definition);
    record.status = "starting";
    this.startStack.push(name);
    this.logger.log?.(`[ServiceManager] Starting service "${name}"`);

    try {
      const dependencies = definition.dependencies ?? [];
      for (const dep of dependencies) {
        await this.start(dep);
      }

      const context = this.createContext();
      const instance = await definition.setup(context);
      this.instances.set(name, instance);
      record.status = "ready";
      record.instance = instance;
      record.error = undefined;

      if (!this.startOrder.includes(name)) {
        this.startOrder.push(name);
      }

      return instance as T;
    } catch (error) {
      record.status = "failed";
      const wrappedError = this.toStartError(name, error);
      record.error = wrappedError;
      this.logger.error?.(`[ServiceManager] Service "${name}" failed to start`, wrappedError);
      throw wrappedError;
    } finally {
      this.startStack.pop();
    }
  }

  async stopAll(): Promise<void> {
    for (const name of [...this.startOrder].reverse()) {
      await this.stop(name);
    }
    this.startOrder.length = 0;
    this.instances.clear();
  }

  async stop(name: ServiceName): Promise<void> {
    if (!this.instances.has(name)) {
      return;
    }

    const record = this.records.get(name);
    if (!record || record.status !== "ready") {
      this.instances.delete(name);
      return;
    }

    const instance = record.instance;
    record.status = "stopped";

    if (instance && record.definition.teardown) {
      try {
        await record.definition.teardown(instance, this.createContext());
      } catch (error) {
        this.logger.error?.(`[ServiceManager] Service "${name}" teardown failed`, error);
      }
    }

    this.instances.delete(name);
  }

  get<T = unknown>(name: ServiceName): T {
    if (!this.instances.has(name)) {
      throw new Error(`Service "${name}" has not been started`);
    }
    return this.instances.get(name) as T;
  }

  has(name: ServiceName): boolean {
    return Boolean(this.getDefinition(name));
  }

  isStarted(name: ServiceName): boolean {
    return this.records.get(name)?.status === "ready";
  }

  getStatus(name: ServiceName): ServiceStatus | undefined {
    return this.records.get(name)?.status;
  }

  getLastErrors(): ServiceStartError[] {
    return this.lastErrors;
  }

  clearLastErrors(): void {
    this.lastErrors = [];
  }

  listServiceNames(): ServiceName[] {
    return this.getServiceRegistry().keys();
  }

  private getDefinition(name: ServiceName): ServiceDefinition | undefined {
    return this.getServiceRegistry().get(name);
  }

  private getServiceRegistry() {
    return this.registry.category<ServiceDefinition>(this.category);
  }

  private getServiceDefinitions(): Array<{ key: string; definition: ServiceDefinition }> {
    return this.getServiceRegistry()
      .entries()
      .map((entry) => ({
        key: entry.key,
        definition: entry.value,
      }));
  }

  private ensureRecord(name: ServiceName, definition: ServiceDefinition): ServiceRecord {
    const record =
      this.records.get(name) ??
      ({
        definition,
        status: "registered",
      } as ServiceRecord);

    record.definition = definition;
    this.records.set(name, record);
    return record;
  }

  private createContext() {
    return {
      app: this.app,
      manager: this,
      config: this.config,
      get: <T = unknown>(name: ServiceName) => this.get<T>(name),
      has: (name: ServiceName) => this.instances.has(name),
    };
  }

  private toStartError(name: ServiceName, error: unknown): ServiceStartError {
    if (error instanceof ServiceStartError) {
      return error;
    }
    const message = error instanceof Error ? error.message : String(error);
    return new ServiceStartError(name, message, error);
  }
}
