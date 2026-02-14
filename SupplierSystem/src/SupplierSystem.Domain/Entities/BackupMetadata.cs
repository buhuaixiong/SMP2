namespace SupplierSystem.Domain.Entities;

public sealed class BackupMetadata
{
    public int Id { get; set; }
    public string BackupType { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public long FileSize { get; set; }
    public string Checksum { get; set; } = null!;
    public string CreatedAt { get; set; } = null!;
    public string? VerifiedAt { get; set; }
    public string? VerificationStatus { get; set; }
    public string? VerificationDetails { get; set; }
    public string? Notes { get; set; }
}
