using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class EmailSettingsStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public EmailSettingsStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<JsonElement?> LoadConfigAsync(string configKey, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == configKey, cancellationToken);

        if (existing == null || string.IsNullOrWhiteSpace(existing.Value))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(existing.Value);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveConfigAsync(string configKey, string payload, string actor, string timestamp, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.SystemConfigs
            .FirstOrDefaultAsync(item => item.Key == configKey, cancellationToken);

        if (existing == null)
        {
            _dbContext.SystemConfigs.Add(new SystemConfig
            {
                Key = configKey,
                Value = payload,
                UpdatedAt = timestamp,
                UpdatedBy = actor,
            });
        }
        else
        {
            existing.Value = payload;
            existing.UpdatedAt = timestamp;
            existing.UpdatedBy = actor;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteConfigAsync(string configKey, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.SystemConfigs
            .FirstOrDefaultAsync(item => item.Key == configKey, cancellationToken);

        if (existing == null)
        {
            return;
        }

        _dbContext.SystemConfigs.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
