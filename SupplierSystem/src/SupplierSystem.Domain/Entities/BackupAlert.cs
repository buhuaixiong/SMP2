namespace SupplierSystem.Domain.Entities;

public sealed class BackupAlert
{
    public int Id { get; set; }
    public string AlertLevel { get; set; } = null!;
    public string AlertType { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Details { get; set; }
    public bool EmailSent { get; set; }
    public bool WindowsLogWritten { get; set; }
    public bool AuditLogged { get; set; }
    public string CreatedAt { get; set; } = null!;
    public string? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}
