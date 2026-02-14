# 服务层基线指标报告（2025-11-16）

| 指标 | 数值 | 说明 |
|------|------|------|
| 代码重复率 | **3.09%** | `node tools/metrics/collect-baseline.js` 通过 jscpd 扫描 `apps/web/src` 得出 |
| 通知 API 使用次数 | 658 行 | 包括 `ElNotification`/`ElMessage`/`ElMessageBox`，来自扫描脚本 |
| 审批逻辑使用次数 | 1058 行 | `approval`/`workflow` 关键字匹配 |
| GitHub PR 数据 | 暂无 | `GITHUB_TOKEN` 未设置，指标待上线后补齐 |

数据文件：`var/metrics/baseline-2025-11-16.json`。  
后续采集：参考 `tools/metrics/collect-baseline.js` 脚本输出。*** End Patch
