# SMP 数据库表与业务映射说明

> 数据库类型：SQL Server  
> 目标库：`SMP`  
> 说明：当前仓库未包含完整后端 SQL Server 建表脚本（`SupplierSystem` 

## 1. 已落地/正在使用的表

| 表名 | 主要作用 | 对应业务 | 关键关系 |
|---|---|---|---|
| `suppliers` | 供应商主档（已创建供应商） | 供应商目录、供应商审批（状态变更审批） | 与 `supplier_registration_applications` 业务衔接；与 `purchase_requests`、`supplier_tag_mappings`、`buyer_supplier_assignments` 关联 |
| `supplier_registration_applications` | 新供应商注册申请池 | 新注册审批（质量经理等角色处理） | 通过审核后转入 `suppliers` |
| `supplier_tag_mappings` | 供应商与标签的多对多映射 | 批量打标签、按标签筛选供应商 | 关联 `suppliers`，支撑标签管理 |
| `buyer_supplier_assignments` | 采购员与供应商分配关系 | 按标签批量分配采购员、采购员仅看自己负责供应商 | 关联 `suppliers`；与权限/角色控制联动 |
| `audit_log` | 审计日志总表 | 关键操作留痕（分配、标签、文件访问等） | 记录操作人、实体、动作与变更 |
| `files` | 文件元数据（上传文件记录） | 供应商文件上传、下载、管理 | 与 `file_access_tokens`、`audit_log` 联动 |
| `file_access_tokens` | 文件下载一次性令牌 | 文件安全下载、防直链、防枚举 | `userId` 外键关联 `users(id)` |
| `users` | 用户主表 | 登录、审批、上传、审计责任主体 | 被 `file_access_tokens`、`rfq_approvals`、`approval_comments`、`purchase_requests` 等引用 |

## 2. RFQ/采购流程扩展表（文档中有明确 DDL）

| 表名 | 主要作用 | 对应业务 | 关键关系 |
|---|---|---|---|
| `rfqs` | RFQ 头表（询价单） | RFQ 创建、发布、评审、审批、PR 生成 | 被多张 RFQ 子表引用；扩展字段包含 `selected_quote_id`、`approval_status`、`pr_generated` |
| `rfq_line_items` | RFQ 行项目 | 多行物料询价 | 外键到 `rfqs` |
| `rfq_attachments` | RFQ 附件（单据级或行级） | 需求附件、技术附件上传 | 外键到 `rfqs`、`rfq_line_items`、`users` |
| `quotes` | 供应商报价头表 | 供应商提交报价 | 被 `quote_line_items`、`purchase_requests` 引用 |
| `quote_line_items` | 报价行项目 | 分行报价、比价 | 外键到 `quotes`、`rfq_line_items` |
| `price_comparison_attachments` | 比价截图附件 | 采购评审时上传 1688/JD/ZKH 等截图 | 外键到 `rfqs`、`rfq_line_items`、`users` |
| `rfq_approvals` | RFQ 审批流节点 | 采购经理/总监逐级审批 | 外键到 `rfqs`、`users` |
| `approval_comments` | 审批评论 | 审批意见与协同讨论 | 外键到 `rfq_approvals`、`users` |
| `purchase_requests` | PR 采购申请头表 | RFQ 审批通过后生成 PR | 外键到 `rfqs`、`quotes`、`suppliers`、`users` |
| `pr_line_items` | PR 行项目 | PR 明细落地 | 外键到 `purchase_requests`、`rfq_line_items`、`quote_line_items` |

## 3. 方案/迁移中的表（按规划文档）

| 表名 | 主要作用 | 对应业务 | 状态 |
|---|---|---|---|
| `supplier_baselines` | 供应商基线快照 | 变更对比、触发审批判断 | 规划/迁移中（文档显示脚本已就绪） |
| `temp_supplier_users` | 临时供应商账号 | 注册后临时账号发放、首登改密 | 规划/迁移中 |
| `temp_account_sequences` | 临时账号序列控制 | 按币种/规则生成不冲突账号编号 | 规划/迁移中 |
| `supplier_users`（字段增强） | 供应商账号表扩展 | 强制改密、初始密码发放追踪 | 规划/迁移中（字段增强） |

## 4. 业务链路视图（便于和业务方对齐）

1. 新供应商注册：`supplier_registration_applications` -> 审批通过 -> `suppliers`。  
2. 供应商治理：`suppliers` + `supplier_tag_mappings` + `buyer_supplier_assignments`。  
3. RFQ 到 PR：`rfqs` -> `rfq_line_items` -> `quotes`/`quote_line_items` -> `rfq_approvals`/`approval_comments` -> `purchase_requests`/`pr_line_items`。  
4. 文件安全：`files` + `file_access_tokens` + `audit_log`。  


