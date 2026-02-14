namespace SupplierSystem.Domain.Entities;

public sealed class TagDef
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? CreatedAt { get; set; }
}
