/**
 * Migration-time check for unmigrated apiFetch<any> usage.
 * @hidden Dev-only.
 */
export function checkUnmigratedApiCalls(): void {
  if (import.meta.env.PROD) return;

  console.warn(
    "[Migration] Please replace apiFetch<any> with explicit generics.\n" +
      "Run `npx vue-tsc --noEmit` to locate remaining any usages.",
  );
}
