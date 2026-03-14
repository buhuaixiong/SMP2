# PermissionService API

文件：`apps/web/src/services/permission.ts`

```ts
export interface PermissionService {
  has(permission: string): boolean
  hasAny(...permissions: string[]): boolean
  hasAll(...permissions: string[]): boolean
  hasRole(...roles: string[]): boolean
  getRole(): string | null
  getPermissions(): string[]
  refresh(force?: boolean): Promise<string[]>
}
```

实现细节：

- 默认从 `useAuthStore` 初始化权限集合，并缓存 TTL（5 分钟）。  
- `refresh(true)` 强制调用 `/auth/me` 更新权限和角色。  
- `v-permission`/`v-role` 指令基于本服务，可在模板层直接做显隐控制。
*** End Patch
