import { registry } from "@/core/registry";
import type { ServiceDefinition } from "@/core/services";
import { auditService } from "./audit";
import type { AuditService } from "./audit";
import { cacheService } from "./cache";
import type { CacheService } from "./cache";
import { httpService } from "./http";
import type { HttpService } from "./http";
import { notificationService } from "./notification";
import type { NotificationService } from "./notification";
import { permissionService } from "./permission";
import type { PermissionService } from "./permission";

const SERVICE_SEQUENCE = {
  notification: 10,
  http: 20,
  permission: 30,
  cache: 40,
  audit: 50,
} as const;

const coreServices = [
  notificationService,
  httpService,
  permissionService,
  cacheService,
  auditService,
] as const;

export function registerServices() {
  const serviceRegistry = registry.category<ServiceDefinition>("services");
  coreServices.forEach((definition) => {
    if (serviceRegistry.has(definition.name)) {
      serviceRegistry.remove(definition.name);
    }
    const sequence = SERVICE_SEQUENCE[definition.name as keyof typeof SERVICE_SEQUENCE] ?? 100;
    serviceRegistry.add(definition.name, definition as ServiceDefinition, { sequence });
  });
}

export type { NotificationService, HttpService, PermissionService, CacheService, AuditService };
