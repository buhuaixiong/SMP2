namespace SupplierSystem.Domain.Entities;

public sealed class ExternalSupplierInvitation
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public string Email { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string InvitationToken { get; set; } = null!;
    public bool? RegistrationCompleted { get; set; }
    public int? RegisteredSupplierId { get; set; }
    public string? InvitedAt { get; set; }
    public string? RegisteredAt { get; set; }
    public string? ExpiresAt { get; set; }
    public string? Status { get; set; }
}
