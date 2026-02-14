using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SupplierSystem.Application.Interfaces;

namespace SupplierSystem.Infrastructure.Services;

/// <summary>
/// 基于滑动窗口的速率限制服务
/// </summary>
public sealed class RateLimitService : IRateLimitService
{
    private const int DefaultLimit = 10;
    private const int DefaultWindowSeconds = 60;

    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly int _limit;
    private readonly int _windowSeconds;
    private readonly Timer? _cleanupTimer;
    private readonly ILogger<RateLimitService> _logger;

    public RateLimitService(int limit = DefaultLimit, int windowSeconds = DefaultWindowSeconds, ILogger<RateLimitService>? logger = null)
    {
        _limit = limit;
        _windowSeconds = windowSeconds;
        _logger = logger ?? new NullLogger<RateLimitService>();
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public bool ShouldBlock(string identifier, string endpoint)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        var key = BuildKey(identifier, endpoint);
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            return false;
        }

        return bucket.RequestCount >= _limit && !bucket.IsReset;
    }

    public void RecordRequest(string identifier, string endpoint)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return;
        }

        var key = BuildKey(identifier, endpoint);
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddSeconds(-_windowSeconds);

        _buckets.AddOrUpdate(key,
            _ => new RateLimitBucket
            {
                FirstRequest = now,
                LastRequest = now,
                RequestCount = 1,
                WindowStart = now,
            },
            (_, existing) =>
            {
                // 如果距离上次请求超过窗口时间，重置计数器
                if (existing.LastRequest < windowStart)
                {
                    return new RateLimitBucket
                    {
                        FirstRequest = now,
                        LastRequest = now,
                        RequestCount = 1,
                        WindowStart = now,
                    };
                }

                existing.LastRequest = now;
                existing.RequestCount++;
                return existing;
            });
    }

    public void Reset(string identifier, string endpoint)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return;
        }

        var key = BuildKey(identifier, endpoint);
        _buckets.TryRemove(key, out _);
    }

    private string BuildKey(string identifier, string endpoint)
    {
        return $"{endpoint}:{identifier.Trim().ToLowerInvariant()}";
    }

    private void CleanupExpired(object? state)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var windowStart = now.AddSeconds(-_windowSeconds);

            foreach (var entry in _buckets.ToArray())
            {
                var bucket = entry.Value;
                if (bucket.LastRequest < windowStart || (bucket.FirstRequest < now.AddHours(-1)))
                {
                    _buckets.TryRemove(entry.Key, out _);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rate limit bucket cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }

    private sealed class RateLimitBucket
    {
        public DateTimeOffset FirstRequest { get; set; }
        public DateTimeOffset LastRequest { get; set; }
        public int RequestCount { get; set; }
        public DateTimeOffset WindowStart { get; set; }
        public bool IsReset { get; set; }
    }
}

/// <summary>
/// 专门用于登录请求的速率限制服务
/// </summary>
public sealed class LoginRateLimitService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, LoginAttemptRecord> _attempts = new();
    private readonly Timer? _cleanupTimer;

    public LoginRateLimitService()
    {
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public bool IsBlocked(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return false;
        }

        var key = NormalizeIp(ipAddress);
        if (!_attempts.TryGetValue(key, out var record))
        {
            return false;
        }

        if (record.LockedUntil.HasValue && record.LockedUntil.Value > DateTimeOffset.UtcNow)
        {
            return true;
        }

        if (record.FailedCount >= MaxAttempts)
        {
            record.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
            return true;
        }

        return false;
    }

    public int? GetRemainingSeconds(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        var key = NormalizeIp(ipAddress);
        if (!_attempts.TryGetValue(key, out var record) || !record.LockedUntil.HasValue)
        {
            return null;
        }

        var remaining = record.LockedUntil.Value - DateTimeOffset.UtcNow;
        return remaining.TotalSeconds > 0 ? (int)remaining.TotalSeconds : null;
    }

    public void RecordFailedAttempt(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return;
        }

        var key = NormalizeIp(ipAddress);
        var now = DateTimeOffset.UtcNow;

        _attempts.AddOrUpdate(key,
            _ => new LoginAttemptRecord { FailedCount = 1, LastAttempt = now },
            (_, existing) =>
            {
                existing.FailedCount++;
                existing.LastAttempt = now;
                return existing;
            });
    }

    public void Reset(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return;
        }

        var key = NormalizeIp(ipAddress);
        _attempts.TryRemove(key, out _);
    }

    public int GetFailedCount(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return 0;
        }

        var key = NormalizeIp(ipAddress);
        return _attempts.TryGetValue(key, out var record) ? record.FailedCount : 0;
    }

    private static string NormalizeIp(string ip)
    {
        return ip.Trim().ToLowerInvariant();
    }

    private void CleanupExpired(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _attempts.ToArray())
        {
            var record = entry.Value;
            // 清理超过1小时的记录
            if (now - record.LastAttempt > TimeSpan.FromHours(1))
            {
                _attempts.TryRemove(entry.Key, out _);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }

    private sealed class LoginAttemptRecord
    {
        public int FailedCount { get; set; }
        public DateTimeOffset LastAttempt { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }
}
