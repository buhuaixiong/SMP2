# 服务层架构优化实施计划

> **版本**: 1.0
> **日期**: 2025-11-16
> **作者**: 开发团队
> **状态**: 计划阶段

---

## 目录

- [1. 概述](#1-概述)
- [2. 阶段 0：准备阶段](#2-阶段-0准备阶段)
- [3. 阶段 1：服务层基础设施](#3-阶段-1服务层基础设施)
- [4. 阶段 2-4：渐进式迁移路线图](#4-阶段-2-4渐进式迁移路线图)
- [5. 指标采集与验证方案](#5-指标采集与验证方案)
- [6. 性能测试与监控方案](#6-性能测试与监控方案)
- [7. 风险管理](#7-风险管理)
- [8. 质量保证](#8-质量保证)
- [9. 附录](#9-附录)

---

## 1. 概述

### 1.1 背景

当前系统存在以下问题：
- API 调用分散在 80+ 个组件中
- 错误处理和通知逻辑重复（214+ 处）
- 缺乏统一的权限检查机制
- 没有统一的缓存策略

### 1.2 目标

借鉴 Odoo 的服务层架构，建立：
- 统一的服务注册和依赖注入系统
- 核心业务服务（通知、HTTP、权限、缓存、审计）
- 完整的开发规范和测试框架

### 1.3 预期收益

| 收益指标 | 目标 | 验证方式 |
|---------|------|---------|
| 代码复用性 | 提升 40% | 重复代码扫描 + 服务调用统计 |
| 开发效率 | 提升 25-30% | PR 数据分析 + 任务时间对比 |
| Bug 修复时间 | 减少 30% | Issue 平均关闭时间统计 |
| 测试覆盖率 | 提升 20% | Vitest 覆盖率报告 |

**指标采集工具**: 详见 [5. 指标采集与验证方案](#5-指标采集与验证方案)

### 1.4 时间线

```
阶段 0：准备阶段           - 1 周（5 个工作日）
阶段 1：服务层基础设施      - 2-3 周（10-15 个工作日）
阶段 2：核心 Composables   - 2 周（10 个工作日）
阶段 3：试点应用与验证      - 1-2 周（5-10 个工作日）
阶段 4：渐进式迁移         - 4-6 周（20-30 个工作日）
总计：                    - 10-14 周（50-70 个工作日）
```

**注**: 阶段 2-4 详见 [4. 阶段 2-4：渐进式迁移路线图](#4-阶段-2-4渐进式迁移路线图)

### 1.5 实施进展快照（2025-11-16）

| 阶段/任务 | 状态 | 说明 |
|-----------|------|------|
| 阶段 0（任务 0.1-0.5） | ✅ 已完成 | 架构设计、规范文档、测试工具、迁移脚本与指标基线全部交付（见 docs/architecture 与 tools/*） |
| 阶段 1（任务 1.1-1.5） | ✅ 已完成 | Registry、ServiceManager、useService 及通知/HTTP/权限/缓存/审计等核心服务均已实现并通过单测（见 `apps/web/src/core`、`apps/web/src/services`、`apps/web/tests`） |
| 任务 1.6（服务文档） | ✅ 已完成 | 服务指南、API 参考与教程已在 `docs/development`、`docs/api`、`docs/tutorials` 中发布 |
| 阶段 2（核心 Composables） | ✅ 已完成 | `apps/web/src/composables` 与 `apps/web/tests/composables` 提供 useNotification/useApprovalWorkflow/usePermission 等高复用逻辑 |
| 阶段 3（试点验证） | ✅ 已完成 | RFQ 管理/详情/创建/审批组件已迁移到服务层（见 `apps/web/src/views/Rfq*.vue`、`apps/web/src/components/RfqApprovalWorkflow.vue`） |
| 阶段 4（全面迁移） | ⏳ 进行中 | 正在更新 `docs/migration-progress.md` 并批量替换遗留模式，剩余模块排期中 |

---

## 2. 阶段 0：准备阶段

**时间**: 1 周（5 个工作日）
**目标**: 建立基础设施、规范和工具

### 2.1 任务清单

#### 任务 0.1：技术调研与设计（2天）

**负责人**: 技术负责人
**优先级**: P0

> **进度**: ✅ 2025-11-16 — 已完成 Odoo 注册表调研并在 `docs/architecture/service-layer-design.md`、`scripts/setup/create-service-structure.mjs` 中沉淀产物

**详细步骤**:

1. **研究 Odoo 注册表实现** [4小时]
   - [ ] 阅读 Odoo registry 源码
   - [ ] 理解事件系统机制
   - [ ] 确定依赖注入实现方式

2. **设计目录结构** [2小时]
   ```
   apps/web/src/
   ├── core/                      # 新增：核心框架
   │   ├── registry/              # 注册表系统
   │   │   ├── index.ts
   │   │   ├── Registry.ts
   │   │   └── types.ts
   │   ├── services/              # 服务层
   │   │   ├── index.ts
   │   │   ├── ServiceManager.ts
   │   │   └── types.ts
   │   └── hooks/                 # 核心 hooks
   │       ├── index.ts
   │       └── useService.ts
   ├── services/                  # 业务服务
   │   ├── notification.ts
   │   ├── http.ts
   │   ├── permission.ts
   │   ├── cache.ts
   │   ├── audit.ts
   │   └── index.ts
   └── directives/                # 指令
       └── permission.ts
   ```

3. **确定技术细节** [2小时]
   - [ ] TypeScript 类型设计
   - [ ] 与 Pinia 的集成方式
   - [ ] 测试策略确定

**可交付物**:
- `docs/architecture/service-layer-design.md`
- 目录结构创建脚本

**潜在风险**:

| 风险 | 等级 | 缓解措施 |
|------|------|---------|
| 目录结构与现有代码冲突 | 低 | 创建新的 `core/` 目录，不影响现有结构 |
| TypeScript 复杂度过高 | 中 | 使用简单的类型定义，逐步增强 |
| 团队不熟悉新概念 | 中 | 提供详细文档和示例，组织培训 |

---

#### 任务 0.2：建立开发规范（1天）

**负责人**: 技术负责人
**优先级**: P0

> **进度**: ✅ 2025-11-16 — `docs/development/services-guide.md`、`composables-guide.md`、`code-review-checklist.md` 已完成

**详细步骤**:

1. **编写服务开发规范** [3小时]
   - [ ] 服务定义规范
   - [ ] 依赖声明规范
   - [ ] 命名规范
   - [ ] 示例代码

2. **编写 Composables 开发规范** [2小时]
   - [ ] 命名规范
   - [ ] 文件组织
   - [ ] 最佳实践

3. **建立 Code Review 清单** [1小时]
   - [ ] 服务 Review 要点
   - [ ] Composable Review 要点
   - [ ] 测试要求

**可交付物**:
- `docs/development/services-guide.md`
- `docs/development/composables-guide.md`
- `docs/development/code-review-checklist.md`

---

#### 任务 0.3：建立测试框架（2天）

**负责人**: QA 负责人
**优先级**: P0

> **进度**: ✅ 2025-11-16 — 已交付 `apps/web/tests/setup/mockServices.ts`、`tests/utils/testHelpers.ts` 与模板用例

**详细步骤**:

1. **配置服务测试环境** [4小时]
   - [ ] 创建 mock 服务工具
   - [ ] 配置测试环境
   - [ ] 集成 vitest

2. **编写测试工具函数** [2小时]
   ```typescript
   // tests/utils/testHelpers.ts
   export function setupTestServices(services: Record<string, any>)
   export function mountWithServices(component, services)
   ```

3. **创建测试模板** [2小时]
   - [ ] 服务测试模板
   - [ ] 组件测试模板（含服务）
   - [ ] 集成测试模板

**可交付物**:
- `tests/setup/mockServices.ts`
- `tests/utils/testHelpers.ts`
- `tests/templates/service.test.ts`
- `tests/templates/component-with-service.test.ts`

**质量标准**:
- 所有新服务必须有单元测试
- 测试覆盖率不低于 80%
- 所有 mock 工具有使用文档

---

#### 任务 0.4：准备迁移工具（1天）

**负责人**: 开发工程师
**优先级**: P1

> **进度**: ✅ 2025-11-16 — `tools/scripts/scan-notifications.js`、`scan-approvals.js`、`migrate-to-service.js` 与 `docs/migration-progress.md` 已上线

**详细步骤**:

1. **编写代码扫描脚本** [3小时]
   ```javascript
   // tools/scripts/scan-notifications.js
   // 功能：
   // - 扫描所有使用 ElNotification 的地方
   // - 生成迁移清单
   // - 统计使用频率
   ```

2. **创建迁移辅助工具** [3小时]
   ```javascript
   // tools/scripts/migrate-to-service.js
   // 功能：
   // - 自动替换简单的调用模式
   // - 生成迁移报告
   ```

3. **建立迁移跟踪** [2小时]
   - [ ] 创建迁移进度表
   - [ ] 设置 GitHub Issues 模板

**可交付物**:
- `tools/scripts/scan-notifications.js`
- `tools/scripts/scan-approvals.js`
- `tools/scripts/migrate-to-service.js`
- `docs/migration-progress.md`

---

#### 任务 0.5：建立指标采集基线（0.5天）

**负责人**: QA 工程师
**优先级**: P0

> **进度**: ✅ 2025-11-16 — 已通过 `tools/metrics/collect-baseline.js` 生成 `var/metrics/baseline-2025-11-16.json` 与 `docs/metrics/baseline-report.md`

**目标**: 采集优化前的基线数据，用于后续效果验证

**详细步骤**:

1. **安装指标采集工具** [1小时]
   ```bash
   npm install --save-dev jscpd @octokit/rest
   ```

2. **执行基线数据采集** [2小时]
   - [ ] 重复代码扫描（jscpd）
   - [ ] 统计通知调用次数（grep）
   - [ ] 统计审批逻辑重复（grep）
   - [ ] 当前测试覆盖率（vitest）
   - [ ] PR 数据统计（GitHub API）

3. **生成基线报告** [1小时]
   - [ ] 汇总所有指标
   - [ ] 创建基线报告文档
   - [ ] 设置目标值

**采集命令**:
```bash
# 重复代码率
npx jscpd apps/web/src --format typescript --min-lines 5

# 通知调用统计
grep -r "ElNotification\|ElMessage" apps/web/src --include="*.vue" --include="*.ts" | wc -l

# 测试覆盖率
cd apps/web && npm run test:coverage

# PR 数据（需要 GITHUB_TOKEN）
node tools/metrics/collect-baseline.js
```

**可交付物**:
- `var/metrics/baseline-YYYY-MM-DD.json`
- `docs/metrics/baseline-report.md`

**基线指标清单**:

| 类别 | 指标 | 采集方式 |
|------|------|---------|
| 代码复用性 | 重复代码行数 | jscpd 扫描 |
| | 通知调用次数 | grep 统计 |
| | 审批逻辑重复次数 | grep 统计 |
| 开发效率 | 最近 30 天 PR 数量 | GitHub API |
| | 平均 PR 审查时间 | GitHub API |
| | 平均代码变更行数 | GitHub API |
| Bug 修复 | 最近 30 天 Bug 数量 | GitHub Issues |
| | 平均修复时间 | GitHub Issues |
| 测试 | 单元测试覆盖率 | Vitest 报告 |

---

### 2.2 阶段 0 验收标准

- [ ] 所有目录结构已创建
- [ ] 开发规范文档已完成并评审
- [ ] 测试框架已配置并通过验证
- [ ] 迁移工具已完成并测试
- [ ] **基线数据已采集并生成报告**
- [ ] 团队成员已接受培训（1小时技术分享）

---

## 3. 阶段 1：服务层基础设施

**时间**: 2-3 周（10-15 个工作日）
**目标**: 实现完整的服务层框架和核心服务

### 3.1 任务清单

#### 任务 1.1：实现注册表系统（3天）

**负责人**: 核心开发工程师
**优先级**: P0

> **进度**: ✅ 2025-11-16 — `apps/web/src/services/index.ts:1` 完成统一注册，`apps/web/src/main.ts:1` 安装 ServiceManager 并在启动前完成 `serviceManager.startAll()`

> **进度**: ✅ 2025-11-16 — ServiceManager 已在 `apps/web/src/core/services/ServiceManager.ts:1` 和 `apps/web/src/core/services/index.ts:1` 完成，`apps/web/tests/core/serviceManager.spec.ts:1` 验证启动、依赖、循环检测与降级行为

> **进度**: ✅ 2025-11-16 — Registry/RegistryManager 已在 `apps/web/src/core/registry/Registry.ts:1` 与 `apps/web/src/core/registry/index.ts:1` 实现，并通过 `apps/web/tests/core/registry.spec.ts:1` 覆盖增删、排序、事件等场景

**实现文件**:
- `apps/web/src/core/registry/Registry.ts`
- `apps/web/src/core/registry/types.ts`
- `apps/web/src/core/registry/index.ts`

**核心功能**:

1. **Registry 类实现** [1.5天]
   ```typescript
   class Registry<T> {
     add(key: string, value: T, options?: AddOptions): this
     get(key: string, defaultValue?: T): T | undefined
     has(key: string): boolean
     remove(key: string): boolean
     getAll(): T[]
     keys(): string[]
     clear(): void
     on(listener: RegistryListener): () => void
   }
   ```

   **关键特性**:
   - 有序存储（支持 sequence）
   - 事件通知机制
   - 链式调用
   - 类型安全

2. **RegistryManager 实现** [0.5天]
   ```typescript
   class RegistryManager {
     category<T>(name: string): Registry<T>
     hasCategory(name: string): boolean
     removeCategory(name: string): boolean
   }
   ```

3. **单元测试** [1天]
   - [ ] 基本添加/获取测试
   - [ ] 序列排序测试
   - [ ] 事件系统测试
   - [ ] 错误处理测试
   - [ ] 边界条件测试

**可交付物**:
- 注册表实现代码
- 完整的单元测试（覆盖率 ≥ 90%）
- API 文档

**验收标准**:
- [ ] 所有单元测试通过
- [ ] 代码通过 Code Review
- [ ] 性能测试通过（10000 项 < 100ms）

---

#### 任务 1.2：实现服务管理器（4天）

**负责人**: 核心开发工程师
**优先级**: P0

**实现文件**:
- `apps/web/src/core/services/ServiceManager.ts`
- `apps/web/src/core/services/types.ts`
- `apps/web/src/core/services/index.ts`

**核心功能**:

1. **ServiceManager 类实现** [2天]
   ```typescript
   class ServiceManager {
     async startAll(): Promise<void>
     async start(name: string): Promise<any>
     get(name: string): any
     async stopAll(): Promise<void>
   }
   ```

   **关键特性**:
   - 依赖注入
   - 循环依赖检测
   - 异步启动支持
   - 错误处理和降级
   - 生命周期管理

2. **Vue 集成** [0.5天]
   ```typescript
   function installServiceManager(app: App, config: Record<string, any>)
   function useServiceManager(): ServiceManager
   ```

3. **单元测试** [1.5天]
   - [ ] 服务启动测试
   - [ ] 依赖注入测试
   - [ ] 循环依赖检测测试
   - [ ] 错误处理测试
   - [ ] 并发启动测试

**可交付物**:
- 服务管理器实现代码
- 完整的单元测试（覆盖率 ≥ 85%）
- 集成测试
- API 文档

**验收标准**:
- [ ] 所有单元测试通过
- [ ] 能正确检测循环依赖
- [ ] 错误不会阻塞应用启动
- [ ] 代码通过 Code Review

**潜在风险**:

| 风险 | 等级 | 缓解措施 |
|------|------|---------|
| 循环依赖检测不完善 | 高 | 完善检测算法，添加详细错误信息 |
| 服务启动失败导致应用崩溃 | 高 | 实现降级机制，非关键服务失败不影响启动 |
| 异步启动顺序问题 | 中 | 使用 Promise 链确保顺序，添加超时机制 |

---

#### 任务 1.3：实现 useService Hook（2天）

**负责人**: 前端开发工程师
**优先级**: P0

> **进度**: ✅ 2025-11-16 — `apps/web/src/core/hooks/useService.ts:1` / `apps/web/src/core/hooks/index.ts:1` 提供 useService / useServices，并由 `apps/web/tests/core/useService.spec.ts:1` 验证多服务与错误处理

**实现文件**:
- `apps/web/src/core/hooks/useService.ts`
- `apps/web/src/core/hooks/index.ts`

**核心功能**:

1. **useService 实现** [0.5天]
   ```typescript
   function useService<T>(serviceName: string): T
   function useServices<T extends readonly string[]>(
     serviceNames: T
   ): Record<T[number], any>
   ```

2. **错误处理** [0.5天]
   - 清晰的错误信息
   - 开发模式下的调试提示

3. **单元测试** [1天]
   - [ ] 基本获取测试
   - [ ] 多服务获取测试
   - [ ] 错误处理测试
   - [ ] 组件集成测试

**可交付物**:
- useService hook 实现
- 单元测试和集成测试
- 使用文档

---

#### 任务 1.4：实现核心服务（5天）

**负责人**: 多名开发工程师（并行开发）
**优先级**: P0

> **进度**: ✅ 2025-11-16 — 通知/HTTP/权限/缓存/审计服务已在 `apps/web/src/services/*.ts:1`、`apps/web/src/directives/permission.ts:1` 实现，并通过 `apps/web/tests/services/*.spec.ts:1` 单测

##### 1.4.1 通知服务（1天）

**负责人**: 工程师 A
**文件**: `apps/web/src/services/notification.ts`

**功能清单**:
- [ ] success() - 成功通知
- [ ] warning() - 警告通知
- [ ] info() - 信息通知
- [ ] error() - 错误通知（支持 sticky）
- [ ] notify() - 自定义通知
- [ ] message() - 简短消息
- [ ] confirm() - 确认对话框

**验收标准**:
- [ ] 所有方法实现并测试
- [ ] 单元测试覆盖率 ≥ 80%
- [ ] 与 Element Plus 正确集成

---

##### 1.4.2 HTTP 服务（1.5天）

**负责人**: 工程师 B
**文件**: `apps/web/src/services/http.ts`

**功能清单**:
- [ ] get() - GET 请求
- [ ] post() - POST 请求
- [ ] put() - PUT 请求
- [ ] delete() - DELETE 请求
- [ ] 请求拦截器（添加 token）
- [ ] 响应拦截器（统一错误处理）
- [ ] 静默模式支持

**验收标准**:
- [ ] 所有方法实现并测试
- [ ] 错误处理正确
- [ ] 与通知服务集成
- [ ] 401 自动跳转登录

---

##### 1.4.3 权限服务（1天）

**负责人**: 工程师 C
**文件**:
- `apps/web/src/services/permission.ts`
- `apps/web/src/directives/permission.ts`

**功能清单**:
- [ ] has() - 检查权限
- [ ] hasRole() - 检查角色
- [ ] hasAny() - 检查任一权限
- [ ] hasAll() - 检查所有权限
- [ ] getRole() - 获取当前角色
- [ ] getPermissions() - 获取所有权限
- [ ] refresh() - 刷新权限
- [ ] v-permission 指令
- [ ] v-role 指令

**验收标准**:
- [ ] 所有方法实现并测试
- [ ] 指令正常工作
- [ ] 权限数据正确缓存

---

##### 1.4.4 缓存服务（1天）

**负责人**: 工程师 D
**文件**: `apps/web/src/services/cache.ts`

**功能清单**:
- [ ] set() - 设置缓存
- [ ] get() - 获取缓存
- [ ] has() - 检查缓存
- [ ] delete() - 删除缓存
- [ ] clear() - 清空缓存
- [ ] getOrSet() - 获取或设置
- [ ] cleanup() - 清理过期缓存
- [ ] TTL 支持
- [ ] 自动清理机制

**验收标准**:
- [ ] 所有方法实现并测试
- [ ] TTL 正确工作
- [ ] 内存泄漏测试通过

---

##### 1.4.5 审计日志服务（1.5天）

**负责人**: 工程师 E
**文件**: `apps/web/src/services/audit.ts`

**功能清单**:
- [ ] log() - 记录日志
- [ ] logView() - 记录查看
- [ ] logCreate() - 记录创建
- [ ] logUpdate() - 记录更新
- [ ] logDelete() - 记录删除
- [ ] flush() - 手动刷新
- [ ] 日志缓冲机制
- [ ] 定期发送
- [ ] beforeunload 发送

**验收标准**:
- [ ] 所有方法实现并测试
- [ ] 缓冲机制正常工作
- [ ] 日志正确发送到服务器

---

#### 任务 1.5：服务注册和初始化（1天）

**负责人**: 核心开发工程师
**优先级**: P0

**实现文件**:
- `apps/web/src/main.ts`（修改）
- `apps/web/src/services/index.ts`

**详细步骤**:

1. **创建服务注册函数** [2小时]
   ```typescript
   function registerServices() {
     const serviceRegistry = registry.category('services')

     serviceRegistry.add('notification', notificationService, { sequence: 10 })
     serviceRegistry.add('http', httpService, { sequence: 20 })
     serviceRegistry.add('permission', permissionService, { sequence: 30 })
     serviceRegistry.add('cache', cacheService, { sequence: 40 })
     serviceRegistry.add('audit', auditService, { sequence: 50 })
   }
   ```

2. **修改应用启动流程** [3小时]
   ```typescript
   async function bootstrap() {
     const app = createApp(App)

     // ... 安装其他插件

     // 注册服务
     registerServices()

     // 启动服务
     const serviceManager = installServiceManager(app)
     await serviceManager.startAll()

     // 挂载应用
     app.mount('#app')
   }
   ```

3. **注册全局指令** [1小时]
   ```typescript
   app.directive('permission', permissionDirective)
   app.directive('role', roleDirective)
   ```

4. **集成测试** [2小时]
   - [ ] 应用正常启动
   - [ ] 所有服务正确初始化
   - [ ] 服务可以在组件中使用
   - [ ] 错误处理正常

**验收标准**:
- [ ] 应用启动无错误
- [ ] 控制台输出服务启动日志
- [ ] 所有服务可用
- [ ] 指令正常工作

---

#### 任务 1.6：编写服务开发文档（2天）

**负责人**: 技术文档工程师
**优先级**: P1

> **进度**: ✅ 2025-11-16 — 服务指南、API 参考（`docs/api/*`）及教程 `docs/tutorials/creating-services.md` 已完成

**文档清单**:

1. **服务开发指南** [1天]
   - [ ] 概述和核心概念
   - [ ] 创建服务的步骤
   - [ ] 最佳实践
   - [ ] 内置服务说明
   - [ ] 测试指南
   - [ ] 常见问题

2. **API 参考文档** [0.5天]
   - [ ] Registry API
   - [ ] ServiceManager API
   - [ ] useService API
   - [ ] 所有服务 API

3. **示例和教程** [0.5天]
   - [ ] Hello World 服务
   - [ ] 带依赖的服务
   - [ ] 组件中使用服务
   - [ ] 测试服务

**可交付物**:
- `docs/development/services-guide.md`
- `docs/api/registry.md`
- `docs/api/service-manager.md`
- `docs/api/services/` (各服务文档)
- `docs/tutorials/creating-services.md`

---

### 3.2 阶段 1 验收标准

**代码质量**:
- [ ] 所有代码通过 ESLint 检查
- [ ] 所有代码通过 TypeScript 类型检查
- [ ] Code Review 完成并通过
- [ ] 单元测试覆盖率 ≥ 85%

**功能完整性**:
- [ ] 注册表系统正常工作
- [ ] 服务管理器正常工作
- [ ] 5 个核心服务全部实现
- [ ] 服务可以在组件中使用
- [ ] 依赖注入正常工作

**文档完整性**:
- [ ] 开发指南完成
- [ ] API 文档完成
- [ ] 示例代码完成
- [ ] 文档经过评审

**性能要求**:
- [ ] 应用启动时间增加 < 100ms
- [ ] 服务调用开销 < 1ms
- [ ] 内存占用增加 < 5MB

---

## 4. 阶段 2-4：渐进式迁移路线图

**时间**: 7-10 周（35-50 个工作日）
**目标**: 分阶段、分批次迁移现有组件到新服务层

### 4.1 整体策略

**核心原则**:
- ✅ **渐进式迁移**: 新旧模式并存，逐步过渡
- ✅ **按域划分**: 按功能模块分批迁移
- ✅ **风险可控**: 每批次迁移后充分验证
- ✅ **可回滚**: 保持回退能力

**迁移优先级**:

| 优先级 | 模块 | 组件数 | 预计时间 | 原因 |
|--------|------|-------|---------|------|
| P0 | RFQ 模块 | 15 | 2 周 | 高频使用，收益最大 |
| P1 | 供应商管理 | 20 | 2.5 周 | 核心业务，重复逻辑多 |
| P2 | 审批流程 | 12 | 1.5 周 | 逻辑统一，易于迁移 |
| P3 | 系统管理 | 18 | 2 周 | 使用频率较低 |
| P4 | 其他模块 | 15 | 2 周 | 长尾模块 |

### 4.2 阶段 2：核心 Composables（2周）

**时间**: 2 周（10 个工作日）
**目标**: 实现高复用的 Composables

#### 任务 2.1：useNotification（1天）

**负责人**: 前端开发工程师
**优先级**: P0

> **进度**: ✅ 2025-11-16 — `apps/web/src/composables/useNotification.ts` 与 `apps/web/tests/composables/useNotification.spec.ts` 已完成，RFQ 相关视图正在使用该 Composable

**功能**:
```typescript
// apps/web/src/composables/useNotification.ts
export function useNotification() {
  const notification = useService<NotificationService>('notification')

  return {
    success: notification.success,
    error: notification.error,
    warning: notification.warning,
    info: notification.info,
    confirm: notification.confirm
  }
}
```

**可交付物**:
- `apps/web/src/composables/useNotification.ts`
- 单元测试
- 使用文档

---

#### 任务 2.2：useApprovalWorkflow（2天）

**负责人**: 前端开发工程师
**优先级**: P0

> **进度**: ✅ 2025-11-16 — `apps/web/src/composables/useApprovalWorkflow.ts` 及对应测试已交付，并在 `apps/web/src/components/RfqApprovalWorkflow.vue` 中投入使用

**功能**:
```typescript
// apps/web/src/composables/useApprovalWorkflow.ts
export function useApprovalWorkflow(entityType: string) {
  const http = useService<HttpService>('http')
  const notification = useNotification()
  const audit = useService<AuditService>('audit')

  const approve = async (id: number, comment?: string) => {
    try {
      await http.post(`/api/${entityType}/${id}/approve`, { comment })
      audit.logUpdate(entityType, id, { action: 'approve' })
      notification.success('审批通过')
    } catch (error) {
      notification.error('审批失败')
      throw error
    }
  }

  // reject, requestChanges...

  return { approve, reject, requestChanges }
}
```

**可交付物**:
- `apps/web/src/composables/useApprovalWorkflow.ts`
- 单元测试
- 集成测试
- 使用文档

---

#### 任务 2.3：其他 Composables（5天）

**实现列表**:

1. **useFormValidation** [1.5天]
   - 统一表单验证逻辑
   - 支持自定义规则
   - 错误信息国际化

2. **usePermission** [1天]
   - 权限检查 composable
   - 与 v-permission 指令配合

3. **useFileUpload** [1.5天]
   - 文件上传逻辑
   - 进度追踪
   - 错误处理

4. **useTableActions** [1天]
   - 表格操作统一封装
   - 批量操作支持

> **进度**: ✅ 2025-11-16 — `apps/web/src/composables/useFormValidation.ts`、`usePermission.ts`、`useFileUpload.ts`、`useTableActions.ts` 及其测试（位于 `apps/web/tests/composables/`）已完成并在 RFQ 场景中使用

**验收标准**:
- [ ] 所有 composables 实现完成
- [ ] 单元测试覆盖率 ≥ 85%
- [ ] 使用文档完整
- [ ] 至少有 2 个组件试用

---

### 4.3 阶段 3：试点应用与验证（1-2周）

**时间**: 1-2 周（5-10 个工作日）
**目标**: 选择 2-3 个模块试点，验证架构可行性

> **进度**: ✅ 2025-11-16 — RFQ Management/Detail/Create 视图与 `RfqApprovalWorkflow` 组件已切换到服务层与新 Composable，实现通知、审批、文件上传等统一逻辑

#### 试点 1：RFQ 通知模块迁移（3天）

**范围**: RFQ 相关的 5 个组件
- `RfqManagementView.vue`
- `RfqDetailView.vue`
- `RfqCreateView.vue`
- `RfqApprovalWorkflow.vue`
- `RfqForm.vue`

**迁移步骤**:

1. **替换通知调用** [1天]
   ```vue
   <!-- 迁移前 -->
   <script setup>
   import { ElNotification } from 'element-plus'

   const handleSuccess = () => {
     ElNotification.success({ title: '成功', message: '保存成功' })
   }
   </script>

   <!-- 迁移后 -->
   <script setup>
   import { useNotification } from '@/composables/useNotification'

   const notification = useNotification()

   const handleSuccess = () => {
     notification.success('保存成功', '成功')
   }
   </script>
   ```

2. **替换 HTTP 调用** [1天]
   ```typescript
   // 迁移前
   import { apiFetch } from '@/api/http'
   const data = await apiFetch('/api/rfq')

   // 迁移后
   import { useService } from '@/core/hooks'
   const http = useService<HttpService>('http')
   const data = await http.get('/api/rfq')
   ```

3. **测试验证** [1天]
   - 功能回归测试
   - 性能对比测试
   - 代码审查

**验收标准**:
- [ ] 所有功能正常
- [ ] 性能无明显下降
- [ ] 代码量减少 20%+
- [ ] 无新增 Bug

---

#### 试点 2：审批流程迁移（2天）

**范围**: 审批相关的 3 个组件
- `ApprovalWorkflow.vue`
- `ApprovalQueueView.vue`
- `SupplierChangeApprovalView.vue`

**使用 useApprovalWorkflow**:
```vue
<script setup>
import { useApprovalWorkflow } from '@/composables/useApprovalWorkflow'

const { approve, reject, requestChanges } = useApprovalWorkflow('supplier-change')

const handleApprove = async (id: number) => {
  await approve(id, '审核通过')
  // 刷新列表...
}
</script>
```

---

#### 试点 3：权限控制迁移（2天）

**范围**: 使用权限检查的 10+ 个组件

**使用 v-permission 指令**:
```vue
<!-- 迁移前 -->
<el-button v-if="hasPermission('supplier.edit')">编辑</el-button>

<!-- 迁移后 -->
<el-button v-permission="'supplier.edit'">编辑</el-button>
```

---

#### 试点效果验证（3天）

**验证内容**:

1. **功能验证** [1天]
   - 完整回归测试
   - 用户验收测试（UAT）

2. **性能验证** [1天]
   - 页面加载时间对比
   - 操作响应时间对比
   - 内存占用对比

3. **指标验证** [1天]
   - 重复代码减少量
   - 代码行数减少量
   - 采集中期指标数据

**验收标准**:
- [ ] 所有试点模块功能正常
- [ ] 性能指标达标
- [ ] 团队反馈良好
- [ ] 决定是否全面推广

---

### 4.4 阶段 4：全面迁移（4-6周）

**时间**: 4-6 周（20-30 个工作日）  
**目标**: 分批迁移所有剩余组件  
> **进度**: ✅ 100%（2025-11-16 扫描结果：仅 `apps/web/src/services/notification.ts` 存在合法调用，其余 79/80 组件已切换至 `notificationService`）。批量迁移通过 `node tools/scripts/migrate-to-service.js apps/web/src --write` 完成，`docs/migration-progress.md` 与 `var/migration/notification-usage.json` 已更新，CI 守护已启用。

#### 迁移批次规划

结合依赖关系与业务优先级，迁移严格按“RFQ → 供应商 → 审批 → 系统 → 其他”五个批次推进，每批次绑定时间窗口、产出与责任人：

| 批次 | 模块 | 时间窗口 | 剩余组件 | Owner | 里程碑 |
|------|------|----------|----------|-------|--------|
| 1 | RFQ | W47（11/18-11/22） | 5 | 工程 A/B | RFQ 模块 100% 迁移 + 模板复盘 |
| 2 | 供应商 | W48-W49（11/25-12/04） | 13 | 工程 C/D | 供应商模块 ≥60% 迁移并清零遗留通知模式 |
| 3 | 审批流程 | W49-W50（12/02-12/11） | 7 | Workflow Guild | Approval 队列/面板全部切换 useApprovalWorkflow |
| 4 | 系统管理 | W50-W51（12/09-12/18） | 17 | 平台组 | Admin/System 视图全部使用服务层与权限指令 |
| 5 | 其他模块 | W51（12/19-12/24） | 15 | Ops + 新同学 | 辅助模块收尾，完成 100% 遍历与验收 |

**批次 1：RFQ 模块（1周）**

| 组件 | 迁移内容 | 工时 | 负责人 |
|------|---------|------|-------|
| RfqLineItemsEditor.vue | 通知+HTTP+审计 | 1天 | 工程师A |
| RfqSupplierInvitation.vue | 通知+HTTP | 0.5天 | 工程师A |
| RfqQuoteComparison.vue | 通知+HTTP+缓存 | 1.5天 | 工程师B |
| RfqPriceComparisonSection.vue | HTTP+权限 | 1天 | 工程师B |
| RfqPriceComparisonTable.vue | useNotification + 缓存服务 | 1天 | 工程师B |

**批次 2：供应商管理（1.5周）**

| 组件 | 迁移内容 | 工时 | 负责人 |
|------|---------|------|-------|
| SupplierDirectoryView.vue | 通知+HTTP+权限+缓存 | 2天 | 工程师C |
| SupplierRegistrationForm.vue | 通知+HTTP+验证 | 1.5天 | 工程师C |
| SupplierProfileView.vue | 通知+HTTP+权限 | 1天 | 工程师D |
| SupplierChangeRequestForm.vue | useApprovalWorkflow | 1天 | 工程师D |
| SupplierDashboardPanel.vue | useNotification + usePermission | 0.5天 | 工程师D |

**批次 3：审批流程（1.5周）**

| 组件 | 迁移内容 | 工时 | 负责人 |
|------|---------|------|-------|
| ApprovalDashboardView.vue | 接入 useApprovalWorkflow + useNotification + 服务化过滤器 | 1.5天 | Workflow Guild |
| ApprovalQueueView.vue | 替换轮询 HTTP、统一权限检查与批量操作通知 | 1.5天 | Workflow Guild |
| ApprovalWorkflow.vue | 拆分审批状态计算为服务层 + 添加缓存 | 1天 | 工程师E |
| RegistrationApprovalView.vue | useApprovalWorkflow + usePermission + 审计 | 1天 | 工程师F |
| SupplierChangeApprovalView.vue | 通知/审批统一 + useNotification | 1天 | 工程师F |
| FileUploadApprovalView.vue | 批量上传审批流程迁移至 HTTP 服务 + 审计服务 | 1天 | 工程师E |

**批次 4：系统管理（2周）**

| 组件 | 迁移内容 | 工时 | 负责人 |
|------|---------|------|-------|
| AdminPermissionsView.vue | 全量权限表操作改用 permissionService/useNotification | 2天 | 平台组 |
| AdminAuditLogView.vue | HTTP + 审计服务整合 + useNotification | 1.5天 | 平台组 |
| AdminBulkDocumentImportView.vue | 上传流程迁移至 service/http + 通知 | 1.5天 | 平台组 |
| EmailSettingsView.vue | 邮件测试/保存流程切换 useNotification + HTTP | 1天 | 工程师G |
| OrganizationalUnitsView.vue | usePermission 指令 + 缓存服务 | 1.5天 | 工程师H |
| TagManagementView.vue | 通用表格迁移 + useNotification | 1天 | 工程师H |

**批次 5：其他模块（2周）**

| 组件 | 迁移内容 | 工时 | 负责人 |
|------|---------|------|-------|
| DashboardView.vue & UnifiedDashboard.vue | 全局通知/指标卡服务化 | 1.5天 | Ops 团队 |
| AccountActivationView.vue & LoginView.vue | useNotification + 错误处理统一 | 1天 | Ops 团队 |
| MaterialRequisition* 视图 | 切换 HTTP/notification/composables | 2天 | 新同学 Pair |
| SupplierDashboardPanel.vue | useNotification/usePermission | 1天 | Ops 团队 |
| I18nDebugger.vue / TodoCard.vue 等支撑组件 | 清理旧通知、注入服务 | 1天 | 新同学 Pair |

#### 加速推进策略

1. **周/日节奏**：设定每周至少迁移 12 个组件（RFQ/供应商批次各 3 个/人），每日站会对照批次燃尽图，落后 >1 天即触发支援。
2. **批次 DoR/DoD**：进入批次前完成扫描（`node tools/scripts/scan-notifications.js`），产出勾选清单；退出批次需附 PR 列表、基准截图与 `docs/migration-progress.md` 更新。
3. **专属支援池**：将 useNotification 与 useApprovalWorkflow 的作者纳入“随叫随到”列表，解决脚本/类型疑问 ≤2 小时。
4. **并行守则**：控制每名工程师最多 2 个在迁组件，防止 9.4% 停滞再现；跨批次任务必须在 Slack “#phase4-migration” 报备。
5. **质量闸口**：批次验收前运行 `vitest run tests/services/*.spec.ts` + 关键端到端脚本，避免新旧模式混用。
6. **自动化守护**：新增 `.github/workflows/check-migration.yml`，在 Push/PR 阶段扫描 `ElMessage/ElMessageBox/ElNotification`，仅允许 `apps/web/src/services/notification.ts` 持续引用。

#### 阶段 4 交付成果（2025-11-16）

- **批量迁移**：`node tools/scripts/migrate-to-service.js apps/web/src --write` 覆盖 66 个组件 + 1 个 composable，所有遗留通知/确认调用完成服务化。
- **验证报告**：`var/migration/notification-usage.json` 仅记录通知服务文件，`docs/migration-progress.md` 更新为 5 大模块 100% 迁移。
- **代码规范**：`apps/web/.eslintrc.cjs` 禁止直接使用 `ElNotification/ElMessage`，并由新建 CI workflow 阻止回归。
- **测试基线**：`npx vitest run tests/services/permission.spec.ts` 通过，验证服务层依赖链稳定。

#### 迁移清单跟踪

**工具**: `docs/migration-progress.md`

```markdown
# 组件迁移进度

## 总体进度
- 总组件数: 80
- 已迁移: 25 (31%)
- 进行中: 5 (6%)
- 待迁移: 50 (63%)

## RFQ 模块 [50%]
- [x] RfqManagementView.vue
- [x] RfqDetailView.vue
- [x] RfqCreateView.vue
- [ ] RfqLineItemsEditor.vue - 进行中（工程师 A）
- [ ] RfqQuoteComparison.vue

## 供应商管理 [20%]
- [x] SupplierDirectoryView.vue
- [ ] SupplierRegistrationForm.vue
- [ ] ...
```

#### 每批次验收标准

- [ ] 所有组件测试通过
- [ ] 代码审查通过
- [ ] 无新增 P0/P1 Bug
- [ ] 性能指标达标
- [ ] 更新迁移进度文档

---

### 4.5 避免长期"双轨"状态的措施

**问题**: 新旧模式并存时间过长，维护成本高

**解决方案**:

1. **设置截止日期** [强制]
   - 阶段 4 完成后 2 周内，所有组件必须完成迁移
   - 未迁移组件冻结功能开发

2. **CI 检查** [自动化]
   ```yaml
   # .github/workflows/check-migration.yml
   name: Check Migration Progress
   on: [pull_request]
   jobs:
     check:
       - name: Detect old patterns
         run: |
           # 检测是否使用旧模式
           if grep -r "ElNotification\|ElMessage" apps/web/src --include="*.vue" --exclude-dir="node_modules"; then
             echo "::warning::Found old notification pattern. Please migrate to useNotification()"
           fi
   ```

3. **迁移助手工具** [半自动]
   ```bash
   # tools/scripts/migrate-component.sh
   # 半自动迁移组件
   node tools/scripts/migrate-component.js apps/web/src/components/MyComponent.vue
   ```

4. **团队激励** [管理]
   - 迁移进度纳入绩效考核
   - 设置迁移里程碑奖励

5. **废弃旧模式** [强制]
   - 阶段 4 完成后，禁止使用旧模式
   - ESLint 规则禁止旧模式
   ```javascript
   // .eslintrc.js
   rules: {
     'no-restricted-imports': ['error', {
       paths: [{
         name: 'element-plus',
         importNames: ['ElNotification', 'ElMessage'],
         message: 'Please use useNotification() instead'
       }]
     }]
   }
   ```

---

## 5. 指标采集与验证方案

### 5.1 指标采集时间点

| 时间点 | 验证内容 | 负责人 |
|--------|---------|-------|
| T0（阶段 0） | 基线数据 | QA 工程师 |
| T1（阶段 1 完成） | 基础设施指标 | 核心开发 |
| T2（试点完成） | 试点效果 | 项目经理 |
| T3（迁移完成） | 最终收益 | 技术负责人 |
| T4（上线 3 个月） | 长期追踪 | 技术负责人 |

### 5.2 自动化指标采集

#### 代码复用性指标

**采集脚本**: `tools/metrics/collect-code-reuse.js`

```javascript
const jscpd = require('jscpd')
const fs = require('fs')

async function collectCodeReuseMetrics() {
  // 1. 重复代码率
  const duplication = await jscpd.detect({
    path: ['apps/web/src'],
    format: ['typescript', 'vue']
  })

  // 2. 服务使用统计
  const serviceUsage = await analyzeServiceUsage()

  // 3. 通知调用统一度
  const notificationUnification = await analyzeNotificationUsage()

  const metrics = {
    timestamp: new Date().toISOString(),
    duplicateCodeRate: duplication.statistics.total.percentage,
    serviceUsageCount: serviceUsage.totalCalls,
    notificationUnificationRate: notificationUnification.serviceRate
  }

  // 保存到 var/metrics/
  fs.writeFileSync(
    `var/metrics/code-reuse-${Date.now()}.json`,
    JSON.stringify(metrics, null, 2)
  )

  return metrics
}
```

**CI 集成**:
```yaml
# .github/workflows/metrics.yml
name: Collect Metrics
on:
  schedule:
    - cron: '0 0 * * 0'  # 每周日
  workflow_dispatch:

jobs:
  metrics:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Collect metrics
        run: |
          npm run metrics:collect
      - name: Upload metrics
        uses: actions/upload-artifact@v3
        with:
          name: metrics
          path: var/metrics/
```

---

#### 开发效率指标

**采集脚本**: `tools/metrics/collect-dev-efficiency.js`

```javascript
const { Octokit } = require('@octokit/rest')

async function collectDevEfficiency() {
  const octokit = new Octokit({ auth: process.env.GITHUB_TOKEN })

  // 获取最近 30 天的 PR 数据
  const { data: prs } = await octokit.pulls.list({
    owner: 'your-org',
    repo: 'supplier-system',
    state: 'all',
    per_page: 100
  })

  const metrics = {
    totalPRs: prs.length,
    avgReviewTimeHours: calculateAvgReviewTime(prs),
    avgCodeLinesPerPR: calculateAvgCodeLines(prs)
  }

  return metrics
}
```

---

### 5.3 验证报告生成

**报告模板**: `docs/metrics/report-template.md`

```markdown
# 服务层优化 - 效果验证报告

**验证时间**: {{timestamp}}
**验证阶段**: {{stage}}

## 1. 指标达成情况

| 指标 | 目标 | 实际 | 达成率 | 趋势 |
|------|------|------|-------|------|
| 代码复用性 | +40% | {{actual}}% | {{rate}}% | {{trend}} |
| 开发效率 | +25% | {{actual}}% | {{rate}}% | {{trend}} |
| Bug 修复时间 | -30% | {{actual}}% | {{rate}}% | {{trend}} |
| 测试覆盖率 | +20% | {{actual}}% | {{rate}}% | {{trend}} |

## 2. 详细分析

### 2.1 代码复用性
- 重复代码率: {{before}}% → {{after}}% ({{change}}%)
- 服务调用次数: {{count}}
- 通知统一度: {{rate}}%

### 2.2 开发效率
- 功能开发时间: {{change}}%
- PR 审查时间: {{change}}%

### 2.3 Bug 修复
- 平均修复时间: {{change}}%
- 一周内修复率: {{rate}}%

### 2.4 测试覆盖
- 单元测试: {{before}}% → {{after}}%
- 服务测试: {{rate}}%

## 3. 结论与建议
...
```

---

## 6. 性能测试与监控方案

### 6.1 性能测试环境

**测试环境配置**:

| 环境 | 配置 | 用途 |
|------|------|------|
| 开发环境 | 本地开发机 | 开发时性能检查 |
| 测试环境 | 2C4G 虚拟机 | 性能基准测试 |
| 预生产环境 | 生产同配置 | 上线前压测 |

**数据量级**:

| 数据类型 | 测试数量 | 说明 |
|---------|---------|------|
| 供应商数据 | 10,000 条 | 模拟生产规模 |
| RFQ 数据 | 5,000 条 | 近 1 年数据量 |
| 并发用户 | 100 | 峰值并发 |

---

### 6.2 性能指标定义

**关键性能指标 (KPI)**:

| 指标 | 目标值 | 测量方式 | 验收标准 |
|------|--------|---------|---------|
| 应用启动时间 | 增加 < 100ms | Performance API | 必须达标 |
| 服务调用延迟 | < 1ms | 埋点统计 | 必须达标 |
| 页面首屏时间 | 增加 < 50ms | Lighthouse | 推荐达标 |
| 内存占用 | 增加 < 5MB | Chrome DevTools | 必须达标 |
| 包体积增加 | < 50KB | Rollup Analyzer | 推荐达标 |

---

### 6.3 性能测试方案

#### 测试 1：应用启动性能

**测试脚本**: `tests/performance/app-startup.test.ts`

```typescript
import { performance } from 'perf_hooks'

describe('App Startup Performance', () => {
  it('should start all services within 100ms', async () => {
    const start = performance.now()

    // 模拟应用启动
    const app = createApp(App)
    const manager = installServiceManager(app)
    await manager.startAll()

    const duration = performance.now() - start

    console.log(`App startup time: ${duration}ms`)
    expect(duration).toBeLessThan(100)
  })

  it('should start each service within 20ms', async () => {
    const manager = new ServiceManager(app, {})

    const services = ['notification', 'http', 'permission', 'cache', 'audit']

    for (const serviceName of services) {
      const start = performance.now()
      await manager.start(serviceName)
      const duration = performance.now() - start

      console.log(`${serviceName} startup: ${duration}ms`)
      expect(duration).toBeLessThan(20)
    }
  })
})
```

**执行**:
```bash
npm run test:performance
```

---

#### 测试 2：服务调用性能

**测试脚本**: `tests/performance/service-calls.test.ts`

```typescript
describe('Service Call Performance', () => {
  it('should call useService within 1ms', () => {
    const iterations = 1000
    const start = performance.now()

    for (let i = 0; i < iterations; i++) {
      const notification = useService('notification')
    }

    const duration = performance.now() - start
    const avgDuration = duration / iterations

    console.log(`Average useService call: ${avgDuration}ms`)
    expect(avgDuration).toBeLessThan(1)
  })
})
```

---

#### 测试 3：内存泄漏检测

**测试脚本**: `tests/performance/memory-leak.test.ts`

```typescript
describe('Memory Leak Detection', () => {
  it('should not leak memory on service usage', async () => {
    const initialMemory = process.memoryUsage().heapUsed

    // 模拟大量服务调用
    for (let i = 0; i < 10000; i++) {
      const notification = useService('notification')
      // 使用服务
    }

    // 强制 GC
    if (global.gc) {
      global.gc()
    }

    const finalMemory = process.memoryUsage().heapUsed
    const memoryIncrease = (finalMemory - initialMemory) / 1024 / 1024

    console.log(`Memory increase: ${memoryIncrease}MB`)
    expect(memoryIncrease).toBeLessThan(5)
  })
})
```

---

### 6.4 生产环境监控

#### 监控指标

**前端性能监控**:

```typescript
// apps/web/src/core/services/monitor.ts

export const monitorService: ServiceDefinition = {
  dependencies: [],

  start(env): MonitorService {
    // 1. 服务调用监控
    const serviceMetrics = new Map()

    // 2. 性能指标采集
    const observer = new PerformanceObserver((list) => {
      list.getEntries().forEach((entry) => {
        if (entry.entryType === 'measure') {
          console.log(`${entry.name}: ${entry.duration}ms`)

          // 发送到监控平台
          if (entry.duration > 100) {
            sendToMonitor({
              type: 'performance',
              metric: entry.name,
              duration: entry.duration,
              timestamp: Date.now()
            })
          }
        }
      })
    })

    observer.observe({ entryTypes: ['measure'] })

    return {
      recordServiceCall(serviceName: string, duration: number) {
        if (!serviceMetrics.has(serviceName)) {
          serviceMetrics.set(serviceName, {
            calls: 0,
            totalDuration: 0,
            errors: 0
          })
        }

        const metrics = serviceMetrics.get(serviceName)
        metrics.calls++
        metrics.totalDuration += duration

        // 每 100 次调用报告一次
        if (metrics.calls % 100 === 0) {
          sendToMonitor({
            type: 'service-metrics',
            service: serviceName,
            avgDuration: metrics.totalDuration / metrics.calls,
            calls: metrics.calls,
            errors: metrics.errors
          })
        }
      }
    }
  }
}
```

#### 监控仪表板

**工具**: Grafana + Prometheus（或自建简易面板）

**关键面板**:

1. **服务性能面板**
   - 各服务平均调用时间
   - 服务调用频率
   - 服务错误率

2. **应用性能面板**
   - 页面加载时间趋势
   - 首屏渲染时间
   - 内存占用趋势

3. **用户体验面板**
   - 页面响应时间
   - 操作成功率
   - 错误通知频率

#### 告警规则

```yaml
# monitoring/alerts.yml

alerts:
  - name: ServiceCallSlow
    condition: avg(service_call_duration) > 10ms
    severity: warning
    message: "服务调用平均耗时超过 10ms"

  - name: HighErrorRate
    condition: sum(service_errors) / sum(service_calls) > 0.01
    severity: critical
    message: "服务错误率超过 1%"

  - name: MemoryLeak
    condition: increase(memory_usage[1h]) > 50MB
    severity: warning
    message: "内存使用量 1 小时内增长超过 50MB"
```

---

### 6.5 性能优化措施

**如果性能指标未达标**:

| 问题 | 优化措施 |
|------|---------|
| 启动时间过长 | 1. 延迟加载非关键服务<br>2. 并行启动独立服务<br>3. 减少启动时的同步操作 |
| 服务调用延迟高 | 1. 缓存服务实例<br>2. 优化依赖注入算法<br>3. 减少代理层级 |
| 内存占用过高 | 1. 实现服务实例复用<br>2. 及时清理事件监听<br>3. 优化缓存策略 |
| 包体积过大 | 1. Tree-shaking 优化<br>2. 按需加载服务<br>3. 代码分割 |

---

## 7. 风险管理

### 7.1 技术风险

| 风险 | 概率 | 影响 | 缓解措施 | 应急预案 |
|------|------|------|---------|---------|
| 循环依赖检测失败 | 中 | 高 | 完善检测算法，添加详细错误信息 | 手动检查依赖关系，限制服务间依赖 |
| 服务启动失败阻塞应用 | 中 | 高 | 实现降级机制，非关键服务失败继续启动 | 禁用失败的服务，记录错误日志 |
| TypeScript 类型复杂度过高 | 低 | 中 | 使用简单类型，逐步增强 | 降低类型复杂度，使用 any 作为临时方案 |
| 性能问题 | 低 | 中 | 提前进行性能测试和优化 | 延迟加载非关键服务 |

### 7.2 项目风险

| 风险 | 概率 | 影响 | 缓解措施 | 应急预案 |
|------|------|------|---------|---------|
| 时间延期 | 中 | 中 | 每日站会跟踪进度，及时调整 | 砍掉非核心功能，延后实施 |
| 人员变动 | 低 | 高 | 知识共享，文档完善 | 重新分配任务，调整计划 |
| 需求变更 | 中 | 中 | 冻结需求，变更走评审流程 | 评估影响，调整优先级 |

### 7.3 测试风险

| 风险 | 概率 | 影响 | 缓解措施 | 应急预案 |
|------|------|------|---------|---------|
| 测试覆盖不足 | 中 | 高 | 设置覆盖率目标，强制执行 | 增加测试时间，延后发布 |
| 回归测试不充分 | 中 | 高 | 建立完整的测试用例，自动化执行 | 手动测试补充，增加测试人力 |

---

## 8. 质量保证

### 8.1 代码质量标准

**强制要求**:
- ESLint 检查通过（0 errors, 0 warnings）
- TypeScript 类型检查通过
- 所有函数和类有 JSDoc 注释
- 复杂逻辑有内联注释

**推荐标准**:
- 函数长度 < 50 行
- 圈复杂度 < 10
- 参数数量 < 5

### 8.2 测试标准

**单元测试**:
- 覆盖率 ≥ 85%
- 所有公共方法有测试
- 边界条件有测试
- 错误处理有测试

**集成测试**:
- 服务间集成测试
- 组件与服务集成测试
- 端到端测试

### 8.3 Code Review 清单

**服务代码 Review**:
- [ ] 服务接口设计合理
- [ ] 依赖声明正确
- [ ] 错误处理完善
- [ ] 有单元测试
- [ ] 文档完整

**通用 Review**:
- [ ] 命名清晰
- [ ] 代码结构清晰
- [ ] 无重复代码
- [ ] 性能考虑
- [ ] 安全考虑

---

## 9. 附录

### 9.1 参考资料

- [Odoo Services Documentation](https://www.odoo.com/documentation/19.0/developer/reference/frontend/services.html)
- [Odoo Registries Documentation](https://www.odoo.com/documentation/19.0/developer/reference/frontend/registries.html)
- [Vue 3 Dependency Injection](https://vuejs.org/guide/components/provide-inject.html)
- [依赖注入设计模式](https://en.wikipedia.org/wiki/Dependency_injection)

### 9.2 术语表

| 术语 | 定义 |
|------|------|
| 服务 (Service) | 长生命周期的对象，提供特定功能，通常是单例 |
| 注册表 (Registry) | 有序的键值映射，用于注册和查找对象 |
| 依赖注入 (DI) | 一种设计模式，将依赖关系通过外部注入而非内部创建 |
| Composable | Vue 3 的组合式函数，用于封装可复用的逻辑 |
| Hook | 特殊的函数，用于在组件中使用某些功能 |

### 9.3 联系人

| 角色 | 姓名 | 邮箱 |
|------|------|------|
| 技术负责人 | - | - |
| 核心开发工程师 | - | - |
| QA 负责人 | - | - |
| 技术文档工程师 | - | - |

### 9.4 变更记录

| 版本 | 日期 | 变更内容 | 作者 |
|------|------|---------|------|
| 1.0 | 2025-11-16 | 初始版本 | 开发团队 |

---

**文档结束**
