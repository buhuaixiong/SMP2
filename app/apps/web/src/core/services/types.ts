import type { App } from "vue";
import type { RegistryManager } from "../registry";
import type { ServiceManager } from "./ServiceManager";

export type ServiceName = string;

export type ServiceStatus = "registered" | "starting" | "ready" | "stopped" | "failed";

export interface ServiceLogger {
  log?: (...args: unknown[]) => void;
  warn?: (...args: unknown[]) => void;
  error?: (...args: unknown[]) => void;
}

export interface ServiceManagerOptions {
  registry?: RegistryManager;
  app?: App;
  config?: Record<string, unknown>;
  category?: string;
  logger?: ServiceLogger;
}

export interface ServiceContext {
  app?: App;
  manager: ServiceManager;
  config: Record<string, unknown>;
  get<T = unknown>(name: ServiceName): T;
  has(name: ServiceName): boolean;
}

export interface ServiceDefinition<T = unknown> {
  name: ServiceName;
  dependencies?: ServiceName[];
  optional?: boolean;
  setup: (context: ServiceContext) => Promise<T> | T;
  teardown?: (instance: T, context: ServiceContext) => Promise<void> | void;
  description?: string;
}

export interface ServiceRecord<T = unknown> {
  definition: ServiceDefinition<T>;
  status: ServiceStatus;
  instance?: T;
  error?: Error;
}

export class ServiceStartError extends Error {
  public readonly service: ServiceName;
  public readonly cause: unknown;

  constructor(service: ServiceName, message: string, cause?: unknown) {
    super(message);
    this.service = service;
    this.cause = cause;
    this.name = "ServiceStartError";
  }
}
