namespace SupplierSystem.Domain.Entities;

public sealed class SupplierRfqInvitation
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public int? SupplierId { get; set; }
    public string? InvitedAt { get; set; }
    public string? RespondedAt { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? InvitationToken { get; set; }
    public string? TokenExpiresAt { get; set; }
    public string? RecipientEmail { get; set; }
    public bool IsExternal { get; set; }
    public string? UpdatedAt { get; set; }
}
