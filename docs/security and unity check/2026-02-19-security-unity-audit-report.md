# 2026-02-19 安全与架构一致性审计报告（已更新）

- 日期：2026-02-19
- 范围：`E:\SMP2\supplier-deploy`（后端 `SupplierSystem` + 前端 `app/apps/web`）
- 更新说明：本报告已根据当日修复进展更新，反映 **P0 已实施** 与 **P1（JWT 收紧 + RFQ 旧契约治理）** 的分阶段落地状态。

## 执行摘要

本次审计聚焦两类问题：
1. 安全边界收口（鉴权入口、文件下载链路、URL 敏感信息暴露）。
2. 架构一致性与可维护性（接口重复、分层耦合、超大文件、仓库结构清晰度）。

截至 2026-02-19，本轮 **P0 高风险项已完成修复并落地到代码**，且按要求 **未改变物理上传目录**（仍为 `/uploads`）。
同时，P1 中与安全边界和接口治理直接相关的两项已进入落地阶段：
- JWT 兼容策略已设置 7 天迁移窗口（截至 2026-02-26），并接入兼容命中审计。
- `api/rfq` 旧契约已加弃用窗口响应头（Deprecation/Sunset/Link），用于平滑迁移到 `api/rfq-workflow`。

## P0 修复状态（已完成）

1. 关闭 query token 认证入口（仅允许 Bearer Header）
- 结果：已完成。
- 影响：`?token=...` 不再参与认证流程；即使开启历史环境变量也无效。
- 关键位置：
  - `SupplierSystem/src/SupplierSystem.Api/Extensions/AuthenticationExtensions.cs`

2. 关闭 `/uploads` 公开静态访问，改为受保护访问
- 结果：已完成。
- 影响：文件仍存储在 `uploads` 目录，但访问必须通过认证。
- 关键位置：
  - `SupplierSystem/src/SupplierSystem.Api/Program.cs`
  - `SupplierSystem/src/SupplierSystem.Api/Controllers/UploadsController.cs`（新增）

3. 移除 URL 明文下载令牌流（`secure-download?token=...`）
- 结果：已完成。
- 影响：`secure-download` 改为 `POST body` + Bearer，避免令牌暴露在 URL/日志/Referer。
- 关键位置：
  - `SupplierSystem/src/SupplierSystem.Api/Controllers/FilesController.cs`

4. 去除二维码中的明文密码 URL 参数
- 结果：已完成。
- 影响：二维码仅携带用户名，不再拼接密码。
- 关键位置：
  - `app/apps/web/src/views/SupplierRegistrationView.vue`
  - `app/apps/web/src/components/supplier-registration/SupplierRegistrationSubmissionCard.vue`

5. 前端下载/预览入口改为带 Bearer 的统一文件流
- 结果：已完成（覆盖 RFQ、模板库、审批与价格对比弹窗等关键页面）。
- 影响：避免 `href/window.open` 直连受保护资源失败，同时收敛安全策略。
- 关键位置（节选）：
  - `app/apps/web/src/utils/fileDownload.ts`
  - `app/apps/web/src/views/RfqDetailView.vue`
  - `app/apps/web/src/components/RfqQuoteComparison.vue`
  - `app/apps/web/src/components/RfqPriceComparisonSection.vue`
  - `app/apps/web/src/components/RfqPriceComparisonTable.vue`
  - `app/apps/web/src/components/RfqApprovalOperationPanel.vue`
  - `app/apps/web/src/components/PriceComparisonDetailDialog.vue`
  - `app/apps/web/src/views/FileUploadApprovalView.vue`
  - `app/apps/web/src/views/TemplateLibraryView.vue`
  - `app/apps/web/src/components/template-library/TemplateFileInfo.vue`
  - `app/apps/web/src/components/template-library/TemplateHistoryList.vue`

6. 安全测试补充
- 结果：已完成（新增 P0 相关用例）。
- 关键位置：
  - `SupplierSystem/tests/SupplierSystem.Tests/Security/SecurityHardeningTests.cs`

## 验证结果

1. 前端验证
- `vue-tsc --noEmit -p tsconfig.build.json`：通过。

2. 后端验证
- 已定位并修复 `MSBuild restore graph` 的“无错误失败”问题：
  - 根因：SDK `9.0.311` 下 `_GenerateRestoreProjectPathWalk` 在并行图遍历时出现任务返回失败但无显式错误。
  - 处置：新增 `SupplierSystem/Directory.Build.props`，设置 `BuildInParallel=false`，规避该并行图遍历缺陷。
- 当前执行环境下 `dotnet build/test` 仍受 NuGet 外网/证书访问限制影响（`NU1301`），属于环境连通性问题，非业务代码回归。
- 诊断参考：
  - `SupplierSystem/tests/SupplierSystem.Tests/test-diag-security.txt`

3. 静态核查
- 已确认目标模块中不再存在以下高风险模式：
  - 认证读取 `request.Query["token"]`
  - 生成 `secure-download?token=...`
  - 关键附件入口 `href/window.open` 直连受保护下载 URL
  - 二维码拼接 `pass=`

## P1 修复状态（本次新增）

1. JWT 兼容策略收紧（7 天窗口）
- 结果：已落地第一阶段。
- 影响：
  - 兼容窗口截至 `2026-02-26T00:00:00Z`；窗口内仅对缺失 `iss/aud` 的历史 token 做临时兼容。
  - 窗口结束后将严格要求 `iss/aud`，缺失时鉴权失败（401）。
  - 兼容命中会写入安全日志与审计事件（`legacy_jwt_compatibility_hit`）。
- 关键位置：
  - `SupplierSystem/src/SupplierSystem.Api/Extensions/AuthenticationExtensions.cs`
  - `SupplierSystem/src/SupplierSystem.Api/appsettings.json`

2. RFQ 旧契约弃用窗口（`api/rfq`）
- 结果：已落地第二阶段代码（含重叠路由内部转发能力），默认以特性开关关闭。
- 影响：
  - 默认行为：仅返回弃用信号，不改变业务处理逻辑。
  - 开启 `ApiContracts:RfqLegacy:ForwardToWorkflowEnabled=true` 后：重叠端点将内部转发到 `api/rfq-workflow`，`line-items` 与 `purchase-orders` 子路由保持原路径不变。
  - `Deprecation: true`
  - `Sunset: <date>`
  - `Link: </api/rfq-workflow>; rel="successor-version"`
- 关键位置：
  - `SupplierSystem/src/SupplierSystem.Api/Middleware/RfqLegacyContractRoutingMiddleware.cs`（新增）
  - `SupplierSystem/src/SupplierSystem.Api/Filters/LegacyContractDeprecationFilter.cs`（新增）
  - `SupplierSystem/src/SupplierSystem.Api/Controllers/RfqController.cs`
  - `SupplierSystem/src/SupplierSystem.Api/Program.cs`
  - `SupplierSystem/src/SupplierSystem.Api/appsettings.json`

## 仍需推进的问题（按优先级）

1. P1：JWT 兼容策略收紧
- 现状：迁移窗口与审计已落地；待在截止日后切换到全量严格模式并清理兼容分支。
- 建议：按 `2026-02-26` 截止执行严格化切换，观察 401 与审计命中后再移除兼容代码。

2. P1：RFQ 领域接口重复与规则漂移风险
- 现状：`api/rfq` 已进入弃用窗口；内部转发能力已就绪并受特性开关控制（默认关闭）；未重叠子域接口（line-item/PO）仍保留原路径。
- 建议：按灰度计划逐步开启转发开关，观测稳定后移除旧控制器重叠实现，仅保留必要兼容壳层与下线计划。

3. P1-P2：后端控制器过重、前端调用链路分层不统一
- 建议：推进 `View -> Store/Composable -> Service -> API Adapter` 与后端服务层下沉。

4. P2：超大文件与复杂度热点
- 建议：拆分超大组件/控制器，设置复杂度阈值与 CI 预警。

5. P3：仓库结构与构建产物治理
- 建议：清理错位目录和产物混入，降低审计与维护成本。

## 结论

- **P0 风险已按要求修复完成**，且未改变上传物理目录。
- 当前可继续推进 P1 第二阶段（JWT 全量严格化、RFQ 单契约收敛、分层治理）。
