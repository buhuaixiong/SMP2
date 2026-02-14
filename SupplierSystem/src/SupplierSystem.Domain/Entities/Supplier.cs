namespace SupplierSystem.Domain.Entities;

public sealed class Supplier
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = null!;
    public string CompanyId { get; set; } = null!;
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Category { get; set; }
    public string? Address { get; set; }
    public string? Status { get; set; }
    public string? CurrentApprover { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
    public string? Notes { get; set; }
    public string? BankAccount { get; set; }
    public string? PaymentTerms { get; set; }
    public string? CreditRating { get; set; }
    public string? ServiceCategory { get; set; }
    public string? Region { get; set; }
    public string? Importance { get; set; }
    public string? ComplianceStatus { get; set; }
    public string? ComplianceNotes { get; set; }
    public string? ComplianceOwner { get; set; }
    public string? ComplianceReviewedAt { get; set; }
    public string? FinancialContact { get; set; }
    public string? PaymentCurrency { get; set; }
    public string? FaxNumber { get; set; }
    public string? BusinessRegistrationNumber { get; set; }
    public string? UpdatedAt { get; set; }
    public string? Stage { get; set; }
    public decimal? ProfileCompletion { get; set; }
    public decimal? DocumentCompletion { get; set; }
    public decimal? CompletionScore { get; set; }
    public string? CompletionStatus { get; set; }
    public string? CompletionLastUpdated { get; set; }
    public int? TempAccountUserId { get; set; }
    public string? TempAccountStatus { get; set; }
    public string? TempAccountExpiresAt { get; set; }
    public int BaselineVersion { get; set; }
    public string? SupplierCode { get; set; }
}
