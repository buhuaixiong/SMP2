# RFQ Multi-Round Bidding Phase 4 Implementation Plan

## Scope

This document defines the Phase 4 execution plan for RFQ multi-round bidding.

Phase 4 focuses on hardening, consolidation, and regression safety after the core multi-round feature set has been delivered across Phases 1 through 3.

Phase 4 supports:

- shared comparison component extraction,
- stronger backend and frontend regression coverage,
- round-data consistency cleanup across detail, history, and print views,
- reduction of duplicated mapping and rendering logic,
- cleanup of known unrelated but blocking test debt that affects confidence in the repository.

## Goals

Phase 4 goals:

1. Make the multi-round RFQ implementation easier to maintain.
2. Add regression protection for the most important round-related workflows.
3. Ensure latest-round, current-round, history, and print outputs stay consistent.
4. Reduce component complexity in the RFQ comparison area.
5. Improve repository health by addressing known test debt that blocks full confidence.

## Business Rules In Scope

- Latest round remains the default operational comparison scope.
- Historical round data remains read-only.
- Suppliers continue to see only current-round data.
- Final award remains constrained to the latest round.
- Print and history data must not mix round scopes incorrectly.
- Purchaser round actions must continue to respect existing round-state rules.

## Out Of Scope For Phase 4

- New major business features beyond current multi-round scope.
- Cross-round analytics dashboards.
- Supplier trend scoring or recommendation engines.
- A new standalone RFQ analytics or reporting module.

## Why Phase 4 Is Needed

Phases 1 through 3 established the main feature set:

- round-aware data model,
- extend current round,
- start next round,
- latest-round-only default comparison,
- internal round history view,
- round-aware print scopes.

However, several high-value engineering gaps remain:

1. `RfqPriceComparisonTable.vue` is still carrying too much shared comparison rendering responsibility.
2. Automated regression coverage for round behavior is still incomplete.
3. Round mapping and display logic now exists in multiple frontend and backend surfaces and needs stronger consistency review.
4. A known unrelated failing backend test still reduces confidence in test results.

## Phase 4 Deliverables

1. A shared round comparison table presentation component.
2. Refactored latest-round and history views to reuse the shared table.
3. Backend tests for core multi-round scenarios.
4. Frontend tests for round actions, history, and print scope behavior.
5. Round data consistency cleanup across RFQ detail, history, and print.
6. Cleanup or resolution of the currently failing security hardening test.

## Primary Engineering Themes

### 1. Hardening

Protect key multi-round workflows with tests and explicit validation.

### 2. Consolidation

Reduce duplicated rendering and normalization logic across current round, history, and print features.

### 3. Confidence

Improve signal quality of builds and test runs so future round work can proceed safely.

## Frontend Plan

### Shared Comparison Rendering Extraction

Current issue:

- `app/apps/web/src/components/RfqPriceComparisonTable.vue` is doing too much.

Recommended Phase 4 action:

- introduce `app/apps/web/src/components/RfqRoundComparisonTable.vue` as a shared, presentation-focused component.

Recommended responsibility split:

- `RfqRoundComparisonTable.vue`
  - table rendering,
  - dynamic supplier columns,
  - recommendation display,
  - supplier detail entry points.
- `RfqPriceComparisonTable.vue`
  - latest-round operational wrapper,
  - data shaping specific to current RFQ detail use.
- `RfqSupplierComparisonHistoryDialog.vue`
  - dialog shell and round switching only,
  - delegate table body to `RfqRoundComparisonTable.vue`.

Expected benefit:

- future changes to comparison presentation are made once instead of in multiple places.

### RFQ Detail Consistency Review

File:

- `app/apps/web/src/views/RfqDetailView.vue`

Phase 4 tasks:

- audit all deadline reads to ensure current round is preferred over RFQ-level fallback,
- audit all quote visibility and summary displays for latest/current round consistency,
- reduce duplicated normalization logic where possible,
- make sure cache invalidation around round actions and history stays reliable.

### Print Preview Cleanup

File:

- `app/apps/web/src/components/RfqComparisonPrintPreviewDialog.vue`

Phase 4 tasks:

- review grouped all-round rendering for maintainability,
- simplify shared formatting logic,
- make sure `latest` and `all` views produce stable, easy-to-verify markup,
- consider extracting repeated print section rendering helpers if needed.

### Frontend Test Coverage

Target area:

- `app/apps/web/tests`

Recommended scenarios:

1. latest-round-only default comparison rendering,
2. round history dialog tab switching,
3. extend deadline dialog validation and refresh behavior,
4. start next round dialog validation and refresh behavior,
5. print scope toggle between latest and all,
6. supplier view without round history and management actions.

## Backend Plan

### Multi-Round Regression Tests

Target area:

- `SupplierSystem/tests/`

Recommended tests:

1. extend deadline succeeds when current round is unopened and has pending suppliers,
2. extend deadline fails when round is opened,
3. extend deadline fails when all suppliers already submitted,
4. start next round succeeds only after current round is opened,
5. start next round closes previous round immediately,
6. selected quote must come from latest round,
7. supplier comparison history returns grouped rounds in correct order,
8. print `scope=latest` excludes historical round rows,
9. print `scope=all` includes grouped round rows with correct round labels.

### Round Data Consistency Review

Files likely involved:

- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqWorkflowStore.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqComparisonPrintService.cs`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.RfqEndpoints.cs`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.BidRoundEndpoints.cs`

Phase 4 tasks:

- verify latest-round-only queries never accidentally mix historical rows,
- verify history queries never omit newly linked round-aware data,
- verify print groups line up with bid round summaries,
- review audit-to-round mapping logic for correctness and edge cases.

### Known Test Debt Cleanup

Current known failing test:

- `SupplierSystem/tests/SupplierSystem.Tests/Security/SecurityHardeningTests.cs:276`

Observed issue:

- missing `X-Legacy-Contract-Forwarded-To` header.

Phase 4 recommendation:

- investigate whether the implementation regressed or the test expectation is outdated,
- fix the implementation or update the test contract as appropriate,
- aim to restore a clean backend test run.

## Suggested File List

### Frontend

- `app/apps/web/src/components/RfqPriceComparisonTable.vue`
- `app/apps/web/src/components/RfqRoundComparisonTable.vue`
- `app/apps/web/src/components/RfqSupplierComparisonHistoryDialog.vue`
- `app/apps/web/src/components/RfqComparisonPrintPreviewDialog.vue`
- `app/apps/web/src/views/RfqDetailView.vue`
- `app/apps/web/src/api/rfq.ts`
- `app/apps/web/src/types/index.ts`
- `app/apps/web/tests/`

### Backend

- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqWorkflowStore.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqComparisonPrintService.cs`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.RfqEndpoints.cs`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.BidRoundEndpoints.cs`
- `SupplierSystem/tests/`

### Known Test Debt

- `SupplierSystem/tests/SupplierSystem.Tests/Security/SecurityHardeningTests.cs`

## Recommended Implementation Order

1. Add backend tests for core multi-round scenarios.
2. Add frontend tests for key round workflows.
3. Extract `RfqRoundComparisonTable.vue`.
4. Refactor current round and history views to reuse the new shared component.
5. Review and simplify round mapping in detail/history/print flows.
6. Investigate and fix the known security hardening test failure.
7. Run full focused validation again.

## Testing Plan

### Backend Validation

- `dotnet build`
- `dotnet test`

Expected outcome:

- all new multi-round tests pass,
- previously failing security test is either fixed or explicitly resolved.

### Frontend Validation

- `npx vue-tsc --noEmit -p tsconfig.build.json`
- `npm run build`
- targeted unit or component tests under `app/apps/web/tests`

Expected outcome:

- RFQ detail, history, and print flows continue to render correctly,
- new shared comparison component does not change business behavior.

### Manual Verification Checklist

1. RFQ detail still defaults to latest round comparison.
2. History dialog still shows all rounds correctly.
3. Print preview latest scope still matches latest round only.
4. Print preview all scope still shows grouped rounds.
5. Purchaser round actions still refresh correctly.
6. Supplier pages still show only current round data.

## Risks And Mitigations

### Risk: Shared Table Extraction Changes Behavior

Mitigation:

- extract presentation only,
- keep wrappers thin,
- validate with both latest-round and history scenarios before merging.

### Risk: More Test Work Than Expected

Mitigation:

- prioritize the highest-risk scenarios first,
- focus on round transitions, latest-only scope, and print scope correctness.

### Risk: Security Test Fix Is Unrelated And Time-Consuming

Mitigation:

- treat it as a dedicated subtask,
- separate multi-round verification from legacy contract investigation,
- but aim to restore a clean suite if feasible within the batch.

## Rollout Strategy

1. Complete tests before any further feature expansion.
2. Refactor shared comparison rendering with behavior locked by tests.
3. Re-run build and print/history verification after refactor.
4. Close out the known failing test to improve repository trust.

## Recommended Next Step After This Plan

Start Phase 4 with regression safety first:

1. backend tests,
2. frontend tests,
3. shared comparison table extraction,
4. round consistency cleanup,
5. known failing security test investigation.
