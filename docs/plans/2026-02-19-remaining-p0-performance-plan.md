# Remaining P0 Performance Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove the remaining P0 bottlenecks confirmed on 2026-02-19: completeness batch update N+1/write amplification, fixed-interval polling without resilience controls, and pending-list hard-limit without true pagination.

**Architecture:** Keep behavior stable while moving bottleneck work to shared infrastructure. Backend changes should push from per-item DB round-trips to bounded batched queries/commits. Frontend polling should be centralized in one composable so every polling view gets visibility pause, in-flight dedupe, backoff, and jitter consistently.

**Tech Stack:** .NET 9 + EF Core + xUnit, Vue 3 + TypeScript + Vitest.

---

## Scope (Only Remaining P0)

1. `SupplierSystem/src/SupplierSystem.Infrastructure/Services/SupplierService.Completeness.cs` still loops per supplier and calls `SaveChangesAsync` per item.
2. `app/apps/web/src/views/ApprovalDashboardView.vue` and `app/apps/web/src/views/UpgradeManagementView.vue` still use fixed 30s interval polling without backoff/jitter/in-flight dedupe.
3. `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeRepository.cs` still uses `TOP (200)` hard cap instead of parameterized pagination.

---

### Task 1: Add Failing Regression Tests for Remaining P0

**Files:**
- Modify: `SupplierSystem/tests/SupplierSystem.Tests/Services/P0PerformanceOptimizationsTests.cs`
- Create: `app/apps/web/tests/composables/useVisibilityAwarePolling.spec.ts`

**Step 1: Write backend failing tests for completeness batching + pending pagination**

```csharp
[Fact]
public void SupplierCompletenessUpdate_ShouldUseBatchWritePattern() { ... }

[Fact]
public void PendingUpgradeList_ShouldUseOffsetFetchPagination() { ... }
```

Checks should fail until implementation is done:
- Completeness file should no longer contain per-item `SaveChangesAsync` flow.
- Repository should contain `OFFSET ... FETCH NEXT ...` and parameterized limit/offset.

**Step 2: Write frontend failing tests for polling resilience**

```ts
it("dedupes in-flight polling calls", async () => { ... });
it("applies exponential backoff with jitter after failures", async () => { ... });
it("pauses polling while hidden and refreshes on visibility restore", async () => { ... });
```

**Step 3: Run tests to verify red state**

Run:
- `dotnet test SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter "FullyQualifiedName~P0PerformanceOptimizationsTests"`
- `npm run test -- --run tests/composables/useVisibilityAwarePolling.spec.ts`

Expected: FAIL in new tests.

**Step 4: Commit**

```bash
git add SupplierSystem/tests/SupplierSystem.Tests/Services/P0PerformanceOptimizationsTests.cs app/apps/web/tests/composables/useVisibilityAwarePolling.spec.ts
git commit -m "test: add remaining P0 performance regression guards"
```

---

### Task 2: Refactor Completeness Update to Batched Read + Batched Commit

**Files:**
- Modify: `SupplierSystem/src/SupplierSystem.Infrastructure/Services/SupplierService.Completeness.cs`
- Modify: `SupplierSystem/src/SupplierSystem.Infrastructure/Services/SupplierService.Helpers.cs` (if helper extraction is needed)
- Test: `SupplierSystem/tests/SupplierSystem.Tests/Services/P0PerformanceOptimizationsTests.cs`

**Step 1: Implement bulk prefetch for documents and whitelist by supplier ids**

Use one query per dataset:
- `SupplierDocuments` where `SupplierId IN (...)`
- `SupplierDocumentWhitelists` where `SupplierId IN (...) AND IsActive = 1`

Materialize dictionaries:
- `Dictionary<int, List<SupplierDocumentResponse>>`
- `Dictionary<int, IReadOnlyList<string>>`

**Step 2: Replace per-item update flow with single batched persistence**

Inside `UpdateAllSuppliersCompletenessAsync`:
- Build updated supplier stubs and history rows in memory.
- Apply with `_context.Suppliers.UpdateRange(...)`.
- Apply with `_context.SupplierCompletionHistories.AddRange(...)`.
- Call `SaveChangesAsync` once per run (or bounded chunk, e.g. 200 rows/chunk).

**Step 3: Keep failure isolation without per-row commits**

On individual supplier calculation errors:
- Continue accumulating `result.Errors` and `result.Failed`.
- Do not abort whole run unless fatal prefetch error occurs.

**Step 4: Verify tests**

Run:
- `dotnet test SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter "FullyQualifiedName~P0PerformanceOptimizationsTests"`
- `dotnet build SupplierSystem.Api/SupplierSystem.Api.csproj --no-restore`

Expected: PASS.

**Step 5: Commit**

```bash
git add SupplierSystem/src/SupplierSystem.Infrastructure/Services/SupplierService.Completeness.cs SupplierSystem/src/SupplierSystem.Infrastructure/Services/SupplierService.Helpers.cs SupplierSystem/tests/SupplierSystem.Tests/Services/P0PerformanceOptimizationsTests.cs
git commit -m "perf: batch completeness reads and writes to remove per-item db round-trips"
```

---

### Task 3: Introduce Shared Visibility-Aware Polling with Backoff/Jitter

**Files:**
- Create: `app/apps/web/src/composables/useVisibilityAwarePolling.ts`
- Modify: `app/apps/web/src/views/ApprovalDashboardView.vue`
- Modify: `app/apps/web/src/views/UpgradeManagementView.vue`
- Test: `app/apps/web/tests/composables/useVisibilityAwarePolling.spec.ts`

**Step 1: Implement composable contract**

```ts
useVisibilityAwarePolling({
  task: async () => Promise<void>,
  baseIntervalMs: number,
  hiddenIntervalMs?: number,
  maxBackoffMultiplier?: number,
  jitterRatio?: number
})
```

Required behavior:
- Single in-flight task at a time.
- Exponential backoff on failure.
- Random jitter.
- Hidden tab pause.
- Immediate refresh on visibility restore.

**Step 2: Migrate `ApprovalDashboardView.vue`**

- Replace manual `setInterval`/listener logic with composable.
- Keep `refreshAll()` behavior unchanged.
- Ensure polling starts/stops on mount/unmount.

**Step 3: Migrate `UpgradeManagementView.vue`**

- Replace current interval logic with composable.
- Preserve state guards (`not_submitted`/`approved`/`returned`) before starting.

**Step 4: Verify tests and build**

Run:
- `npm run test -- --run tests/composables/useVisibilityAwarePolling.spec.ts tests/stores/lockdown.spec.ts tests/stores/supplier.spec.ts`
- `npm run build`

Expected: PASS.

**Step 5: Commit**

```bash
git add app/apps/web/src/composables/useVisibilityAwarePolling.ts app/apps/web/src/views/ApprovalDashboardView.vue app/apps/web/src/views/UpgradeManagementView.vue app/apps/web/tests/composables/useVisibilityAwarePolling.spec.ts
git commit -m "perf: unify resilient visibility-aware polling for approval and upgrade views"
```

---

### Task 4: Replace Pending List Hard Cap with Real Pagination

**Files:**
- Modify: `SupplierSystem/src/SupplierSystem.Api/Controllers/TempSuppliersController.cs`
- Modify: `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeService.Status.cs`
- Modify: `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeRepository.cs`
- Modify: `app/apps/web/src/api/upgrade.ts`
- Modify: `app/apps/web/src/views/ApprovalDashboardView.vue`
- Test: `SupplierSystem/tests/SupplierSystem.Tests/Services/P0PerformanceOptimizationsTests.cs`

**Step 1: Add request contract for pagination**

Controller accepts query:
- `page` (default 1, min 1)
- `pageSize` (default 50, max 200)

Service/repository receive normalized `offset` + `limit`.

**Step 2: Implement paged SQL**

Replace `TOP (200)` query with:

```sql
ORDER BY sua.submittedAt ASC
OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY;
```

Add total-count query for pagination metadata.

**Step 3: Return paged response shape**

Backend response:

```json
{
  "items": [...],
  "pagination": { "page": 1, "pageSize": 50, "total": 123, "hasMore": true }
}
```

**Step 4: Frontend integration**

- Update `fetchPendingUpgradeApplications` signature to pass pagination params.
- Update `ApprovalDashboardView.vue` to request first page on load and fetch correct page on page change.

**Step 5: Verify**

Run:
- `dotnet test SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter "FullyQualifiedName~P0PerformanceOptimizationsTests"`
- `dotnet build SupplierSystem.Api/SupplierSystem.Api.csproj --no-restore`
- `npm run build`

Expected: PASS.

**Step 6: Commit**

```bash
git add SupplierSystem/src/SupplierSystem.Api/Controllers/TempSuppliersController.cs SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeService.Status.cs SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeRepository.cs app/apps/web/src/api/upgrade.ts app/apps/web/src/views/ApprovalDashboardView.vue SupplierSystem/tests/SupplierSystem.Tests/Services/P0PerformanceOptimizationsTests.cs
git commit -m "perf: implement real paging for pending upgrade applications"
```

---

## Final Verification Gate

Run in order:

1. `dotnet build SupplierSystem.Api/SupplierSystem.Api.csproj --no-restore` (from `SupplierSystem/src`)
2. `dotnet test SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter "FullyQualifiedName~P0PerformanceOptimizationsTests"` (from `SupplierSystem/tests`)
3. `npm run test -- --run tests/composables/useVisibilityAwarePolling.spec.ts tests/stores/lockdown.spec.ts tests/stores/supplier.spec.ts` (from `app/apps/web`)
4. `npm run build` (from `app/apps/web`)

Expected:
- all commands pass
- no remaining P0 findings from the 2026-02-19 report checklist

