/// <reference types="vitest" />

import type { Mock } from "vitest";

// Mock factory type that creates mockable functions
type Mockable<T extends (...args: any[]) => any> = {
  [K in keyof T]: T[K] extends (...args: infer Args) => infer Return
    ? Mock<Args, Return>
    : never;
};

// Helper to create a mock object with Mock properties
export function createMock<T>(): T & { [K in keyof T]: T[K] extends (...args: any[]) => any ? Mock : T[K] } {
  const mockObj: Record<string, any> = {};
  for (const key of Object.keys(mockObj)) {
    mockObj[key] = vi.fn();
  }
  return mockObj as any;
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
