namespace SupplierSystem.Domain.Entities;

public sealed class Rfq
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? DeliveryPeriod { get; set; }
    public string? Status { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? MaterialType { get; set; }
    public string? MaterialCategoryType { get; set; }
    public bool IsLineItemMode { get; set; }
    public string? DistributionCategory { get; set; }
    public string? DistributionSubcategory { get; set; }
    public string? RfqType { get; set; }
    public decimal? BudgetAmount { get; set; }
    public string? RequiredDocuments { get; set; }
    public string? EvaluationCriteria { get; set; }
    public string? ValidUntil { get; set; }
    public string? RequestingParty { get; set; }
    public string? RequestingDepartment { get; set; }
    public string? RequirementDate { get; set; }
    public string? DetailedParameters { get; set; }
    public int? MinSupplierCount { get; set; }
    public string? SupplierExceptionNote { get; set; }
    public long? SelectedQuoteId { get; set; }
    public string? ReviewCompletedAt { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? PrStatus { get; set; }
    public long? RequisitionId { get; set; }
}
