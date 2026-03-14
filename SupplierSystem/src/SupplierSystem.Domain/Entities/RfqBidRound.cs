namespace SupplierSystem.Domain.Entities;

public sealed class RfqBidRound
{
    public long Id { get; set; }
    public long RfqId { get; set; }
    public int RoundNumber { get; set; }
    public string? BidDeadline { get; set; }
    public string? Status { get; set; }
    public string? OpenedAt { get; set; }
    public string? ClosedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? ExtensionReason { get; set; }
    public long? StartedFromRoundId { get; set; }
}
