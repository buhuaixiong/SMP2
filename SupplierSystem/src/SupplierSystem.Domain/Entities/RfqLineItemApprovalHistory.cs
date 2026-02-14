namespace SupplierSystem.Domain.Entities;

public sealed class RfqLineItemApprovalHistory
{
    public long Id { get; set; }
    public long RfqLineItemId { get; set; }
    public string Step { get; set; } = null!;
    public string ApproverId { get; set; } = null!;
    public string ApproverName { get; set; } = null!;
    public string ApproverRole { get; set; } = null!;
    public string Decision { get; set; } = null!;
    public string? Comments { get; set; }
    public long? PreviousQuoteId { get; set; }
    public long? NewQuoteId { get; set; }
    public string? ChangeReason { get; set; }
    public string? CreatedAt { get; set; }
}
