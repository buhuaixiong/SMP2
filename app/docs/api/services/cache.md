# CacheService API

文件：`apps/web/src/services/cache.ts`

```ts
export interface CacheService {
  set<T>(key: string, value: T, ttlMs?: number): void
  get<T>(key: string): T | undefined
  has(key: string): boolean
  delete(key: string): boolean
  clear(): void
  getOrSet<T>(key: string, factory: () => T | Promise<T>, ttlMs?: number): Promise<T>
  cleanup(): void
}
```

- 默认 TTL：5 分钟，可传 `Infinity` 表示永久。  
- 内部存在自动清理定时器（60s），在 `teardown` 中会清除。  
- 适合缓存配置、下拉选项或计算结果，不建议存储大型列表。

### 示例

```ts
const cache = useService<CacheService>("cache")
const supplier = await cache.getOrSet(`supplier:${id}`, () => http.get(`/suppliers/${id}`), 300000)
```
