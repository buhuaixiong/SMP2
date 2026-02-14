namespace SupplierSystem.Domain.Entities;

public sealed class ExchangeRateHistory
{
    public int Id { get; set; }
    public string Currency { get; set; } = null!;
    public decimal Rate { get; set; }
    public string EffectiveDate { get; set; } = null!;
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
}
