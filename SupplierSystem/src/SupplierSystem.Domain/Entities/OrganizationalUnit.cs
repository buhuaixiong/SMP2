namespace SupplierSystem.Domain.Entities;

public sealed class OrganizationalUnit
{
    public int Id { get; set; }
    public string TenantId { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Type { get; set; }
    public int? ParentId { get; set; }
    public int Level { get; set; }
    public string? Path { get; set; }
    public string? Description { get; set; }
    public string? AdminIds { get; set; }
    public string? Function { get; set; }
    public string? Category { get; set; }
    public string? Region { get; set; }
    public bool IsActive { get; set; }
    public string? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
