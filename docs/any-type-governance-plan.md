# Any 类型治理方案（分阶段实施计划）

> **版本**: v2.0（根据评审反馈更新）
> **更新日期**: 2026-02-01

---

## 修订记录（v2.0）

| 问题 | 修复方案 |
|------|----------|
| API 契约未验证 | 新增「七、API 响应基线清单」章节 |
| 迁移策略不可中断 | 新增「1.4 过渡兼容策略」使用重载签名 |
| `instanceof AxiosError` 不稳定 | 改用 `axios.isAxiosError()` |
| `stack` 可能泄露服务端细节 | 文档标注仅 debug 模式可用 |
| Lockdown 响应未纳入统一模型 | 明确优先级策略 |

---

## 一、API 响应类型建模（Phase 1）

### 1.1 核心类型定义

```typescript
// types/api.ts - 新增文件

import axios from 'axios';

// ==================== 基础响应结构 ====================

export interface ApiSuccessResponse<T> {
  success: true;
  data: T;
  message?: string;
}

export interface ApiErrorResponse {
  success: false;
  error: string;
  code: string;
  details?: Record<string, unknown>;
  /** @hidden 仅开发环境可透出，生产环境禁止显示 */
  stack?: string;
}

export type ApiResponse<T> = ApiSuccessResponse<T> | ApiErrorResponse;

// ==================== 分页响应结构 ====================

export interface PaginatedResponse<T> {
  success: true;
  data: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  message?: string;
}

// ==================== Lockdown 特殊响应 ====================

/**
 * Lockdown 模式响应（高优先级）
 * - HTTP 状态码 503
 * - 独立于常规 ApiResponse 模型
 */
export interface LockdownErrorResponse {
  error: string;
  message: string;
  lockdown: true;
  retryAfter?: number;
}

/**
 * 检测是否为 Lockdown 响应
 * - 优先级最高：在任何 ApiErrorResponse 检查之前调用
 */
export function isLockdownResponse(data: unknown): data is LockdownErrorResponse {
  return (
    typeof data === 'object' &&
    data !== null &&
    'lockdown' in data &&
    (data as LockdownErrorResponse).lockdown === true
  );
}

// ==================== 错误类型守卫 ====================

/**
 * 检测 AxiosError（使用 axios.isAxiosError 替代 instanceof）
 * - 更稳定的跨运行时检测
 */
export function isAxiosError(error: unknown): error is axios.AxiosError {
  return axios.isAxiosError(error);
}

/**
 * 检测标准 API 错误响应
 * - 必须在 isLockdownResponse 之后调用
 */
export function isApiErrorResponse(data: unknown): data is ApiErrorResponse {
  if (!data || typeof data !== 'object') return false;
  return (
    'success' in data &&
    (data as ApiErrorResponse).success === false &&
    'error' in data &&
    'code' in data
  );
}
```

### 1.2 错误处理工具

```typescript
// utils/errorHandling.ts - 新增文件

import axios from 'axios';
import type { ApiErrorResponse, LockdownErrorResponse } from '@/types/api';

/**
 * 安全提取错误信息（用户友好）
 * - 优先级：Lockdown > ApiErrorResponse > AxiosError > Error > 默认
 */
export function extractErrorMessage(error: unknown, devMode = import.meta.env.DEV): string {
  // 1. Axios 错误（最常见）
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;

    // 1.1 Lockdown 优先（最高优先级）
    if (isLockdownResponse(data)) {
      return data.message || '系统维护中，请稍后再试';
    }

    // 1.2 标准 API 错误
    if (isApiErrorResponse(data)) {
      return data.error || data.message || '请求失败';
    }

    // 1.3 网络错误降级
    return error.message || '网络错误，请检查连接';
  }

  // 2. 普通 Error
  if (error instanceof Error) {
    // 生产环境隐藏敏感信息
    if (!devMode && error.message.includes('stack')) {
      return '操作失败，请联系管理员';
    }
    return error.message;
  }

  // 3. 兜底
  return '未知错误';
}

/**
 * 提取错误码（用于程序化处理）
 * - 仅从 ApiErrorResponse 获取
 */
export function getErrorCode(error: unknown): string | undefined {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    if (isApiErrorResponse(data)) {
      return data.code;
    }
  }
  return undefined;
}

/**
 * 提取错误详情（仅开发环境使用）
 * @hidden
 */
export function getErrorDetails(error: unknown, devMode = import.meta.env.DEV): Record<string, unknown> | null {
  if (!devMode) return null;

  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    if (isApiErrorResponse(data) && data.details) {
      return data.details;
    }
  }
  return null;
}

/**
 * 判断是否为可重试错误
 */
export function isRetryableError(error: unknown): boolean {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    // 5xx 错误可重试，401/403 不可重试
    return status !== undefined && status >= 500;
  }
  return false;
}
```

### 1.3 HTTP 客户端改造（过渡兼容策略）

```typescript
// api/http.ts

import axios, { AxiosInstance, AxiosError } from 'axios';
import type { ApiResponse, PaginatedResponse, LockdownErrorResponse, isLockdownResponse } from '@/types/api';

// ==================== 现有实现保持不变 ====================

const patchUrl = (url: string) => { /* ... */ };

type ApiFetchInit = {
  method?: string;
  headers?: any;
  body?: any;
  data?: any;
  params?: any;
  signal?: AbortSignal;
  parseData?: boolean;
};

// ==================== 过渡策略：重载签名 ====================

/**
 * @deprecated 请使用 `apiFetch<T>(url, init)` 显式指定类型
 * 过渡期使用，保留默认 any 以避免全链路报错
 */
export function apiFetch<T = any>(url: string, init?: ApiFetchInit): Promise<T>;

/**
 * 推荐签名：强制要求泛型参数
 */
export function apiFetch<T>(url: string, init?: ApiFetchInit): Promise<T>;

export async function apiFetch<T>(url: string, init: ApiFetchInit = {}): Promise<T> {
  const method = (init.method || 'GET').toUpperCase();
  let data = init.data ?? init.body;
  const isFormData = typeof FormData !== 'undefined' && data instanceof FormData;
  const headers: Record<string, any> = { ...(init.headers || {}) };

  if (data && !isFormData && typeof data === 'object') {
    headers['Content-Type'] = headers['Content-Type'] || 'application/json';
  }

  const res = await axios.request<T>({
    url: patchUrl(url),
    method: method as any,
    headers,
    params: init.params,
    data,
    signal: init.signal
  });
  return res.data;
}

// ==================== 分页专用方法 ====================

export async function apiFetchPaginated<T>(
  url: string,
  init: ApiFetchInit = {}
): Promise<PaginatedResponse<T>> {
  const response = await axios.request<PaginatedResponse<T>>({
    url: patchUrl(url),
    method: (init.method || 'GET').toUpperCase() as any,
    headers: init.headers,
    params: init.params,
    data: init.data,
    signal: init.signal
  });
  return response.data;
}

// ==================== 便捷方法（保持默认 any 作为过渡）====================

export const get = <T = any>(url: string, config?: any) =>
  axios.get<T>(patchUrl(url), config).then((r) => r.data);

export const post = <T = any>(url: string, data?: any, config?: any) =>
  axios.post<T>(patchUrl(url), data, config).then((r) => r.data);

export const put = <T = any>(url: string, data?: any, config?: any) =>
  axios.put<T>(patchUrl(url), data, config).then((r) => r.data);

export const del = <T = any>(url: string, config?: any) =>
  axios.delete<T>(patchUrl(url), config).then((r) => r.data);
```

### 1.4 迁移检查工具

```typescript
// utils/migrationCheck.ts - 新增文件（仅用于迁移期间）

/**
 * 检测代码中是否存在未迁移的 apiFetch<any> 调用
 * @hidden 仅开发调试使用
 */
export function checkUnmigratedApiCalls(): void {
  if (import.meta.env.PROD) return;

  // 警告：运行时检测不实际执行，仅做类型检查标记
  console.warn(
    '[Migration] 请逐步将 apiFetch<any> 替换为具体类型\n' +
    '使用 npx vue-tsc --noEmit 可查看类型错误'
  );
}
```

---

## 二、RFQ 业务类型补全（Phase 2）

### 2.1 Tariff Calculation 类型

```typescript
// types/index.ts 新增

export interface TariffCalculationResult {
  originalPrice: number;
  originalCurrency: string;
  exchangeRate: number | null;
  originalPriceUsd: number | null;
  tariffRate: number;
  tariffRateMissing: boolean;
  tariffRatePercent: string | null;
  freightRate: number;
  freightRatePercent: string | null;
  specialTariffRate: number;
  specialTariffRatePercent: string | null;
  tariffAmount: number;
  freightAmount: number;
  specialTariffAmount: number;
  tariffAmountUsd: number | null;
  freightAmountUsd: number | null;
  specialTariffAmountUsd: number | null;
  totalTariffAmount: number;
  totalTariffAmountUsd: number | null;
  standardCostLocal: number;
  standardCostUsd: number | null;
  standardCost: number;
  standardCostCurrency: string | null;
  shippingCountry: string | null;
  productGroup: string | null;
  productOrigin: string | null;
  projectLocation: string | null;
  deliveryTerms: string | null;
  isDdp: boolean;
  hasSpecialTariff: boolean;
  warnings: Array<{
    code: string | null;
    message: string | null;
    severity: string | null;
  }> | null;
  error: string | null;
}
```

### 2.2 Evaluation Criteria 类型

```typescript
// 前端默认评审维度（后端可接受任意字段）
export interface EvaluationCriteria {
  price?: number;      // 价格权重
  quality?: number;    // 质量权重
  delivery?: number;   // 交期权重
  service?: number;    // 服务权重
  // 允许扩展字段（支持后端动态添加维度）
  [key: string]: number | string | undefined;
}

// 评审结果
export interface ReviewScores {
  price?: number;
  quality?: number;
  delivery?: number;
  service?: number;
  [key: string]: number | undefined;
}
```

---

## 三、API 文件改造示例（Phase 2）

### 3.1 fetchRfqs 改造（分页处理）

```typescript
// api/rfq.ts

// 响应类型定义
interface RfqWorkflowListResponse {
  data: Rfq[];
  pagination?: {
    page: number;
    pageSize: number;
    total: number;
    totalPages: number;
  };
}

// 改造后
export async function fetchRfqs(params?: RfqListParams): Promise<RfqListResponse> {
  const payload = await apiFetch<RfqWorkflowListResponse>(
    '/rfq-workflow',
    { params: normalizeRfqListParams(params) }
  );

  return {
    data: payload.data,
    pagination: payload.pagination
  };
}
```

### 3.2 fetchRfqReview 改造

```typescript
export async function fetchRfqReview(rfqId: number): Promise<RfqReview | null> {
  try {
    const response = await apiFetch<{ data: { review?: RfqReview } }>(
      `/rfq-workflow/${rfqId}`
    );
    return response.data?.review ?? null;
  } catch {
    return null;
  }
}
```

---

## 四、错误处理迁移策略（Phase 4）

### 4.1 批量替换模式

```typescript
// 模式 1: 简单日志记录
// 改造前
catch (error: any) {
  console.error('[Module] operation failed', error);
}

// 改造后
catch (error: unknown) {
  console.error('[Module] operation failed', error);
}

// 模式 2: 消息显示
// 改造前
catch (error: any) {
  ElMessage.error(error.message || '操作失败');
}

// 改造后
catch (error: unknown) {
  ElMessage.error(extractErrorMessage(error));
}

// 模式 3: 特定错误码处理
// 改造前
catch (error: any) {
  if (error.code === 'INVALID_RFQ_ID') {
    // ...
  }
}

// 改造后
catch (error: unknown) {
  const code = getErrorCode(error);
  if (code === 'INVALID_RFQ_ID') {
    // ...
  }
}
```

---

## 五、实施优先级

### Phase 1: 基础设施（预计 2-3 天）

**状态**：已完成（2026-02-01）

1. 创建 `types/api.ts` - API 响应类型 + 类型守卫
2. 创建 `utils/errorHandling.ts` - 错误处理工具
3. 修改 `api/http.ts` - 添加重载签名（过渡兼容）
4. 创建 `docs/api-response-baseline.md` - API 响应基线清单

### Phase 2: RFQ 核心 API（预计 3-4 天）

**状态**：已完成（2026-02-01）

1. 补全 Tariff Calculation 类型
2. 改造 `api/rfq.ts`（20处 any）
3. 改造 `api/audit.ts`（5处 any）
4. 改造 `api/changeRequests.ts`（4处 any）

### Phase 3: 核心组件（预计 5-7 天）

**状态**：进行中（2026-02-01）
- 已完成：RfqPriceComparisonSection.vue、RfqDetailView.vue、RfqPriceComparisonTable.vue、RfqQuoteComparison.vue、RfqApprovalOperationPanel.vue
- 补充：RfqLineItemWorkflowLayout.vue

1. `RfqPriceComparisonSection.vue`（55处 any）
2. `RfqDetailView.vue`（24处 any）
3. `RfqPriceComparisonTable.vue`（20处 any）
4. `RfqQuoteComparison.vue`（19处 any）
5. `RfqApprovalOperationPanel.vue`（18处 any）

### Phase 4: 错误处理规范化（预计 3-4 天）

**状态**：进行中（2026-02-01）
- 已更新：RfqApprovalOperationPanel.vue、RfqDetailView.vue、RfqQuoteComparison.vue
- 补充：RfqLineItemWorkflowLayout.vue

1. 批量替换 `catch (error: any)` → `catch (error: unknown)`
2. 使用 `extractErrorMessage` 获取错误信息
3. 移除过渡重载签名

---

## 六、验证策略

1. **编译检查**: `npm run build` 无 TypeScript 错误
2. **类型检查**: `npx vue-tsc --noEmit` 通过
3. **功能验证**: 重点测试 RFQ 流程（创建、报价、比价、审批）
4. **错误场景**: 验证网络错误、业务错误、Lockdown 场景

---

## 七、API 响应基线清单

> **目的**: 验证后端 API 响应结构与前端类型定义的一致性
> **维护**: 新增 API 需同步更新此清单

### 7.1 统一响应格式

| 场景 | HTTP 状态码 | 响应结构 | 前端类型 |
|------|------------|----------|----------|
| 成功 | 200 | `{ success: true, data: T, message?: string }` | `ApiSuccessResponse<T>` |
| 分页成功 | 200 | `{ success: true, data: T[], page, pageSize, total, totalPages }` | `PaginatedResponse<T>` |
| 业务错误 | 200 | `{ success: false, error, code, details? }` | `ApiErrorResponse` |
| 参数错误 | 400 | `{ success: false, error, code }` | `ApiErrorResponse` |
| 未授权 | 401 | 标准 HTTP 认证失败 | 跳转登录 |
| 禁止访问 | 403 | 标准 HTTP 禁止访问 | 权限提示 |
| Lockdown | 503 | `{ error, message, lockdown: true, retryAfter? }` | `LockdownErrorResponse` |

### 7.2 已验证 API 清单

```markdown
| API 端点 | 状态 | 响应结构 | 备注 |
|----------|------|----------|------|
| POST /api/auth/login | ✅ | `{ success, data: { token, user }, mustChangePassword? }` | |
| GET /api/auth/me | ✅ | `{ success, data: user }` | |
| GET /api/rfq-workflow | ✅ | `{ success, data: Rfq[], pagination? }` | |
| GET /api/rfq-workflow/:id | ✅ | `{ success, data: Rfq }` | |
| POST /api/quotes | ✅ | `{ success, data: Quote }` | |
| ... | | | |
```

### 7.3 响应验证脚本

```typescript
// scripts/verify-api-responses.ts
// 用于验证实际 API 响应是否符合类型定义

import axios from 'axios';
import type { ApiResponse, PaginatedResponse } from '@/types/api';

const client = axios.create({ baseURL: '/api' });

async function verifyResponse<T>(
  method: 'GET' | 'POST' | 'PUT' | 'DELETE',
  url: string,
  validator: (data: unknown) => data is T
): Promise<{ ok: boolean; error?: string }> {
  try {
    const response = await client({ method, url });
    if (!validator(response.data)) {
      return { ok: false, error: 'Response validation failed' };
    }
    return { ok: true };
  } catch (error) {
    return { ok: false, error: String(error) };
  }
}

// 使用示例
// await verifyResponse('GET', '/rfq-workflow', isApiResponse<Rfq[]>);
```

---

## 八、风险与回滚

### 8.1 风险等级

| 变更 | 风险等级 | 说明 |
|------|----------|------|
| `catch (error: any)` → `catch (error: unknown)` | 低 | 运行时行为不变 |
| 添加明确的类型标注 | 低 | 仅编译时检查 |
| 使用过渡重载签名 | 低 | 向后兼容 |
| 修改 API 返回类型推断 | 中 | 影响调用方类型检查 |
| 移除默认 any 泛型 | 中 | 需要批量替换 |

### 8.2 回滚策略

- 保持 Git 提交原子性（每次 PR 限制在 5 个文件以内）
- 类型变更不修改运行时逻辑，回滚安全
- 过渡重载签名保留至 Phase 4 完成

---

## 九、持续治理机制

### 9.1 ESLint 规则配置

```javascript
// .eslintrc.cjs
module.exports = {
  rules: {
    // 禁止显式 any（带白名单）
    '@typescript-eslint/no-explicit-any': [
      'warn',
      {
        ignoreRestArgs: false,
        // 允许以下场景使用 any
        ignoreTypeArgs: true,  // 泛型参数（如 apiFetch<any> 迁移期间）
        // 允许的目录/文件
        allowedNames: [
          'mock/*',
          '*.d.ts',
          '**/env.d.ts',
          '**/vitest.d.ts',
        ]
      }
    ],
    // 鼓励使用 unknown 替代 any
    '@typescript-eslint/no-unsafe-call': 'error',
    '@typescript-eslint/no-unsafe-member-access': 'error',
    '@typescript-eslint/no-unsafe-assignment': 'error',
  }
};
```

### 9.2 允许使用 any 的场景（白名单）

| 场景 | 文件/目录 | 理由 |
|------|-----------|------|
| 环境变量类型扩展 | `env.d.ts` | 第三方库无类型 |
| 测试夹具 | `__tests__/**/*`, `vitest.d.ts` | 测试代码 |
| 第三方库垫片 | `types/axios.d.ts` | 库无官方类型 |
| 动态 JSON 响应 | `evaluationCriteria: Record<string, any>` | 后端动态结构 |
| 临时迁移期间 | `api/http.ts` 重载 | 渐进式迁移 |

> **记录位置**: `docs/any-usage-whitelist.md`

### 9.3 PR 准入规则

```markdown
## PR 检查清单

### 新增 any 类型检查
- [ ] 若新增代码包含 `any` 类型，请说明理由
- [ ] 是否可使用 `unknown` 替代？
- [ ] 是否可使用具体类型替代？
- [ ] 是否属于白名单场景？

### 新增 API 调用检查
- [ ] 是否为 `apiFetch<T>` 提供了具体类型？
- [ ] 响应类型是否与后端 API 匹配？（参考 api-response-baseline.md）
```

---

## 十、附录

### A. 相关文档

- `docs/any-type-abuse-report.md` - 原始问题报告
- `docs/any-usage-whitelist.md` - any 使用白名单
- `docs/api-response-baseline.md` - API 响应基线清单

### B. 参考资料

- [TypeScript ESLint Rules](https://typescript-eslint.io/rules/)
- [Axios Error Handling](https://axios-http.com/docs/handling_errors)
- [Vue 3 TypeScript Guide](https://vuejs.org/guide/typescript/overview.html)
