using System.Collections.Concurrent;

namespace SupplierSystem.Api.Services;

public sealed class BackupScheduler
{
    private readonly BackupService _backupService;
    private readonly ArchiveService _archiveService;
    private readonly BackupAlertService _alertService;
    private readonly SchedulerState _state = new();

    public BackupScheduler(
        BackupService backupService,
        ArchiveService archiveService,
        BackupAlertService alertService)
    {
        _backupService = backupService;
        _archiveService = archiveService;
        _alertService = alertService;
        _state.Initialized = true;
    }

    public BackupSchedulerStatus GetStatus()
    {
        var config = BackupConfig.Load();
        return new BackupSchedulerStatus
        {
            Initialized = _state.Initialized,
            Tasks = new Dictionary<string, string>
            {
                ["dailyBackup"] = "manual",
                ["archiveSeparation"] = "manual",
            },
            IsRunning = new Dictionary<string, bool>
            {
                ["dailyBackup"] = _state.IsRunningDaily,
                ["archiveSeparation"] = _state.IsRunningArchive,
            },
            LastRun = new Dictionary<string, BackupRunResult?>
            {
                ["dailyBackup"] = _state.LastDaily,
                ["archiveSeparation"] = _state.LastArchive,
            },
            Config = new Dictionary<string, object?>
            {
                ["dailyBackupSchedule"] = config.Schedule.DailyBackup,
                ["archiveSchedule"] = config.Schedule.ArchiveSeparation,
            },
        };
    }

    public async Task<BackupRunResult?> TriggerManualBackupAsync(CancellationToken cancellationToken)
    {
        if (_state.IsRunningDaily)
        {
            return _state.LastDaily;
        }

        _state.IsRunningDaily = true;
        var run = new BackupRunResult { StartTime = DateTimeOffset.UtcNow.ToString("o") };

        try
        {
            var backup = await _backupService.TriggerManualBackupAsync(false, cancellationToken);
            run.Success = true;
            run.Result = backup;
        }
        catch (Exception ex)
        {
            run.Success = false;
            run.Error = ex.Message;
            await _alertService.RecordAsync("critical", "backup_failed", ex.Message, null, false, false, false, cancellationToken);
        }
        finally
        {
            run.EndTime = DateTimeOffset.UtcNow.ToString("o");
            _state.LastDaily = run;
            _state.IsRunningDaily = false;
        }

        return run;
    }

    public async Task<BackupRunResult?> TriggerManualArchiveAsync(CancellationToken cancellationToken)
    {
        if (_state.IsRunningArchive)
        {
            return _state.LastArchive;
        }

        _state.IsRunningArchive = true;
        var run = new BackupRunResult { StartTime = DateTimeOffset.UtcNow.ToString("o") };

        try
        {
            var stats = await _archiveService.PerformArchiveSeparationAsync(cancellationToken);
            run.Success = true;
            run.Result = stats;
        }
        catch (Exception ex)
        {
            run.Success = false;
            run.Error = ex.Message;
            await _alertService.RecordAsync("critical", "archive_failed", ex.Message, null, false, false, false, cancellationToken);
        }
        finally
        {
            run.EndTime = DateTimeOffset.UtcNow.ToString("o");
            _state.LastArchive = run;
            _state.IsRunningArchive = false;
        }

        return run;
    }

    private sealed class SchedulerState
    {
        public bool Initialized { get; set; }
        public bool IsRunningDaily { get; set; }
        public bool IsRunningArchive { get; set; }
        public BackupRunResult? LastDaily { get; set; }
        public BackupRunResult? LastArchive { get; set; }
    }
}

public sealed class BackupSchedulerStatus
{
    public bool Initialized { get; set; }
    public Dictionary<string, string> Tasks { get; set; } = new();
    public Dictionary<string, bool> IsRunning { get; set; } = new();
    public Dictionary<string, BackupRunResult?> LastRun { get; set; } = new();
    public Dictionary<string, object?> Config { get; set; } = new();
}

public sealed class BackupRunResult
{
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public object? Result { get; set; }
}
