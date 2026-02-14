import type { ServiceDefinition } from "@/core/services";

interface CacheEntry<T> {
  value: T;
  expiresAt?: number;
}

export interface CacheService {
  set<T>(key: string, value: T, ttlMs?: number): void;
  get<T>(key: string): T | undefined;
  has(key: string): boolean;
  delete(key: string): boolean;
  clear(): void;
  getOrSet<T>(key: string, factory: () => T | Promise<T>, ttlMs?: number): Promise<T>;
  cleanup(): void;
}

const DEFAULT_TTL = 5 * 60 * 1000;
const CLEANUP_INTERVAL = 60 * 1000;

export const cacheService: ServiceDefinition<CacheService> = {
  name: "cache",
  setup() {
    const store = new Map<string, CacheEntry<any>>();
    let cleanupTimer: ReturnType<typeof setInterval> | null = null;

    const cleanup = () => {
      const now = Date.now();
      for (const [key, entry] of store.entries()) {
        if (entry.expiresAt && entry.expiresAt <= now) {
          store.delete(key);
        }
      }
    };

    cleanupTimer = setInterval(cleanup, CLEANUP_INTERVAL);

    const api: CacheService & { __cleanupTimer?: ReturnType<typeof setInterval> | null } = {
      set(key, value, ttlMs = DEFAULT_TTL) {
        store.set(key, {
          value,
          expiresAt: ttlMs === Infinity ? undefined : Date.now() + ttlMs,
        });
      },
      get(key) {
        const entry = store.get(key);
        if (!entry) {
          return undefined;
        }
        if (entry.expiresAt && entry.expiresAt <= Date.now()) {
          store.delete(key);
          return undefined;
        }
        return entry.value;
      },
      has(key) {
        return this.get(key) !== undefined;
      },
      delete(key) {
        return store.delete(key);
      },
      clear() {
        store.clear();
      },
      async getOrSet<T>(key: string, factory: () => T | Promise<T>, ttlMs = DEFAULT_TTL): Promise<T> {
        const existing = this.get<T>(key);
        if (existing !== undefined) {
          return existing;
        }
        const value = await factory();
        this.set(key, value, ttlMs);
        return value;
      },
      cleanup,
    };
    api.__cleanupTimer = cleanupTimer;
    return api;
  },
  teardown(instance) {
    const timer = (instance as any).__cleanupTimer as ReturnType<typeof setInterval> | null;
    if (timer) {
      clearInterval(timer);
      (instance as any).__cleanupTimer = null;
    }
    instance.clear();
  },
};
