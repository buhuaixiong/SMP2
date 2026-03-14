using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Infrastructure.Services;

public sealed class AuditArchiveService : IAuditArchiveService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly ILogger<AuditArchiveService> _logger;
    private readonly string _archiveRoot;

    public AuditArchiveService(
        SupplierSystemDbContext dbContext,
        IConfiguration configuration,
        ILogger<AuditArchiveService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        // Get archive path from configuration, default to app/apps/api/audit-archive
        var basePath = configuration["AuditArchive:Path"]
            ?? Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "app", "apps", "api", "audit-archive"));
        _archiveRoot = basePath;
    }

    public async Task<string?> GetPreviousHashAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.HashChainValue != null)
            .OrderByDescending(a => a.Id)
            .Select(a => a.HashChainValue)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public string GenerateHashChainValue(AuditLog log, string? previousHash)
    {
        var payload = new Dictionary<string, object?>
        {
            ["previousHash"] = string.IsNullOrWhiteSpace(previousHash) ? "genesis" : previousHash,
            ["timestamp"] = log.CreatedAt.ToString("o"),
            ["actorId"] = log.ActorId ?? "system",
            ["actorName"] = log.ActorName ?? "system",
            ["action"] = log.Action ?? string.Empty,
            ["entityType"] = log.EntityType ?? string.Empty,
            ["entityId"] = log.EntityId ?? string.Empty,
            ["changes"] = log.Changes,
            ["ipAddress"] = log.IpAddress ?? "unknown",
        };

        var json = JsonSerializer.Serialize(payload);
        return CalculateHash(json);
    }

    public async Task ArchiveAsync(AuditLog log, CancellationToken cancellationToken)
    {
        try
        {
            EnsureArchiveDirectory();

            var timestamp = log.CreatedAt == default ? DateTime.UtcNow : log.CreatedAt;
            var datePath = GetArchiveDatePath(timestamp);
            Directory.CreateDirectory(datePath);

            var fileStamp = timestamp.ToString("yyyy-MM-ddTHH-mm-ss-fff");
            var fileName = $"sensitive-{fileStamp}-{log.Id}.json";
            var filePath = Path.Combine(datePath, fileName);

            var archiveData = new Dictionary<string, object?>
            {
                ["auditLogId"] = log.Id,
                ["archivedAt"] = DateTime.UtcNow.ToString("o"),
                ["logEntry"] = new Dictionary<string, object?>
                {
                    ["id"] = log.Id,
                    ["actorId"] = log.ActorId,
                    ["actorName"] = log.ActorName,
                    ["entityType"] = log.EntityType,
                    ["entityId"] = log.EntityId,
                    ["action"] = log.Action,
                    ["changes"] = ParseJson(log.Changes),
                    ["summary"] = log.Summary,
                    ["ipAddress"] = log.IpAddress,
                    ["isSensitive"] = log.IsSensitive,
                    ["immutable"] = log.Immutable,
                    ["hashChainValue"] = log.HashChainValue,
                    ["createdAt"] = log.CreatedAt.ToString("o"),
                },
            };

            var archiveJson = JsonSerializer.Serialize(archiveData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, archiveJson, Encoding.UTF8, cancellationToken);
            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);

            var fileHash = CalculateHash(archiveJson);
            var signaturePath = $"{filePath}.sig";
            var signatureData = new Dictionary<string, object?>
            {
                ["auditLogId"] = log.Id,
                ["filePath"] = fileName,
                ["hash"] = fileHash,
                ["algorithm"] = "SHA-256",
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
            };
            var signatureJson = JsonSerializer.Serialize(signatureData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(signaturePath, signatureJson, Encoding.UTF8, cancellationToken);
            File.SetAttributes(signaturePath, File.GetAttributes(signaturePath) | FileAttributes.ReadOnly);

            var metadata = await _dbContext.AuditArchiveMetadata
                .FirstOrDefaultAsync(m => m.AuditLogId == log.Id, cancellationToken);

            if (metadata == null)
            {
                metadata = new AuditArchiveMetadata
                {
                    AuditLogId = log.Id,
                    ArchiveFilePath = filePath,
                    FileHash = fileHash,
                    ArchiveDate = DateTime.UtcNow.ToString("o"),
                    VerificationStatus = "archived",
                };
                _dbContext.AuditArchiveMetadata.Add(metadata);
            }
            else
            {
                metadata.ArchiveFilePath = filePath;
                metadata.FileHash = fileHash;
                metadata.ArchiveDate = DateTime.UtcNow.ToString("o");
                metadata.VerificationStatus = "archived";
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuditArchive] Failed to archive log {AuditLogId}", log.Id);
        }
    }

    public async Task<AuditArchiveStats> GetStatsAsync(CancellationToken cancellationToken)
    {
        var totalArchived = await _dbContext.AuditArchiveMetadata.CountAsync(cancellationToken);
        var totalVerified = await _dbContext.AuditArchiveMetadata
            .CountAsync(m => m.VerificationStatus == "verified", cancellationToken);
        var totalFailed = await _dbContext.AuditArchiveMetadata
            .CountAsync(m => m.VerificationStatus == "failed", cancellationToken);

        var oldest = await _dbContext.AuditArchiveMetadata
            .OrderBy(m => m.ArchiveDate)
            .Select(m => m.ArchiveDate)
            .FirstOrDefaultAsync(cancellationToken);

        var newest = await _dbContext.AuditArchiveMetadata
            .OrderByDescending(m => m.ArchiveDate)
            .Select(m => m.ArchiveDate)
            .FirstOrDefaultAsync(cancellationToken);

        return new AuditArchiveStats
        {
            TotalArchived = totalArchived,
            TotalVerified = totalVerified,
            TotalFailed = totalFailed,
            OldestArchive = oldest,
            NewestArchive = newest,
        };
    }

    public async Task<AuditArchiveVerificationResult> VerifyArchivedLogAsync(
        int auditLogId,
        CancellationToken cancellationToken)
    {
        var metadata = await _dbContext.AuditArchiveMetadata
            .FirstOrDefaultAsync(m => m.AuditLogId == auditLogId, cancellationToken);

        if (metadata == null)
        {
            return new AuditArchiveVerificationResult
            {
                Valid = false,
                Message = "Archive metadata not found.",
            };
        }

        if (!File.Exists(metadata.ArchiveFilePath))
        {
            return new AuditArchiveVerificationResult
            {
                Valid = false,
                Message = "Archive file not found.",
                Details = new Dictionary<string, object?> { ["expectedPath"] = metadata.ArchiveFilePath }
            };
        }

        var content = await File.ReadAllTextAsync(metadata.ArchiveFilePath, cancellationToken);
        var currentHash = CalculateHash(content);

        if (!string.Equals(currentHash, metadata.FileHash, StringComparison.OrdinalIgnoreCase))
        {
            metadata.VerificationStatus = "failed";
            metadata.VerifiedAt = DateTime.UtcNow.ToString("o");
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new AuditArchiveVerificationResult
            {
                Valid = false,
                Message = "Archive hash mismatch.",
                Details = new Dictionary<string, object?>
                {
                    ["expectedHash"] = metadata.FileHash,
                    ["actualHash"] = currentHash,
                }
            };
        }

        metadata.VerificationStatus = "verified";
        metadata.VerifiedAt = DateTime.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuditArchiveVerificationResult
        {
            Valid = true,
            Message = "Archive integrity verified.",
            Details = new Dictionary<string, object?>
            {
                ["filePath"] = metadata.ArchiveFilePath,
                ["hash"] = currentHash,
                ["verifiedAt"] = metadata.VerifiedAt,
            }
        };
    }

    public async Task<AuditHashChainVerificationResult> VerifyHashChainAsync(
        int? startId,
        int? endId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.AuditLogs.AsNoTracking()
            .Where(a => a.HashChainValue != null);

        if (startId.HasValue)
        {
            query = query.Where(a => a.Id >= startId.Value);
        }

        if (endId.HasValue)
        {
            query = query.Where(a => a.Id <= endId.Value);
        }

        var logs = await query.OrderBy(a => a.Id).ToListAsync(cancellationToken);
        if (logs.Count == 0)
        {
            return new AuditHashChainVerificationResult
            {
                Valid = true,
                Message = "No hash chain entries found.",
                VerifiedCount = 0,
            };
        }

        var broken = new List<AuditHashChainBreak>();
        string? previousHash = null;

        foreach (var log in logs)
        {
            var expected = GenerateHashChainValue(log, previousHash);
            if (!string.Equals(expected, log.HashChainValue, StringComparison.OrdinalIgnoreCase))
            {
                broken.Add(new AuditHashChainBreak
                {
                    LogId = log.Id,
                    ExpectedHash = expected,
                    ActualHash = log.HashChainValue,
                    CreatedAt = log.CreatedAt.ToString("o"),
                });
            }

            previousHash = log.HashChainValue;
        }

        if (broken.Count > 0)
        {
            return new AuditHashChainVerificationResult
            {
                Valid = false,
                Message = $"Detected {broken.Count} hash chain breaks.",
                BrokenChains = broken,
                VerifiedCount = logs.Count,
            };
        }

        return new AuditHashChainVerificationResult
        {
            Valid = true,
            Message = $"Hash chain verified for {logs.Count} entries.",
            VerifiedCount = logs.Count,
        };
    }

    public async Task<AuditArchiveMetadata?> GetMetadataAsync(int auditLogId, CancellationToken cancellationToken)
    {
        return await _dbContext.AuditArchiveMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.AuditLogId == auditLogId, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditArchiveListItem>> ListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        var offset = (Math.Max(1, page) - 1) * Math.Max(1, limit);
        var items = await (from meta in _dbContext.AuditArchiveMetadata.AsNoTracking()
                           join log in _dbContext.AuditLogs.AsNoTracking()
                               on meta.AuditLogId equals log.Id
                           orderby meta.ArchiveDate descending
                           select new AuditArchiveListItem
                           {
                               Metadata = meta,
                               Log = log,
                           })
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items;
    }

    private void EnsureArchiveDirectory()
    {
        if (!Directory.Exists(_archiveRoot))
        {
            Directory.CreateDirectory(_archiveRoot);
        }
    }

    private string GetArchiveDatePath(DateTime date)
    {
        return Path.Combine(_archiveRoot, date.ToString("yyyy-MM-dd"));
    }

    private static string CalculateHash(string data)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static object? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return json;
        }
    }
}
