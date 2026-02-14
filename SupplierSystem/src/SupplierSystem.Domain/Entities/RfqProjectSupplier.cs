namespace SupplierSystem.Domain.Entities;

public sealed class RfqProjectSupplier
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public int? SupplierId { get; set; }
    public string? InvitedAt { get; set; }
    public string? RespondedAt { get; set; }
}
