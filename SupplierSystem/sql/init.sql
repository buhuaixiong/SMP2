-- =====================================================
-- SupplierSystem 数据库初始化脚本
-- SQL Server 版本
-- =====================================================

-- 创建数据库（如果不存在）
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'SupplierSystemDev')
BEGIN
    CREATE DATABASE SupplierSystemDev;
END
GO

USE SupplierSystemDev;
GO

-- =====================================================
-- 基础表
-- =====================================================

-- 用户表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users')
BEGIN
    CREATE TABLE users (
        id NVARCHAR(64) NOT NULL PRIMARY KEY,
        username NVARCHAR(100) NOT NULL UNIQUE,
        password_hash NVARCHAR(255) NOT NULL,
        auth_version INT NOT NULL DEFAULT 1,
        email NVARCHAR(320),
        name NVARCHAR(200) NOT NULL,
        role NVARCHAR(50) NOT NULL DEFAULT 'user',
        department NVARCHAR(100),
        status NVARCHAR(50),
        account_type NVARCHAR(50),
        tenant_id NVARCHAR(64),
        supplier_id INT,
        temp_account_id INT,
        related_application_id INT,
        must_change_password BIT DEFAULT 0,
        force_password_reset BIT DEFAULT 0,
        initial_password_issued_at NVARCHAR(64),
        last_login_at DATETIME2,
        created_at DATETIME2 DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2,
        last_password_change DATETIME,
        failed_login_attempts INT DEFAULT 0,
        locked_until DATETIME,
        is_active BIT DEFAULT 1
    );

    CREATE INDEX IX_users_username ON users(username);
    CREATE INDEX IX_users_email ON users(email);
    CREATE INDEX IX_users_role ON users(role);
END
GO

-- 供应商表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'suppliers')
BEGIN
    CREATE TABLE suppliers (
        id INT IDENTITY(1,1) PRIMARY KEY,
        company_name NVARCHAR(255) NOT NULL,
        company_id NVARCHAR(100) NOT NULL,
        contact_person NVARCHAR(200),
        contact_phone NVARCHAR(50),
        contact_email NVARCHAR(255),
        category NVARCHAR(100),
        address NVARCHAR(500),
        status NVARCHAR(50) DEFAULT 'pending',
        current_approver NVARCHAR(200),
        created_by NVARCHAR(200),
        created_at DATETIME,
        notes NVARCHAR(1000),
        bank_account NVARCHAR(100),
        payment_terms NVARCHAR(100),
        credit_rating NVARCHAR(50),
        service_category NVARCHAR(100),
        region NVARCHAR(100),
        importance NVARCHAR(50),
        compliance_status NVARCHAR(50),
        compliance_notes NVARCHAR(1000),
        compliance_owner NVARCHAR(200),
        compliance_reviewed_at DATETIME,
        financial_contact NVARCHAR(200),
        payment_currency NVARCHAR(10),
        fax_number NVARCHAR(50),
        business_registration_number NVARCHAR(100),
        updated_at DATETIME,
        stage NVARCHAR(50),
        profile_completion DECIMAL(5,2),
        document_completion DECIMAL(5,2),
        completion_score DECIMAL(5,2),
        completion_status NVARCHAR(50),
        completion_last_updated DATETIME,
        temp_account_user_id INT,
        temp_account_status NVARCHAR(50),
        temp_account_expires_at DATETIME,
        baseline_version INT DEFAULT 1,
        supplier_code NVARCHAR(50)
    );

    CREATE INDEX IX_suppliers_status ON suppliers(status);
    CREATE INDEX IX_suppliers_company_name ON suppliers(company_name);
    CREATE INDEX IX_suppliers_stage ON suppliers(stage);
END
GO

-- 标签定义表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tag_defs')
BEGIN
    CREATE TABLE tag_defs (
        id INT IDENTITY(1,1) PRIMARY KEY,
        name NVARCHAR(100) NOT NULL,
        description NVARCHAR(500),
        color NVARCHAR(20),
        created_at DATETIME
    );
END
GO

-- 供应商标签关联表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'supplier_tags')
BEGIN
    CREATE TABLE supplier_tags (
        supplier_id INT NOT NULL,
        tag_id INT NOT NULL,
        PRIMARY KEY (supplier_id, tag_id),
        FOREIGN KEY (supplier_id) REFERENCES suppliers(id) ON DELETE CASCADE,
        FOREIGN KEY (tag_id) REFERENCES tag_defs(id) ON DELETE CASCADE
    );
END
GO

-- 供应商文档表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'supplier_documents')
BEGIN
    CREATE TABLE supplier_documents (
        id INT IDENTITY(1,1) PRIMARY KEY,
        supplier_id INT NOT NULL,
        doc_type NVARCHAR(100) NOT NULL,
        stored_name NVARCHAR(255) NOT NULL,
        original_name NVARCHAR(255),
        file_size BIGINT,
        uploaded_at DATETIME,
        uploaded_by NVARCHAR(200),
        valid_from DATETIME,
        expires_at DATETIME,
        status NVARCHAR(50) DEFAULT 'active',
        notes NVARCHAR(500),
        category NVARCHAR(100),
        is_required INT DEFAULT 0,
        FOREIGN KEY (supplier_id) REFERENCES suppliers(id) ON DELETE CASCADE
    );

    CREATE INDEX IX_supplier_documents_supplier_id ON supplier_documents(supplier_id);
    CREATE INDEX IX_supplier_documents_expires_at ON supplier_documents(expires_at);
END
GO

-- 供应商草稿表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'supplier_drafts')
BEGIN
    CREATE TABLE supplier_drafts (
        id INT IDENTITY(1,1) PRIMARY KEY,
        supplier_id INT NOT NULL,
        draft_data NVARCHAR(MAX),
        created_at DATETIME,
        updated_at DATETIME,
        created_by NVARCHAR(200),
        updated_by NVARCHAR(200),
        FOREIGN KEY (supplier_id) REFERENCES suppliers(id) ON DELETE CASCADE
    );
END
GO

-- =====================================================
-- RFQ 相关表
-- =====================================================

-- RFQ表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'rfqs')
BEGIN
    CREATE TABLE rfqs (
        id INT IDENTITY(1,1) PRIMARY KEY,
        title NVARCHAR(500),
        description NVARCHAR(MAX),
        amount DECIMAL(18,2),
        currency NVARCHAR(10),
        delivery_period INT,
        status NVARCHAR(50) DEFAULT 'draft',
        created_by NVARCHAR(200),
        created_at DATETIME,
        updated_at DATETIME,
        material_type NVARCHAR(100),
        material_category_type NVARCHAR(50),
        is_line_item_mode INT DEFAULT 0,
        distribution_category NVARCHAR(100),
        distribution_subcategory NVARCHAR(100),
        rfq_type NVARCHAR(50),
        budget_amount DECIMAL(18,2),
        required_documents NVARCHAR(MAX),
        evaluation_criteria NVARCHAR(MAX),
        valid_until DATETIME,
        requesting_party NVARCHAR(200),
        requesting_department NVARCHAR(200),
        requirement_date DATETIME,
        detailed_parameters NVARCHAR(MAX),
        min_supplier_count INT,
        supplier_exception_note NVARCHAR(500),
        selected_quote_id INT,
        review_completed_at DATETIME,
        approval_status NVARCHAR(50),
        pr_status NVARCHAR(50),
        requisition_id INT
    );

    CREATE INDEX IX_rfqs_status ON rfqs(status);
    CREATE INDEX IX_rfqs_created_at ON rfqs(created_at);
END
GO

-- RFQ行项目表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'rfq_line_items')
BEGIN
    CREATE TABLE rfq_line_items (
        id INT IDENTITY(1,1) PRIMARY KEY,
        rfq_id INT NOT NULL,
        line_number INT,
        material_category NVARCHAR(100),
        brand NVARCHAR(200),
        item_name NVARCHAR(500),
        specifications NVARCHAR(MAX),
        quantity DECIMAL(18,4),
        unit NVARCHAR(50),
        estimated_unit_price DECIMAL(18,4),
        currency NVARCHAR(10),
        parameters NVARCHAR(MAX),
        notes NVARCHAR(500),
        created_at DATETIME,
        status NVARCHAR(50) DEFAULT 'pending',
        current_approver_role NVARCHAR(100),
        selected_quote_id INT,
        po_id INT,
        updated_at DATETIME,
        FOREIGN KEY (rfq_id) REFERENCES rfqs(id) ON DELETE CASCADE
    );

    CREATE INDEX IX_rfq_line_items_rfq_id ON rfq_line_items(rfq_id);
END
GO

-- RFQ附件表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'rfq_attachments')
BEGIN
    CREATE TABLE rfq_attachments (
        id INT IDENTITY(1,1) PRIMARY KEY,
        rfq_id INT NOT NULL,
        line_item_id INT,
        file_name NVARCHAR(255),
        file_path NVARCHAR(500),
        file_size BIGINT,
        file_type NVARCHAR(100),
        uploaded_by NVARCHAR(200),
        uploaded_at DATETIME,
        description NVARCHAR(500),
        FOREIGN KEY (rfq_id) REFERENCES rfqs(id) ON DELETE CASCADE
    );
END
GO

-- 报价表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'quotes')
BEGIN
    CREATE TABLE quotes (
        id INT IDENTITY(1,1) PRIMARY KEY,
        rfq_id INT NOT NULL,
        supplier_id INT NOT NULL,
        unit_price DECIMAL(18,4),
        total_amount DECIMAL(18,2),
        currency NVARCHAR(10),
        delivery_date DATETIME,
        payment_terms NVARCHAR(200),
        notes NVARCHAR(MAX),
        status NVARCHAR(50) DEFAULT 'draft',
        submitted_at DATETIME,
        withdrawal_reason NVARCHAR(500),
        withdrawn_at DATETIME,
        created_at DATETIME,
        updated_at DATETIME,
        brand NVARCHAR(200),
        tax_status NVARCHAR(50),
        parameters NVARCHAR(MAX),
        optional_config NVARCHAR(MAX),
        version INT DEFAULT 1,
        is_latest INT DEFAULT 1,
        modified_count INT DEFAULT 0,
        ip_address NVARCHAR(50),
        can_modify_until DATETIME,
        delivery_terms NVARCHAR(500),
        shipping_location NVARCHAR(200),
        shipping_country NVARCHAR(100),
        total_standard_cost_local DECIMAL(18,4),
        total_standard_cost_usd DECIMAL(18,4),
        total_tariff_amount_local DECIMAL(18,4),
        total_tariff_amount_usd DECIMAL(18,4),
        has_special_tariff INT DEFAULT 0,
        FOREIGN KEY (rfq_id) REFERENCES rfqs(id) ON DELETE CASCADE,
        FOREIGN KEY (supplier_id) REFERENCES suppliers(id)
    );

    CREATE INDEX IX_quotes_rfq_id ON quotes(rfq_id);
    CREATE INDEX IX_quotes_supplier_id ON quotes(supplier_id);
    CREATE INDEX IX_quotes_status ON quotes(status);
END
GO

-- 报价行项目表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'quote_line_items')
BEGIN
    CREATE TABLE quote_line_items (
        id INT IDENTITY(1,1) PRIMARY KEY,
        quote_id INT NOT NULL,
        rfq_line_item_id INT NOT NULL,
        unit_price DECIMAL(18,4),
        minimum_order_quantity DECIMAL(18,4),
        standard_package_quantity DECIMAL(18,4),
        total_price DECIMAL(18,2),
        brand NVARCHAR(200),
        tax_status NVARCHAR(50),
        delivery_date DATETIME,
        delivery_period INT,
        parameters NVARCHAR(MAX),
        notes NVARCHAR(500),
        product_origin NVARCHAR(100),
        product_group NVARCHAR(100),
        original_price_usd DECIMAL(18,4),
        exchange_rate DECIMAL(10,6),
        exchange_rate_date DATETIME,
        tariff_rate DECIMAL(5,2),
        tariff_rate_percent DECIMAL(5,2),
        tariff_amount_local DECIMAL(18,4),
        tariff_amount_usd DECIMAL(18,4),
        special_tariff_rate DECIMAL(5,2),
        special_tariff_rate_percent DECIMAL(5,2),
        special_tariff_amount_local DECIMAL(18,4),
        special_tariff_amount_usd DECIMAL(18,4),
        has_special_tariff INT DEFAULT 0,
        standard_cost_local DECIMAL(18,4),
        standard_cost_usd DECIMAL(18,4),
        standard_cost_currency NVARCHAR(10),
        calculated_at DATETIME,
        FOREIGN KEY (quote_id) REFERENCES quotes(id) ON DELETE CASCADE
    );
END
GO

-- =====================================================
-- 发票表
-- =====================================================

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
END
GO

-- =====================================================
-- 审计日志表
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'audit_log')
BEGIN
    CREATE TABLE audit_log (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        actor_id NVARCHAR(100),
        actor_name NVARCHAR(200),
        entity_type NVARCHAR(100),
        entity_id NVARCHAR(100),
        action NVARCHAR(100),
        changes NVARCHAR(MAX),
        summary NVARCHAR(500),
        ip_address NVARCHAR(50),
        is_sensitive INT DEFAULT 0,
        immutable INT DEFAULT 0,
        hash_chain_value NVARCHAR(100),
        createdAt DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_audit_log_entity ON audit_log(entity_type, entity_id);
    CREATE INDEX IX_audit_log_created_at ON audit_log(createdAt);
END
GO

-- =====================================================
-- 通知表
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'notifications')
BEGIN
    CREATE TABLE notifications (
        id INT IDENTITY(1,1) PRIMARY KEY,
        user_id INT NOT NULL,
        title NVARCHAR(200) NOT NULL,
        message NVARCHAR(1000),
        type NVARCHAR(50),
        status NVARCHAR(20) DEFAULT 'unread',
        related_entity_type NVARCHAR(100),
        related_entity_id NVARCHAR(100),
        created_at DATETIME DEFAULT GETDATE(),
        read_at DATETIME
    );

    CREATE INDEX IX_notifications_user_id ON notifications(user_id);
    CREATE INDEX IX_notifications_status ON notifications(status);
END
GO

-- =====================================================
-- 认证相关表
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'token_blacklist')
BEGIN
    CREATE TABLE token_blacklist (
        id INT IDENTITY(1,1) PRIMARY KEY,
        token_hash NVARCHAR(255) NOT NULL,
        user_id NVARCHAR(64),
        blacklisted_at NVARCHAR(64) NOT NULL,
        expires_at NVARCHAR(64) NOT NULL,
        reason NVARCHAR(200)
    );

    CREATE INDEX IX_token_blacklist_expires ON token_blacklist(expires_at);
    CREATE INDEX IX_token_blacklist_token_hash ON token_blacklist(token_hash);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'active_sessions')
BEGIN
    CREATE TABLE active_sessions (
        id INT IDENTITY(1,1) PRIMARY KEY,
        user_id NVARCHAR(64) NOT NULL,
        token_hash NVARCHAR(255) NOT NULL,
        issued_at NVARCHAR(64) NOT NULL,
        expires_at NVARCHAR(64) NOT NULL,
        ip_address NVARCHAR(50),
        user_agent NVARCHAR(500),
        created_at NVARCHAR(64)
    );

    CREATE INDEX IX_active_sessions_user_id ON active_sessions(user_id);
    CREATE INDEX IX_active_sessions_expires ON active_sessions(expires_at);
    CREATE INDEX IX_active_sessions_token_hash ON active_sessions(token_hash);
END
GO

-- =====================================================
-- 系统配置表
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'system_config')
BEGIN
    CREATE TABLE system_config (
        [key] NVARCHAR(100) PRIMARY KEY,
        value NVARCHAR(MAX),
        updatedAt NVARCHAR(64),
        updatedBy NVARCHAR(200),
        metadata NVARCHAR(MAX)
    );
END
GO

-- =====================================================
-- 插入初始管理员用户
-- =====================================================

IF NOT EXISTS (SELECT * FROM users WHERE username = 'admin')
BEGIN
    INSERT INTO users (id, username, password_hash, email, name, role, must_change_password, created_at)
    VALUES (
        'admin',
        'admin',
        '$2a$11$K7L1OJ45/4Y2nIvhRVpCe.FSmhDdWoXehVzJptJ/op0lLfFLWqKPG', -- password: admin123
        'admin@example.com',
        'System Administrator',
        'admin',
        0,
        GETDATE()
    );
END
GO

PRINT '数据库初始化完成！';
GO
