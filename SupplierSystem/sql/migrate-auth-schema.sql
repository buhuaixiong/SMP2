-- SupplierSystem schema in-place migration (SQL Server)
-- Safe to run multiple times. Keeps existing data.

USE SupplierSystemDev;
GO

-- =====================================================
-- Users table: add missing columns and align IDs if needed
-- =====================================================
IF OBJECT_ID('dbo.users', 'U') IS NULL
BEGIN
    PRINT 'Table dbo.users not found; skip users migration.';
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.users', 'status') IS NULL
        ALTER TABLE dbo.users ADD status NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.users', 'auth_version') IS NULL
        ALTER TABLE dbo.users ADD auth_version INT NOT NULL CONSTRAINT DF_users_auth_version DEFAULT 1;
    IF COL_LENGTH('dbo.users', 'account_type') IS NULL
        ALTER TABLE dbo.users ADD account_type NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.users', 'tenant_id') IS NULL
        ALTER TABLE dbo.users ADD tenant_id NVARCHAR(64) NULL;
    IF COL_LENGTH('dbo.users', 'supplier_id') IS NULL
        ALTER TABLE dbo.users ADD supplier_id INT NULL;
    IF COL_LENGTH('dbo.users', 'temp_account_id') IS NULL
        ALTER TABLE dbo.users ADD temp_account_id INT NULL;
    IF COL_LENGTH('dbo.users', 'related_application_id') IS NULL
        ALTER TABLE dbo.users ADD related_application_id INT NULL;
    IF COL_LENGTH('dbo.users', 'initial_password_issued_at') IS NULL
        ALTER TABLE dbo.users ADD initial_password_issued_at NVARCHAR(64) NULL;
    IF COL_LENGTH('dbo.users', 'last_login_at') IS NULL
        ALTER TABLE dbo.users ADD last_login_at DATETIME2 NULL;

    IF COL_LENGTH('dbo.users', 'auth_version') IS NOT NULL
        UPDATE dbo.users SET auth_version = 1 WHERE auth_version IS NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.users') AND name = 'IX_users_email'
    )
        CREATE INDEX IX_users_email ON dbo.users(email);

    -- Optional: convert numeric/uniqueidentifier IDs to NVARCHAR(64)
    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.users')
          AND c.name = 'id'
          AND t.name IN ('int', 'bigint', 'uniqueidentifier')
    )
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM sys.foreign_key_columns fkc
            WHERE fkc.referenced_object_id = OBJECT_ID('dbo.users')
        )
        BEGIN
            PRINT 'users.id has foreign key references; skip id type conversion.';
        END
        ELSE
        BEGIN
            IF COL_LENGTH('dbo.users', 'id_new') IS NULL
                ALTER TABLE dbo.users ADD id_new NVARCHAR(64) NULL;

            EXEC(N'UPDATE dbo.users SET id_new = CAST(id AS NVARCHAR(64)) WHERE id_new IS NULL;');

            DECLARE @pk_users sysname;
            SELECT @pk_users = kc.name
            FROM sys.key_constraints kc
            WHERE kc.parent_object_id = OBJECT_ID('dbo.users')
              AND kc.type = 'PK';

            IF @pk_users IS NOT NULL
            BEGIN
                DECLARE @sql_drop_pk NVARCHAR(4000);
                SET @sql_drop_pk = N'ALTER TABLE dbo.users DROP CONSTRAINT [' + REPLACE(@pk_users, ']', ']]') + N']';
                EXEC (@sql_drop_pk);
            END

            ALTER TABLE dbo.users DROP COLUMN id;
            EXEC sp_rename 'dbo.users.id_new', 'id', 'COLUMN';

            ALTER TABLE dbo.users ALTER COLUMN id NVARCHAR(64) NOT NULL;
            ALTER TABLE dbo.users ADD CONSTRAINT PK_users PRIMARY KEY (id);
        END
    END
END
GO

-- =====================================================
-- Token blacklist: add reason and align user_id + timestamp columns
-- =====================================================
IF OBJECT_ID('dbo.token_blacklist', 'U') IS NULL
BEGIN
    PRINT 'Table dbo.token_blacklist not found; skip token_blacklist migration.';
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.token_blacklist', 'reason') IS NULL
        ALTER TABLE dbo.token_blacklist ADD reason NVARCHAR(200) NULL;

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.token_blacklist')
          AND c.name = 'user_id'
          AND t.name NOT IN ('nvarchar', 'varchar')
    )
        EXEC(N'ALTER TABLE dbo.token_blacklist ALTER COLUMN user_id NVARCHAR(64) NULL');

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.token_blacklist')
          AND c.name IN ('blacklisted_at', 'expires_at')
          AND t.name NOT IN ('nvarchar', 'varchar')
    )
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.token_blacklist') AND name = 'IX_token_blacklist_expires'
        )
            DROP INDEX IX_token_blacklist_expires ON dbo.token_blacklist;

        DECLARE @df_token_blacklisted_at sysname;
        SELECT @df_token_blacklisted_at = dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.token_blacklist')
          AND c.name = 'blacklisted_at';
        IF @df_token_blacklisted_at IS NOT NULL
        BEGIN
            DECLARE @sql_drop_token_blacklisted NVARCHAR(4000);
            SET @sql_drop_token_blacklisted = N'ALTER TABLE dbo.token_blacklist DROP CONSTRAINT [' + REPLACE(@df_token_blacklisted_at, ']', ']]') + N']';
            EXEC (@sql_drop_token_blacklisted);
        END

        IF COL_LENGTH('dbo.token_blacklist', 'blacklisted_at') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.token_blacklist ALTER COLUMN blacklisted_at NVARCHAR(64) NOT NULL');
        IF COL_LENGTH('dbo.token_blacklist', 'expires_at') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.token_blacklist ALTER COLUMN expires_at NVARCHAR(64) NOT NULL');

        IF COL_LENGTH('dbo.token_blacklist', 'blacklisted_at') IS NOT NULL
            EXEC(N'UPDATE dbo.token_blacklist SET blacklisted_at = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, blacklisted_at), 127) WHERE TRY_CONVERT(DATETIME2, blacklisted_at) IS NOT NULL;');

        IF COL_LENGTH('dbo.token_blacklist', 'expires_at') IS NOT NULL
            EXEC(N'UPDATE dbo.token_blacklist SET expires_at = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, expires_at), 127) WHERE TRY_CONVERT(DATETIME2, expires_at) IS NOT NULL;');
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.token_blacklist') AND name = 'IX_token_blacklist_expires'
    )
        CREATE INDEX IX_token_blacklist_expires ON dbo.token_blacklist(expires_at);

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.token_blacklist') AND name = 'IX_token_blacklist_token_hash'
    )
        CREATE INDEX IX_token_blacklist_token_hash ON dbo.token_blacklist(token_hash);
END
GO

-- =====================================================
-- Token blacklist: convert bigint identity to int identity
-- =====================================================
IF OBJECT_ID('dbo.token_blacklist', 'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.token_blacklist')
          AND c.name = 'id'
          AND t.name = 'bigint'
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.token_blacklist WHERE id > 2147483647)
        BEGIN
            PRINT 'token_blacklist.id exceeds INT range; skip id conversion.';
        END
        ELSE
        BEGIN
            IF OBJECT_ID('dbo.token_blacklist_tmp', 'U') IS NOT NULL
                DROP TABLE dbo.token_blacklist_tmp;

            CREATE TABLE dbo.token_blacklist_tmp (
                id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                token_hash NVARCHAR(255) NOT NULL,
                user_id NVARCHAR(64) NULL,
                blacklisted_at NVARCHAR(64) NOT NULL,
                expires_at NVARCHAR(64) NOT NULL,
                reason NVARCHAR(200) NULL
            );

            SET IDENTITY_INSERT dbo.token_blacklist_tmp ON;
            INSERT INTO dbo.token_blacklist_tmp (id, token_hash, user_id, blacklisted_at, expires_at, reason)
            SELECT CAST(id AS INT), token_hash, user_id, blacklisted_at, expires_at, reason
            FROM dbo.token_blacklist;
            SET IDENTITY_INSERT dbo.token_blacklist_tmp OFF;

            DROP TABLE dbo.token_blacklist;
            EXEC sp_rename 'dbo.token_blacklist_tmp', 'token_blacklist';

            CREATE INDEX IX_token_blacklist_expires ON dbo.token_blacklist(expires_at);
            CREATE INDEX IX_token_blacklist_token_hash ON dbo.token_blacklist(token_hash);
        END
    END
END
GO

-- =====================================================
-- Active sessions: align user_id + timestamp columns
-- =====================================================
IF OBJECT_ID('dbo.active_sessions', 'U') IS NULL
BEGIN
    PRINT 'Table dbo.active_sessions not found; skip active_sessions migration.';
END
ELSE
BEGIN
    DECLARE @alter_active BIT = 0;

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.active_sessions')
          AND c.name = 'user_id'
          AND t.name NOT IN ('nvarchar', 'varchar')
    )
        SET @alter_active = 1;

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.active_sessions')
          AND c.name IN ('issued_at', 'expires_at', 'created_at')
          AND t.name NOT IN ('nvarchar', 'varchar')
    )
        SET @alter_active = 1;

    IF @alter_active = 1
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.active_sessions') AND name = 'IX_active_sessions_user_id'
        )
            DROP INDEX IX_active_sessions_user_id ON dbo.active_sessions;
        IF EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.active_sessions') AND name = 'IX_active_sessions_expires'
        )
            DROP INDEX IX_active_sessions_expires ON dbo.active_sessions;

        DECLARE @df_active_sessions_created_at sysname;
        SELECT @df_active_sessions_created_at = dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.active_sessions')
          AND c.name = 'created_at';
        IF @df_active_sessions_created_at IS NOT NULL
        BEGIN
            DECLARE @sql_drop_active_created NVARCHAR(4000);
            SET @sql_drop_active_created = N'ALTER TABLE dbo.active_sessions DROP CONSTRAINT [' + REPLACE(@df_active_sessions_created_at, ']', ']]') + N']';
            EXEC (@sql_drop_active_created);
        END

        IF COL_LENGTH('dbo.active_sessions', 'user_id') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.active_sessions ALTER COLUMN user_id NVARCHAR(64) NOT NULL');
        IF COL_LENGTH('dbo.active_sessions', 'issued_at') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.active_sessions ALTER COLUMN issued_at NVARCHAR(64) NOT NULL');
        IF COL_LENGTH('dbo.active_sessions', 'expires_at') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.active_sessions ALTER COLUMN expires_at NVARCHAR(64) NOT NULL');
        IF COL_LENGTH('dbo.active_sessions', 'created_at') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.active_sessions ALTER COLUMN created_at NVARCHAR(64) NULL');

        IF COL_LENGTH('dbo.active_sessions', 'issued_at') IS NOT NULL
            EXEC(N'UPDATE dbo.active_sessions SET issued_at = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, issued_at), 127) WHERE TRY_CONVERT(DATETIME2, issued_at) IS NOT NULL;');

        IF COL_LENGTH('dbo.active_sessions', 'expires_at') IS NOT NULL
            EXEC(N'UPDATE dbo.active_sessions SET expires_at = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, expires_at), 127) WHERE TRY_CONVERT(DATETIME2, expires_at) IS NOT NULL;');

        IF COL_LENGTH('dbo.active_sessions', 'created_at') IS NOT NULL
            EXEC(N'UPDATE dbo.active_sessions SET created_at = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, created_at), 127) WHERE TRY_CONVERT(DATETIME2, created_at) IS NOT NULL;');
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.active_sessions') AND name = 'IX_active_sessions_user_id'
    )
        CREATE INDEX IX_active_sessions_user_id ON dbo.active_sessions(user_id);

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.active_sessions') AND name = 'IX_active_sessions_expires'
    )
        CREATE INDEX IX_active_sessions_expires ON dbo.active_sessions(expires_at);

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.active_sessions') AND name = 'IX_active_sessions_token_hash'
    )
        CREATE INDEX IX_active_sessions_token_hash ON dbo.active_sessions(token_hash);
END
GO

-- =====================================================
-- Active sessions: convert bigint identity to int identity
-- =====================================================
IF OBJECT_ID('dbo.active_sessions', 'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.active_sessions')
          AND c.name = 'id'
          AND t.name = 'bigint'
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.active_sessions WHERE id > 2147483647)
        BEGIN
            PRINT 'active_sessions.id exceeds INT range; skip id conversion.';
        END
        ELSE
        BEGIN
            IF OBJECT_ID('dbo.active_sessions_tmp', 'U') IS NOT NULL
                DROP TABLE dbo.active_sessions_tmp;

            CREATE TABLE dbo.active_sessions_tmp (
                id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                user_id NVARCHAR(64) NOT NULL,
                token_hash NVARCHAR(255) NOT NULL,
                issued_at NVARCHAR(64) NOT NULL,
                expires_at NVARCHAR(64) NOT NULL,
                ip_address NVARCHAR(50) NULL,
                user_agent NVARCHAR(500) NULL,
                created_at NVARCHAR(64) NULL
            );

            SET IDENTITY_INSERT dbo.active_sessions_tmp ON;
            INSERT INTO dbo.active_sessions_tmp (id, user_id, token_hash, issued_at, expires_at, ip_address, user_agent, created_at)
            SELECT CAST(id AS INT), user_id, token_hash, issued_at, expires_at, ip_address, user_agent, created_at
            FROM dbo.active_sessions;
            SET IDENTITY_INSERT dbo.active_sessions_tmp OFF;

            DROP TABLE dbo.active_sessions;
            EXEC sp_rename 'dbo.active_sessions_tmp', 'active_sessions';

            CREATE INDEX IX_active_sessions_user_id ON dbo.active_sessions(user_id);
            CREATE INDEX IX_active_sessions_expires ON dbo.active_sessions(expires_at);
            CREATE INDEX IX_active_sessions_token_hash ON dbo.active_sessions(token_hash);
        END
    END
END
GO

-- =====================================================
-- Core tables: rename legacy snake_case to camelCase
-- =====================================================

IF OBJECT_ID('dbo.suppliers', 'U') IS NULL
BEGIN
    PRINT 'Table dbo.suppliers not found; skip suppliers migration.';
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.suppliers', 'company_name') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'companyName') IS NULL
        EXEC sp_rename 'dbo.suppliers.company_name', 'companyName', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'company_id') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'companyId') IS NULL
        EXEC sp_rename 'dbo.suppliers.company_id', 'companyId', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'contact_person') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'contactPerson') IS NULL
        EXEC sp_rename 'dbo.suppliers.contact_person', 'contactPerson', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'contact_phone') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'contactPhone') IS NULL
        EXEC sp_rename 'dbo.suppliers.contact_phone', 'contactPhone', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'contact_email') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'contactEmail') IS NULL
        EXEC sp_rename 'dbo.suppliers.contact_email', 'contactEmail', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'current_approver') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'currentApprover') IS NULL
        EXEC sp_rename 'dbo.suppliers.current_approver', 'currentApprover', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'created_by') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'createdBy') IS NULL
        EXEC sp_rename 'dbo.suppliers.created_by', 'createdBy', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'created_at') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'createdAt') IS NULL
        EXEC sp_rename 'dbo.suppliers.created_at', 'createdAt', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'bank_account') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'bankAccount') IS NULL
        EXEC sp_rename 'dbo.suppliers.bank_account', 'bankAccount', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'payment_terms') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'paymentTerms') IS NULL
        EXEC sp_rename 'dbo.suppliers.payment_terms', 'paymentTerms', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'credit_rating') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'creditRating') IS NULL
        EXEC sp_rename 'dbo.suppliers.credit_rating', 'creditRating', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'service_category') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'serviceCategory') IS NULL
        EXEC sp_rename 'dbo.suppliers.service_category', 'serviceCategory', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'compliance_status') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'complianceStatus') IS NULL
        EXEC sp_rename 'dbo.suppliers.compliance_status', 'complianceStatus', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'compliance_notes') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'complianceNotes') IS NULL
        EXEC sp_rename 'dbo.suppliers.compliance_notes', 'complianceNotes', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'compliance_owner') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'complianceOwner') IS NULL
        EXEC sp_rename 'dbo.suppliers.compliance_owner', 'complianceOwner', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'compliance_reviewed_at') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'complianceReviewedAt') IS NULL
        EXEC sp_rename 'dbo.suppliers.compliance_reviewed_at', 'complianceReviewedAt', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'financial_contact') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'financialContact') IS NULL
        EXEC sp_rename 'dbo.suppliers.financial_contact', 'financialContact', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'payment_currency') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'paymentCurrency') IS NULL
        EXEC sp_rename 'dbo.suppliers.payment_currency', 'paymentCurrency', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'fax_number') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'faxNumber') IS NULL
        EXEC sp_rename 'dbo.suppliers.fax_number', 'faxNumber', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'business_registration_number') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'businessRegistrationNumber') IS NULL
        EXEC sp_rename 'dbo.suppliers.business_registration_number', 'businessRegistrationNumber', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'updated_at') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'updatedAt') IS NULL
        EXEC sp_rename 'dbo.suppliers.updated_at', 'updatedAt', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'profile_completion') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'profileCompletion') IS NULL
        EXEC sp_rename 'dbo.suppliers.profile_completion', 'profileCompletion', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'document_completion') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'documentCompletion') IS NULL
        EXEC sp_rename 'dbo.suppliers.document_completion', 'documentCompletion', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'completion_score') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'completionScore') IS NULL
        EXEC sp_rename 'dbo.suppliers.completion_score', 'completionScore', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'completion_status') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'completionStatus') IS NULL
        EXEC sp_rename 'dbo.suppliers.completion_status', 'completionStatus', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'completion_last_updated') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'completionLastUpdated') IS NULL
        EXEC sp_rename 'dbo.suppliers.completion_last_updated', 'completionLastUpdated', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'temp_account_user_id') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'tempAccountUserId') IS NULL
        EXEC sp_rename 'dbo.suppliers.temp_account_user_id', 'tempAccountUserId', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'temp_account_status') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'tempAccountStatus') IS NULL
        EXEC sp_rename 'dbo.suppliers.temp_account_status', 'tempAccountStatus', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'temp_account_expires_at') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'tempAccountExpiresAt') IS NULL
        EXEC sp_rename 'dbo.suppliers.temp_account_expires_at', 'tempAccountExpiresAt', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'baseline_version') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'baselineVersion') IS NULL
        EXEC sp_rename 'dbo.suppliers.baseline_version', 'baselineVersion', 'COLUMN';
    IF COL_LENGTH('dbo.suppliers', 'supplier_code') IS NOT NULL AND COL_LENGTH('dbo.suppliers', 'supplierCode') IS NULL
        EXEC sp_rename 'dbo.suppliers.supplier_code', 'supplierCode', 'COLUMN';

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.suppliers')
          AND c.name IN ('createdAt', 'updatedAt', 'complianceReviewedAt', 'completionLastUpdated', 'tempAccountExpiresAt')
          AND t.name NOT IN ('nvarchar', 'varchar', 'datetime', 'datetime2', 'smalldatetime', 'date', 'datetimeoffset')
    )
    BEGIN
        DECLARE @df_suppliers_created_at sysname;
        SELECT @df_suppliers_created_at = dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.suppliers')
          AND c.name = 'createdAt';
        IF @df_suppliers_created_at IS NOT NULL
        BEGIN
            DECLARE @sql_drop_suppliers_created NVARCHAR(4000);
            SET @sql_drop_suppliers_created = N'ALTER TABLE dbo.suppliers DROP CONSTRAINT [' + REPLACE(@df_suppliers_created_at, ']', ']]') + N']';
            EXEC (@sql_drop_suppliers_created);
        END

        IF COL_LENGTH('dbo.suppliers', 'createdAt') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.suppliers ALTER COLUMN createdAt NVARCHAR(64) NULL');
        IF COL_LENGTH('dbo.suppliers', 'updatedAt') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.suppliers ALTER COLUMN updatedAt NVARCHAR(64) NULL');
        IF COL_LENGTH('dbo.suppliers', 'complianceReviewedAt') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.suppliers ALTER COLUMN complianceReviewedAt NVARCHAR(64) NULL');
        IF COL_LENGTH('dbo.suppliers', 'completionLastUpdated') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.suppliers ALTER COLUMN completionLastUpdated NVARCHAR(64) NULL');
        IF COL_LENGTH('dbo.suppliers', 'tempAccountExpiresAt') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.suppliers ALTER COLUMN tempAccountExpiresAt NVARCHAR(64) NULL');

        IF COL_LENGTH('dbo.suppliers', 'createdAt') IS NOT NULL
            EXEC(N'UPDATE dbo.suppliers SET createdAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, createdAt), 127) WHERE TRY_CONVERT(DATETIME2, createdAt) IS NOT NULL;');
        IF COL_LENGTH('dbo.suppliers', 'updatedAt') IS NOT NULL
            EXEC(N'UPDATE dbo.suppliers SET updatedAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, updatedAt), 127) WHERE TRY_CONVERT(DATETIME2, updatedAt) IS NOT NULL;');
        IF COL_LENGTH('dbo.suppliers', 'complianceReviewedAt') IS NOT NULL
            EXEC(N'UPDATE dbo.suppliers SET complianceReviewedAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, complianceReviewedAt), 127) WHERE TRY_CONVERT(DATETIME2, complianceReviewedAt) IS NOT NULL;');
        IF COL_LENGTH('dbo.suppliers', 'completionLastUpdated') IS NOT NULL
            EXEC(N'UPDATE dbo.suppliers SET completionLastUpdated = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, completionLastUpdated), 127) WHERE TRY_CONVERT(DATETIME2, completionLastUpdated) IS NOT NULL;');
        IF COL_LENGTH('dbo.suppliers', 'tempAccountExpiresAt') IS NOT NULL
            EXEC(N'UPDATE dbo.suppliers SET tempAccountExpiresAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, tempAccountExpiresAt), 127) WHERE TRY_CONVERT(DATETIME2, tempAccountExpiresAt) IS NOT NULL;');
    END
END
GO

IF OBJECT_ID('dbo.tag_defs', 'U') IS NULL
BEGIN
    PRINT 'Table dbo.tag_defs not found; skip tag_defs migration.';
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.tag_defs', 'created_at') IS NOT NULL AND COL_LENGTH('dbo.tag_defs', 'createdAt') IS NULL
        EXEC sp_rename 'dbo.tag_defs.created_at', 'createdAt', 'COLUMN';

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.tag_defs')
          AND c.name = 'createdAt'
          AND t.name NOT IN ('nvarchar', 'varchar')
    )
    BEGIN
        EXEC(N'ALTER TABLE dbo.tag_defs ALTER COLUMN createdAt NVARCHAR(64) NULL');
        IF COL_LENGTH('dbo.tag_defs', 'createdAt') IS NOT NULL
            EXEC(N'UPDATE dbo.tag_defs SET createdAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, createdAt), 127) WHERE TRY_CONVERT(DATETIME2, createdAt) IS NOT NULL;');
    END
END
GO

IF OBJECT_ID('dbo.supplier_tags', 'U') IS NULL
BEGIN
    PRINT 'Table dbo.supplier_tags not found; skip supplier_tags migration.';
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.supplier_tags', 'supplier_id') IS NOT NULL AND COL_LENGTH('dbo.supplier_tags', 'supplierId') IS NULL
        EXEC sp_rename 'dbo.supplier_tags.supplier_id', 'supplierId', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_tags', 'tag_id') IS NOT NULL AND COL_LENGTH('dbo.supplier_tags', 'tagId') IS NULL
        EXEC sp_rename 'dbo.supplier_tags.tag_id', 'tagId', 'COLUMN';
END
GO

IF OBJECT_ID('dbo.supplier_documents', 'U') IS NULL
BEGIN
    PRINT 'Table dbo.supplier_documents not found; skip supplier_documents migration.';
END
ELSE
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.supplier_documents') AND name = 'IX_supplier_documents_expires_at'
    )
        DROP INDEX IX_supplier_documents_expires_at ON dbo.supplier_documents;

    IF COL_LENGTH('dbo.supplier_documents', 'supplier_id') IS NOT NULL AND COL_LENGTH('dbo.supplier_documents', 'supplierId') IS NULL
        EXEC sp_rename 'dbo.supplier_documents.supplier_id', 'supplierId', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_documents', 'doc_type') IS NOT NULL AND COL_LENGTH('dbo.supplier_documents', 'docType') IS NULL
        EXEC sp_rename 'dbo.supplier_documents.doc_type', 'docType', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_documents', 'stored_name') IS NOT NULL AND COL_LENGTH('dbo.supplier_documents', 'storedName') IS NULL
        EXEC sp_rename 'dbo.supplier_documents.stored_name', 'storedName', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_documents', 'original_name') IS NOT NULL AND COL_LENGTH('dbo.supplier_documents', 'originalName') IS NULL
        EXEC sp_rename 'dbo.supplier_documents.original_name', 'originalName', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_documents', 'file_size') IS NOT NULL AND COL_LENGTH('dbo.supplier_documents', 'fileSize') IS NULL
        EXEC sp_rename 'dbo.supplier_documents.file_size', 'fileSize', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_documents', 'uploaded_at') IS NOT NULL AND COL_LENGTH('dbo.supplier_documents', 'uploadedAt') IS NULL
        EXEC sp_rename 'dbo.supplier_documents.uploaded_at', 'uploadedAt', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_documents', 'uploaded_by') IS NOT NULL AND COL_LENGTH('dbo.supplier_documents', 'uploadedBy') IS NULL
        EXEC sp_rename 'dbo.supplier_documents.uploaded_by', 'uploadedBy', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_documents', 'valid_from') IS NOT NULL AND COL_LENGTH('dbo.supplier_documents', 'validFrom') IS NULL
        EXEC sp_rename 'dbo.supplier_documents.valid_from', 'validFrom', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_documents', 'expires_at') IS NOT NULL AND COL_LENGTH('dbo.supplier_documents', 'expiresAt') IS NULL
        EXEC sp_rename 'dbo.supplier_documents.expires_at', 'expiresAt', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_documents', 'is_required') IS NOT NULL AND COL_LENGTH('dbo.supplier_documents', 'isRequired') IS NULL
        EXEC sp_rename 'dbo.supplier_documents.is_required', 'isRequired', 'COLUMN';

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.supplier_documents')
          AND c.name IN ('uploadedAt', 'validFrom', 'expiresAt')
          AND (t.name NOT IN ('nvarchar', 'varchar') OR c.max_length = -1)
    )
    BEGIN
        IF COL_LENGTH('dbo.supplier_documents', 'uploadedAt') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.supplier_documents ALTER COLUMN uploadedAt NVARCHAR(64) NULL');
        IF COL_LENGTH('dbo.supplier_documents', 'validFrom') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.supplier_documents ALTER COLUMN validFrom NVARCHAR(64) NULL');
        IF COL_LENGTH('dbo.supplier_documents', 'expiresAt') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.supplier_documents ALTER COLUMN expiresAt NVARCHAR(64) NULL');

        IF COL_LENGTH('dbo.supplier_documents', 'uploadedAt') IS NOT NULL
            EXEC(N'UPDATE dbo.supplier_documents SET uploadedAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, uploadedAt), 127) WHERE TRY_CONVERT(DATETIME2, uploadedAt) IS NOT NULL;');
        IF COL_LENGTH('dbo.supplier_documents', 'validFrom') IS NOT NULL
            EXEC(N'UPDATE dbo.supplier_documents SET validFrom = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, validFrom), 127) WHERE TRY_CONVERT(DATETIME2, validFrom) IS NOT NULL;');
        IF COL_LENGTH('dbo.supplier_documents', 'expiresAt') IS NOT NULL
            EXEC(N'UPDATE dbo.supplier_documents SET expiresAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, expiresAt), 127) WHERE TRY_CONVERT(DATETIME2, expiresAt) IS NOT NULL;');
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.supplier_documents') AND name = 'IX_supplier_documents_expires_at'
    )
        CREATE INDEX IX_supplier_documents_expires_at ON dbo.supplier_documents(expiresAt);
END
GO

IF OBJECT_ID('dbo.supplier_drafts', 'U') IS NULL
BEGIN
    PRINT 'Table dbo.supplier_drafts not found; skip supplier_drafts migration.';
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.supplier_drafts', 'supplier_id') IS NOT NULL AND COL_LENGTH('dbo.supplier_drafts', 'supplierId') IS NULL
        EXEC sp_rename 'dbo.supplier_drafts.supplier_id', 'supplierId', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_drafts', 'draft_data') IS NOT NULL AND COL_LENGTH('dbo.supplier_drafts', 'draftData') IS NULL
        EXEC sp_rename 'dbo.supplier_drafts.draft_data', 'draftData', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_drafts', 'created_at') IS NOT NULL AND COL_LENGTH('dbo.supplier_drafts', 'createdAt') IS NULL
        EXEC sp_rename 'dbo.supplier_drafts.created_at', 'createdAt', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_drafts', 'updated_at') IS NOT NULL AND COL_LENGTH('dbo.supplier_drafts', 'updatedAt') IS NULL
        EXEC sp_rename 'dbo.supplier_drafts.updated_at', 'updatedAt', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_drafts', 'created_by') IS NOT NULL AND COL_LENGTH('dbo.supplier_drafts', 'createdBy') IS NULL
        EXEC sp_rename 'dbo.supplier_drafts.created_by', 'createdBy', 'COLUMN';
    IF COL_LENGTH('dbo.supplier_drafts', 'updated_by') IS NOT NULL AND COL_LENGTH('dbo.supplier_drafts', 'updatedBy') IS NULL
        EXEC sp_rename 'dbo.supplier_drafts.updated_by', 'updatedBy', 'COLUMN';

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.supplier_drafts')
          AND c.name IN ('createdAt', 'updatedAt')
          AND t.name NOT IN ('nvarchar', 'varchar')
    )
    BEGIN
        IF COL_LENGTH('dbo.supplier_drafts', 'createdAt') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.supplier_drafts ALTER COLUMN createdAt NVARCHAR(64) NULL');
        IF COL_LENGTH('dbo.supplier_drafts', 'updatedAt') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.supplier_drafts ALTER COLUMN updatedAt NVARCHAR(64) NULL');

        IF COL_LENGTH('dbo.supplier_drafts', 'createdAt') IS NOT NULL
            EXEC(N'UPDATE dbo.supplier_drafts SET createdAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, createdAt), 127) WHERE TRY_CONVERT(DATETIME2, createdAt) IS NOT NULL;');
        IF COL_LENGTH('dbo.supplier_drafts', 'updatedAt') IS NOT NULL
            EXEC(N'UPDATE dbo.supplier_drafts SET updatedAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, updatedAt), 127) WHERE TRY_CONVERT(DATETIME2, updatedAt) IS NOT NULL;');
    END
END
GO

-- =====================================================
-- Notifications: align column names and types
-- =====================================================
IF OBJECT_ID('dbo.notifications', 'U') IS NULL
BEGIN
    PRINT 'Table dbo.notifications not found; skip notifications migration.';
END
ELSE
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.notifications') AND name = 'IX_notifications_user_id'
    )
        DROP INDEX IX_notifications_user_id ON dbo.notifications;

    IF COL_LENGTH('dbo.notifications', 'user_id') IS NOT NULL AND COL_LENGTH('dbo.notifications', 'userId') IS NULL
        EXEC sp_rename 'dbo.notifications.user_id', 'userId', 'COLUMN';
    IF COL_LENGTH('dbo.notifications', 'related_entity_type') IS NOT NULL AND COL_LENGTH('dbo.notifications', 'relatedEntityType') IS NULL
        EXEC sp_rename 'dbo.notifications.related_entity_type', 'relatedEntityType', 'COLUMN';
    IF COL_LENGTH('dbo.notifications', 'created_at') IS NOT NULL AND COL_LENGTH('dbo.notifications', 'createdAt') IS NULL
        EXEC sp_rename 'dbo.notifications.created_at', 'createdAt', 'COLUMN';
    IF COL_LENGTH('dbo.notifications', 'read_at') IS NOT NULL AND COL_LENGTH('dbo.notifications', 'readAt') IS NULL
        EXEC sp_rename 'dbo.notifications.read_at', 'readAt', 'COLUMN';

    IF COL_LENGTH('dbo.notifications', 'supplierId') IS NULL
        ALTER TABLE dbo.notifications ADD supplierId INT NULL;
    IF COL_LENGTH('dbo.notifications', 'priority') IS NULL
        ALTER TABLE dbo.notifications ADD priority NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.notifications', 'expiresAt') IS NULL
        ALTER TABLE dbo.notifications ADD expiresAt NVARCHAR(64) NULL;
    IF COL_LENGTH('dbo.notifications', 'metadata') IS NULL
        ALTER TABLE dbo.notifications ADD metadata NVARCHAR(MAX) NULL;
    IF COL_LENGTH('dbo.notifications', 'relatedEntityId') IS NULL
        ALTER TABLE dbo.notifications ADD relatedEntityId INT NULL;

    IF COL_LENGTH('dbo.notifications', 'related_entity_id') IS NOT NULL AND COL_LENGTH('dbo.notifications', 'relatedEntityId') IS NOT NULL
    BEGIN
        EXEC(N'UPDATE dbo.notifications SET relatedEntityId = TRY_CONVERT(INT, related_entity_id) WHERE relatedEntityId IS NULL AND TRY_CONVERT(INT, related_entity_id) IS NOT NULL;');
    END

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.notifications')
          AND c.name = 'userId'
          AND (t.name NOT IN ('nvarchar', 'varchar') OR c.max_length = -1)
    )
        EXEC(N'ALTER TABLE dbo.notifications ALTER COLUMN userId NVARCHAR(64) NULL');

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.notifications')
          AND c.name IN ('createdAt', 'readAt')
          AND (t.name NOT IN ('nvarchar', 'varchar') OR c.max_length = -1)
    )
    BEGIN
        DECLARE @df_notifications_created_at sysname;
        SELECT @df_notifications_created_at = dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.notifications')
          AND c.name = 'createdAt';
        IF @df_notifications_created_at IS NOT NULL
        BEGIN
            DECLARE @sql_drop_notifications_created NVARCHAR(4000);
            SET @sql_drop_notifications_created = N'ALTER TABLE dbo.notifications DROP CONSTRAINT [' + REPLACE(@df_notifications_created_at, ']', ']]') + N']';
            EXEC (@sql_drop_notifications_created);
        END

        IF COL_LENGTH('dbo.notifications', 'createdAt') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.notifications ALTER COLUMN createdAt NVARCHAR(64) NULL');
        IF COL_LENGTH('dbo.notifications', 'readAt') IS NOT NULL
            EXEC(N'ALTER TABLE dbo.notifications ALTER COLUMN readAt NVARCHAR(64) NULL');

        IF COL_LENGTH('dbo.notifications', 'createdAt') IS NOT NULL
            EXEC(N'UPDATE dbo.notifications SET createdAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, createdAt), 127) WHERE TRY_CONVERT(DATETIME2, createdAt) IS NOT NULL;');
        IF COL_LENGTH('dbo.notifications', 'readAt') IS NOT NULL
            EXEC(N'UPDATE dbo.notifications SET readAt = CONVERT(VARCHAR(33), TRY_CONVERT(DATETIME2, readAt), 127) WHERE TRY_CONVERT(DATETIME2, readAt) IS NOT NULL;');
    END

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.notifications') AND name = 'IX_notifications_user_id'
    )
        CREATE INDEX IX_notifications_user_id ON dbo.notifications(userId);
END
GO

-- =====================================================
-- Audit log: align snake_case columns to EF property names
-- =====================================================
IF OBJECT_ID('dbo.audit_log', 'U') IS NULL
BEGIN
    PRINT 'Table dbo.audit_log not found; skip audit_log migration.';
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.audit_log', 'actor_id') IS NOT NULL AND COL_LENGTH('dbo.audit_log', 'ActorId') IS NULL
        EXEC sp_rename 'dbo.audit_log.actor_id', 'ActorId', 'COLUMN';
    IF COL_LENGTH('dbo.audit_log', 'actor_name') IS NOT NULL AND COL_LENGTH('dbo.audit_log', 'ActorName') IS NULL
        EXEC sp_rename 'dbo.audit_log.actor_name', 'ActorName', 'COLUMN';
    IF COL_LENGTH('dbo.audit_log', 'entity_type') IS NOT NULL AND COL_LENGTH('dbo.audit_log', 'EntityType') IS NULL
        EXEC sp_rename 'dbo.audit_log.entity_type', 'EntityType', 'COLUMN';
    IF COL_LENGTH('dbo.audit_log', 'entity_id') IS NOT NULL AND COL_LENGTH('dbo.audit_log', 'EntityId') IS NULL
        EXEC sp_rename 'dbo.audit_log.entity_id', 'EntityId', 'COLUMN';
    IF COL_LENGTH('dbo.audit_log', 'ip_address') IS NOT NULL AND COL_LENGTH('dbo.audit_log', 'IpAddress') IS NULL
        EXEC sp_rename 'dbo.audit_log.ip_address', 'IpAddress', 'COLUMN';
    IF COL_LENGTH('dbo.audit_log', 'hash_chain_value') IS NOT NULL AND COL_LENGTH('dbo.audit_log', 'HashChainValue') IS NULL
        EXEC sp_rename 'dbo.audit_log.hash_chain_value', 'HashChainValue', 'COLUMN';
    IF COL_LENGTH('dbo.audit_log', 'is_sensitive') IS NOT NULL AND COL_LENGTH('dbo.audit_log', 'IsSensitive') IS NULL
        EXEC sp_rename 'dbo.audit_log.is_sensitive', 'IsSensitive', 'COLUMN';
END
GO

-- =====================================================
-- Audit log: convert bigint identity to int identity
-- =====================================================
IF OBJECT_ID('dbo.audit_log', 'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID('dbo.audit_log')
          AND c.name = 'id'
          AND t.name = 'bigint'
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.audit_log WHERE id > 2147483647)
        BEGIN
            PRINT 'audit_log.id exceeds INT range; skip id conversion.';
        END
        ELSE
        BEGIN
            IF OBJECT_ID('dbo.audit_log_tmp', 'U') IS NOT NULL
                DROP TABLE dbo.audit_log_tmp;

            CREATE TABLE dbo.audit_log_tmp (
                id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                ActorId NVARCHAR(100) NULL,
                ActorName NVARCHAR(200) NULL,
                EntityType NVARCHAR(100) NULL,
                EntityId NVARCHAR(100) NULL,
                action NVARCHAR(100) NULL,
                changes NVARCHAR(MAX) NULL,
                summary NVARCHAR(500) NULL,
                IpAddress NVARCHAR(50) NULL,
                IsSensitive INT NULL,
                immutable INT NULL,
                HashChainValue NVARCHAR(100) NULL,
                createdAt DATETIME NULL
            );

            SET IDENTITY_INSERT dbo.audit_log_tmp ON;
            INSERT INTO dbo.audit_log_tmp (id, ActorId, ActorName, EntityType, EntityId, action, changes, summary, IpAddress, IsSensitive, immutable, HashChainValue, createdAt)
            SELECT CAST(id AS INT), ActorId, ActorName, EntityType, EntityId, action, changes, summary, IpAddress, IsSensitive, immutable, HashChainValue, createdAt
            FROM dbo.audit_log;
            SET IDENTITY_INSERT dbo.audit_log_tmp OFF;

            DROP TABLE dbo.audit_log;
            EXEC sp_rename 'dbo.audit_log_tmp', 'audit_log';
        END
    END
END
GO

-- =====================================================
-- Audit archive metadata: create table if missing
-- =====================================================
IF OBJECT_ID('dbo.audit_archive_metadata', 'U') IS NULL
BEGIN
    CREATE TABLE audit_archive_metadata (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AuditLogId INT NOT NULL,
        ArchiveFilePath NVARCHAR(1024) NOT NULL,
        FileHash NVARCHAR(256) NOT NULL,
        ArchiveDate NVARCHAR(64) NULL,
        VerifiedAt NVARCHAR(64) NULL,
        VerificationStatus NVARCHAR(50) NULL
    );

    CREATE INDEX IX_audit_archive_metadata_log ON audit_archive_metadata(AuditLogId);
END
GO

-- =====================================================
-- Organizational units & purchasing groups (auth payload dependencies)
-- =====================================================
IF OBJECT_ID('dbo.organizational_units', 'U') IS NULL
BEGIN
    CREATE TABLE organizational_units (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TenantId NVARCHAR(64) NOT NULL,
        Code NVARCHAR(50) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        [Type] NVARCHAR(50) NULL,
        ParentId INT NULL,
        Level INT NOT NULL DEFAULT 0,
        Path NVARCHAR(500) NULL,
        Description NVARCHAR(500) NULL,
        AdminIds NVARCHAR(MAX) NULL,
        [Function] NVARCHAR(100) NULL,
        Category NVARCHAR(100) NULL,
        Region NVARCHAR(100) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt NVARCHAR(64) NULL,
        CreatedBy NVARCHAR(200) NULL,
        UpdatedAt NVARCHAR(64) NULL,
        UpdatedBy NVARCHAR(200) NULL,
        DeletedAt NVARCHAR(64) NULL,
        DeletedBy NVARCHAR(200) NULL
    );

    CREATE INDEX IX_organizational_units_tenant ON organizational_units(TenantId);
    CREATE INDEX IX_organizational_units_parent ON organizational_units(ParentId);
    CREATE INDEX IX_organizational_units_code ON organizational_units(Code);
END
GO

IF OBJECT_ID('dbo.organizational_unit_members', 'U') IS NULL
BEGIN
    CREATE TABLE organizational_unit_members (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TenantId NVARCHAR(64) NOT NULL,
        UnitId INT NOT NULL,
        UserId NVARCHAR(64) NOT NULL,
        Role NVARCHAR(50) NULL,
        JoinedAt NVARCHAR(64) NULL,
        AssignedBy NVARCHAR(200) NULL,
        Notes NVARCHAR(500) NULL
    );

    CREATE INDEX IX_org_unit_members_unit ON organizational_unit_members(UnitId);
    CREATE INDEX IX_org_unit_members_user ON organizational_unit_members(UserId);

    ALTER TABLE organizational_unit_members
        ADD CONSTRAINT FK_org_unit_members_unit
        FOREIGN KEY (UnitId) REFERENCES organizational_units(Id);
END
GO

IF OBJECT_ID('dbo.purchasing_groups', 'U') IS NULL
BEGIN
    CREATE TABLE purchasing_groups (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Code NVARCHAR(50) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        Description NVARCHAR(500) NULL,
        Category NVARCHAR(100) NULL,
        Region NVARCHAR(100) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt NVARCHAR(64) NULL,
        CreatedBy NVARCHAR(200) NULL,
        UpdatedAt NVARCHAR(64) NULL,
        UpdatedBy NVARCHAR(200) NULL,
        DeletedAt NVARCHAR(64) NULL,
        DeletedBy NVARCHAR(200) NULL
    );

    CREATE INDEX IX_purchasing_groups_code ON purchasing_groups(Code);
END
GO

IF OBJECT_ID('dbo.purchasing_group_members', 'U') IS NULL
BEGIN
    CREATE TABLE purchasing_group_members (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        GroupId INT NOT NULL,
        BuyerId NVARCHAR(64) NOT NULL,
        Role NVARCHAR(50) NULL,
        JoinedAt NVARCHAR(64) NULL,
        AssignedBy NVARCHAR(200) NULL,
        Notes NVARCHAR(500) NULL
    );

    CREATE INDEX IX_purchasing_group_members_group ON purchasing_group_members(GroupId);
    CREATE INDEX IX_purchasing_group_members_buyer ON purchasing_group_members(BuyerId);

    ALTER TABLE purchasing_group_members
        ADD CONSTRAINT FK_purchasing_group_members_group
        FOREIGN KEY (GroupId) REFERENCES purchasing_groups(Id);
END
GO
