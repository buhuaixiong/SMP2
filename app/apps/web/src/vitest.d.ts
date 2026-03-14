/// <reference types="vitest" />

import type { Mock } from "vitest";

type MockFn = (...args: unknown[]) => unknown;

// Mock factory type that creates mockable functions
type Mockable<T extends MockFn> = {
  [K in keyof T]: T[K] extends (...args: infer Args) => infer Return
    ? Mock<Args, Return>
    : never;
};

// Helper to create a mock object with Mock properties
export function createMock<T>(): T & { [K in keyof T]: T[K] extends MockFn ? Mock : T[K] } {
  const mockObj: Record<string, Mock> = {};
  for (const key of Object.keys(mockObj)) {
    mockObj[key] = vi.fn();
  }
  return mockObj as T & { [K in keyof T]: T[K] extends MockFn ? Mock : T[K] };
}

// Partial mock for NotificationService with only required methods
export type MockNotificationService = {
  success: Mock;
  error: Mock;
  warning: Mock;
  info?: Mock;
  notify?: Mock;
  message?: Mock;
  confirm?: Mock;
  prompt?: Mock;
  alert?: Mock;
};

// Partial mock for ServiceManager with only required methods
export type MockServiceManager = {
  has: Mock;
  isStarted: Mock;
  get: Mock;
};
