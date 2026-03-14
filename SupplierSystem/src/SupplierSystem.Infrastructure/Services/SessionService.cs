using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Infrastructure.Services;

public sealed class SessionService : ISessionService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly ITokenBlacklistService _tokenBlacklistService;
    private readonly IAuditService _auditService;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        SupplierSystemDbContext dbContext,
        ITokenBlacklistService tokenBlacklistService,
        IAuditService auditService,
        ILogger<SessionService> logger)
    {
        _dbContext = dbContext;
        _tokenBlacklistService = tokenBlacklistService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task RegisterAsync(string token, string userId, string? ipAddress, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var jwt = _tokenHandler.ReadJwtToken(token);
        var exp = jwt.Payload.Expiration;
        if (!exp.HasValue)
        {
            return;
        }

        var tokenHash = HashToken(token);
        var issuedAt = DateTimeOffset.UtcNow.ToString("o");
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp.Value).ToString("o");

        _dbContext.ActiveSessions.Add(new ActiveSession
        {
            UserId = userId,
            TokenHash = tokenHash,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = issuedAt,
        });

        await _dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<SessionInvalidationResult> InvalidateUserSessionsAsync(
        string userId,
        string reason,
        string? currentIp,
        string? currentUserAgent)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new SessionInvalidationResult();
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var sessions = await _dbContext.ActiveSessions
            .Where(s => s.UserId == userId && string.Compare(s.ExpiresAt, now) > 0)
            .ToListAsync()
            .ConfigureAwait(false);

        if (sessions.Count == 0)
        {
            return new SessionInvalidationResult();
        }

        var invalidated = 0;
        var skipped = 0;
        var toRemove = new List<ActiveSession>();

        foreach (var session in sessions)
        {
            if (ShouldInvalidateSession(currentIp, currentUserAgent, session.IpAddress, session.UserAgent))
            {
                await _tokenBlacklistService.AddTokenHashAsync(
                    session.TokenHash,
                    userId,
                    session.ExpiresAt,
                    reason).ConfigureAwait(false);

                invalidated++;
                toRemove.Add(session);
            }
            else
            {
                skipped++;
            }
        }

        if (toRemove.Count > 0)
        {
            _dbContext.ActiveSessions.RemoveRange(toRemove);
            await _dbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        if (invalidated > 0)
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = userId,
                ActorName = userId,
                EntityType = "session",
                EntityId = userId,
                Action = "sessions_invalidated",
                Changes = System.Text.Json.JsonSerializer.Serialize(new
                {
                    invalidated,
                    skipped,
                    reason,
                    currentIp,
                }),
            }).ConfigureAwait(false);
        }

        return new SessionInvalidationResult
        {
            Invalidated = invalidated,
            Skipped = skipped,
            Total = sessions.Count,
        };
    }

    public async Task<SessionInvalidationResult> InvalidateAllSessionsAsync(string userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new SessionInvalidationResult();
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var sessions = await _dbContext.ActiveSessions
            .Where(s => s.UserId == userId && string.Compare(s.ExpiresAt, now) > 0)
            .ToListAsync()
            .ConfigureAwait(false);

        if (sessions.Count == 0)
        {
            return new SessionInvalidationResult();
        }

        var invalidated = 0;
        foreach (var session in sessions)
        {
            var added = await _tokenBlacklistService.AddTokenHashAsync(
                session.TokenHash,
                userId,
                session.ExpiresAt,
                reason).ConfigureAwait(false);

            if (added)
            {
                invalidated++;
            }
        }

        _dbContext.ActiveSessions.RemoveRange(sessions);
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);

        if (invalidated > 0)
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = userId,
                ActorName = userId,
                EntityType = "session",
                EntityId = userId,
                Action = "sessions_revoked",
                Changes = System.Text.Json.JsonSerializer.Serialize(new
                {
                    invalidated,
                    reason,
                }),
            }).ConfigureAwait(false);
        }

        return new SessionInvalidationResult
        {
            Invalidated = invalidated,
            Skipped = sessions.Count - invalidated,
            Total = sessions.Count,
        };
    }

    public async Task<bool> RemoveAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var hash = HashToken(token);
        var session = await _dbContext.ActiveSessions
            .FirstOrDefaultAsync(s => s.TokenHash == hash)
            .ConfigureAwait(false);

        if (session == null)
        {
            return false;
        }

        _dbContext.ActiveSessions.Remove(session);
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }

    public async Task<int> CountActiveAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        return await _dbContext.ActiveSessions
            .CountAsync(s => s.UserId == userId && string.Compare(s.ExpiresAt, now) > 0)
            .ConfigureAwait(false);
    }

    public async Task<int> InvalidateAllOtherSessionsAsync(string userId, string currentTokenHash)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(currentTokenHash))
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var sessions = await _dbContext.ActiveSessions
            .Where(s => s.UserId == userId &&
                        s.TokenHash != currentTokenHash &&
                        string.Compare(s.ExpiresAt, now) > 0)
            .ToListAsync()
            .ConfigureAwait(false);

        if (sessions.Count == 0)
        {
            return 0;
        }

        var invalidated = 0;
        foreach (var session in sessions)
        {
            var added = await _tokenBlacklistService.AddTokenHashAsync(
                session.TokenHash,
                userId,
                session.ExpiresAt,
                "user_logout_others").ConfigureAwait(false);

            if (added)
            {
                invalidated++;
            }
        }

        _dbContext.ActiveSessions.RemoveRange(sessions);
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);

        if (invalidated > 0)
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = userId,
                ActorName = userId,
                EntityType = "session",
                EntityId = userId,
                Action = "logout_other_devices",
                Changes = System.Text.Json.JsonSerializer.Serialize(new { invalidatedCount = invalidated }),
            }).ConfigureAwait(false);
        }

        return invalidated;
    }

    public string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool ShouldInvalidateSession(string? currentIp, string? currentUa, string? sessionIp, string? sessionUa)
    {
        if (string.IsNullOrWhiteSpace(currentIp) || string.IsNullOrWhiteSpace(currentUa))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sessionIp) || string.IsNullOrWhiteSpace(sessionUa))
        {
            return false;
        }

        var ipDifferent = !string.Equals(currentIp, sessionIp, StringComparison.OrdinalIgnoreCase);
        var uaDifferent = !string.Equals(currentUa, sessionUa, StringComparison.OrdinalIgnoreCase);
        return ipDifferent && uaDifferent;
    }
}
