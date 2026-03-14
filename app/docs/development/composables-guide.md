# Composables 开发规范

**版本**: 1.0  
**更新时间**: 2025-11-16  
**范围**: `apps/web/src/composables`

## 1. 设计原则

1. **单一职责**：每个 Composable 聚焦一个业务或横切功能（如通知、审批流程）。  
2. **显式依赖**：通过 `useService` / `useStore` 获取依赖，避免隐式导入。  
3. **可测试性**：暴露明确的输入/输出，避免在函数内部直接访问 DOM。  
4. **可注册性**：阶段 2 开始，核心 Composable 将注册到 `Registry`，便于动态管理。

## 2. 文件组织

```
apps/web/src/composables/
├── useNotification.ts
├── useApprovalWorkflow.ts
├── useSupplierSearch.ts
└── index.ts               # 集中导出
```

- 命名统一 `useXxx`，与 Vue 官方约定一致。  
- 若 Composable 规模较大，可建立子目录（如 `approval/useApprovalSteps.ts`）。  
- 需要跨模块共享的常量放在 `apps/web/src/composables/constants.ts`。

## 3. 模板

```ts
import { computed, ref } from "vue"
import { useService } from "@/core/hooks"
import type { HttpService } from "@/services"

export function useSupplierSearch() {
  const http = useService<HttpService>("http")
  const loading = ref(false)
  const results = ref([])

  const hasResult = computed(() => results.value.length > 0)

  const search = async (keyword: string) => {
    loading.value = true
    try {
      results.value = await http.get("/suppliers", { params: { q: keyword } })
    } finally {
      loading.value = false
    }
  }

  return {
    loading,
    results,
    hasResult,
    search,
  }
}
```

## 4. 依赖规范

| 类型 | 规范 |
|------|------|
| 服务 | 使用 `useService` 获取；若服务尚未启动会抛错，因此调用应在组件 `setup` 阶段进行 |
| Store | 仅引用 `pinia` store 的 getter/action，禁止直接修改外部 state |
| 工具 | 优先复用 `apps/web/src/utils` 中的函数，避免重复实现 |

## 5. 测试要求

- 使用 `tests/templates/component-with-service.test.ts` 作为骨架；  
- 可通过 `mountWithServices` 向被测组件注入 mock 服务；  
- 对异步逻辑需覆盖 loading/error 分支；  
- 若 Composable 改写 DOM，必须在测试中断言副作用（如 document.title）。

## 6. 最佳实践

1. **返回对象而非数组**，便于调用侧解构与类型推断。  
2. **暴露可扩展钩子**，如 `onSuccess`、`onError` 回调，减少业务层重复。  
3. **命名一致**：loading/useXxx/done 采用统一字段，避免 `isLoading` / `loading` 混用。  
4. **解耦视图**：Composable 内不要引用 Element Plus 组件实例，而是返回纯数据和命令。

## 7. 审核清单

- [ ] 是否遵循 `useXxx` 命名与目录规范  
- [ ] 是否复用已有服务而非重复封装 Axios/通知  
- [ ] 是否覆盖必要测试（含错误/空数据场景）  
- [ ] 是否提供必要的类型定义与 JSDoc  
- [ ] 是否更新导出（`composables/index.ts`）与文档

---
本指南若需更新，请同步 `docs/development/service-layer-implementation-plan.md` 中的任务状态。*** End Patch
