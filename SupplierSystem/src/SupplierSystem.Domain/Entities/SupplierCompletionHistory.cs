namespace SupplierSystem.Domain.Entities;

public sealed class SupplierCompletionHistory
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public decimal ProfileCompletion { get; set; }
    public decimal DocumentCompletion { get; set; }
    public decimal CompletionScore { get; set; }
    public string CompletionStatus { get; set; } = null!;
    public string? MissingProfileFields { get; set; }
    public string? MissingDocumentTypes { get; set; }
    public string? TriggeredBy { get; set; }
    public string? TriggerReason { get; set; }
    public string? RecordedAt { get; set; }
}
