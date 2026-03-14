IF COL_LENGTH('rfqs', 'view_link') IS NULL
BEGIN
    ALTER TABLE rfqs ADD view_link NVARCHAR(1000) NULL;
END
GO
