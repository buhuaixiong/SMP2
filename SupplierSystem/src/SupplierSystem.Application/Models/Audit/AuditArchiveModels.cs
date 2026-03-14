using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Application.Models.Audit;

public sealed class AuditArchiveStats
{
    public int TotalArchived { get; set; }
    public int TotalVerified { get; set; }
    public int TotalFailed { get; set; }
    public string? OldestArchive { get; set; }
    public string? NewestArchive { get; set; }
}

public sealed class AuditArchiveVerificationResult
{
    public bool Valid { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object?>? Details { get; set; }
}

public sealed class AuditHashChainVerificationResult
{
    public bool Valid { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<AuditHashChainBreak>? BrokenChains { get; set; }
    public int VerifiedCount { get; set; }
}

public sealed class AuditHashChainBreak
{
    public int LogId { get; set; }
    public string? ExpectedHash { get; set; }
    public string? ActualHash { get; set; }
    public string? CreatedAt { get; set; }
}

public sealed class AuditArchiveListItem
{
    public AuditArchiveMetadata Metadata { get; set; } = null!;
    public AuditLog Log { get; set; } = null!;
}
