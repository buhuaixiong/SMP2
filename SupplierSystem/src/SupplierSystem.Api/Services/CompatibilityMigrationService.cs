using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class CompatibilityMigrationService
{
    private const string ConfigKey = "compat.sqlite.migrated";
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private readonly SupplierSystemDbContext _dbContext;
    private readonly CompatibilitySchemaService _schemaService;

    public CompatibilityMigrationService(
        SupplierSystemDbContext dbContext,
        IWebHostEnvironment _,
        CompatibilitySchemaService schemaService)
    {
        _dbContext = dbContext;
        _schemaService = schemaService;
    }

    public async Task EnsureMigratedAsync(CancellationToken cancellationToken)
    {
        await _schemaService.EnsureTablesAsync(cancellationToken);

        var status = await _dbContext.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == ConfigKey, cancellationToken);

        if (string.Equals(status?.Value, "done", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await MigrationLock.WaitAsync(cancellationToken);
        try
        {
            status = await _dbContext.SystemConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Key == ConfigKey, cancellationToken);
            if (string.Equals(status?.Value, "done", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await SaveStatusAsync("done", cancellationToken);
        }
        finally
        {
            MigrationLock.Release();
        }
    }

    private async Task SaveStatusAsync(string value, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToString("o");
        var existing = await _dbContext.SystemConfigs
            .FirstOrDefaultAsync(item => item.Key == ConfigKey, cancellationToken);

        if (existing == null)
        {
            _dbContext.SystemConfigs.Add(new SystemConfig
            {
                Key = ConfigKey,
                Value = value,
                UpdatedAt = now,
                UpdatedBy = "system"
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = now;
            existing.UpdatedBy = "system";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
