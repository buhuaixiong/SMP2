using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Application.Interfaces;

public interface IAuditArchiveService
{
    Task<string?> GetPreviousHashAsync(CancellationToken cancellationToken);
    string GenerateHashChainValue(AuditLog log, string? previousHash);
    Task ArchiveAsync(AuditLog log, CancellationToken cancellationToken);
    Task<AuditArchiveStats> GetStatsAsync(CancellationToken cancellationToken);
    Task<AuditArchiveVerificationResult> VerifyArchivedLogAsync(int auditLogId, CancellationToken cancellationToken);
    Task<AuditHashChainVerificationResult> VerifyHashChainAsync(int? startId, int? endId, CancellationToken cancellationToken);
    Task<AuditArchiveMetadata?> GetMetadataAsync(int auditLogId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditArchiveListItem>> ListAsync(int page, int limit, CancellationToken cancellationToken);
}
