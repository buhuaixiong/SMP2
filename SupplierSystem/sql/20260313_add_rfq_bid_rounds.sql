IF OBJECT_ID('dbo.rfq_bid_rounds', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.rfq_bid_rounds (
        id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        rfq_id BIGINT NOT NULL,
        round_number INT NOT NULL,
        bid_deadline NVARCHAR(64) NULL,
        status NVARCHAR(32) NOT NULL CONSTRAINT DF_rfq_bid_rounds_status DEFAULT ('draft'),
        opened_at NVARCHAR(64) NULL,
        closed_at NVARCHAR(64) NULL,
        created_by NVARCHAR(128) NULL,
        created_at NVARCHAR(64) NULL,
        updated_at NVARCHAR(64) NULL,
        extension_reason NVARCHAR(1000) NULL,
        started_from_round_id BIGINT NULL
    );

    CREATE UNIQUE INDEX IX_rfq_bid_rounds_rfq_id_round_number
        ON dbo.rfq_bid_rounds (rfq_id, round_number);

    CREATE INDEX IX_rfq_bid_rounds_rfq_id_status
        ON dbo.rfq_bid_rounds (rfq_id, status);
END;

IF COL_LENGTH('dbo.quotes', 'bid_round_id') IS NULL
BEGIN
    ALTER TABLE dbo.quotes ADD bid_round_id BIGINT NULL;
    CREATE INDEX IX_quotes_rfq_id_bid_round_id_supplier_id_is_latest
        ON dbo.quotes (rfq_id, bid_round_id, supplier_id, is_latest);
END;

IF COL_LENGTH('dbo.supplier_rfq_invitations', 'bid_round_id') IS NULL
BEGIN
    ALTER TABLE dbo.supplier_rfq_invitations ADD bid_round_id BIGINT NULL;
    CREATE INDEX IX_supplier_rfq_invitations_rfq_id_bid_round_id_supplier_id
        ON dbo.supplier_rfq_invitations (rfq_id, bid_round_id, supplier_id);
END;

IF COL_LENGTH('dbo.rfq_price_audit', 'bid_round_id') IS NULL
BEGIN
    ALTER TABLE dbo.rfq_price_audit ADD bid_round_id BIGINT NULL;
END;

IF COL_LENGTH('dbo.rfq_price_audit', 'round_number') IS NULL
BEGIN
    ALTER TABLE dbo.rfq_price_audit ADD round_number INT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_rfq_price_audit_rfq_id_bid_round_id_supplier_id_rfq_line_item_id'
      AND object_id = OBJECT_ID('dbo.rfq_price_audit'))
BEGIN
    CREATE INDEX IX_rfq_price_audit_rfq_id_bid_round_id_supplier_id_rfq_line_item_id
        ON dbo.rfq_price_audit (rfq_id, bid_round_id, supplier_id, rfq_line_item_id);
END;

WITH rounds AS (
    SELECT
        rfq.id AS rfq_id,
        rfq.valid_until,
        rfq.created_by,
        rfq.created_at,
        rfq.updated_at,
        rfq.review_completed_at,
        rfq.status,
        ROW_NUMBER() OVER (PARTITION BY rfq.id ORDER BY rfq.id) AS row_num
    FROM dbo.rfqs rfq
)
INSERT INTO dbo.rfq_bid_rounds (
    rfq_id,
    round_number,
    bid_deadline,
    status,
    opened_at,
    closed_at,
    created_by,
    created_at,
    updated_at
)
SELECT
    rounds.rfq_id,
    1,
    rounds.valid_until,
    CASE
        WHEN LOWER(ISNULL(rounds.status, '')) IN ('under_review', 'closed') THEN 'closed'
        WHEN LOWER(ISNULL(rounds.status, '')) IN ('published', 'in_progress') THEN 'published'
        ELSE 'draft'
    END,
    CASE
        WHEN LOWER(ISNULL(rounds.status, '')) IN ('under_review', 'closed') THEN rounds.valid_until
        ELSE NULL
    END,
    CASE
        WHEN LOWER(ISNULL(rounds.status, '')) = 'closed' THEN ISNULL(rounds.review_completed_at, rounds.updated_at)
        ELSE NULL
    END,
    rounds.created_by,
    rounds.created_at,
    rounds.updated_at
FROM rounds
WHERE rounds.row_num = 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.rfq_bid_rounds existing
      WHERE existing.rfq_id = rounds.rfq_id
  );

UPDATE q
SET q.bid_round_id = br.id
FROM dbo.quotes q
JOIN dbo.rfq_bid_rounds br ON br.rfq_id = q.rfq_id AND br.round_number = 1
WHERE q.bid_round_id IS NULL;

UPDATE inv
SET inv.bid_round_id = br.id
FROM dbo.supplier_rfq_invitations inv
JOIN dbo.rfq_bid_rounds br ON br.rfq_id = inv.rfq_id AND br.round_number = 1
WHERE inv.bid_round_id IS NULL;

UPDATE audit
SET audit.bid_round_id = br.id,
    audit.round_number = 1
FROM dbo.rfq_price_audit audit
JOIN dbo.rfq_bid_rounds br ON br.rfq_id = audit.rfq_id AND br.round_number = 1
WHERE audit.bid_round_id IS NULL;
