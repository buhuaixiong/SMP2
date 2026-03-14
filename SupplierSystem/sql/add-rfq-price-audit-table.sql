IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'rfq_price_audit')
BEGIN
    CREATE TABLE rfq_price_audit (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        rfq_id BIGINT NOT NULL,
        rfq_title NVARCHAR(500),
        rfq_created_at DATETIME2(3),
        rfq_line_item_id BIGINT NULL,
        line_number INT NULL,
        quantity DECIMAL(18,6),
        quote_id BIGINT NULL,
        supplier_id BIGINT NULL,
        supplier_name NVARCHAR(200),
        supplier_ip NVARCHAR(100),
        quoted_unit_price DECIMAL(18,6),
        quoted_total_price DECIMAL(18,6),
        quote_currency NVARCHAR(10),
        quote_submitted_at DATETIME2(3),
        approval_status NVARCHAR(50),
        approval_decision NVARCHAR(500),
        approval_decided_at DATETIME2(3),
        selected_quote_id BIGINT NULL,
        selected_supplier_id BIGINT NULL,
        selected_supplier_name NVARCHAR(200),
        selected_unit_price DECIMAL(18,6),
        selected_currency NVARCHAR(10),
        pr_filled_by NVARCHAR(200),
        pr_filled_at DATETIME2(3),
        created_at DATETIME2(3),
        updated_at DATETIME2(3)
    );

    CREATE INDEX IX_rfq_price_audit_rfq_id ON rfq_price_audit(rfq_id);
    CREATE INDEX IX_rfq_price_audit_line_item_id ON rfq_price_audit(rfq_line_item_id);
    CREATE INDEX IX_rfq_price_audit_supplier_id ON rfq_price_audit(supplier_id);
END
GO
