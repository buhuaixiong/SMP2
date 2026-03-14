# 性能瓶颈分析与优化建议报告

- 日期: 2026-02-19
- 项目: `E:\SMP2\supplier-deploy`
- 结论性质: 基于代码静态审查（未改代码、未做线上压测）
- 更新状态: P0 已全部落地并完成二次验证（2026-02-19）

## 1. 核心发现（2026-02-19 初次审查基线）

> 说明：本节记录的是初次审查时的“问题基线快照”。  
> 最新状态以第 5 节“P0 实施更新”和第 6 节“验证结果”为准。

### 1.1 不必要或高频网络调用
- 全局锁定状态从根组件开始 10 秒轮询，长期占用网络与后端资源。  
  证据: `app/apps/web/src/App.vue:44`，`app/apps/web/src/stores/lockdown.ts:195`
- 审批看板 30 秒轮询，并且每个周期同时拉取待处理/已处理数据。  
  证据: `app/apps/web/src/views/ApprovalDashboardView.vue:513`，`app/apps/web/src/views/ApprovalDashboardView.vue:660`
- 升级管理页 30 秒轮询升级状态。  
  证据: `app/apps/web/src/views/UpgradeManagementView.vue:949`，`app/apps/web/src/views/UpgradeManagementView.vue:1391`
- 审批页筛选/搜索会触发服务端拉取，存在“交互即请求”的放大效应。  
  证据: `app/apps/web/src/views/ApprovalDashboardView.vue:44`，`app/apps/web/src/views/ApprovalDashboardView.vue:64`，`app/apps/web/src/views/ApprovalDashboardView.vue:652`
- 供应商列表存在父子组件重复请求风险。  
  证据: `app/apps/web/src/views/UnifiedDashboard.vue:202`，`app/apps/web/src/components/SupplierDashboardPanel.vue:464`

### 1.2 后端查询存在 N+1 / 多次往返
- 升级状态列表中按应用循环查文档，符合 N+1 模式。  
  证据: `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeService.Status.cs:61`
- 待处理列表查询未见分页，数据量上升时响应会退化。  
  证据: `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeRepository.cs:112`
- 完整度批处理逐条查询并逐条 `SaveChanges`，数据库往返次数高。  
  证据: `SupplierSystem/src/SupplierSystem.Infrastructure/Services/SupplierService.Completeness.cs:19`
- Dashboard 统计由多个 `CountAsync` 组成，单请求内多次 SQL 往返。  
  证据: `SupplierSystem/src/SupplierSystem.Api/Controllers/DashboardController.Todos.cs:101`，`SupplierSystem/src/SupplierSystem.Api/Controllers/DashboardController.Stats.cs:40`

### 1.3 前端主线程计算与重渲染压力
- 报价对比组件存在多层映射/查找/排序，属于热路径重计算。  
  证据: `app/apps/web/src/components/RfqPriceComparisonTable.vue:382`
- 工作流布局 `computed` 中有多轮嵌套查找，数据规模大时会卡顿。  
  证据: `app/apps/web/src/components/RfqLineItemWorkflowLayout.vue:265`
- 有 `computed` 内调试日志，增加运行时开销。  
  证据: `app/apps/web/src/components/RfqQuoteComparison.vue:1532`
- 注册页深度监听+JSON 深拷贝保存草稿，易造成输入时掉帧。  
  证据: `app/apps/web/src/views/SupplierRegistrationView.vue:864`，`app/apps/web/src/views/SupplierRegistrationView.vue:1267`

### 1.4 资源体积与加载链路
- 当前入口 JS 体积偏大（约 504KB 未压缩），首屏解析执行压力较高。  
  证据: `SupplierSystem/src/SupplierSystem.Api/wwwroot/index.html:10`
- `wwwroot/assets` 存在大量历史产物（文件数量高），增加部署包体积和运维复杂度。  
- 构建配置未去除 `console`，生产运行仍携带调试输出。  
  证据: `app/apps/web/vite.config.ts:53`
- 后端未见显式启用响应压缩/输出缓存配置。  
  证据: `SupplierSystem/src/SupplierSystem.Api/Program.cs:211`

### 1.5 当前状态（截至 2026-02-19 二次验证）
- 已闭环（P0）: 轮询韧性治理（可见性暂停、backoff、jitter、in-flight 防重入）已覆盖锁定状态、审批看板、升级管理页。
- 已闭环（P0）: 供应商列表同参数并发请求去重已落地。
- 已闭环（P0）: 升级审批列表 N+1 已改为批量查文档并按 `ApplicationId` 分组回填。
- 已闭环（P0）: Pending 列表已由 `TOP (200)` 升级为参数化分页（`OFFSET/FETCH` + `pageSize` 上限），并补齐采购员场景“先过滤后分页”。
- 已闭环（P0）: 完整度更新已由“逐条查询+逐条提交”改为“批量预取+分批提交”。
- 待推进（P1/P2）: 主线程热点计算优化、深度监听优化、构建分包、静态资源治理、传输压缩与缓存策略。

## 2. 优化建议（按优先级）

### P0（优先执行，直接改善“反应迟钝”）
- 轮询治理: 增加页面不可见暂停、失败退避（backoff）、随机抖动（jitter）、动态间隔。
- 请求去重: 对同 key 请求做 in-flight 去重 + 短 TTL 缓存，避免父子组件并发重复拉取。
- 搜索与筛选: 服务端查询增加防抖（300-500ms）与最小输入长度；可本地处理时避免重复打接口。
- N+1 修复: 升级状态改为一次批量查询文档并按 `ApplicationId` 分组回填。
- 查询分页: 待处理列表强制分页与排序，限制单次返回量。
- 批处理改造: 完整度更新改为批量读取+批量提交，避免“每条一提交”。

### P1（提升交互流畅度）
- 将大计算前移到数据层: 建立索引 `Map`，减少 `computed` 中重复 `find/filter/sort`。
- 减少无效响应式追踪: 深度监听改字段级监听；草稿保存改节流与增量写入。
- 组件渲染优化: 大列表采用虚拟滚动；拆分大组件，稳定 props 引用。
- 用户提到 React `memo/lazy`: 在当前 Vue 项目中对应使用 `computed`/`v-memo`/`defineAsyncComponent`。

### P2（提升加载速度与稳定性）
- 构建优化: 路由级懒加载 + 手动分包（vendor/feature chunk）。
- 清理产物: 部署前清理历史 `wwwroot/assets`，仅保留当前构建引用资源。
- 生产去调试: 关闭生产 `console` 输出并移除非必要日志。
- 传输优化: 启用 Brotli/Gzip、`Cache-Control: immutable`（哈希资源）、ETag；稳定接口启用 Output Cache。

## 3. 建议的落地顺序

1. 先做 P0（请求与数据库往返）: 通常对“慢、卡、等待长”的体感改善最明显。  
2. 再做 P1（主线程与渲染）: 提升输入、切换、滚动等交互流畅性。  
3. 最后做 P2（资源与加载链路）: 优化首屏与整体吞吐。  

## 4. 验证指标（实施后建议跟踪）

- 前端: 首屏 `LCP`、交互延迟 `INP`、主线程长任务数量、单页请求数。
- 后端: 接口 P95/P99、SQL 查询次数/请求、慢查询比例、缓存命中率。
- 网络: 轮询接口 QPS、重复请求率、单用户带宽占用。

---

## 5. P0 实施更新（已完成）

### 5.1 前端请求与轮询优化（已完成）
- 锁定状态轮询治理已落地：页面隐藏时不发请求、失败退避与抖动、可见时立即恢复、in-flight 防重入。  
  代码: `app/apps/web/src/stores/lockdown.ts`
- 供应商列表请求去重已落地：同参数并发请求只发一次，减少重复网络调用。  
  代码: `app/apps/web/src/stores/supplier.ts`
- 审批看板筛选/搜索不再触发服务端重拉（保留本地过滤），并新增可见性轮询控制。  
  代码: `app/apps/web/src/views/ApprovalDashboardView.vue`
- 审批看板轮询升级为统一韧性策略：in-flight 去重、失败退避（backoff）、随机抖动（jitter）、可见性恢复即刷新。  
  代码: `app/apps/web/src/composables/useVisibilityAwarePolling.ts`，`app/apps/web/src/views/ApprovalDashboardView.vue`
- 供应商目录筛选请求增加 300ms 防抖，降低频繁筛选导致的请求风暴。  
  代码: `app/apps/web/src/views/SupplierDirectoryView.vue`
- 升级管理页轮询升级为统一韧性策略：in-flight 去重、失败退避（backoff）、随机抖动（jitter）、可见性恢复即刷新。  
  代码: `app/apps/web/src/composables/useVisibilityAwarePolling.ts`，`app/apps/web/src/views/UpgradeManagementView.vue`

### 5.2 后端 N+1 与查询上限优化（已完成）
- 升级审批列表从“逐条查文档”改为“按 applicationId 批量查询并分组回填”。  
  代码: `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeRepository.cs`  
  代码: `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeService.Status.cs`
- Pending 列表由固定 `TOP (200)` 升级为参数化分页（`OFFSET/FETCH`，并对 `pageSize` 做上限约束）。  
  代码: `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeRepository.cs`  
  代码: `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeService.Status.cs`  
  代码: `SupplierSystem/src/SupplierSystem.Api/Controllers/TempSuppliersController.cs`
- 采购员待处理分页场景补充“先按分配供应商过滤再分页”的查询路径，避免分页后再内存过滤导致漏数。  
  代码: `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeRepository.cs`  
  代码: `SupplierSystem/src/SupplierSystem.Api/Services/TempSuppliers/TempSupplierUpgradeService.Status.cs`

### 5.3 后端完整度批处理改造（已完成）
- 完整度更新由“逐供应商查询 + 逐条 `SaveChanges`”改造为“批量预取文档/白名单 + 分批提交更新与历史记录”，显著降低数据库往返。  
  代码: `SupplierSystem/src/SupplierSystem.Infrastructure/Services/SupplierService.Completeness.cs`

## 6. 验证结果（2026-02-19）

### 6.1 前端验证
- 命令: `npm run test -- --run tests/stores/lockdown.spec.ts tests/stores/supplier.spec.ts`  
  结果: 通过（4/4）
- 命令: `npm run test -- --run tests/composables/useVisibilityAwarePolling.spec.ts tests/stores/lockdown.spec.ts tests/stores/supplier.spec.ts`  
  结果: 通过（7/7）
- 命令: `npm run build`  
  结果: 通过（`vue-tsc` + `vite build`）

### 6.2 后端验证
- 命令: `dotnet build SupplierSystem.Api/SupplierSystem.Api.csproj --no-restore`  
  结果: 通过
- 命令: `dotnet test SupplierSystem.Tests/SupplierSystem.Tests.csproj --filter "FullyQualifiedName~P0PerformanceOptimizationsTests"`  
  结果: 通过（9/9）

### 6.3 测试工程阻塞修复（辅助）
- 为恢复测试可执行性，修复了测试项目的编译阻塞：  
  `SupplierSystem/tests/SupplierSystem.Tests/Security/SecurityHardeningTests.cs` 增加 `using Microsoft.Extensions.Configuration;`

## 7. 后续建议（保持原优先级）

1. 继续推进 P1：主线程重计算与重渲染优化（大组件拆分、热点 computed 索引化、虚拟列表）。  
2. 推进 P2：构建与静态资源链路优化（大 chunk 拆分、静态资源治理、传输压缩与缓存策略）。  
3. 增加运行期指标看板：请求去重命中率、轮询 QPS、接口 P95/P99，形成持续观测闭环。  

## 8. P1 运行期采样脚本（新增，2026-02-19）

为将 P1 优化从“体感改善”转为“可量化对比”，新增前端基线采样脚本：

- 脚本: `app/apps/web/scripts/collect-web-metrics.mjs`
- 命令入口: `app/apps/web/package.json` 中 `perf:baseline`
- 输出目录: `app/apps/web/artifacts/perf-baseline/*.json`

### 8.1 采样指标

- `LCP`（通过 `largest-contentful-paint` 观察）
- `INP` 近似值（通过 `event` entry 最大交互时长估算，字段 `inpApprox`）
- 主线程长任务数量与总时长（`longtask`）
- 请求数/失败请求数/4xx5xx 数
- 单轮页面墙钟耗时（`wallTimeMs`）

### 8.2 使用方式

1. 本地启动待测前端（例如 `npm run dev` 或 `npm run preview`）。
2. 在 `app/apps/web` 执行：
   - `npm run perf:baseline -- --url http://127.0.0.1:5173 --runs 7`
3. 查看输出 JSON，重点比较：
   - `summary.lcp.p75`
   - `summary.inpApprox.p75`
   - `summary.longTaskCount.p75`
   - `summary.requestCount.avg`

### 8.3 建议阈值（P1 验收参考）

- `LCP p75 <= 2500ms`
- `INP近似 p75 <= 200ms`
- `longTaskCount p75` 相比改造前下降
- `requestCount avg` 不上升（或下降）

> 备注: `inpApprox` 为实验室近似值，最终仍应以真实用户监控（RUM）INP 为准。

---

说明: 本报告现已包含“问题分析 + 优化建议 + P0 实施状态 + 验证证据”。
