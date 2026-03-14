namespace SupplierSystem.Application.Models.Audit;

public sealed class AuditEntry
{
    public string? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public string? Changes { get; set; }
    public string? Summary { get; set; }
    public string? IpAddress { get; set; }
    public bool IsSensitive { get; set; }
    public bool Immutable { get; set; }
    public string? HashChainValue { get; set; }
}
