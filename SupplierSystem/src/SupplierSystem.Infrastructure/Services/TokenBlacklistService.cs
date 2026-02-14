using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Infrastructure.Services;

public sealed class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, TokenCacheEntry> _cache = new();
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly ILogger<TokenBlacklistService> _logger;
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);

    public TokenBlacklistService(
        IServiceScopeFactory scopeFactory,
        ILogger<TokenBlacklistService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<string?> GetReasonAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = HashToken(token);
        if (_cache.TryGetValue(hash, out var cached) && IsNotExpired(cached.ExpiresAt))
        {
            return cached.Reason;
        }

        _cache.TryRemove(hash, out _);

        var now = DateTimeOffset.UtcNow.ToString("o");
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupplierSystemDbContext>();
        var entry = await dbContext.TokenBlacklist
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == hash && string.Compare(x.ExpiresAt, now) > 0)
            .ConfigureAwait(false);

        if (entry == null)
        {
            return null;
        }

        _cache.TryAdd(hash, new TokenCacheEntry(entry.ExpiresAt, entry.Reason));
        return entry.Reason;
    }

    public async Task<bool> IsBlacklistedAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var hash = HashToken(token);
        if (_cache.TryGetValue(hash, out var cached) && IsNotExpired(cached.ExpiresAt))
        {
            return true;
        }

        _cache.TryRemove(hash, out _);

        var now = DateTimeOffset.UtcNow.ToString("o");
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupplierSystemDbContext>();
        var entry = await dbContext.TokenBlacklist
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == hash && string.Compare(x.ExpiresAt, now) > 0)
            .ConfigureAwait(false);

        if (entry != null)
        {
            _cache.TryAdd(hash, new TokenCacheEntry(entry.ExpiresAt, entry.Reason));
            return true;
        }

        return false;
    }

    public async Task<bool> AddAsync(string token, string? userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var jwt = _tokenHandler.ReadJwtToken(token);
        var exp = jwt.Payload.Expiration;
        if (!exp.HasValue)
        {
            return false;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp.Value).ToString("o");
        var now = DateTimeOffset.UtcNow.ToString("o");
        var tokenHash = HashToken(token);

        var entry = new TokenBlacklistEntry
        {
            TokenHash = tokenHash,
            UserId = userId,
            BlacklistedAt = now,
            ExpiresAt = expiresAt,
            Reason = reason,
        };

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupplierSystemDbContext>();
        dbContext.TokenBlacklist.Add(entry);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        _cache[tokenHash] = new TokenCacheEntry(expiresAt, reason);
        return true;
    }

    public async Task<int> RemoveExpiredAsync()
    {
        var lockTaken = false;
        try
        {
            await _cleanupLock.WaitAsync().ConfigureAwait(false);
            lockTaken = true;
            var now = DateTimeOffset.UtcNow.ToString("o");
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SupplierSystemDbContext>();
            var expired = await dbContext.TokenBlacklist
                .Where(x => string.Compare(x.ExpiresAt, now) <= 0)
                .ToListAsync()
                .ConfigureAwait(false);

            if (expired.Count == 0)
            {
                return 0;
            }

            foreach (var entry in expired)
            {
                _cache.TryRemove(entry.TokenHash, out _);
            }

            dbContext.TokenBlacklist.RemoveRange(expired);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            _logger.LogInformation("Removed {Count} expired tokens from blacklist", expired.Count);
            return expired.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing expired tokens from blacklist");
            return 0;
        }
        finally
        {
            if (lockTaken)
            {
                _cleanupLock.Release();
            }
        }
    }

    public async Task<bool> AddTokenHashAsync(string tokenHash, string userId, string expiresAt, string reason)
    {
        if (string.IsNullOrWhiteSpace(tokenHash) || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var entry = new TokenBlacklistEntry
        {
            TokenHash = tokenHash,
            UserId = userId,
            BlacklistedAt = DateTimeOffset.UtcNow.ToString("o"),
            ExpiresAt = expiresAt,
            Reason = reason,
        };

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupplierSystemDbContext>();
        dbContext.TokenBlacklist.Add(entry);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        _cache[tokenHash] = new TokenCacheEntry(expiresAt, reason);
        return true;
    }

    private static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool IsNotExpired(string expiresAt)
    {
        if (!DateTimeOffset.TryParse(expiresAt, out var parsed))
        {
            return false;
        }

        return parsed > DateTimeOffset.UtcNow;
    }

    private sealed record TokenCacheEntry(string ExpiresAt, string? Reason);
}
