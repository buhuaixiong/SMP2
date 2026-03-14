# ServiceManager API

文件：`apps/web/src/core/services/ServiceManager.ts`

## 构造函数

```ts
new ServiceManager({
  registry?: RegistryManager,
  app?: App,
  config?: Record<string, unknown>,
  category?: string,            // 默认 "services"
  logger?: { log?, warn?, error? },
})
```

- `registry`：可注入自定义 RegistryManager（测试用）。  
- `config`：通过 `context.config` 传递静态配置。  
- `logger`：默认使用 `console`。

## 关键方法

| 方法 | 返回值 | 描述 |
|------|--------|------|
| `setApp(app: App)` | `void` | 设置 Vue `App` 实例 |
| `getApp()` | `App \| undefined` | 获取当前 App |
| `startAll()` | `Promise<void>` | 按序启动所有注册服务，错误将收集到 `getLastErrors()` |
| `start(name)` | `Promise<T>` | 启动单个服务，返回实例 |
| `stopAll()` | `Promise<void>` | 按逆序调用各服务 `teardown` 并清理实例 |
| `stop(name)` | `Promise<void>` | 停止某个服务 |
| `get(name)` | `T` | 获取已启动服务实例；未启动将抛错 |
| `has(name)` | `boolean` | 是否存在服务定义 |
| `isStarted(name)` | `boolean` | 服务是否已经启动 |
| `getStatus(name)` | `"registered" \| "starting" \| "ready" \| "stopped" \| "failed"` | 当前状态 |
| `getLastErrors()` | `ServiceStartError[]` | 最近 `startAll` 过程中出现的错误 |
| `clearLastErrors()` | `void` | 清空错误记录 |
| `listServiceNames()` | `string[]` | 返回注册表中的服务名称 |

## ServiceContext

`setup` 函数会收到以下上下文：

```ts
interface ServiceContext {
  app?: App
  manager: ServiceManager
  config: Record<string, unknown>
  get<T>(name: string): T
  has(name: string): boolean
}
```

## ServiceDefinition

```ts
interface ServiceDefinition<T = unknown> {
  name: string
  dependencies?: string[]
  optional?: boolean
  setup(context: ServiceContext): Promise<T> | T
  teardown?(instance: T, context: ServiceContext): Promise<void> | void
  description?: string
}
```

## 错误类型

`ServiceStartError extends Error`，包含：

- `service`: 服务名  
- `cause`: 原始错误  
- `message`: 归一化后的描述（循环依赖/未注册/启动失败等）

## Vue 集成

```ts
import { installServiceManager } from "@/core/services"

const serviceManager = installServiceManager(app, { config: { locale: "zh-CN" } })
await serviceManager.startAll()
```

组件内部使用：

```ts
import { useService } from "@/core/hooks"
const http = useService<HttpService>("http")
```

## 日志与性能建议

- `logger` 默认写入 `console`，可在生产环境注入自定义 logger；  
- 启动顺序：按 `sequence` + `dependencies` 拓扑排序；  
- 对于非关键服务，可在 `setup` 中捕获错误并返回降级实现，再由 ServiceManager 记录。*** End Patch
