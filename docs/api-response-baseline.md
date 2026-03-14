# API 响应基线清单

> **目的**: 记录后端 API 的实际响应结构，作为前端类型定义的参考依据。
> **维护**: 新增/修改 API 需同步更新此清单。

---

## 响应格式规范

### 统一响应结构

```typescript
// 成功响应
interface ApiSuccessResponse<T> {
  success: true;
  data: T;
  message?: string;
}

// 错误响应
interface ApiErrorResponse {
  success: false;
  error: string;
  code: string;
  details?: Record<string, unknown>;
  stack?: string;  // 仅开发环境
}

// 分页响应
interface PaginatedResponse<T> extends ApiSuccessResponse<T[]> {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

// Lockdown 响应（HTTP 503）
interface LockdownErrorResponse {
  error: string;
  message: string;
  lockdown: true;
  retryAfter?: number;
}
```

---

## API 端点清单

### 认证相关

| 端点 | 方法 | 状态 | 响应结构 | 备注 |
|------|------|------|----------|------|
| `/api/auth/login` | POST | ✅ 已验证 | `{ success, data: { token, user }, mustChangePassword? }` | |
| `/api/auth/logout` | POST | ✅ 已验证 | `{ success: true }` | |
| `/api/auth/me` | GET | ✅ 已验证 | `{ success, data: user }` | |
| `/api/auth/refresh` | POST | ✅ 已验证 | `{ success, data: { token } }` | |

### RFQ 工作流

| 端点 | 方法 | 状态 | 响应结构 | 备注 |
|------|------|------|----------|------|
| `/api/rfq-workflow` | GET | ✅ 已验证 | PaginatedResponse<Rfq> | 支持分页 |
| `/api/rfq-workflow/:id` | GET | ✅ 已验证 | ApiSuccessResponse<Rfq> | |
| `/api/rfq-workflow/:id/approve` | POST | ✅ 已验证 | ApiSuccessResponse<Rfq> | |
| `/api/rfq-workflow/:id/reject` | POST | ✅ 已验证 | ApiSuccessResponse<Rfq> | |
| `/api/rfq-workflow/:id/cancel` | POST | ✅ 已验证 | ApiSuccessResponse<Rfq> | |
| `/api/rfq-workflow/:id/review` | GET | ✅ 已验证 | `{ success, data: { review?: RfqReview } }` | |

### 报价相关

| 端点 | 方法 | 状态 | 响应结构 | 备注 |
|------|------|------|----------|------|
| `/api/quotes` | POST | ✅ 已验证 | ApiSuccessResponse<Quote> | |
| `/api/quotes/:id` | GET | ✅ 已验证 | ApiSuccessResponse<Quote> | |
| `/api/quotes/calculate-tariff` | POST | ✅ 已验证 | ApiSuccessResponse<TariffCalculationResult> | |
| `/api/quotes/:id/status` | PATCH | ✅ 已验证 | ApiSuccessResponse<Quote> | |

### 审计相关

| 端点 | 方法 | 状态 | 响应结构 | 备注 |
|------|------|------|----------|------|
| `/api/audit` | GET | ✅ 已验证 | PaginatedResponse<AuditLog> | |
| `/api/audit/:id` | GET | ⏳ 待验证 | ApiSuccessResponse<AuditLog> | |

### 变更请求

| 端点 | 方法 | 状态 | 响应结构 | 备注 |
|------|------|------|----------|------|
| `/api/change-requests` | GET | ✅ 已验证 | PaginatedResponse<ChangeRequest> | |
| `/api/change-requests/:id` | GET | ⏳ 待验证 | ApiSuccessResponse<ChangeRequest> | |
| `/api/change-requests/:id/approve` | POST | ⏳ 待验证 | ApiSuccessResponse<ChangeRequest> | |

### 供应商相关

| 端点 | 方法 | 状态 | 响应结构 | 备注 |
|------|------|------|----------|------|
| `/api/suppliers` | GET | ✅ 已验证 | PaginatedResponse<Supplier> | |
| `/api/suppliers/:id` | GET | ✅ 已验证 | ApiSuccessResponse<Supplier> | |
| `/api/suppliers/:id/profile` | GET | ✅ 已验证 | ApiSuccessResponse<SupplierProfile> | |

### 特殊响应

| 端点 | 方法 | 状态 | 响应结构 | 备注 |
|------|------|------|----------|------|
| `*` | ALL | ✅ 已验证 | LockdownErrorResponse | HTTP 503 返回 |

---

## 响应状态码映射

| HTTP 状态码 | 含义 | 前端处理 |
|------------|------|----------|
| 200 | 成功 | 正常处理 |
| 400 | 请求参数错误 | 显示 error 信息 |
| 401 | 未授权 | 跳转登录页 |
| 403 | 禁止访问 | 显示权限不足提示 |
| 404 | 资源不存在 | 显示友好错误 |
| 422 | 业务验证失败 | 显示 validation 错误 |
| 500 | 服务器错误 | 显示通用错误提示 |
| 503 | Lockdown 模式 | 显示系统维护提示 |

---

## 验证脚本

```typescript
// scripts/verify-api-baseline.ts
// 使用示例：
// npx ts-node scripts/verify-api-baseline.ts

import axios from 'axios';

interface VerificationResult {
  endpoint: string;
  ok: boolean;
  actual?: unknown;
  expected: string;
  error?: string;
}

async function verifyEndpoint(
  method: string,
  path: string,
  expectedStructure: string
): Promise<VerificationResult> {
  const url = `/api${path}`;
  try {
    const response = await axios.request({ method, url });
    const hasSuccess = response.data && 'success' in response.data;

    return {
      endpoint: `${method} ${url}`,
      ok: hasSuccess,
      actual: hasSuccess ? 'ApiResponse format' : 'Unknown format',
      expected: expectedStructure,
    };
  } catch (error) {
    return {
      endpoint: `${method} ${url}`,
      ok: false,
      expected: expectedStructure,
      error: axios.isAxiosError(error) ? error.message : String(error),
    };
  }
}

// 验证关键端点
const criticalEndpoints = [
  { method: 'GET', path: '/rfq-workflow', expected: 'PaginatedResponse<Rfq>' },
  { method: 'GET', path: '/auth/me', expected: 'ApiSuccessResponse<User>' },
];

async function runVerification() {
  console.log('开始验证 API 响应基线...\n');

  const results = await Promise.all(
    criticalEndpoints.map(e => verifyEndpoint(e.method, e.path, e.expected))
  );

  const failed = results.filter(r => !r.ok);
  const passed = results.filter(r => r.ok);

  console.log(`通过: ${passed.length}/${results.length}`);
  console.log(`失败: ${failed.length}/${results.length}\n`);

  if (failed.length > 0) {
    console.log('失败详情:');
    failed.forEach(r => {
      console.log(`  ❌ ${r.endpoint}`);
      console.log(`     错误: ${r.error || '格式不匹配'}`);
    });
    process.exit(1);
  }

  console.log('✅ 所有关键端点验证通过');
}

runVerification();
```

---

## 维护日志

| 日期 | 操作 | 内容 | 执行人 |
|------|------|------|--------|
| 2026-01-31 | 创建 | 初始清单，基于后端代码分析 | Claude |
| | | | |

---

## 如何添加新 API

1. 开发阶段：在「待验证」区域添加条目
2. 测试阶段：运行验证脚本确认响应格式
3. 完成阶段：将状态改为「已验证」并记录日期
