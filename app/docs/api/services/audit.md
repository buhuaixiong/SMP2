# AuditService API

文件：`apps/web/src/services/audit.ts`

```ts
export interface AuditService {
  log(event: AuditEventPayload): void
  logView(entity: string, entityId?: string | number, description?: string): void
  logCreate(entity: string, data?: Record<string, unknown>): void
  logUpdate(entity: string, data?: Record<string, unknown>): void
  logDelete(entity: string, entityId?: string | number, description?: string): void
  flush(options?: { force?: boolean }): Promise<void>
}
```

`AuditEventPayload` 字段：`entity`, `entityId`, `action`, `description`, `data`, `timestamp`。

特性：

- 内部维护队列（默认批次 20 条），每 15s 自动调用 `http.post("/audit/logs")`。  
- `flush({ force: true })` 可在操作完成后手动发送。  
- `beforeunload` 时使用 `navigator.sendBeacon` 兜底。
