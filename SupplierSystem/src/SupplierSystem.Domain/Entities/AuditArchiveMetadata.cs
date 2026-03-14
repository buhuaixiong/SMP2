namespace SupplierSystem.Domain.Entities;

public sealed class AuditArchiveMetadata
{
    public int Id { get; set; }
    public int AuditLogId { get; set; }
    public string ArchiveFilePath { get; set; } = null!;
    public string FileHash { get; set; } = null!;
    public string? ArchiveDate { get; set; }
    public string? VerifiedAt { get; set; }
    public string? VerificationStatus { get; set; }
}
