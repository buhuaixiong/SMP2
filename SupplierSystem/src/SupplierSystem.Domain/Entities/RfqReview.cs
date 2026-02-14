namespace SupplierSystem.Domain.Entities;

public sealed class RfqReview
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public int? SelectedQuoteId { get; set; }
    public string? ReviewScores { get; set; }
    public string? Comments { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewedAt { get; set; }
}
