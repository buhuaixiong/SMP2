namespace SupplierSystem.Domain.Entities;

public sealed class InvoiceRestoreRequest
{
    public int Id { get; set; }
    public int? InvoiceId { get; set; }
    public int? SupplierId { get; set; }
    public string? Status { get; set; }
    public string? RequestedBy { get; set; }
    public string? RequestedAt { get; set; }
    public string? DecidedBy { get; set; }
    public string? DecidedAt { get; set; }
    public string? Reason { get; set; }
    public string? DecisionNotes { get; set; }
}
