namespace SupplierSystem.Api.Models.Rfq;

public sealed class CreateRfqItem
{
    public int? LineNumber { get; set; }
    public string? MaterialType { get; set; }
    public string? Description { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? TargetPrice { get; set; }
    public string? Remarks { get; set; }
}

public sealed class CreateRfqRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? RfqType { get; set; }
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
    public string? DeliveryPeriod { get; set; }
    public decimal? BudgetAmount { get; set; }
    public string? Currency { get; set; }
    public string? ValidUntil { get; set; }
    public List<CreateRfqItem>? Items { get; set; }
}

public sealed class QuoteItemRequest
{
    public int? LineNumber { get; set; }
    public string? Description { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Remarks { get; set; }
}

public sealed class SubmitQuoteRequest
{
    public decimal? TotalPrice { get; set; }
    public string? Currency { get; set; }
    public string? DeliveryPeriod { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? Remarks { get; set; }
    public List<QuoteItemRequest>? Items { get; set; }
}

public sealed class SendInvitationsRequest
{
    public List<int>? SupplierIds { get; set; }
}

public sealed class ReviewRfqRequest
{
    public int? SelectedQuoteId { get; set; }
    public string? ReviewScoresJson { get; set; }
    public string? Comments { get; set; }
}
