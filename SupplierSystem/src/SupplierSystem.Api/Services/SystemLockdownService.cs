using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class SystemLockdownService
{
    private const string LockdownKey = "emergency_lockdown";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);
    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IMemoryCache _cache;

    public SystemLockdownService(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        IMemoryCache cache)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _cache = cache;
    }

    public async Task<SystemLockdownStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(LockdownKey, out SystemLockdownStatus? cached) && cached != null)
        {
            return cached;
        }

        var status = await LoadStatusAsync(cancellationToken);
        _cache.Set(LockdownKey, status, CacheTtl);
        return status;
    }

    public async Task<SystemLockdownStatus> ActivateAsync(
        string actorId,
        string actorName,
        string reason,
        string announcement,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToString("o");
        var metadata = new Dictionary<string, object?>
        {
            ["reason"] = reason,
            ["announcement"] = announcement,
            ["activatedAt"] = now,
            ["activatedBy"] = actorId,
            ["activatedByName"] = actorName,
        };

        var record = await EnsureConfigAsync(cancellationToken);
        record.Value = "active";
        record.Metadata = JsonSerializer.Serialize(metadata);
        record.UpdatedAt = now;
        record.UpdatedBy = actorId;

        _dbContext.SystemConfigs.Update(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = actorId,
            ActorName = actorName,
            EntityType = "system",
            EntityId = LockdownKey,
            Action = "LOCKDOWN_ACTIVATED",
            Changes = JsonSerializer.Serialize(new { reason, announcement, activatedAt = now }),
        }).ConfigureAwait(false);

        _cache.Remove(LockdownKey);
        return await GetStatusAsync(cancellationToken);
    }

    public async Task<SystemLockdownStatus> DeactivateAsync(
        string actorId,
        string actorName,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToString("o");
        var metadata = new Dictionary<string, object?>
        {
            ["reason"] = null,
            ["announcement"] = null,
            ["activatedAt"] = null,
            ["activatedBy"] = null,
            ["deactivatedAt"] = now,
            ["deactivatedBy"] = actorId,
            ["deactivatedByName"] = actorName,
        };

        var record = await EnsureConfigAsync(cancellationToken);
        record.Value = "inactive";
        record.Metadata = JsonSerializer.Serialize(metadata);
        record.UpdatedAt = now;
        record.UpdatedBy = actorId;

        _dbContext.SystemConfigs.Update(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = actorId,
            ActorName = actorName,
            EntityType = "system",
            EntityId = LockdownKey,
            Action = "LOCKDOWN_DEACTIVATED",
            Changes = JsonSerializer.Serialize(new { deactivatedAt = now }),
        }).ConfigureAwait(false);

        _cache.Remove(LockdownKey);
        return await GetStatusAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LockdownHistoryEntry>> GetHistoryAsync(int limit, CancellationToken cancellationToken)
    {
        var logs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityType == "system"
                        && a.EntityId == LockdownKey
                        && (a.Action == "LOCKDOWN_ACTIVATED" || a.Action == "LOCKDOWN_DEACTIVATED"))
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return logs.Select(log => new LockdownHistoryEntry
        {
            Id = log.Id,
            Action = log.Action,
            ActorId = log.ActorId,
            ActorName = log.ActorName,
            Timestamp = log.CreatedAt.ToString("o"),
            Changes = ParseJson(log.Changes),
            IpAddress = log.IpAddress,
        }).ToList();
    }

    private async Task<SystemConfig> EnsureConfigAsync(CancellationToken cancellationToken)
    {
        var record = await _dbContext.SystemConfigs
            .FirstOrDefaultAsync(s => s.Key == LockdownKey, cancellationToken);

        if (record != null)
        {
            return record;
        }

        record = new SystemConfig
        {
            Key = LockdownKey,
            Value = "inactive",
            Metadata = JsonSerializer.Serialize(new
            {
                reason = (string?)null,
                announcement = (string?)null,
                activatedAt = (string?)null,
                activatedBy = (string?)null,
            }),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedBy = "system",
        };

        _dbContext.SystemConfigs.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return record;
    }

    private async Task<SystemLockdownStatus> LoadStatusAsync(CancellationToken cancellationToken)
    {
        var record = await EnsureConfigAsync(cancellationToken);
        var metadata = ParseMetadata(record.Metadata);

        return new SystemLockdownStatus
        {
            IsActive = string.Equals(record.Value, "active", StringComparison.OrdinalIgnoreCase),
            Announcement = metadata.Announcement,
            Reason = metadata.Reason,
            ActivatedAt = metadata.ActivatedAt,
            ActivatedBy = metadata.ActivatedBy,
            ActivatedByName = metadata.ActivatedByName,
        };
    }

    private static LockdownMetadata ParseMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new LockdownMetadata();
        }

        try
        {
            return JsonSerializer.Deserialize<LockdownMetadata>(json) ?? new LockdownMetadata();
        }
        catch
        {
            return new LockdownMetadata();
        }
    }

    private static Dictionary<string, object?>? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        }
        catch
        {
            return null;
        }
    }

    private sealed class LockdownMetadata
    {
        public string? Reason { get; set; }
        public string? Announcement { get; set; }
        public string? ActivatedAt { get; set; }
        public string? ActivatedBy { get; set; }
        public string? ActivatedByName { get; set; }
        public string? DeactivatedAt { get; set; }
        public string? DeactivatedBy { get; set; }
        public string? DeactivatedByName { get; set; }
    }
}

public sealed class SystemLockdownStatus
{
    public bool IsActive { get; set; }
    public string? Announcement { get; set; }
    public string? Reason { get; set; }
    public string? ActivatedAt { get; set; }
    public string? ActivatedBy { get; set; }
    public string? ActivatedByName { get; set; }
}

public sealed class LockdownHistoryEntry
{
    public int Id { get; set; }
    public string? Action { get; set; }
    public string? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? Timestamp { get; set; }
    public Dictionary<string, object?>? Changes { get; set; }
    public string? IpAddress { get; set; }
}
