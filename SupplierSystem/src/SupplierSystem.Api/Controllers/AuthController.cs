using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

/// <summary>
/// 认证控制器 - 继承统一基类
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAccountLockoutService _lockoutService;
    private readonly ISessionService _sessionService;
    private readonly SupplierSystemDbContext _dbContext;

    public AuthController(
        IAuthService authService,
        IAccountLockoutService lockoutService,
        ISessionService sessionService,
        SupplierSystemDbContext dbContext)
    {
        _authService = authService;
        _lockoutService = lockoutService;
        _sessionService = sessionService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// User login endpoint
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>Authentication response with JWT token</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(object), StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        var response = await _authService.LoginAsync(request, ip, userAgent);
        return Success(response);
    }

    /// <summary>
    /// Get current authenticated user information
    /// </summary>
    /// <returns>Current user data</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        return Success(new { user });
    }

    /// <summary>
    /// Change user password
    /// </summary>
    /// <param name="request">Password change request</param>
    /// <returns>Success response</returns>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        try
        {
            await _authService.ChangePasswordAsync(user.Id, request);
            return Success(null, "Password changed successfully.");
        }
        catch (ServiceException ex) when (ex.StatusCode == 400)
        {
            return BadRequest(ex.Message, ex.ErrorCode);
        }
    }

    /// <summary>
    /// Logout current session and invalidate token
    /// </summary>
    /// <returns>No content</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        var token = Request.Headers.Authorization.ToString();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Bearer ".Length..].Trim();
        }

        await _authService.LogoutAsync(token, user);
        return NoContent();
    }

    /// <summary>
    /// Get account lockout statistics (admin only)
    /// </summary>
    /// <returns>Lockout statistics</returns>
    [HttpGet("lockout-stats")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public IActionResult GetLockoutStats()
    {
        var user = RequireAuthenticatedAdmin(out var errorResult);
        if (errorResult != null) return errorResult;

        return Success(_lockoutService.GetStats());
    }

    /// <summary>
    /// Get token blacklist statistics (admin only)
    /// </summary>
    /// <param name="details">Include detailed token information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Blacklist statistics</returns>
    [HttpGet("blacklist-stats")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBlacklistStats([FromQuery] bool details, CancellationToken cancellationToken)
    {
        var user = RequireAuthenticatedAdmin(out var errorResult);
        if (errorResult != null) return errorResult;

        var now = DateTimeOffset.UtcNow.ToString("o");
        var total = await _dbContext.TokenBlacklist.CountAsync(cancellationToken);
        var active = await _dbContext.TokenBlacklist.CountAsync(
            entry => string.Compare(entry.ExpiresAt, now) > 0,
            cancellationToken);
        var expired = await _dbContext.TokenBlacklist.CountAsync(
            entry => string.Compare(entry.ExpiresAt, now) <= 0,
            cancellationToken);

        var reasonStats = await _dbContext.TokenBlacklist.AsNoTracking()
            .Where(entry => string.Compare(entry.ExpiresAt, now) > 0)
            .GroupBy(entry => entry.Reason ?? "unknown")
            .Select(group => new { reason = group.Key, count = group.Count() })
            .ToListAsync(cancellationToken);

        var stats = new Dictionary<string, object?>
        {
            ["memory"] = new { total = active },
            ["database"] = new { total, active, expired },
            ["byReason"] = reasonStats.ToDictionary(item => item.reason, item => item.count)
        };

        if (details)
        {
            var recentTokens = await _dbContext.TokenBlacklist.AsNoTracking()
                .Where(entry => string.Compare(entry.ExpiresAt, now) > 0)
                .OrderByDescending(entry => entry.BlacklistedAt)
                .Take(50)
                .Select(entry => new
                {
                    tokenHashPrefix = entry.TokenHash.Length > 16
                        ? entry.TokenHash.Substring(0, 16) + "..."
                        : entry.TokenHash,
                    userId = entry.UserId,
                    blacklistedAt = entry.BlacklistedAt,
                    expiresAt = entry.ExpiresAt,
                    reason = entry.Reason
                })
                .ToListAsync(cancellationToken);

            stats["recentTokens"] = recentTokens;
        }

        return Success(stats);
    }

    /// <summary>
    /// 验证用户是否为管理员，不是则返回 Forbid 结果
    /// </summary>
    private IActionResult? RequireAdmin(AuthUser user)
    {
        return string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase)
            ? null
            : Forbid();
    }

    /// <summary>
    /// 验证用户已认证且为管理员，返回用户对象
    /// </summary>
    private AuthUser? RequireAuthenticatedAdmin(out IActionResult? errorResult)
    {
        errorResult = null;
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            errorResult = Unauthorized();
            return null;
        }
        var adminResult = RequireAdmin(user);
        if (adminResult != null)
        {
            errorResult = adminResult;
            return null;
        }
        return user;
    }

    /// <summary>
    /// 获取当前认证的用户，如果未认证则返回错误响应
    /// </summary>
    private AuthUser? GetAuthenticatedUser(out IActionResult? errorResult)
    {
        errorResult = null;
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            errorResult = Unauthorized();
            return null;
        }
        return user;
    }

    /// <summary>
    /// Revoke all tokens for a specific user (admin only)
    /// </summary>
    /// <param name="userId">Target user ID</param>
    /// <param name="request">Revoke request with optional reason</param>
    /// <returns>Revocation result</returns>
    [HttpPost("revoke-user-tokens/{userId}")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeUserTokens(string userId, [FromBody] RevokeUserTokensRequest? request)
    {
        var adminUser = RequireAuthenticatedAdmin(out var errorResult);
        if (errorResult != null) return errorResult;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest("User ID is required.");
        }

        var reason = string.IsNullOrWhiteSpace(request?.Reason) ? "admin_revoke" : request.Reason.Trim();
        var result = await _sessionService.InvalidateAllSessionsAsync(userId, reason);

        return Success(new
        {
            message = $"Revoked {result.Invalidated} token(s) for user {userId}",
            count = result.Invalidated,
            reason
        });
    }

    /// <summary>
    /// Unlock a locked user account (admin only)
    /// </summary>
    /// <param name="username">Username to unlock</param>
    /// <returns>Unlock result</returns>
    [HttpPost("unlock-account/{username}")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public IActionResult UnlockAccount(string username)
    {
        var adminUser = RequireAuthenticatedAdmin(out var errorResult);
        if (errorResult != null) return errorResult;

        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Username is required.");
        }

        var wasLocked = _lockoutService.Unlock(username, adminUser!.Id);
        if (!wasLocked)
        {
            return NotFound($"Account '{username}' was not locked or does not exist.");
        }

        return Success(new
        {
            username,
            unlockedBy = adminUser!.Name,
            message = $"Account '{username}' has been unlocked successfully."
        });
    }

    /// <summary>
    /// Logout from all other devices except current session
    /// </summary>
    /// <returns>Logout result</returns>
    [HttpPost("logout-other-devices")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutOtherDevices()
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        var token = Request.Headers.Authorization.ToString();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest("No valid session found.");
        }

        var currentTokenHash = _sessionService.HashToken(token);
        var invalidatedCount = await _sessionService.InvalidateAllOtherSessionsAsync(user.Id, currentTokenHash);

        return Success(new
        {
            invalidatedCount,
            remainingSessions = 1,
            message = invalidatedCount > 0
                ? $"Successfully logged out {invalidatedCount} other device(s)."
                : "No other active sessions found."
        });
    }

    /// <summary>
    /// Get external supplier invitation details by token
    /// </summary>
    /// <param name="token">Invitation token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Invitation details</returns>
    [HttpGet("invitation/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvitation(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest("Invitation token is required.");
        }

        var invitation = await (from entry in _dbContext.ExternalSupplierInvitations.AsNoTracking()
                                join rfq in _dbContext.Rfqs.AsNoTracking() on (long)entry.RfqId equals rfq.Id
                                where entry.InvitationToken == token
                                select new
                                {
                                    entry.Email,
                                    entry.CompanyName,
                                    entry.ContactPerson,
                                    entry.RfqId,
                                    entry.ExpiresAt,
                                    entry.RegistrationCompleted,
                                    rfqTitle = rfq.Title,
                                    rfqValidUntil = rfq.ValidUntil,
                                }).FirstOrDefaultAsync(cancellationToken);

        if (invitation == null)
        {
            return NotFound("Invalid invitation token.");
        }

        if (!string.IsNullOrWhiteSpace(invitation.ExpiresAt)
            && DateTimeOffset.TryParse(invitation.ExpiresAt, out var expiresAt)
            && DateTimeOffset.UtcNow > expiresAt)
        {
            return BadRequest("Invitation has expired.");
        }

        if (invitation.RegistrationCompleted == true)
        {
            return BadRequest("This invitation has already been used.");
        }

        return Success(new
        {
            valid = true,
            data = new
            {
                email = invitation.Email,
                companyName = invitation.CompanyName,
                contactPerson = invitation.ContactPerson,
                rfqId = invitation.RfqId,
                rfqTitle = invitation.rfqTitle,
                rfqValidUntil = invitation.rfqValidUntil,
                expiresAt = invitation.ExpiresAt
            }
        });
    }

    public sealed class RevokeUserTokensRequest
    {
        public string? Reason { get; set; }
    }
}
