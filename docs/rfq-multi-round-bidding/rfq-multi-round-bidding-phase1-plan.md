# RFQ Multi-Round Bidding Phase 1 Implementation Plan

## Scope

This document breaks the multi-round RFQ bidding blueprint into a practical Phase 1 implementation plan.

Phase 1 focuses on the minimum safe rollout needed to support:

- round-aware RFQ data model,
- historical RFQs mapped into round 1,
- deadline extension for the current round,
- starting the next bidding round,
- latest-round-only award selection,
- latest-round-only default price comparison behavior.

Phase 1 does not aim to fully finish all round-history UI details. It is intended to establish the core backend and current-round user flow first.

## Business Rules In Scope

- Deadline extension applies only to the current round.
- Deadline extension is only allowed before the round is opened.
- Deadline extension is only allowed when there are invited suppliers who have not submitted quotes.
- A next round can only be created after the current round has been opened.
- Starting a next round immediately closes the previous round.
- Closed rounds are read-only.
- Suppliers for a next round are reselected by the purchaser.
- Suppliers who did not quote previously may still be invited into later rounds.
- Default price comparison should use the latest round only.
- Final award can only be selected from the latest round.

## Out Of Scope For Phase 1

- Full round-by-round supplier detailed comparison UI.
- PDF export or print redesign for round history.
- Fine-grained historical round analytics.
- Any supplier-side history page exposing prior round prices.

## Deliverables

Phase 1 deliverables:

1. New RFQ bid round entity and persistence layer.
2. Migration and data backfill to map legacy RFQs into round 1.
3. Round-aware RFQ store and visibility logic.
4. API to extend current round deadline.
5. API to start the next bidding round.
6. Backend validation that award selection must come from the latest round.
7. RFQ purchaser UI showing current round data and actions.
8. Default price comparison switched to the latest round.
9. Feature flag gating the multi-round experience.

## Backend File-Level Plan

### Domain Entities

#### Add

- `SupplierSystem/src/SupplierSystem.Domain/Entities/RfqBidRound.cs`

Suggested members:

- `Id`
- `RfqId`
- `RoundNumber`
- `BidDeadline`
- `Status`
- `OpenedAt`
- `ClosedAt`
- `CreatedBy`
- `CreatedAt`
- `UpdatedAt`
- `ExtensionReason`
- `StartedFromRoundId`

#### Update

- `SupplierSystem/src/SupplierSystem.Domain/Entities/Quote.cs`
  - add `BidRoundId`
- `SupplierSystem/src/SupplierSystem.Domain/Entities/SupplierRfqInvitation.cs`
  - add `BidRoundId`
- `SupplierSystem/src/SupplierSystem.Domain/Entities/RfqPriceAuditRecord.cs`
  - add `BidRoundId`
  - add `RoundNumber`

### EF Core / Data Layer

#### Update DbContext

- `SupplierSystem/src/SupplierSystem.Infrastructure/Data/SupplierSystemDbContext.cs`
  - add `DbSet<RfqBidRound>`

#### Add New Configuration

- `SupplierSystem/src/SupplierSystem.Infrastructure/Data/Configurations/RfqBidRoundEntityConfiguration.cs`

Configuration should map:

- table name `rfq_bid_rounds`
- key and column names
- unique index on `(rfq_id, round_number)`

#### Update Existing Configurations

- `SupplierSystem/src/SupplierSystem.Infrastructure/Data/Configurations/QuoteEntityConfiguration.cs`
  - map `BidRoundId -> bid_round_id`
- `SupplierSystem/src/SupplierSystem.Infrastructure/Data/Configurations/SupplierRfqInvitationEntityConfiguration.cs`
  - map `BidRoundId -> bid_round_id`
- `SupplierSystem/src/SupplierSystem.Infrastructure/Data/Configurations/RfqPriceAuditRecordEntityConfiguration.cs`
  - map `BidRoundId -> bid_round_id`
  - map `RoundNumber -> round_number`

### Migration Plan

#### Migration 1: Add Structures

Create:

- `rfq_bid_rounds`

Add nullable columns:

- `quotes.bid_round_id`
- `supplier_rfq_invitations.bid_round_id`
- `rfq_price_audit.bid_round_id`
- `rfq_price_audit.round_number`

#### Migration 2: Backfill Legacy Data

Backfill algorithm:

1. For each RFQ, create a `round 1` record.
2. Use RFQ `valid_until` as `bid_deadline`.
3. Set initial round status from RFQ status.
4. Assign the created round id to:
   - all quotes under that RFQ,
   - all supplier invitations under that RFQ,
   - all rfq price audit rows under that RFQ.
5. Set `round_number = 1` on backfilled audit rows.

Suggested initial round status mapping:

- RFQ `draft`, `published`, `in_progress` -> round `published`
- RFQ `under_review`, `closed` -> round `closed`

#### Migration 3: Add Indexes / Constraints

- unique `(rfq_id, round_number)`
- lookup indexes on new `bid_round_id` columns
- foreign keys where safe

#### Rollback Consideration

Keep Phase 1 schema additive and tolerant:

- new columns remain nullable initially,
- avoid immediate hard `not null` constraints,
- allow the feature flag to hide new behavior even if schema exists.

## Service / Store Plan

### `RfqWorkflowStore`

File:

- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqWorkflowStore.cs`

Add methods for:

- `FindCurrentBidRoundAsync(rfqId, ...)`
- `LoadBidRoundsAsync(rfqId, ...)`
- `CreateBidRound(...)`
- `CloseBidRound(...)`
- `ExtendBidRoundDeadline(...)`
- `LoadRoundInvitedSuppliersAsync(rfqId, bidRoundId, ...)`
- `LoadRoundQuotesAsync(rfqId, bidRoundId, ...)`
- `LoadLatestBidRoundQuotesAsync(rfqId, ...)`

Refactor existing RFQ detail aggregation to be able to work with:

- current round invitations,
- current round quotes,
- latest round selection.

### `QuoteVisibility`

File:

- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/QuoteVisibility.cs`

Current logic is RFQ-wide and must become current-round-aware.

New behavior:

- read current round,
- count invited suppliers in current round only,
- count submitted quotes in current round only,
- compare against current round deadline,
- lock/unlock quote visibility based on current round state.

### `RfqPriceAuditService`

File:

- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqPriceAuditService.cs`

Update audit writes so each record is associated with:

- `BidRoundId`
- `RoundNumber`

This is required for future round-aware comparison and print features.

## Controller / API Plan

### RFQ Detail Endpoints

Files likely impacted:

- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.RfqEndpoints.cs`

RFQ detail response should be extended to include:

- `currentRound`
- `rounds`
- current round invited supplier summary
- current round quote summary

### Extend Deadline Endpoint

Add endpoint:

- `POST /api/rfq-workflow/{id}/extend-bid-deadline`

Suggested request:

```json
{
  "newDeadline": "2026-03-26T12:00:00Z",
  "reason": "Waiting for remaining suppliers"
}
```

Validation rules:

- RFQ exists
- current round exists
- current round is `published`
- current round is not opened
- current round has pending invited suppliers
- new deadline is later than current deadline and current time

Audit should capture:

- old deadline
- new deadline
- reason
- actor

### Start Next Round Endpoint

Add endpoint:

- `POST /api/rfq-workflow/{id}/start-next-round`

Suggested request:

```json
{
  "deadline": "2026-03-30T12:00:00Z",
  "supplierIds": [101, 102, 205],
  "reason": "Round 1 pricing not competitive"
}
```

Behavior:

- verify current round is already opened,
- immediately set current round to `closed`,
- create round `N + 1`,
- create supplier invitations for selected suppliers,
- make round `N + 1` the current round.

Audit should capture:

- previous round number
- new round number
- supplier ids
- reason
- actor

### Quote Submission / Update / Withdraw Endpoints

Files likely impacted:

- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.QuoteEndpoints.cs`

Update validations so:

- supplier actions only target the current round,
- historical closed rounds reject changes,
- supplier cannot edit a quote from a closed round.

### Review / Selection Endpoints

Files likely impacted:

- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.ReviewEndpoints.cs`

Add validation:

- `selectedQuoteId` must belong to the latest round,
- selecting a quote from a historical round must fail with a clear message.

## Frontend File-Level Plan

### Type Definitions

File:

- `app/apps/web/src/types/index.ts`

Add:

- `RfqBidRound`
- `RfqBidRoundSummary`
- RFQ detail shape updates for:
  - `currentRound`
  - `rounds`

### API Layer

File:

- `app/apps/web/src/api/rfq.ts`

Add methods:

- `fetchRfqBidRounds`
- `extendRfqBidDeadline`
- `startNextRfqBidRound`

Update RFQ detail API typing to accept round-aware payloads.

### RFQ Detail View

File:

- `app/apps/web/src/views/RfqDetailView.vue`

Add:

- current round summary card
- extend deadline button
- start next round button
- latest-round-only comparison wiring

Add button visibility rules based on:

- purchaser role,
- current round status,
- pending suppliers,
- final RFQ state.

### New Components

Recommended new files:

- `app/apps/web/src/components/RfqBidRoundSummaryCard.vue`
- `app/apps/web/src/components/ExtendBidDeadlineDialog.vue`
- `app/apps/web/src/components/StartNextBidRoundDialog.vue`

These components should isolate current-round state and actions from the already large `RfqDetailView`.

### Supplier Quote Form

File:

- `app/apps/web/src/components/SupplierQuoteForm.vue`

Update to:

- show current round number,
- show current round deadline,
- reject editing when the RFQ page is bound to a closed historical round,
- not show historical round prices.

### Price Comparison Table

File:

- `app/apps/web/src/components/RfqPriceComparisonTable.vue`

Phase 1 target:

- default data source must be latest round only.

Historical round comparison UI can remain Phase 2.

### Localization

Files:

- `app/apps/web/src/locales/zh/rfq.json`
- `app/apps/web/src/locales/en/rfq.json`
- `app/apps/web/src/locales/th/rfq.json`

Add strings for:

- current round
- round number
- extend deadline
- start next round
- round closed
- latest round only
- cannot edit closed round
- next round supplier selection
- round deadline extension reason

## Feature Flag Integration

Recommended flag:

- `Features:RfqMultiRoundBidding`

Usage:

- backend can still expose schema-safe fallbacks when disabled,
- frontend should hide new controls when disabled,
- current single-round logic can remain the default when the flag is off.

## Suggested Implementation Order

1. Domain entity updates
2. EF configuration updates
3. Migration and backfill
4. `RfqWorkflowStore` round-aware methods
5. `QuoteVisibility` refactor
6. extend deadline endpoint
7. start next round endpoint
8. latest-round-only selection validation
9. frontend types and APIs
10. current round summary card and dialogs
11. RFQ detail integration
12. latest-round-only price comparison behavior
13. regression testing

## Testing Plan

### Backend Tests

Add or update tests for:

- historical RFQs backfilled into round 1
- extend deadline success path
- extend deadline blocked when round already opened
- extend deadline blocked when all suppliers already submitted
- start next round success path
- previous round becomes `closed`
- latest round becomes current round
- quotes in closed rounds cannot be modified
- award selection from historical round is rejected

### Frontend Tests

Add tests for:

- current round actions visibility
- extend deadline dialog validation
- next round supplier selection behavior
- latest-round-only comparison rendering
- no edit controls for historical rounds

### Manual Verification Checklist

- open a historical RFQ and confirm it behaves as round 1
- verify purchasers can extend deadline before opening
- verify purchasers can start round 2 after opening
- verify round 1 becomes closed immediately
- verify supplier from round 1 non-response can be selected for round 2
- verify default comparison only shows round 2 after round 2 exists
- verify final selection cannot use round 1 quote

## Rollback Strategy

### Code Rollback

Use the existing deployment backup approach before production release.

### Database Rollback

Phase 1 should remain rollback-friendly by:

- making schema additive,
- leaving new columns nullable initially,
- avoiding destructive rewrites,
- using the feature flag to disable behavior quickly.

If necessary:

- roll back code,
- turn off the feature flag,
- leave additive schema in place until a controlled cleanup window.

## Recommended Next Step After This Plan

Begin with the data layer implementation only:

1. add entities and EF config,
2. prepare migration and backfill,
3. add round-aware store methods,
4. stop before UI changes and validate the data model in isolation.
