# RFQ Multi-Round Bidding Phase 2 Implementation Plan

## Scope

This document defines the detailed execution plan for Phase 2 of RFQ multi-round bidding.

Phase 2 focuses on completing the internal comparison and reporting experience after the Phase 1 round model is in place.

Phase 2 supports:

- latest-round-only default purchaser comparison,
- internal multi-round supplier comparison history,
- round-aware print and export data,
- round-aware audit and attachment/reporting consistency,
- supplier-side protection so historical round prices remain hidden.

Phase 2 should preserve the current RFQ detail page interaction model and follow the established UI style already used in the system.

## Goals

Phase 2 goals:

1. Make the default operational comparison view strictly latest-round-only.
2. Let internal users inspect full round-by-round supplier comparison history.
3. Keep supplier-facing pages limited to the current round only.
4. Make print/export and reporting outputs aware of bid rounds.
5. Reuse existing RFQ page layout and shared Element Plus components wherever possible.

## Business Rules In Scope

- The default price comparison on the RFQ detail page shows the latest round only.
- Internal detailed comparison can show all rounds.
- Historical rounds are read-only.
- Supplier-facing pages must not show historical round prices or historical comparison data.
- Final award remains limited to the latest round.
- Historical reporting must remain traceable by round number.

## Out Of Scope For Phase 2

- Predictive pricing analytics across rounds.
- Automatic supplier ranking or trend recommendations across rounds.
- Major RFQ detail page redesign.
- Separate standalone multi-round comparison page unless later required.

## Current UI And Frontend Constraints

The current RFQ frontend already has a stable visual pattern that should be preserved.

Observed patterns:

- `app/apps/web/src/views/RfqDetailView.vue` uses `PageHeader`, stacked `el-card` sections, embedded `el-table`, and modal dialogs.
- `app/apps/web/src/components/RfqPriceComparisonTable.vue` already contains the main comparison table and supplier-detail dialog behavior.
- `app/apps/web/src/components/SupplierQuoteForm.vue` presents supplier work as a focused current-task card with RFQ summary above the form.
- `app/apps/web/src/components/PriceComparisonDetailDialog.vue` provides an existing dialog pattern for deeper drill-down detail.

Phase 2 should extend these patterns instead of introducing a new visual system.

## UI Design Principles

### Preserve Existing RFQ Detail Structure

Continue using:

- `PageHeader` for the top action area,
- `el-card` for major content sections,
- `el-descriptions` for summary metadata,
- `el-table` for structured line item and quote comparison data,
- `el-dialog` for secondary drill-down content,
- `el-tag` and `el-alert` for status and guidance.

Do not replace the RFQ detail page with a dashboard-style layout.

### Reuse Before Creating New Components

Prefer reusing or extracting from existing components before building entirely new ones.

Recommended reuse strategy:

- reuse `RfqPriceComparisonTable.vue` for latest-round comparison,
- extract reusable comparison table rendering logic from `RfqPriceComparisonTable.vue` for historical views,
- reuse dialog interaction patterns from `PriceComparisonDetailDialog.vue` and `RfqComparisonPrintPreviewDialog.vue`,
- keep supplier-side quoting inside `SupplierQuoteForm.vue` with only round-aware summary additions.

### Current Round Is The Primary View

The RFQ detail page should remain optimized for the operational current round.

Use a secondary interaction for history, such as:

- a `View Round History` button,
- a dialog with round tabs,
- or a card-level expandable history section.

Recommended choice for consistency:

- use an `el-dialog` with `el-tabs` by round.

This keeps the main page simple and aligned with the current product style.

## Phase 2 Deliverables

1. Latest-round-only purchaser comparison behavior.
2. Internal all-round supplier comparison history API.
3. RFQ detail UI entry point for round history.
4. Multi-round history dialog using reusable comparison components.
5. Round-aware print/export data models and endpoints.
6. Round-aware audit/report attachment consistency.
7. Localization coverage for Phase 2 texts.
8. Regression tests for latest-only and all-round visibility behavior.

## Backend File-Level Plan

### Data Model Extensions

Recommended schema additions beyond Phase 1:

- add `bid_round_id` to `price_comparison_attachments`,
- evaluate and add `bid_round_id` to `rfq_external_invitations`.

Files likely impacted:

- `SupplierSystem/src/SupplierSystem.Domain/Entities/PriceComparisonAttachment.cs`
- `SupplierSystem/src/SupplierSystem.Domain/Entities/RfqExternalInvitation.cs`
- `SupplierSystem/src/SupplierSystem.Infrastructure/Data/Configurations/PriceComparisonAttachmentEntityConfiguration.cs`
- `SupplierSystem/src/SupplierSystem.Infrastructure/Data/Configurations/RfqExternalInvitationEntityConfiguration.cs`
- `SupplierSystem/sql/`

### Migration / Backfill Plan

Add a new additive SQL script for Phase 2.

Suggested steps:

1. Add nullable `bid_round_id` columns.
2. Backfill legacy data to round 1.
3. Add supporting indexes.
4. Keep columns nullable for rollback safety.

Recommended indexes:

- `price_comparison_attachments(rfq_id, bid_round_id, line_item_id)`
- `rfq_external_invitations(rfq_id, bid_round_id, email)`

### Store / Query Layer

File:

- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqWorkflowStore.cs`

Add round-aware query methods for:

- latest-round price comparison attachments,
- all-round grouped price comparison attachments,
- latest-round quote comparison rows,
- all-round supplier comparison history,
- round-aware external invitation lookups if Phase 2 includes external history tracking.

Suggested methods:

- `LoadLatestRoundPriceComparisonAttachmentsAsync(rfqId, ...)`
- `LoadRoundPriceComparisonAttachmentsAsync(rfqId, bidRoundId, ...)`
- `LoadSupplierComparisonHistoryAsync(rfqId, ...)`
- `LoadRoundQuoteComparisonRowsAsync(rfqId, bidRoundId, ...)`

### Comparison / Print Services

Files:

- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqComparisonPrintService.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqComparisonPrintModels.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqPriceAuditService.cs`

Required changes:

- make print data round-aware,
- support latest-round-only print output,
- support all-round history export payloads,
- ensure audit records for comparison uploads and selection history remain tied to `bid_round_id` and `round_number`.

### API Layer

#### Keep RFQ Detail Lean

`GET /api/rfq-workflow/{id}` should remain the latest-round operational view.

That response should provide:

- current round summary,
- latest-round quotes,
- latest-round price comparisons,
- latest-round invited supplier status.

Do not overload this endpoint with full round-history payloads.

#### Add Supplier Comparison History Endpoint

Add endpoint:

- `GET /api/rfq-workflow/{id}/supplier-comparison-history`

Purpose:

- internal use only,
- returns grouped comparison data for all rounds,
- intended for dialog-level drill-down UI.

Suggested response shape:

```json
{
  "data": {
    "rfqId": 123,
    "currentRoundId": 12,
    "latestRoundId": 12,
    "rounds": [
      {
        "id": 11,
        "roundNumber": 1,
        "status": "closed",
        "bidDeadline": "2026-03-20T12:00:00Z",
        "openedAt": "2026-03-20T12:00:00Z",
        "closedAt": "2026-03-22T09:00:00Z",
        "invitedCount": 5,
        "submittedCount": 4,
        "quotes": [],
        "priceComparisons": []
      }
    ]
  }
}
```

#### Add Or Clarify Latest-Round Comparison Endpoint

Option A:

- continue deriving latest-round comparison from `GET /api/rfq-workflow/{id}`.

Option B:

- add `GET /api/rfq-workflow/{id}/price-comparison?scope=current`.

Recommended default:

- keep the RFQ detail endpoint as latest-round-only,
- add a separate history endpoint for all-round detail.

#### Extend Print / Export Endpoint

Current file:

- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.PrintEndpoints.cs`

Suggested enhancement:

- support `scope=latest|all`.

Behavior:

- `latest` for current operational print preview,
- `all` for multi-round print/export history.

## Frontend File-Level Plan

### Type Definitions

File:

- `app/apps/web/src/types/index.ts`

Add or extend types for:

- `RfqBidRoundHistoryEntry`
- `RfqRoundComparisonViewModel`
- `RfqSupplierComparisonHistoryResponse`
- `PriceComparisonAttachment` with `bidRoundId` and `roundNumber`
- `Rfq.currentRound`
- `Rfq.rounds`

Ensure round-aware typing is explicit rather than relying on loose `Record<string, unknown>` structures.

### API Layer

File:

- `app/apps/web/src/api/rfq.ts`

Add methods:

- `fetchRfqBidRounds`
- `fetchRfqSupplierComparisonHistory`
- `fetchLatestRoundPriceComparison` if separated from detail payload

Also update existing helpers so `fetchRfqWorkflow` and `fetchSupplierRfq` normalize round-aware fields consistently.

### RFQ Detail View

File:

- `app/apps/web/src/views/RfqDetailView.vue`

Required changes:

- show a current/latest round summary card above the comparison section,
- add a `View Round History` action for internal users only,
- keep the main `RfqPriceComparisonTable` wired to latest round only,
- show a small text hint that default comparison is scoped to the latest round,
- keep supplier mode free of any historical round UI.

### Recommended New Components

#### `RfqBidRoundHistoryCard.vue`

Recommended path:

- `app/apps/web/src/components/RfqBidRoundHistoryCard.vue`

Purpose:

- display current round summary,
- show compact round list or round badges,
- expose `View Round History` action.

Recommended content:

- round number,
- deadline,
- status,
- invited/submitted/pending summary,
- total round count.

#### `RfqSupplierComparisonHistoryDialog.vue`

Recommended path:

- `app/apps/web/src/components/RfqSupplierComparisonHistoryDialog.vue`

Purpose:

- show all-round supplier comparison history for internal users.

Recommended structure:

- `el-dialog`
- top summary area
- `el-tabs` by round
- round summary `el-descriptions`
- embedded reusable comparison table

#### `RfqRoundComparisonTable.vue`

Recommended path:

- `app/apps/web/src/components/RfqRoundComparisonTable.vue`

Purpose:

- extract shared rendering logic from `RfqPriceComparisonTable.vue`,
- avoid duplicating comparison table logic between latest-round and history views.

### Existing Component Refactor Recommendation

File:

- `app/apps/web/src/components/RfqPriceComparisonTable.vue`

Phase 2 should refactor this component just enough to separate:

- data shaping logic,
- table presentation,
- supplier detail dialog interaction.

Recommended split:

1. keep `RfqPriceComparisonTable.vue` as the latest-round operational wrapper,
2. extract reusable inner table markup into `RfqRoundComparisonTable.vue`,
3. keep `PriceComparisonDetailDialog.vue` reused by both wrappers.

### Supplier Quote Form

File:

- `app/apps/web/src/components/SupplierQuoteForm.vue`

Phase 2 tasks:

- show current round number more explicitly,
- continue using current round deadline only,
- avoid rendering any historical comparison or prior round totals,
- ensure any latest-round-only assumptions use `currentRound` first and RFQ-level deadline only as fallback.

### Localization

Files:

- `app/apps/web/src/locales/zh/rfq.json`
- `app/apps/web/src/locales/en/rfq.json`
- `app/apps/web/src/locales/th/rfq.json`

Add strings for:

- latest round only,
- round history,
- round comparison history,
- current round summary,
- historical rounds,
- total rounds,
- view round history,
- latest operational comparison,
- internal historical comparison.

## Detailed UI Plan

### RFQ Detail Page Layout

Recommended order on purchaser/internal view:

1. `PageHeader`
2. RFQ info card
3. current round summary card
4. supplier invitation card
5. latest-round comparison card
6. approval / print / PR related sections

The new round card should not replace RFQ info. It should sit between RFQ metadata and the quote comparison area.

### Current Round Summary Card Design

Use an `el-card` with the same title styling already used on the page.

Suggested content:

- title: `Current Round` or `Latest Round`
- round number tag
- status tag
- deadline display
- invited / submitted / pending counts
- `View Round History` button on the right

Suggested UI behavior:

- if only one round exists, show the summary card but keep the history button hidden or disabled,
- if multiple rounds exist, show a secondary note such as `Total 3 rounds`.

### Historical Comparison Dialog Design

Use `el-dialog` for consistency with current RFQ detail interactions.

Inside the dialog:

- top summary strip for RFQ title and total rounds,
- `el-tabs` where each tab is `Round 1`, `Round 2`, `Round 3`,
- per-tab `el-descriptions` for round metadata,
- a shared comparison table below,
- existing supplier detail drill-down remains available inside each round.

This is preferred over an inline expandable card because the comparison table is already large.

### Visual Style Rules

Follow existing RFQ styling conventions:

- neutral white cards,
- light gray section backgrounds only for nested info blocks,
- status communicated through `el-tag` color and concise text,
- no dashboard-like gradient summary cards,
- no new icon-heavy KPI strip unless it matches existing RFQ pages.

Color guidance:

- current/latest round: primary or success emphasis,
- closed historical rounds: info or warning,
- pending states: warning,
- read-only informational notes: standard `el-alert type="info"`.

## Component Reuse Matrix

### Direct Reuse

- `app/apps/web/src/components/PriceComparisonDetailDialog.vue`
- `app/apps/web/src/components/RfqComparisonPrintPreviewDialog.vue`
- `app/apps/web/src/components/layout/PageHeader.vue`
- Element Plus building blocks already used in RFQ views.

### Refactor And Reuse

- `app/apps/web/src/components/RfqPriceComparisonTable.vue`

Extract reusable pieces from it rather than copying the full component.

### Do Not Reuse For Phase 2 RFQ UI

- `app/apps/web/src/components/supplier-directory/SummaryCards.vue`

Reason:

- its dashboard-like summary-card style does not match the current RFQ detail page pattern and would introduce unnecessary visual inconsistency.

## Suggested Implementation Order

1. Add Phase 2 schema additions for attachment/invitation round linkage.
2. Backfill historical rows into round 1.
3. Add latest-round-only and all-round grouped store methods.
4. Implement supplier comparison history endpoint.
5. Update print/export service models for round scope.
6. Add frontend types and API methods.
7. Add current round summary card component.
8. Refactor comparison table for shared reuse.
9. Add all-round comparison dialog.
10. Integrate latest-only comparison and history entry into RFQ detail page.
11. Verify supplier-side history remains hidden.
12. Add tests and run regression validation.

## Testing Plan

### Backend Tests

Add or update tests for:

- latest-round comparison returns only latest round quotes,
- history endpoint returns all rounds in descending order,
- price comparison attachments are scoped by round,
- supplier endpoints do not expose historical round quotes,
- print/export `scope=latest` excludes historical rounds,
- print/export `scope=all` includes all rounds.

### Frontend Tests

Add tests for:

- RFQ detail page renders latest-round-only comparison by default,
- `View Round History` button is visible only to internal roles,
- history dialog renders round tabs correctly,
- switching tabs updates the comparison table correctly,
- supplier view never shows round history entry points,
- latest-round summary card reflects backend `currentRound` data.

### Manual Verification Checklist

- RFQ with only round 1 still renders correctly.
- RFQ with round 1 and round 2 shows only round 2 on the main comparison area.
- Internal history dialog shows both round 1 and round 2.
- Supplier sees only the current round quote form and current round invitation metadata.
- Historical round comparison remains read-only.
- Latest-round print output excludes old round rows.
- All-round export includes round grouping and round number traceability.

## Rollout And Rollback Strategy

### Rollout

1. Deploy additive Phase 2 schema.
2. Run Phase 2 backfill script.
3. Verify history queries in a local or controlled environment.
4. Enable frontend history UI only after API verification.
5. Smoke-test latest-only comparison and supplier-side visibility.

### Rollback

Keep Phase 2 rollback-safe by:

- using additive schema only,
- keeping new round-link fields nullable,
- preserving latest-round-only fallback through existing RFQ detail payload,
- allowing UI to hide history features if needed while schema remains in place.

If a rollback is required:

- roll back application code,
- hide Phase 2 UI,
- leave additive schema until controlled cleanup.

## Recommended Next Step After This Plan

Begin with the backend history package only:

1. add round linkage for price comparison attachments and external invitations,
2. add history query methods and grouped API response,
3. validate latest-only comparison behavior before starting UI work,
4. then implement the current round summary card and history dialog with extracted reusable comparison table logic.
