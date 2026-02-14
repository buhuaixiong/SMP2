namespace SupplierSystem.Domain.Entities;

public sealed class DocumentTypeDef
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string? Category { get; set; }
    public bool? IsRequired { get; set; }
    public string? Description { get; set; }
}
