namespace SupplierSystem.Domain.Entities;

public sealed class RfqExternalInvitation
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public string Email { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Status { get; set; }
    public string? InvitedAt { get; set; }
    public string? RespondedAt { get; set; }
    public string? Notes { get; set; }
    public string? Token { get; set; }
    public string? TokenExpiresAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedAt { get; set; }
}
