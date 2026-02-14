namespace SupplierSystem.Domain.Entities;

public sealed class OrganizationalUnitMember
{
    public int Id { get; set; }
    public string TenantId { get; set; } = null!;
    public int UnitId { get; set; }
    public string UserId { get; set; } = null!;
    public string? Role { get; set; }
    public string? JoinedAt { get; set; }
    public string? AssignedBy { get; set; }
    public string? Notes { get; set; }
}
