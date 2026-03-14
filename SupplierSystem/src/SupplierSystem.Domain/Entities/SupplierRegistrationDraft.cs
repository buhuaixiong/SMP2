namespace SupplierSystem.Domain.Entities;

public sealed class SupplierRegistrationDraft
{
    public int Id { get; set; }
    public string DraftToken { get; set; } = null!;
    public string? Status { get; set; }
    public string? RawPayload { get; set; }
    public string? NormalizedPayload { get; set; }
    public string? ValidationErrors { get; set; }
    public string? LastStep { get; set; }
    public string? ContactEmail { get; set; }
    public string? ProcurementEmail { get; set; }
    public string? ExpiresAt { get; set; }
    public int? SubmittedApplicationId { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
