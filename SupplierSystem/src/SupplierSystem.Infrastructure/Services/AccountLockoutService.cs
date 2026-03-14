using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Infrastructure.Services;

/// <summary>
/// Account lockout service using MemoryCache.
/// </summary>
public sealed class AccountLockoutService : IAccountLockoutService
{
    private const int MaxFailedAttempts = 5;
    private const int LockoutDurationMinutes = 30;
    private static readonly TimeSpan FailureSlidingExpiration = TimeSpan.FromHours(24);
    private static readonly TimeSpan LockedAbsoluteExpiration = TimeSpan.FromHours(24);

    private readonly IMemoryCache _cache;
    private readonly IAuditService _auditService;
    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<AccountLockoutService> _logger;

    public AccountLockoutService(IMemoryCache cache, IAuditService auditService, ILogger<AccountLockoutService> logger)
    {
        _cache = cache;
        _auditService = auditService;
        _logger = logger;
    }

    public AccountLockoutStatus Check(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return new AccountLockoutStatus();
        }

        var key = Normalize(username);
        if (!_cache.TryGetValue(key, out var cachedRecord) || cachedRecord is not LockoutRecord record)
        {
            _trackedKeys.TryRemove(key, out _);
            return new AccountLockoutStatus();
        }

        var now = DateTimeOffset.UtcNow;
        if (record.LockedUntil.HasValue && record.LockedUntil.Value > now)
        {
            var remaining = (int)Math.Ceiling((record.LockedUntil.Value - now).TotalMinutes);
            return new AccountLockoutStatus
            {
                Locked = true,
                RemainingTimeMinutes = Math.Max(1, remaining),
                Attempts = record.Count,
            };
        }

        if (record.LockedUntil.HasValue)
        {
            _cache.Remove(key);
            _trackedKeys.TryRemove(key, out _);
            return new AccountLockoutStatus();
        }

        return new AccountLockoutStatus
        {
            Locked = false,
            RemainingTimeMinutes = 0,
            Attempts = record.Count,
        };
    }

    public FailedAttemptResult RecordFailedAttempt(string username, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return new FailedAttemptResult
            {
                Locked = false,
                RemainingAttempts = MaxFailedAttempts,
            };
        }

        var key = Normalize(username);
        var now = DateTimeOffset.UtcNow;
        var record = GetOrCreateRecord(key, now);

        FailedAttemptResult result;
        bool lockedNow = false;
        DateTimeOffset? lockedUntil = null;

        lock (record)
        {
            if (record.LockedUntil.HasValue && record.LockedUntil.Value <= now)
            {
                record.Count = 0;
                record.LockedUntil = null;
            }

            if (record.LockedUntil.HasValue && record.LockedUntil.Value > now)
            {
                result = new FailedAttemptResult
                {
                    Locked = true,
                    RemainingAttempts = 0,
                    LockedUntil = record.LockedUntil,
                };
            }
            else
            {
                record.Count += 1;
                record.LastAttempt = now;

                if (record.Count >= MaxFailedAttempts)
                {
                    lockedUntil = now.AddMinutes(LockoutDurationMinutes);
                    record.LockedUntil = lockedUntil;
                    lockedNow = true;
                    result = new FailedAttemptResult
                    {
                        Locked = true,
                        RemainingAttempts = 0,
                        LockedUntil = lockedUntil,
                    };
                }
                else
                {
                    result = new FailedAttemptResult
                    {
                        Locked = false,
                        RemainingAttempts = MaxFailedAttempts - record.Count,
                    };
                }
            }
        }

        SetRecord(key, record, now);

        if (lockedNow && lockedUntil.HasValue)
        {
            _ = LogLockoutAuditAsync(key, lockedUntil.Value, record.Count, ipAddress);
        }

        return result;
    }

    public void Reset(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var key = Normalize(username);
        var hadFailures = _cache.TryGetValue(key, out _);

        _cache.Remove(key);
        _trackedKeys.TryRemove(key, out _);

        if (hadFailures)
        {
            _ = LogResetAuditAsync(key);
        }
    }

    public bool Unlock(string username, string? adminId)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var key = Normalize(username);
        var wasLocked = _cache.TryGetValue(key, out var record) &&
                        record is LockoutRecord lockoutRecord &&
                        lockoutRecord.LockedUntil.HasValue;

        _cache.Remove(key);
        _trackedKeys.TryRemove(key, out _);

        if (wasLocked)
        {
            _ = LogUnlockAuditAsync(key, adminId);
        }

        return wasLocked;
    }

    public LockoutStats GetStats()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var totalAccounts = 0;
            var currentlyLocked = 0;
            var accountsWithFailures = 0;

            foreach (var key in _trackedKeys.Keys.ToArray())
            {
                if (!_cache.TryGetValue(key, out var cachedRecord) || cachedRecord is not LockoutRecord record)
                {
                    _trackedKeys.TryRemove(key, out _);
                    continue;
                }

                totalAccounts += 1;

                if (record.Count > 0)
                {
                    accountsWithFailures += 1;
                }

                if (record.LockedUntil.HasValue && record.LockedUntil.Value > now)
                {
                    currentlyLocked += 1;
                }
            }

            return new LockoutStats
            {
                TotalAccounts = totalAccounts,
                CurrentlyLocked = currentlyLocked,
                AccountsWithFailures = accountsWithFailures,
                MaxFailedAttempts = MaxFailedAttempts,
                LockoutDurationMinutes = LockoutDurationMinutes,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lockout stats");
            return new LockoutStats
            {
                TotalAccounts = 0,
                CurrentlyLocked = 0,
                AccountsWithFailures = 0,
                MaxFailedAttempts = MaxFailedAttempts,
                LockoutDurationMinutes = LockoutDurationMinutes,
            };
        }
    }

    private LockoutRecord GetOrCreateRecord(string key, DateTimeOffset now)
    {
        _trackedKeys.TryAdd(key, 0);
        return _cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = FailureSlidingExpiration;
            entry.RegisterPostEvictionCallback(OnCacheEntryEvicted, this);
            return new LockoutRecord { Count = 0, LastAttempt = now };
        })!;
    }

    private void SetRecord(string key, LockoutRecord record, DateTimeOffset now)
    {
        var options = new MemoryCacheEntryOptions();
        _trackedKeys.TryAdd(key, 0);
        options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = OnCacheEntryEvicted,
            State = this,
        });

        if (record.LockedUntil.HasValue && record.LockedUntil.Value > now)
        {
            var remaining = record.LockedUntil.Value - now;
            var minExpiration = TimeSpan.FromSeconds(1);
            options.AbsoluteExpirationRelativeToNow = remaining > minExpiration ? remaining : minExpiration;
        }
        else
        {
            options.SlidingExpiration = FailureSlidingExpiration;
        }

        _cache.Set(key, record, options);
    }

    private static string Normalize(string username) => username.Trim().ToLowerInvariant();

    private static void OnCacheEntryEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (reason == EvictionReason.Replaced)
        {
            return;
        }

        if (state is AccountLockoutService service && key is string keyString)
        {
            service._trackedKeys.TryRemove(keyString, out _);
        }
    }

    private async Task LogLockoutAuditAsync(string username, DateTimeOffset lockedUntil, int attemptCount, string? ipAddress)
    {
        try
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = username,
                ActorName = username,
                EntityType = "auth",
                EntityId = username,
                Action = "account_locked",
                Changes = System.Text.Json.JsonSerializer.Serialize(new
                {
                    reason = "too_many_failed_attempts",
                    attemptCount,
                    lockedUntil = lockedUntil.ToString("o"),
                    lockoutDurationMinutes = LockoutDurationMinutes,
                }),
                IpAddress = ipAddress,
            });
        }
        catch
        {
        }
    }

    private async Task LogResetAuditAsync(string username)
    {
        try
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = username,
                ActorName = username,
                EntityType = "auth",
                EntityId = username,
                Action = "failed_attempts_reset",
                Changes = System.Text.Json.JsonSerializer.Serialize(new { reason = "successful_login" }),
            });
        }
        catch
        {
        }
    }

    private async Task LogUnlockAuditAsync(string username, string? adminId)
    {
        try
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = adminId ?? "system",
                ActorName = adminId ?? "system",
                EntityType = "auth",
                EntityId = username,
                Action = "account_unlocked",
                Changes = System.Text.Json.JsonSerializer.Serialize(new
                {
                    unlockedBy = adminId ?? "system",
                    reason = "manual_unlock",
                }),
            });
        }
        catch
        {
        }
    }

    private sealed class LockoutRecord
    {
        public int Count { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
        public DateTimeOffset LastAttempt { get; set; }
    }
}
