# RFQ Multi-Round Bidding Blueprint

## Goal

Add multi-round bidding to RFQs so purchasers can:

- extend the bid opening deadline for the current round when some suppliers have not submitted quotes yet,
- start a new bidding round after opening when round results are unsatisfactory,
- reselect suppliers for each new round, including suppliers who did not quote in earlier rounds,
- view only the latest round in default price comparison,
- view full round-by-round price history in supplier detailed comparison,
- award only from the final round.

This blueprint is based on current SMP behavior and is intended to guide a rollback-safe, phased implementation.

## Confirmed Business Rules

- Deadline extension applies only to the current round.
- Deadline extension is allowed only before the current round is opened.
- Deadline extension is allowed only when at least one invited supplier has not submitted a quote.
- A new bidding round can be started only after the current round has been opened.
- When a new round starts, the previous round immediately becomes `closed`.
- Closed historical rounds are read-only and cannot be edited.
- Suppliers for a new round are reselected by the purchaser.
- Suppliers who did not quote in a previous round may still be invited into a later round.
- Default RFQ price comparison shows only the latest round.
- Supplier detailed comparison shows all rounds.
- Supplier-facing pages must not show historical round prices.
- Final award can only be selected from the latest round.

## Current System Constraints

The current RFQ workflow is single-round and centered on RFQ-level deadline and visibility rules.

- RFQ deadline is stored on `rfqs.valid_until` and reused throughout quote visibility and quote submission.
- Quote visibility is currently RFQ-scoped, not round-scoped.
- Quotes do not contain a bid round identifier.
- Supplier invitations do not contain a bid round identifier.
- RFQ price audit records do not contain a bid round identifier.

Relevant current files:

- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/QuoteVisibility.cs`
- `SupplierSystem/src/SupplierSystem.Domain/Entities/Quote.cs`
- `SupplierSystem/src/SupplierSystem.Domain/Entities/SupplierRfqInvitation.cs`
- `SupplierSystem/src/SupplierSystem.Domain/Entities/RfqPriceAuditRecord.cs`

## High-Level Design

Do not create a second RFQ record for second-round bidding.

Instead:

- keep one RFQ as the business document,
- add explicit RFQ bidding rounds under that RFQ,
- treat the latest round as the current operational round,
- keep historical rounds immutable after they close.

This avoids fragmenting business history across multiple RFQ IDs and keeps price comparison, approval, and auditing traceable under one RFQ.

## Data Model Design

### New Table: `rfq_bid_rounds`

Recommended fields:

- `id bigint identity primary key`
- `rfq_id bigint not null`
- `round_number int not null`
- `bid_deadline datetime2(3) not null`
- `status nvarchar(32) not null`
- `opened_at datetime2(3) null`
- `closed_at datetime2(3) null`
- `created_by nvarchar(128) null`
- `created_at datetime2(3) not null`
- `updated_at datetime2(3) null`
- `extension_reason nvarchar(1000) null`
- `started_from_round_id bigint null`

Recommended constraints/indexes:

- `unique(rfq_id, round_number)`
- `index(rfq_id, status)`
- foreign key `rfq_id -> rfqs.id`
- foreign key `started_from_round_id -> rfq_bid_rounds.id`

### Existing Tables To Extend

#### `quotes`

Add:

- `bid_round_id bigint null`

Recommended index:

- `index(rfq_id, bid_round_id, supplier_id, is_latest)`

#### `supplier_rfq_invitations`

Add:

- `bid_round_id bigint null`

Recommended index:

- `index(rfq_id, bid_round_id, supplier_id)`

#### `rfq_price_audit`

Add:

- `bid_round_id bigint null`
- `round_number int null`

Recommended index:

- `index(rfq_id, bid_round_id, supplier_id, rfq_line_item_id)`

### Additional Recommended Extensions

Also evaluate adding `bid_round_id` to:

- `price_comparison_attachments`
- `rfq_external_invitations`

This keeps platform comparisons and public invitation flows aligned with round behavior.

## Migration Strategy

### Step 1: Structural Migration

- Create `rfq_bid_rounds`.
- Add nullable `bid_round_id` to `quotes`.
- Add nullable `bid_round_id` to `supplier_rfq_invitations`.
- Add nullable `bid_round_id` and `round_number` to `rfq_price_audit`.

### Step 2: Historical Data Backfill

For every existing RFQ:

- create `round 1`,
- set `bid_deadline` from `rfqs.valid_until`,
- derive round status from RFQ status.

Suggested status mapping:

- RFQ `draft`, `published`, `in_progress` -> round `published`
- RFQ `under_review`, `closed` -> round `closed`

Then backfill:

- all existing `quotes` -> `round 1`
- all existing `supplier_rfq_invitations` -> `round 1`
- all existing `rfq_price_audit` rows -> `round 1`

### Step 3: Constraints And Indexes

- add foreign keys,
- add indexes,
- add uniqueness constraints.

### Step 4: Tightening Nullability Later

Keep new fields nullable during initial rollout.

After code and data are stable:

- enforce non-null `bid_round_id` where appropriate.

## Feature Flag

Add a feature flag:

- `Features:RfqMultiRoundBidding`

Rollout recommendation:

1. deploy code compatible with new schema,
2. run migration,
3. backfill round 1,
4. keep flag off for initial verification,
5. enable feature after smoke testing.

This gives a safer rollback path even if schema changes remain in place.

## Round Status Model

Suggested round statuses:

- `published` - accepting quotes
- `opened` - bid opening completed, comparison allowed
- `closed` - read-only historical round

Rules:

- deadline extension is only allowed in `published`,
- once the round is opened, suppliers can no longer modify that round,
- starting a new round immediately transitions the previous round to `closed`,
- closed rounds are immutable.

## RFQ Main Status Compatibility

Keep RFQ-level status fields for backward compatibility:

- `rfqs.status`
- `rfqs.selected_quote_id`
- `rfqs.review_completed_at`

Interpretation after round support:

- RFQ-level fields represent the final or current latest-round snapshot,
- round-level fields drive quote submission, opening, and comparison.

## API Design

### Extend Current Round Deadline

`POST /api/rfq-workflow/{id}/extend-bid-deadline`

Request body:

```json
{
  "newDeadline": "2026-03-26T12:00:00Z",
  "reason": "Waiting for remaining suppliers"
}
```

Validation:

- RFQ exists,
- current round exists,
- current round is `published`,
- current round has at least one supplier not yet submitted,
- `newDeadline` is later than now,
- `newDeadline` is later than the current round deadline.

### Start Next Round

`POST /api/rfq-workflow/{id}/start-next-round`

Request body:

```json
{
  "deadline": "2026-03-30T12:00:00Z",
  "supplierIds": [101, 102, 205],
  "reason": "Round 1 pricing not competitive"
}
```

Validation:

- RFQ exists,
- current round exists,
- current round is already opened,
- supplier list is not empty,
- RFQ is still in a state that allows additional bidding.

Behavior:

- set previous current round to `closed`,
- create round `N + 1`,
- create round invitations for selected suppliers,
- make new round current.

### Get Bid Rounds Summary

`GET /api/rfq-workflow/{id}/bid-rounds`

Example response:

```json
{
  "data": [
    {
      "id": 11,
      "roundNumber": 1,
      "status": "closed",
      "bidDeadline": "2026-03-20T12:00:00Z",
      "invitedCount": 5,
      "submittedCount": 4,
      "withdrawnCount": 1
    },
    {
      "id": 12,
      "roundNumber": 2,
      "status": "published",
      "bidDeadline": "2026-03-30T12:00:00Z",
      "invitedCount": 3,
      "submittedCount": 1,
      "withdrawnCount": 0
    }
  ]
}
```

### Default Price Comparison

`GET /api/rfq-workflow/{id}/price-comparison?scope=current`

- returns only latest round data.

### Supplier Detailed Comparison History

`GET /api/rfq-workflow/{id}/supplier-comparison-history`

- returns grouped data for round 1, round 2, round 3, etc.

## Backend Logic Changes

### Quote Visibility

Replace RFQ-level deadline visibility with current-round deadline visibility.

Current logic to rework:

- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/QuoteVisibility.cs`

New rules:

- compare only current round invited suppliers,
- count only current round submitted quotes,
- unlock only if all invited suppliers in the current round submitted or current round deadline passed.

### Quote Submission / Update / Withdrawal

These actions must become round-aware.

Rules:

- supplier can only submit/update/withdraw in the latest current round,
- historical rounds reject modifications,
- supplier page should not expose historical round prices.

### Invitations

Invitation queries and reminder logic must become round-aware.

Current invitation records are RFQ-wide and need filtering by `bid_round_id` once available.

### Review And Award

Award selection must enforce:

- `selectedQuoteId` belongs to the latest round,
- historical-round quotes cannot be selected.

### Audit

Audit should record:

- deadline extension,
- next-round creation,
- invited supplier list for each round,
- opening and closing of each round,
- final award from the latest round.

## Frontend Design

### Purchaser RFQ Detail View

Add a current round summary card showing:

- round number,
- deadline,
- status,
- invited count,
- submitted count,
- withdrawn count.

Add actions:

- `延期开标` / `Extend Deadline`
- `发起下一轮投标` / `Start Next Round`

### Supplier RFQ View

Show only the current round:

- current round number,
- current round deadline,
- quote form for current round only.

Do not show:

- historical round prices,
- historical round editing actions.

### Default Price Comparison

The existing comparison area should default to latest round only.

This becomes the operational decision view.

### Supplier Detailed Comparison

Show all rounds.

Recommended UI:

- tabs per round, or
- expandable grouped comparison by supplier and round.

## Permission Matrix

### Purchaser

- extend deadline: allowed
- start next round: allowed
- view current-round comparison: allowed
- view multi-round detailed comparison: allowed
- final award from latest round: allowed

### Procurement Manager

- view current-round comparison: allowed
- view multi-round detailed comparison: allowed
- start next round: not granted by default in this blueprint

### Procurement Director

- view current-round comparison: allowed
- view multi-round detailed comparison: allowed
- approval of final result: allowed

### Supplier

- view and act only on current round invitation and quote entry,
- no historical pricing visibility.

## Files Likely Affected

### Backend

- `SupplierSystem/src/SupplierSystem.Domain/Entities/Quote.cs`
- `SupplierSystem/src/SupplierSystem.Domain/Entities/SupplierRfqInvitation.cs`
- `SupplierSystem/src/SupplierSystem.Domain/Entities/RfqPriceAuditRecord.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/QuoteVisibility.cs`
- `SupplierSystem/src/SupplierSystem.Api/Services/Rfq/RfqWorkflowStore.cs`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.RfqEndpoints.cs`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.QuoteEndpoints.cs`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.ReviewEndpoints.cs`
- EF configuration files under `SupplierSystem/src/SupplierSystem.Infrastructure/Data/Configurations`
- EF migration files

### Frontend

- `app/apps/web/src/views/RfqDetailView.vue`
- `app/apps/web/src/components/SupplierQuoteForm.vue`
- `app/apps/web/src/components/RfqPriceComparisonTable.vue`
- `app/apps/web/src/api/rfq.ts`
- `app/apps/web/src/types/index.ts`
- `app/apps/web/src/locales/zh/rfq.json`
- `app/apps/web/src/locales/en/rfq.json`
- `app/apps/web/src/locales/th/rfq.json`

## Rollback Strategy

### Code Rollback

Continue current deployment practice:

- produce rollback-safe publish backup before deployment,
- restore previous publish folder if release fails.

### Data Rollback

Schema changes must be reversible.

Recommended pattern:

- additive migration first,
- backfill second,
- feature flag default off,
- only tighten constraints after stable validation.

This allows emergency rollback to single-round behavior even if the new schema remains present.

## Recommended Delivery Phases

### Phase 1

- add round schema,
- backfill historical RFQs to round 1,
- make quote visibility round-aware,
- implement extend deadline,
- implement start next round,
- enforce final award from latest round only.

### Phase 2

- switch default price comparison to latest round only,
- add supplier detailed multi-round comparison UI,
- update print/export and audit reporting to become round-aware.

## Validation Checklist

- old RFQs automatically behave as round 1,
- extend deadline works only when current round is unopened and suppliers are still pending,
- next round can include suppliers who skipped prior rounds,
- starting next round closes the previous round immediately,
- suppliers can only act on the current round,
- default comparison shows only latest round,
- detailed comparison shows all rounds,
- historical rounds cannot be edited,
- final award cannot target a historical round quote.

## Recommended Next Step

Start with the implementation package for Phase 1:

1. entity and EF configuration changes,
2. migration and backfill,
3. round-aware store/service updates,
4. deadline extension endpoint,
5. start-next-round endpoint,
6. front-end current-round controls.
