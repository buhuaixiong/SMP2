using System.Threading;

namespace SupplierSystem.Api.Services;

public sealed class AuthSchemaMonitor
{
    public const int CurrentSchemaVersion = 2;
    private int _repairCount;
    private int _outdatedCount;
    private readonly ILogger<AuthSchemaMonitor> _logger;

    public AuthSchemaMonitor(ILogger<AuthSchemaMonitor> logger)
    {
        _logger = logger;
    }

    public void RecordRepair(string userId, string context)
    {
        var count = Interlocked.Increment(ref _repairCount);
        _logger.LogWarning("[AuthSchemaMonitor] Auto-repair #{Count} | User: {UserId} | Context: {Context}", count, userId, context);
    }

    public void RecordOutdatedCache(string userId, string oldVersion)
    {
        var count = Interlocked.Increment(ref _outdatedCount);
        _logger.LogInformation("[AuthSchemaMonitor] Outdated cache #{Count} | User: {UserId} | Old version: {OldVersion} | Current: {Current}",
            count, userId, oldVersion, CurrentSchemaVersion);
    }

    public AuthSchemaStats GetStats()
    {
        return new AuthSchemaStats
        {
            CurrentVersion = CurrentSchemaVersion,
            RepairCount = _repairCount,
            OutdatedCount = _outdatedCount,
        };
    }
}

public sealed class AuthSchemaStats
{
    public int CurrentVersion { get; set; }
    public int RepairCount { get; set; }
    public int OutdatedCount { get; set; }
}
