using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class ArchiveService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly ILogger<ArchiveService> _logger;

    public ArchiveService(SupplierSystemDbContext dbContext, ILogger<ArchiveService> logger)
        => (_dbContext, _logger) = (dbContext, logger);

    public ArchiveStats GetArchiveStats()
    {
        var config = BackupConfig.Load();
        return new ArchiveStats
        {
            Rfq = CountArchives(config.Paths.RfqArchive),
            Supplier = CountArchives(config.Paths.SupplierArchive),
            Audit = CountArchives(config.Paths.AuditArchive),
            UploadsArchive = CountArchives(config.Paths.UploadsArchive),
        };
    }

    public IReadOnlyList<Dictionary<string, object?>> QueryArchive(string category, int year, string sql, IReadOnlyList<object?> parameters)
    {
        var archivePath = GetArchivePath(category, year);
        if (!File.Exists(archivePath)) return Array.Empty<Dictionary<string, object?>>();

        using var connection = new SqliteConnection($"Data Source={archivePath};Mode=ReadOnly;");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < parameters.Count; i++)
        {
            var param = command.CreateParameter();
            param.ParameterName = $"@p{i}";
            param.Value = parameters[i] ?? DBNull.Value;
            command.Parameters.Add(param);
        }

        using var reader = command.ExecuteReader();
        var results = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            results.Add(row);
        }
        return results;
    }

    public long QueryArchiveScalar(string category, int year, string sql, IReadOnlyList<object?> parameters)
    {
        var archivePath = GetArchivePath(category, year);
        if (!File.Exists(archivePath)) return 0;

        using var connection = new SqliteConnection($"Data Source={archivePath};Mode=ReadOnly;");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < parameters.Count; i++)
        {
            var param = command.CreateParameter();
            param.ParameterName = $"@p{i}";
            param.Value = parameters[i] ?? DBNull.Value;
            command.Parameters.Add(param);
        }

        var result = command.ExecuteScalar();
        return result switch
        {
            null or DBNull => 0,
            long l => l,
            int i => i,
            decimal d => (long)d,
            _ => long.TryParse(result.ToString(), out var parsed) ? parsed : 0
        };
    }

    private static ArchiveDirStats CountArchives(string dir)
    {
        if (!Directory.Exists(dir)) return new ArchiveDirStats();

        var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(p => p.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0) return new ArchiveDirStats();

        var years = files.Select(f =>
        {
            var match = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(f), "(\\d{4})");
            return match.Success && int.TryParse(match.Groups[1].Value, out var y) ? y : -1;
        }).Where(y => y > 0).Distinct().OrderByDescending(y => y).ToList();

        return new ArchiveDirStats
        {
            Count = files.Count,
            TotalSize = files.Sum(f => new FileInfo(f).Length),
            Years = years,
        };
    }

    private static string GetArchivePath(string category, int year)
    {
        var config = BackupConfig.Load();
        var folder = category.ToLowerInvariant() switch
        {
            "rfq" => config.Paths.RfqArchive,
            "supplier" => config.Paths.SupplierArchive,
            "audit" => config.Paths.AuditArchive,
            _ => throw new ArgumentException("Unknown archive category.", nameof(category))
        };
        return Path.Combine(folder, $"{category}-archive-{year}.db");
    }

    public async Task<ArchiveSeparationResult> PerformArchiveSeparationAsync(CancellationToken cancellationToken)
    {
        var config = BackupConfig.Load();
        var cutoffYear = DateTime.UtcNow.Year - config.Retention.SupplierArchiveYears;
        var cutoffDate = new DateTime(cutoffYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var result = new ArchiveSeparationResult();

        var documents = await _dbContext.SupplierDocuments.AsNoTracking()
            .Where(d => !string.IsNullOrWhiteSpace(d.UploadedAt) && !string.IsNullOrWhiteSpace(d.StoredName))
            .ToListAsync(cancellationToken);

        foreach (var doc in documents)
        {
            if (!DateTimeOffset.TryParse(doc.UploadedAt, out var uploadedAt) || uploadedAt > cutoffDate)
                continue;

            var sourcePath = Path.Combine(config.Paths.UploadsDir, doc.StoredName!);
            var archiveDir = Path.Combine(config.Paths.UploadsArchive, uploadedAt.Year.ToString());
            var archivePath = Path.Combine(archiveDir, doc.StoredName!);

            try
            {
                if (!File.Exists(sourcePath))
                {
                    result.Skipped++;
                    continue;
                }
                Directory.CreateDirectory(archiveDir);
                File.Move(sourcePath, archivePath, true);
                result.Moved++;
            }
            catch (Exception ex)
            {
                result.Errors++;
                _logger.LogWarning(ex, "[Archive] Failed to move file {File}.", sourcePath);
            }
        }

        result.CutoffDate = cutoffDate.ToString("o");
        return result;
    }
}

#pragma warning disable SA1402
public sealed class ArchiveStats
{
    public ArchiveDirStats Rfq { get; init; } = new();
    public ArchiveDirStats Supplier { get; init; } = new();
    public ArchiveDirStats Audit { get; init; } = new();
    public ArchiveDirStats UploadsArchive { get; init; } = new();
}

public sealed class ArchiveDirStats
{
    public int Count { get; init; }
    public long TotalSize { get; init; }
    public List<int> Years { get; init; } = new();
}

public sealed class ArchiveSeparationResult
{
    public int Moved { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public string? CutoffDate { get; set; }
}
#pragma warning restore SA1402
