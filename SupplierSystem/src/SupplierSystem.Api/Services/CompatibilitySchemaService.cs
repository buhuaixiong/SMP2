using Microsoft.EntityFrameworkCore;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class CompatibilitySchemaService
{
    private readonly SupplierSystemDbContext _dbContext;

    public CompatibilitySchemaService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureTablesAsync(CancellationToken cancellationToken)
    {
        foreach (var sql in TableScripts)
        {
            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    private static readonly string[] TableScripts =
    {
        @"
IF OBJECT_ID('files', 'U') IS NULL
BEGIN
    CREATE TABLE files (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        supplier_id INT NULL,
        agreementNumber NVARCHAR(200) NULL,
        fileType NVARCHAR(200) NULL,
        validFrom DATETIME2 NULL,
        validTo DATETIME2 NULL,
        status NVARCHAR(50) NULL,
        uploadTime DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        uploaderName NVARCHAR(200) NULL,
        originalName NVARCHAR(512) NULL,
        storedName NVARCHAR(512) NOT NULL
    )
END",
        @"
IF OBJECT_ID('invoices', 'U') IS NULL
BEGIN
    CREATE TABLE invoices (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        supplier_id INT NULL,
        rfq_id INT NULL,
        order_id INT NULL,
        invoice_number NVARCHAR(200) NULL,
        amount DECIMAL(18,4) NULL,
        tax_amount DECIMAL(18,4) NULL,
        total_amount DECIMAL(18,4) NULL,
        type NVARCHAR(100) NULL,
        status NVARCHAR(100) NULL,
        issue_date DATETIME2 NULL,
        due_date DATETIME2 NULL,
        uploaded_by NVARCHAR(128) NULL,
        uploaded_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        reviewer_id NVARCHAR(128) NULL,
        reviewed_at DATETIME2 NULL,
        review_notes NVARCHAR(MAX) NULL,
        rejection_reason NVARCHAR(MAX) NULL,
        validation_errors NVARCHAR(MAX) NULL,
        assistance_requested BIT NULL,
        assistance_type NVARCHAR(100) NULL,
        verification_points NVARCHAR(MAX) NULL,
        assistance_deadline DATETIME2 NULL,
        assistance_status NVARCHAR(100) NULL,
        director_approved BIT NULL,
        director_approver_id NVARCHAR(128) NULL,
        director_approved_at DATETIME2 NULL,
        director_approval_notes NVARCHAR(MAX) NULL,
        credit_report_url NVARCHAR(1024) NULL,
        pre_payment_proof NVARCHAR(1024) NULL,
        tax_rate DECIMAL(9,4) NULL,
        invoice_type NVARCHAR(100) NULL,
        signature_seal NVARCHAR(200) NULL,
        created_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NULL DEFAULT SYSUTCDATETIME()
    )
END",
        @"
IF OBJECT_ID('invoice_files', 'U') IS NULL
BEGIN
    CREATE TABLE invoice_files (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        invoice_id INT NOT NULL,
        reconciliation_id INT NULL,
        supplier_id INT NULL,
        original_name NVARCHAR(512) NOT NULL,
        stored_name NVARCHAR(512) NOT NULL,
        storage_path NVARCHAR(1024) NOT NULL,
        file_size BIGINT NULL,
        mime_type NVARCHAR(200) NULL,
        uploaded_by NVARCHAR(128) NULL,
        uploaded_by_name NVARCHAR(200) NULL,
        uploaded_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        checksum NVARCHAR(256) NULL,
        deleted_at DATETIME2 NULL
    )
END",
        @"
IF OBJECT_ID('warehouse_receipts', 'U') IS NULL
BEGIN
    CREATE TABLE warehouse_receipts (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        receipt_number NVARCHAR(200) NOT NULL,
        supplier_id INT NOT NULL,
        purchase_order_number NVARCHAR(200) NULL,
        receipt_date DATETIME2 NOT NULL,
        total_amount DECIMAL(18,4) NOT NULL DEFAULT 0,
        tax_amount DECIMAL(18,4) NULL,
        grand_total DECIMAL(18,4) NOT NULL DEFAULT 0,
        currency NVARCHAR(10) NULL,
        warehouse_location NVARCHAR(200) NULL,
        receiver_name NVARCHAR(200) NULL,
        notes NVARCHAR(MAX) NULL,
        status NVARCHAR(50) NULL,
        created_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NULL DEFAULT SYSUTCDATETIME()
    )
END",
        @"
IF OBJECT_ID('warehouse_receipt_details', 'U') IS NULL
BEGIN
    CREATE TABLE warehouse_receipt_details (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        warehouse_receipt_id INT NOT NULL,
        line_number INT NOT NULL,
        item_code NVARCHAR(200) NULL,
        item_name NVARCHAR(200) NOT NULL,
        specification NVARCHAR(200) NULL,
        unit NVARCHAR(50) NULL,
        quantity DECIMAL(18,4) NOT NULL,
        unit_price DECIMAL(18,4) NOT NULL,
        amount DECIMAL(18,4) NOT NULL,
        tax_rate DECIMAL(9,4) NULL,
        tax_amount DECIMAL(18,4) NULL,
        total_amount DECIMAL(18,4) NOT NULL,
        quality_status NVARCHAR(50) NULL,
        notes NVARCHAR(MAX) NULL
    )
END",
        @"
IF OBJECT_ID('reconciliation', 'U') IS NULL
BEGIN
    CREATE TABLE reconciliation (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        reconciliation_number NVARCHAR(200) NULL,
        supplier_id INT NOT NULL,
        warehouse_receipt_id INT NULL,
        period_start DATETIME2 NULL,
        period_end DATETIME2 NULL,
        total_invoice_amount DECIMAL(18,4) NULL,
        total_receipt_amount DECIMAL(18,4) NULL,
        variance_amount DECIMAL(18,4) NULL,
        variance_percentage DECIMAL(18,6) NULL,
        status NVARCHAR(50) NULL,
        match_type NVARCHAR(50) NULL,
        confidence_score DECIMAL(18,6) NULL,
        created_by NVARCHAR(128) NULL,
        created_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        confirmed_by NVARCHAR(128) NULL,
        confirmed_at DATETIME2 NULL,
        notes NVARCHAR(MAX) NULL,
        updated_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        invoice_id INT NULL,
        invoice_amount DECIMAL(18,4) NULL,
        receipt_amount DECIMAL(18,4) NULL,
        accountant_confirmed BIT NULL,
        accountant_confirmed_by NVARCHAR(128) NULL,
        accountant_confirmed_at DATETIME2 NULL,
        confirmation_notes NVARCHAR(MAX) NULL
    )
END",
        @"
IF OBJECT_ID('invoice_reconciliation_match', 'U') IS NULL
BEGIN
    CREATE TABLE invoice_reconciliation_match (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        reconciliation_id INT NOT NULL,
        invoice_id INT NOT NULL,
        warehouse_receipt_id INT NULL,
        match_type NVARCHAR(50) NULL,
        match_confidence DECIMAL(18,6) NULL,
        confidence_score DECIMAL(18,6) NULL,
        invoice_amount DECIMAL(18,4) NULL,
        receipt_amount DECIMAL(18,4) NULL,
        variance_amount DECIMAL(18,4) NULL,
        variance_percentage DECIMAL(18,6) NULL,
        matched_at DATETIME2 NULL,
        matched_by NVARCHAR(128) NULL,
        notes NVARCHAR(MAX) NULL,
        match_notes NVARCHAR(MAX) NULL
    )
END",
        @"
IF OBJECT_ID('reconciliation_variance_analysis', 'U') IS NULL
BEGIN
    CREATE TABLE reconciliation_variance_analysis (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        reconciliation_id INT NOT NULL,
        invoice_id INT NULL,
        variance_type NVARCHAR(100) NOT NULL,
        variance_amount DECIMAL(18,4) NOT NULL,
        expected_amount DECIMAL(18,4) NULL,
        actual_amount DECIMAL(18,4) NULL,
        variance_percentage DECIMAL(18,6) NULL,
        severity NVARCHAR(50) NULL,
        root_cause NVARCHAR(MAX) NULL,
        resolution_action NVARCHAR(MAX) NULL,
        status NVARCHAR(50) NULL,
        assigned_to NVARCHAR(128) NULL,
        resolved_at DATETIME2 NULL,
        resolved_by NVARCHAR(128) NULL,
        notes NVARCHAR(MAX) NULL,
        created_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        variance_reason NVARCHAR(MAX) NULL,
        analyzed_by NVARCHAR(128) NULL,
        resolution_status NVARCHAR(100) NULL,
        analyzed_at DATETIME2 NULL
    )
END",
        @"
IF OBJECT_ID('reconciliation_status_history', 'U') IS NULL
BEGIN
    CREATE TABLE reconciliation_status_history (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        reconciliation_id INT NOT NULL,
        from_status NVARCHAR(50) NULL,
        to_status NVARCHAR(50) NOT NULL,
        changed_by NVARCHAR(128) NULL,
        changed_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        reason NVARCHAR(MAX) NULL,
        notes NVARCHAR(MAX) NULL,
        change_reason NVARCHAR(MAX) NULL
    )
END",
        @"
IF OBJECT_ID('reconciliation_thresholds', 'U') IS NULL
BEGIN
    CREATE TABLE reconciliation_thresholds (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        threshold_name NVARCHAR(200) NOT NULL,
        acceptable_variance_percentage DECIMAL(18,6) NULL,
        warning_variance_percentage DECIMAL(18,6) NULL,
        auto_match_enabled BIT NULL,
        description NVARCHAR(MAX) NULL,
        created_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        is_active BIT NOT NULL DEFAULT 1
    )
END",
        @"
IF OBJECT_ID('workflow_instances', 'U') IS NULL
BEGIN
    CREATE TABLE workflow_instances (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        workflowType NVARCHAR(200) NULL,
        entityType NVARCHAR(200) NULL,
        entityId NVARCHAR(200) NULL,
        status NVARCHAR(50) NULL,
        currentStep NVARCHAR(200) NULL,
        createdBy NVARCHAR(200) NULL,
        createdAt DATETIME2 NULL,
        updatedAt DATETIME2 NULL
    )
END",
        @"
IF OBJECT_ID('workflow_steps', 'U') IS NULL
BEGIN
    CREATE TABLE workflow_steps (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        workflowId INT NULL,
        stepOrder INT NULL,
        name NVARCHAR(200) NULL,
        assignee NVARCHAR(200) NULL,
        status NVARCHAR(50) NULL,
        dueAt DATETIME2 NULL,
        completedAt DATETIME2 NULL,
        notes NVARCHAR(MAX) NULL
    )
END",
        @"
IF OBJECT_ID('supplier_change_requests', 'U') IS NULL
BEGIN
    CREATE TABLE supplier_change_requests (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        supplier_id INT NOT NULL,
        changeType NVARCHAR(100) NULL,
        riskLevel NVARCHAR(50) NULL,
        status NVARCHAR(100) NULL,
        currentStep NVARCHAR(100) NULL,
        payload NVARCHAR(MAX) NULL,
        submittedBy NVARCHAR(200) NULL,
        submittedAt DATETIME2 NULL,
        updatedAt DATETIME2 NULL,
        requiresQuality BIT NULL
    )
END",
        @"
IF OBJECT_ID('change_request_approvals', 'U') IS NULL
BEGIN
    CREATE TABLE change_request_approvals (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        requestId INT NOT NULL,
        step NVARCHAR(100) NOT NULL,
        approverId NVARCHAR(128) NOT NULL,
        approverName NVARCHAR(200) NULL,
        decision NVARCHAR(50) NOT NULL,
        comments NVARCHAR(MAX) NULL,
        createdAt DATETIME2 NULL DEFAULT SYSUTCDATETIME()
    )
END",
        @"
IF OBJECT_ID('supplier_file_uploads', 'U') IS NULL
BEGIN
    CREATE TABLE supplier_file_uploads (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        supplier_id INT NOT NULL,
        fileId INT NOT NULL,
        fileName NVARCHAR(512) NULL,
        fileDescription NVARCHAR(MAX) NULL,
        status NVARCHAR(100) NULL,
        currentStep NVARCHAR(100) NULL,
        submittedBy NVARCHAR(200) NULL,
        submittedAt DATETIME2 NULL,
        riskLevel NVARCHAR(50) NULL,
        validFrom DATETIME2 NULL,
        validTo DATETIME2 NULL,
        createdAt DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        updatedAt DATETIME2 NULL DEFAULT SYSUTCDATETIME()
    )
END",
        @"
IF OBJECT_ID('supplier_file_approvals', 'U') IS NULL
BEGIN
    CREATE TABLE supplier_file_approvals (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        uploadId INT NOT NULL,
        step NVARCHAR(100) NOT NULL,
        stepName NVARCHAR(200) NULL,
        approverId NVARCHAR(128) NULL,
        approverName NVARCHAR(200) NULL,
        decision NVARCHAR(50) NULL,
        comments NVARCHAR(MAX) NULL,
        createdAt DATETIME2 NULL DEFAULT SYSUTCDATETIME()
    )
END",
        @"
IF OBJECT_ID('file_upload_configs', 'U') IS NULL
BEGIN
    CREATE TABLE file_upload_configs (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        scenario NVARCHAR(100) NOT NULL,
        scenario_name NVARCHAR(200) NOT NULL,
        scenario_description NVARCHAR(MAX) NULL,
        allowed_formats NVARCHAR(MAX) NOT NULL,
        max_file_size INT NOT NULL,
        max_file_count INT NOT NULL,
        enable_virus_scan BIT NULL,
        created_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        updated_by NVARCHAR(128) NULL
    )
END",
        @"
IF OBJECT_ID('file_scan_records', 'U') IS NULL
BEGIN
    CREATE TABLE file_scan_records (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        file_path NVARCHAR(1024) NOT NULL,
        original_name NVARCHAR(512) NOT NULL,
        file_size BIGINT NULL,
        mime_type NVARCHAR(200) NULL,
        scan_status NVARCHAR(50) NOT NULL,
        scan_result NVARCHAR(MAX) NULL,
        scan_engine NVARCHAR(100) NULL,
        scan_duration INT NULL,
        is_clean BIT NULL,
        threat_name NVARCHAR(200) NULL,
        scanned_at DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        uploaded_by NVARCHAR(128) NULL,
        scenario NVARCHAR(100) NULL,
        quarantined BIT NULL,
        quarantine_path NVARCHAR(1024) NULL
    )
END",
        @"
IF OBJECT_ID('file_access_tokens', 'U') IS NULL
BEGIN
    CREATE TABLE file_access_tokens (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        token NVARCHAR(128) NOT NULL,
        userId NVARCHAR(128) NOT NULL,
        resourceType NVARCHAR(100) NOT NULL,
        resourceId NVARCHAR(200) NOT NULL,
        category NVARCHAR(100) NOT NULL,
        storagePath NVARCHAR(1024) NOT NULL,
        originalName NVARCHAR(512) NULL,
        fileSize BIGINT NULL,
        mimeType NVARCHAR(200) NULL,
        createdAt DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        expiresAt DATETIME2 NOT NULL,
        usedAt DATETIME2 NULL,
        ipAddress NVARCHAR(100) NULL,
        usedIpAddress NVARCHAR(100) NULL
    )
END",
        @"
IF OBJECT_ID('supplier_upgrade_applications', 'U') IS NULL
BEGIN
    CREATE TABLE supplier_upgrade_applications (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        supplier_id INT NOT NULL,
        status NVARCHAR(100) NOT NULL,
        currentStep NVARCHAR(100) NULL,
        submittedAt DATETIME2 NULL,
        submittedBy NVARCHAR(200) NULL,
        dueAt DATETIME2 NULL,
        workflowId INT NULL,
        rejectionReason NVARCHAR(MAX) NULL,
        resubmittedAt DATETIME2 NULL,
        createdAt DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        updatedAt DATETIME2 NULL DEFAULT SYSUTCDATETIME()
    )
END",
        @"
IF OBJECT_ID('supplier_upgrade_documents', 'U') IS NULL
BEGIN
    CREATE TABLE supplier_upgrade_documents (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        applicationId INT NOT NULL,
        requirementCode NVARCHAR(200) NOT NULL,
        requirementName NVARCHAR(200) NOT NULL,
        fileId INT NOT NULL,
        uploadedAt DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        uploadedBy NVARCHAR(200) NULL,
        status NVARCHAR(50) NULL,
        notes NVARCHAR(MAX) NULL
    )
END",
        @"
IF OBJECT_ID('supplier_upgrade_reviews', 'U') IS NULL
BEGIN
    CREATE TABLE supplier_upgrade_reviews (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        applicationId INT NOT NULL,
        stepKey NVARCHAR(100) NOT NULL,
        stepName NVARCHAR(200) NOT NULL,
        decision NVARCHAR(50) NOT NULL,
        comments NVARCHAR(MAX) NULL,
        decidedById NVARCHAR(128) NULL,
        decidedByName NVARCHAR(200) NULL,
        decidedAt DATETIME2 NULL DEFAULT SYSUTCDATETIME()
    )
END"
    };
}
