# 组件迁移进度（服务层）

**更新时间**: 2025-11-16  
**负责人**: 前端团队

## 总览

| 模块 | 组件总数 | 已迁移 | 进行中 | 待迁移 | 说明 |
|------|---------|--------|--------|--------|------|
| RFQ | 15 | 15 | 0 | 0 | 所有 RFQ 视图/组件均切换到服务层（通知、HTTP、缓存、审计） |
| 供应商管理 | 20 | 20 | 0 | 0 | 包含目录、注册、档案、审批等场景全部完成迁移 |
| 审批流程 | 12 | 12 | 0 | 0 | 队列、仪表盘、审批表单均使用 useApprovalWorkflow/useNotification |
| 系统管理 | 18 | 18 | 0 | 0 | 权限、组织、模板、升级、锁定等页面已完成服务化改造 |
| 其他模块 | 15 | 15 | 0 | 0 | Dashboard、登录/注册、MRP/PR/SR 等辅助模块同步完成 |

**关键事实**

- 66 个 Vue 组件 + 1 个 composable 通过脚本批量迁移，完成通知、确认、消息相关服务化。
- 所有组件统一通过 `useNotification()` composable 获取 `notificationService`，配合 ESLint/CI 禁止回退到 Element Plus 原生 API。
- `node tools/scripts/migrate-to-service.js apps/web/src --write` 已执行；变更涵盖 `apps/web/src` 下所有遗留 `ElMessage/ElMessageBox/ElNotification` 调用。
- `tools/scripts/scan-notifications.js apps/web/src` 最新扫描仅剩 `apps/web/src/services/notification.ts` 合法使用；报告见 `var/migration/notification-usage.json`。
- PR 需附带扫描结果与 `docs/migration-progress.md` 更新，确保服务层迁移状态透明。

## RFQ 模块 ✅
- [x] RfqManagementView.vue
- [x] RfqDetailView.vue
- [x] RfqCreateView.vue
- [x] RfqApprovalWorkflow.vue
- [x] RfqLineItemsEditor.vue
- [x] RfqQuoteComparison.vue
- [x] RfqPriceComparisonSection.vue / Table.vue / OperationPanel.vue / SupplierInvitation.vue

## 供应商管理 ✅
- [x] SupplierDirectoryView.vue
- [x] SupplierRegistrationView.vue / SupplierRegistrationForm.vue
- [x] SupplierProfileView.vue / SupplierView.vue
- [x] SupplierChangeRequestForm.vue / SupplierChangeApprovalView.vue
- [x] SupplierDashboardPanel.vue / SupplierFileUploadsView.vue / SupplierQuoteForm.vue

## 审批流程 ✅
- [x] ApprovalDashboardView.vue / ApprovalQueueView.vue
- [x] ApprovalWorkflow.vue / RfqApprovalWorkflow.vue
- [x] RegistrationApprovalView.vue / SupplierChangeApprovalView.vue / FileUploadApprovalView.vue

## 系统管理 ✅
- [x] AdminPermissionsView.vue / AdminAuditLogView.vue / AdminBulkDocumentImportView.vue
- [x] AdminExchangeRateManagementView.vue / AdminEmergencyLockdownView.vue
- [x] OrganizationalUnitsView.vue / TagManagementView.vue / UpgradeManagementView.vue

## 其他模块 ✅
- [x] LoginView.vue / AccountActivationView.vue / RegistrationStatusView.vue
- [x] DashboardView.vue / UnifiedDashboard.vue
- [x] MaterialRequisition* / PurchasingGroupsView.vue / FinanceAccountantInvoiceView.vue
- [x] AutoLoginView.vue / SupplierReconciliationView.vue / PrConfirmationView.vue

## 自动化 & 操作指南

1. **扫描脚本**  
   - `node tools/scripts/scan-notifications.js apps/web/src`  
   - 输出：`var/migration/notification-usage.json`
2. **批处理迁移**  
   - `node tools/scripts/migrate-to-service.js <fileOrDir> --write`
3. **CI 守护**  
   - `.github/workflows/check-migration.yml` 在 PR/Push 阶段阻止新增遗留通知模式。
4. **人工校验**  
   - 回归 `apps/web/tests/services/permission.spec.ts` 与关键 E2E 场景，确保持久化逻辑/提示语一致。
5. **文档同步**  
   - 迁移完成后更新本文件、`docs/development/service-layer-implementation-plan.md` 以及 PR 说明。
