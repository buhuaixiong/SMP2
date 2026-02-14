namespace SupplierSystem.Domain.Entities;

public sealed class SupplierBaseline
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public int Version { get; set; }
    public string Payload { get; set; } = null!;
    public string? Checksum { get; set; }
    public string? CreatedAt { get; set; }
}
