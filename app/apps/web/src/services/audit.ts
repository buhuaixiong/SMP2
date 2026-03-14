import type { ServiceDefinition } from "@/core/services";
import type { HttpService } from "./http";
import type { NotificationService } from "./notification";

export interface AuditEventPayload {
  entity: string;
  entityId?: string | number;
  action: "view" | "create" | "update" | "delete" | "custom";
  description?: string;
  data?: Record<string, unknown>;
  timestamp?: number;
}

export interface AuditService {
  log(event: AuditEventPayload): void;
  logView(entity: string, entityId?: string | number, description?: string): void;
  logCreate(entity: string, data?: Record<string, unknown>): void;
  logUpdate(entity: string, data?: Record<string, unknown>): void;
  logDelete(entity: string, entityId?: string | number, description?: string): void;
  flush(options?: { force?: boolean }): Promise<void>;
}

const FLUSH_INTERVAL = 15 * 1000;
const MAX_BATCH_SIZE = 20;
const AUDIT_ENDPOINT = "/audit/logs";

export const auditService: ServiceDefinition<AuditService> = {
  name: "audit",
  dependencies: ["http", "notification"],
  setup({ get }) {
    const http = get<HttpService>("http");
    const notification = get<NotificationService>("notification");
    const queue: AuditEventPayload[] = [];
    let flushing = false;
    let timer: ReturnType<typeof setInterval> | null = null;

    const push = (event: AuditEventPayload) => {
      queue.push({ ...event, timestamp: event.timestamp ?? Date.now() });
      if (queue.length >= MAX_BATCH_SIZE) {
        void flush({ force: true });
      }
    };

    const flush = async (options?: { force?: boolean }) => {
      if (flushing) {
        return;
      }
      if (queue.length === 0 && !options?.force) {
        return;
      }
      flushing = true;
      const batch = queue.splice(0, queue.length);
      try {
        if (batch.length === 0) {
          return;
        }
        await http.post(AUDIT_ENDPOINT, { events: batch }, { silent: true });
      } catch (error) {
        notification.warning("审计日志上传失败，将稍后重试", "Audit");
        // Put events back to queue for retry
        queue.unshift(...batch);
        throw error;
      } finally {
        flushing = false;
      }
    };

    const beforeUnload = () => {
      if (queue.length === 0) {
        return;
      }
      const payload = JSON.stringify({ events: queue });
      try {
        if (typeof navigator !== "undefined" && typeof navigator.sendBeacon === "function") {
          navigator.sendBeacon(`/api${AUDIT_ENDPOINT}`, payload);
        } else {
          void http.post(AUDIT_ENDPOINT, { events: queue }, { silent: true });
        }
      } catch {
        // ignore
      }
    };

    if (typeof window !== "undefined") {
      timer = setInterval(() => {
        void flush();
      }, FLUSH_INTERVAL);
      window.addEventListener("beforeunload", beforeUnload);
    }

    const api: AuditService & {
      __timer?: ReturnType<typeof setInterval> | null;
      __beforeUnload?: () => void;
      __queue?: AuditEventPayload[];
    } = {
      log(event) {
        push(event);
      },
      logView(entity, entityId, description) {
        push({ entity, entityId, action: "view", description });
      },
      logCreate(entity, data) {
        push({ entity, action: "create", data });
      },
      logUpdate(entity, data) {
        push({ entity, action: "update", data });
      },
      logDelete(entity, entityId, description) {
        push({ entity, entityId, action: "delete", description });
      },
      flush,
    };
    api.__timer = timer;
    api.__beforeUnload = beforeUnload;
    api.__queue = queue;
    return api;
  },
  teardown(instance) {
    const service = instance as AuditService & {
      __timer?: ReturnType<typeof setInterval> | null;
      __beforeUnload?: () => void;
      __queue?: AuditEventPayload[];
    };
    if (typeof window !== "undefined" && service.__beforeUnload) {
      window.removeEventListener("beforeunload", service.__beforeUnload);
    }
    if (service.__timer) {
      clearInterval(service.__timer);
    }
    if (service.__queue) {
      service.__queue.length = 0;
    }
  },
};
