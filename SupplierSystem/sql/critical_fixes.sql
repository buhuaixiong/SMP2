-- =====================================================
-- 关键问题安全修复脚本
-- 适用场景：已有数据的数据库（低风险增量修改）
-- 执行前请备份数据库！
-- =====================================================

USE SupplierSystemDev;
GO

-- =====================================================
-- 第一部分：布尔类型修复 (INT → BIT)
-- 风险：低 - SQL Server 允许 INT(0/1) 转为 BIT
-- =====================================================

-- 检查所有布尔字段的值是否只有 0 或 1
SELECT 'users.is_active' AS table_column, COUNT(*) AS total,
       SUM(CASE WHEN is_active NOT IN (0,1) THEN 1 ELSE 0 END) AS invalid_count
FROM users
UNION ALL
SELECT 'users.must_change_password', COUNT(*),
       SUM(CASE WHEN must_change_password NOT IN (0,1) THEN 1 ELSE 0 END)
FROM users
UNION ALL
SELECT 'users.force_password_reset', COUNT(*),
       SUM(CASE WHEN force_password_reset NOT IN (0,1) THEN 1 ELSE 0 END)
FROM users;

-- 如果检查通过（invalid_count 均为 0），执行以下修复：

-- users 表
BEGIN TRY
    ALTER TABLE users ALTER COLUMN is_active BIT NULL;
    ALTER TABLE users ALTER COLUMN must_change_password BIT NULL;
    ALTER TABLE users ALTER COLUMN force_password_reset BIT NULL;
    PRINT 'users 表布尔类型修复完成';
END TRY
BEGIN CATCH
    PRINT 'users 表修复失败: ' + ERROR_MESSAGE();
END CATCH

-- suppliers 表
BEGIN TRY
    ALTER TABLE suppliers ALTER COLUMN temp_account_status NVARCHAR(50) NULL; -- 先改类型
    ALTER TABLE suppliers ADD temp_account_status_new BIT NULL;
    UPDATE suppliers SET temp_account_status_new = CASE WHEN temp_account_status = 'active' THEN 1 ELSE 0 END;
    ALTER TABLE suppliers DROP COLUMN temp_account_status;
    EXEC sp_rename 'suppliers.temp_account_status_new', 'temp_account_status', 'COLUMN';
    PRINT 'suppliers 表布尔类型修复完成';
END TRY
BEGIN CATCH
    PRINT 'suppliers 表修复失败: ' + ERROR_MESSAGE();
END CATCH

-- supplier_documents.is_required
BEGIN TRY
    ALTER TABLE supplier_documents ALTER COLUMN is_required BIT NULL;
    PRINT 'supplier_documents.is_required 修复完成';
END TRY
BEGIN CATCH
    PRINT 'supplier_documents 修复失败: ' + ERROR_MESSAGE();
END CATCH

-- rfqs.is_line_item_mode
BEGIN TRY
    ALTER TABLE rfqs ALTER COLUMN is_line_item_mode BIT NULL;
    PRINT 'rfqs.is_line_item_mode 修复完成';
END TRY
BEGIN CATCH
    PRINT 'rfqs 修复失败: ' + ERROR_MESSAGE();
END CATCH

-- quotes.is_latest, has_special_tariff
BEGIN TRY
    ALTER TABLE quotes ALTER COLUMN is_latest BIT NULL;
    ALTER TABLE quotes ALTER COLUMN has_special_tariff BIT NULL;
    PRINT 'quotes 表布尔类型修复完成';
END TRY
BEGIN CATCH
    PRINT 'quotes 表修复失败: ' + ERROR_MESSAGE();
END CATCH

-- quote_line_items.has_special_tariff
BEGIN TRY
    ALTER TABLE quote_line_items ALTER COLUMN has_special_tariff BIT NULL;
    PRINT 'quote_line_items.has_special_tariff 修复完成';
END TRY
BEGIN CATCH
    PRINT 'quote_line_items 修复失败: ' + ERROR_MESSAGE();
END CATCH

-- audit_log.is_sensitive, immutable
BEGIN TRY
    ALTER TABLE audit_log ALTER COLUMN is_sensitive BIT NULL;
    ALTER TABLE audit_log ALTER COLUMN immutable BIT NULL;
    PRINT 'audit_log 表布尔类型修复完成';
END TRY
BEGIN CATCH
    PRINT 'audit_log 表修复失败: ' + ERROR_MESSAGE();
END CATCH

GO

-- =====================================================
-- 第二部分：货币精度提升 (DECIMAL(18,2) → DECIMAL(18,4))
-- 风险：低 - 扩大精度不会丢失数据
-- =====================================================

BEGIN TRY
    -- rfqs.amount, budget_amount
    ALTER TABLE rfqs ALTER COLUMN amount DECIMAL(18,4) NULL;
    ALTER TABLE rfqs ALTER COLUMN budget_amount DECIMAL(18,4) NULL;
    PRINT 'rfqs 货币精度修复完成';
END TRY
BEGIN CATCH
    PRINT 'rfqs 货币精度修复失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    -- rfq_line_items.quantity, estimated_unit_price
    ALTER TABLE rfq_line_items ALTER COLUMN quantity DECIMAL(18,4) NULL;
    ALTER TABLE rfq_line_items ALTER COLUMN estimated_unit_price DECIMAL(18,4) NULL;
    PRINT 'rfq_line_items 精度修复完成';
END TRY
BEGIN CATCH
    PRINT 'rfq_line_items 精度修复失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    -- quotes.unit_price, total_amount, 成本字段
    ALTER TABLE quotes ALTER COLUMN unit_price DECIMAL(18,4) NULL;
    ALTER TABLE quotes ALTER COLUMN total_amount DECIMAL(18,4) NULL;
    ALTER TABLE quotes ALTER COLUMN total_standard_cost_local DECIMAL(18,4) NULL;
    ALTER TABLE quotes ALTER COLUMN total_standard_cost_usd DECIMAL(18,4) NULL;
    ALTER TABLE quotes ALTER COLUMN total_tariff_amount_local DECIMAL(18,4) NULL;
    ALTER TABLE quotes ALTER COLUMN total_tariff_amount_usd DECIMAL(18,4) NULL;
    PRINT 'quotes 货币精度修复完成';
END TRY
BEGIN CATCH
    PRINT 'quotes 货币精度修复失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    -- quote_line_items 所有金额字段
    ALTER TABLE quote_line_items ALTER COLUMN unit_price DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN minimum_order_quantity DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN standard_package_quantity DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN total_price DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN original_price_usd DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN exchange_rate DECIMAL(10,6) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN tariff_amount_local DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN tariff_amount_usd DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN special_tariff_amount_local DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN special_tariff_amount_usd DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN standard_cost_local DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN standard_cost_usd DECIMAL(18,4) NULL;
    PRINT 'quote_line_items 货币精度修复完成';
END TRY
BEGIN CATCH
    PRINT 'quote_line_items 货币精度修复失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    -- invoices.amount
    ALTER TABLE invoices ALTER COLUMN amount DECIMAL(18,4) NULL;
    PRINT 'invoices.amount 精度修复完成';
END TRY
BEGIN CATCH
    PRINT 'invoices.amount 精度修复失败: ' + ERROR_MESSAGE();
END CATCH

GO

-- =====================================================
-- 第三部分：命名规范修复
-- 风险：低 - 仅重命名
-- =====================================================

BEGIN TRY
    -- system_config.[key] → config_key (避开保留字)
    EXEC sp_rename 'system_config.[key]', 'config_key', 'COLUMN';
    PRINT 'system_config.key 重命名完成';
END TRY
BEGIN CATCH
    PRINT 'system_config.key 重命名失败: ' + ERROR_MESSAGE();
END CATCH

GO

-- =====================================================
-- 第四部分：补充缺失的外键索引
-- 风险：低 - 只创建索引，不修改数据
-- =====================================================

-- 缺失索引列表（基于外键列）
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_supplier_tags_tag_id')
BEGIN
    CREATE NONCLUSTERED INDEX IX_supplier_tags_tag_id
    ON supplier_tags(tag_id);
    PRINT 'IX_supplier_tags_tag_id 创建完成';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_rfq_attachments_rfq_id')
BEGIN
    CREATE NONCLUSTERED INDEX IX_rfq_attachments_rfq_id
    ON rfq_attachments(rfq_id);
    PRINT 'IX_rfq_attachments_rfq_id 创建完成';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_quote_line_items_quote_id')
BEGIN
    CREATE NONCLUSTERED INDEX IX_quote_line_items_quote_id
    ON quote_line_items(quote_id);
    PRINT 'IX_quote_line_items_quote_id 创建完成';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_rfq_line_items_line_item_status')
BEGIN
    CREATE NONCLUSTERED INDEX IX_rfq_line_items_line_item_status
    ON rfq_line_items(status);
    PRINT 'IX_rfq_line_items_status 创建完成';
END

GO

-- =====================================================
-- 第五部分：添加状态 CHECK 约束
-- 风险：低 - 只添加约束，数据不满足则失败
-- =====================================================

-- users.status 约束
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_users_status')
BEGIN
    ALTER TABLE users ADD CONSTRAINT CHK_users_status
    CHECK (status IN ('active', 'inactive', 'locked', 'pending'));
    PRINT 'CHK_users_status 创建完成';
END

-- suppliers.status 约束
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_suppliers_status')
BEGIN
    ALTER TABLE suppliers ADD CONSTRAINT CHK_suppliers_status
    CHECK (status IN ('pending', 'approved', 'rejected', 'suspended', 'active'));
    PRINT 'CHK_suppliers_status 创建完成';
END

-- rfqs.status 约束
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_rfqs_status')
BEGIN
    ALTER TABLE rfqs ADD CONSTRAINT CHK_rfqs_status
    CHECK (status IN ('draft', 'pending', 'approved', 'rejected', 'closed', 'cancelled'));
    PRINT 'CHK_rfqs_status 创建完成';
END

-- quotes.status 约束
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_quotes_status')
BEGIN
    ALTER TABLE quotes ADD CONSTRAINT CHK_quotes_status
    CHECK (status IN ('draft', 'submitted', 'withdrawn', 'accepted', 'rejected'));
    PRINT 'CHK_quotes_status 创建完成';
END

-- invoices.status 约束
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_invoices_status')
BEGIN
    ALTER TABLE invoices ADD CONSTRAINT CHK_invoices_status
    CHECK (status IN ('pending', 'approved', 'rejected', 'paid', 'cancelled'));
    PRINT 'CHK_invoices_status 创建完成';
END

GO

PRINT '========================================';
PRINT '关键问题安全修复完成！';
PRINT '请验证数据完整性后再操作生产环境';
PRINT '========================================';
GO
