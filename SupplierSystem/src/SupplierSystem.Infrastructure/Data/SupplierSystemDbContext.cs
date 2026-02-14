﻿﻿using System.Collections.Generic;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data.Configurations;

namespace SupplierSystem.Infrastructure.Data;

public sealed class SupplierSystemDbContext : DbContext
{
    public SupplierSystemDbContext(DbContextOptions<SupplierSystemDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<TagDef> TagDefs => Set<TagDef>();
    public DbSet<SupplierTag> SupplierTags => Set<SupplierTag>();
    public DbSet<SupplierDocument> SupplierDocuments => Set<SupplierDocument>();
    public DbSet<SupplierDraft> SupplierDrafts => Set<SupplierDraft>();
    public DbSet<SupplierRegistrationApplication> SupplierRegistrationApplications => Set<SupplierRegistrationApplication>();
    public DbSet<SupplierRegistrationDraft> SupplierRegistrationDrafts => Set<SupplierRegistrationDraft>();
    public DbSet<SupplierRegistrationBlacklist> SupplierRegistrationBlacklist => Set<SupplierRegistrationBlacklist>();
    public DbSet<SupplierDocumentWhitelist> SupplierDocumentWhitelists => Set<SupplierDocumentWhitelist>();
    public DbSet<SupplierRfqInvitation> SupplierRfqInvitations => Set<SupplierRfqInvitation>();
    public DbSet<Rfq> Rfqs => Set<Rfq>();
    public DbSet<RfqLineItem> RfqLineItems => Set<RfqLineItem>();
    public DbSet<RfqAttachment> RfqAttachments => Set<RfqAttachment>();
    public DbSet<PriceComparisonAttachment> PriceComparisonAttachments => Set<PriceComparisonAttachment>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLineItem> QuoteLineItems => Set<QuoteLineItem>();
    public DbSet<QuoteAttachment> QuoteAttachments => Set<QuoteAttachment>();
    public DbSet<QuoteVersion> QuoteVersions => Set<QuoteVersion>();
    public DbSet<RfqReview> RfqReviews => Set<RfqReview>();
    public DbSet<RfqApproval> RfqApprovals => Set<RfqApproval>();
    public DbSet<ApprovalComment> ApprovalComments => Set<ApprovalComment>();
    public DbSet<RfqPrRecord> RfqPrRecords => Set<RfqPrRecord>();
    public DbSet<RfqPriceAuditRecord> RfqPriceAuditRecords => Set<RfqPriceAuditRecord>();
    public DbSet<RfqLineItemApprovalHistory> RfqLineItemApprovalHistories => Set<RfqLineItemApprovalHistory>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<RfqExternalInvitation> RfqExternalInvitations => Set<RfqExternalInvitation>();
    public DbSet<RfqStatusHistory> RfqStatusHistories => Set<RfqStatusHistory>();
    public DbSet<QuoteStatusHistory> QuoteStatusHistories => Set<QuoteStatusHistory>();
    public DbSet<LineItemStatusHistory> LineItemStatusHistories => Set<LineItemStatusHistory>();
    public DbSet<MaterialRequisition> MaterialRequisitions => Set<MaterialRequisition>();
    public DbSet<MaterialRequisitionItem> MaterialRequisitionItems => Set<MaterialRequisitionItem>();
    public DbSet<MaterialRequisitionAttachment> MaterialRequisitionAttachments => Set<MaterialRequisitionAttachment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TokenBlacklistEntry> TokenBlacklist => Set<TokenBlacklistEntry>();
    public DbSet<ActiveSession> ActiveSessions => Set<ActiveSession>();
    public DbSet<OrganizationalUnit> OrganizationalUnits => Set<OrganizationalUnit>();
    public DbSet<OrganizationalUnitMember> OrganizationalUnitMembers => Set<OrganizationalUnitMember>();
    public DbSet<PurchasingGroup> PurchasingGroups => Set<PurchasingGroup>();
    public DbSet<PurchasingGroupMember> PurchasingGroupMembers => Set<PurchasingGroupMember>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<AuditArchiveMetadata> AuditArchiveMetadata => Set<AuditArchiveMetadata>();
    public DbSet<TemplateDocument> TemplateDocuments => Set<TemplateDocument>();
    public DbSet<ExchangeRateHistory> ExchangeRateHistories => Set<ExchangeRateHistory>();
    public DbSet<FreightRateHistory> FreightRateHistories => Set<FreightRateHistory>();
    public DbSet<BuyerSupplierAssignment> BuyerSupplierAssignments => Set<BuyerSupplierAssignment>();
    public DbSet<BuyerSupplierPermission> BuyerSupplierPermissions => Set<BuyerSupplierPermission>();
    public DbSet<BackupMetadata> BackupMetadata => Set<BackupMetadata>();
    public DbSet<BackupAlert> BackupAlerts => Set<BackupAlert>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractVersion> ContractVersions => Set<ContractVersion>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<WarehouseReceipt> WarehouseReceipts => Set<WarehouseReceipt>();
    public DbSet<WarehouseReceiptDetail> WarehouseReceiptDetails => Set<WarehouseReceiptDetail>();
    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();
    public DbSet<InvoiceReconciliationMatch> InvoiceReconciliationMatches => Set<InvoiceReconciliationMatch>();
    public DbSet<ReconciliationVarianceAnalysis> ReconciliationVarianceAnalyses => Set<ReconciliationVarianceAnalysis>();
    public DbSet<ReconciliationStatusHistory> ReconciliationStatusHistories => Set<ReconciliationStatusHistory>();
    public DbSet<ReconciliationThreshold> ReconciliationThresholds => Set<ReconciliationThreshold>();
    public DbSet<InvoiceFile> InvoiceFiles => Set<InvoiceFile>();
    public DbSet<InvoiceRestoreRequest> InvoiceRestoreRequests => Set<InvoiceRestoreRequest>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<SupplierRating> SupplierRatings => Set<SupplierRating>();
    public DbSet<SupplierCompletionHistory> SupplierCompletionHistories => Set<SupplierCompletionHistory>();
    public DbSet<SupplierChangeRequest> SupplierChangeRequests => Set<SupplierChangeRequest>();
    public DbSet<SupplierBaseline> SupplierBaselines => Set<SupplierBaseline>();
    public DbSet<SupplierRiskAssessment> SupplierRiskAssessments => Set<SupplierRiskAssessment>();
    public DbSet<SupplierUpgradeApplication> SupplierUpgradeApplications => Set<SupplierUpgradeApplication>();
    public DbSet<SupplierUpgradeDocument> SupplierUpgradeDocuments => Set<SupplierUpgradeDocument>();
    public DbSet<SupplierUpgradeReview> SupplierUpgradeReviews => Set<SupplierUpgradeReview>();
    public DbSet<FileRecord> Files => Set<FileRecord>();
    public DbSet<TempSupplierUser> TempSupplierUsers => Set<TempSupplierUser>();
    public DbSet<TempAccountSequence> TempAccountSequences => Set<TempAccountSequence>();
    public DbSet<ExternalSupplierInvitation> ExternalSupplierInvitations => Set<ExternalSupplierInvitation>();
    public DbSet<SupplierFileUpload> SupplierFileUploads => Set<SupplierFileUpload>();
    public DbSet<SupplierFileApproval> SupplierFileApprovals => Set<SupplierFileApproval>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<ReminderQueue> ReminderQueues => Set<ReminderQueue>();
    public DbSet<ContractReminderSetting> ContractReminderSettings => Set<ContractReminderSetting>();
    public DbSet<RfqProject> RfqProjects => Set<RfqProject>();
    public DbSet<RfqQuote> RfqQuotes => Set<RfqQuote>();
    public DbSet<RfqProjectSupplier> RfqProjectSuppliers => Set<RfqProjectSupplier>();
    public DbSet<FileUploadConfig> FileUploadConfigs => Set<FileUploadConfig>();
    public DbSet<FileScanRecord> FileScanRecords => Set<FileScanRecord>();
    public DbSet<DocumentTypeDef> DocumentTypeDefs => Set<DocumentTypeDef>();
    public DbSet<PurchasingGroupSupplier> PurchasingGroupSuppliers => Set<PurchasingGroupSupplier>();
    public DbSet<BuyerSupplierAccessCache> BuyerSupplierAccessCaches => Set<BuyerSupplierAccessCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringToDateTimeConverter = new ValueConverter<string?, DateTime?>(
            value => ParseDateTimeInvariant(value),
            value => FormatDateTimeInvariant(value));

        modelBuilder.ApplyConfiguration(new UserEntityConfiguration());
        modelBuilder.Entity<Supplier>()
            .ToTable("suppliers", tb => tb.HasTrigger("trg_dbo_suppliers_audit_update"))
            .HasKey(s => s.Id);
        modelBuilder.Entity<TagDef>().ToTable("tag_defs").HasKey(t => t.Id);
        modelBuilder.Entity<SupplierTag>(entity =>
        {
            entity.ToTable("supplier_tags");
            entity.HasKey(st => new { st.SupplierId, st.TagId });
            entity.Property(st => st.SupplierId).HasColumnName("supplierId");
            entity.Property(st => st.TagId).HasColumnName("tagId");
        });
        modelBuilder.Entity<SupplierDocument>(entity =>
        {
            entity.ToTable("supplier_documents");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).HasColumnName("id");
            entity.Property(d => d.SupplierId).HasColumnName("supplierId");
            entity.Property(d => d.DocType).HasColumnName("docType");
            entity.Property(d => d.StoredName).HasColumnName("storedName");
            entity.Property(d => d.OriginalName).HasColumnName("originalName");
            entity.Property(d => d.UploadedAt).HasColumnName("uploadedAt");
            entity.Property(d => d.UploadedBy).HasColumnName("uploadedBy");
            entity.Property(d => d.ValidFrom).HasColumnName("validFrom");
            entity.Property(d => d.ExpiresAt).HasColumnName("expiresAt");
            entity.Property(d => d.Status).HasColumnName("status");
            entity.Property(d => d.Notes).HasColumnName("notes");
            entity.Property(d => d.FileSize).HasColumnName("fileSize");
            entity.Property(d => d.Category).HasColumnName("category");
            entity.Property(d => d.IsRequired).HasColumnName("isRequired");
        });
        modelBuilder.Entity<SupplierDraft>().ToTable("supplier_drafts").HasKey(d => d.Id);
        modelBuilder.Entity<SupplierRegistrationApplication>()
            .ToTable("supplier_registration_applications_v", tb => tb.HasTrigger("trg_dbo_supplier_registration_applications_audit_update"))
            .HasKey(a => a.Id);
        modelBuilder.Entity<SupplierRegistrationDraft>().ToTable("supplier_registration_drafts").HasKey(d => d.Id);
        modelBuilder.Entity<SupplierRfqInvitation>().ToTable("supplier_rfq_invitations").HasKey(i => i.Id);
        modelBuilder.Entity<Rfq>()
            .ToTable("rfqs", tb => tb.HasTrigger("trg_dbo_rfqs_audit_update"))
            .HasKey(r => r.Id);
        modelBuilder.Entity<AuditLog>().ToTable("audit_log").HasKey(a => a.Id);
        modelBuilder.Entity<OrganizationalUnit>().ToTable("organizational_units").HasKey(o => o.Id);
        modelBuilder.Entity<OrganizationalUnitMember>().ToTable("organizational_unit_members").HasKey(m => m.Id);
        modelBuilder.Entity<PurchasingGroup>()
            .ToTable("purchasing_groups", tb => tb.HasTrigger("trg_dbo_purchasing_groups_audit_update"))
            .HasKey(g => g.Id);
        modelBuilder.Entity<PurchasingGroupMember>().ToTable("purchasing_group_members").HasKey(m => m.Id);
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.SupplierId).HasColumnName("supplier_id");
        });
        modelBuilder.Entity<SystemConfig>().ToTable("system_config").HasKey(s => s.Key);
        modelBuilder.Entity<AuditArchiveMetadata>().ToTable("audit_archive_metadata").HasKey(a => a.Id);
        modelBuilder.Entity<TemplateDocument>().ToTable("template_documents").HasKey(t => t.Id);
        modelBuilder.Entity<ExchangeRateHistory>().ToTable("exchange_rate_history").HasKey(e => e.Id);
        modelBuilder.Entity<FreightRateHistory>().ToTable("freight_rate_history").HasKey(e => e.Id);
        modelBuilder.Entity<BuyerSupplierAssignment>().ToTable("buyer_supplier_assignments").HasKey(b => b.Id);
        modelBuilder.Entity<BuyerSupplierPermission>()
            .ToTable("buyer_supplier_permissions", tb => tb.HasTrigger("trg_dbo_buyer_supplier_permissions_audit_update"))
            .HasKey(b => b.Id);
        modelBuilder.Entity<BackupMetadata>().ToTable("backup_metadata").HasKey(b => b.Id);

        modelBuilder.Entity<SupplierRegistrationBlacklist>(entity =>
        {
            entity.ToTable("supplier_registration_blacklist");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BlacklistType).HasColumnName("blacklist_type");
            entity.Property(e => e.BlacklistValue).HasColumnName("blacklist_value");
            entity.Property(e => e.AddedBy).HasColumnName("added_by");
            entity.Property(e => e.AddedByName).HasColumnName("added_by_name");
            entity.Property(e => e.AddedAt).HasColumnName("added_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
        });

        modelBuilder.Entity<SupplierDocumentWhitelist>(entity =>
        {
            entity.ToTable("supplier_document_whitelist");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.DocumentType).HasColumnName("document_type");
            entity.Property(e => e.ExemptedBy).HasColumnName("exempted_by");
            entity.Property(e => e.ExemptedByName).HasColumnName("exempted_by_name");
            entity.Property(e => e.ExemptedAt).HasColumnName("exempted_at");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
        });

        modelBuilder.Entity<SupplierRfqInvitation>(entity =>
        {
            entity.ToTable("supplier_rfq_invitations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.InvitedAt).HasColumnName("invited_at");
            entity.Property(e => e.RespondedAt).HasColumnName("responded_at");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.InvitationToken).HasColumnName("invitation_token");
            entity.Property(e => e.TokenExpiresAt).HasColumnName("token_expires_at");
            entity.Property(e => e.RecipientEmail).HasColumnName("recipient_email");
            entity.Property(e => e.IsExternal).HasColumnName("is_external");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Rfq>(entity =>
        {
            entity.ToTable("rfqs", tb => tb.HasTrigger("trg_dbo_rfqs_audit_update"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.DeliveryPeriod).HasColumnName("delivery_period");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.MaterialType).HasColumnName("material_type");
            entity.Property(e => e.MaterialCategoryType).HasColumnName("material_category_type");
            entity.Property(e => e.IsLineItemMode).HasColumnName("is_line_item_mode");
            entity.Property(e => e.DistributionCategory).HasColumnName("distribution_category");
            entity.Property(e => e.DistributionSubcategory).HasColumnName("distribution_subcategory");
            entity.Property(e => e.RfqType).HasColumnName("rfq_type");
            entity.Property(e => e.BudgetAmount).HasColumnName("budget_amount");
            entity.Property(e => e.RequiredDocuments).HasColumnName("required_documents");
            entity.Property(e => e.EvaluationCriteria).HasColumnName("evaluation_criteria");
            entity.Property(e => e.ValidUntil).HasColumnName("valid_until");
            entity.Property(e => e.RequestingParty).HasColumnName("requesting_party");
            entity.Property(e => e.RequestingDepartment).HasColumnName("requesting_department");
            entity.Property(e => e.RequirementDate).HasColumnName("requirement_date");
            entity.Property(e => e.DetailedParameters).HasColumnName("detailed_parameters");
            entity.Property(e => e.MinSupplierCount).HasColumnName("min_supplier_count");
            entity.Property(e => e.SupplierExceptionNote).HasColumnName("supplier_exception_note");
            entity.Property(e => e.SelectedQuoteId).HasColumnName("selected_quote_id");
            entity.Property(e => e.ReviewCompletedAt).HasColumnName("review_completed_at");
            entity.Property(e => e.ApprovalStatus).HasColumnName("approval_status");
            entity.Property(e => e.PrStatus).HasColumnName("pr_status");
            entity.Property(e => e.RequisitionId).HasColumnName("requisition_id");
        });

        modelBuilder.Entity<RfqLineItem>(entity =>
        {
            entity.ToTable("rfq_line_items", tb => tb.HasTrigger("trg_dbo_rfq_line_items_audit_update"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.LineNumber).HasColumnName("line_number");
            entity.Property(e => e.MaterialCategory).HasColumnName("material_category");
            entity.Property(e => e.Brand).HasColumnName("brand");
            entity.Property(e => e.ItemName).HasColumnName("item_name");
            entity.Property(e => e.Specifications).HasColumnName("specifications");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Unit).HasColumnName("unit");
            entity.Property(e => e.EstimatedUnitPrice).HasColumnName("estimated_unit_price");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.Parameters).HasColumnName("parameters");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CurrentApproverRole).HasColumnName("current_approver_role");
            entity.Property(e => e.SelectedQuoteId).HasColumnName("selected_quote_id");
            entity.Property(e => e.PoId).HasColumnName("po_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<RfqAttachment>(entity =>
        {
            entity.ToTable("rfq_attachments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.LineItemId).HasColumnName("line_item_id");
            entity.Property(e => e.FileName).HasColumnName("file_name");
            entity.Property(e => e.FilePath).HasColumnName("file_path");
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.FileType).HasColumnName("file_type");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
            entity.Property(e => e.Description).HasColumnName("description");
        });

        modelBuilder.Entity<PriceComparisonAttachment>(entity =>
        {
            entity.ToTable("price_comparison_attachments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.LineItemId).HasColumnName("line_item_id");
            entity.Property(e => e.Platform).HasColumnName("platform");
            entity.Property(e => e.FileName).HasColumnName("file_name");
            entity.Property(e => e.FilePath).HasColumnName("file_path");
            entity.Property(e => e.ProductUrl).HasColumnName("product_url");
            entity.Property(e => e.PlatformPrice).HasColumnName("platform_price");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.StoredFileName).HasColumnName("stored_file_name");
            entity.Property(e => e.OriginalFileName).HasColumnName("original_file_name");
        });

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.ToTable("quotes", tb => tb.HasTrigger("trg_dbo_quotes_audit_update"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.DeliveryDate).HasColumnName("delivery_date");
            entity.Property(e => e.PaymentTerms).HasColumnName("payment_terms");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.WithdrawalReason).HasColumnName("withdrawal_reason");
            entity.Property(e => e.WithdrawnAt).HasColumnName("withdrawn_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Brand).HasColumnName("brand");
            entity.Property(e => e.TaxStatus).HasColumnName("tax_status");
            entity.Property(e => e.Parameters).HasColumnName("parameters");
            entity.Property(e => e.OptionalConfig).HasColumnName("optional_config");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.IsLatest).HasColumnName("is_latest");
            entity.Property(e => e.ModifiedCount).HasColumnName("modified_count");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.CanModifyUntil).HasColumnName("can_modify_until");
            entity.Property(e => e.DeliveryTerms).HasColumnName("delivery_terms");
            entity.Property(e => e.ShippingLocation).HasColumnName("shipping_location");
            entity.Property(e => e.ShippingCountry).HasColumnName("shipping_country");
            entity.Property(e => e.TotalStandardCostLocal).HasColumnName("total_standard_cost_local");
            entity.Property(e => e.TotalStandardCostUsd).HasColumnName("total_standard_cost_usd");
            entity.Property(e => e.TotalTariffAmountLocal).HasColumnName("total_tariff_amount_local");
            entity.Property(e => e.TotalTariffAmountUsd).HasColumnName("total_tariff_amount_usd");
            entity.Property(e => e.HasSpecialTariff).HasColumnName("has_special_tariff");
        });

        modelBuilder.Entity<QuoteLineItem>(entity =>
        {
            entity.ToTable("quote_line_items", tb => tb.HasTrigger("trg_dbo_quote_line_items_audit_update"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuoteId).HasColumnName("quote_id");
            entity.Property(e => e.RfqLineItemId).HasColumnName("rfq_line_item_id");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price");
            entity.Property(e => e.MinimumOrderQuantity).HasColumnName("minimum_order_quantity");
            entity.Property(e => e.StandardPackageQuantity).HasColumnName("standard_package_quantity");
            entity.Property(e => e.TotalPrice).HasColumnName("total_price");
            entity.Property(e => e.Brand).HasColumnName("brand");
            entity.Property(e => e.TaxStatus).HasColumnName("tax_status");
            entity.Property(e => e.DeliveryDate).HasColumnName("delivery_date");
            entity.Property(e => e.DeliveryPeriod).HasColumnName("delivery_period");
            entity.Property(e => e.Parameters).HasColumnName("parameters");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.ProductOrigin).HasColumnName("product_origin");
            entity.Property(e => e.ProductGroup).HasColumnName("product_group");
            entity.Property(e => e.OriginalPriceUsd).HasColumnName("original_price_usd");
            entity.Property(e => e.ExchangeRate).HasColumnName("exchange_rate");
            entity.Property(e => e.ExchangeRateDate).HasColumnName("exchange_rate_date");
            entity.Property(e => e.TariffRate).HasColumnName("tariff_rate");
            entity.Property(e => e.TariffRatePercent).HasColumnName("tariff_rate_percent");
            entity.Property(e => e.TariffAmountLocal).HasColumnName("tariff_amount_local");
            entity.Property(e => e.TariffAmountUsd).HasColumnName("tariff_amount_usd");
            entity.Property(e => e.SpecialTariffRate).HasColumnName("special_tariff_rate");
            entity.Property(e => e.SpecialTariffRatePercent).HasColumnName("special_tariff_rate_percent");
            entity.Property(e => e.SpecialTariffAmountLocal).HasColumnName("special_tariff_amount_local");
            entity.Property(e => e.SpecialTariffAmountUsd).HasColumnName("special_tariff_amount_usd");
            entity.Property(e => e.HasSpecialTariff).HasColumnName("has_special_tariff");
            entity.Property(e => e.StandardCostLocal).HasColumnName("standard_cost_local");
            entity.Property(e => e.StandardCostUsd).HasColumnName("standard_cost_usd");
            entity.Property(e => e.StandardCostCurrency).HasColumnName("standard_cost_currency");
            entity.Property(e => e.CalculatedAt).HasColumnName("calculated_at");
        });

        modelBuilder.Entity<QuoteAttachment>(entity =>
        {
            entity.ToTable("quote_attachments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("bigint");
            entity.Property(e => e.QuoteId)
                .HasColumnName("quote_id")
                .HasColumnType("bigint");
            entity.Property(e => e.OriginalName).HasColumnName("original_name");
            entity.Property(e => e.StoredName).HasColumnName("stored_name");
            entity.Property(e => e.FileType).HasColumnName("file_type");
            entity.Property(e => e.FileSize)
                .HasColumnName("file_size")
                .HasColumnType("int");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
            entity.Property(e => e.UploadedBy)
                .HasColumnName("uploaded_by")
                .HasColumnType("bigint");
        });

        modelBuilder.Entity<QuoteVersion>(entity =>
        {
            entity.ToTable("quote_versions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuoteId).HasColumnName("quote_id");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount");
            entity.Property(e => e.Brand).HasColumnName("brand");
            entity.Property(e => e.TaxStatus).HasColumnName("tax_status");
            entity.Property(e => e.Parameters).HasColumnName("parameters");
            entity.Property(e => e.OptionalConfig).HasColumnName("optional_config");
            entity.Property(e => e.DeliveryDate).HasColumnName("delivery_date");
            entity.Property(e => e.PaymentTerms).HasColumnName("payment_terms");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.ModifiedAt).HasColumnName("modified_at");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.ChangeSummary).HasColumnName("change_summary");
        });

        modelBuilder.Entity<RfqReview>(entity =>
        {
            entity.ToTable("rfq_reviews");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.SelectedQuoteId).HasColumnName("selected_quote_id");
            entity.Property(e => e.ReviewScores).HasColumnName("review_scores");
            entity.Property(e => e.Comments).HasColumnName("comments");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
        });

        modelBuilder.Entity<RfqApproval>(entity =>
        {
            entity.ToTable("rfq_approvals");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.StepOrder).HasColumnName("step_order");
            entity.Property(e => e.StepName).HasColumnName("step_name");
            entity.Property(e => e.ApproverRole).HasColumnName("approver_role");
            entity.Property(e => e.ApproverId).HasColumnName("approver_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Decision).HasColumnName("decision");
            entity.Property(e => e.DecidedAt).HasColumnName("decided_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<ApprovalComment>(entity =>
        {
            entity.ToTable("approval_comments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ApprovalId).HasColumnName("approval_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.AuthorName).HasColumnName("author_name");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<RfqPrRecord>(entity =>
        {
            entity.ToTable("rfq_pr_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.PrNumber).HasColumnName("pr_number");
            entity.Property(e => e.PrDate).HasColumnName("pr_date");
            entity.Property(e => e.FilledBy).HasColumnName("filled_by");
            entity.Property(e => e.FilledAt).HasColumnName("filled_at");
            entity.Property(e => e.DepartmentConfirmerId).HasColumnName("department_confirmer_id");
            entity.Property(e => e.DepartmentConfirmerName).HasColumnName("department_confirmer_name");
            entity.Property(e => e.ConfirmationStatus).HasColumnName("confirmation_status");
            entity.Property(e => e.ConfirmationNotes).HasColumnName("confirmation_notes");
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
        });
        modelBuilder.Entity<RfqPriceAuditRecord>(entity =>
        {
            entity.ToTable("rfq_price_audit");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.RfqTitle).HasColumnName("rfq_title");
            entity.Property(e => e.RfqCreatedAt)
                .HasColumnName("rfq_created_at")
                .HasConversion(stringToDateTimeConverter)
                .HasColumnType("datetime2(3)");
            entity.Property(e => e.RfqLineItemId).HasColumnName("rfq_line_item_id");
            entity.Property(e => e.LineNumber).HasColumnName("line_number");
            entity.Property(e => e.Quantity)
                .HasColumnName("quantity")
                .HasColumnType("decimal(18,6)");
            entity.Property(e => e.QuoteId).HasColumnName("quote_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.SupplierName).HasColumnName("supplier_name");
            entity.Property(e => e.SupplierIp).HasColumnName("supplier_ip");
            entity.Property(e => e.QuotedUnitPrice)
                .HasColumnName("quoted_unit_price")
                .HasColumnType("decimal(18,6)");
            entity.Property(e => e.QuotedTotalPrice)
                .HasColumnName("quoted_total_price")
                .HasColumnType("decimal(18,6)");
            entity.Property(e => e.QuoteCurrency).HasColumnName("quote_currency");
            entity.Property(e => e.QuoteSubmittedAt)
                .HasColumnName("quote_submitted_at")
                .HasConversion(stringToDateTimeConverter)
                .HasColumnType("datetime2(3)");
            entity.Property(e => e.ApprovalStatus).HasColumnName("approval_status");
            entity.Property(e => e.ApprovalDecision).HasColumnName("approval_decision");
            entity.Property(e => e.ApprovalDecidedAt)
                .HasColumnName("approval_decided_at")
                .HasConversion(stringToDateTimeConverter)
                .HasColumnType("datetime2(3)");
            entity.Property(e => e.SelectedQuoteId).HasColumnName("selected_quote_id");
            entity.Property(e => e.SelectedSupplierId).HasColumnName("selected_supplier_id");
            entity.Property(e => e.SelectedSupplierName).HasColumnName("selected_supplier_name");
            entity.Property(e => e.SelectedUnitPrice)
                .HasColumnName("selected_unit_price")
                .HasColumnType("decimal(18,6)");
            entity.Property(e => e.SelectedCurrency).HasColumnName("selected_currency");
            entity.Property(e => e.PrFilledBy).HasColumnName("pr_filled_by");
            entity.Property(e => e.PrFilledAt)
                .HasColumnName("pr_filled_at")
                .HasConversion(stringToDateTimeConverter)
                .HasColumnType("datetime2(3)");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<RfqLineItemApprovalHistory>(entity =>
        {
            entity.ToTable("rfq_line_item_approval_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqLineItemId).HasColumnName("rfq_line_item_id");
            entity.Property(e => e.Step).HasColumnName("step");
            entity.Property(e => e.ApproverId).HasColumnName("approver_id");
            entity.Property(e => e.ApproverName).HasColumnName("approver_name");
            entity.Property(e => e.ApproverRole).HasColumnName("approver_role");
            entity.Property(e => e.Decision).HasColumnName("decision");
            entity.Property(e => e.Comments).HasColumnName("comments");
            entity.Property(e => e.PreviousQuoteId).HasColumnName("previous_quote_id");
            entity.Property(e => e.NewQuoteId).HasColumnName("new_quote_id");
            entity.Property(e => e.ChangeReason).HasColumnName("change_reason");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("purchase_orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PoNumber).HasColumnName("po_number");
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.ItemCount).HasColumnName("item_count");
            entity.Property(e => e.PoFilePath).HasColumnName("po_file_path");
            entity.Property(e => e.PoFileName).HasColumnName("po_file_name");
            entity.Property(e => e.PoFileSize).HasColumnName("po_file_size");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<RfqExternalInvitation>(entity =>
        {
            entity.ToTable("rfq_external_invitations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.CompanyName).HasColumnName("company_name");
            entity.Property(e => e.ContactPerson).HasColumnName("contact_person");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.InvitedAt).HasColumnName("invited_at");
            entity.Property(e => e.RespondedAt).HasColumnName("responded_at");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Token).HasColumnName("token");
            entity.Property(e => e.TokenExpiresAt).HasColumnName("token_expires_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<RfqStatusHistory>(entity =>
        {
            entity.ToTable("rfq_status_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.FromStatus).HasColumnName("from_status");
            entity.Property(e => e.ToStatus).HasColumnName("to_status");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.ChangedAt).HasColumnName("changed_at");
            entity.Property(e => e.Reason).HasColumnName("reason");
        });

        modelBuilder.Entity<QuoteStatusHistory>(entity =>
        {
            entity.ToTable("quote_status_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuoteId).HasColumnName("quote_id");
            entity.Property(e => e.FromStatus).HasColumnName("from_status");
            entity.Property(e => e.ToStatus).HasColumnName("to_status");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.ChangedAt).HasColumnName("changed_at");
            entity.Property(e => e.Reason).HasColumnName("reason");
        });

        modelBuilder.Entity<LineItemStatusHistory>(entity =>
        {
            entity.ToTable("line_item_status_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LineItemId).HasColumnName("line_item_id");
            entity.Property(e => e.FromStatus).HasColumnName("from_status");
            entity.Property(e => e.ToStatus).HasColumnName("to_status");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.ChangedAt).HasColumnName("changed_at");
            entity.Property(e => e.Reason).HasColumnName("reason");
        });

        modelBuilder.Entity<TokenBlacklistEntry>(entity =>
        {
            entity.ToTable("token_blacklist");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).HasColumnName("token_hash");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.BlacklistedAt).HasColumnName("blacklisted_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        });

        modelBuilder.Entity<ActiveSession>(entity =>
        {
            entity.ToTable("active_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TokenHash).HasColumnName("token_hash");
            entity.Property(e => e.IssuedAt).HasColumnName("issued_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("nvarchar(64)");
        });

        modelBuilder.Entity<MaterialRequisition>(entity =>
        {
            entity.ToTable("material_requisitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequisitionNumber).HasColumnName("requisition_number");
            entity.Property(e => e.RequestingDepartment).HasColumnName("requesting_department");
            entity.Property(e => e.RequestingPersonId).HasColumnName("requesting_person_id");
            entity.Property(e => e.RequestingPersonName).HasColumnName("requesting_person_name");
            entity.Property(e => e.RequiredDate).HasColumnName("required_date");
            entity.Property(e => e.ItemName).HasColumnName("item_name");
            entity.Property(e => e.ItemDescription).HasColumnName("item_description");
            entity.Property(e => e.EstimatedBudget).HasColumnName("estimated_budget");
            entity.Property(e => e.AttachmentFiles).HasColumnName("attachment_files");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.ApprovedById).HasColumnName("approved_by_id");
            entity.Property(e => e.ApprovedByName).HasColumnName("approved_by_name");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.RejectedById).HasColumnName("rejected_by_id");
            entity.Property(e => e.RejectedByName).HasColumnName("rejected_by_name");
            entity.Property(e => e.RejectedAt).HasColumnName("rejected_at");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.ConvertedToRfqId).HasColumnName("converted_to_rfq_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<MaterialRequisitionItem>(entity =>
        {
            entity.ToTable("material_requisition_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequisitionId).HasColumnName("requisition_id");
            entity.Property(e => e.ItemType).HasColumnName("item_type");
            entity.Property(e => e.ItemSubtype).HasColumnName("item_subtype");
            entity.Property(e => e.ItemName).HasColumnName("item_name");
            entity.Property(e => e.ItemDescription).HasColumnName("item_description");
            entity.Property(e => e.EstimatedBudget).HasColumnName("estimated_budget");
            entity.Property(e => e.ConvertedToRfqId).HasColumnName("converted_to_rfq_id");
            entity.Property(e => e.ConvertedAt).HasColumnName("converted_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<MaterialRequisitionAttachment>(entity =>
        {
            entity.ToTable("material_requisition_attachments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequisitionId).HasColumnName("requisition_id");
            entity.Property(e => e.OriginalName).HasColumnName("original_name");
            entity.Property(e => e.StoredName).HasColumnName("stored_name");
            entity.Property(e => e.FileType).HasColumnName("file_type");
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");
        });

        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.ToTable("system_config");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
            entity.Property(e => e.UpdatedBy).HasColumnName("updatedBy");
            entity.Property(e => e.Metadata).HasColumnName("metadata");
        });

        modelBuilder.Entity<AuditArchiveMetadata>(entity =>
        {
            entity.ToTable("audit_archive_metadata");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AuditLogId).HasColumnName("auditLogId");
            entity.Property(e => e.ArchiveFilePath).HasColumnName("archiveFilePath");
            entity.Property(e => e.FileHash).HasColumnName("fileHash");
            entity.Property(e => e.ArchiveDate).HasColumnName("archiveDate");
            entity.Property(e => e.VerifiedAt).HasColumnName("verifiedAt");
            entity.Property(e => e.VerificationStatus).HasColumnName("verificationStatus");
        });

        modelBuilder.Entity<TemplateDocument>(entity =>
        {
            entity.ToTable("template_documents", tb => tb.HasTrigger("trg_dbo_template_documents_audit_update"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TemplateCode).HasColumnName("templateCode");
            entity.Property(e => e.TemplateName).HasColumnName("templateName");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.StoredName).HasColumnName("storedName");
            entity.Property(e => e.OriginalName).HasColumnName("originalName");
            entity.Property(e => e.FileType).HasColumnName("fileType");
            entity.Property(e => e.FileSize).HasColumnName("fileSize");
            entity.Property(e => e.UploadedBy).HasColumnName("uploadedBy");
            entity.Property(e => e.UploadedAt).HasColumnName("uploadedAt");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
        });

        modelBuilder.Entity<ExchangeRateHistory>(entity =>
        {
            entity.ToTable("exchange_rate_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.Rate).HasColumnName("rate");
            entity.Property(e => e.EffectiveDate).HasColumnName("effective_date");
            entity.Property(e => e.Source).HasColumnName("source");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<FreightRateHistory>(entity =>
        {
            entity.ToTable("freight_rate_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RouteCode).HasColumnName("route_code");
            entity.Property(e => e.RouteName).HasColumnName("route_name");
            entity.Property(e => e.RouteNameZh).HasColumnName("route_name_zh");
            entity.Property(e => e.Rate).HasColumnName("rate");
            entity.Property(e => e.Year).HasColumnName("year");
            entity.Property(e => e.Source).HasColumnName("source");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<BuyerSupplierAssignment>(entity =>
        {
            entity.ToTable("buyer_supplier_assignments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BuyerId).HasColumnName("buyerId");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
        });

        modelBuilder.Entity<BuyerSupplierPermission>(entity =>
        {
            entity.ToTable("buyer_supplier_permissions", tb => tb.HasTrigger("trg_dbo_buyer_supplier_permissions_audit_update"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BuyerId).HasColumnName("buyerId");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.CanViewProfile).HasColumnName("canViewProfile");
            entity.Property(e => e.ReceiveContractAlerts).HasColumnName("receiveContractAlerts");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
            entity.Property(e => e.UpdatedBy).HasColumnName("updatedBy");
        });

        modelBuilder.Entity<BackupMetadata>(entity =>
        {
            entity.ToTable("backup_metadata");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BackupType).HasColumnName("backup_type");
            entity.Property(e => e.FilePath).HasColumnName("file_path");
            entity.Property(e => e.FileName).HasColumnName("file_name");
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.Checksum).HasColumnName("checksum");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.VerifiedAt).HasColumnName("verified_at");
            entity.Property(e => e.VerificationStatus).HasColumnName("verification_status");
            entity.Property(e => e.VerificationDetails).HasColumnName("verification_details");
            entity.Property(e => e.Notes).HasColumnName("notes");
        });

        modelBuilder.Entity<BackupAlert>(entity =>
        {
            entity.ToTable("backup_alerts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AlertLevel).HasColumnName("alert_level");
            entity.Property(e => e.AlertType).HasColumnName("alert_type");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.EmailSent).HasColumnName("email_sent");
            entity.Property(e => e.WindowsLogWritten).HasColumnName("windows_log_written");
            entity.Property(e => e.AuditLogged).HasColumnName("audit_logged");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by");
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.ToTable("contracts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.AgreementNumber).HasColumnName("agreementNumber");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.PaymentCycle).HasColumnName("paymentCycle");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effectiveFrom");
            entity.Property(e => e.EffectiveTo).HasColumnName("effectiveTo");
            entity.Property(e => e.AutoRenew).HasColumnName("autoRenew");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.IsMandatory).HasColumnName("isMandatory");
        });

        modelBuilder.Entity<ContractVersion>(entity =>
        {
            entity.ToTable("contract_versions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ContractId).HasColumnName("contractId");
            entity.Property(e => e.VersionNumber).HasColumnName("versionNumber");
            entity.Property(e => e.StoredName).HasColumnName("storedName");
            entity.Property(e => e.OriginalName).HasColumnName("originalName");
            entity.Property(e => e.ChangeLog).HasColumnName("changeLog");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
            entity.Property(e => e.FileSize).HasColumnName("fileSize");
        });

        modelBuilder.Entity<SupplierDocument>()
            .Property(d => d.IsRequired)
            .HasConversion<int>();
        modelBuilder.Entity<SupplierRegistrationApplication>()
            .Property(a => a.TrackingAccountCreated)
            .HasConversion<int>();
        modelBuilder.Entity<SupplierRegistrationBlacklist>()
            .Property(b => b.IsActive)
            .HasConversion<int>();
        modelBuilder.Entity<SupplierRfqInvitation>()
            .Property(i => i.IsExternal)
            .HasConversion<int>();
        modelBuilder.Entity<Rfq>()
            .Property(r => r.IsLineItemMode)
            .HasConversion<int>();
        modelBuilder.Entity<AuditLog>()
            .Property(a => a.CreatedAt)
            .HasColumnName("createdAt");
        modelBuilder.Entity<AuditLog>()
            .Property(a => a.IsSensitive)
            .HasConversion<int>();
        modelBuilder.Entity<AuditLog>()
            .Property(a => a.Immutable)
            .HasConversion<int>();
        modelBuilder.Entity<Quote>()
            .Property(q => q.IsLatest)
            .HasConversion<int>();
        modelBuilder.Entity<Quote>()
            .Property(q => q.HasSpecialTariff)
            .HasConversion<int>();
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.HasSpecialTariff)
            .HasConversion<int>();

        modelBuilder.Entity<Notification>()
            .Property(n => n.Status)
            .HasDefaultValue("unread");
        modelBuilder.Entity<TemplateDocument>()
            .Property(t => t.IsActive)
            .HasConversion<int>();
        modelBuilder.Entity<BuyerSupplierPermission>()
            .Property(p => p.CanViewProfile)
            .HasConversion<int>();
        modelBuilder.Entity<BuyerSupplierPermission>()
            .Property(p => p.ReceiveContractAlerts)
            .HasConversion<int>();

        modelBuilder.Entity<Contract>()
            .Property(c => c.AutoRenew)
            .HasConversion<int>();
        modelBuilder.Entity<Contract>()
            .Property(c => c.IsMandatory)
            .HasConversion<int>();

        modelBuilder.Entity<ApprovalHistory>(entity =>
        {
            entity.ToTable("approval_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Step).HasColumnName("step");
            entity.Property(e => e.Approver).HasColumnName("approver");
            entity.Property(e => e.Result).HasColumnName("result");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.Comments).HasColumnName("comments");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("invoices");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.InvoiceNumber).HasColumnName("invoice_number");
            entity.Property(e => e.InvoiceDate).HasColumnName("invoice_date");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.TaxRate).HasColumnName("tax_rate");
            entity.Property(e => e.InvoiceType).HasColumnName("invoice_type");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.PrePaymentProof).HasColumnName("pre_payment_proof");
            entity.Property(e => e.SignatureSeal).HasColumnName("signature_seal");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.ReviewNotes).HasColumnName("review_notes");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.AssistanceRequested).HasColumnName("assistance_requested");
            entity.Property(e => e.AssistanceType).HasColumnName("assistance_type");
            entity.Property(e => e.VerificationPoints).HasColumnName("verification_points");
            entity.Property(e => e.AssistanceDeadline).HasColumnName("assistance_deadline");
            entity.Property(e => e.AssistanceStatus).HasColumnName("assistance_status");
            entity.Property(e => e.DirectorApproved).HasColumnName("director_approved");
            entity.Property(e => e.DirectorApproverId).HasColumnName("director_approver_id");
            entity.Property(e => e.DirectorApprovedAt).HasColumnName("director_approved_at");
            entity.Property(e => e.DirectorApprovalNotes).HasColumnName("director_approval_notes");
            entity.Property(e => e.CreditReportUrl).HasColumnName("credit_report_url");
            entity.Property(e => e.FileName).HasColumnName("file_name");
            entity.Property(e => e.StoredFileName).HasColumnName("stored_file_name");
            entity.Property(e => e.FilePath).HasColumnName("file_path");
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.FileType).HasColumnName("file_type");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        });

        modelBuilder.Entity<WarehouseReceipt>(entity =>
        {
            entity.ToTable("warehouse_receipts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ReceiptNumber).HasColumnName("receipt_number");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.PurchaseOrderNumber).HasColumnName("purchase_order_number");
            entity.Property(e => e.ReceiptDate).HasColumnName("receipt_date");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount");
            entity.Property(e => e.TaxAmount).HasColumnName("tax_amount");
            entity.Property(e => e.GrandTotal).HasColumnName("grand_total");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.WarehouseLocation).HasColumnName("warehouse_location");
            entity.Property(e => e.ReceiverName).HasColumnName("receiver_name");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<WarehouseReceiptDetail>(entity =>
        {
            entity.ToTable("warehouse_receipt_details");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WarehouseReceiptId).HasColumnName("warehouse_receipt_id");
            entity.Property(e => e.LineNumber).HasColumnName("line_number");
            entity.Property(e => e.ItemCode).HasColumnName("item_code");
            entity.Property(e => e.ItemName).HasColumnName("item_name");
            entity.Property(e => e.Specification).HasColumnName("specification");
            entity.Property(e => e.Unit).HasColumnName("unit");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.TaxRate).HasColumnName("tax_rate");
            entity.Property(e => e.TaxAmount).HasColumnName("tax_amount");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount");
            entity.Property(e => e.QualityStatus).HasColumnName("quality_status");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Reconciliation>(entity =>
        {
            entity.ToTable("reconciliation");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ReconciliationNumber).HasColumnName("reconciliation_number");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.WarehouseReceiptId).HasColumnName("warehouse_receipt_id");
            entity.Property(e => e.PeriodStart).HasColumnName("period_start");
            entity.Property(e => e.PeriodEnd).HasColumnName("period_end");
            entity.Property(e => e.TotalInvoiceAmount).HasColumnName("total_invoice_amount");
            entity.Property(e => e.TotalReceiptAmount).HasColumnName("total_receipt_amount");
            entity.Property(e => e.VarianceAmount).HasColumnName("variance_amount");
            entity.Property(e => e.VariancePercentage).HasColumnName("variance_percentage");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.MatchType).HasColumnName("match_type");
            entity.Property(e => e.ConfidenceScore).HasColumnName("confidence_score");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ConfirmedBy).HasColumnName("confirmed_by");
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<InvoiceReconciliationMatch>(entity =>
        {
            entity.ToTable("invoice_reconciliation_match");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ReconciliationId).HasColumnName("reconciliation_id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.WarehouseReceiptId).HasColumnName("warehouse_receipt_id");
            entity.Property(e => e.MatchType).HasColumnName("match_type");
            entity.Property(e => e.MatchConfidence).HasColumnName("match_confidence");
            entity.Property(e => e.InvoiceAmount).HasColumnName("invoice_amount");
            entity.Property(e => e.ReceiptAmount).HasColumnName("receipt_amount");
            entity.Property(e => e.VarianceAmount).HasColumnName("variance_amount");
            entity.Property(e => e.VariancePercentage).HasColumnName("variance_percentage");
            entity.Property(e => e.MatchedAt).HasColumnName("matched_at");
            entity.Property(e => e.MatchedBy).HasColumnName("matched_by");
            entity.Property(e => e.Notes).HasColumnName("notes");
        });

        modelBuilder.Entity<ReconciliationVarianceAnalysis>(entity =>
        {
            entity.ToTable("reconciliation_variance_analysis");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ReconciliationId).HasColumnName("reconciliation_id");
            entity.Property(e => e.VarianceType).HasColumnName("variance_type");
            entity.Property(e => e.VarianceAmount).HasColumnName("variance_amount");
            entity.Property(e => e.ExpectedAmount).HasColumnName("expected_amount");
            entity.Property(e => e.ActualAmount).HasColumnName("actual_amount");
            entity.Property(e => e.VariancePercentage).HasColumnName("variance_percentage");
            entity.Property(e => e.Severity).HasColumnName("severity");
            entity.Property(e => e.RootCause).HasColumnName("root_cause");
            entity.Property(e => e.ResolutionAction).HasColumnName("resolution_action");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ReconciliationStatusHistory>(entity =>
        {
            entity.ToTable("reconciliation_status_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ReconciliationId).HasColumnName("reconciliation_id");
            entity.Property(e => e.FromStatus).HasColumnName("from_status");
            entity.Property(e => e.ToStatus).HasColumnName("to_status");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.ChangedAt).HasColumnName("changed_at");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.Notes).HasColumnName("notes");
        });

        modelBuilder.Entity<ReconciliationThreshold>(entity =>
        {
            entity.ToTable("reconciliation_thresholds");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ThresholdName).HasColumnName("threshold_name");
            entity.Property(e => e.AcceptableVariancePercentage).HasColumnName("acceptable_variance_percentage");
            entity.Property(e => e.WarningVariancePercentage).HasColumnName("warning_variance_percentage");
            entity.Property(e => e.AutoMatchEnabled).HasColumnName("auto_match_enabled");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<InvoiceFile>(entity =>
        {
            entity.ToTable("invoice_files");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.ReconciliationId).HasColumnName("reconciliation_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.OriginalName).HasColumnName("original_name");
            entity.Property(e => e.StoredName).HasColumnName("stored_name");
            entity.Property(e => e.StoragePath).HasColumnName("storage_path");
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.MimeType).HasColumnName("mime_type");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");
            entity.Property(e => e.UploadedByName).HasColumnName("uploaded_by_name");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
            entity.Property(e => e.Checksum).HasColumnName("checksum");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<InvoiceRestoreRequest>(entity =>
        {
            entity.ToTable("invoice_restore_requests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoiceId");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.RequestedBy).HasColumnName("requestedBy");
            entity.Property(e => e.RequestedAt).HasColumnName("requestedAt");
            entity.Property(e => e.DecidedBy).HasColumnName("decidedBy");
            entity.Property(e => e.DecidedAt).HasColumnName("decidedAt");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.DecisionNotes).HasColumnName("decisionNotes");
        });

        modelBuilder.Entity<Settlement>(entity =>
        {
            entity.ToTable("settlements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StatementNumber).HasColumnName("statement_number");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.PeriodStart).HasColumnName("period_start");
            entity.Property(e => e.PeriodEnd).HasColumnName("period_end");
            entity.Property(e => e.TotalInvoices).HasColumnName("total_invoices");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount");
            entity.Property(e => e.TaxAmount).HasColumnName("tax_amount");
            entity.Property(e => e.GrandTotal).HasColumnName("grand_total");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ReviewerId).HasColumnName("reviewer_id");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewNotes).HasColumnName("review_notes");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.DirectorApproved).HasColumnName("director_approved");
            entity.Property(e => e.DirectorApproverId).HasColumnName("director_approver_id");
            entity.Property(e => e.DirectorApprovedAt).HasColumnName("director_approved_at");
            entity.Property(e => e.DirectorApprovalNotes).HasColumnName("director_approval_notes");
            entity.Property(e => e.ExceptionalReason).HasColumnName("exceptional_reason");
            entity.Property(e => e.PaymentDueDate).HasColumnName("payment_due_date");
            entity.Property(e => e.PaidDate).HasColumnName("paid_date");
            entity.Property(e => e.DisputeReceived).HasColumnName("dispute_received");
            entity.Property(e => e.DisputeReason).HasColumnName("dispute_reason");
            entity.Property(e => e.DisputedItems).HasColumnName("disputed_items");
            entity.Property(e => e.SupportingDocuments).HasColumnName("supporting_documents");
            entity.Property(e => e.DisputeProcessorId).HasColumnName("dispute_processor_id");
            entity.Property(e => e.DisputeReceivedAt).HasColumnName("dispute_received_at");
            entity.Property(e => e.Details).HasColumnName("details");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderNumber).HasColumnName("order_number");
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.ActualAmount).HasColumnName("actual_amount");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<SupplierRating>(entity =>
        {
            entity.ToTable("supplier_ratings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.PeriodStart).HasColumnName("periodStart");
            entity.Property(e => e.PeriodEnd).HasColumnName("periodEnd");
            entity.Property(e => e.OnTimeDelivery).HasColumnName("onTimeDelivery");
            entity.Property(e => e.QualityScore).HasColumnName("qualityScore");
            entity.Property(e => e.ServiceScore).HasColumnName("serviceScore");
            entity.Property(e => e.CostScore).HasColumnName("costScore");
            entity.Property(e => e.OverallScore).HasColumnName("overallScore");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
        });

        modelBuilder.Entity<SupplierCompletionHistory>(entity =>
        {
            entity.ToTable("supplier_completion_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.ProfileCompletion).HasColumnName("profileCompletion");
            entity.Property(e => e.DocumentCompletion).HasColumnName("documentCompletion");
            entity.Property(e => e.CompletionScore).HasColumnName("completionScore");
            entity.Property(e => e.CompletionStatus).HasColumnName("completionStatus");
            entity.Property(e => e.MissingProfileFields).HasColumnName("missingProfileFields");
            entity.Property(e => e.MissingDocumentTypes).HasColumnName("missingDocumentTypes");
            entity.Property(e => e.TriggeredBy).HasColumnName("triggeredBy");
            entity.Property(e => e.TriggerReason).HasColumnName("triggerReason");
            entity.Property(e => e.RecordedAt).HasColumnName("recordedAt");
        });

        modelBuilder.Entity<SupplierChangeRequest>(entity =>
        {
            entity.ToTable("supplier_change_requests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.ChangeType).HasColumnName("changeType");
            entity.Property(e => e.RiskLevel).HasColumnName("riskLevel");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CurrentStep).HasColumnName("currentStep");
            entity.Property(e => e.Payload).HasColumnName("payload");
            entity.Property(e => e.SubmittedBy).HasColumnName("submittedBy");
            entity.Property(e => e.SubmittedAt).HasColumnName("submittedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
            entity.Property(e => e.RequiresQuality).HasColumnName("requiresQuality");
        });

        modelBuilder.Entity<SupplierBaseline>(entity =>
        {
            entity.ToTable("supplier_baselines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.Payload).HasColumnName("payload");
            entity.Property(e => e.Checksum).HasColumnName("checksum");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
        });

        modelBuilder.Entity<SupplierRiskAssessment>(entity =>
        {
            entity.ToTable("supplier_risk_assessments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.RiskLevel).HasColumnName("risk_level");
            entity.Property(e => e.RiskScore).HasColumnName("risk_score");
            entity.Property(e => e.RiskType).HasColumnName("risk_type");
            entity.Property(e => e.AssessmentDate).HasColumnName("assessment_date");
            entity.Property(e => e.AssessedBy).HasColumnName("assessed_by");
            entity.Property(e => e.RiskFactors).HasColumnName("risk_factors");
            entity.Property(e => e.MitigationRecommendations).HasColumnName("mitigation_recommendations");
        });

        modelBuilder.Entity<SupplierUpgradeApplication>(entity =>
        {
            entity.ToTable("supplier_upgrade_applications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CurrentStep).HasColumnName("currentStep");
            entity.Property(e => e.SubmittedAt).HasColumnName("submittedAt");
            entity.Property(e => e.SubmittedBy).HasColumnName("submittedBy");
            entity.Property(e => e.DueAt).HasColumnName("dueAt");
            entity.Property(e => e.WorkflowId).HasColumnName("workflowId");
            entity.Property(e => e.RejectionReason).HasColumnName("rejectionReason");
            entity.Property(e => e.ResubmittedAt).HasColumnName("resubmittedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
        });

        modelBuilder.Entity<SupplierUpgradeDocument>(entity =>
        {
            entity.ToTable("supplier_upgrade_documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ApplicationId).HasColumnName("applicationId");
            entity.Property(e => e.RequirementCode).HasColumnName("requirementCode");
            entity.Property(e => e.RequirementName).HasColumnName("requirementName");
            entity.Property(e => e.FileId).HasColumnName("fileId");
            entity.Property(e => e.UploadedAt).HasColumnName("uploadedAt");
            entity.Property(e => e.UploadedBy).HasColumnName("uploadedBy");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Notes).HasColumnName("notes");
        });

        modelBuilder.Entity<SupplierUpgradeReview>(entity =>
        {
            entity.ToTable("supplier_upgrade_reviews");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ApplicationId).HasColumnName("applicationId");
            entity.Property(e => e.StepKey).HasColumnName("stepKey");
            entity.Property(e => e.StepName).HasColumnName("stepName");
            entity.Property(e => e.Decision).HasColumnName("decision");
            entity.Property(e => e.Comments).HasColumnName("comments");
            entity.Property(e => e.DecidedById).HasColumnName("decidedById");
            entity.Property(e => e.DecidedByName).HasColumnName("decidedByName");
            entity.Property(e => e.DecidedAt).HasColumnName("decidedAt");
        });

        modelBuilder.Entity<FileRecord>(entity =>
        {
            entity.ToTable("files");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.AgreementNumber).HasColumnName("agreementNumber");
            entity.Property(e => e.FileType).HasColumnName("fileType");
            entity.Property(e => e.ValidFrom).HasColumnName("validFrom");
            entity.Property(e => e.ValidTo).HasColumnName("validTo");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.UploadTime).HasColumnName("uploadTime");
            entity.Property(e => e.UploaderName).HasColumnName("uploaderName");
            entity.Property(e => e.OriginalName).HasColumnName("originalName");
            entity.Property(e => e.StoredName).HasColumnName("storedName");
        });

        modelBuilder.Entity<TempSupplierUser>(entity =>
        {
            entity.ToTable("temp_supplier_users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Username).HasColumnName("username");
            entity.Property(e => e.PasswordHash).HasColumnName("passwordHash");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.ExpiresAt).HasColumnName("expiresAt");
            entity.Property(e => e.LastLoginAt).HasColumnName("lastLoginAt");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
        });

        modelBuilder.Entity<TempAccountSequence>(entity =>
        {
            entity.ToTable("temp_account_sequences");
            entity.HasKey(e => e.Currency);
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.LastNumber).HasColumnName("lastNumber");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
        });

        modelBuilder.Entity<ExternalSupplierInvitation>(entity =>
        {
            entity.ToTable("external_supplier_invitations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RfqId).HasColumnName("rfq_id");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.CompanyName).HasColumnName("company_name");
            entity.Property(e => e.ContactPerson).HasColumnName("contact_person");
            entity.Property(e => e.InvitationToken).HasColumnName("invitation_token");
            entity.Property(e => e.RegistrationCompleted).HasColumnName("registration_completed");
            entity.Property(e => e.RegisteredSupplierId).HasColumnName("registered_supplier_id");
            entity.Property(e => e.InvitedAt).HasColumnName("invited_at");
            entity.Property(e => e.RegisteredAt).HasColumnName("registered_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.Status).HasColumnName("status");
        });

        modelBuilder.Entity<SupplierFileUpload>(entity =>
        {
            entity.ToTable("supplier_file_uploads");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.FileId).HasColumnName("fileId");
            entity.Property(e => e.FileName).HasColumnName("fileName");
            entity.Property(e => e.FileDescription).HasColumnName("fileDescription");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CurrentStep).HasColumnName("currentStep");
            entity.Property(e => e.SubmittedBy).HasColumnName("submittedBy");
            entity.Property(e => e.SubmittedAt).HasColumnName("submittedAt");
            entity.Property(e => e.RiskLevel).HasColumnName("riskLevel");
            entity.Property(e => e.ValidFrom).HasColumnName("validFrom");
            entity.Property(e => e.ValidTo).HasColumnName("validTo");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
        });

        modelBuilder.Entity<SupplierFileApproval>(entity =>
        {
            entity.ToTable("supplier_file_approvals");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UploadId).HasColumnName("uploadId");
            entity.Property(e => e.Step).HasColumnName("step");
            entity.Property(e => e.StepName).HasColumnName("stepName");
            entity.Property(e => e.ApproverId).HasColumnName("approverId");
            entity.Property(e => e.ApproverName).HasColumnName("approverName");
            entity.Property(e => e.Decision).HasColumnName("decision");
            entity.Property(e => e.Comments).HasColumnName("comments");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
        });

        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.ToTable("workflow_instances");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WorkflowType).HasColumnName("workflowType");
            entity.Property(e => e.EntityType).HasColumnName("entityType");
            entity.Property(e => e.EntityId).HasColumnName("entityId");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CurrentStep).HasColumnName("currentStep");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
        });

        modelBuilder.Entity<WorkflowStep>(entity =>
        {
            entity.ToTable("workflow_steps");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WorkflowId).HasColumnName("workflowId");
            entity.Property(e => e.StepOrder).HasColumnName("stepOrder");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Assignee).HasColumnName("assignee");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.DueAt).HasColumnName("dueAt");
            entity.Property(e => e.CompletedAt).HasColumnName("completedAt");
            entity.Property(e => e.Notes).HasColumnName("notes");
        });

        modelBuilder.Entity<ReminderQueue>(entity =>
        {
            entity.ToTable("reminder_queue");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.EntityType).HasColumnName("entityType");
            entity.Property(e => e.EntityId).HasColumnName("entityId");
            entity.Property(e => e.DueAt).HasColumnName("dueAt");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Payload).HasColumnName("payload");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.SentAt).HasColumnName("sentAt");
        });

        modelBuilder.Entity<ContractReminderSetting>(entity =>
        {
            entity.ToTable("contract_reminder_settings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Scope).HasColumnName("scope");
            entity.Property(e => e.Settings).HasColumnName("settings");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
            entity.Property(e => e.UpdatedBy).HasColumnName("updatedBy");
        });

        modelBuilder.Entity<RfqProject>(entity =>
        {
            entity.ToTable("rfq_projects");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.DueDate).HasColumnName("dueDate");
            entity.Property(e => e.LockDate).HasColumnName("lockDate");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
        });

        modelBuilder.Entity<RfqQuote>(entity =>
        {
            entity.ToTable("rfq_quotes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProjectId).HasColumnName("projectId");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.LeadTime).HasColumnName("leadTime");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.SubmittedAt).HasColumnName("submittedAt");
            entity.Property(e => e.LockedAt).HasColumnName("lockedAt");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.ChangeLog).HasColumnName("changeLog");
        });

        modelBuilder.Entity<RfqProjectSupplier>(entity =>
        {
            entity.ToTable("rfq_project_suppliers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProjectId).HasColumnName("projectId");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.InvitedAt).HasColumnName("invitedAt");
            entity.Property(e => e.RespondedAt).HasColumnName("respondedAt");
        });

        modelBuilder.Entity<FileUploadConfig>(entity =>
        {
            entity.ToTable("file_upload_configs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Scenario).HasColumnName("scenario");
            entity.Property(e => e.ScenarioName).HasColumnName("scenario_name");
            entity.Property(e => e.ScenarioDescription).HasColumnName("scenario_description");
            entity.Property(e => e.AllowedFormats).HasColumnName("allowed_formats");
            entity.Property(e => e.MaxFileSize).HasColumnName("max_file_size");
            entity.Property(e => e.MaxFileCount).HasColumnName("max_file_count");
            entity.Property(e => e.EnableVirusScan).HasColumnName("enable_virus_scan");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<FileScanRecord>(entity =>
        {
            entity.ToTable("file_scan_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FilePath).HasColumnName("file_path");
            entity.Property(e => e.OriginalName).HasColumnName("original_name");
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.MimeType).HasColumnName("mime_type");
            entity.Property(e => e.ScanStatus).HasColumnName("scan_status");
            entity.Property(e => e.ScanResult).HasColumnName("scan_result");
            entity.Property(e => e.ScanEngine).HasColumnName("scan_engine");
            entity.Property(e => e.ScanDuration).HasColumnName("scan_duration");
            entity.Property(e => e.IsClean).HasColumnName("is_clean");
            entity.Property(e => e.ThreatName).HasColumnName("threat_name");
            entity.Property(e => e.ScannedAt).HasColumnName("scanned_at");
            entity.Property(e => e.UploadedBy).HasColumnName("uploaded_by");
            entity.Property(e => e.Scenario).HasColumnName("scenario");
            entity.Property(e => e.Quarantined).HasColumnName("quarantined");
            entity.Property(e => e.QuarantinePath).HasColumnName("quarantine_path");
        });

        modelBuilder.Entity<DocumentTypeDef>(entity =>
        {
            entity.ToTable("document_type_defs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.IsRequired).HasColumnName("isRequired");
            entity.Property(e => e.Description).HasColumnName("description");
        });

        modelBuilder.Entity<PurchasingGroupSupplier>(entity =>
        {
            entity.ToTable("purchasing_group_suppliers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GroupId).HasColumnName("groupId");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.AssignedAt).HasColumnName("assignedAt");
            entity.Property(e => e.AssignedBy).HasColumnName("assignedBy");
            entity.Property(e => e.IsPrimary).HasColumnName("isPrimary");
            entity.Property(e => e.Notes).HasColumnName("notes");
        });

        modelBuilder.Entity<BuyerSupplierAccessCache>(entity =>
        {
            entity.ToTable("buyer_supplier_access_cache");
            entity.HasKey(e => new { e.BuyerId, e.SupplierId, e.AccessType });
            entity.Property(e => e.BuyerId).HasColumnName("buyerId");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.AccessType).HasColumnName("accessType");
            entity.Property(e => e.GroupId).HasColumnName("groupId");
            entity.Property(e => e.GroupName).HasColumnName("groupName");
            entity.Property(e => e.LastUpdated).HasColumnName("lastUpdated");
        });


        ApplyNumericConventions(modelBuilder);
    }

    private static void ApplyNumericConventions(ModelBuilder modelBuilder)
    {
        var intToLongConverter = new ValueConverter<int, long>(
            value => value,
            value => checked((int)value));
        var nullableIntToLongConverter = new ValueConverter<int?, long?>(
            value => value.HasValue ? value.Value : null,
            value => value.HasValue ? checked((int)value.Value) : null);
        var deliveryPeriodConverter = new ValueConverter<string?, decimal?>(
            value => ParseDecimalInvariant(value),
            value => value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : null);
        var decimalToDoubleConverter = new ValueConverter<decimal, double>(
            value => (double)value,
            value => Convert.ToDecimal(value));
        var nullableDecimalToDoubleConverter = new ValueConverter<decimal?, double?>(
            value => value.HasValue ? (double?)value.Value : null,
            value => value.HasValue ? (decimal?)Convert.ToDecimal(value.Value) : null);
        var nullableDecimalToIntConverter = new ValueConverter<decimal?, int?>(
            value => value.HasValue ? (int?)Convert.ToInt32(value.Value) : null,
            value => value.HasValue ? (decimal?)Convert.ToDecimal(value.Value) : null);
        var nullableLongToIntConverter = new ValueConverter<long?, int?>(
            value => value.HasValue ? checked((int)value.Value) : null,
            value => value.HasValue ? (long?)value.Value : null);
        var stringToDateTimeConverter = new ValueConverter<string?, DateTime?>(
            value => ParseDateTimeInvariant(value),
            value => FormatDateTimeInvariant(value));
        var dateTimeToStringConverter = new ValueConverter<DateTime, string>(
            value => FormatDateTimeInvariant(value) ?? value.ToString("o", CultureInfo.InvariantCulture),
            value => ParseDateTimeInvariant(value) ?? DateTime.MinValue);
        var stringToDateOnlyConverter = new ValueConverter<string?, DateOnly?>(
            value => ParseDateOnlyInvariant(value),
            value => FormatDateOnlyInvariant(value));

        ApplyAuditDateTimeStringConversions(modelBuilder, stringToDateTimeConverter);

        modelBuilder.Entity<Supplier>()
            .Property(s => s.Id)
            .HasConversion(intToLongConverter)
            .HasColumnType("bigint");
        modelBuilder.Entity<Supplier>()
            .Property(s => s.TempAccountUserId)
            .HasConversion(nullableIntToLongConverter)
            .HasColumnType("bigint");
        modelBuilder.Entity<Supplier>()
            .Property(s => s.CreatedAt)
            .HasConversion(stringToDateTimeConverter)
            .HasColumnType("datetime2(3)");
        modelBuilder.Entity<Supplier>()
            .Property(s => s.UpdatedAt)
            .HasConversion(stringToDateTimeConverter)
            .HasColumnType("datetime2(3)");
        modelBuilder.Entity<Supplier>()
            .Property(s => s.ComplianceReviewedAt)
            .HasConversion(stringToDateTimeConverter)
            .HasColumnType("datetime2(3)");
        modelBuilder.Entity<Supplier>()
            .Property(s => s.CompletionLastUpdated)
            .HasConversion(stringToDateTimeConverter)
            .HasColumnType("datetime2(3)");
        modelBuilder.Entity<Supplier>()
            .Property(s => s.TempAccountExpiresAt)
            .HasConversion(stringToDateTimeConverter)
            .HasColumnType("datetime2(3)");
        modelBuilder.Entity<Supplier>()
            .Property(s => s.ProfileCompletion)
            .HasConversion(nullableDecimalToIntConverter)
            .HasColumnType("int");
        modelBuilder.Entity<Supplier>()
            .Property(s => s.DocumentCompletion)
            .HasConversion(nullableDecimalToIntConverter)
            .HasColumnType("int");
        modelBuilder.Entity<Supplier>()
            .Property(s => s.CompletionScore)
            .HasConversion(nullableDecimalToIntConverter)
            .HasColumnType("int");

        modelBuilder.Entity<SupplierRegistrationApplication>()
            .Property(a => a.BusinessLicenseFileSize)
            .HasConversion(nullableLongToIntConverter)
            .HasColumnType("int");
        modelBuilder.Entity<SupplierRegistrationApplication>()
            .Property(a => a.BankAccountFileSize)
            .HasConversion(nullableLongToIntConverter)
            .HasColumnType("int");

        modelBuilder.Entity<Rfq>()
            .Property(r => r.Id)
            .HasColumnType("bigint");
        modelBuilder.Entity<Rfq>()
            .Property(r => r.SelectedQuoteId)
            .HasColumnType("bigint");
        modelBuilder.Entity<Rfq>()
            .Property(r => r.DeliveryPeriod)
            .HasConversion(deliveryPeriodConverter)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<Rfq>()
            .Property(r => r.RequirementDate)
            .HasConversion(stringToDateOnlyConverter)
            .HasColumnType("date");
        modelBuilder.Entity<Rfq>()
            .Property(r => r.Amount)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<Rfq>()
            .Property(r => r.BudgetAmount)
            .HasColumnType("decimal(18,6)");

        modelBuilder.Entity<RfqLineItem>()
            .Property(r => r.Id)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqLineItem>()
            .Property(r => r.RfqId)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqLineItem>()
            .Property(r => r.SelectedQuoteId)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqLineItem>()
            .Property(r => r.PoId)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqLineItem>()
            .Property(r => r.Quantity)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<RfqLineItem>()
            .Property(r => r.EstimatedUnitPrice)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<RfqLineItemApprovalHistory>()
            .Property(h => h.Id)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqPriceAuditRecord>()
            .Property(r => r.Id)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqPriceAuditRecord>()
            .Property(r => r.RfqId)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqPriceAuditRecord>()
            .Property(r => r.RfqLineItemId)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqPriceAuditRecord>()
            .Property(r => r.QuoteId)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqPriceAuditRecord>()
            .Property(r => r.SupplierId)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqPriceAuditRecord>()
            .Property(r => r.SelectedQuoteId)
            .HasColumnType("bigint");
        modelBuilder.Entity<RfqPriceAuditRecord>()
            .Property(r => r.SelectedSupplierId)
            .HasColumnType("bigint");


        modelBuilder.Entity<Quote>()
            .Property(q => q.Id)
            .HasColumnType("bigint");
        modelBuilder.Entity<Quote>()
            .Property(q => q.RfqId)
            .HasColumnType("bigint");
        modelBuilder.Entity<Quote>()
            .Property(q => q.SupplierId)
            .HasConversion(intToLongConverter)
            .HasColumnType("bigint");
        modelBuilder.Entity<Quote>()
            .Property(q => q.UnitPrice)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<Quote>()
            .Property(q => q.TotalAmount)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<Quote>()
            .Property(q => q.TotalStandardCostLocal)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<Quote>()
            .Property(q => q.TotalStandardCostUsd)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<Quote>()
            .Property(q => q.TotalTariffAmountLocal)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<Quote>()
            .Property(q => q.TotalTariffAmountUsd)
            .HasColumnType("decimal(18,6)");

        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.Id)
            .HasColumnType("bigint");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.QuoteId)
            .HasColumnType("bigint");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.RfqLineItemId)
            .HasColumnType("bigint");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.DeliveryPeriod)
            .HasColumnType("int");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.UnitPrice)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.MinimumOrderQuantity)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.StandardPackageQuantity)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.TotalPrice)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.OriginalPriceUsd)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.ExchangeRate)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.TariffRate)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.TariffRatePercent)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.TariffAmountLocal)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.TariffAmountUsd)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.SpecialTariffRate)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.SpecialTariffRatePercent)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.SpecialTariffAmountLocal)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.SpecialTariffAmountUsd)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.StandardCostLocal)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.StandardCostUsd)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteLineItem>()
            .Property(q => q.ExchangeRateDate)
            .HasConversion(stringToDateOnlyConverter)
            .HasColumnType("date");

        modelBuilder.Entity<QuoteVersion>()
            .Property(q => q.TotalAmount)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<QuoteVersion>()
            .Property(q => q.UnitPrice)
            .HasColumnType("decimal(18,6)");

        modelBuilder.Entity<ExchangeRateHistory>()
            .Property(e => e.Rate)
            .HasColumnType("decimal(18,6)");

        modelBuilder.Entity<FreightRateHistory>()
            .Property(e => e.Rate)
            .HasColumnType("decimal(18,6)");

        modelBuilder.Entity<MaterialRequisition>()
            .Property(r => r.EstimatedBudget)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<MaterialRequisition>()
            .Property(r => r.Quantity)
            .HasColumnType("decimal(18,6)");

        modelBuilder.Entity<MaterialRequisitionItem>()
            .Property(r => r.EstimatedBudget)
            .HasColumnType("decimal(18,6)");
        modelBuilder.Entity<MaterialRequisitionItem>()
            .Property(r => r.Quantity)
            .HasColumnType("decimal(18,6)");

        modelBuilder.Entity<PriceComparisonAttachment>()
            .Property(p => p.Id)
            .HasConversion(intToLongConverter)
            .HasColumnType("bigint");
        modelBuilder.Entity<PriceComparisonAttachment>()
            .Property(p => p.RfqId)
            .HasConversion(intToLongConverter)
            .HasColumnType("bigint");
        modelBuilder.Entity<PriceComparisonAttachment>()
            .Property(p => p.LineItemId)
            .HasConversion(nullableIntToLongConverter)
            .HasColumnType("bigint");
        modelBuilder.Entity<PriceComparisonAttachment>()
            .Property(p => p.PlatformPrice)
            .HasColumnType("decimal(18,6)");

        modelBuilder.Entity<PurchaseOrder>()
            .Property(p => p.TotalAmount)
            .HasColumnType("decimal(18,6)");

        modelBuilder.Entity<Invoice>()
            .Property(i => i.Amount)
            .HasColumnType("decimal(18,6)");

        modelBuilder.Entity<AuditLog>()
            .Property(a => a.CreatedAt)
            .HasConversion(dateTimeToStringConverter);

        ApplyFloatCompatibility(modelBuilder, decimalToDoubleConverter, nullableDecimalToDoubleConverter);
    }

    private static void ApplyAuditDateTimeStringConversions(
        ModelBuilder modelBuilder,
        ValueConverter<string?, DateTime?> stringToDateTimeConverter)
    {
        var auditColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "created_at",
            "updated_at"
        };

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(storeObject);
                if (columnName == null || !auditColumns.Contains(columnName))
                {
                    continue;
                }

                if (property.ClrType == typeof(string))
                {
                    var configuredColumnType = property.GetColumnType();
                    if (!string.IsNullOrWhiteSpace(configuredColumnType) &&
                        !IsDateTimeColumnType(configuredColumnType))
                    {
                        continue;
                    }

                    if (property.GetValueConverter() == null)
                    {
                        property.SetValueConverter(stringToDateTimeConverter);
                    }

                    property.SetColumnType("datetime2(3)");
                    continue;
                }

                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetColumnType("datetime2(3)");
                }
            }
        }
    }

    private static void ApplyFloatCompatibility(
        ModelBuilder modelBuilder,
        ValueConverter<decimal, double> decimalToDoubleConverter,
        ValueConverter<decimal?, double?> nullableDecimalToDoubleConverter)
    {
        var floatColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["exchange_rate_history"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "rate" },
            ["freight_rate_history"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "rate" },
            ["invoice_reconciliation_match"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "invoice_amount",
                "match_confidence",
                "receipt_amount",
                "variance_amount",
                "variance_percentage",
            },
            ["material_requisition_items"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "estimated_budget",
                "quantity",
            },
            ["material_requisitions"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "estimated_budget",
                "quantity",
            },
            ["orders"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "actual_amount" },
            ["price_comparison_attachments"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "platform_price" },
            ["purchase_orders"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "total_amount" },
            ["quote_line_items"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "exchange_rate",
                "minimum_order_quantity",
                "original_price_usd",
                "special_tariff_amount_local",
                "special_tariff_amount_usd",
                "special_tariff_rate",
                "special_tariff_rate_percent",
                "standard_cost_local",
                "standard_cost_usd",
                "standard_package_quantity",
                "tariff_amount_local",
                "tariff_amount_usd",
                "tariff_rate",
                "tariff_rate_percent",
                "total_price",
                "unit_price",
            },
            ["quote_price_comparisons"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "online_price",
                "price_variance_percent",
            },
            ["quote_versions"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "total_amount",
                "unit_price",
            },
            ["quotes"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "total_amount",
                "total_standard_cost_local",
                "total_standard_cost_usd",
                "total_tariff_amount_local",
                "total_tariff_amount_usd",
                "unit_price",
            },
            ["reconciliation"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "confidence_score",
                "total_invoice_amount",
                "total_receipt_amount",
                "variance_amount",
                "variance_percentage",
            },
            ["reconciliation_thresholds"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "acceptable_variance_percentage",
                "warning_variance_percentage",
            },
            ["reconciliation_variance_analysis"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "actual_amount",
                "expected_amount",
                "variance_amount",
                "variance_percentage",
            },
            ["rfq_line_items"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "estimated_unit_price",
                "quantity",
            },
            ["rfq_quote_items"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "minimum_order_quantity",
                "quantity",
                "total_amount",
                "unit_price",
            },
            ["rfq_quotes"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "price" },
            ["risk_alerts"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "risk_score" },
            ["settlements"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "grand_total",
                "tax_amount",
                "total_amount",
            },
            ["supplier_completion_history"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "completionScore",
                "documentCompletion",
                "profileCompletion",
            },
            ["supplier_ratings"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "costScore",
                "onTimeDelivery",
                "overallScore",
                "qualityScore",
                "serviceScore",
            },
            ["supplier_risk_assessments"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "risk_score" },
            ["tariff_rates"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "rate_2023",
                "rate_2024",
                "rate_2025",
            },
            ["warehouse_receipt_details"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "amount",
                "quantity",
                "tax_amount",
                "tax_rate",
                "total_amount",
                "unit_price",
            },
            ["warehouse_receipts"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "grand_total",
                "tax_amount",
                "total_amount",
            },
        };

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            if (!floatColumns.TryGetValue(tableName, out var columns))
            {
                continue;
            }

            var tableId = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(tableId);
                if (string.IsNullOrWhiteSpace(columnName) || !columns.Contains(columnName))
                {
                    continue;
                }

                if (property.ClrType == typeof(decimal))
                {
                    property.SetValueConverter(decimalToDoubleConverter);
                    property.SetColumnType("float");
                }
                else if (property.ClrType == typeof(decimal?))
                {
                    property.SetValueConverter(nullableDecimalToDoubleConverter);
                    property.SetColumnType("float");
                }
            }
        }
    }

    private static decimal? ParseDecimalInvariant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateTime? ParseDateTimeInvariant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private static bool IsDateTimeColumnType(string columnType)
    {
        return columnType.StartsWith("datetime", StringComparison.OrdinalIgnoreCase)
            || columnType.StartsWith("smalldatetime", StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnType, "date", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FormatDateTimeInvariant(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("o", CultureInfo.InvariantCulture)
            : null;
    }

    private static DateOnly? ParseDateOnlyInvariant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static string? FormatDateOnlyInvariant(DateOnly? value)
    {
        return value.HasValue
            ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }
}











