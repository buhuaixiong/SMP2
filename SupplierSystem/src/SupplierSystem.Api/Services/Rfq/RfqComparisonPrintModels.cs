namespace SupplierSystem.Api.Services.Rfq;

public sealed class RfqComparisonPrintData
{
    public long RfqId { get; init; }
    public string Scope { get; init; } = "latest";
    public string? Title { get; init; }
    public string? Status { get; init; }
    public string? PrintedAt { get; init; }
    public string? PrintedBy { get; init; }
    public string? ReviewCompletedAt { get; init; }
    public string? MaterialType { get; init; }
    public string? RfqType { get; init; }
    public string? Currency { get; init; }
    public decimal? BudgetAmount { get; init; }
    public string? ValidUntil { get; init; }
    public string? CreatedBy { get; init; }
    public string? CreatedAt { get; init; }
    public string? RequestingParty { get; init; }
    public string? RequestingDepartment { get; init; }
    public string? Description { get; init; }
    public string? SelectedSupplierSummary { get; init; }
    public int InvitedSupplierCount { get; init; }
    public int SubmittedSupplierCount { get; init; }
    public int WithdrawnSupplierCount { get; init; }
    public RfqComparisonPrintRoundSummary? CurrentRound { get; init; }
    public RfqComparisonPrintRoundSummary? LatestRound { get; init; }
    public List<RfqComparisonSupplierSummaryRow> SupplierSummary { get; init; } = [];
    public List<RfqComparisonQuoteRow> QuoteRows { get; init; } = [];
    public List<RfqComparisonAuditRow> AuditRows { get; init; } = [];
    public List<RfqComparisonPrintRoundGroup> RoundGroups { get; init; } = [];
}

public sealed class RfqComparisonPrintRoundSummary
{
    public long Id { get; init; }
    public int RoundNumber { get; init; }
    public string? Status { get; init; }
    public string? BidDeadline { get; init; }
    public string? OpenedAt { get; init; }
    public string? ClosedAt { get; init; }
    public int InvitedSupplierCount { get; init; }
    public int SubmittedSupplierCount { get; init; }
    public int WithdrawnSupplierCount { get; init; }
}

public sealed class RfqComparisonPrintRoundGroup
{
    public long? RoundId { get; init; }
    public int RoundNumber { get; init; }
    public string? Status { get; init; }
    public string? BidDeadline { get; init; }
    public int InvitedSupplierCount { get; init; }
    public int SubmittedSupplierCount { get; init; }
    public int WithdrawnSupplierCount { get; init; }
    public List<RfqComparisonSupplierSummaryRow> SupplierSummary { get; init; } = [];
    public List<RfqComparisonQuoteRow> QuoteRows { get; init; } = [];
}

public sealed class RfqComparisonSupplierSummaryRow
{
    public string? SupplierName { get; init; }
    public string? SupplierCode { get; init; }
    public string? VendorCode { get; init; }
    public string? QuoteStatus { get; init; }
    public string? QuoteSubmittedAt { get; init; }
}

public sealed class RfqComparisonQuoteRow
{
    public int? RoundNumber { get; init; }
    public int? LineNumber { get; init; }
    public string? MaterialCode { get; init; }
    public string? ItemName { get; init; }
    public string? Specifications { get; init; }
    public string? Unit { get; init; }
    public decimal? Quantity { get; init; }
    public string? SupplierName { get; init; }
    public string? QuoteCurrency { get; init; }
    public decimal? QuotedUnitPrice { get; init; }
    public decimal? QuotedTotalPrice { get; init; }
    public string? QuoteSubmittedAt { get; init; }
    public bool IsSelected { get; init; }
    public string? SelectedSupplierName { get; init; }
    public string? SelectedCurrency { get; init; }
    public decimal? SelectedUnitPrice { get; init; }
}

public sealed class RfqComparisonAuditRow
{
    public int? RoundNumber { get; init; }
    public string? OccurredAt { get; init; }
    public string? ActorName { get; init; }
    public string? EntityType { get; init; }
    public string? Action { get; init; }
    public string? Detail { get; init; }
}
