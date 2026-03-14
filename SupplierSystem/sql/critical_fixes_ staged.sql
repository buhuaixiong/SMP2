-- =====================================================
-- 【第一阶段】执行前检查与备份验证
-- 目的：确保环境安全，数据可恢复
-- =====================================================

PRINT '========================================';
PRINT '【第一阶段】执行前检查';
PRINT '========================================';
PRINT '';

/* ---------- 1.1 确认数据库连接 ---------- */
PRINT '1.1 检查数据库连接...';

DECLARE @db_name NVARCHAR(100) = DB_NAME();
DECLARE @server_version NVARCHAR(100) = @@VERSION;
PRINT '当前数据库: ' + @db_name;
PRINT 'SQL Server 版本: ' + LEFT(@server_version, 50);

/* ---------- 1.2 备份检查 ---------- */
PRINT '';
PRINT '1.2 检查最近备份...';

DECLARE @latest_backup NVARCHAR(100);
SELECT TOP 1 @latest_backup = name + ' @ ' + CONVERT(NVARCHAR, backup_finish_date, 120)
FROM msdb.dbo.backupset
WHERE database_name = @db_name
ORDER BY backup_finish_date DESC;

IF @latest_backup IS NULL
BEGIN
    PRINT '⚠️ 警告: 数据库未找到备份记录！';
    PRINT '建议执行: BACKUP DATABASE [' + @db_name + '] TO DISK = ''path/backup.bak'' ';
END
ELSE
BEGIN
    PRINT '最新备份: ' + @latest_backup;
END

/* ---------- 1.3 数据快照（用于对比） ---------- */
PRINT '';
PRINT '1.3 记录关键表数据量...';

CREATE TABLE #data_snapshot (
    table_name NVARCHAR(100),
    row_count INT,
    col_count INT
);

INSERT INTO #data_snapshot
SELECT
    t.name,
    p.row_count,
    c.col_count
FROM sys.tables t
CROSS APPLY (
    SELECT SUM(p.rows) AS row_count
    FROM sys.partitions p
    WHERE p.object_id = t.object_id AND p.index_id IN (0,1)
) p
CROSS APPLY (
    SELECT COUNT(*) AS col_count
    FROM sys.columns c
    WHERE c.object_id = t.object_id
) c
WHERE t.is_ms_shipped = 0;

SELECT * FROM #data_snapshot ORDER BY row_count DESC;
PRINT '请截图保存上述数据量，作为修复后对比依据';

/* ---------- 1.4 布尔字段值验证 ---------- */
PRINT '';
PRINT '1.4 验证布尔字段值范围（必须是0或1）...';

DECLARE @bool_error BIT = 0;

-- users.is_active
IF EXISTS (SELECT 1 FROM users WHERE is_active NOT IN (0,1))
BEGIN
    PRINT '⚠️ users.is_active 存在非法值！';
    SET @bool_error = 1;
END

-- users.must_change_password
IF EXISTS (SELECT 1 FROM users WHERE must_change_password NOT IN (0,1) AND must_change_password IS NOT NULL)
BEGIN
    PRINT '⚠️ users.must_change_password 存在非法值！';
    SET @bool_error = 1;
END

-- users.force_password_reset
IF EXISTS (SELECT 1 FROM users WHERE force_password_reset NOT IN (0,1) AND force_password_reset IS NOT NULL)
BEGIN
    PRINT '⚠️ users.force_password_reset 存在非法值！';
    SET @bool_error = 1;
END

-- supplier_documents.is_required
IF EXISTS (SELECT 1 FROM supplier_documents WHERE is_required NOT IN (0,1) AND is_required IS NOT NULL)
BEGIN
    PRINT '⚠️ supplier_documents.is_required 存在非法值！';
    SET @bool_error = 1;
END

-- rfqs.is_line_item_mode
IF EXISTS (SELECT 1 FROM rfqs WHERE is_line_item_mode NOT IN (0,1) AND is_line_item_mode IS NOT NULL)
BEGIN
    PRINT '⚠️ rfqs.is_line_item_mode 存在非法值！';
    SET @bool_error = 1;
END

-- quotes.is_latest, has_special_tariff
IF EXISTS (SELECT 1 FROM quotes WHERE is_latest NOT IN (0,1) AND is_latest IS NOT NULL)
BEGIN
    PRINT '⚠️ quotes.is_latest 存在非法值！';
    SET @bool_error = 1;
END
IF EXISTS (SELECT 1 FROM quotes WHERE has_special_tariff NOT IN (0,1) AND has_special_tariff IS NOT NULL)
BEGIN
    PRINT '⚠️ quotes.has_special_tariff 存在非法值！';
    SET @bool_error = 1;
END

-- quote_line_items.has_special_tariff
IF EXISTS (SELECT 1 FROM quote_line_items WHERE has_special_tariff NOT IN (0,1) AND has_special_tariff IS NOT NULL)
BEGIN
    PRINT '⚠️ quote_line_items.has_special_tariff 存在非法值！';
    SET @bool_error = 1;
END

-- audit_log.is_sensitive, immutable
IF EXISTS (SELECT 1 FROM audit_log WHERE is_sensitive NOT IN (0,1) AND is_sensitive IS NOT NULL)
BEGIN
    PRINT '⚠️ audit_log.is_sensitive 存在非法值！';
    SET @bool_error = 1;
END
IF EXISTS (SELECT 1 FROM audit_log WHERE immutable NOT IN (0,1) AND immutable IS NOT NULL)
BEGIN
    PRINT '⚠️ audit_log.immutable 存在非法值！';
    SET @bool_error = 1;
END

IF @bool_error = 0
BEGIN
    PRINT '✅ 所有布尔字段值验证通过';
END
ELSE
BEGIN
    PRINT '⚠️ 存在非法布尔值，请修正后再继续';
    -- 暂停执行
    RAISERROR('布尔字段验证失败', 16, 1);
END

/* ---------- 1.4.1 suppliers.temp_account_status 值映射检查 ---------- */
PRINT '';
PRINT '1.4.1 检查 suppliers.temp_account_status 未映射值...';

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('suppliers') AND name = 'temp_account_status')
BEGIN
    SELECT
        temp_account_status,
        COUNT(*) AS cnt
    FROM suppliers
    WHERE temp_account_status IS NOT NULL
      AND LOWER(LTRIM(RTRIM(temp_account_status))) NOT IN ('active', 'inactive', '1', '0', 'true', 'false', 'yes', 'no')
    GROUP BY temp_account_status
    ORDER BY cnt DESC;

    IF EXISTS (
        SELECT 1
        FROM suppliers
        WHERE temp_account_status IS NOT NULL
          AND LOWER(LTRIM(RTRIM(temp_account_status))) NOT IN ('active', 'inactive', '1', '0', 'true', 'false', 'yes', 'no')
    )
    BEGIN
        PRINT '⚠️ suppliers.temp_account_status 存在未映射值，请确认是否需要补充映射';
    END
    ELSE
    BEGIN
        PRINT '✅ suppliers.temp_account_status 未发现未映射值';
    END
END
ELSE
BEGIN
    PRINT 'ℹ️ suppliers.temp_account_status 列不存在，跳过';
END

/* ---------- 1.5 状态字段值分布 ---------- */
PRINT '';
PRINT '1.5 记录状态字段值分布（用于CHECK约束验证）...';

PRINT 'users.status: ' + (SELECT STRING_AGG(status, ', ') FROM (SELECT DISTINCT status FROM users) t);
PRINT 'suppliers.status: ' + (SELECT STRING_AGG(status, ', ') FROM (SELECT DISTINCT status FROM suppliers) t);
PRINT 'rfqs.status: ' + (SELECT STRING_AGG(status, ', ') FROM (SELECT DISTINCT status FROM rfqs) t);
PRINT 'quotes.status: ' + (SELECT STRING_AGG(status, ', ') FROM (SELECT DISTINCT status FROM quotes) t);
PRINT 'invoices.status: ' + (SELECT STRING_AGG(status, ', ') FROM (SELECT DISTINCT status FROM invoices) t);

PRINT '';
PRINT '========================================';
PRINT '【第一阶段】检查完成';
PRINT '确认以上检查全部通过后，执行【第二阶段】';
PRINT '========================================';

-- 暂停，等待用户确认
PRINT '请检查第一阶段输出，确认环境安全后继续';
PRINT '如需继续，请手动执行: SET NOEXEC OFF，然后从【第二阶段】开始执行';
SET NOEXEC ON;

GO

-- =====================================================
-- 【第二阶段】布尔类型修复 (INT → BIT)
-- 事务控制：每表独立事务，失败不影响其他
-- =====================================================

PRINT '========================================';
PRINT '【第二阶段】布尔类型修复';
PRINT '========================================';
PRINT '';

BEGIN TRY
    BEGIN TRANSACTION T1_users;

    PRINT '修复 users.is_active...';
    ALTER TABLE users ALTER COLUMN is_active BIT NULL;
    PRINT '✅ users.is_active';

    PRINT '修复 users.must_change_password...';
    ALTER TABLE users ALTER COLUMN must_change_password BIT NULL;
    PRINT '✅ users.must_change_password';

    PRINT '修复 users.force_password_reset...';
    ALTER TABLE users ALTER COLUMN force_password_reset BIT NULL;
    PRINT '✅ users.force_password_reset';

    COMMIT TRANSACTION T1_users;
    PRINT '✅ users 表布尔类型修复完成';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T1_users;
    PRINT '❌ users 表修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION T2_suppliers;

    PRINT '修复 suppliers.temp_account_status...';

    -- 检查列是否存在
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('suppliers') AND name = 'temp_account_status')
    BEGIN
        -- 先添加新列
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('suppliers') AND name = 'temp_account_status_new')
        BEGIN
            ALTER TABLE suppliers ADD temp_account_status_new BIT NULL;
        END

        -- 转换数据
        UPDATE suppliers SET temp_account_status_new =
            CASE WHEN temp_account_status IN ('active', '1', 'true', 'yes') THEN 1
                 WHEN temp_account_status IN ('inactive', '0', 'false', 'no') THEN 0
                 ELSE NULL END;

        -- 删除旧列
        ALTER TABLE suppliers DROP COLUMN temp_account_status;
        EXEC sp_rename 'suppliers.temp_account_status_new', 'temp_account_status', 'COLUMN';
        PRINT '✅ suppliers.temp_account_status';
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ suppliers.temp_account_status 列不存在，跳过';
    END

    COMMIT TRANSACTION T2_suppliers;
    PRINT '✅ suppliers 表布尔类型修复完成';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T2_suppliers;
    PRINT '❌ suppliers 表修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION T3_supplier_docs;

    PRINT '修复 supplier_documents.is_required...';
    ALTER TABLE supplier_documents ALTER COLUMN is_required BIT NULL;
    PRINT '✅ supplier_documents.is_required';

    COMMIT TRANSACTION T3_supplier_docs;
    PRINT '✅ supplier_documents 表修复完成';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T3_supplier_docs;
    PRINT '❌ supplier_documents 表修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION T4_rfqs;

    PRINT '修复 rfqs.is_line_item_mode...';
    ALTER TABLE rfqs ALTER COLUMN is_line_item_mode BIT NULL;
    PRINT '✅ rfqs.is_line_item_mode';

    COMMIT TRANSACTION T4_rfqs;
    PRINT '✅ rfqs 表修复完成';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T4_rfqs;
    PRINT '❌ rfqs 表修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION T5_quotes;

    PRINT '修复 quotes.is_latest...';
    ALTER TABLE quotes ALTER COLUMN is_latest BIT NULL;
    PRINT '✅ quotes.is_latest';

    PRINT '修复 quotes.has_special_tariff...';
    ALTER TABLE quotes ALTER COLUMN has_special_tariff BIT NULL;
    PRINT '✅ quotes.has_special_tariff';

    COMMIT TRANSACTION T5_quotes;
    PRINT '✅ quotes 表布尔类型修复完成';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T5_quotes;
    PRINT '❌ quotes 表修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION T6_quote_items;

    PRINT '修复 quote_line_items.has_special_tariff...';
    ALTER TABLE quote_line_items ALTER COLUMN has_special_tariff BIT NULL;
    PRINT '✅ quote_line_items.has_special_tariff';

    COMMIT TRANSACTION T6_quote_items;
    PRINT '✅ quote_line_items 表修复完成';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T6_quote_items;
    PRINT '❌ quote_line_items 表修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION T7_audit;

    PRINT '修复 audit_log.is_sensitive...';
    ALTER TABLE audit_log ALTER COLUMN is_sensitive BIT NULL;
    PRINT '✅ audit_log.is_sensitive';

    PRINT '修复 audit_log.immutable...';
    ALTER TABLE audit_log ALTER COLUMN immutable BIT NULL;
    PRINT '✅ audit_log.immutable';

    COMMIT TRANSACTION T7_audit;
    PRINT '✅ audit_log 表布尔类型修复完成';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T7_audit;
    PRINT '❌ audit_log 表修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

PRINT '';
PRINT '========================================';
PRINT '【第二阶段】布尔类型修复完成';
PRINT '========================================';
GO

-- =====================================================
-- 【第三阶段】货币精度修复 (DECIMAL(18,2) → DECIMAL(18,4))
-- =====================================================

PRINT '========================================';
PRINT '【第三阶段】货币精度修复';
PRINT '========================================';
PRINT '';

BEGIN TRY
    BEGIN TRANSACTION T_rfqs_amount;

    PRINT '修复 rfqs.amount, budget_amount...';
    ALTER TABLE rfqs ALTER COLUMN amount DECIMAL(18,4) NULL;
    ALTER TABLE rfqs ALTER COLUMN budget_amount DECIMAL(18,4) NULL;
    PRINT '✅ rfqs.amount, budget_amount';

    COMMIT TRANSACTION T_rfqs_amount;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T_rfqs_amount;
    PRINT '❌ rfqs 货币精度修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION T_rfq_items;

    PRINT '修复 rfq_line_items.quantity, estimated_unit_price...';
    ALTER TABLE rfq_line_items ALTER COLUMN quantity DECIMAL(18,4) NULL;
    ALTER TABLE rfq_line_items ALTER COLUMN estimated_unit_price DECIMAL(18,4) NULL;
    PRINT '✅ rfq_line_items.quantity, estimated_unit_price';

    COMMIT TRANSACTION T_rfq_items;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T_rfq_items;
    PRINT '❌ rfq_line_items 精度修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION T_quotes_money;

    PRINT '修复 quotes 货币字段...';
    ALTER TABLE quotes ALTER COLUMN unit_price DECIMAL(18,4) NULL;
    ALTER TABLE quotes ALTER COLUMN total_amount DECIMAL(18,4) NULL;
    ALTER TABLE quotes ALTER COLUMN total_standard_cost_local DECIMAL(18,4) NULL;
    ALTER TABLE quotes ALTER COLUMN total_standard_cost_usd DECIMAL(18,4) NULL;
    ALTER TABLE quotes ALTER COLUMN total_tariff_amount_local DECIMAL(18,4) NULL;
    ALTER TABLE quotes ALTER COLUMN total_tariff_amount_usd DECIMAL(18,4) NULL;
    PRINT '✅ quotes 货币字段';

    COMMIT TRANSACTION T_quotes_money;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T_quotes_money;
    PRINT '❌ quotes 货币精度修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION T_quote_items_money;

    PRINT '修复 quote_line_items 货币字段...';
    ALTER TABLE quote_line_items ALTER COLUMN unit_price DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN minimum_order_quantity DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN standard_package_quantity DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN total_price DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN original_price_usd DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN tariff_amount_local DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN tariff_amount_usd DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN special_tariff_amount_local DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN special_tariff_amount_usd DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN standard_cost_local DECIMAL(18,4) NULL;
    ALTER TABLE quote_line_items ALTER COLUMN standard_cost_usd DECIMAL(18,4) NULL;
    PRINT '✅ quote_line_items 货币字段';

    COMMIT TRANSACTION T_quote_items_money;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T_quote_items_money;
    PRINT '❌ quote_line_items 货币精度修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

BEGIN TRY
    BEGIN TRANSACTION T_invoices;

    PRINT '修复 invoices.amount...';
    ALTER TABLE invoices ALTER COLUMN amount DECIMAL(18,4) NULL;
    PRINT '✅ invoices.amount';

    COMMIT TRANSACTION T_invoices;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T_invoices;
    PRINT '❌ invoices.amount 精度修复失败: ' + ERROR_MESSAGE();
END CATCH
GO

PRINT '';
PRINT '========================================';
PRINT '【第三阶段】货币精度修复完成';
PRINT '========================================';
GO

-- =====================================================
-- 【第四阶段】命名规范修复
-- =====================================================

PRINT '========================================';
PRINT '【第四阶段】命名规范修复';
PRINT '========================================';
PRINT '';

BEGIN TRY
    BEGIN TRANSACTION T_rename;

    -- 检查并重命名 [key] 为 config_key
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('system_config') AND name = 'key')
    BEGIN
        PRINT '重命名 system_config.[key] → config_key...';
        EXEC sp_rename 'system_config.[key]', 'config_key', 'COLUMN';
        PRINT '✅ system_config.config_key';
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ system_config.[key] 列不存在或已重命名，跳过';
    END

    COMMIT TRANSACTION T_rename;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION T_rename;
    PRINT '❌ 重命名失败: ' + ERROR_MESSAGE();
END CATCH
GO

PRINT '';
PRINT '========================================';
PRINT '【第四阶段】命名规范修复完成';
PRINT '========================================';
GO

-- =====================================================
-- 【第五阶段】补充缺失的外键索引
-- =====================================================

PRINT '========================================';
PRINT '【第五阶段】索引补充';
PRINT '========================================';
PRINT '';

BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('supplier_tags') AND name = 'IX_supplier_tags_tag_id')
    BEGIN
        CREATE NONCLUSTERED INDEX IX_supplier_tags_tag_id ON supplier_tags(tag_id);
        PRINT '✅ IX_supplier_tags_tag_id';
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ IX_supplier_tags_tag_id 已存在';
    END
END TRY
BEGIN CATCH
    PRINT '❌ IX_supplier_tags_tag_id 创建失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('rfq_attachments') AND name = 'IX_rfq_attachments_rfq_id')
    BEGIN
        CREATE NONCLUSTERED INDEX IX_rfq_attachments_rfq_id ON rfq_attachments(rfq_id);
        PRINT '✅ IX_rfq_attachments_rfq_id';
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ IX_rfq_attachments_rfq_id 已存在';
    END
END TRY
BEGIN CATCH
    PRINT '❌ IX_rfq_attachments_rfq_id 创建失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('quote_line_items') AND name = 'IX_quote_line_items_quote_id')
    BEGIN
        CREATE NONCLUSTERED INDEX IX_quote_line_items_quote_id ON quote_line_items(quote_id);
        PRINT '✅ IX_quote_line_items_quote_id';
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ IX_quote_line_items_quote_id 已存在';
    END
END TRY
BEGIN CATCH
    PRINT '❌ IX_quote_line_items_quote_id 创建失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('rfq_line_items') AND name = 'IX_rfq_line_items_status')
    BEGIN
        CREATE NONCLUSTERED INDEX IX_rfq_line_items_status ON rfq_line_items(status);
        PRINT '✅ IX_rfq_line_items_status';
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ IX_rfq_line_items_status 已存在';
    END
END TRY
BEGIN CATCH
    PRINT '❌ IX_rfq_line_items_status 创建失败: ' + ERROR_MESSAGE();
END CATCH

PRINT '';
PRINT '========================================';
PRINT '【第五阶段】索引补充完成';
PRINT '========================================';
GO

-- =====================================================
-- 【第六阶段】CHECK 约束添加
-- =====================================================

PRINT '========================================';
PRINT '【第六阶段】CHECK 约束添加';
PRINT '========================================';
PRINT '';

BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_users_status')
    BEGIN
        -- 先验证数据是否满足约束
        IF NOT EXISTS (SELECT 1 FROM users WHERE status NOT IN ('active', 'inactive', 'locked', 'pending'))
        BEGIN
            ALTER TABLE users ADD CONSTRAINT CHK_users_status
            CHECK (status IN ('active', 'inactive', 'locked', 'pending'));
            PRINT '✅ CHK_users_status';
        END
        ELSE
        BEGIN
            PRINT '⚠️ users.status 存在非法值，跳过约束创建';
            SELECT DISTINCT status FROM users WHERE status NOT IN ('active', 'inactive', 'locked', 'pending');
        END
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ CHK_users_status 已存在';
    END
END TRY
BEGIN CATCH
    PRINT '❌ CHK_users_status 创建失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_suppliers_status')
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM suppliers WHERE status NOT IN ('pending', 'approved', 'rejected', 'suspended', 'active'))
        BEGIN
            ALTER TABLE suppliers ADD CONSTRAINT CHK_suppliers_status
            CHECK (status IN ('pending', 'approved', 'rejected', 'suspended', 'active'));
            PRINT '✅ CHK_suppliers_status';
        END
        ELSE
        BEGIN
            PRINT '⚠️ suppliers.status 存在非法值，跳过约束创建';
            SELECT DISTINCT status FROM suppliers WHERE status NOT IN ('pending', 'approved', 'rejected', 'suspended', 'active');
        END
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ CHK_suppliers_status 已存在';
    END
END TRY
BEGIN CATCH
    PRINT '❌ CHK_suppliers_status 创建失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_rfqs_status')
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM rfqs WHERE status NOT IN ('draft', 'pending', 'approved', 'rejected', 'closed', 'cancelled'))
        BEGIN
            ALTER TABLE rfqs ADD CONSTRAINT CHK_rfqs_status
            CHECK (status IN ('draft', 'pending', 'approved', 'rejected', 'closed', 'cancelled'));
            PRINT '✅ CHK_rfqs_status';
        END
        ELSE
        BEGIN
            PRINT '⚠️ rfqs.status 存在非法值，跳过约束创建';
            SELECT DISTINCT status FROM rfqs WHERE status NOT IN ('draft', 'pending', 'approved', 'rejected', 'closed', 'cancelled');
        END
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ CHK_rfqs_status 已存在';
    END
END TRY
BEGIN CATCH
    PRINT '❌ CHK_rfqs_status 创建失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_quotes_status')
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM quotes WHERE status NOT IN ('draft', 'submitted', 'withdrawn', 'accepted', 'rejected'))
        BEGIN
            ALTER TABLE quotes ADD CONSTRAINT CHK_quotes_status
            CHECK (status IN ('draft', 'submitted', 'withdrawn', 'accepted', 'rejected'));
            PRINT '✅ CHK_quotes_status';
        END
        ELSE
        BEGIN
            PRINT '⚠️ quotes.status 存在非法值，跳过约束创建';
            SELECT DISTINCT status FROM quotes WHERE status NOT IN ('draft', 'submitted', 'withdrawn', 'accepted', 'rejected');
        END
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ CHK_quotes_status 已存在';
    END
END TRY
BEGIN CATCH
    PRINT '❌ CHK_quotes_status 创建失败: ' + ERROR_MESSAGE();
END CATCH

BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CHK_invoices_status')
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM invoices WHERE status NOT IN ('pending', 'approved', 'rejected', 'paid', 'cancelled'))
        BEGIN
            ALTER TABLE invoices ADD CONSTRAINT CHK_invoices_status
            CHECK (status IN ('pending', 'approved', 'rejected', 'paid', 'cancelled'));
            PRINT '✅ CHK_invoices_status';
        END
        ELSE
        BEGIN
            PRINT '⚠️ invoices.status 存在非法值，跳过约束创建';
            SELECT DISTINCT status FROM invoices WHERE status NOT IN ('pending', 'approved', 'rejected', 'paid', 'cancelled');
        END
    END
    ELSE
    BEGIN
        PRINT 'ℹ️ CHK_invoices_status 已存在';
    END
END TRY
BEGIN CATCH
    PRINT '❌ CHK_invoices_status 创建失败: ' + ERROR_MESSAGE();
END CATCH

PRINT '';
PRINT '========================================';
PRINT '【第六阶段】CHECK 约束添加完成';
PRINT '========================================';
GO

-- =====================================================
-- 【第七阶段】修复验证
-- =====================================================

PRINT '';
PRINT '╔════════════════════════════════════════╗';
PRINT '║       【第七阶段】修复验证报告          ║';
PRINT '╚════════════════════════════════════════╝';
PRINT '';

/* 验证1：布尔类型 */
PRINT '【验证1】布尔类型检查:';
SELECT
    t.name AS table_name,
    c.name AS column_name,
    ty.name AS data_type,
    CASE WHEN ty.name = 'bit' THEN '✅' ELSE '❌' END AS status
FROM sys.columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.name IN ('is_active', 'must_change_password', 'force_password_reset',
                 'is_required', 'is_line_item_mode', 'is_latest',
                 'has_special_tariff', 'is_sensitive', 'immutable',
                 'signature_seal', 'is_active')
  AND t.is_ms_shipped = 0
ORDER BY t.name, c.name;

/* 验证2：货币精度 */
PRINT '';
PRINT '【验证2】货币精度检查:';
SELECT
    t.name AS table_name,
    c.name AS column_name,
    ty.name AS data_type,
    c.max_length AS max_len,
    c.scale AS scale,
    CASE WHEN ty.name = 'decimal' AND c.precision = 18 AND c.scale = 4 THEN '✅' ELSE '❌' END AS status
FROM sys.columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.name IN ('amount', 'total_amount', 'budget_amount', 'unit_price',
                 'quantity', 'estimated_unit_price', 'total_price',
                 'total_standard_cost_local', 'total_standard_cost_usd',
                 'total_tariff_amount_local', 'total_tariff_amount_usd',
                 'tariff_amount_local', 'tariff_amount_usd',
                 'standard_cost_local', 'standard_cost_usd',
                 'original_price_usd')
  AND ty.name = 'decimal'
  AND t.is_ms_shipped = 0
ORDER BY t.name, c.name;

/* 验证3：索引 */
PRINT '';
PRINT '【验证3】缺失索引检查:';
SELECT
    t.name AS table_name,
    c.name AS column_name,
    '❌ 缺失索引' AS status
FROM sys.foreign_key_columns fkc
INNER JOIN sys.tables t ON fkc.parent_object_id = t.object_id
INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE t.is_ms_shipped = 0
  AND NOT EXISTS (
    SELECT 1 FROM sys.index_columns ic
    INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE ic.object_id = fkc.parent_object_id AND ic.column_id = fkc.parent_column_id
  )
ORDER BY t.name, c.name;

IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = OBJECT_ID('supplier_tags') AND c.name = 'tag_id'
               AND EXISTS (SELECT 1 FROM sys.index_columns WHERE object_id = c.object_id AND column_id = c.column_id))
BEGIN
    SELECT 'supplier_tags.tag_id' AS missing_index;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = OBJECT_ID('rfq_attachments') AND c.name = 'rfq_id'
               AND EXISTS (SELECT 1 FROM sys.index_columns WHERE object_id = c.object_id AND column_id = c.column_id))
BEGIN
    SELECT 'rfq_attachments.rfq_id' AS missing_index;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = OBJECT_ID('quote_line_items') AND c.name = 'quote_id'
               AND EXISTS (SELECT 1 FROM sys.index_columns WHERE object_id = c.object_id AND column_id = c.column_id))
BEGIN
    SELECT 'quote_line_items.quote_id' AS missing_index;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('supplier_tags') AND name = 'IX_supplier_tags_tag_id'
    UNION ALL
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('rfq_attachments') AND name = 'IX_rfq_attachments_rfq_id'
    UNION ALL
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('quote_line_items') AND name = 'IX_quote_line_items_quote_id'
    UNION ALL
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('rfq_line_items') AND name = 'IX_rfq_line_items_status'
)
BEGIN
    PRINT '⚠️ 仍有索引缺失';
END
ELSE
BEGIN
    PRINT '✅ 所有关键索引已创建';
END

/* 验证4：CHECK约束 */
PRINT '';
PRINT '【验证4】CHECK 约束检查:';
SELECT
    t.name AS table_name,
    chk.name AS constraint_name,
    chk.definition AS definition,
    CASE WHEN chk.name IS NOT NULL THEN '✅' ELSE '❌' END AS status
FROM sys.tables t
LEFT JOIN sys.check_constraints chk ON t.object_id = chk.parent_object_id
WHERE t.name IN ('users', 'suppliers', 'rfqs', 'quotes', 'invoices')
ORDER BY t.name;

/* 验证5：命名规范 */
PRINT '';
PRINT '【验证5】命名规范检查:';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('system_config') AND name = 'key')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('system_config') AND name = 'config_key')
    BEGIN
        PRINT '✅ system_config.config_key (已重命名)';
    END
END
ELSE
BEGIN
    PRINT '❌ system_config.[key] 仍存在';
END

PRINT '';
PRINT '╔════════════════════════════════════════╗';
PRINT '║       修复验证完成                      ║';
PRINT '╚════════════════════════════════════════╝';
GO

-- =====================================================
-- 【回滚脚本】如需回滚，执行以下语句
-- =====================================================

PRINT '';
PRINT '════════════════════════════════════════';
PRINT '【回滚脚本】如需回滚，执行以下语句';
PRINT '（请根据实际修改选择性执行）';
PRINT '════════════════════════════════════════';
PRINT '';
PRINT '-- 恢复布尔类型 (BIT → INT)';
PRINT 'ALTER TABLE users ALTER COLUMN is_active INT NULL;';
PRINT 'ALTER TABLE supplier_documents ALTER COLUMN is_required INT NULL;';
PRINT 'ALTER TABLE rfqs ALTER COLUMN is_line_item_mode INT NULL;';
PRINT 'ALTER TABLE quotes ALTER COLUMN is_latest INT NULL;';
PRINT 'ALTER TABLE quotes ALTER COLUMN has_special_tariff INT NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN has_special_tariff INT NULL;';
PRINT 'ALTER TABLE audit_log ALTER COLUMN is_sensitive INT NULL;';
PRINT 'ALTER TABLE audit_log ALTER COLUMN immutable INT NULL;';
PRINT 'ALTER TABLE users ALTER COLUMN must_change_password INT NULL;';
PRINT 'ALTER TABLE users ALTER COLUMN force_password_reset INT NULL;';
PRINT 'ALTER TABLE suppliers ALTER COLUMN temp_account_status NVARCHAR(50) NULL;';
PRINT '';
PRINT '-- 恢复货币精度 (DECIMAL(18,4) → DECIMAL(18,2))';
PRINT 'ALTER TABLE rfqs ALTER COLUMN amount DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE rfqs ALTER COLUMN budget_amount DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE rfq_line_items ALTER COLUMN quantity DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE rfq_line_items ALTER COLUMN estimated_unit_price DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quotes ALTER COLUMN unit_price DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quotes ALTER COLUMN total_amount DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quotes ALTER COLUMN total_standard_cost_local DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quotes ALTER COLUMN total_standard_cost_usd DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quotes ALTER COLUMN total_tariff_amount_local DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quotes ALTER COLUMN total_tariff_amount_usd DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN unit_price DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN minimum_order_quantity DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN standard_package_quantity DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN total_price DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN original_price_usd DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN tariff_amount_local DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN tariff_amount_usd DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN special_tariff_amount_local DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN special_tariff_amount_usd DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN standard_cost_local DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE quote_line_items ALTER COLUMN standard_cost_usd DECIMAL(18,2) NULL;';
PRINT 'ALTER TABLE invoices ALTER COLUMN amount DECIMAL(18,2) NULL;';
PRINT '';
PRINT '-- 删除 CHECK 约束';
PRINT 'ALTER TABLE users DROP CONSTRAINT IF EXISTS CHK_users_status;';
PRINT 'ALTER TABLE suppliers DROP CONSTRAINT IF EXISTS CHK_suppliers_status;';
PRINT 'ALTER TABLE rfqs DROP CONSTRAINT IF EXISTS CHK_rfqs_status;';
PRINT 'ALTER TABLE quotes DROP CONSTRAINT IF EXISTS CHK_quotes_status;';
PRINT 'ALTER TABLE invoices DROP CONSTRAINT IF EXISTS CHK_invoices_status;';
PRINT '';
PRINT '-- 删除索引';
PRINT 'DROP INDEX IF EXISTS IX_supplier_tags_tag_id ON supplier_tags;';
PRINT 'DROP INDEX IF EXISTS IX_rfq_attachments_rfq_id ON rfq_attachments;';
PRINT 'DROP INDEX IF EXISTS IX_quote_line_items_quote_id ON quote_line_items;';
PRINT 'DROP INDEX IF EXISTS IX_rfq_line_items_status ON rfq_line_items;';
PRINT '';
PRINT '-- 重命名列';
PRINT 'EXEC sp_rename ''system_config.config_key'', ''key'', ''COLUMN'';';
GO
