-- Fix validity date offset for upgrade documents stored in files table.
-- The adjustment adds the server timezone offset minutes to validFrom/validTo.
-- Optional: set @cutoff to limit updates to records uploaded before a fix date.

DECLARE @offsetMinutes INT = DATEPART(TZOFFSET, SYSDATETIMEOFFSET());
DECLARE @cutoff DATETIME2 = NULL;

WITH target_files AS (
    SELECT f.id
    FROM files f
    INNER JOIN supplier_upgrade_documents d ON d.fileId = f.id
    WHERE (f.validFrom IS NOT NULL OR f.validTo IS NOT NULL)
      AND (@cutoff IS NULL OR f.uploadTime < @cutoff)
)
UPDATE f
SET validFrom = CASE
        WHEN f.validFrom IS NULL THEN NULL
        ELSE DATEADD(MINUTE, @offsetMinutes, f.validFrom)
    END,
    validTo = CASE
        WHEN f.validTo IS NULL THEN NULL
        ELSE DATEADD(MINUTE, @offsetMinutes, f.validTo)
    END
FROM files f
INNER JOIN target_files t ON t.id = f.id;

SELECT @@ROWCOUNT AS affected_rows;
