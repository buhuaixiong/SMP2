namespace SupplierSystem.Api.Services;

public sealed class BackupConfig
{
    private const string DefaultBackupRoot = "C:/supplier-deploy/backups";
    private const string DefaultArchiveRoot = "C:/supplier-deploy/archives";
    private const string DefaultUploadsArchive = "C:/supplier-deploy/uploads-archive";
    private const string DefaultDataDir = "C:/supplier-deploy/data";
    private const string DefaultUploadsDir = "C:/supplier-deploy/uploads";

    public BackupPaths Paths { get; }
    public BackupRetention Retention { get; }
    public BackupSchedule Schedule { get; }
    public BackupAlerts Alerts { get; }
    public BackupVerification Verification { get; }
    public DifferentialBackupConfig Differential { get; }

    public static BackupConfig Load() => new BackupConfig();

    private BackupConfig()
    {
        var backupRoot = GetEnv("BACKUP_ROOT_DIR") ?? DefaultBackupRoot;
        var archiveRoot = GetEnv("ARCHIVE_ROOT_DIR") ?? DefaultArchiveRoot;
        var uploadsArchive = GetEnv("UPLOADS_ARCHIVE_DIR") ?? DefaultUploadsArchive;
        var dataDir = GetEnv("DATA_DIR") ?? DefaultDataDir;
        var uploadsDir = GetEnv("UPLOAD_DIR") ?? DefaultUploadsDir;

        Paths = new BackupPaths
        {
            BackupRoot = backupRoot,
            ArchiveRoot = archiveRoot,
            UploadsArchive = uploadsArchive,
            DataDir = dataDir,
            UploadsDir = uploadsDir,
            Daily = Path.Combine(backupRoot, "daily"),
            Weekly = Path.Combine(backupRoot, "weekly"),
            Monthly = Path.Combine(backupRoot, "monthly"),
            Yearly = Path.Combine(backupRoot, "yearly"),
            Differential = Path.Combine(backupRoot, "differential"),
            RfqArchive = Path.Combine(archiveRoot, "rfq"),
            SupplierArchive = Path.Combine(archiveRoot, "supplier"),
            AuditArchive = Path.Combine(archiveRoot, "audit"),
        };

        Retention = new BackupRetention
        {
            DailyDays = GetInt("BACKUP_DAILY_RETENTION_DAYS", 7),
            WeeklyWeeks = GetInt("BACKUP_WEEKLY_RETENTION_WEEKS", 12),
            MonthlyMonths = GetInt("BACKUP_MONTHLY_RETENTION_MONTHS", 36),
            YearlyYears = GetInt("BACKUP_YEARLY_RETENTION_YEARS", 5),
            DifferentialDays = GetInt("BACKUP_DIFFERENTIAL_RETENTION_DAYS", 3),
            RfqArchiveYears = GetInt("ARCHIVE_RFQ_RETENTION_YEARS", 5),
            SupplierArchiveYears = GetInt("ARCHIVE_SUPPLIER_RETENTION_YEARS", 3),
            AuditArchiveYears = GetInt("ARCHIVE_AUDIT_RETENTION_YEARS", 7),
        };

        Schedule = new BackupSchedule
        {
            DailyBackup = GetEnv("BACKUP_DAILY_SCHEDULE") ?? "0 0 * * *",
            DifferentialSchedule = GetEnv("BACKUP_DIFFERENTIAL_SCHEDULE") ?? "0 */4 * * *",
            ArchiveSeparation = GetEnv("ARCHIVE_MONTHLY_SCHEDULE") ?? "0 3 1 * *",
        };

        Alerts = new BackupAlerts
        {
            Enabled = !string.Equals(GetEnv("BACKUP_ALERT_ENABLED"), "false", StringComparison.OrdinalIgnoreCase),
            EmailTo = GetEnv("BACKUP_ALERT_EMAIL_TO") ?? "admin@example.com",
            SpaceWarningGB = GetInt("BACKUP_SPACE_WARNING_GB", 50),
            SpaceCriticalGB = GetInt("BACKUP_SPACE_CRITICAL_GB", 20),
        };

        Verification = new BackupVerification
        {
            Enabled = !string.Equals(GetEnv("BACKUP_VERIFY_ENABLED"), "false", StringComparison.OrdinalIgnoreCase),
            MinSizeBytes = GetInt("BACKUP_MIN_SIZE_BYTES", 100000),
        };

        Differential = new DifferentialBackupConfig
        {
            Enabled = !string.Equals(GetEnv("BACKUP_DIFFERENTIAL_ENABLED"), "false", StringComparison.OrdinalIgnoreCase),
            IntervalHours = GetInt("BACKUP_DIFFERENTIAL_INTERVAL_HOURS", 6),
            MaxDifferentialCount = GetInt("BACKUP_DIFFERENTIAL_MAX_COUNT", 12),
        };
    }

    private static string? GetEnv(string name) => Environment.GetEnvironmentVariable(name);
    private static int GetInt(string name, int fallback) => int.TryParse(GetEnv(name), out var v) ? v : fallback;
}

#pragma warning disable SA1402
public sealed class BackupPaths
{
    public string BackupRoot { get; init; } = string.Empty;
    public string ArchiveRoot { get; init; } = string.Empty;
    public string UploadsArchive { get; init; } = string.Empty;
    public string DataDir { get; init; } = string.Empty;
    public string UploadsDir { get; init; } = string.Empty;
    public string Daily { get; init; } = string.Empty;
    public string Weekly { get; init; } = string.Empty;
    public string Monthly { get; init; } = string.Empty;
    public string Yearly { get; init; } = string.Empty;
    public string Differential { get; init; } = string.Empty;
    public string RfqArchive { get; init; } = string.Empty;
    public string SupplierArchive { get; init; } = string.Empty;
    public string AuditArchive { get; init; } = string.Empty;
}

public sealed class BackupRetention
{
    public int DailyDays { get; init; }
    public int WeeklyWeeks { get; init; }
    public int MonthlyMonths { get; init; }
    public int YearlyYears { get; init; }
    public int DifferentialDays { get; init; }
    public int RfqArchiveYears { get; init; }
    public int SupplierArchiveYears { get; init; }
    public int AuditArchiveYears { get; init; }
}

public sealed class BackupSchedule
{
    public string DailyBackup { get; init; } = string.Empty;
    public string DifferentialSchedule { get; init; } = string.Empty;
    public string ArchiveSeparation { get; init; } = string.Empty;
}

public sealed class BackupAlerts
{
    public bool Enabled { get; init; }
    public string EmailTo { get; init; } = string.Empty;
    public int SpaceWarningGB { get; init; }
    public int SpaceCriticalGB { get; init; }
}

public sealed class BackupVerification
{
    public bool Enabled { get; init; }
    public int MinSizeBytes { get; init; }
}

public sealed class DifferentialBackupConfig
{
    public bool Enabled { get; init; }
    public int IntervalHours { get; init; }
    public int MaxDifferentialCount { get; init; }
}
#pragma warning restore SA1402
