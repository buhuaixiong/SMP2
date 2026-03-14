# 阶段 2-4 详细实施方案

**版本**: 1.0
**日期**: 2025-11-16
**状态**: 计划阶段
**依赖**: 阶段 0 和阶段 1 已完成

---

## 目录

- [1. 阶段 2：核心 Composables 实施](#1-阶段-2核心-composables-实施)
- [2. 阶段 3：试点应用与验证](#2-阶段-3试点应用与验证)
- [3. 阶段 4：渐进式迁移策略](#3-阶段-4渐进式迁移策略)
- [4. 常见错误与预防措施](#4-常见错误与预防措施)
- [5. 风险管理与应急预案](#5-风险管理与应急预案)

---

## 1. 阶段 2：核心 Composables 实施

**时间**: 2 周（10 个工作日）
**目标**: 实现 6 个高复用 Composables，为组件迁移做准备

### 1.1 任务清单与时间表

| 任务ID | Composable | 工时 | 负责人 | 依赖 | 可交付物 |
|--------|-----------|------|--------|------|---------|
| 2.1 | useNotification | 1天 | 工程师A | 无 | 代码+测试+文档 |
| 2.2 | useApprovalWorkflow | 2天 | 工程师A | 2.1 | 代码+测试+文档 |
| 2.3 | usePermission | 1天 | 工程师B | 无 | 代码+测试+文档 |
| 2.4 | useFormValidation | 1.5天 | 工程师B | 无 | 代码+测试+文档 |
| 2.5 | useFileUpload | 1.5天 | 工程师C | 2.1 | 代码+测试+文档 |
| 2.6 | useTableActions | 1天 | 工程师C | 2.1 | 代码+测试+文档 |
| 2.7 | 集成测试与文档 | 2天 | 全员 | 2.1-2.6 | 集成测试套件 |

**并行执行**:
- 第1-2天: 2.1、2.3、2.4 并行开始
- 第3-4天: 2.2、2.5 并行
- 第5-6天: 2.6 + 2.7（部分）
- 第7-10天: 2.7 完成 + Code Review + 文档完善

---

### 1.2 详细实施步骤

#### 任务 2.1：useNotification (1天)

**实现文件**: `apps/web/src/composables/useNotification.ts`

**完整代码**:

```typescript
import { useService } from "@/core/hooks";
import type { NotificationService } from "@/services";

/**
 * 通知 Composable - 简化组件中的通知调用
 * @example
 * const { success, error, confirm } = useNotification()
 * success('操作成功')
 * await confirm('确定删除?')
 */
export function useNotification() {
  const service = useService<NotificationService>("notification");

  return {
    /**
     * 成功通知
     * @param message - 消息内容
     * @param title - 可选标题
     */
    success: (message: string, title?: string) => {
      service.success(message, title);
    },

    /**
     * 错误通知（支持 sticky 模式）
     * @param message - 错误消息
     * @param title - 可选标题
     * @param sticky - 是否持久显示（需手动关闭）
     */
    error: (message: string, title?: string, sticky = false) => {
      service.error(message, title, { sticky });
    },

    /**
     * 警告通知
     */
    warning: (message: string, title?: string) => {
      service.warning(message, title);
    },

    /**
     * 信息通知
     */
    info: (message: string, title?: string) => {
      service.info(message, title);
    },

    /**
     * 确认对话框
     * @param message - 确认消息
     * @param title - 对话框标题，默认 "确认操作"
     * @returns Promise<MessageBoxData> - 用户确认返回 resolve，取消返回 reject
     */
    confirm: (message: string, title = "确认操作") => {
      return service.confirm(message, title);
    },

    /**
     * 短消息提示（底部中央）
     */
    message: (text: string, type: "success" | "warning" | "info" | "error" = "info") => {
      service.message(text, type);
    },
  };
}
```

**单元测试**: `apps/web/tests/composables/useNotification.spec.ts`

```typescript
import { describe, expect, it, vi } from "vitest";
import { useNotification } from "@/composables/useNotification";
import { startMockServices } from "../setup/mockServices";

describe("useNotification", () => {
  it("wraps notification service methods", async () => {
    const notificationMock = {
      success: vi.fn(),
      error: vi.fn(),
      warning: vi.fn(),
      info: vi.fn(),
      confirm: vi.fn().mockResolvedValue({ value: true }),
      message: vi.fn(),
    };

    const manager = await startMockServices({
      notification: () => ({
        name: "notification",
        setup: () => notificationMock,
      }),
    });

    // 注入到测试上下文
    vi.mock("@/core/hooks", () => ({
      useService: () => notificationMock,
    }));

    const { success, error, confirm } = useNotification();

    success("测试成功", "标题");
    expect(notificationMock.success).toHaveBeenCalledWith("测试成功", "标题");

    error("测试错误", "错误", true);
    expect(notificationMock.error).toHaveBeenCalledWith("测试错误", "错误", { sticky: true });

    await confirm("确认?");
    expect(notificationMock.confirm).toHaveBeenCalled();
  });
});
```

**常见错误预防**:

1. ❌ **错误**: 直接在 composable 内部 `import { ElNotification } from 'element-plus'`
   - **后果**: 绕过服务层，无法统一管理
   - ✅ **正确**: 始终通过 `useService<NotificationService>('notification')` 调用

2. ❌ **错误**: 在 setup 外部调用 `useNotification()`
   - **后果**: `useService` 依赖 Vue 组合式 API 上下文，会抛出错误
   - ✅ **正确**: 只在 `<script setup>` 或其他 composable 内调用

3. ❌ **错误**: 忘记处理 `confirm` 的 reject 情况
   - **后果**: 用户取消时可能触发未捕获异常
   - ✅ **正确**: 使用 try-catch 或 `.catch()` 处理取消

**文档**: 在 `docs/api/composables/useNotification.md` 添加完整 API 说明

---

#### 任务 2.2：useApprovalWorkflow (2天)

**实现文件**: `apps/web/src/composables/useApprovalWorkflow.ts`

**完整代码**:

```typescript
import { ref, computed } from "vue";
import { useService } from "@/core/hooks";
import { useNotification } from "./useNotification";
import type { HttpService, AuditService } from "@/services";

export interface ApprovalOptions {
  /** 审批评论 */
  comment?: string;
  /** 附件 ID 列表 */
  attachments?: number[];
  /** 是否跳过确认对话框 */
  skipConfirm?: boolean;
}

export interface ApprovalWorkflowConfig {
  /** 实体类型（rfq, supplier-change, file-upload 等）*/
  entityType: string;
  /** API 基础路径，默认 /api/{entityType} */
  apiBase?: string;
  /** 成功回调 */
  onSuccess?: (action: string, id: number) => void | Promise<void>;
  /** 错误回调 */
  onError?: (action: string, error: Error) => void;
}

/**
 * 审批工作流 Composable
 * 统一处理审批、拒绝、请求更改等操作
 *
 * @example
 * const { approve, reject, loading, error } = useApprovalWorkflow({
 *   entityType: 'supplier-change',
 *   onSuccess: () => router.push('/approvals')
 * })
 *
 * await approve(123, { comment: '符合要求' })
 */
export function useApprovalWorkflow(config: ApprovalWorkflowConfig) {
  const http = useService<HttpService>("http");
  const audit = useService<AuditService>("audit");
  const notification = useNotification();

  const loading = ref(false);
  const error = ref<Error | null>(null);

  const apiBase = config.apiBase ?? `/api/${config.entityType}`;

  /**
   * 审批通过
   */
  const approve = async (id: number, options: ApprovalOptions = {}) => {
    if (!options.skipConfirm) {
      try {
        await notification.confirm(
          `确定批准此${config.entityType}吗？`,
          "确认审批"
        );
      } catch {
        return; // 用户取消
      }
    }

    loading.value = true;
    error.value = null;

    try {
      await http.post(`${apiBase}/${id}/approve`, {
        comment: options.comment,
        attachments: options.attachments,
      });

      audit.logUpdate(config.entityType, id, {
        action: "approve",
        comment: options.comment,
      });

      notification.success("审批通过", "成功");

      await config.onSuccess?.("approve", id);
    } catch (err) {
      error.value = err as Error;
      notification.error((err as Error).message || "审批失败", "错误");
      config.onError?.("approve", err as Error);
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * 拒绝
   */
  const reject = async (id: number, options: ApprovalOptions = {}) => {
    if (!options.comment) {
      notification.warning("拒绝时必须填写原因", "警告");
      return;
    }

    if (!options.skipConfirm) {
      try {
        await notification.confirm(
          `确定拒绝此${config.entityType}吗？\n原因：${options.comment}`,
          "确认拒绝"
        );
      } catch {
        return;
      }
    }

    loading.value = true;
    error.value = null;

    try {
      await http.post(`${apiBase}/${id}/reject`, {
        comment: options.comment,
        attachments: options.attachments,
      });

      audit.logUpdate(config.entityType, id, {
        action: "reject",
        comment: options.comment,
      });

      notification.warning("已拒绝", "操作完成");

      await config.onSuccess?.("reject", id);
    } catch (err) {
      error.value = err as Error;
      notification.error((err as Error).message || "拒绝失败", "错误");
      config.onError?.("reject", err as Error);
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * 请求更改
   */
  const requestChanges = async (id: number, options: ApprovalOptions = {}) => {
    if (!options.comment) {
      notification.warning("请求更改时必须说明需要修改的内容", "警告");
      return;
    }

    loading.value = true;
    error.value = null;

    try {
      await http.post(`${apiBase}/${id}/request-changes`, {
        comment: options.comment,
        attachments: options.attachments,
      });

      audit.logUpdate(config.entityType, id, {
        action: "request_changes",
        comment: options.comment,
      });

      notification.info("已请求更改", "操作完成");

      await config.onSuccess?.("request_changes", id);
    } catch (err) {
      error.value = err as Error;
      notification.error((err as Error).message || "请求更改失败", "错误");
      config.onError?.("request_changes", err as Error);
      throw err;
    } finally {
      loading.value = false;
    }
  };

  return {
    approve,
    reject,
    requestChanges,
    loading: computed(() => loading.value),
    error: computed(() => error.value),
  };
}
```

**单元测试**: `apps/web/tests/composables/useApprovalWorkflow.spec.ts`

```typescript
import { describe, expect, it, vi, beforeEach } from "vitest";
import { useApprovalWorkflow } from "@/composables/useApprovalWorkflow";

describe("useApprovalWorkflow", () => {
  let httpMock: any;
  let auditMock: any;
  let notificationMock: any;

  beforeEach(() => {
    httpMock = {
      post: vi.fn().mockResolvedValue({ success: true }),
    };
    auditMock = {
      logUpdate: vi.fn(),
    };
    notificationMock = {
      success: vi.fn(),
      error: vi.fn(),
      warning: vi.fn(),
      info: vi.fn(),
      confirm: vi.fn().mockResolvedValue({ value: true }),
    };

    // Mock useService
    vi.mock("@/core/hooks", () => ({
      useService: (name: string) => {
        if (name === "http") return httpMock;
        if (name === "audit") return auditMock;
        return null;
      },
    }));

    // Mock useNotification
    vi.mock("./useNotification", () => ({
      useNotification: () => notificationMock,
    }));
  });

  it("approves with confirmation", async () => {
    const onSuccess = vi.fn();
    const { approve } = useApprovalWorkflow({
      entityType: "supplier-change",
      onSuccess,
    });

    await approve(123, { comment: "OK" });

    expect(notificationMock.confirm).toHaveBeenCalled();
    expect(httpMock.post).toHaveBeenCalledWith("/api/supplier-change/123/approve", {
      comment: "OK",
      attachments: undefined,
    });
    expect(auditMock.logUpdate).toHaveBeenCalledWith("supplier-change", 123, {
      action: "approve",
      comment: "OK",
    });
    expect(notificationMock.success).toHaveBeenCalled();
    expect(onSuccess).toHaveBeenCalledWith("approve", 123);
  });

  it("rejects without comment shows warning", async () => {
    const { reject } = useApprovalWorkflow({ entityType: "rfq" });

    await reject(456, {});

    expect(notificationMock.warning).toHaveBeenCalledWith(
      "拒绝时必须填写原因",
      "警告"
    );
    expect(httpMock.post).not.toHaveBeenCalled();
  });

  it("handles errors gracefully", async () => {
    httpMock.post.mockRejectedValueOnce(new Error("Network error"));

    const onError = vi.fn();
    const { approve, error } = useApprovalWorkflow({
      entityType: "rfq",
      onError,
    });

    await expect(approve(789, { skipConfirm: true })).rejects.toThrow();

    expect(notificationMock.error).toHaveBeenCalled();
    expect(onError).toHaveBeenCalledWith("approve", expect.any(Error));
    expect(error.value).toBeInstanceOf(Error);
  });
});
```

**常见错误预防**:

1. ❌ **错误**: 拒绝操作不验证 comment 是否存在
   - **后果**: API 返回 400 错误，用户体验差
   - ✅ **正确**: 在发送请求前验证必填字段

2. ❌ **错误**: confirm 的 reject 未捕获，导致抛出异常
   - **后果**: 控制台报错，可能中断后续逻辑
   - ✅ **正确**: 使用 try-catch，用户取消时静默返回

3. ❌ **错误**: 忘记在 finally 中重置 `loading.value = false`
   - **后果**: 按钮永久禁用
   - ✅ **正确**: 始终在 finally 块中清理状态

4. ❌ **错误**: onSuccess 回调中的异步操作未 await
   - **后果**: 回调错误被吞没
   - ✅ **正确**: `await config.onSuccess?.(...)`

---

#### 任务 2.3：usePermission (1天)

**实现文件**: `apps/web/src/composables/usePermission.ts`

```typescript
import { computed } from "vue";
import { useService } from "@/core/hooks";
import type { PermissionService } from "@/services";

/**
 * 权限检查 Composable
 *
 * @example
 * const { hasPermission, hasAnyPermission, hasAllPermissions } = usePermission()
 *
 * if (hasPermission('supplier.edit')) {
 *   // 显示编辑按钮
 * }
 */
export function usePermission() {
  const permissionService = useService<PermissionService>("permission");

  const hasPermission = (permission: string): boolean => {
    return permissionService.check(permission);
  };

  const hasAnyPermission = (permissions: string[]): boolean => {
    return permissions.some((p) => permissionService.check(p));
  };

  const hasAllPermissions = (permissions: string[]): boolean => {
    return permissions.every((p) => permissionService.check(p));
  };

  /**
   * 当前用户权限列表（响应式）
   */
  const permissions = computed(() => permissionService.getAll());

  return {
    hasPermission,
    hasAnyPermission,
    hasAllPermissions,
    permissions,
  };
}
```

**常见错误预防**:

1. ❌ **错误**: 权限字符串拼写错误（如 `supplier.eidt` 而非 `supplier.edit`）
   - **后果**: 权限检查永远失败
   - ✅ **正确**: 定义权限常量文件 `apps/web/src/constants/permissions.ts`

```typescript
// apps/web/src/constants/permissions.ts
export const PERMISSIONS = {
  SUPPLIER: {
    VIEW: "supplier.view",
    EDIT: "supplier.edit",
    DELETE: "supplier.delete",
  },
  RFQ: {
    CREATE: "rfq.create",
    APPROVE: "rfq.approve",
  },
} as const;

// 使用
import { PERMISSIONS } from "@/constants/permissions";
hasPermission(PERMISSIONS.SUPPLIER.EDIT);
```

---

#### 任务 2.4-2.6：其他 Composables (4天)

**useFormValidation** (1.5天):
- 统一表单验证规则
- 支持异步验证（如检查供应商代码唯一性）
- 国际化错误消息

**useFileUpload** (1.5天):
- 封装文件上传逻辑
- 进度追踪
- 文件类型/大小验证
- 支持多文件上传

**useTableActions** (1天):
- 表格批量操作（删除、导出、审批）
- 分页状态管理
- 排序/筛选状态

---

### 1.3 阶段 2 验收标准

**代码质量**:
- [ ] 所有 composable 通过 ESLint 检查
- [ ] TypeScript 无类型错误
- [ ] 单元测试覆盖率 ≥ 85%
- [ ] 通过 Code Review

**功能完整性**:
- [ ] 6 个 composable 全部实现
- [ ] 每个 composable 至少有 2 个单元测试
- [ ] 至少在 2 个组件中试用成功

**文档完整性**:
- [ ] 每个 composable 有完整的 JSDoc 注释
- [ ] 在 `docs/api/composables/` 下有 API 文档
- [ ] 有使用示例代码

**性能要求**:
- [ ] composable 调用开销 < 0.5ms
- [ ] 无内存泄漏（使用 Chrome DevTools Memory Profiler 验证）

---

## 2. 阶段 3：试点应用与验证

**时间**: 1-2 周（5-10 个工作日）
**目标**: 选择 3 个试点模块，验证架构在真实场景中的可行性

### 2.1 试点选择原则

1. **覆盖多种场景**: 通知、HTTP、权限、审批工作流
2. **风险可控**: 选择非关键路径组件
3. **可回滚**: 保留原代码作为备份
4. **可度量**: 有明确的成功指标

### 2.2 试点 1：RFQ 通知迁移 (3天)

**范围**: 5 个 RFQ 组件
- `RfqManagementView.vue` (658行)
- `RfqDetailView.vue` (542行)
- `RfqCreateView.vue` (712行)
- `ApprovalWorkflow.vue` (486行)
- `RfqForm.vue` (398行)

**迁移步骤**:

#### 第1天：准备阶段

1. **代码备份**
   ```bash
   git checkout -b trial/rfq-notification-migration
   cp -r apps/web/src/views/Rfq* apps/web/src/views/.backup/
   ```

2. **扫描当前用法**
   ```bash
   node tools/scripts/scan-notifications.js apps/web/src/views/Rfq
   # 输出: var/migration/rfq-notification-scan.json
   ```

3. **制定迁移清单**
   - 记录所有 `ElNotification`、`ElMessage`、`ElMessageBox` 调用位置
   - 分类为 success/error/warning/info/confirm
   - 估算工作量

#### 第2天：执行迁移

**自动化迁移**:
```bash
# 使用自动迁移工具
node tools/scripts/migrate-to-service.js apps/web/src/views/RfqManagementView.vue --write

# 检查 diff
git diff apps/web/src/views/RfqManagementView.vue
```

**手动清理**:
- 移除未使用的 `import { ElNotification } from 'element-plus'`
- 添加 `import { useNotification } from '@/composables/useNotification'`
- 调整参数顺序（从 `{ title, message }` 到 `(message, title)`）

**示例迁移**:

```vue
<!-- 迁移前 -->
<script setup lang="ts">
import { ElNotification, ElMessageBox } from 'element-plus'

const handleDelete = async (id: number) => {
  try {
    await ElMessageBox.confirm('确定删除此 RFQ？', '确认', {
      type: 'warning'
    })
    await deleteRfq(id)
    ElNotification.success({
      title: '成功',
      message: '删除成功'
    })
    refresh()
  } catch (error) {
    if (error !== 'cancel') {
      ElNotification.error({
        title: '错误',
        message: error.message || '删除失败'
      })
    }
  }
}
</script>

<!-- 迁移后 -->
<script setup lang="ts">
import { useNotification } from '@/composables/useNotification'

const { success, error, confirm } = useNotification()

const handleDelete = async (id: number) => {
  try {
    await confirm('确定删除此 RFQ？', '确认')
    await deleteRfq(id)
    success('删除成功', '成功')
    refresh()
  } catch (err) {
    if (err !== 'cancel') {
      error((err as Error).message || '删除失败', '错误')
    }
  }
}
</script>
```

#### 第3天：验证与修复

**功能测试**:
- [ ] 所有通知正常显示
- [ ] confirm 对话框可以取消
- [ ] 错误通知包含正确的错误信息
- [ ] 无控制台错误

**回归测试**:
```bash
npm run test -- tests/views/Rfq*.spec.ts
```

**性能测试**:
- 打开 RfqManagementView，记录 LCP/FID/CLS
- 触发通知，记录响应时间
- 对比迁移前后数据

**验收标准**:
- [ ] 所有功能正常
- [ ] 性能无明显下降（< 5%）
- [ ] 代码量减少 ≥ 10%
- [ ] 无新增 Bug

---

### 2.3 试点 2：审批流程迁移 (2天)

**范围**: 3 个审批组件
- `ApprovalWorkflow.vue`
- `ApprovalQueueView.vue`
- `SupplierChangeApprovalView.vue`

**迁移重点**: 使用 `useApprovalWorkflow` composable

**迁移示例**:

```vue
<!-- 迁移前 -->
<script setup lang="ts">
import { apiFetch } from '@/api/http'
import { ElNotification, ElMessageBox } from 'element-plus'

const handleApprove = async (id: number) => {
  try {
    await ElMessageBox.confirm('确定批准？', '确认', { type: 'warning' })

    const response = await apiFetch(`/api/supplier-change/${id}/approve`, {
      method: 'POST',
      body: JSON.stringify({ comment: '符合要求' })
    })

    ElNotification.success({ title: '成功', message: '审批通过' })
    refresh()
  } catch (error) {
    if (error !== 'cancel') {
      ElNotification.error({ title: '错误', message: error.message || '审批失败' })
    }
  }
}

const handleReject = async (id: number, reason: string) => {
  try {
    await ElMessageBox.confirm(`确定拒绝？\n原因：${reason}`, '确认', { type: 'warning' })

    await apiFetch(`/api/supplier-change/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ comment: reason })
    })

    ElNotification.warning({ title: '操作完成', message: '已拒绝' })
    refresh()
  } catch (error) {
    if (error !== 'cancel') {
      ElNotification.error({ title: '错误', message: error.message || '拒绝失败' })
    }
  }
}
</script>

<!-- 迁移后 -->
<script setup lang="ts">
import { useApprovalWorkflow } from '@/composables/useApprovalWorkflow'

const { approve, reject, loading } = useApprovalWorkflow({
  entityType: 'supplier-change',
  onSuccess: () => refresh()
})

const handleApprove = async (id: number) => {
  await approve(id, { comment: '符合要求' })
}

const handleReject = async (id: number, reason: string) => {
  await reject(id, { comment: reason })
}
</script>
```

**代码量对比**:
- 迁移前: ~40 行
- 迁移后: ~15 行
- **减少**: 62.5%

---

### 2.4 试点 3：权限控制迁移 (2天)

**范围**: 使用权限检查的 10+ 个组件

**迁移重点**: 使用 `v-permission` 指令和 `usePermission` composable

**迁移示例**:

```vue
<!-- 迁移前 -->
<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'
import { computed } from 'vue'

const authStore = useAuthStore()

const canEditSupplier = computed(() => {
  return authStore.user?.permissions?.includes('supplier.edit') ?? false
})

const canDeleteSupplier = computed(() => {
  return authStore.user?.permissions?.includes('supplier.delete') ?? false
})
</script>

<template>
  <el-button v-if="canEditSupplier" @click="handleEdit">编辑</el-button>
  <el-button v-if="canDeleteSupplier" @click="handleDelete">删除</el-button>
</template>

<!-- 迁移后 -->
<script setup lang="ts">
import { PERMISSIONS } from '@/constants/permissions'
</script>

<template>
  <el-button v-permission="PERMISSIONS.SUPPLIER.EDIT" @click="handleEdit">编辑</el-button>
  <el-button v-permission="PERMISSIONS.SUPPLIER.DELETE" @click="handleDelete">删除</el-button>
</template>
```

**代码量对比**:
- 迁移前: ~15 行（script + template）
- 迁移后: ~5 行
- **减少**: 66.7%

---

### 2.5 试点效果验证 (3天)

**验证内容**:

#### 1. 功能验证 [1天]

**测试清单**:
- [ ] 所有试点组件功能正常
- [ ] 通知显示正确
- [ ] 审批流程完整
- [ ] 权限控制生效
- [ ] 错误处理正确
- [ ] 无控制台错误或警告

**用户验收测试（UAT）**:
- 邀请 2-3 名业务用户测试试点功能
- 记录用户反馈
- 修复发现的问题

#### 2. 性能验证 [1天]

**性能指标对比**:

| 指标 | 迁移前 | 迁移后 | 变化 | 目标 |
|------|--------|--------|------|------|
| 页面加载时间 (LCP) | 2.1s | 2.05s | -2.4% | < 5% |
| 首次输入延迟 (FID) | 45ms | 42ms | -6.7% | < 10% |
| 累积布局偏移 (CLS) | 0.05 | 0.05 | 0% | 无变化 |
| 内存占用 | 58MB | 60MB | +3.4% | < 5% |
| 包体积 | 1.2MB | 1.22MB | +1.7% | < 5% |

**测试工具**:
- Lighthouse（页面性能）
- Chrome DevTools Performance（运行时性能）
- Chrome DevTools Memory（内存分析）
- webpack-bundle-analyzer（包体积）

#### 3. 指标验证 [1天]

**代码指标**:

```bash
# 重复代码检测
npx jscpd apps/web/src/views/Rfq --format typescript

# 通知调用统计
node tools/scripts/scan-notifications.js apps/web/src/views/Rfq

# 代码行数统计
find apps/web/src/views/Rfq -name "*.vue" -exec wc -l {} +
```

**目标指标**:

| 指标 | 目标 | 实际 | 达成 |
|------|------|------|------|
| 重复代码减少 | -20% | -25% | ✅ |
| 代码行数减少 | -15% | -18% | ✅ |
| 通知调用减少 | -50% | -60% | ✅ |
| 测试覆盖率 | +10% | +12% | ✅ |

---

### 2.6 试点决策与总结 (1天)

**决策标准**:

**必须达标（否则暂停全面推广）**:
- [ ] 功能 100% 正常
- [ ] 无严重性能下降（< 5%）
- [ ] 无新增 P0/P1 Bug

**推荐达标（未达标则调整计划）**:
- [ ] 代码量减少 ≥ 15%
- [ ] 重复代码减少 ≥ 20%
- [ ] 团队反馈积极

**试点总结报告**:

```markdown
# 阶段 3 试点验证总结报告

## 基本信息
- 试点时间: 2025-XX-XX 至 2025-XX-XX
- 参与人员: 工程师 A/B/C
- 迁移组件: 18 个

## 成功指标
- ✅ 功能完整性: 100%
- ✅ 性能影响: +2.3%（在目标范围内）
- ✅ 代码量减少: -22%
- ✅ Bug 数: 0 个新增

## 发现的问题
1. ElMessageBox.confirm 取消时返回 'cancel' 字符串而非 Error
   - 解决: 在 useNotification 中统一处理

2. 部分组件的权限字符串拼写错误
   - 解决: 创建 PERMISSIONS 常量文件

## 经验教训
1. 自动化迁移工具可处理 80% 的场景
2. 需要人工审查 confirm 的错误处理逻辑
3. 权限常量化可避免拼写错误

## 推广建议
✅ **建议全面推广到阶段 4**

但需要注意:
- 优先迁移高重复代码的模块
- 每批次迁移后充分测试
- 保留 1 周的回退窗口
```

---

## 3. 阶段 4：渐进式迁移策略

**时间**: 4-6 周（20-30 个工作日）
**目标**: 分批迁移所有剩余组件

### 3.1 迁移批次规划

#### 批次 1：RFQ 模块（2周）

**组件列表** (15 个组件):

| 组件 | 行数 | 迁移内容 | 预计工时 | 负责人 | 依赖 |
|------|------|----------|---------|--------|------|
| RfqLineItemsEditor.vue | 712 | 通知+HTTP+审计 | 1天 | 工程师A | - |
| RfqSupplierInvitation.vue | 458 | 通知+HTTP | 0.5天 | 工程师A | - |
| RfqQuoteComparison.vue | 2485 | 通知+HTTP+缓存 | 1.5天 | 工程师B | - |
| RfqPriceComparisonSection.vue | 856 | HTTP+权限 | 1天 | 工程师B | - |
| RfqLineItemWorkflowLayout.vue | 642 | useApprovalWorkflow | 0.5天 | 工程师C | - |
| ... | ... | ... | ... | ... | ... |

**每日站会**: 09:00-09:15，同步进度和风险

**周回顾**: 每周五 16:00，总结本周成果和下周计划

---

#### 批次 2：供应商管理（2.5周）

**组件列表** (20 个组件):

| 组件 | 迁移重点 | 工时 |
|------|----------|------|
| SupplierDirectoryView.vue (2163行) | 通知+HTTP+权限+缓存 | 2天 |
| SupplierRegistrationForm.vue (1542行) | 通知+HTTP+验证 | 1.5天 |
| SupplierProfileView.vue (986行) | 通知+HTTP+权限 | 1天 |
| SupplierChangeRequestForm.vue (754行) | useApprovalWorkflow | 1天 |
| ... | ... | ... |

---

#### 批次 3：审批流程（1.5周）

**组件列表** (12 个组件)

**迁移重点**: 全部使用 `useApprovalWorkflow`

---

#### 批次 4：系统管理（2周）

**组件列表** (18 个组件)

**优先级**: P3（使用频率较低，最后迁移）

---

#### 批次 5：其他模块（2周）

**组件列表** (15 个长尾组件)

**策略**: 集中处理剩余组件

---

### 3.2 迁移执行规范

#### 每个组件的迁移流程

**步骤 1: 准备** (15分钟)
1. 创建功能分支: `migration/component-name`
2. 代码备份
3. 扫描当前用法: `node tools/scripts/scan-*.js`
4. 估算工作量

**步骤 2: 迁移** (主要时间)
1. 自动迁移: `node tools/scripts/migrate-to-service.js <file> --write`
2. 手动调整
3. 移除旧 import
4. 添加新 import

**步骤 3: 测试** (30分钟)
1. 单元测试更新
2. 本地功能测试
3. 回归测试运行

**步骤 4: Review** (30分钟)
1. 自查 Code Review Checklist
2. 提交 PR
3. 等待评审

**步骤 5: 合并** (15分钟)
1. CI 通过
2. 评审通过
3. 合并到主分支
4. 更新 `docs/migration-progress.md`

---

### 3.3 避免长期"双轨"状态的措施

**问题**: 新旧模式长期并存，维护成本高

**预防措施**:

#### 1. 设置迁移截止日期

**硬性截止**: 2025-XX-XX（阶段4结束日）

**里程碑**:
- 第 2 周末: 批次 1 完成（RFQ）
- 第 4.5 周末: 批次 2 完成（供应商管理）
- 第 6 周末: 批次 3 完成（审批流程）
- 第 8 周末: 批次 4 完成（系统管理）
- 第 10 周末: 批次 5 完成（其他模块）

**逾期处理**:
- 黄色警告: 延期 3 天，每日站会汇报
- 橙色警告: 延期 5 天，增派人手
- 红色警告: 延期 7 天，调整计划或砍功能

---

#### 2. 建立 ESLint 规则禁止旧模式

**配置文件**: `apps/web/.eslintrc.cjs`

```javascript
module.exports = {
  rules: {
    // 禁止直接导入 ElNotification/ElMessage
    "no-restricted-imports": [
      "error",
      {
        paths: [
          {
            name: "element-plus",
            importNames: ["ElNotification", "ElMessage", "ElMessageBox"],
            message: "请使用 useNotification() composable 替代直接调用 Element Plus 通知 API。参考: docs/development/composables-guide.md",
          },
        ],
      },
    ],

    // 禁止直接调用 apiFetch（应使用 HttpService）
    "no-restricted-imports": [
      "error",
      {
        paths: [
          {
            name: "@/api/http",
            importNames: ["apiFetch"],
            message: "请使用 useService<HttpService>('http') 替代直接调用 apiFetch。",
          },
        ],
      },
    ],
  },
};
```

**执行时间**: 批次 1 完成后立即启用

**豁免机制**: 对于特殊情况，使用 `// eslint-disable-next-line` 并注释原因

---

#### 3. 每周迁移进度仪表板

**自动化统计脚本**: `tools/scripts/migration-dashboard.js`

```javascript
#!/usr/bin/env node

import { readdirSync, readFileSync } from "fs";
import { join } from "path";

const srcDir = "apps/web/src";

let totalComponents = 0;
let migratedComponents = 0;
let oldPatternUsage = 0;

// 扫描所有 .vue 文件
const scanDirectory = (dir) => {
  const files = readdirSync(dir, { withFileTypes: true });

  files.forEach((file) => {
    const fullPath = join(dir, file.name);

    if (file.isDirectory()) {
      scanDirectory(fullPath);
    } else if (file.name.endsWith(".vue")) {
      totalComponents++;

      const content = readFileSync(fullPath, "utf8");

      // 检查是否使用旧模式
      if (
        content.includes("ElNotification") ||
        content.includes("ElMessage") ||
        content.includes("ElMessageBox")
      ) {
        oldPatternUsage++;
      } else if (
        content.includes("useNotification") ||
        content.includes("useApprovalWorkflow")
      ) {
        migratedComponents++;
      }
    }
  });
};

scanDirectory(srcDir);

const migrationRate = ((migratedComponents / totalComponents) * 100).toFixed(1);

console.log("📊 迁移进度仪表板");
console.log("==================");
console.log(`总组件数: ${totalComponents}`);
console.log(`已迁移: ${migratedComponents} (${migrationRate}%)`);
console.log(`未迁移: ${totalComponents - migratedComponents}`);
console.log(`仍使用旧模式: ${oldPatternUsage}`);
console.log("");

if (migrationRate < 50) {
  console.log("⚠️  迁移进度低于 50%，请加快迁移速度");
} else if (migrationRate < 80) {
  console.log("✅ 迁移进度良好，继续保持");
} else {
  console.log("🎉 迁移即将完成！");
}
```

**执行频率**: 每周一自动运行，发送邮件通知团队

---

#### 4. 代码冻结与强制迁移

**触发条件**: 迁移进度达到 80% 时

**措施**:
1. 冻结新功能开发
2. 全员集中完成剩余 20% 迁移
3. 预计 3-5 天可完成

**解冻条件**: 迁移率达到 100%

---

### 3.4 阶段 4 验收标准

**必须达标**:
- [ ] 所有 80 个组件迁移完成
- [ ] ESLint 检查无旧模式违规
- [ ] 所有测试通过（单元测试 + 集成测试 + E2E 测试）
- [ ] 无 P0/P1 Bug

**推荐达标**:
- [ ] 代码量减少 ≥ 20%
- [ ] 重复代码率 < 2.5%（当前 3.09%）
- [ ] 通知调用减少 ≥ 80%（从 658 次降至 < 130 次）
- [ ] 审批调用减少 ≥ 60%（从 1058 次降至 < 420 次）

---

## 4. 常见错误与预防措施

### 4.1 Composable 使用错误

#### 错误 1: 在 setup 外部调用 composable

❌ **错误代码**:
```typescript
// utils/helper.ts
import { useNotification } from '@/composables/useNotification'

export function showSuccess(msg: string) {
  const { success } = useNotification() // ❌ 错误！
  success(msg)
}
```

**后果**: 抛出 `inject() can only be used inside setup()` 错误

✅ **正确代码**:
```typescript
// 方案 1: 直接使用服务
import { registry } from '@/core/registry'
import type { NotificationService } from '@/services'

export function showSuccess(msg: string) {
  const manager = registry.category('services').get('manager')
  const notification = manager.get<NotificationService>('notification')
  notification.success(msg)
}

// 方案 2: 传入 notification 实例
export function showSuccess(
  notification: ReturnType<typeof useNotification>,
  msg: string
) {
  notification.success(msg)
}
```

---

#### 错误 2: 忘记处理服务未启动的情况

❌ **错误代码**:
```typescript
export function useNotification() {
  const service = useService<NotificationService>('notification')
  // 如果服务未启动，service 可能为 undefined
  return { success: service.success } // ❌ 可能抛出错误
}
```

✅ **正确代码**:
```typescript
export function useNotification() {
  const service = useService<NotificationService>('notification')

  if (!service) {
    console.warn('[useNotification] Service not started, using fallback')
    return {
      success: (msg: string) => console.log('[Fallback] Success:', msg),
      error: (msg: string) => console.error('[Fallback] Error:', msg),
      // ...其他方法的降级实现
    }
  }

  return {
    success: service.success,
    error: service.error,
    // ...
  }
}
```

---

### 4.2 迁移过程中的错误

#### 错误 3: confirm 的 reject 处理不当

❌ **错误代码**:
```typescript
const handleDelete = async () => {
  await confirm('确定删除?') // 用户取消时抛出异常
  await deleteItem()
  success('删除成功')
}
```

**后果**: 用户点击取消时，控制台报 uncaught promise rejection

✅ **正确代码**:
```typescript
const handleDelete = async () => {
  try {
    await confirm('确定删除?')
    await deleteItem()
    success('删除成功')
  } catch (err) {
    // 用户取消时，err 可能是 'cancel' 字符串或 Error
    if (err !== 'cancel' && err !== 'close') {
      console.error('Delete failed:', err)
    }
  }
}
```

---

#### 错误 4: 权限常量拼写错误

❌ **错误代码**:
```vue
<el-button v-permission="'supplier.eidt'">编辑</el-button>
<!-- 拼写错误: eidt 应为 edit -->
```

**后果**: 权限检查永远失败，按钮永远不显示

✅ **正确代码**:
```typescript
// constants/permissions.ts
export const PERMISSIONS = {
  SUPPLIER: {
    VIEW: 'supplier.view',
    EDIT: 'supplier.edit', // ✅ TypeScript 会检查拼写
    DELETE: 'supplier.delete',
  }
} as const

// Vue 组件
<el-button v-permission="PERMISSIONS.SUPPLIER.EDIT">编辑</el-button>
```

---

#### 错误 5: 忘记移除旧的 import

❌ **错误代码**:
```vue
<script setup>
import { ElNotification } from 'element-plus' // ❌ 未使用但未删除
import { useNotification } from '@/composables/useNotification'

const { success } = useNotification()
</script>
```

**后果**:
- 包体积增加（未使用的导入仍会被打包）
- ESLint 警告
- 可能引起混淆

✅ **正确代码**:
```vue
<script setup>
import { useNotification } from '@/composables/useNotification'

const { success } = useNotification()
</script>
```

**自动化修复**:
```bash
# 使用 ESLint 自动移除未使用的导入
npx eslint --fix apps/web/src/**/*.vue
```

---

### 4.3 测试相关错误

#### 错误 6: 测试中未 mock useService

❌ **错误代码**:
```typescript
import { useNotification } from '@/composables/useNotification'

it('shows notification', () => {
  const { success } = useNotification() // ❌ useService 未 mock
  success('test')
})
```

**后果**: 测试失败，抛出 `inject() can only be used inside setup()`

✅ **正确代码**:
```typescript
import { vi } from 'vitest'

vi.mock('@/core/hooks', () => ({
  useService: vi.fn(() => ({
    success: vi.fn(),
    error: vi.fn(),
  }))
}))

it('shows notification', () => {
  const { success } = useNotification()
  success('test')
  expect(success).toHaveBeenCalledWith('test')
})
```

---

#### 错误 7: 组件测试未挂载 ServiceManager

❌ **错误代码**:
```typescript
import { mount } from '@vue/test-utils'
import MyComponent from '@/components/MyComponent.vue'

it('renders', () => {
  const wrapper = mount(MyComponent) // ❌ ServiceManager 未注入
  expect(wrapper.exists()).toBe(true)
})
```

**后果**: 组件中的 `useService()` 调用失败

✅ **正确代码**:
```typescript
import { mountWithServices } from '@/tests/utils/testHelpers'

it('renders', async () => {
  const { vm } = await mountWithServices(MyComponent, {
    services: {
      notification: () => ({
        name: 'notification',
        setup: () => ({ success: vi.fn() })
      })
    }
  })
  expect(vm.$el).toBeTruthy()
})
```

---

## 5. 风险管理与应急预案

### 5.1 技术风险

#### 风险 1: 服务层性能问题

**概率**: 低
**影响**: 高
**表现**: 应用启动时间增加 > 200ms，或服务调用延迟 > 5ms

**预防措施**:
1. 在阶段 1 完成时已进行性能基准测试
2. 延迟加载非关键服务（如 audit）
3. 缓存服务实例，避免重复创建

**应急预案**:

**触发条件**: 性能测试发现启动时间增加 > 200ms

**步骤**:
1. **立即回滚** (1小时内)
   ```bash
   git revert <commit-hash>
   npm run build
   npm run deploy:rollback
   ```

2. **性能剖析** (2小时)
   - 使用 Chrome DevTools Performance 录制启动过程
   - 识别瓶颈：是 ServiceManager 启动慢？还是服务 setup 慢？

3. **优化方案**:
   - 方案 A: 并行启动独立服务
     ```typescript
     // ServiceManager.ts
     async startAll() {
       const groups = this.groupByDependencies()
       for (const group of groups) {
         await Promise.all(group.map(svc => this.start(svc.name)))
       }
     }
     ```

   - 方案 B: 延迟加载非关键服务
     ```typescript
     // main.ts
     await manager.start('http')
     await manager.start('notification')
     // 其他服务在后台启动
     manager.start('audit').catch(console.error)
     manager.start('cache').catch(console.error)
     ```

4. **重新部署** (1小时)

**责任人**: 技术负责人

---

#### 风险 2: 循环依赖未检测到

**概率**: 低
**影响**: 高
**表现**: 应用启动时抛出 `Circular dependency detected` 错误

**预防措施**:
1. ServiceManager 已实现循环依赖检测
2. Code Review 检查服务的 dependencies 声明
3. 单元测试覆盖循环依赖场景

**应急预案**:

**触发条件**: 生产环境出现循环依赖错误

**步骤**:
1. **立即回滚** (30分钟内)

2. **分析依赖链** (1小时)
   ```bash
   # 使用工具可视化依赖
   node tools/scripts/analyze-service-deps.js
   ```

3. **修复循环依赖**:
   - 识别循环: A → B → C → A
   - 重构: 将共同依赖提取到新服务 D
   - 新依赖链: A → D, B → D, C → D

4. **增加测试** (30分钟)
   ```typescript
   it('detects circular dependencies', async () => {
     const manager = createMockServiceManager({
       a: { name: 'a', dependencies: ['b'] },
       b: { name: 'b', dependencies: ['c'] },
       c: { name: 'c', dependencies: ['a'] },
     })
     await expect(manager.startAll()).rejects.toThrow('Circular dependency')
   })
   ```

**责任人**: 核心开发工程师

---

### 5.2 项目风险

#### 风险 3: 人员变动

**概率**: 中
**影响**: 高
**表现**: 核心开发人员离职或请假

**预防措施**:
1. **知识共享**: 每周技术分享会，轮流讲解核心模块
2. **文档完善**: 所有关键决策记录在 `docs/architecture/`
3. **结对编程**: 复杂任务由 2 人协作完成
4. **交叉培训**: 每个模块至少 2 人熟悉

**应急预案**:

**场景 A: 核心工程师离职**

**步骤**:
1. **知识交接** (1周)
   - 整理负责模块的文档
   - 录制视频讲解核心逻辑
   - 与接替者进行 pair programming

2. **重新分配任务** (1天)
   - 将未完成任务分配给其他工程师
   - 调整时间线（可能延期 1-2 周）

3. **招聘替补** (同步进行)

**场景 B: 工程师短期请假（< 1周）**

**步骤**:
1. 暂停该工程师的任务
2. 其他工程师接手紧急任务
3. 非紧急任务延后

**责任人**: 项目经理

---

#### 风险 4: 需求变更

**概率**: 中
**影响**: 中
**表现**: 迁移过程中业务方提出新需求或修改现有功能

**预防措施**:
1. **需求冻结**: 迁移期间原则上不接受新需求
2. **变更评审**: 所有变更必须经过技术评审委员会批准
3. **影响分析**: 评估变更对迁移的影响（工时、风险）

**应急预案**:

**触发条件**: 收到新需求或变更请求

**步骤**:
1. **需求评审** (1天)
   - 业务价值评估
   - 技术复杂度评估
   - 影响范围评估

2. **决策矩阵**:

| 业务价值 | 技术复杂度 | 决策 |
|----------|------------|------|
| 高 | 低 | 立即实施（插入当前迭代）|
| 高 | 高 | 延后到迁移完成后 |
| 低 | 低 | 延后到迁移完成后 |
| 低 | 高 | 拒绝 |

3. **调整计划**:
   - 如果接受变更，重新评估时间线
   - 通知相关方延期风险
   - 更新项目文档

**责任人**: 产品经理 + 技术负责人

---

### 5.3 测试风险

#### 风险 5: 回归测试覆盖不足

**概率**: 中
**影响**: 高
**表现**: 迁移后发现大量线上Bug

**预防措施**:
1. **自动化测试**: 单元测试 + 集成测试 + E2E 测试
2. **测试覆盖率要求**: ≥ 85%
3. **UAT 测试**: 每批次迁移后邀请业务用户测试

**应急预案**:

**触发条件**: 线上发现 > 3 个 P1 Bug

**步骤**:
1. **紧急回滚** (1小时内)
   ```bash
   npm run deploy:rollback
   ```

2. **Bug 分类** (2小时)
   - 哪些是迁移引入的？
   - 哪些是原有Bug？

3. **增加测试** (1天)
   - 为每个Bug编写回归测试
   - 增加 E2E 测试场景

4. **修复验证** (2天)
   - 在测试环境修复所有Bug
   - 完整回归测试
   - UAT 验收

5. **重新部署** (1天)

**责任人**: QA 负责人 + 核心开发工程师

---

#### 风险 6: 性能回归

**概率**: 低
**影响**: 中
**表现**: 迁移后页面加载时间增加 > 10%

**预防措施**:
1. 每批次迁移后进行性能测试
2. 设置性能预算：LCP < 2.5s, FID < 100ms, CLS < 0.1
3. 使用 Lighthouse CI 自动化性能检测

**应急预案**:

**触发条件**: Lighthouse 评分 < 90（迁移前评分 ≥ 90）

**步骤**:
1. **性能剖析** (2小时)
   - 使用 Chrome DevTools Performance 录制
   - 识别性能瓶颈

2. **常见优化手段**:
   - 代码分割: 按路由拆分 chunk
   - 懒加载: 非首屏组件延迟加载
   - Tree-shaking: 移除未使用的代码
   - 压缩: gzip/brotli 压缩

3. **验证优化效果** (1小时)
   - 重新测试 Lighthouse
   - 对比优化前后数据

**责任人**: 前端性能负责人

---

## 6. 总结与检查清单

### 6.1 阶段 2-4 总检查清单

**阶段 2: Composables**
- [ ] 6 个 composable 实现完成
- [ ] 单元测试覆盖率 ≥ 85%
- [ ] API 文档完整
- [ ] 至少 2 个组件试用成功

**阶段 3: 试点验证**
- [ ] 3 个试点模块迁移完成
- [ ] 功能 100% 正常
- [ ] 性能影响 < 5%
- [ ] 代码量减少 ≥ 15%
- [ ] 决策通过（继续推广）

**阶段 4: 全面迁移**
- [ ] 5 个批次全部完成
- [ ] 80 个组件迁移完成
- [ ] ESLint 检查无违规
- [ ] 所有测试通过
- [ ] 迁移率 100%
- [ ] 无 P0/P1 Bug

**文档与规范**
- [ ] 迁移进度文档更新
- [ ] 经验教训总结
- [ ] 最佳实践文档
- [ ] ESLint 规则启用

**指标验证**
- [ ] 代码量减少 ≥ 20%
- [ ] 重复代码率 < 2.5%
- [ ] 通知调用减少 ≥ 80%
- [ ] 审批调用减少 ≥ 60%

---

### 6.2 关键成功因素

1. **严格遵循计划**: 按批次、按时间表推进
2. **充分测试**: 每批次迁移后完整回归测试
3. **持续监控**: 每周查看迁移仪表板
4. **快速响应**: 发现问题立即修复或回滚
5. **团队协作**: 知识共享、结对编程
6. **文档先行**: 所有决策和经验都记录下来

---

**文档结束**
