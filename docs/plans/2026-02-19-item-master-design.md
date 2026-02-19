# 物料管理（Item Master）功能设计

## 1. 背景与目标

目标是在现有系统中新增“物料主数据管理”能力，满足以下业务场景：

- 采购经理可导入/更新 Excel（参考 `docs/Jenny Item master on Jan 27.xlsm`）。
- 采购员（现有角色 `purchaser`）只能查看自己名下物料。
- 采购总监（`procurement_director`）可全览全部物料。
- 数据归属字段以 Excel 最后一列 `Sourcing name` 为准。

本设计遵循最小侵入原则：复用现有权限体系、审计体系与 OpenXML 读取能力，新增独立物料主数据模块，避免污染 RFQ/Requisition 语义。

## 2. 已确认业务决策（冻结）

1. 归属映射：`Sourcing name -> users.username`（忽略大小写）。
2. 唯一键：`Fac + Item Number + Vendor`。
3. 未匹配用户：允许写入，标记未分配（`owner_user_id = null`），仅经理/总监可见。
4. 同文件重复键：保留最后一行，记录 warning。
5. 更新覆盖规则：空单元格覆盖旧值（写空/null）。
6. 采购经理可全览物料数据。
7. 默认导入全 sheet（可勾选部分 sheet）。
8. 导入不做删除/失效同步（仅新增/更新）。
9. 每次导入保留批次记录与明细统计。
10. 采购员角色直接使用现有 `purchaser`。

## 3. 数据模型设计

### 3.1 主表：`item_master_records`

建议字段（核心）：

- `id` bigint identity pk
- `fac` nvarchar(20) not null
- `item_number` nvarchar(100) not null
- `vendor` nvarchar(100) not null
- `sourcing_name` nvarchar(200) null
- `owner_user_id` nvarchar(64) null
- `owner_username_snapshot` nvarchar(100) null
- `item_description` nvarchar(500) null
- `unit` nvarchar(30) null
- `moq` float null
- `spq` float null
- `currency` nvarchar(10) null
- `price_break_1` float null
- `exchange_rate` float null
- `vendor_name` nvarchar(255) null
- `terms` nvarchar(50) null
- `terms_desc` nvarchar(255) null
- `company` nvarchar(50) null
- `class` nvarchar(50) null
- `raw_payload` nvarchar(max) null（可选，保留原始行快照）
- `created_at` datetime2(3) not null
- `updated_at` datetime2(3) not null
- `last_import_batch_id` bigint null

索引与约束：

- `UX_item_master_records_fac_item_vendor` unique (`fac`,`item_number`,`vendor`)
- `IX_item_master_records_owner_user_id`
- `IX_item_master_records_sourcing_name`
- `IX_item_master_records_fac`

### 3.2 批次表：`item_master_import_batches`

- `id` bigint identity pk
- `file_name` nvarchar(255) not null
- `sheet_scope` nvarchar(500) not null（如 `HZ,TH`）
- `status` nvarchar(30) not null（`running/success/partial_success/failed`）
- `started_at` datetime2(3) not null
- `finished_at` datetime2(3) null
- `imported_by_user_id` nvarchar(64) not null
- `imported_by_name` nvarchar(200) null
- `inserted_count` int not null default 0
- `updated_count` int not null default 0
- `warning_count` int not null default 0
- `error_count` int not null default 0
- `summary_json` nvarchar(max) null（聚合信息）
- `warnings_json` nvarchar(max) null（行级 warning）
- `errors_json` nvarchar(max) null（行级 error）

## 4. 导入流程设计

1. 采购经理上传文件，未指定 sheet 时默认读取全部工作表。
2. 基于第 6 行表头构建列映射；关键列必须包含：
   - `Fac`、`Item Number`、`Vendor`、`Sourcing name`
3. 读取数据行并标准化键：
   - `fac_key = trim(fac).upper()`
   - `item_key = trim(item_number).upper()`
   - `vendor_key = trim(vendor).upper()`
4. 同批次内去重：同键只保留最后一行，前面行写 warning。
5. 用户映射：`sourcing_name` 按 `users.username`（忽略大小写）匹配。
   - 命中：写 `owner_user_id`
   - 未命中：`owner_user_id = null`，并记 warning
6. Upsert（按 `fac + item_number + vendor`）：
   - 存在则 update（空值覆盖）
   - 不存在则 insert
7. 写入批次统计与明细（insert/update/warning/error）。
8. 调用 `IAuditService` 记录批次级审计。

## 5. API 设计

新增控制器：`SupplierSystem/src/SupplierSystem.Api/Controllers/ItemMasterController.cs`

### 5.1 导入

- `POST /api/item-master/import`
- 入参：`multipart/form-data`
  - `file`（xlsm/xlsx）
  - `sheets`（可选，多个；为空则全量）
- 权限：`item_master.import.manage`
- 返回：批次 ID + 统计 + warning/error 摘要

### 5.2 列表查询

- `GET /api/item-master`
- 常见筛选：`fac`、`itemNumber`、`vendor`、`sourcingName`、`unassignedOnly`、分页
- 权限：
  - `item_master.view.own`（采购员）
  - `item_master.view.all`（经理/总监）
- 数据可见性：
  - 采购员：强制 `owner_user_id = current_user.id`
  - 经理/总监：无 owner 限制

### 5.3 批次记录

- `GET /api/item-master/import-batches`
- `GET /api/item-master/import-batches/{id}`
- 权限：`item_master.view.all`

## 6. 权限模型变更

`SupplierSystem/src/SupplierSystem.Application/Security/Permissions.cs` 新增：

- `ItemMasterImportManage = "item_master.import.manage"`
- `ItemMasterViewAll = "item_master.view.all"`
- `ItemMasterViewOwn = "item_master.view.own"`

`SupplierSystem/src/SupplierSystem.Application/Security/RolePermissions.cs` 挂载：

- `purchaser`：`item_master.view.own`
- `procurement_manager`：`item_master.import.manage` + `item_master.view.all`
- `procurement_director`：`item_master.view.all`
- `admin`：默认包含全部

## 7. 页面行为

### 7.1 采购经理

- 导入入口（上传 + sheet 勾选）
- 导入结果弹窗（统计 + warning/error）
- 物料全量列表（支持“仅未分配”筛选）
- 批次历史页（可追溯）

### 7.2 采购员

- 仅显示本人归属物料
- 不显示导入入口

### 7.3 采购总监

- 全量列表 + 聚合统计（按采购员/Fac）

## 8. 错误处理与审计

- `fatal`：文件损坏、sheet 不存在、表头不可识别、事务失败 -> 批次 `failed`
- `row_error`：行级解析失败 -> 跳过该行，批次可 `partial_success`
- `warning`：同文件重复键、未匹配采购员

审计：

- 业务批次明细写 `item_master_import_batches`
- 同步写 `audit_log`（`entity_type=item_master_import_batch`）

## 9. 测试策略

建议最小测试集：

1. 导入解析测试：表头映射、sheet 选择、数字/空值处理。
2. 重复键策略测试：同批次保留最后一行。
3. 归属映射测试：`sourcing_name -> username` 命中/未命中。
4. Upsert 测试：新增与更新路径；空值覆盖旧值。
5. 权限可见性测试：
   - `purchaser` 仅看自己
   - `procurement_manager`/`procurement_director` 全览
6. 批次记录测试：统计与状态正确。

## 10. 落地顺序

1. SQL 迁移：新增两张表 + 索引/唯一约束。
2. Domain/DbContext 映射。
3. 导入服务（解析 + 校验 + 去重 + upsert + 批次记录）。
4. Controller 接口与权限接入。
5. 自动化测试补齐。
6. 前端页面接入与联调。
7. 用 `Jenny Item master on Jan 27.xlsm` 进行预生产验证。

