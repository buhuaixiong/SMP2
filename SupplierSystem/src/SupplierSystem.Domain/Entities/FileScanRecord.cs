namespace SupplierSystem.Domain.Entities;

public sealed class FileScanRecord
{
    public int Id { get; set; }
    public string FilePath { get; set; } = null!;
    public string OriginalName { get; set; } = null!;
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public string ScanStatus { get; set; } = null!;
    public string? ScanResult { get; set; }
    public string? ScanEngine { get; set; }
    public int? ScanDuration { get; set; }
    public bool? IsClean { get; set; }
    public string? ThreatName { get; set; }
    public string? ScannedAt { get; set; }
    public int? UploadedBy { get; set; }
    public string? Scenario { get; set; }
    public bool? Quarantined { get; set; }
    public string? QuarantinePath { get; set; }
}
