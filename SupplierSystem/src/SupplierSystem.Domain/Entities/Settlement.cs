namespace SupplierSystem.Domain.Entities;

public sealed class Settlement
{
    public int Id { get; set; }
    public string? StatementNumber { get; set; }
    public int? SupplierId { get; set; }
    public int? RfqId { get; set; }
    public string? Type { get; set; }
    public string? PeriodStart { get; set; }
    public string? PeriodEnd { get; set; }
    public int? TotalInvoices { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? GrandTotal { get; set; }
    public string? Status { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int? ReviewerId { get; set; }
    public string? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
    public bool? DirectorApproved { get; set; }
    public int? DirectorApproverId { get; set; }
    public string? DirectorApprovedAt { get; set; }
    public string? DirectorApprovalNotes { get; set; }
    public string? ExceptionalReason { get; set; }
    public string? PaymentDueDate { get; set; }
    public string? PaidDate { get; set; }
    public bool? DisputeReceived { get; set; }
    public string? DisputeReason { get; set; }
    public string? DisputedItems { get; set; }
    public string? SupportingDocuments { get; set; }
    public int? DisputeProcessorId { get; set; }
    public string? DisputeReceivedAt { get; set; }
    public string? Details { get; set; }
}
