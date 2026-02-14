import type { RegistryAddOptions, RegistryEntry, RegistryEvent, RegistryListener } from "./types";

const DEFAULT_SEQUENCE = 100;

export class Registry<T> {
  private readonly entriesMap = new Map<string, RegistryEntry<T>>();
  private readonly listeners = new Set<RegistryListener<T>>();
  private entryCounter = 0;

  add(key: string, value: T, options: RegistryAddOptions = {}): this {
    if (!key) {
      throw new Error("Registry.add(key) requires a non-empty key");
    }

    const entry: RegistryEntry<T> = {
      key,
      value,
      metadata: options.metadata,
      sequence: options.sequence ?? DEFAULT_SEQUENCE,
      addedAt: this.entryCounter++,
    };

    this.entriesMap.set(key, entry);
    this.emit({ type: "add", entry });
    return this;
  }

  get(key: string, defaultValue?: T): T | undefined {
    return this.entriesMap.get(key)?.value ?? defaultValue;
  }

  has(key: string): boolean {
    return this.entriesMap.has(key);
  }

  remove(key: string): boolean {
    const entry = this.entriesMap.get(key);
    const deleted = this.entriesMap.delete(key);
    if (deleted) {
      this.emit({ type: "remove", entry });
    }
    return deleted;
  }

  clear(): void {
    if (this.entriesMap.size === 0) {
      return;
    }
    this.entriesMap.clear();
    this.emit({ type: "clear" });
  }

  getAll(): T[] {
    return this.sortedEntries().map((entry) => entry.value);
  }

  keys(): string[] {
    return this.sortedEntries().map((entry) => entry.key);
  }

  entries(): RegistryEntry<T>[] {
    return this.sortedEntries();
  }

  on(listener: RegistryListener<T>): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  private sortedEntries(): RegistryEntry<T>[] {
    return Array.from(this.entriesMap.values()).sort((a, b) => {
      if (a.sequence === b.sequence) {
        return a.addedAt - b.addedAt;
      }
      return a.sequence - b.sequence;
    });
  }

  private emit(event: RegistryEvent<T>): void {
    this.listeners.forEach((listener) => {
      try {
        listener(event);
      } catch (error) {
        console.error("[registry] listener failed", error);
      }
    });
  }
}

export class RegistryManager {
  private readonly categories = new Map<string, Registry<any>>();

  category<T>(name: string): Registry<T> {
    if (!name) {
      throw new Error("RegistryManager.category(name) requires a non-empty name");
    }
    const registry = this.categories.get(name) ?? new Registry<T>();
    this.categories.set(name, registry);
    return registry;
  }

  hasCategory(name: string): boolean {
    return this.categories.has(name);
  }

  removeCategory(name: string): boolean {
    return this.categories.delete(name);
  }

  clear(): void {
    this.categories.clear();
  }
}
