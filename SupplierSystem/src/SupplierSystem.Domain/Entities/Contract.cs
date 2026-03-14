namespace SupplierSystem.Domain.Entities;

public sealed class Contract
{
    public int Id { get; set; }
    public int? SupplierId { get; set; }
    public string? Title { get; set; }
    public string? AgreementNumber { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public string? PaymentCycle { get; set; }
    public string? EffectiveFrom { get; set; }
    public string? EffectiveTo { get; set; }
    public bool AutoRenew { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsMandatory { get; set; }
}
