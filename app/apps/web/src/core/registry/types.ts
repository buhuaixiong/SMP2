export type RegistryEventType = "add" | "remove" | "clear";

export interface RegistryAddOptions {
  sequence?: number;
  metadata?: Record<string, unknown>;
}

export interface RegistryEntry<T> {
  key: string;
  value: T;
  sequence: number;
  metadata?: Record<string, unknown>;
  addedAt: number;
}

export interface RegistryEvent<T> {
  type: RegistryEventType;
  entry?: RegistryEntry<T>;
}

export type RegistryListener<T> = (event: RegistryEvent<T>) => void;
