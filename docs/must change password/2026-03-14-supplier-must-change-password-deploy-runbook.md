# Supplier Must-Change-Password Deployment and Rollback Runbook

**Purpose:** Provide an operations-focused checklist for safely releasing the supplier forced password change feature and rolling it back if needed.

**Related plan:** `Y:\ESTHER\SMP2\supplier-deploy\docs\must change password\2026-03-14-supplier-must-change-password-plan.md`

---

## Scope

- Included roles for forced password change: `temp_supplier`, `formal_supplier`, `supplier`
- Excluded role: `tracking`
- Excluded statuses from one-time SQL update: `deleted`, `frozen`
- Feature flag/state source: `users.must_change_password`

## Release Goal

- Deploy backend and frontend changes that enforce password update for flagged supplier users.
- After code validation, run a one-time SQL update so all existing suppliers are forced to change password on next login.
- Preserve the ability to quickly roll back code and stop the rollout SQL if problems appear.

---

## Preconditions

Complete all of the following before production release:

- [ ] Backend code is built and validated.
- [ ] Frontend code is built and validated.
- [ ] Login flow has been tested with at least one supplier test account.
- [ ] `/api/auth/me` returns `mustChangePassword`.
- [ ] API-side blocking for flagged supplier users has been tested.
- [ ] Auto-login or invitation entry points have been tested.
- [ ] A production publish backup procedure is ready.
- [ ] A database backup or restore point is available before executing the one-time SQL.

---

## Pre-Deployment Backup Checklist

### 1. Backup production application files

Before copying new code, create a publish backup on the production share.

**Current production path**
- `\\apeaweb01\smp2\backend\SupplierSystem\artifacts\publish\api`

**Recommended backup naming**
- `\\apeaweb01\smp2\backend\SupplierSystem\artifacts\publish\api_backup_YYYYMMDDHHMMSS`

**Notes**
- Do not overwrite an existing backup folder.
- Keep `web.config`, `appsettings*.json`, `logs`, and `uploads` protected during deployment.

### 2. Backup production database

Before running the one-time SQL, create a database backup for `SMP`.

**Minimum requirement**
- Confirm a restorable database backup exists from immediately before the rollout SQL.

**Recommended evidence to capture**
- Backup timestamp
- Operator name
- Backup file location or DBA confirmation

### 3. Capture pre-release counts

Run and save the count of affected supplier users before the rollout SQL.

```sql
SELECT COUNT(*) AS target_supplier_user_count
FROM users
WHERE LOWER(role) IN ('temp_supplier', 'formal_supplier', 'supplier')
  AND (status IS NULL OR LOWER(status) NOT IN ('deleted', 'frozen'));
```

---

## Deployment Steps

### Phase 1: Deploy application code only

1. Create `app_offline.htm` in the production publish directory to avoid partial IIS hot-reload issues.
2. Back up the current publish directory.
3. Copy the new publish output into the production publish directory.
4. Exclude the following from overwrite unless there is an explicit config change:
   - `web.config`
   - `appsettings.json`
   - `appsettings.Development.json`
   - `appsettings.Production.json`
   - `logs\`
   - `uploads\`
5. Remove `app_offline.htm` to bring the application back online.
6. Verify the site and API are reachable before running the rollout SQL.

**Minimum smoke check after code deploy**
- [ ] Main app page loads
- [ ] Normal login still works for a non-supplier/internal account
- [ ] Supplier test account can reach the new forced password flow if manually flagged
- [ ] No startup failure (`500.30`)

### Phase 2: Validate feature behavior with a test supplier

Before mass-updating existing suppliers, validate with one controlled supplier account.

**Recommended validation approach**
1. Pick a supplier test account in one of the included roles.
2. Set only that account to `must_change_password = 1`.
3. Verify:
   - login redirects to the forced password change page
   - business pages are blocked
   - business APIs are blocked
   - password change succeeds
   - access is restored immediately after change

**Suggested single-account SQL**

```sql
UPDATE users
SET must_change_password = 1
WHERE id = '<TEST_USER_ID>'
  AND LOWER(role) IN ('temp_supplier', 'formal_supplier', 'supplier');
```

### Phase 3: Execute the one-time rollout SQL

Only proceed after Phase 2 passes.

**Production rollout SQL**

```sql
UPDATE users
SET must_change_password = 1
WHERE LOWER(role) IN ('temp_supplier', 'formal_supplier', 'supplier')
  AND (status IS NULL OR LOWER(status) NOT IN ('deleted', 'frozen'));
```

**Immediately after execution**
- Record affected row count.
- Save execution timestamp.
- Save operator name.

**Post-SQL smoke checks**
- [ ] Existing supplier account is redirected to forced password change flow
- [ ] Internal user login remains unchanged
- [ ] `tracking` account remains unchanged if tested
- [ ] Supplier can complete password change and continue normally

---

## Verification Checklist

### Application health

- [ ] `https://ecichinahr.ecintl.com/smpBackend/` returns `200`
- [ ] Key backend endpoints respond normally
- [ ] No startup or runtime fatal errors appear in production logs

### Supplier enforcement

- [ ] Existing supplier login redirects to required password change page
- [ ] Supplier cannot navigate to RFQ, quote, or dashboard pages before changing password
- [ ] Supplier cannot call protected business APIs before changing password
- [ ] Supplier can still call password change endpoint
- [ ] Supplier regains access after password change

### Non-supplier regression

- [ ] Internal purchaser user login unaffected
- [ ] Internal admin user login unaffected
- [ ] `tracking` account unaffected

---

## Rollback Strategy

There are two rollback layers:

1. **Code rollback** - restore previous publish output
2. **Data rollback** - reverse the one-time `must_change_password` update if necessary

Use the least invasive rollback that resolves the issue.

### A. Code rollback only

Use this when:
- the app fails to start after deployment
- the forced password flow is broken due to code or routing
- internal users are impacted by the new enforcement logic

**Steps**
1. Create `app_offline.htm` in the production publish directory.
2. Restore the latest known-good publish backup into:
   - `\\apeaweb01\smp2\backend\SupplierSystem\artifacts\publish\api`
3. Preserve production config files unless the known-good backup specifically includes required config.
4. Remove `app_offline.htm`.
5. Re-test application startup and login.

**Evidence to record**
- Restored backup folder path
- Rollback timestamp
- Operator name

### B. Roll back the one-time supplier flag update

Use this when:
- code is healthy, but business decides to stop the rollout
- too many suppliers are unexpectedly blocked
- communications/training are not ready

**Safest option**
- Reverse only the users changed by the rollout batch, using a captured target list or timestamped export.

**If a targeted user list was captured before rollout**

```sql
-- Recommended: update only the captured rollout set.
-- Replace <captured ids> with the actual audited list.
UPDATE users
SET must_change_password = 0
WHERE id IN (<captured ids>);
```

**Fallback rollback when no captured user list exists**

```sql
UPDATE users
SET must_change_password = 0
WHERE LOWER(role) IN ('temp_supplier', 'formal_supplier', 'supplier')
  AND (status IS NULL OR LOWER(status) NOT IN ('deleted', 'frozen'));
```

**Warning**
- The fallback SQL can also clear legitimate forced-change states created for other reasons.
- Prefer a targeted rollback based on an audited user set captured during rollout.

### C. Full rollback

Use this when both code and data must be reverted.

**Order**
1. Roll back code to the last known-good publish output.
2. Confirm application stability.
3. Roll back the rollout SQL if business access must be restored immediately.

---

## Recommended Evidence to Capture During Release

- Publish backup folder path
- Deployment timestamp
- Operator name
- Code version/build identifier
- Affected row count from rollout SQL
- Smoke test accounts used
- Final status: success / partial rollback / full rollback

---

## Communication Template

### Pre-release

- Supplier forced password change feature is being deployed.
- Existing supplier users will be required to change password on next login.
- Internal users and tracking accounts are not affected.

### Post-release success

- Deployment completed successfully.
- Existing supplier users will be prompted to change password at next login.
- Support team should expect supplier questions about the required password change screen.

### Rollback notice

- Supplier forced password change rollout has been rolled back.
- Previous login behavior has been restored.
- Follow-up release timing will be communicated separately.

---

## Final Go/No-Go Checklist

- [ ] Code backup completed
- [ ] Database backup completed
- [ ] Test supplier validation passed
- [ ] Internal user regression check passed
- [ ] Auto-login bypass check passed
- [ ] Production rollout SQL reviewed
- [ ] Rollback SQL prepared
- [ ] Support/business communication prepared
