namespace SupplierSystem.Domain.Entities;

public sealed class SupplierUpgradeReview
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string StepKey { get; set; } = null!;
    public string StepName { get; set; } = null!;
    public string Decision { get; set; } = null!;
    public string? Comments { get; set; }
    public string? DecidedById { get; set; }
    public string? DecidedByName { get; set; }
    public string? DecidedAt { get; set; }
}
