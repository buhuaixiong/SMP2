# 教程：创建新的服务

## 1. 准备工作
1. 阅读 `docs/development/services-guide.md` 获取规范。  
2. 运行 `node scripts/setup/create-service-structure.mjs` 确保核心目录存在。  
3. 决定服务名称（例如 `supplierAnalytics`）及依赖。

## 2. 编写 ServiceDefinition
```ts
// apps/web/src/services/supplier-analytics.ts
import type { ServiceDefinition } from "@/core/services"
import type { HttpService } from "@/services"

export interface SupplierAnalyticsService {
  fetchSummary(): Promise<{ total: number; active: number }>
}

export const supplierAnalyticsService: ServiceDefinition<SupplierAnalyticsService> = {
  name: "supplierAnalytics",
  dependencies: ["http"],
  async setup({ get }) {
    const http = get<HttpService>("http")
    return {
      fetchSummary() {
        return http.get("/analytics/suppliers")
      },
    }
  },
}
```

## 3. 注册与导出
```ts
// apps/web/src/services/index.ts
import { supplierAnalyticsService } from "./supplier-analytics"
import type { SupplierAnalyticsService } from "./supplier-analytics"

const SERVICE_SEQUENCE = { supplierAnalytics: 60, ... }
coreServices.push(supplierAnalyticsService)
export type { SupplierAnalyticsService }
```

## 4. 在组件中使用
```ts
import { useService } from "@/core/hooks"
import type { SupplierAnalyticsService } from "@/services"

const analytics = useService<SupplierAnalyticsService>("supplierAnalytics")
const summary = await analytics.fetchSummary()
```

## 5. 编写测试
```ts
import { describe, it, expect, vi } from "vitest"
import { startMockServices } from "@/tests/setup/mockServices"
import { supplierAnalyticsService } from "@/services/supplier-analytics"

describe("supplierAnalyticsService", () => {
  it("fetches summary", async () => {
    const http = { get: vi.fn().mockResolvedValue({ total: 10, active: 7 }) }
    const manager = await startMockServices({
      http: () => ({ name: "http", setup: () => http }),
      supplierAnalytics: () => supplierAnalyticsService,
    })
    const service = await manager.start<SupplierAnalyticsService>("supplierAnalytics")
    expect(await service.fetchSummary()).toEqual({ total: 10, active: 7 })
  })
})
```

## 6. 更新文档
1. 在 `docs/api/services/` 下新增 `<service>.md`；  
2. 如需教程/示例，补充到本文件；  
3. 在实施计划中更新任务状态。

完成上述步骤后，提交 PR 并附上扫描/测试结果。*** End Patch
