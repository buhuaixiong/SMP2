# Supplier Registration Page Redesign Plan

## 1. Background & Goals
- Align the web registration experience with `SOUP04-07(1) Vendor approval form (ECI) 2022`.
- Capture all approval-critical information with clear guidance to reduce resubmissions.
- Surface approval implications for suppliers and internal reviewers up-front.

## 2. Guiding Principles
- **Field Parity:** Mirror Excel labels and helper text, maintaining bilingual context.
- **Progressive Disclosure:** Show fields only when relevant (e.g., company type toggles).
- **Approval Awareness:** Indicate which fields drive extended approvals.
- **Auditability:** Store structured change logs for every submission and edit.
- **Accessibility:** Stepwise flow with validation, inline error copy, and download support.

## 3. Information Architecture

### 3.1 Stepper Overview
| Step | Title | Purpose | Key Data |
| --- | --- | --- | --- |
| 1 | Company Profile | Identify supplier entity | Company names, stage, company type |
| 2 | Registration & Contacts | Legal registrations and core contacts | Business registration, addresses, purchaser & finance contacts |
| 3 | Business Scope | Operational context | Product categories, delivery coverage, quality certifications |
| 4 | Finance & Payment | Billing, tax, and logistics preferences | Invoice type, payment terms/methods, ship code, billing period |
| 5 | Bank & Submission | Bank details plus stamped form workflow | Bank info, download original template, upload stamped file |

### 3.2 Section Details
- **Company Profile**
  - Fields: English/Chinese company names, supplier stage (radio from existing enums), company type (Limited, Partnership, Sole Proprietor, Other).
  - Conditional fields:
    - Limited Company: authorized capital, issued capital, directors (textarea).
    - Partnership/Sole Proprietor: owners list.
    - Other: company type other text input.
- **Registration & Contacts**
  - Legal: registered office, business address, business registration number, year established, employee count.
  - Contacts:
    - Primary contact (name, phone, email).
    - Purchaser liaison email (existing requirement).
    - Finance contact (name, phone, email) highlighted as approval-critical.
- **Business Scope**
  - Supplier classification (DM/IDM) mapping to RFQ material type.
  - Product categories (multi-select with "Other" text).
  - Quality system certifications (repeatable list).
  - Delivery locations (text or multi-select), supporting hints from Excel.
  - Optional uploads: certificates, business license (future extension but design placeholders now).
- **Finance & Payment**
  - Invoice type (China VAT options + global).
  - Payment terms (number of days, validated range).
  - Payment methods (checkbox set: Cash, Cheque/Banker Draft, Wire Transfer, Other text).
  - Billing period (free text or select).
  - Ship code dropdown with tooltip referencing A-J codes from Excel footnotes.
- **Bank & Submission**
  - Bank name, bank address, bank account number, Swift code (Category A fields).
  - Download panel:
    - Provide direct download link to the original Excel form.
    - Instructions to download, fill, stamp, and upload the stamped PDF/JPG.
  - Upload control for stamped, signed form.
  - Attestation checkbox ("I certify that the above details are true...") acknowledging submission requirements.
- Post-submission workflow: inform the supplier that a system-generated temporary tracking account will be issued immediately after submission, limited to approval-status visibility, and that once the finance accountant provides the official supplier code and approvals complete, the code with default password `666666` will replace it.

### 3.3 UX Enhancements
- Step header with progress bar and ability to save draft per step.
- Inline bilingual labels: e.g., `Name of Vendor in English / 供应商英文名称`.
- Category A fields marked with warning badge and tooltip: "Editing after registration triggers full approval."
- Review screen before submission summarizing each section, listing Category A values prominently, offering edit shortcuts.
- Submission success screen exposes the temporary tracking account credentials (with copy/email actions), shows expected approval sequence, and reminds the supplier that, after finance accounting provides the supplier code, that code plus default password `666666` will become the login for the temporary supplier portal once approvals finish.

## 4. Approval Logic Integration

### 4.1 Field Categorization
- **Category A (Full approval chain when changed post-registration)**
  - Company names (EN/CN), bank name, bank address, bank account number, Swift code,
    finance contact name/phone/email, invoice type, payment terms, payment methods.
- **Category B (Purchaser confirmation only)**
  - Sales or customer service contact data, general remarks, marketing contact.
- **Category C (Informational)**
  - Optional uploads, reference descriptions that do not influence approvals.

### 4.2 Approval Flows
| Flow | Trigger | Sequence |
| --- | --- | --- |
| FULL_CHAIN | Any Category A field change post-registration | Purchaser -> Quality -> Purchasing Manager -> Purchasing Director -> Finance Director -> Finance Accountant -> Complete |
| PURCHASER_ONLY | Only Category B fields change | Purchaser -> Complete |
| NO_APPROVAL | Category C changes only | Auto-complete, log only |

### 4.3 Change Detection Workflow
1. On update submission, API receives both baseline snapshot and new payload.
2. Backend computes `changedFields` with metadata `{ fieldKey, oldValue, newValue, category }`.
3. Highest-tier category drives `flowType`.
4. Approval task created with:
   - Required steps per flow.
   - Change log attached for reviewer context.
5. Notifications include bilingual field labels and previous/new values.

### 4.4 Temporary Account Lifecycle
- After successful submission, create a `temp_supplier_user` record (e.g., `TMP-2025-0001`) with a random initial password and link it to the pending registration.
- Temporary account IDs follow per-currency sequences (e.g., KRW `TMP-KRW-006` if `TMP-KRW-001`...`005` already exist); maintain a `temp_account_sequences` tracker to avoid collisions under concurrency.
- Restrict the temporary account to an approval-tracking portal that shows step status, approver contact hints, submitted documents, and messaging guidance; editing of registration data remains disabled.
- Display credentials on the success screen and dispatch them via email/SMS using encrypted channels (TLS 1.2+, signed templates). Store only salted hashes; never persist plaintext.
- During the Finance Accountant step, capture the accountant-provided supplier code (e.g., `KR-000123`) and validate uniqueness, currency alignment, and format before persisting:
  - Create or update the supplier user to use the provided code as username.
  - Set the default password to `666666`, store it hashed, and flag for mandatory reset on first login.
  - Transfer portal access from the temporary account to the new supplier-code account, immediately revoke the temporary password/tokens, and enqueue backend deletion of the temporary credential record.
- Send final notifications with login instructions, password-change reminder, and a link to the temporary supplier portal.
- Enforce security controls (password-change requirement, login attempt limits, audit logging) across both account stages.
- Password communication & lifecycle controls:
  - Notifications include secure deep links directing suppliers to a password-reset flow; default passwords never shown after first render. SMS payloads use masked hints plus one-time confirmation codes.
  - First-login flow forces password change via dedicated endpoint (`POST /auth/password-reset`) that checks `forcePasswordReset` flag, enforces complexity, and records completion timestamp.
  - Support UI allows procurement/finance admins to trigger single-use reset links; each link expires after 15 minutes or one use.
  - Security policy: lock account for 15 minutes after 5 failed attempts; alert procurement security contact after 3 lockouts in 24 hours.
  - Recovery process when credentials lost or suspected leaked:
    - Supplier requests reset via portal ("Forgot credentials") which verifies contact through email+SMS OTP.
    - Admin console shows audit trail, option to revoke temp/final credentials, and reissue via encrypted channel.
  - Telemetry tracks reset/lockout metrics; anomalies trigger alerts to security operations.
  - After the supplier completes first login and mandatory password change with the new supplier code, backend jobs purge any remaining temporary account artifacts and remove cached temporary passwords from secure stores.

## 5. Validation Strategy
- Build a shared validation library (implemented in `shared/validation/supplierRegistrationSchema.js`) so that field rules, conditional logic, and approval-trigger calculations remain identical everywhere.
- Step-level client validation using the shared schema before step transitions and on final submission.
- Server-side validation wraps the same schema to guarantee parity and produce structured error payloads for the UI.
- Bank account fields validated together; require numeric/format compliance.
- Multi-step guard: cannot advance to next step until current step valid.
- Draft saving uses the shared schema in "relaxed" mode to persist partial payloads while marking incomplete sections (API: `POST /suppliers/:id/drafts` + `GET` for resume).
- Localized validation and helper text anchored in i18n resources.

## 6. Data & API Changes
### 6.1 Backend & Data Model
- Extend Supplier DTO/model with new fields (`financeContact`, `productTypes`, `qualityCertifications`, `paymentMethods`, `bankInfo`, `tempAccountId`, `supplierCode`).
- Database migrations:
  - Add columns or JSON fields with sensible defaults for existing suppliers.
  - Persist baseline snapshot for change comparisons (new `supplier_baselines` table keyed by supplierId + version).
  - Introduce `temp_supplier_users` (id, supplierId, username, passwordHash, expiresAt, status) and update `supplier_users` to track `forcePasswordReset`, `initialPasswordIssuedAt`.
  - Ship backfill scripts to populate baseline rows and seed default values so legacy suppliers continue to function without spurious approval triggers.
  - Scripts implemented (`scripts/seed-baselines.js`, `scripts/sync-temp-account-flags.js`, `scripts/baseline-integrity-check.js`) with npm aliases for repeatable operations.
  - Backfill plan:
    - Script A: `seed_baselines.js` extracts authoritative supplier snapshots (company info, bank data, finance contacts) and inserts into `supplier_baselines`.
    - Script B: `sync_temp_account_flags.js` initializes `forcePasswordReset` and `tempAccountId` fields for legacy users (default to null/false).
    - Script C: `baseline_integrity_check.js` cross-compares live supplier records vs. `supplier_baselines` and logs discrepancies.
  - Validation & sampling:
    - Automated job verifies hash/checksum per baseline row and ensures mandatory fields are populated; failures raise deployment blockers.
    - Data quality dashboard monitors baseline coverage percentage (target 100%).
    - Manual sampling: 5% of suppliers per business unit (minimum 20 records) reviewed weekly in staging; 1% (minimum 50 records) spot-checked after production backfill.
    - Audit log retains backfill runs with operator, timestamp, affected range, and checksum summary for rollback traceability.
  - Maintain `temp_account_sequences` table keyed by currency to ensure gapless sequential issuance; provide administrative tooling to reconcile sequences after imports.
- API endpoints:
  - `POST /suppliers/preview-approval` accepts pending changes and returns flow prediction.
  - `POST /suppliers/:id/temp-accounts` issues the temporary tracking account and returns credentials metadata.
  - `PATCH /suppliers/:id` accepts `payload` + `draft` flag + optional stamped form upload.
  - `POST /suppliers/:id/finalize-code` (invoked by approval service) accepts the accountant-provided supplier code, validates uniqueness/format, provisions the final account, and deactivates the temporary credentials.
- Auth service updates:
  - Support login for temporary accounts with limited scopes.
  - Store default password `666666` hashed and enforce first-login reset plus password history checks.
  - Implement maximum retry counts, lockout, and audit logging for account lifecycle events.
- File storage: configure secure upload bucket/folder for stamped forms with retention policy and traceability to the account lifecycle.

### 6.2 Frontend & Auth Integration
- Registration wizard calls preview API to show expected approvals and handles temp account issuance on submission (display, copy, download credential PDF).
- Success screen fetches temporary credentials, renders QR code / copy buttons, and allows resending via email/SMS.
- Add a `TemporarySupplierPortal` route that consumes approval-status API, guards editing, and surfaces contact/help content.
- Login page updates to accept either temp account IDs or final supplier codes; enforce password reset workflow and prompt to change from the default `666666`.
- Procurement/finance console gains ability to reissue credentials, view logs, and manually trigger final code creation if needed.
- Coordinate with platform teams (auth, notification, file storage) to schedule dependencies, test end-to-end credential issuance, and verify template rendering before rollout.
- Current implementation coverage:
  - Shared schema wired into backend submission route; draft save/resume endpoints available (`POST/GET /supplier-registrations/drafts`).
  - Database schema now stores draft tokens, temp account IDs, and baseline metadata; helper scripts seeded via npm scripts `baseline:seed`, `baseline:check`, `temp-account:sync`.
  - Front-end wizard still uses legacy single-form flow; draft resume UX pending.
  - Next milestone: implement temp account issuance + approval callbacks (Step 3), expose preview/finalize APIs, then refactor front-end wizard to consume the draft + preview endpoints and surface approval flow preview.

## 7. Integration with Approval Engine
- Define workflow templates (YAML/JSON) for `FULL_CHAIN`, `PURCHASER_ONLY`, `NO_APPROVAL`.
- Approval service consumes templates to create task queues and SLAs.
- All approval state changes emit domain events (`approval.step.started`, `approval.step.completed`, `approval.finalized`) that the core service subscribes to for triggering callbacks (`preview-approval`, `temp-accounts`, `finalize-code`) and notification fan-out.
- After submission, orchestration layer invokes `POST /suppliers/:id/temp-accounts` and persists credential metadata for notifications.
- UI surfaces upcoming steps and responsible roles for both suppliers and internal users.
- On Finance Accountant completion, approval service calls `POST /suppliers/:id/finalize-code`, passes the accountant-entered supplier code for validation, and schedules credential notifications once provisioning succeeds.
- Notify suppliers at each lifecycle transition (submission, purchaser confirmation, final code issuance) with bilingual content and security reminders.
- Duplicate submission prevention via idempotency key (hash of payload without uploads).
- Monitoring, SLA & failover:
  - Define SLA per approval stage (e.g., purchaser confirmation <= 1 business day, manager/director approvals <= 2 days, finance director/accountant combined <= 3 days).
  - Real-time dashboards display queue depth, average wait time, and aging tasks per stage; red/amber thresholds trigger OpsGenie/PagerDuty alerts.
  - Stuck approval detection job checks for tasks exceeding SLA by 50% and triggers escalation emails/SMS plus Slack channel notifications.
  - Automatic fallback: if a stage exceeds SLA by 100%, system reassigns to backup approver pool or escalates to higher role with visibility to procurement leadership.
  - Event log correlation ties supplier submission ID to approval events for quick traceability; includes correlation IDs in logs and notifications.
  - Provide manual override tools in admin console to reroute, skip (with justification), or rollback to previous step; all overrides audited.

## 8. Testing Plan
- **Unit Tests**
  - Validation coverage for Category A conditional fields.
  - Change detection edge cases (mixed field categories).
  - Workflow selection logic.
  - Account lifecycle services (temporary issuance, final code promotion, credential deactivation).
- **Integration Tests**
  - API from submission through approval chain creation.
  - Upload/download cycle for stamped form (mock storage adapters).
  - Temporary account issuance and final code endpoints, including forced password-reset markers and notification payloads.
- **E2E Tests (Playwright/Vitest)**
  - Wizard navigation, bilingual rendering, draft resume.
  - Scenario: edit finance contact only -> full chain triggered.
  - Scenario: edit sales contact only -> purchaser-only triggered.
  - Scenario: download template, upload stamped file, submit successfully.
  - Scenario: log in with temporary account to view approval status, then validate automatic transition to supplier-code login after approval completion.
- **User Testing**
  - Pilot with sample suppliers to validate clarity of bank step, credential instructions, and approval messaging.

## 9. Rollout Strategy
- Training materials for procurement, quality, and finance on new flows and UI.
- Supplier-facing quick guide illustrating steps and stamped form requirement.
- Credential communication plan: templates for temporary account issuance, final supplier code with default password `666666`, and mandatory password-change reminders.
- Migrate existing supplier records to populate baseline snapshots before enabling edits.
- Stage rollout under feature flag; monitor approval queue load, completion time, and submission error rates.
- Gather feedback during pilot; iterate on tooltips and optional fields before full launch.
- Ready support/helpdesk playbooks for credential resets, lockouts, and first-login assistance.

## 10. Timeline & Status
| Window | Scope | Milestone / Acceptance Criteria | Status |
| --- | --- | --- | --- |
| Weeks 1-2 | Requirements, field dictionary, approval matrix, migration design | Cross-functional sign-off on requirements pack; data model delta reviewed; approval templates drafted and approved. | ✅ 完成 |
| Weeks 3-4 | Shared validation library, draft APIs, stepper shell UI | Shared schema implemented (`shared/validation`), backend `POST/GET /supplier-registrations/drafts` live; front-end shell pending. | 🚧 后端完成，前端待做 |
| Weeks 4-5 | Database migrations, baseline snapshot tooling, backfill rehearsal | Schema 扩展（draft/temp account/baseline 表）已合入；脚本 `baseline:seed`, `temp-account:sync`, `baseline:check` 就绪；回填执行待上线计划。 | ⏳ 脚本就绪，生产回填待执行 |
| Week 6 | Simplified end-to-end flow (no temp account) in staging | Wizard 提交 → 审批路由 → 手动账号下发；需等前端向导改造完成后验证。 | ⏳ 未开始 (依赖前端改造) |
| Week 7 | Limited pilot in production (simplified flow, feature flag) | Pilot supplier onboarding with manual credential handling; dashboards active. | ⏳ 未开始 |
| Week 8 | Temporary account service & portal dark launch | 实现 `POST /suppliers/:id/temp-accounts`、临时门户只读视图、安全评审。 | ⏳ 未开始 |
| Week 9 | Pilot enablement of account switch | 财务出码后 `finalize-code` 回调、临时凭据撤销、通知模板验收。 | ⏳ 未开始 |
| Week 10 | General availability rollout | 全量启用、监控上线、回顾安排。 | ⏳ 未开始 |

## 11. Risks & Mitigations
- **Baseline Gaps:** Legacy records missing key data cause unnecessary approvals.  
  Mitigation: Run data audit and populate defaults before go-live.
- **Approval Bottlenecks:** Longer chains may slow approvals.  
  Mitigation: SLA dashboards, escalation alerts, potential parallel approval options.
- **Supplier Drop-off:** Additional steps deter completion.  
  Mitigation: Save draft, concise instructions, highlight mandatory fields early.
- **Upload Failures:** Large stamped files or format issues.  
  Mitigation: Enforce file size/type limits, provide fallback email submission guidance.
  Contingency:
  - Offer offline submission: downloadable cover letter with unique submission ID, plus secure inbox (`supplier-onboarding@...`) and SFTP drop; back-office uploads documents on behalf of supplier and logs the action.
  - In-product retriable uploads with progress indicator, resume support, and clear error messaging (accepted formats, max size, troubleshooting link).
  - Success screen lists support hotline/WhatsApp/email and reminds suppliers they can quote the submission ID for manual assistance.
  - Support SOP: within 2 hours of upload failure ticket, specialist contacts supplier, validates documents, and records manual upload in audit trail.
- **Default Password Exposure:** Default `666666` could be intercepted if communicated insecurely.  
  Mitigation: Use secure channels (encrypted email/SMS), enforce first-login password reset, and expire credentials if unused within defined window.

## 12. Deliverables
- Updated Vue components (`SupplierRegistrationView.vue`, `SupplierRegistrationForm.vue`, wizard shell, review screen, download/upload modal).
- New frontend modules for `TemporarySupplierPortal` and credential presentation components (success screen, credential resend dialog, login update).
- Backend changes (models, migrations, approval templates, file storage handlers).
- Auth service enhancements covering temporary account issuance, supplier-code activation, password reset enforcement, and audit logging.
- Documentation: field dictionary, approval matrix, user guides, release notes.
- Automated test suites (unit, integration, e2e) with coverage reports.
- Operational playbook for support teams and procurement stakeholders.

## 13. Implementation Notes & Attention Points
- Build and test the temporary account lifecycle service before exposing UI to avoid orphaned registrations; include feature flags to toggle issuance in lower environments.
- Use queue or event-driven handlers to transition from temporary account to supplier code once the Finance Accountant step completes to decouple approval latency from API response times.
- Mask temporary credentials in logs, restrict who can view them in admin tools, and auto-expire unused temporary accounts (e.g., 30 days).
- Force suppliers to change the default password `666666` at first login and prevent reuse of the same password; communicate the requirement prominently in both UI and notifications.
- Ensure the temporary supplier portal is responsive, read-only, and instrumented with analytics to monitor where suppliers seek clarification.
- Provide dedicated support scripts for procurement/finance teams to reissue credentials safely without manual database intervention.
- Track dependencies on shared services (authentication gateway, notification templates, file storage) in the rollout checklist and secure sign-off before enabling supplier-facing features.
- Implement automated cleanup tasks: one triggered when the finance accountant issues the supplier code (revoking and deleting temporary credentials), and another after first-login password change to purge residual temporary artifacts and tokens.

