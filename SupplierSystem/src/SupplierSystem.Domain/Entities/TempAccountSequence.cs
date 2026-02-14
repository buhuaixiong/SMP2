namespace SupplierSystem.Domain.Entities;

public sealed class TempAccountSequence
{
    public string Currency { get; set; } = null!;
    public int LastNumber { get; set; }
    public string? UpdatedAt { get; set; }
}
