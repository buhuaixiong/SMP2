# Service Layer Architecture Design

**状态**: 已实施（Stage 1 完成）  
**更新日期**: 2025-11-16  
**适用范围**: `apps/web` 前端工程

## 1. 背景与目标

现有前端在 80+ 组件里直接操作 Element Plus、Axios 与权限判断，重复代码和缺乏统一治理导致：

- 通知与错误处理逻辑散落 200+ 处，修改困难；
- API 调用缺少统一拦截与性能监控；
- 权限校验依赖零散的 Pinia store；
- 业务横切关注点（缓存、审计）难以复用。

本设计以 Odoo Service Layer 为参考，实现可注册、可注入、可监控的服务框架，目标：

1. **统一注册中心**：所有服务和 Composable 通过 Registry 管理，提供可观察的事件机制。
2. **服务管理器**：支持声明式依赖、异步启动、降级策略与生命周期控制。
3. **核心横切服务**：通知、HTTP、权限、缓存、审计具备一致 API 并易于扩展。
4. **开发体验**：通过 `useService`/`useServices`、全局指令和测试工具简化调用与验证。

## 2. 核心组件

### 2.1 Registry & RegistryManager

| 要素 | 说明 |
|------|------|
| `Registry<T>` | 维护有序条目，支持 `add/get/remove/keys/getAll/on`，可根据 `sequence` 控制加载顺序。 |
| `RegistryManager` | 以 `category` 维度管理多个 Registry（`services`、`composables`、`directives` 等）。 |
| 事件机制 | `add/remove/clear` 事件可用于开发期间调试或未来的热更新 Hook。 |

### 2.2 ServiceManager

- **依赖声明**：服务通过 `dependencies` 列表声明依赖，启动顺序由拓扑排序+序号决定。
- **循环检测**：内部维护 `startStack`，一旦出现重复路径立即抛出清晰错误。
- **生命周期**：`setup` 产生实例、`teardown` 负责资源回收（如定时器、缓存）。
- **容错**：`startAll` 收集 `ServiceStartError`，允许非关键服务失败时记录并降级。
- **Vue 集成**：`installServiceManager(app)` 挂载到 `app.provide`，`useServiceManager()`/`useService()` 在组件中消费。

### 2.3 useService / useServices

统一入口检查服务注册与启动状态，便于类型推断：

```ts
import { useService } from "@/core/hooks"
const http = useService<HttpService>("http")
await http.get("/suppliers")
```

对多服务场景可使用 `useServices(["http", "notification"])` 返回 Record。

## 3. 内置服务概览

| 服务 | 作用 | 关键依赖 |
|------|------|---------|
| `notification` | Element Plus Notification/Message/MessageBox 的统一包装，提供 sticky 错误等功能 | 无 |
| `http` | Axios 单例 + `apiFetch` 包装，统一注入 Token/错误提示/静默模式 | `notification` |
| `permission` | 基于 `useAuthStore` 的权限缓存、TTL 刷新及 `v-permission`/`v-role` 指令 | `http` |
| `cache` | 内存型 KV + TTL + 自动清理，用于列表、配置缓存 | 无 |
| `audit` | 事件缓冲/定时/`beforeunload` 发送审计日志 | `http`, `notification` |

所有服务在 `apps/web/src/services/index.ts` 统一注册，并在 `main.ts` 中 `serviceManager.startAll()` 后挂载应用。

## 4. 流程示意

```
App bootstrap
 ├─ createApp(App)
 ├─ registerServices()        # 写入 registry.services
 ├─ installServiceManager()   # 提供 ServiceManager 实例
 ├─ serviceManager.startAll()
 │    ├─ notification.setup()
 │    ├─ http.setup(notification)
 │    ├─ permission.setup(http)
 │    ├─ cache.setup()
 │    └─ audit.setup(http, notification)
 ├─ register directives (permission/role)
 └─ app.mount("#app")
```

## 5. 扩展/定制指南

1. **新增服务**  
   - 在 `apps/web/src/services/<name>.ts` 导出 `ServiceDefinition`，声明 `dependencies`；  
   - 在 `services/index.ts` 注册并设定 `sequence`；  
   - 若需对外暴露类型，在 `services/index.ts` re-export。

2. **自定义 Composable**  
   - 优先调用现有服务（如 `useService("http")`）避免直接依赖外部库；  
   - 将通用逻辑注册到 `registry.category("composables")` 以便统一管理（阶段 2 目标）。

3. **指令/插件**  
   - 指令/插件同样可以依赖 ServiceManager（示例：`permissionDirective` 通过 `inject` 获取 manager）。

4. **测试策略**  
   - 使用 `tests/setup/mockServices.ts` 的 `createMockServiceManager`、`withService` 等辅助函数；  
   - 模拟服务请在 `teardown` 中清理资源，避免影响后续用例。

## 6. 性能与监控

- `http` 服务在请求/响应拦截器里记录耗时，可用于后续性能埋点；
- `audit` 服务的缓冲区大小与刷写间隔可在配置中调整，默认 20 条/15s；
- 注册表事件为未来的 devtools/热更新提供扩展点。

## 7. 未来工作

- 阶段 2：提炼跨模块 Composables 并注册到 `registry.category("composables")`；
- 阶段 3：挑选 RFQ/供应商模块做试点迁移并验证基线指标；
- 阶段 4：按路线图推进全面迁移，并通过 `tools/scripts/scan-*.js`/`docs/migration-progress.md` 追踪。

---
如需修改设计，请同步更新本文件并在 `docs/development/service-layer-implementation-plan.md` 记录变更。*** End Patch*** End Patch
