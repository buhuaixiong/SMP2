namespace SupplierSystem.Domain.Entities;

public sealed class ContractReminderSetting
{
    public int Id { get; set; }
    public string Scope { get; set; } = null!;
    public string Settings { get; set; } = null!;
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
