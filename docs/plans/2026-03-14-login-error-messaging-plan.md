# Login Error Messaging Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Show clear, localized login error messages in Chinese, English, and Thai for common sign-in failures such as invalid credentials, login timeout, login already in progress, locked account, frozen account, and deleted account.

**Architecture:** Keep backend authentication behavior unchanged and add a frontend error-mapping layer that converts transport/backend errors into stable i18n keys. `LoginView.vue` should display localized user-facing messages, while the mapping logic lives in a small pure helper for easy testing and future reuse.

**Tech Stack:** Vue 3, TypeScript, Pinia, Element Plus notifications, Vitest, i18n JSON locale files

---

### Task 1: Login error mapping helper

**Files:**
- Create: `app/apps/web/src/utils/authLoginError.ts`
- Test: `app/apps/web/tests/utils/authLoginError.spec.ts`

**Step 1: Write the failing test**

Create pure-function tests for these mappings:

- `401 Invalid credentials` -> `auth.loginErrors.invalidCredentials`
- `423 locked` -> `auth.loginErrors.accountLocked`
- `429 LOGIN_IN_PROGRESS` -> `auth.loginErrors.loginInProgress`
- timeout/network timeout -> `auth.loginErrors.timeout`
- `403 Account is frozen` -> `auth.loginErrors.accountFrozen`
- `403 Account has been deleted` -> `auth.loginErrors.accountDeleted`
- fallback -> `auth.loginErrors.genericFailure`

**Step 2: Run test to verify it fails**

Run:

```bash
powershell -ExecutionPolicy Bypass -File app/apps/web/scripts/run-vitest.ps1 run tests/utils/authLoginError.spec.ts
```

Expected: FAIL because the helper does not exist yet.

**Step 3: Write minimal implementation**

Create `authLoginError.ts` with a pure function such as:

```ts
export function getLoginErrorMessageKey(error: unknown): string {
  // returns auth.loginErrors.* key
}
```

Use only the minimum classification logic needed for the failing tests.

**Step 4: Run test to verify it passes**

Run:

```bash
powershell -ExecutionPolicy Bypass -File app/apps/web/scripts/run-vitest.ps1 run tests/utils/authLoginError.spec.ts
```

Expected: PASS.

**Step 5: Commit**

```bash
git add app/apps/web/src/utils/authLoginError.ts app/apps/web/tests/utils/authLoginError.spec.ts
git commit -m "feat: map login errors to localized message keys"
```

### Task 2: Locale keys for zh / en / th

**Files:**
- Modify: `app/apps/web/src/locales/zh/auth.json`
- Modify: `app/apps/web/src/locales/en/auth.json`
- Modify: `app/apps/web/src/locales/th/auth.json`
- Test: `app/apps/web/tests/locales/authLoginMessages.spec.ts`

**Step 1: Write the failing test**

Create a locale coverage test to verify all three locale files contain:

- `auth.loginErrors.invalidCredentials`
- `auth.loginErrors.timeout`
- `auth.loginErrors.loginInProgress`
- `auth.loginErrors.accountLocked`
- `auth.loginErrors.accountFrozen`
- `auth.loginErrors.accountDeleted`
- `auth.loginErrors.genericFailure`

**Step 2: Run test to verify it fails**

Run:

```bash
powershell -ExecutionPolicy Bypass -File app/apps/web/scripts/run-vitest.ps1 run tests/locales/authLoginMessages.spec.ts
```

Expected: FAIL because the keys are missing.

**Step 3: Write minimal implementation**

Add locale strings.

Suggested copy:

- zh:
  - 用户名或密码错误
  - 登录超时，请稍后重试
  - 登录请求处理中，请稍后再试
  - 账号已被锁定，请 30 分钟后再试
  - 账号已被冻结，请联系管理员
  - 账号已被停用
  - 登录失败，请稍后重试
- en:
  - Invalid username or password
  - Login timed out. Please try again.
  - Login is already being processed. Please wait a moment.
  - Your account is locked. Please try again in 30 minutes.
  - Your account is frozen. Please contact an administrator.
  - Your account has been deactivated.
  - Login failed. Please try again later.
- th:
  - ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง
  - การเข้าสู่ระบบหมดเวลา กรุณาลองใหม่อีกครั้ง
  - ระบบกำลังดำเนินการเข้าสู่ระบบอยู่ กรุณารอสักครู่
  - บัญชีของคุณถูกล็อก กรุณาลองใหม่อีกครั้งใน 30 นาที
  - บัญชีของคุณถูกระงับ กรุณาติดต่อผู้ดูแลระบบ
  - บัญชีของคุณถูกปิดใช้งานแล้ว
  - เข้าสู่ระบบไม่สำเร็จ กรุณาลองใหม่อีกครั้งภายหลัง

**Step 4: Run test to verify it passes**

Run:

```bash
powershell -ExecutionPolicy Bypass -File app/apps/web/scripts/run-vitest.ps1 run tests/locales/authLoginMessages.spec.ts
```

Expected: PASS.

**Step 5: Commit**

```bash
git add app/apps/web/src/locales/zh/auth.json app/apps/web/src/locales/en/auth.json app/apps/web/src/locales/th/auth.json app/apps/web/tests/locales/authLoginMessages.spec.ts
git commit -m "feat: localize login error messages"
```

### Task 3: Login view integration

**Files:**
- Modify: `app/apps/web/src/views/LoginView.vue`
- Reuse: `app/apps/web/src/utils/authLoginError.ts`

**Step 1: Write the failing test**

Extend `authLoginError.spec.ts` or create a thin integration test proving `LoginView` uses the mapped i18n key instead of raw backend error text.

**Step 2: Run test to verify it fails**

Run:

```bash
powershell -ExecutionPolicy Bypass -File app/apps/web/scripts/run-vitest.ps1 run tests/utils/authLoginError.spec.ts
```

Expected: FAIL because `LoginView.vue` still shows `error.message` directly.

**Step 3: Write minimal implementation**

In `LoginView.vue`, replace raw error display with:

```ts
notification.error(t(getLoginErrorMessageKey(error)))
```

Keep success handling and forced-password-change redirect logic unchanged.

**Step 4: Run test to verify it passes**

Run:

```bash
powershell -ExecutionPolicy Bypass -File app/apps/web/scripts/run-vitest.ps1 run tests/utils/authLoginError.spec.ts
```

Expected: PASS.

**Step 5: Commit**

```bash
git add app/apps/web/src/views/LoginView.vue app/apps/web/src/utils/authLoginError.ts app/apps/web/tests/utils/authLoginError.spec.ts
git commit -m "feat: show localized login error feedback"
```

### Task 4: Timeout-specific coverage

**Files:**
- Modify: `app/apps/web/src/utils/authLoginError.ts`
- Modify: `app/apps/web/tests/utils/authLoginError.spec.ts`

**Step 1: Write the failing test**

Add explicit cases for:

- `timeout`
- `ECONNABORTED`
- canceled/aborted transport wording that should surface as timeout

**Step 2: Run test to verify it fails**

Run:

```bash
powershell -ExecutionPolicy Bypass -File app/apps/web/scripts/run-vitest.ps1 run tests/utils/authLoginError.spec.ts
```

Expected: FAIL on timeout classification.

**Step 3: Write minimal implementation**

Match timeout-like errors conservatively and return `auth.loginErrors.timeout`.

**Step 4: Run test to verify it passes**

Run:

```bash
powershell -ExecutionPolicy Bypass -File app/apps/web/scripts/run-vitest.ps1 run tests/utils/authLoginError.spec.ts
```

Expected: PASS.

**Step 5: Commit**

```bash
git add app/apps/web/src/utils/authLoginError.ts app/apps/web/tests/utils/authLoginError.spec.ts
git commit -m "feat: classify login timeout errors"
```

### Task 5: Focused verification

**Files:**
- No new files; verification task

**Step 1: Run frontend tests**

```bash
powershell -ExecutionPolicy Bypass -File app/apps/web/scripts/run-vitest.ps1 run tests/utils/authLoginError.spec.ts tests/locales/authLoginMessages.spec.ts
```

Expected: All pass.

**Step 2: Run frontend typecheck**

```bash
node app/node_modules/vue-tsc/bin/vue-tsc.js --noEmit -p app/apps/web/tsconfig.build.json
```

Expected: PASS.

**Step 3: Manual verification**

Test on `https://ecichinahr.ecintl.com/smpBackend/login`:

- wrong username/password -> localized invalid credentials message
- repeated login / in-progress state -> localized “login in progress”
- timeout/network issue -> localized timeout message
- frozen/deleted/locked account -> localized specific message
- switch locale between zh/en/th and verify copy changes correctly

---

**Implementation notes**

- Keep backend login API unchanged unless a later requirement asks for stable backend error codes.
- Frontend should not expose raw backend English messages directly to end users on the login page.
- Use a message-key mapping helper instead of embedding logic in `LoginView.vue`.
- Prefer a pure utility function because it is easier to test than direct component notification behavior.
