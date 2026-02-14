# Registry API 参考

文件：`apps/web/src/core/registry/Registry.ts`  
类型：`Registry<T>` 与 `RegistryManager`

## Registry\<T\>

| 方法 | 描述 |
|------|------|
| `add(key: string, value: T, options?: { sequence?: number; metadata?: Record<string, unknown> })` | 添加条目，并根据 `sequence` + 添加顺序排序；返回 `this` 以便链式调用 |
| `get(key: string, defaultValue?: T)` | 获取条目值；若不存在返回 `defaultValue` |
| `has(key: string)` | 判断是否存在条目 |
| `remove(key: string)` | 移除条目并触发 `remove` 事件 |
| `clear()` | 清空注册表并触发 `clear` 事件 |
| `getAll()` | 以排序后的数组形式返回值 |
| `keys()` | 返回排序后的 key 列表 |
| `entries()` | 返回完整的 `RegistryEntry<T>`（含 `sequence`/`metadata`/`addedAt`） |
| `on(listener)` | 订阅 `add/remove/clear` 事件，返回取消订阅函数 |

事件回调类型：`(event: { type: "add" | "remove" | "clear"; entry?: RegistryEntry<T> }) => void`

## RegistryManager

| 方法 | 描述 |
|------|------|
| `category<T>(name: string)` | 获取/创建指定分类的 Registry，例如 `registry.category<ServiceDefinition>("services")` |
| `hasCategory(name: string)` | 判断分类是否存在 |
| `removeCategory(name: string)` | 删除分类 |
| `clear()` | 清空所有分类 |

推荐分类：  
`services`（服务定义）、`composables`（复用逻辑）、`directives`（自定义指令）。

## 使用示例

```ts
import { registry } from "@/core/registry"
import type { ServiceDefinition } from "@/core/services"

registry
  .category<ServiceDefinition>("services")
  .add("foo", {
    name: "foo",
    setup: () => ({ run: () => "ok" }),
  }, { sequence: 60 })
```
