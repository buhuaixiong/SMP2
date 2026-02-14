namespace SupplierSystem.Api.Services.Scheduling;

public sealed class MaintenanceSchedulerOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 30;
    public int TokenCleanupMinutes { get; set; } = 60;
    public int TrackingAccountCleanupHours { get; set; } = 24;
    public int TrackingAccountTtlDays { get; set; } = 30;
    public int CompletenessHours { get; set; } = 24;
    public int BackupDays { get; set; } = 7;
    public int ArchiveDays { get; set; } = 30;
    public int ReminderBatchSize { get; set; } = 50;
    public int DocumentReminderHorizonDays { get; set; } = 90;
    public int ContractExpiredLookbackDays { get; set; } = 30;
    public int[] DocumentReminderWindowsDays { get; set; } = new[] { 30, 60, 90 };

    public TimeSpan CycleInterval => TimeSpan.FromMinutes(Math.Max(1, IntervalMinutes));
    public TimeSpan TokenCleanupInterval => TimeSpan.FromMinutes(Math.Max(1, TokenCleanupMinutes));
    public TimeSpan TrackingAccountCleanupInterval => TimeSpan.FromHours(Math.Max(1, TrackingAccountCleanupHours));
    public TimeSpan CompletenessInterval => TimeSpan.FromHours(Math.Max(1, CompletenessHours));
    public TimeSpan BackupInterval => TimeSpan.FromDays(Math.Max(1, BackupDays));
    public TimeSpan ArchiveInterval => TimeSpan.FromDays(Math.Max(1, ArchiveDays));
}
