namespace SupplierSystem.Domain.Entities;

public sealed class PurchasingGroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string BuyerId { get; set; } = null!;
    public string? Role { get; set; }
    public string? JoinedAt { get; set; }
    public string? AssignedBy { get; set; }
    public string? Notes { get; set; }
}
