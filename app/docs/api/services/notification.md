# NotificationService API

文件：`apps/web/src/services/notification.ts`

```ts
export interface NotificationService {
  success(message: string, title?: string, options?: NotificationOptions): void
  warning(message: string, title?: string, options?: NotificationOptions): void
  info(message: string, title?: string, options?: NotificationOptions): void
  error(message: string, title?: string, options?: NotificationOptions & { sticky?: boolean }): void
  notify(options: NotificationParams): void
  message(message: string, type?: "success" | "warning" | "info" | "error", options?: MessageParams): void
  confirm(message: string, title?: string, options?: MessageBoxOptions): Promise<MessageBoxData>
}
```

- `error` 支持 `sticky` 选项，`duration=0` 且强制 `showClose`。  
- `confirm` 默认 `title="确认操作"`, `type="warning"`。  
- Element Plus Notification/Message/MessageBox 均由该服务封装。

### 使用示例

```ts
import { useService } from "@/core/hooks"
import type { NotificationService } from "@/services"

const notification = useService<NotificationService>("notification")
notification.success("保存成功", "供应商")
```
