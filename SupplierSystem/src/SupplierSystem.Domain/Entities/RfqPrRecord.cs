namespace SupplierSystem.Domain.Entities;

public sealed class RfqPrRecord
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public string PrNumber { get; set; } = null!;
    public string? PrDate { get; set; }
    public string FilledBy { get; set; } = null!;
    public string? FilledAt { get; set; }
    public string? DepartmentConfirmerId { get; set; }
    public string? DepartmentConfirmerName { get; set; }
    public string? ConfirmationStatus { get; set; }
    public string? ConfirmationNotes { get; set; }
    public string? ConfirmedAt { get; set; }
}
