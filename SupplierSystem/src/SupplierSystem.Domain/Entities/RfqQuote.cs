namespace SupplierSystem.Domain.Entities;

public sealed class RfqQuote
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public int? SupplierId { get; set; }
    public int? Version { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? LeadTime { get; set; }
    public string? Status { get; set; }
    public string? SubmittedAt { get; set; }
    public string? LockedAt { get; set; }
    public string? Notes { get; set; }
    public string? ChangeLog { get; set; }
}
