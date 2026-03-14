# P1 Performance Risk Mitigation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Eliminate the remaining P1 interaction bottlenecks from the 2026-02-19 report by reducing hot-path recomputation, deep reactive tracking overhead, and unnecessary rerenders in high-traffic RFQ and registration pages.

**Architecture:** Keep business behavior unchanged, but move repeated lookup/sorting work into indexed data structures and reduce broad reactive subscriptions. Refactor in vertical slices: first add regression guards, then optimize each hotspot component with minimal API surface change, then verify with focused tests and build.

**Tech Stack:** Vue 3 + TypeScript + Element Plus + Vitest.

---

## Scope (Only P1 Risks)

1. `app/apps/web/src/views/SupplierRegistrationView.vue` deep watch + full JSON clone for draft payload causes input lag under large form state.
2. `app/apps/web/src/components/RfqPriceComparisonTable.vue` rebuilds nested structures with repeated `find/filter/sort` on each recompute.
3. `app/apps/web/src/components/RfqLineItemWorkflowLayout.vue` uses repeated lookup patterns in computed branches for pending PO rows.
4. `app/apps/web/src/components/RfqQuoteComparison.vue` keeps debug logs and repeated quote traversals in derived computations.

---

### Task 1: Add Failing Regression Guards for P1 Hotspots

**Files:**
- Create: `app/apps/web/tests/performance/rfqDerivedData.spec.ts`
- Create: `app/apps/web/tests/performance/supplierRegistrationDraftTracking.spec.ts`
- Modify: `app/apps/web/package.json` (only if script alias is needed)

**Step 1: Write failing test for RFQ derived data indexing expectation**

Create focused unit tests for pure derivation helpers (to be extracted in next tasks):
- repeated lookup path should be replaced by indexed lookup path
- output rows must be stable with same business semantics

**Step 2: Write failing test for draft change tracking granularity**

Assert:
- changing a single field marks only that field as dirty
- autosave scheduling is throttled and not triggered by unrelated object branches

**Step 3: Run tests to verify red state**

Run:
- `npm run test -- --run tests/performance/rfqDerivedData.spec.ts tests/performance/supplierRegistrationDraftTracking.spec.ts`

Expected: FAIL because helper API/behavior is not implemented yet.

**Step 4: Commit**

```bash
git add app/apps/web/tests/performance/rfqDerivedData.spec.ts app/apps/web/tests/performance/supplierRegistrationDraftTracking.spec.ts app/apps/web/package.json
git commit -m "test: add p1 performance regression guards for rfq derivation and draft tracking"
```

---

### Task 2: Refactor Supplier Registration Draft Tracking to Field-Level Diff

**Files:**
- Modify: `app/apps/web/src/views/SupplierRegistrationView.vue`
- Create: `app/apps/web/src/utils/formChangeTracker.ts`
- Test: `app/apps/web/tests/performance/supplierRegistrationDraftTracking.spec.ts`

**Step 1: Extract lightweight change tracker helper**

Create utility functions:
- `computeChangedKeys(prev, next, watchedKeys)`
- `buildDraftPatch(form, changedKeys)`

Keep implementation shallow and deterministic for declared form keys.

**Step 2: Replace deep watch with grouped field watches**

In `SupplierRegistrationView.vue`:
- remove `watch(form, ..., { deep: true })`
- watch key groups (`basic`, `contact`, `payment`, `files`) and merge changed keys
- keep existing validation/preview clearing behavior unchanged

**Step 3: Replace full JSON clone payload with incremental payload**

Replace:
- `JSON.parse(JSON.stringify(...))`

With:
- typed payload builder using known fields
- array copy only where needed (`paymentMethods`)

**Step 4: Verify tests + build**

Run:
- `npm run test -- --run tests/performance/supplierRegistrationDraftTracking.spec.ts`
- `npm run build`

Expected: PASS.

**Step 5: Commit**

```bash
git add app/apps/web/src/views/SupplierRegistrationView.vue app/apps/web/src/utils/formChangeTracker.ts app/apps/web/tests/performance/supplierRegistrationDraftTracking.spec.ts
git commit -m "perf: replace deep draft watch with field-level tracking and incremental payload"
```

---

### Task 3: Index RFQ Price Comparison Data to Remove Nested Find/Sort Hot Paths

**Files:**
- Modify: `app/apps/web/src/components/RfqPriceComparisonTable.vue`
- Create: `app/apps/web/src/utils/rfqPriceComparisonIndex.ts`
- Test: `app/apps/web/tests/performance/rfqDerivedData.spec.ts`

**Step 1: Extract line-item key normalization and quote-item indexing helper**

Add helper:
- `buildQuoteItemIndex(quotes)` returning `Map<quoteId, Map<lineItemKey, quoteItem>>`
- shared key normalization to avoid repeated field probing

**Step 2: Refactor `tableData` computed to O(1) lookup path**

In `RfqPriceComparisonTable.vue`:
- build index once in computed dependency scope
- replace nested `find` calls in `findMatchingQuoteItem` path with index lookup
- keep fallback semantics for single-row/single-quote edge cases

**Step 3: Keep sort work minimal and deterministic**

- compute comparison value once per supplier detail
- sort using precomputed numeric field

**Step 4: Verify tests + build**

Run:
- `npm run test -- --run tests/performance/rfqDerivedData.spec.ts`
- `npm run build`

Expected: PASS.

**Step 5: Commit**

```bash
git add app/apps/web/src/components/RfqPriceComparisonTable.vue app/apps/web/src/utils/rfqPriceComparisonIndex.ts app/apps/web/tests/performance/rfqDerivedData.spec.ts
git commit -m "perf: index quote items to remove nested rfq price table lookups"
```

---

### Task 4: Optimize Workflow/Quote Comparison Derived Computations

**Files:**
- Modify: `app/apps/web/src/components/RfqLineItemWorkflowLayout.vue`
- Modify: `app/apps/web/src/components/RfqQuoteComparison.vue`
- Test: `app/apps/web/tests/performance/rfqDerivedData.spec.ts`

**Step 1: Pre-index quotes and selected line-item mappings in workflow layout**

In `RfqLineItemWorkflowLayout.vue`:
- add `quoteById` map computed
- add line-item-to-selected-quote projection map
- replace repeated `.find(...)` inside `pendingPoItemsWithQuotes`

**Step 2: Collapse repeated count/filter traversals**

- derive pending/completed/pending_po from one pass over `lineItems`
- expose reused derived object for template counts/lists

**Step 3: Remove runtime debug logs and merge quote-summary traversals**

In `RfqQuoteComparison.vue`:
- remove `console.log` inside computed
- compute `numericUnitPrices`, min/max/avg in one derived reducer

**Step 4: Verify tests + build**

Run:
- `npm run test -- --run tests/performance/rfqDerivedData.spec.ts`
- `npm run build`

Expected: PASS.

**Step 5: Commit**

```bash
git add app/apps/web/src/components/RfqLineItemWorkflowLayout.vue app/apps/web/src/components/RfqQuoteComparison.vue app/apps/web/tests/performance/rfqDerivedData.spec.ts
git commit -m "perf: reduce workflow and quote comparison recomputation overhead"
```

---

## Final Verification Gate

Run in order (from `app/apps/web`):

1. `npm run test -- --run tests/performance/rfqDerivedData.spec.ts tests/performance/supplierRegistrationDraftTracking.spec.ts`
2. `npm run test -- --run tests/composables/useVisibilityAwarePolling.spec.ts tests/stores/lockdown.spec.ts tests/stores/supplier.spec.ts`
3. `npm run build`

Expected:
- all tests pass
- build passes
- no behavior regression in RFQ selection flow, supplier registration draft save/load, and approval preview interactions

---

## Rollback Criteria

Rollback a task if any of the following appears:
- selected quote binding changes unexpectedly after refresh/reload
- draft autosave misses edits or sends malformed payload
- computed output order/content diverges from baseline snapshot tests

Rollback method:
- revert only the last task commit and re-run Final Verification Gate before proceeding.
