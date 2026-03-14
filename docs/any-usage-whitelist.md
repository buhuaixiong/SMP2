# Any 类型使用白名单

> **目的**: 记录项目中允许使用 `any` 类型的合理场景，作为代码审查的参考依据。
> **维护**: 任何新增白名单条目需经技术负责人审批。

---

## 白名单条目

| ID | 场景 | 位置 | 理由 | 审批状态 |
|----|------|------|------|----------|
| WL-001 | 环境变量类型定义 | `app/apps/web/src/env.d.ts` | Vite 环境变量无类型支持 | ✅ 已批准 |
| WL-002 | 第三方库垫片 | `app/apps/web/src/types/axios.d.ts` | Axios 自定义扩展 | ✅ 已批准 |
| WL-003 | 测试夹具/数据 | `app/apps/web/src/__tests__/**/*` | 测试代码允许 flexibility | ✅ 已批准 |
| WL-004 | 动态 JSON 字段 | `evaluationCriteria: Record<string, any>` | 后端动态字段结构 | ✅ 已批准 |
| WL-005 | API 过渡期间 | `app/apps/web/src/api/http.ts` 重载 | 渐进式迁移需要 | ⏳ 临时，Phase 4 后移除 |

---

## 白名单审批规则

### 1. 允许的场景

- **第三方库无类型**: 库本身无 `@types/xxx` 包，且官方文档使用示例为 JavaScript
- **环境变量定义**: Vite/Node.js 环境变量类型扩展
- **测试代码**: 测试夹具、mock 数据
- **动态 JSON**: 后端返回动态键值对（如 `Record<string, number>` 无法穷举时）
- **迁移过渡期**: 明确的迁移计划中，暂时保留的 `any`

### 2. 禁止的场景

- **API 响应类型**: 有明确后端结构但仍用 `apiFetch<any>`
- **Vue Props**: `defineProps<{ foo: any }>()`
- **组件 Emit**: `emit('update:modelValue', value: any)`
- **Store State**: `state: () => ({ items: any[] })`
- **函数参数**: `function process(data: any)`

### 3. 申请流程

1. 在 `any-usage-whitelist.md` 新增条目（状态标记为 `⏳ 待审批`）
2. 在代码中添加注释：
   ```typescript
   // WL-XXX: 理由简述
   const x: any = ...
   ```
3. 提交 PR，说明业务场景
4. 技术负责人审批后，状态改为 `✅ 已批准`

---

## 示例：动态 JSON 场景

```typescript
// WL-004: 后端评审维度可动态扩展
interface EvaluationCriteria {
  price?: number;
  quality?: number;
  // ...
  [key: string]: number | string | undefined;  // 扩展维度
}
```

---

## 维护日志

| 日期 | 操作 | 内容 | 执行人 |
|------|------|------|--------|
| 2026-01-31 | 创建 | 初始白名单条目 | Claude |
