-- Add missing "deleted" status for users lookup table.
IF OBJECT_ID('dbo.lkp_dbo_users_status', 'U') IS NULL
BEGIN
    PRINT 'lkp_dbo_users_status not found; skipping.';
END
ELSE IF COL_LENGTH('dbo.lkp_dbo_users_status', 'value') IS NULL
BEGIN
    PRINT 'lkp_dbo_users_status.value column not found; skipping.';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.lkp_dbo_users_status WHERE value = 'deleted')
    BEGIN
        INSERT INTO dbo.lkp_dbo_users_status (value)
        VALUES ('deleted');
        PRINT 'Inserted \"deleted\" into lkp_dbo_users_status.';
    END
    ELSE
    BEGIN
        PRINT '\"deleted\" already exists in lkp_dbo_users_status.';
    END
END
