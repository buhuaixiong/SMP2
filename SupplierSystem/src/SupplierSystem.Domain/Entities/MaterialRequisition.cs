namespace SupplierSystem.Domain.Entities;

public sealed class MaterialRequisition
{
    public int Id { get; set; }
    public string RequisitionNumber { get; set; } = null!;
    public string RequestingDepartment { get; set; } = null!;
    public string RequestingPersonId { get; set; } = null!;
    public string? RequestingPersonName { get; set; }
    public string RequiredDate { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? ItemDescription { get; set; }
    public string? Specifications { get; set; }
    public decimal? EstimatedBudget { get; set; }
    public string? Currency { get; set; }
    public string? Priority { get; set; }
    public string? AttachmentFiles { get; set; }
    public string? Status { get; set; }
    public string? SubmittedAt { get; set; }
    public string? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public string? ApprovedAt { get; set; }
    public string? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public string? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public int? ConvertedToRfqId { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? Notes { get; set; }
}
