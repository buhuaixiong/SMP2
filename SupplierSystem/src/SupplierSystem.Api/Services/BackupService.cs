using System.Security.Cryptography;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class BackupService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly ILogger<BackupService> _logger;

    public BackupService(SupplierSystemDbContext dbContext, ILogger<BackupService> logger)
        => (_dbContext, _logger) = (dbContext, logger);

    /// <summary>
    /// Execute a scheduled backup. Full backup runs on the first day of the month,
    /// otherwise differential backups run when enabled.
    /// </summary>
    public async Task<BackupResult> PerformBackupAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var config = BackupConfig.Load();

        if (now.Day == 1)
        {
            return await PerformFullBackupAsync(cancellationToken);
        }

        if (config.Differential.Enabled)
        {
            return await PerformDifferentialBackupAsync(cancellationToken);
        }

        return await PerformFullBackupAsync(cancellationToken);
    }

    /// <summary>
    /// 执行完整备份
    /// </summary>
    public async Task<BackupResult> PerformFullBackupAsync(CancellationToken cancellationToken)
    {
        var config = BackupConfig.Load();
        var backupPath = GenerateFullBackupPath(DateTime.UtcNow, config.Paths);
        var start = DateTime.UtcNow;
        var freeSpaceGb = GetFreeSpace(config.Paths.BackupRoot) / BytesPerGB;

        if (freeSpaceGb < config.Alerts.SpaceCriticalGB)
            throw new InvalidOperationException($"Insufficient disk space ({freeSpaceGb:0.00} GB).");

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? config.Paths.BackupRoot);
        await BackupDatabaseAsync(backupPath, true, cancellationToken);

        var stats = new FileInfo(backupPath);
        var checksum = await GenerateSha256Async(backupPath, cancellationToken);

        var verification = config.Verification.Enabled
            ? await VerifyBackupAsync(backupPath, config.Verification.MinSizeBytes, cancellationToken)
            : new BackupVerificationResult { Passed = true };

        await RecordBackupMetadataAsync(new BackupMetadata
        {
            BackupType = "full",
            FilePath = backupPath,
            FileName = Path.GetFileName(backupPath),
            FileSize = stats.Length,
            Checksum = checksum,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            VerifiedAt = verification.Passed ? DateTimeOffset.UtcNow.ToString("o") : null,
            VerificationStatus = verification.Passed ? "passed" : "failed",
            VerificationDetails = verification.Details is null ? null : System.Text.Json.JsonSerializer.Serialize(verification.Details),
        }, cancellationToken);

        return new BackupResult
        {
            Success = true,
            Path = backupPath,
            Size = stats.Length,
            Checksum = checksum,
            Verification = verification,
            DurationMs = (long)(DateTime.UtcNow - start).TotalMilliseconds,
            FreeSpaceGB = freeSpaceGb,
            BackupType = "full",
        };
    }

    /// <summary>
    /// 执行差异备份（自上次完整备份以来的变更）
    /// </summary>
    public async Task<BackupResult> PerformDifferentialBackupAsync(CancellationToken cancellationToken)
    {
        var config = BackupConfig.Load();
        var fullBackupPath = FindLatestFullBackup(config.Paths.Daily);
        if (fullBackupPath == null || !File.Exists(fullBackupPath))
        {
            _logger.LogWarning("[Backup] No full backup found, performing full backup instead.");
            return await PerformFullBackupAsync(cancellationToken);
        }

        var backupPath = GenerateDifferentialBackupPath(DateTime.UtcNow, config.Paths);
        var start = DateTime.UtcNow;
        var freeSpaceGb = GetFreeSpace(config.Paths.BackupRoot) / BytesPerGB;

        if (freeSpaceGb < config.Alerts.SpaceCriticalGB)
            throw new InvalidOperationException($"Insufficient disk space ({freeSpaceGb:0.00} GB).");

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? config.Paths.BackupRoot);
        await BackupDatabaseAsync(backupPath, false, cancellationToken);

        var stats = new FileInfo(backupPath);
        var checksum = await GenerateSha256Async(backupPath, cancellationToken);

        var verification = config.Verification.Enabled
            ? await VerifyBackupAsync(backupPath, config.Verification.MinSizeBytes, cancellationToken)
            : new BackupVerificationResult { Passed = true };

        await RecordBackupMetadataAsync(new BackupMetadata
        {
            BackupType = "differential",
            FilePath = backupPath,
            FileName = Path.GetFileName(backupPath),
            FileSize = stats.Length,
            Checksum = checksum,
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            VerifiedAt = verification.Passed ? DateTimeOffset.UtcNow.ToString("o") : null,
            VerificationStatus = verification.Passed ? "passed" : "failed",
            VerificationDetails = verification.Details is null ? null : System.Text.Json.JsonSerializer.Serialize(verification.Details),
            Notes = $"Based on: {fullBackupPath}",
        }, cancellationToken);

        _logger.LogInformation("[Backup] Differential backup completed: {Path}", backupPath);

        return new BackupResult
        {
            Success = true,
            Path = backupPath,
            Size = stats.Length,
            Checksum = checksum,
            Verification = verification,
            DurationMs = (long)(DateTime.UtcNow - start).TotalMilliseconds,
            FreeSpaceGB = freeSpaceGb,
            BackupType = "differential",
            BasedOnFullBackup = fullBackupPath,
        };
    }

    /// <summary>
    /// 查找最新的完整备份文件
    /// </summary>
    private static string? FindLatestFullBackup(string dailyPath)
    {
        if (!Directory.Exists(dailyPath)) return null;

        var files = Directory.GetFiles(dailyPath, "*-full-*.bak", SearchOption.TopDirectoryOnly);
        if (files.Length == 0) return null;

        return files.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    private static string GenerateFullBackupPath(DateTime date, BackupPaths paths)
        => Path.Combine(paths.Daily, $"supplier-{date:yyyy-MM-dd}-full.bak");

    private static string GenerateDifferentialBackupPath(DateTime date, BackupPaths paths)
    {
        var timeSuffix = date.ToString("HHmmss");
        return Path.Combine(paths.Differential, $"supplier-{date:yyyy-MM-dd}-{timeSuffix}-diff.bak");
    }

    public async Task<BackupVerificationResult> VerifyBackupAsync(string backupPath, int minSizeBytes, CancellationToken cancellationToken)
    {
        var result = new BackupVerificationResult { Passed = false };
        if (!File.Exists(backupPath))
        {
            result.Details = new Dictionary<string, object?> { ["fileExists"] = false, ["integrityCheck"] = "File does not exist." };
            return result;
        }

        var info = new FileInfo(backupPath);
        result.Details = new Dictionary<string, object?> { ["fileExists"] = true, ["fileSize"] = info.Length };

        if (info.Length < minSizeBytes)
        {
            result.Details["integrityCheck"] = $"File size below minimum ({minSizeBytes}).";
            return result;
        }

        var verifyOk = await VerifyBackupFileAsync(backupPath, cancellationToken);
        result.Details["integrityCheck"] = verifyOk ? "ok" : "verify_failed";
        result.Passed = verifyOk;
        return result;
    }

    public BackupRotationResult RotateBackups()
    {
        var config = BackupConfig.Load();
        var today = DateTime.UtcNow;
        var fullPath = GenerateFullBackupPath(today, config.Paths);

        if (!File.Exists(fullPath))
            return new BackupRotationResult();

        var result = new BackupRotationResult();

        if (today.DayOfWeek == DayOfWeek.Sunday)
        {
            var weeklyPath = Path.Combine(config.Paths.Weekly, $"supplier-{today:yyyy}-W{today.GetWeekOfYear():D2}-full.bak");
            Directory.CreateDirectory(Path.GetDirectoryName(weeklyPath)!);
            File.Copy(fullPath, weeklyPath, true);
            result.Weekly = true;
        }

        if (today.Day == 1)
        {
            var monthlyPath = Path.Combine(config.Paths.Monthly, $"supplier-{today:yyyy}-{today.Month:D2}-full.bak");
            Directory.CreateDirectory(Path.GetDirectoryName(monthlyPath)!);
            File.Copy(fullPath, monthlyPath, true);
            result.Monthly = true;
        }

        if (today.Month == 1 && today.Day == 1)
        {
            var yearlyPath = Path.Combine(config.Paths.Yearly, $"supplier-{today:yyyy}-full.bak");
            Directory.CreateDirectory(Path.GetDirectoryName(yearlyPath)!);
            File.Copy(fullPath, yearlyPath, true);
            result.Yearly = true;
        }

        return result;
    }

    public BackupCleanupResult CleanupExpiredBackups()
    {
        var config = BackupConfig.Load();
        return new BackupCleanupResult
        {
            Full = CleanupDir(config.Paths.Daily, TimeSpan.FromDays(config.Retention.DailyDays), "*-full-*.bak"),
            Differential = CleanupDir(config.Paths.Differential, TimeSpan.FromDays(config.Retention.DifferentialDays), "*-diff-*.bak"),
            Weekly = CleanupDir(config.Paths.Weekly, TimeSpan.FromDays(config.Retention.WeeklyWeeks * 7), "*.bak"),
            Monthly = CleanupDir(config.Paths.Monthly, TimeSpan.FromDays(config.Retention.MonthlyMonths * 30), "*.bak"),
            Yearly = CleanupDir(config.Paths.Yearly, TimeSpan.FromDays(config.Retention.YearlyYears * 365), "*.bak"),
        };
    }

    public BackupStats GetBackupStats()
    {
        var config = BackupConfig.Load();
        var freeSpaceBytes = GetFreeSpace(config.Paths.BackupRoot);
        var freeSpaceGb = freeSpaceBytes / BytesPerGB;

        var latestFullBackup = FindLatestFullBackup(config.Paths.Daily);
        var latestDiffBackup = FindLatestDifferentialBackup(config.Paths.Differential);
        var lastBackupTime = GetLatestBackupTime(config);

        return new BackupStats
        {
            Full = CountFiles(config.Paths.Daily, "*-full-*.bak"),
            Differential = CountFiles(config.Paths.Differential, "*-diff-*.bak"),
            Weekly = CountFiles(config.Paths.Weekly, "*.bak"),
            Monthly = CountFiles(config.Paths.Monthly, "*.bak"),
            Yearly = CountFiles(config.Paths.Yearly, "*.bak"),
            FreeSpaceBytes = freeSpaceBytes,
            FreeSpaceGB = freeSpaceGb.ToString("0.00"),
            Config = new BackupStatsConfig
            {
                Retention = config.Retention,
                Schedule = config.Schedule,
                Differential = config.Differential,
            },
            LatestFullBackup = latestFullBackup,
            LatestDifferentialBackup = latestDiffBackup,
            LastBackupTime = lastBackupTime,
            BackupMode = GetBackupMode(config),
        };
    }

    private static string? FindLatestDifferentialBackup(string diffPath)
    {
        if (!Directory.Exists(diffPath)) return null;

        var files = Directory.GetFiles(diffPath, "*-diff-*.bak", SearchOption.TopDirectoryOnly);
        if (files.Length == 0) return null;

        return files.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    private static DateTime? GetLatestBackupTime(BackupConfig config)
    {
        var latestFull = FindLatestFullBackup(config.Paths.Daily);
        var latestDiff = FindLatestDifferentialBackup(config.Paths.Differential);

        var fullTime = latestFull != null ? File.GetLastWriteTimeUtc(latestFull) : DateTime.MinValue;
        var diffTime = latestDiff != null ? File.GetLastWriteTimeUtc(latestDiff) : DateTime.MinValue;

        return fullTime > diffTime ? fullTime : diffTime;
    }

    private static string GetBackupMode(BackupConfig config)
    {
        if (!config.Differential.Enabled) return "full-only";
        if (config.Differential.IntervalHours >= 24) return "full-daily";
        return "full-with-differential";
    }

    public async Task<IReadOnlyList<BackupMetadata>> GetBackupHistoryAsync(int limit, CancellationToken cancellationToken)
    {
        await EnsureMetadataTableAsync(cancellationToken);
        return await _dbContext.BackupMetadata
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<BackupResult> TriggerManualBackupAsync(bool forceFull, CancellationToken cancellationToken)
    {
        var result = forceFull
            ? await PerformFullBackupAsync(cancellationToken)
            : await PerformBackupAsync(cancellationToken);

        if (result.BackupType == "full")
        {
            RotateBackups();
        }
        CleanupExpiredBackups();

        return result;
    }

    private async Task BackupDatabaseAsync(string filePath, bool isFullBackup, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"BACKUP DATABASE [{connection.Database}] TO DISK = @path WITH {(isFullBackup ? "INIT" : "DIFFERENTIAL, SKIP")}, NAME = @name";
            var pathParam = command.CreateParameter();
            pathParam.ParameterName = "@path";
            pathParam.Value = filePath;
            command.Parameters.Add(pathParam);

            var nameParam = command.CreateParameter();
            nameParam.ParameterName = "@name";
            nameParam.Value = $"supplier-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}_{(isFullBackup ? "full" : "diff")}";
            command.Parameters.Add(nameParam);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private async Task<bool> VerifyBackupFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "RESTORE VERIFYONLY FROM DISK = @path";
            var pathParam = command.CreateParameter();
            pathParam.ParameterName = "@path";
            pathParam.Value = filePath;
            command.Parameters.Add(pathParam);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Backup] Backup verification failed for {Path}", filePath);
            return false;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static DirectoryStats CountFiles(string dir, string pattern)
    {
        if (!Directory.Exists(dir)) return new DirectoryStats();

        var files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
        if (files.Length == 0) return new DirectoryStats();

        var latest = files.Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .First();

        return new DirectoryStats
        {
            Count = files.Length,
            TotalSize = files.Sum(f => f.Length),
            Latest = latest.Name,
            LatestTime = latest.LastWriteTimeUtc.ToString("o"),
        };
    }

    private static long GetFreeSpace(string root)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace;
        }
        catch { return 0; }
    }

    private static DirectoryCleanupResult CleanupDir(string dir, TimeSpan maxAge, string pattern)
    {
        if (!Directory.Exists(dir)) return new DirectoryCleanupResult();

        var cutoff = DateTime.UtcNow - maxAge;
        var files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
        var result = new DirectoryCleanupResult();

        foreach (var file in files)
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc < cutoff)
                {
                    info.Delete();
                    result.Deleted++;
                }
            }
            catch { result.Errors++; }
        }

        return result;
    }

    private static async Task<string> GenerateSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task RecordBackupMetadataAsync(BackupMetadata metadata, CancellationToken cancellationToken)
    {
        await EnsureMetadataTableAsync(cancellationToken);
        _dbContext.BackupMetadata.Add(metadata);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMetadataTableAsync(CancellationToken cancellationToken)
    {
        await _dbContext.Database.ExecuteSqlRawAsync(Sql.CreateMetadataTable, cancellationToken);
    }

    private const double BytesPerGB = 1024.0 * 1024.0 * 1024.0;
}

file static class Sql
{
    public const string CreateMetadataTable = @"
IF OBJECT_ID('backup_metadata', 'U') IS NULL
BEGIN
    CREATE TABLE backup_metadata (
        id INT IDENTITY(1,1) PRIMARY KEY,
        backup_type NVARCHAR(50) NOT NULL,
        file_path NVARCHAR(512) NOT NULL,
        file_name NVARCHAR(256) NOT NULL,
        file_size BIGINT NOT NULL,
        checksum NVARCHAR(128) NOT NULL,
        created_at NVARCHAR(64) NOT NULL,
        verified_at NVARCHAR(64) NULL,
        verification_status NVARCHAR(32) NULL,
        verification_details NVARCHAR(MAX) NULL,
        notes NVARCHAR(MAX) NULL
    )
END";
}

#pragma warning disable SA1402
public sealed class BackupResult
{
    public bool Success { get; init; }
    public string? Path { get; init; }
    public long Size { get; init; }
    public string? Checksum { get; init; }
    public BackupVerificationResult? Verification { get; init; }
    public long DurationMs { get; init; }
    public double FreeSpaceGB { get; init; }
    public string BackupType { get; init; } = "unknown";
    public string? BasedOnFullBackup { get; init; }
    public string? Message { get; init; }
}

public sealed class BackupVerificationResult
{
    public bool Passed { get; set; }
    public Dictionary<string, object?>? Details { get; set; }
}

public sealed class BackupRotationResult
{
    public bool Weekly { get; set; }
    public bool Monthly { get; set; }
    public bool Yearly { get; set; }
}

public sealed class BackupCleanupResult
{
    public DirectoryCleanupResult Full { get; init; } = new();
    public DirectoryCleanupResult Differential { get; init; } = new();
    public DirectoryCleanupResult Weekly { get; init; } = new();
    public DirectoryCleanupResult Monthly { get; init; } = new();
    public DirectoryCleanupResult Yearly { get; init; } = new();
}

public sealed class DirectoryCleanupResult
{
    public int Deleted { get; set; }
    public int Errors { get; set; }
}

public sealed class BackupStats
{
    public DirectoryStats Full { get; init; } = new();
    public DirectoryStats Differential { get; init; } = new();
    public DirectoryStats Weekly { get; init; } = new();
    public DirectoryStats Monthly { get; init; } = new();
    public DirectoryStats Yearly { get; init; } = new();
    public long FreeSpaceBytes { get; init; }
    public string FreeSpaceGB { get; init; } = "0.00";
    public BackupStatsConfig Config { get; init; } = new();
    public string? LatestFullBackup { get; init; }
    public string? LatestDifferentialBackup { get; init; }
    public DateTime? LastBackupTime { get; init; }
    public string BackupMode { get; init; } = "unknown";
}

public sealed class BackupStatsConfig
{
    public BackupRetention? Retention { get; init; }
    public BackupSchedule? Schedule { get; init; }
    public DifferentialBackupConfig? Differential { get; init; }
}

public sealed class DirectoryStats
{
    public int Count { get; init; }
    public long TotalSize { get; init; }
    public string? Latest { get; init; }
    public string? LatestTime { get; init; }
}
#pragma warning restore SA1402

internal static class DateTimeExtensions
{
    internal static int GetWeekOfYear(this DateTime date)
    {
        var ci = System.Globalization.CultureInfo.CurrentCulture;
        return ci.Calendar.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
    }
}
