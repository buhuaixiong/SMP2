# Account Consistency Middleware QA Notes

1. Authenticate as a supplier user whose `supplierId` references a supplier with mismatched `stage`.
   - Issue `POST /api/suppliers/{id}/temp-accounts` via Postman.
   - Expect HTTP 500 with payload `{ error: '账户数据不一致', userId, supplierId }`.
2. Add the route to the whitelist by setting `ACCOUNT_CONSISTENCY_WHITELIST_ROLES=finance_accountant` and restart the API.
   - Repeat the same request. Ensure it now returns 200 and creates the temp account.
3. Verify non-supplier roles are unaffected:
   - Authenticate as `admin` and call `PUT /api/suppliers/{id}`; request should bypass middleware and succeed.
4. For a valid supplier account, call `POST /api/suppliers/{id}/finalize-code` and confirm the middleware allows the request and `req.supplier` is populated (inspect server logs).
