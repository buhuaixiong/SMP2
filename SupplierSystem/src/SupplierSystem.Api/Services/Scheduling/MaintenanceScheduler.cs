using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.Reminders;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Scheduling;

public sealed class MaintenanceScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MaintenanceSchedulerOptions _options;
    private readonly ILogger<MaintenanceScheduler> _logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private readonly HashSet<string> _invalidCronLogged = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _lastTokenCleanup;
    private DateTimeOffset? _lastTrackingAccountCleanup;
    private DateTimeOffset? _lastCompletenessUpdate;
    private DateTimeOffset? _lastBackup;
    private DateTimeOffset? _lastArchive;
    private DateTimeOffset? _lastScheduleCheck;

    public MaintenanceScheduler(
        IServiceScopeFactory scopeFactory,
        IOptions<MaintenanceSchedulerOptions> options,
        ILogger<MaintenanceScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[Scheduler] Maintenance scheduler is disabled.");
            return;
        }

        _logger.LogInformation("[Scheduler] Maintenance scheduler started. Interval: {Interval}.", _options.CycleInterval);

        await RunCycleAsync(stoppingToken);
        using var timer = new PeriodicTimer(_options.CycleInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        if (!await _runLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("[Scheduler] Previous run still active. Skipping.");
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            _logger.LogInformation("[Scheduler] Running cycle at {Timestamp}.", now);
            var scheduleWindowStart = _lastScheduleCheck ?? now - _options.CycleInterval;
            var scheduleWindowEnd = now;
            _lastScheduleCheck = now;

            using var scope = _scopeFactory.CreateScope();
            var reminderService = scope.ServiceProvider.GetRequiredService<ReminderQueueService>();
            var reminderWindows = _options.DocumentReminderWindowsDays?.Length > 0
                ? _options.DocumentReminderWindowsDays
                : new[] { 30, 60, 90 };

            await reminderService.ProcessDocumentExpirationsAsync(
                now,
                reminderWindows,
                _options.DocumentReminderHorizonDays,
                cancellationToken);

            await reminderService.ProcessContractExpirationsAsync(
                now,
                _options.ContractExpiredLookbackDays,
                cancellationToken);

            await reminderService.DispatchPendingRemindersAsync(
                now,
                _options.ReminderBatchSize,
                cancellationToken);

            if (ShouldRun(_lastTokenCleanup, _options.TokenCleanupInterval, now))
            {
                await CleanupTokensAsync(scope.ServiceProvider, cancellationToken);
                _lastTokenCleanup = now;
            }

            if (ShouldRun(_lastTrackingAccountCleanup, _options.TrackingAccountCleanupInterval, now))
            {
                await CleanupTrackingAccountsAsync(scope.ServiceProvider, now, cancellationToken);
                _lastTrackingAccountCleanup = now;
            }

            if (ShouldRun(_lastCompletenessUpdate, _options.CompletenessInterval, now))
            {
                await UpdateCompletenessAsync(scope.ServiceProvider, cancellationToken);
                _lastCompletenessUpdate = now;
            }

            var backupConfig = BackupConfig.Load();
            var shouldRunDailyBackup = ShouldRunScheduled(
                backupConfig.Schedule.DailyBackup,
                scheduleWindowStart,
                scheduleWindowEnd,
                "daily-backup");
            var shouldRunDifferentialBackup = backupConfig.Differential.Enabled && ShouldRunScheduled(
                backupConfig.Schedule.DifferentialSchedule,
                scheduleWindowStart,
                scheduleWindowEnd,
                "differential-backup");
            if (shouldRunDailyBackup || shouldRunDifferentialBackup)
            {
                var ranBackup = await RunBackupAsync(
                    scope.ServiceProvider,
                    shouldRunDailyBackup,
                    shouldRunDifferentialBackup,
                    cancellationToken);
                if (ranBackup)
                {
                    _lastBackup = now;
                }
            }
            else if (ShouldRun(_lastBackup, _options.BackupInterval, now))
            {
                var ranBackup = await RunBackupAsync(scope.ServiceProvider, true, false, cancellationToken);
                if (ranBackup)
                {
                    _lastBackup = now;
                }
            }

            if (ShouldRun(_lastArchive, _options.ArchiveInterval, now))
            {
                await RunArchiveAsync(scope.ServiceProvider, cancellationToken);
                _lastArchive = now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scheduler] Cycle failed.");
        }
        finally
        {
            _runLock.Release();
        }
    }

    private async Task CleanupTokensAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<SupplierSystemDbContext>();
        var tokenBlacklist = services.GetRequiredService<ITokenBlacklistService>();

        var removed = await CleanupExpiredFileTokensAsync(dbContext, cancellationToken);
        var blacklistRemoved = await tokenBlacklist.RemoveExpiredAsync();

        if (removed > 0 || blacklistRemoved > 0)
        {
            _logger.LogInformation("[Scheduler] Token cleanup completed. File tokens: {FileTokens}, blacklist: {Blacklist}.", removed, blacklistRemoved);
        }
    }

    private async Task CleanupTrackingAccountsAsync(
        IServiceProvider services,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<SupplierSystemDbContext>();
        var cache = services.GetRequiredService<IMemoryCache>();

        var candidates = await dbContext.Users
            .AsNoTracking()
            .Where(u => (u.AccountType == "tracking" || u.Role == "tracking") && u.SupplierId == null)
            .Select(u => new { u.Id, u.CreatedAt })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        var ttlDays = Math.Max(1, _options.TrackingAccountTtlDays);
        var cutoff = now.AddDays(-ttlDays);
        var expiredIds = new List<string>();

        foreach (var candidate in candidates)
        {
            if (!TryParseUtcTimestamp(candidate.CreatedAt, out var createdAt))
            {
                continue;
            }

            if (createdAt <= cutoff)
            {
                expiredIds.Add(candidate.Id);
            }
        }

        if (expiredIds.Count == 0)
        {
            return;
        }

        var sessions = await dbContext.ActiveSessions
            .Where(s => expiredIds.Contains(s.UserId))
            .ToListAsync(cancellationToken);

        var blacklist = await dbContext.TokenBlacklist
            .Where(entry => entry.UserId != null && expiredIds.Contains(entry.UserId))
            .ToListAsync(cancellationToken);

        var users = await dbContext.Users
            .Where(u => expiredIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        if (sessions.Count > 0)
        {
            dbContext.ActiveSessions.RemoveRange(sessions);
        }

        if (blacklist.Count > 0)
        {
            dbContext.TokenBlacklist.RemoveRange(blacklist);
        }

        if (users.Count > 0)
        {
            dbContext.Users.RemoveRange(users);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var userId in expiredIds)
        {
            cache.Remove($"auth:user:{userId}");
        }

        _logger.LogInformation(
            "[Scheduler] Tracking account cleanup completed. Users: {Users}, sessions: {Sessions}, blacklist: {Blacklist}.",
            users.Count,
            sessions.Count,
            blacklist.Count);
    }

    private async Task UpdateCompletenessAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var supplierService = services.GetRequiredService<ISupplierService>();
        var result = await supplierService.UpdateAllSuppliersCompletenessAsync("scheduler", "scheduled_task", cancellationToken);
        if (result.Failed > 0)
        {
            _logger.LogWarning("[Scheduler] Completeness update completed with failures: {Failed}/{Total}.", result.Failed, result.Total);
        }
        else
        {
            _logger.LogInformation("[Scheduler] Completeness update completed: {Successful}/{Total}.", result.Successful, result.Total);
        }
    }

    private async Task<bool> RunBackupAsync(
        IServiceProvider services,
        bool runDaily,
        bool runDifferential,
        CancellationToken cancellationToken)
    {
        var backupService = services.GetRequiredService<BackupService>();
        var auditService = services.GetRequiredService<IAuditService>();
        var scheduleKind = runDaily ? "daily" : "differential";
        try
        {
            BackupResult result;
            if (runDaily)
            {
                result = await backupService.TriggerManualBackupAsync(false, cancellationToken);
            }
            else if (runDifferential)
            {
                result = await backupService.PerformDifferentialBackupAsync(cancellationToken);
                if (string.Equals(result.BackupType, "full", StringComparison.OrdinalIgnoreCase))
                {
                    backupService.RotateBackups();
                }
                backupService.CleanupExpiredBackups();
            }
            else
            {
                return false;
            }

            if (result.Success)
            {
                _logger.LogInformation("[Scheduler] Backup task completed. Type: {Type}.", result.BackupType);
                await LogBackupAuditAsync(auditService, scheduleKind, result, null);
                return true;
            }

            _logger.LogWarning("[Scheduler] Backup task finished without success. Type: {Type}.", result.BackupType);
            await LogBackupAuditAsync(auditService, scheduleKind, result, null);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scheduler] Backup task failed.");
            await LogBackupAuditAsync(auditService, scheduleKind, null, ex);
            return false;
        }
    }

    private async Task RunArchiveAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var archiveService = services.GetRequiredService<ArchiveService>();
        try
        {
            await archiveService.PerformArchiveSeparationAsync(cancellationToken);
            _logger.LogInformation("[Scheduler] Archive separation task completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scheduler] Archive separation failed.");
        }
    }

    private async Task LogBackupAuditAsync(
        IAuditService auditService,
        string scheduleKind,
        BackupResult? result,
        Exception? exception)
    {
        try
        {
            var success = exception == null && result?.Success == true;
            var summary = success
                ? $"Backup succeeded ({scheduleKind}, {result?.BackupType ?? "unknown"})."
                : $"Backup failed ({scheduleKind}).";
            var changes = JsonSerializer.Serialize(new
            {
                schedule = scheduleKind,
                success,
                backupType = result?.BackupType,
                path = result?.Path,
                size = result?.Size,
                durationMs = result?.DurationMs,
                freeSpaceGb = result?.FreeSpaceGB,
                error = exception?.Message,
            });

            await auditService.LogAsync(new AuditEntry
            {
                ActorId = "system",
                ActorName = "System",
                EntityType = "backup",
                EntityId = scheduleKind,
                Action = success ? "backup_success" : "backup_failed",
                Summary = summary,
                Changes = changes,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Scheduler] Failed to write backup audit entry.");
        }
    }

    private bool ShouldRunScheduled(
        string? cronExpression,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        string scheduleName)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            LogInvalidCron(scheduleName, cronExpression);
            return false;
        }

        if (!CronExpression.TryParse(cronExpression, out var cron))
        {
            LogInvalidCron(scheduleName, cronExpression);
            return false;
        }

        return cron.MatchesWithin(windowStart, windowEnd);
    }

    private void LogInvalidCron(string scheduleName, string? cronExpression)
    {
        var key = $"{scheduleName}:{cronExpression}";
        if (_invalidCronLogged.Add(key))
        {
            _logger.LogWarning(
                "[Scheduler] Invalid cron expression for {ScheduleName}: {CronExpression}",
                scheduleName,
                cronExpression);
        }
    }

    private static bool ShouldRun(DateTimeOffset? lastRun, TimeSpan interval, DateTimeOffset now)
    {
        return !lastRun.HasValue || now - lastRun.Value >= interval;
    }

    private static DateTimeOffset TruncateToMinute(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero);
    }

    private sealed class CronExpression
    {
        private readonly CronField _minute;
        private readonly CronField _hour;
        private readonly CronField _dayOfMonth;
        private readonly CronField _month;
        private readonly CronField _dayOfWeek;

        private CronExpression(
            CronField minute,
            CronField hour,
            CronField dayOfMonth,
            CronField month,
            CronField dayOfWeek)
        {
            _minute = minute;
            _hour = hour;
            _dayOfMonth = dayOfMonth;
            _month = month;
            _dayOfWeek = dayOfWeek;
        }

        public static bool TryParse(string expression, [NotNullWhen(true)] out CronExpression? cron)
        {
            cron = null;
            var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
            {
                return false;
            }

            if (!CronField.TryParse(parts[0], 0, 59, false, out var minute) ||
                !CronField.TryParse(parts[1], 0, 23, false, out var hour) ||
                !CronField.TryParse(parts[2], 1, 31, false, out var dayOfMonth) ||
                !CronField.TryParse(parts[3], 1, 12, false, out var month) ||
                !CronField.TryParse(parts[4], 0, 6, true, out var dayOfWeek))
            {
                return false;
            }

            cron = new CronExpression(minute, hour, dayOfMonth, month, dayOfWeek);
            return true;
        }

        public bool Matches(DateTimeOffset time)
        {
            var utc = time.UtcDateTime;
            if (!_minute.Matches(utc.Minute) || !_hour.Matches(utc.Hour) || !_month.Matches(utc.Month))
            {
                return false;
            }

            var domMatches = _dayOfMonth.Matches(utc.Day);
            var dowMatches = _dayOfWeek.Matches((int)utc.DayOfWeek);

            if (_dayOfMonth.IsWildcard && _dayOfWeek.IsWildcard)
            {
                return true;
            }

            if (_dayOfMonth.IsWildcard)
            {
                return dowMatches;
            }

            if (_dayOfWeek.IsWildcard)
            {
                return domMatches;
            }

            return domMatches || dowMatches;
        }

        public bool MatchesWithin(DateTimeOffset windowStart, DateTimeOffset windowEnd)
        {
            if (windowEnd <= windowStart)
            {
                return false;
            }

            var cursor = TruncateToMinute(windowStart);
            if (cursor <= windowStart)
            {
                cursor = cursor.AddMinutes(1);
            }

            var end = TruncateToMinute(windowEnd);
            while (cursor <= end)
            {
                if (Matches(cursor))
                {
                    return true;
                }

                cursor = cursor.AddMinutes(1);
            }

            return false;
        }
    }

    private sealed class CronField
    {
        private readonly bool[] _allowed;
        private readonly int _min;

        private CronField(bool[] allowed, int min, bool isWildcard)
        {
            _allowed = allowed;
            _min = min;
            IsWildcard = isWildcard;
        }

        public bool IsWildcard { get; }

        public bool Matches(int value)
        {
            if (value < _min || value >= _min + _allowed.Length)
            {
                return false;
            }

            return _allowed[value - _min];
        }

        public static bool TryParse(
            string field,
            int min,
            int max,
            bool mapSundayToZero,
            [NotNullWhen(true)] out CronField? cronField)
        {
            cronField = null;
            if (string.IsNullOrWhiteSpace(field))
            {
                return false;
            }

            var isWildcard = string.Equals(field.Trim(), "*", StringComparison.Ordinal);
            var allowed = new bool[max - min + 1];
            var segments = field.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            foreach (var rawSegment in segments)
            {
                var segment = rawSegment.Trim();
                if (segment.Length == 0)
                {
                    continue;
                }

                var step = 1;
                var stepParts = segment.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (stepParts.Length > 2)
                {
                    return false;
                }

                if (stepParts.Length == 2 && !int.TryParse(stepParts[1], out step))
                {
                    return false;
                }

                if (step <= 0)
                {
                    return false;
                }

                var rangePart = stepParts[0];
                int start;
                int end;
                if (string.Equals(rangePart, "*", StringComparison.Ordinal))
                {
                    start = min;
                    end = max;
                }
                else if (rangePart.Contains('-'))
                {
                    var rangeParts = rangePart.Split('-', StringSplitOptions.RemoveEmptyEntries);
                    if (rangeParts.Length != 2 ||
                        !int.TryParse(rangeParts[0], out start) ||
                        !int.TryParse(rangeParts[1], out end))
                    {
                        return false;
                    }
                }
                else if (!int.TryParse(rangePart, out start))
                {
                    return false;
                }
                else
                {
                    end = start;
                }

                if (end < start)
                {
                    return false;
                }

                for (var value = start; value <= end; value += step)
                {
                    var normalized = mapSundayToZero && value == 7 ? 0 : value;
                    if (normalized < min || normalized > max)
                    {
                        return false;
                    }

                    allowed[normalized - min] = true;
                }
            }

            cronField = new CronField(allowed, min, isWildcard);
            return true;
        }
    }

    private static bool TryParseUtcTimestamp(string? value, out DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            timestamp = default;
            return false;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out timestamp);
    }

    private static async Task<int> CleanupExpiredFileTokensAsync(
        SupplierSystemDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM file_access_tokens WHERE expiresAt < DATEADD(day, -1, SYSUTCDATETIME())";
        var removed = await command.ExecuteNonQueryAsync(cancellationToken);
        return removed;
    }
}
