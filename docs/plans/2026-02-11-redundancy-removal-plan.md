# Redundancy Removal Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove only confirmed, behavior-preserving redundant code and stale build artifacts with explicit evidence and test gates.

**Architecture:** Prioritize high-confidence duplication where function signatures and control flow are materially identical. First centralize backend helper logic, then frontend rule/constants logic, then converge duplicated endpoint flow. Keep translation-content duplication out of code-refactor scope unless product confirms de-dup semantics.

**Tech Stack:** .NET 9 + xUnit, Vue 3 + TypeScript + Vitest, PowerShell.

---

## Confirmed True Redundancy Inventory

### 1) Backend `JsonElement` reader duplication (confirmed)

**Evidence pattern:** identical helper signature and equivalent implementation logic copied into multiple controllers.

**Confirmed locations:**
- `SupplierSystem/src/SupplierSystem.Api/Controllers/BuyerAssignmentsController.cs:232`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/CountryFreightRatesController.cs:376`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/ExchangeRatesController.cs:338`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/FreightRatesController.cs:407`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/NotificationsController.cs:339`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/SettlementsController.cs:727`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/SupplierRatingsController.cs:305`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/SystemLockdownController.cs:185`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/TariffsController.cs:119`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/WhitelistBlacklistController.cs:394`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/WhitelistBlacklistController.cs:750`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/WorkflowsController.cs:473`

**Scope note:** includes related duplicated `ReadInt` / `ReadDecimal` / `ReadBool` / `ReadIntArray` in same family.

---

### 2) Backend SMTP client construction duplication (confirmed)

**Evidence pattern:** same pickup-directory and network-client setup repeated in multiple services/controllers.

**Confirmed locations (`CreatePickupClient`)**:
- `SupplierSystem/src/SupplierSystem.Api/Controllers/EmailSettingsController.cs:330`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.Notifications.cs:247`
- `SupplierSystem/src/SupplierSystem.Api/Services/FileUploads/FileUploadReminderService.cs:304`
- `SupplierSystem/src/SupplierSystem.Api/Services/Reminders/ReminderNotifier.cs:171`
- `SupplierSystem/src/SupplierSystem.Api/Services/Registrations/SupplierRegistrationService.Email.cs:470`

---

### 3) Frontend supplier-portal user rule duplication (confirmed)

**Evidence pattern:** same `staffRoles` set + `supplier.segment.manage` permission exclusion logic repeated.

**Confirmed locations:**
- `app/apps/web/src/stores/supplier.ts:232`
- `app/apps/web/src/views/DashboardView.vue:194`
- `app/apps/web/src/views/SupplierDirectoryView.vue:1014`
- `app/apps/web/src/components/SupplierDashboardPanel.vue:208`

---

### 4) Frontend empty compliance summary constant duplication (confirmed)

**Confirmed locations:**
- `app/apps/web/src/stores/supplier.ts:54`
- `app/apps/web/src/components/SupplierDashboardPanel.vue:188`

---

### 5) Frontend document type detection duplication (confirmed)

**Evidence pattern:** repeated filename-keyword matcher and overlapping static document-type arrays.

**Confirmed locations:**
- `app/apps/web/src/components/BulkDocumentUpload.vue:370`
- `app/apps/web/src/components/DocumentUploadWidget.vue:388`
- `app/apps/web/src/views/AdminBulkDocumentImportView.vue:529`

---

### 6) Backend `import-excel` endpoint flow duplication (confirmed)

**Evidence pattern:** same file-null check, sheet/header defaulting, parser invocation, and exception mapping across endpoints.

**Confirmed locations:**
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RequisitionsController.Import.cs:10`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqController.cs:130`
- `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqWorkflowController.RfqEndpoints.cs:49`

---

### 7) Deploy artifact redundancy in `wwwroot/assets` (confirmed, non-functional)

**Evidence summary (scan result):**
- duplicate-hash groups: `9`
- duplicate files involved: `19`
- base bundles with multi-version history: `174`

**Primary folder:**
- `SupplierSystem/src/SupplierSystem.Api/wwwroot/assets`

**Entry reference:**
- current runtime entry points are pinned in `SupplierSystem/src/SupplierSystem.Api/wwwroot/index.html:10`

---

## Out of Scope (Not confirmed as code redundancy)

- Locale JSON files with identical content across languages (e.g. EN/TH `rfq.json`) are treated as translation-state artifacts, not code-level redundancy.
- Do not auto-merge or delete locale resources without product/i18n sign-off.

---

## Execution Tasks (Only for Confirmed Items)

### Task 1: Introduce unified backend JSON reader helper

**Files:**
- Create: `SupplierSystem/src/SupplierSystem.Api/Helpers/JsonElementValueReader.cs`
- Modify: all controllers listed in Inventory #1
- Test: `SupplierSystem/tests/SupplierSystem.Tests/Helpers/JsonElementValueReaderTests.cs`

**Step 1: Write failing tests**

```csharp
[Fact]
public void ReadString_ReturnsFirstNonNullAlias() { ... }
[Fact]
public void ReadInt_ReadsFromStringAndNumber() { ... }
```

**Step 2: Verify failing**

Run: `dotnet test SupplierSystem/tests/SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter JsonElementValueReaderTests`
Expected: FAIL.

**Step 3: Implement helper + migrate controllers incrementally**

Run after each batch: `dotnet build SupplierSystem/SupplierSystem.sln`
Expected: PASS.

**Step 4: Verify tests pass**

Run: `dotnet test SupplierSystem/tests/SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter JsonElementValueReaderTests`
Expected: PASS.

---

### Task 2: Introduce unified SMTP client factory

**Files:**
- Create: `SupplierSystem/src/SupplierSystem.Api/Helpers/SmtpClientFactory.cs`
- Modify: all files listed in Inventory #2
- Test: `SupplierSystem/tests/SupplierSystem.Tests/Helpers/SmtpClientFactoryTests.cs`

**Step 1: Write failing tests**

```csharp
[Fact]
public void PickupClient_UsesSpecifiedPickupDirectory() { ... }
[Fact]
public void NetworkClient_SetsSslAndOptionalCredentials() { ... }
```

**Step 2: Verify failing**

Run: `dotnet test SupplierSystem/tests/SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter SmtpClientFactoryTests`
Expected: FAIL.

**Step 3: Implement + migrate all call sites**

Run: `dotnet build SupplierSystem/SupplierSystem.sln`
Expected: PASS.

**Step 4: Verify with focused suites**

Run: `dotnet test SupplierSystem/tests/SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter "SmtpClientFactoryTests|NotificationServiceTests|SupplierRegistrationServiceTests"`
Expected: PASS.

---

### Task 3: Consolidate frontend supplier-portal rule and compliance default

**Files:**
- Create: `app/apps/web/src/constants/supplierPortal.ts`
- Modify: files listed in Inventory #3 and #4
- Test: `app/apps/web/tests/utils/supplierPortal.spec.ts`

**Step 1: Write failing tests**

```ts
it("returns false for staff roles", () => { ... });
it("returns shared empty compliance summary defaults", () => { ... });
```

**Step 2: Verify failing**

Run (from `app/`): `npm run test:web -- tests/utils/supplierPortal.spec.ts`
Expected: FAIL.

**Step 3: Implement shared module and replace duplicates**

Run (from `app/`): `npm run build`
Expected: PASS.

**Step 4: Verify tests**

Run (from `app/`): `npm run test:web -- tests/utils/supplierPortal.spec.ts`
Expected: PASS.

---

### Task 4: Consolidate frontend document type constants and detection

**Files:**
- Create: `app/apps/web/src/constants/documentTypes.ts`
- Create: `app/apps/web/src/utils/documentTypeDetector.ts`
- Modify: files listed in Inventory #5
- Test: `app/apps/web/tests/utils/documentTypeDetector.spec.ts`

**Step 1: Write failing tests**

```ts
it("detects business_license for English and Chinese name patterns", () => { ... });
```

**Step 2: Verify failing**

Run (from `app/`): `npm run test:web -- tests/utils/documentTypeDetector.spec.ts`
Expected: FAIL.

**Step 3: Implement shared detector and constants, migrate consumers**

Run (from `app/`): `npm run build`
Expected: PASS.

**Step 4: Verify tests**

Run (from `app/`): `npm run test:web -- tests/utils/documentTypeDetector.spec.ts`
Expected: PASS.

---

### Task 5: Converge duplicated `import-excel` endpoint flow

**Files:**
- Create: `SupplierSystem/src/SupplierSystem.Api/Helpers/ExcelImportEndpointHelper.cs`
- Modify: files listed in Inventory #6
- Test: `SupplierSystem/tests/SupplierSystem.Tests/Controllers/ExcelImportEndpointHelperTests.cs`

**Step 1: Write failing tests**

```csharp
[Fact]
public void Execute_WhenFileMissing_ReturnsBadRequest() { ... }
[Fact]
public void Execute_OnInvalidOperation_ReturnsBadRequestWithError() { ... }
```

**Step 2: Verify failing**

Run: `dotnet test SupplierSystem/tests/SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter ExcelImportEndpointHelperTests`
Expected: FAIL.

**Step 3: Implement helper and migrate three endpoints**

Run: `dotnet build SupplierSystem/SupplierSystem.sln`
Expected: PASS.

**Step 4: Verify existing behavior with focused tests**

Run: `dotnet test SupplierSystem/tests/SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter "Rfq|Requisition|ExcelImportEndpointHelperTests"`
Expected: PASS.

---

### Task 6: Add asset redundancy audit/cleanup scripts

**Files:**
- Create: `SupplierSystem/scripts/audit-wwwroot-assets.ps1`
- Create: `SupplierSystem/scripts/clean-wwwroot-assets.ps1`
- Modify: `docs/DEPLOYMENT.md`

**Step 1: Implement audit script (hash duplicates + version fanout per bundle base name)**

Run: `powershell -ExecutionPolicy Bypass -File SupplierSystem/scripts/audit-wwwroot-assets.ps1`
Expected: reports duplicate groups and multi-version groups.

**Step 2: Implement safe cleanup script**

Run (dry run): `powershell -ExecutionPolicy Bypass -File SupplierSystem/scripts/clean-wwwroot-assets.ps1 -WhatIf`
Expected: only stale unreferenced candidates listed.

**Step 3: Document deployment usage**

Update `docs/DEPLOYMENT.md` with audit/cleanup/rollback steps.

---

## Final Verification Gate

Run in order:

1. `dotnet build SupplierSystem/SupplierSystem.sln`
2. `dotnet test SupplierSystem/tests/SupplierSystem.Tests/SupplierSystem.Tests.csproj`
3. `cd app && npm run build`
4. `cd app && npm run test:web`

Expected: all pass; all confirmed duplicate logic families replaced by canonical shared modules.

