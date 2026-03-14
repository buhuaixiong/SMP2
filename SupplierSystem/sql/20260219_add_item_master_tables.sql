IF OBJECT_ID(N'dbo.item_master_records', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.item_master_records
    (
        id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        fac NVARCHAR(20) NOT NULL,
        item_number NVARCHAR(100) NOT NULL,
        vendor NVARCHAR(100) NOT NULL,
        sourcing_name NVARCHAR(200) NULL,
        owner_user_id NVARCHAR(64) NULL,
        owner_username_snapshot NVARCHAR(100) NULL,
        item_description NVARCHAR(500) NULL,
        unit NVARCHAR(30) NULL,
        moq FLOAT NULL,
        spq FLOAT NULL,
        currency NVARCHAR(10) NULL,
        price_break_1 FLOAT NULL,
        exchange_rate FLOAT NULL,
        vendor_name NVARCHAR(255) NULL,
        terms NVARCHAR(50) NULL,
        terms_desc NVARCHAR(255) NULL,
        company NVARCHAR(50) NULL,
        class NVARCHAR(50) NULL,
        raw_payload NVARCHAR(MAX) NULL,
        created_at DATETIME2(3) NOT NULL,
        updated_at DATETIME2(3) NOT NULL,
        last_import_batch_id BIGINT NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_item_master_records_fac_item_vendor'
      AND object_id = OBJECT_ID(N'dbo.item_master_records')
)
BEGIN
    CREATE UNIQUE INDEX UX_item_master_records_fac_item_vendor
        ON dbo.item_master_records(fac, item_number, vendor);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_item_master_records_owner_user_id'
      AND object_id = OBJECT_ID(N'dbo.item_master_records')
)
BEGIN
    CREATE INDEX IX_item_master_records_owner_user_id
        ON dbo.item_master_records(owner_user_id);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_item_master_records_sourcing_name'
      AND object_id = OBJECT_ID(N'dbo.item_master_records')
)
BEGIN
    CREATE INDEX IX_item_master_records_sourcing_name
        ON dbo.item_master_records(sourcing_name);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_item_master_records_fac'
      AND object_id = OBJECT_ID(N'dbo.item_master_records')
)
BEGIN
    CREATE INDEX IX_item_master_records_fac
        ON dbo.item_master_records(fac);
END;
GO

IF OBJECT_ID(N'dbo.item_master_import_batches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.item_master_import_batches
    (
        id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        file_name NVARCHAR(255) NOT NULL,
        sheet_scope NVARCHAR(500) NOT NULL,
        status NVARCHAR(30) NOT NULL,
        started_at DATETIME2(3) NOT NULL,
        finished_at DATETIME2(3) NULL,
        imported_by_user_id NVARCHAR(64) NOT NULL,
        imported_by_name NVARCHAR(200) NULL,
        inserted_count INT NOT NULL CONSTRAINT DF_item_master_import_batches_inserted_count DEFAULT(0),
        updated_count INT NOT NULL CONSTRAINT DF_item_master_import_batches_updated_count DEFAULT(0),
        warning_count INT NOT NULL CONSTRAINT DF_item_master_import_batches_warning_count DEFAULT(0),
        error_count INT NOT NULL CONSTRAINT DF_item_master_import_batches_error_count DEFAULT(0),
        summary_json NVARCHAR(MAX) NULL,
        warnings_json NVARCHAR(MAX) NULL,
        errors_json NVARCHAR(MAX) NULL
    );
END;
GO
