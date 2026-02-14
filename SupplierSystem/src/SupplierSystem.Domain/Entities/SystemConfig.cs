namespace SupplierSystem.Domain.Entities;

public sealed class SystemConfig
{
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string? Metadata { get; set; }
}
