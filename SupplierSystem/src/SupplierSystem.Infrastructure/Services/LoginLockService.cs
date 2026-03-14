using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Interfaces;

namespace SupplierSystem.Infrastructure.Services;

public sealed class LoginLockService : ILoginLockService
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _locks = new();
    private readonly ILogger<LoginLockService> _logger;

    public LoginLockService(ILogger<LoginLockService> logger)
    {
        _logger = logger;
    }

    public bool TryAcquire(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        if (_locks.TryGetValue(userId, out var existing) && now - existing < LockDuration)
        {
            return false;
        }

        _locks[userId] = now;
        return true;
    }

    public void Release(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        _locks.TryRemove(userId, out _);
    }
}
