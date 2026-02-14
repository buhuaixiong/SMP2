namespace SupplierSystem.Domain.Entities;

public sealed class SupplierRegistrationApplication
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = null!;
    public string? EnglishName { get; set; }
    public string? ChineseName { get; set; }
    public string CompanyType { get; set; } = null!;
    public string? CompanyTypeOther { get; set; }
    public string? AuthorizedCapital { get; set; }
    public string? IssuedCapital { get; set; }
    public string? Directors { get; set; }
    public string? Owners { get; set; }
    public string RegisteredOffice { get; set; } = null!;
    public string BusinessRegistrationNumber { get; set; } = null!;
    public string BusinessAddress { get; set; } = null!;
    public string? BusinessPhone { get; set; }
    public string? BusinessFax { get; set; }
    public string ContactName { get; set; } = null!;
    public string ContactEmail { get; set; } = null!;
    public string ContactPhone { get; set; } = null!;
    public string? FinanceContactName { get; set; }
    public string? FinanceContactEmail { get; set; }
    public string? FinanceContactPhone { get; set; }
    public string? BusinessNature { get; set; }
    public string OperatingCurrency { get; set; } = null!;
    public string DeliveryLocation { get; set; } = null!;
    public string ShipCode { get; set; } = null!;
    public string? ProductOrigin { get; set; }
    public string? ProductTypes { get; set; }
    public string? InvoiceType { get; set; }
    public string? PaymentTermsDays { get; set; }
    public string? PaymentMethods { get; set; }
    public string? PaymentMethodsOther { get; set; }
    public string BankName { get; set; } = null!;
    public string BankAddress { get; set; } = null!;
    public string BankAccountNumber { get; set; } = null!;
    public string? SwiftCode { get; set; }
    public string? Notes { get; set; }
    public string? SupplierClassification { get; set; }
    public string? SupplierCode { get; set; }
    public string? BusinessLicenseFileName { get; set; }
    public string? BusinessLicenseFilePath { get; set; }
    public string? BusinessLicenseFileMime { get; set; }
    public long? BusinessLicenseFileSize { get; set; }
    public string? BankAccountFileName { get; set; }
    public string? BankAccountFilePath { get; set; }
    public string? BankAccountFileMime { get; set; }
    public long? BankAccountFileSize { get; set; }
    public string? ProcurementEmail { get; set; }
    public int? SupplierId { get; set; }
    public int? TempAccountUserId { get; set; }
    public string? DraftToken { get; set; }
    public int BaselineVersion { get; set; }
    public string? Status { get; set; }
    public string? AssignedPurchaserEmail { get; set; }
    public string? AssignedPurchaserId { get; set; }
    public string? TrackingAccountId { get; set; }
    public bool TrackingAccountCreated { get; set; }
    public string? SupplierCodeBoundAt { get; set; }
    public string? SupplierCodeBoundBy { get; set; }
    public string? ActivationToken { get; set; }
    public string? ActivationTokenExpiry { get; set; }
    public string? ActivatedAt { get; set; }
    public string? QualityManagerId { get; set; }
    public string? QualityApprovalTime { get; set; }
    public string? QualityApprovalComment { get; set; }
    public string? QualityApprovalStatus { get; set; }
    public string? PurchaserId { get; set; }
    public string? PurchaserApprovalTime { get; set; }
    public string? PurchaserApprovalComment { get; set; }
    public string? PurchaserApprovalStatus { get; set; }
    public string? ProcurementManagerId { get; set; }
    public string? ProcurementManagerApprovalTime { get; set; }
    public string? ProcurementManagerApprovalComment { get; set; }
    public string? ProcurementManagerApprovalStatus { get; set; }
    public string? ProcurementDirectorId { get; set; }
    public string? ProcurementDirectorApprovalTime { get; set; }
    public string? ProcurementDirectorApprovalComment { get; set; }
    public string? ProcurementDirectorApprovalStatus { get; set; }
    public string? FinanceDirectorId { get; set; }
    public string? FinanceDirectorApprovalTime { get; set; }
    public string? FinanceDirectorApprovalComment { get; set; }
    public string? FinanceDirectorApprovalStatus { get; set; }
    public string? FinanceAccountantId { get; set; }
    public string? FinanceAccountantApprovalTime { get; set; }
    public string? FinanceAccountantApprovalComment { get; set; }
    public string? FinanceAccountantApprovalStatus { get; set; }
    public string? FinanceCashierId { get; set; }
    public string? FinanceCashierApprovalTime { get; set; }
    public string? FinanceCashierApprovalComment { get; set; }
    public string? FinanceCashierApprovalStatus { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
