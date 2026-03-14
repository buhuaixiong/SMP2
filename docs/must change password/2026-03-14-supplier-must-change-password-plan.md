# Supplier Must-Change-Password Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Force all existing supplier users to change their password on their next login before they can access any business functionality.

**Architecture:** Reuse the existing `users.must_change_password` flag as the single source of truth. Expose that flag in authenticated user payloads, enforce it in the SPA and on protected backend APIs, and run a one-time SQL update to mark current supplier users as requiring a password change.

**Tech Stack:** .NET 9 API, EF Core, Vue 3 + TypeScript, Vue Router, SQL Server

---

## Scope Decisions

- Included roles: `temp_supplier`, `formal_supplier`, `supplier`
- Excluded role: `tracking`
- Excluded statuses from one-time rollout SQL: `deleted`, `frozen`
- Existing field to use: `users.must_change_password`
- Do not introduce a second password-reset state model for this rollout.

## Desired Behavior

- Existing supplier users are marked with `must_change_password = 1`.
- On their next login, they are redirected to a required password change screen.
- Until password change succeeds, they cannot access RFQ, quote, dashboard, or other business pages.
- Direct API access is also blocked except for the minimum auth endpoints needed to complete the flow.
- After password change succeeds, the user continues normally without needing a separate admin action.

## Key Risks

- Frontend-only enforcement can be bypassed by direct API calls.
- Auto-login or special supplier entry points may bypass the normal login view.
- A rollout SQL with the wrong user filter could affect internal users.

## Recommended Rollout Order

1. Deploy backend and frontend enforcement code.
2. Validate the flow using a controlled supplier test account.
3. Run the one-time supplier update SQL in production.
4. Smoke test with 1-2 real supplier accounts.
5. Notify the business that all suppliers will need to change password on next login.

---

## Execution Checklist

### Task 1: Expose must-change-password in auth payloads

**Files**
- Modify: `SupplierSystem/src/SupplierSystem.Application/Models/Auth/AuthUser.cs`
- Modify: `SupplierSystem/src/SupplierSystem.Api/Controllers/AuthController.cs`
- Inspect/modify if needed: `SupplierSystem/src/SupplierSystem.Infrastructure/Services/AuthService.cs`

**Steps**
1. Add `MustChangePassword` to `AuthUser`.
2. Ensure login responses include the flag for authenticated users.
3. Ensure `GET /api/auth/me` also returns the flag.
4. Verify the value survives page refresh by using `/api/auth/me`, not only the login response.

**Acceptance**
- `POST /api/auth/login` returns `mustChangePassword`.
- `GET /api/auth/me` returns `mustChangePassword`.

### Task 2: Add a forced password change page in the web app

**Files**
- Create: `app/apps/web/src/views/ChangePasswordRequiredView.vue`
- Reuse/inspect: `app/apps/web/src/components/ChangePasswordDialog.vue`
- Modify if needed: `app/apps/web/src/api/auth.ts`

**Steps**
1. Create a dedicated page for forced password change rather than relying only on a dismissible dialog.
2. Reuse the existing change-password API call and validation rules.
3. Make the page clearly indicate that password change is required before continuing.
4. On success, refresh auth state and redirect to the original target page or supplier home.

**Acceptance**
- A supplier flagged with `mustChangePassword=true` can complete the flow entirely from this page.

### Task 3: Redirect flagged suppliers immediately after login

**Files**
- Modify: `app/apps/web/src/views/LoginView.vue`
- Inspect/modify: `app/apps/web/src/stores/auth.ts`

**Steps**
1. After successful login, check `mustChangePassword`.
2. If the authenticated user is one of the supplier roles and the flag is true, redirect to the required change-password route.
3. Preserve the intended destination so the user can continue there after changing password.

**Acceptance**
- Flagged suppliers never land on the normal destination page immediately after login.
- Internal users remain unchanged.

### Task 4: Add router-level enforcement

**Files**
- Modify: `app/apps/web/src/router/index.ts`

**Steps**
1. Register the new required password change route.
2. Add route guard logic that checks the authenticated user state.
3. If the user is a supplier with `mustChangePassword=true`, block navigation to all non-exempt routes.
4. Allow only the required password change route, logout route, and any minimal auth bootstrap route needed for page load.

**Acceptance**
- Manual navigation to arbitrary app URLs still redirects flagged suppliers to the change-password page.

### Task 5: Add backend API enforcement

**Files**
- Modify: auth/request pipeline files around authenticated API handling
- Likely inspect/modify: `SupplierSystem/src/SupplierSystem.Api/Extensions/ApplicationBootstrapExtensions.cs`
- Inspect middleware/filters used for auth enforcement

**Steps**
1. Add a backend guard that checks whether the authenticated user is a supplier and `must_change_password=true`.
2. Allow only these endpoints while flagged:
   - `POST /api/auth/change-password`
   - `GET /api/auth/me`
   - `POST /api/auth/logout`
3. Reject other authenticated business endpoints with a stable error code such as `PASSWORD_CHANGE_REQUIRED`.

**Acceptance**
- Direct API calls from a flagged supplier are blocked even if they bypass the SPA.

### Task 6: Prevent bypass through special supplier entry points

**Files**
- Inspect/modify: `SupplierSystem/src/SupplierSystem.Api/Controllers/PublicRfqController.cs`
- Inspect/modify: `SupplierSystem/src/SupplierSystem.Api/Services/PublicRfqService.cs`

**Steps**
1. Review auto-login and invitation-based login flows.
2. Ensure they also honor `must_change_password`.
3. If needed, redirect those users into the forced password change flow instead of directly into RFQ pages.

**Acceptance**
- RFQ invitation auto-login cannot bypass forced password change.

### Task 7: Add backend tests

**Files**
- Modify/create: `SupplierSystem/tests/...`

**Steps**
1. Add tests for login returning `mustChangePassword`.
2. Add tests for `/api/auth/me` returning `mustChangePassword`.
3. Add tests for API blocking when a supplier is flagged.
4. Add tests confirming `change-password` is still allowed.
5. Add tests confirming successful password change clears the block.

**Acceptance**
- Automated tests cover both the happy path and the enforcement path.

### Task 8: Add frontend tests

**Files**
- Modify/create: `app/apps/web/tests/...`

**Steps**
1. Add a login-flow test for redirecting flagged suppliers.
2. Add a router-guard test for blocking arbitrary routes.
3. Add a success-flow test for returning to the intended page after password change.
4. Add a regression test showing internal users are unaffected.

**Acceptance**
- Frontend tests prove the UX behavior without manual retesting for every change.

### Task 9: Prepare the one-time production SQL

**Files**
- Create: `SupplierSystem/sql/20260314_force_existing_suppliers_to_change_password.sql`

**SQL intent**

```sql
UPDATE users
SET must_change_password = 1
WHERE LOWER(role) IN ('temp_supplier', 'formal_supplier', 'supplier')
  AND (status IS NULL OR LOWER(status) NOT IN ('deleted', 'frozen'));
```

**Steps**
1. Save the SQL as a formal script in the repo.
2. Before executing in production, run a count query to confirm the expected row count.
3. Execute only after app code is deployed and validated.

**Acceptance**
- Existing supplier users are marked for required password change without touching internal or tracking accounts.

### Task 10: Production validation and cleanup

**Files**
- No required code file; operational verification task

**Steps**
1. Test with a supplier account that is included in the rollout.
2. Confirm login redirects to the required password change page.
3. Confirm normal pages and APIs are blocked until password change succeeds.
4. Confirm password change succeeds and the user regains access immediately.
5. If any temporary diagnostics were enabled during rollout, disable them after verification.

**Acceptance**
- Supplier forced-change flow works end-to-end in production.
- No effect on internal users or excluded tracking accounts.

---

## Implementation Notes

- Prefer a dedicated full-page forced-change UX over a modal-only UX.
- Do not rely on the dormant `ForcePasswordReset` field for this rollout.
- Keep `must_change_password` as the single enforceable switch.
- Ensure backend enforcement uses role checks that match the agreed rollout scope exactly.
- Before running the one-time SQL, capture the target row count for auditability.

## Operational Checklist

- [ ] Backend returns `mustChangePassword` from login and `/auth/me`
- [ ] Frontend has required password change route
- [ ] Login flow redirects flagged suppliers
- [ ] Router blocks flagged suppliers from other pages
- [ ] Backend blocks flagged suppliers from business APIs
- [ ] Auto-login/invitation paths cannot bypass enforcement
- [ ] Backend tests added and passing
- [ ] Frontend tests added and passing
- [ ] Production SQL script created and reviewed
- [ ] Production smoke test completed
