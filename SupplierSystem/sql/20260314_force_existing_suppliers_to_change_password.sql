-- Force all existing supplier users to change password on next login.
-- Scope: temp_supplier, formal_supplier, supplier
-- Excludes: tracking, deleted, frozen

SELECT COUNT(*) AS target_supplier_user_count
FROM dbo.users
WHERE LOWER(role) IN ('temp_supplier', 'formal_supplier', 'supplier')
  AND (status IS NULL OR LOWER(status) NOT IN ('deleted', 'frozen'));

UPDATE dbo.users
SET must_change_password = 1,
    updated_at = CONVERT(nvarchar(64), SYSUTCDATETIME(), 127)
WHERE LOWER(role) IN ('temp_supplier', 'formal_supplier', 'supplier')
  AND (status IS NULL OR LOWER(status) NOT IN ('deleted', 'frozen'));

SELECT COUNT(*) AS updated_supplier_user_count
FROM dbo.users
WHERE LOWER(role) IN ('temp_supplier', 'formal_supplier', 'supplier')
  AND (status IS NULL OR LOWER(status) NOT IN ('deleted', 'frozen'))
  AND must_change_password = 1;
