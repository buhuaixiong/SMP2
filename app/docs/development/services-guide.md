# 服务开发指南

**版本**: 1.0 （2025-11-16）  
**适用范围**: `apps/web` 前端项目（Vue 3 + TypeScript）

## 1. 核心概念

- **ServiceDefinition**：描述服务的名称、依赖、`setup`/`teardown` 函数，用于 ServiceManager 启动。
- **Registry**：有序存储服务定义，允许同类服务按 `sequence` 排序。
- **ServiceManager**：负责解析依赖、启动/停止服务，并向 Vue 应用提供实例。
- **useService/useServices**：在组件或 Composable 中消费服务的首选方式。

## 2. 创建服务的标准流程

1. **定义文件**  
   在 `apps/web/src/services/<name>.ts` 导出 `ServiceDefinition`：
   ```ts
   import type { ServiceDefinition } from "@/core/services"

   export interface FooService { ping(): string }

   export const fooService: ServiceDefinition<FooService> = {
     name: "foo",
     dependencies: ["http"],
     async setup({ get }) {
       const http = get<HttpService>("http")
       return {
         ping() {
           return http.get("/foo/ping")
         },
       }
     },
     async teardown(instance) {
       // 清理定时器或取消订阅
     },
   }
   ```

2. **注册**  
   在 `apps/web/src/services/index.ts` 中导入并加入 `coreServices`，根据依赖设置 `sequence`。

3. **类型导出**  
   将接口导出供调用方使用：`export type { FooService } from "./foo"`。

4. **使用**  
   - 组件/Composable：`const foo = useService<FooService>("foo")`；  
   - 渲染逻辑中避免直接依赖 Axios/Pinia，而是委托 FooService。

5. **测试**  
   - 使用 `tests/templates/service.test.ts` 生成测试骨架；  
   - 可通过 `tests/setup/mockServices.ts` 的 `createMockServiceManager` 注入依赖；  
   - 覆盖：初始化、依赖注入、错误分支、teardown。

## 3. 命名与依赖规范

| 项目 | 规范 |
|------|------|
| 服务标识符 | 使用 kebab/camel 合理组合（`notification`、`supplierAudit`），保持唯一 |
| 文件名 | 与服务名一致，如 `notification.ts`、`supplier-audit.ts` |
| 依赖声明 | 明确列出必需服务，避免运行时 `useService` 再次调用 |
| 可选依赖 | 使用 `dependencies` + `optional: true` 或在 `setup` 中 try/catch |
| 序号 | `sequence` 以 10 为粒度，确保基础服务先于业务服务 |

## 4. 内置服务摘要

| 服务 | 主要 API | 备注 |
|------|---------|------|
| `notification` | `success/info/warning/error/notify/message/confirm` | Element Plus 封装，`error` 支持 `sticky` |
| `http` | `request/get/post/put/delete/fetch` | 自动注入 Token、统一错误展示、`silent` 模式 |
| `permission` | `has/hasAny/hasAll/hasRole/getRole/refresh` | TTL 缓存 + `v-permission` 指令 |
| `cache` | `set/get/has/delete/clear/getOrSet/cleanup` | 内存 TTL + 自动清理 |
| `audit` | `log/logView/logCreate/logUpdate/logDelete/flush` | 批量发送，支持 `beforeunload` |

## 5. 最佳实践

1. **聚焦职责**：一个服务只处理一个业务域/横切关注点，避免“万能服务”。  
2. **无状态优先**：除缓存/权限等特例外，大部分服务应保持无状态，或明确 state 来源。  
3. **不可变配置**：通过 `ServiceManagerOptions.config` 传递环境信息，避免硬编码。  
4. **丰富日志**：使用 `context.manager` 的 logger 写入关键日志便于排障。  
5. **Teardown 友好**：确保所有订阅/定时器可在 `teardown` 中清除，以支持测试与热更新。  
6. **错误约定**：向调用方抛出 Error 对象而不是字符串，统一由组件决定展示方式。  
7. **文档同步**：添加/修改服务后更新 `docs/api/services/<name>.md`。

## 6. 测试指南

- **单元测试**：使用模板，模拟依赖时通过 `vi.fn()` stub 出具 `ServiceDefinition`。  
- **组件测试**：借助 `mountWithServices` 将所需服务注入 Vue 测试实例。  
- **性能测试**：若服务启动耗时 >10ms，考虑在 `tests/performance` 添加专项用例。  
- **Mock 规则**：mock 服务名称使用 `mock:<service>`，避免与真实服务冲突。

## 7. FAQ

**Q: 服务能访问 Pinia store 吗？**  
A: 可以，但建议通过注入 store getter 而非直接引用，为 SSR/测试提供灵活性。

**Q: 业务组件仍可直接调用 Axios 吗？**  
A: 不推荐。除非特殊一次性脚本，所有 HTTP 调用应走 `http` 服务以统一 token/错误处理。

**Q: 服务间能形成环依赖吗？**  
A: ServiceManager 在启动阶段会检测环依赖并抛错。若确有互相调用需求，可拆分公共逻辑或通过事件总线解耦。

---
如需更新规范，请开 PR 同步修改本指南，并在 `docs/development/service-layer-implementation-plan.md` 中记录状态。*** End Patch
