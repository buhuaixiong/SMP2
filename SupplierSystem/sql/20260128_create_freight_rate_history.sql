IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[freight_rate_history]')
      AND type IN (N'U')
)
BEGIN
    CREATE TABLE [dbo].[freight_rate_history]
    (
        [id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [route_code] NVARCHAR(100) NOT NULL,
        [route_name] NVARCHAR(200) NULL,
        [route_name_zh] NVARCHAR(200) NULL,
        [rate] FLOAT NOT NULL,
        [year] INT NOT NULL,
        [source] NVARCHAR(50) NULL,
        [notes] NVARCHAR(500) NULL,
        [created_by] NVARCHAR(100) NULL,
        [created_at] NVARCHAR(50) NULL
    );

    CREATE INDEX IX_freight_rate_history_route_year
        ON [dbo].[freight_rate_history] ([route_code], [year]);
END
