# RFQ Multi-Round Bidding Phase 3 Implementation Plan

## Scope

This document defines the Phase 3 execution plan for RFQ multi-round bidding.

Phase 3 focuses on turning the current round-aware foundation into a fully operable purchaser workflow and a complete round-aware reporting output.

Phase 3 supports:

- purchaser-side round action UI,
- extend current round deadline from the RFQ detail page,
- start the next bidding round from the RFQ detail page,
- round-aware print preview and export scope selection,
- shared comparison rendering improvements to reduce duplicated logic,
- stronger regression coverage for latest-round and all-round scenarios.

## Goals

Phase 3 goals:

1. Let purchasers manage round progression directly in the RFQ detail page.
2. Complete the round-aware print and export experience.
3. Keep the default operational flow centered on the latest round.
4. Reuse existing RFQ detail patterns and shared components.
5. Reduce maintenance risk by extracting reusable comparison rendering pieces.

## Business Rules In Scope

- Purchasers can extend only the current round.
- Purchasers can start the next round only after the current round is opened.
- Starting a next round immediately closes the previous round.
- Supplier reselection remains required for a new round.
- Default comparison remains latest-round-only.
- Historical round views remain read-only.
- Supplier-facing pages continue to hide historical round prices.
- Print/export must support latest-round-only and all-round scopes explicitly.

## Out Of Scope For Phase 3

- Predictive pricing analytics across rounds.
- Supplier performance trend dashboards across rounds.
- Automatic round recommendation logic.
- A standalone RFQ round administration screen outside the RFQ detail page.

## Current Gaps After Phase 2

After the current implementation batch, the remaining high-value gaps are:

1. Purchasers cannot yet trigger `extend-bid-deadline` from the frontend.
2. Purchasers cannot yet trigger `start-next-round` from the frontend.
3. Print preview still uses a single-round RFQ-level output model.
4. Print/export does not yet support explicit `latest` versus `all` round scopes.
5. `RfqPriceComparisonTable.vue` is now reused more heavily and should be refactored slightly to avoid duplicated rendering logic in future batches.

## Phase 3 Deliverables

1. `Extend Deadline` purchaser dialog integrated into RFQ detail.
2. `Start Next Round` purchaser dialog integrated into RFQ detail.
3. Frontend API methods and payload types for both round actions.
4. Round-aware comparison print service with `scope=latest|all`.
5. Print preview UI with scope selection.
6. Shared comparison rendering extraction for latest-round and history use cases.
7. Regression tests for round actions and round-aware print/export behavior.

## UX And UI Principles

### Keep RFQ Detail As The Control Center

The RFQ detail page remains the main operational surface.

Phase 3 should continue using:

- `PageHeader` for high-level actions,
- `el-card` for grouped sections,
- `el-dialog` for secondary flows,
- `el-form` within dialogs,
- `el-alert` for business rule guidance,
- `el-tag` for round and status labeling.

Do not move round actions into a new page.

### Put Round Actions Near The Current Round Summary

Round actions should be visually associated with the current round card instead of being buried in unrelated sections.

Recommended placement:

- in the current round summary card header, or
- in the RFQ detail page header actions when space allows.

Recommended default:

- place the actions in or next to the current round summary card so users clearly understand that the action affects the current round.

### Use Dialogs For Multi-Step Operations

Both of the following actions should use dialogs rather than inline form expansion:

- extend current round deadline,
- start next round.

Reason:

- both require validation,
- both are higher-importance actions,
- the product already uses dialogs for detailed RFQ interactions.

## Backend Plan

### Round Action Endpoints

The backend endpoints already exist from Phase 1 foundation work:

- `POST /api/rfq-workflow/{id}/extend-bid-deadline`
- `POST /api/rfq-workflow/{id}/start-next-round`

Phase 3 backend work should focus on:

- making sure responses contain enough updated round summary data for the frontend,
- tightening validations where frontend needs explicit messages,
- ensuring audit trail entries remain consistent and round-aware.

Files likely impacted:

- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.BidRoundEndpoints.cs`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.BidRoundHelpers.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqWorkflowStore.cs`

### Print / Export Refactor

Current files:

- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.PrintEndpoints.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqComparisonPrintService.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqComparisonPrintModels.cs`

Current limitation:

- print output is still RFQ-level and not explicitly scoped by round.

Phase 3 backend changes:

1. Add `scope=latest|all` to the print endpoint.
2. Refactor `RfqComparisonPrintService` so it can build:
   - latest-round-only print data,
   - all-round grouped print data.
3. Extend print models to carry round summaries and grouped round sections.
4. Ensure audit rows and selected supplier rows remain traceable by round number.

### Recommended Print Model Changes

Keep existing top-level RFQ metadata, then add round-aware sections.

Suggested additions:

- `Scope`
- `CurrentRound`
- `Rounds`
- `LatestRoundSupplierSummary`
- `LatestRoundQuoteRows`
- `RoundGroups`

Suggested group shape:

- `RoundNumber`
- `Status`
- `BidDeadline`
- `InvitedSupplierCount`
- `SubmittedSupplierCount`
- `WithdrawnSupplierCount`
- `SupplierSummary[]`
- `QuoteRows[]`
- `AuditRows[]`

## Frontend Plan

### API Layer

File:

- `app/apps/web/src/api/rfq.ts`

Add methods:

- `extendRfqBidDeadline(rfqId, payload)`
- `startNextRfqBidRound(rfqId, payload)`
- update `fetchRfqComparisonPrintData` to support `scope`

Add payload types for:

- deadline extension request,
- next round creation request,
- print scope query.

### Type Layer

File:

- `app/apps/web/src/types/index.ts`

Add or extend types for:

- `ExtendRfqBidDeadlinePayload`
- `StartNextRfqBidRoundPayload`
- `RfqComparisonPrintScope`
- round-aware print group models

### RFQ Detail View

File:

- `app/apps/web/src/views/RfqDetailView.vue`

Phase 3 tasks:

- add purchaser-only action buttons for:
  - extend deadline,
  - start next round,
- wire both dialogs to refresh RFQ detail after success,
- clear round history cache after success so reopened history reflects the latest data,
- keep supplier role free of all round-management actions.

### New Dialog Components

#### `RfqExtendBidDeadlineDialog.vue`

Recommended path:

- `app/apps/web/src/components/RfqExtendBidDeadlineDialog.vue`

Fields:

- new deadline,
- reason.

Recommended behavior:

- show current round number,
- show current round deadline,
- block submit when no deadline is entered,
- show backend validation message if round can no longer be extended.

#### `RfqStartNextRoundDialog.vue`

Recommended path:

- `app/apps/web/src/components/RfqStartNextRoundDialog.vue`

Fields:

- next round deadline,
- reason,
- supplier selection.

Recommended behavior:

- show current round summary at the top,
- require at least one supplier,
- clearly indicate that a new round closes the previous round immediately,
- reuse existing supplier picker or supplier selection patterns from RFQ creation/edit flows.

### Print Preview Component

File:

- `app/apps/web/src/components/RfqComparisonPrintPreviewDialog.vue`

Phase 3 tasks:

- add a scope selector at the top:
  - `Latest Round`
  - `All Rounds`
- load print data based on selected scope,
- keep latest-round-only as the default scope,
- when `scope=all`, render grouped round sections in the preview instead of a single combined table.

Recommended rendering structure for `scope=all`:

1. RFQ basic information
2. round summary overview
3. a section per round
4. grouped supplier summary per round
5. grouped quote rows per round
6. grouped audit trail per round or a combined audit trail with round labels

### Shared Comparison Rendering Refactor

Files:

- `app/apps/web/src/components/RfqPriceComparisonTable.vue`
- `app/apps/web/src/components/RfqSupplierComparisonHistoryDialog.vue`

Phase 3 should extract one shared presentation component.

Recommended new component:

- `app/apps/web/src/components/RfqRoundComparisonTable.vue`

Purpose:

- provide a reusable table renderer,
- keep the latest-round wrapper lightweight,
- avoid future duplication in history, print, and round drill-down features.

## Suggested File List

### Backend

- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.BidRoundEndpoints.cs`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.PrintEndpoints.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqComparisonPrintService.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqComparisonPrintModels.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqWorkflowStore.cs`

### Frontend

- `app/apps/web/src/views/RfqDetailView.vue`
- `app/apps/web/src/api/rfq.ts`
- `app/apps/web/src/types/index.ts`
- `app/apps/web/src/components/RfqComparisonPrintPreviewDialog.vue`
- `app/apps/web/src/components/RfqPriceComparisonTable.vue`
- `app/apps/web/src/components/RfqSupplierComparisonHistoryDialog.vue`

### New Frontend Components

- `app/apps/web/src/components/RfqExtendBidDeadlineDialog.vue`
- `app/apps/web/src/components/RfqStartNextRoundDialog.vue`
- `app/apps/web/src/components/RfqRoundComparisonTable.vue`

### Localization

- `app/apps/web/src/locales/zh/rfq.json`
- `app/apps/web/src/locales/en/rfq.json`
- `app/apps/web/src/locales/th/rfq.json`

## Recommended Implementation Order

1. Add frontend payload types and API methods for round actions.
2. Build `RfqExtendBidDeadlineDialog.vue`.
3. Build `RfqStartNextRoundDialog.vue`.
4. Integrate both dialogs into `RfqDetailView.vue`.
5. Update detail page refresh and round-history cache invalidation after success.
6. Refactor print endpoint and service to support `scope=latest|all`.
7. Update print preview dialog to switch scopes and render all-round grouped content.
8. Extract reusable comparison table rendering into `RfqRoundComparisonTable.vue`.
9. Run frontend and backend regression validation.

## Testing Plan

### Backend Tests

Add or update tests for:

- extend deadline success path,
- extend deadline rejection after round opened,
- start next round success path,
- start next round rejection before round opened,
- print `scope=latest` output excluding historical rounds,
- print `scope=all` output including grouped rounds,
- selected quote visibility remaining latest-round-only.

### Frontend Tests

Add tests for:

- purchaser sees round management actions,
- supplier does not see round management actions,
- extend deadline dialog validates required inputs,
- start next round dialog requires supplier selection,
- successful action refreshes RFQ detail and round summary,
- print preview scope switch reloads data correctly,
- all-round preview renders grouped sections.

### Manual Verification Checklist

- Purchaser opens RFQ detail and can extend the current round.
- Purchaser opens RFQ detail and can start the next round after opening conditions are met.
- Starting the next round updates the current round summary immediately.
- Default comparison still shows only the latest round.
- Round history dialog still shows all rounds correctly after a new round is created.
- Print preview default scope shows latest round only.
- Print preview all-round scope shows grouped round sections.
- Supplier pages still show only current round data.

## Risks And Mitigations

### Risk: `RfqDetailView.vue` Keeps Growing

Mitigation:

- isolate round actions into dedicated dialogs,
- move heavy form logic into dialog components,
- keep the detail page responsible only for orchestration and refresh.

### Risk: Print Preview Becomes Too Complex

Mitigation:

- keep `latest` as the default scope,
- use grouped sections for `all` rather than overloading one giant mixed table,
- reuse shared renderers where possible.

### Risk: Environment Build Noise

Known environment issue:

- frontend full build may fail due to locked generated files such as `components.d.ts`.

Mitigation:

- use `vue-tsc` as a code-level validation step,
- treat file-lock errors separately from code correctness,
- rerun full build when the environment lock is cleared.

## Rollout Strategy

1. Complete the round action dialogs and frontend wiring.
2. Verify backend action and refresh behavior locally.
3. Complete print `scope=latest|all` backend support.
4. Update print preview UI and verify grouped round output.
5. Run targeted tests and manual end-to-end round lifecycle validation.

## Recommended Next Step After This Plan

Start with the purchaser-side round actions first:

1. add frontend API methods,
2. add extend deadline dialog,
3. add start next round dialog,
4. wire both into `RfqDetailView.vue`,
5. then finish print/export round scoping,
6. finally extract the shared comparison rendering layer.
