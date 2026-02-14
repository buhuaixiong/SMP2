-- =====================================================
-- 仅添加发票表（数据库已有其他表且含数据）
-- SQL Server 版本
-- =====================================================

USE SupplierSystemDev;
GO

-- 发票表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'invoices')
BEGIN
    CREATE TABLE invoices (
        id INT IDENTITY(1,1) PRIMARY KEY,
        supplier_id INT NOT NULL,
        rfq_id INT,
        order_id INT,
        invoice_number NVARCHAR(100) NOT NULL,
        invoice_date DATETIME NOT NULL,
        amount DECIMAL(18,2) NOT NULL,
        currency NVARCHAR(10),
        tax_rate NVARCHAR(50),
        invoice_type NVARCHAR(50),
        type NVARCHAR(50),
        pre_payment_proof NVARCHAR(500),
        signature_seal BIT DEFAULT 0,
        status NVARCHAR(50) DEFAULT 'pending',
        review_notes NVARCHAR(1000),
        rejection_reason NVARCHAR(500),
        reviewed_by INT,
        reviewed_at DATETIME,
        file_name NVARCHAR(255),
        stored_file_name NVARCHAR(255),
        file_path NVARCHAR(500),
        file_size BIGINT,
        file_type NVARCHAR(100),
        created_at DATETIME NOT NULL,
        updated_at DATETIME,
        created_by INT NOT NULL,
        FOREIGN KEY (supplier_id) REFERENCES suppliers(id)
    );

    CREATE INDEX IX_invoices_supplier_id ON invoices(supplier_id);
    CREATE INDEX IX_invoices_status ON invoices(status);
    CREATE INDEX IX_invoices_created_at ON invoices(created_at);

    PRINT '发票表创建完成！';
END
ELSE
BEGIN
    PRINT '发票表已存在，无需创建。';
END
GO
