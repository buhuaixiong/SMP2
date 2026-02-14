using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed record PublicRfqPreviewInfo(
    long Id,
    string? Title,
    string? Description,
    string? DeliveryPeriod,
    decimal? BudgetAmount,
    string? Currency,
    string? ValidUntil,
    bool IsExpired,
    string InviterName);

public sealed record PublicRfqPreviewResult(
    bool IsRegistered,
    PublicRfqPreviewInfo RfqPreview,
    string? RecipientEmail,
    PublicRfqSupplierInfo? SupplierInfo,
    string Message);

public sealed record PublicRfqSupplierInfo(int SupplierId, string? CompanyName);

public sealed record PublicAutoLoginUser(string Id, string Name, string Role, int? SupplierId);

public sealed record PublicAutoLoginResult(string Token, PublicAutoLoginUser User, long RfqId);

public sealed class PublicRfqService
{
    private const string DefaultInviterName = "Purchaser";
    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ISessionService _sessionService;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public PublicRfqService(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        ISessionService sessionService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _sessionService = sessionService;
        _jwtSettings = BuildJwtSettings(configuration);
        _jwtSettings.Secret = ResolveSecret(_jwtSettings.Secret, configuration["JWT_SECRET"]);
    }

    public async Task<PublicRfqPreviewResult> GetPreviewAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var trimmedToken = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedToken))
        {
            throw new HttpResponseException(404, new { message = "Invalid invitation token.", error = "INVALID_TOKEN" });
        }

        var invitation = await _dbContext.SupplierRfqInvitations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvitationToken == trimmedToken, cancellationToken);

        if (invitation == null)
        {
            throw new HttpResponseException(404, new { message = "Invalid invitation token.", error = "INVALID_TOKEN" });
        }

        if (IsTokenExpired(invitation.TokenExpiresAt))
        {
            throw new HttpResponseException(403, new
            {
                message = "Invitation link has expired.",
                error = "TOKEN_EXPIRED",
                expiredAt = invitation.TokenExpiresAt
            });
        }

        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == invitation.RfqId, cancellationToken);

        if (rfq == null)
        {
            throw new HttpResponseException(404, new { message = "Invalid invitation token.", error = "INVALID_TOKEN" });
        }

        var inviterName = await ResolveInviterNameAsync(rfq.CreatedBy, cancellationToken);
        var isExpired = IsTokenExpired(rfq.ValidUntil);
        var isRegistered = invitation.SupplierId.HasValue && !invitation.IsExternal;

        Supplier? supplier = null;
        if (invitation.SupplierId.HasValue)
        {
            supplier = await _dbContext.Suppliers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == invitation.SupplierId.Value, cancellationToken);
        }

        var message = isRegistered
            ? "You are already registered. Please log in to view the full RFQ."
            : "Complete registration to view the full RFQ and submit a quote.";

        var preview = new PublicRfqPreviewInfo(
            rfq.Id,
            rfq.Title,
            rfq.Description,
            rfq.DeliveryPeriod,
            rfq.BudgetAmount,
            rfq.Currency,
            rfq.ValidUntil,
            isExpired,
            string.IsNullOrWhiteSpace(inviterName) ? DefaultInviterName : inviterName);

        var supplierInfo = isRegistered && invitation.SupplierId.HasValue
            ? new PublicRfqSupplierInfo(invitation.SupplierId.Value, supplier?.CompanyName)
            : null;

        return new PublicRfqPreviewResult(
            isRegistered,
            preview,
            invitation.RecipientEmail,
            supplierInfo,
            message);
    }

    public async Task<PublicAutoLoginResult> AutoLoginAsync(
        string token,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var trimmedToken = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedToken))
        {
            throw new HttpResponseException(404, new { message = "Invalid login token.", error = "INVALID_TOKEN" });
        }

        var invitation = await _dbContext.SupplierRfqInvitations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvitationToken == trimmedToken && !i.IsExternal, cancellationToken);

        if (invitation == null)
        {
            throw new HttpResponseException(404, new { message = "Invalid login token.", error = "INVALID_TOKEN" });
        }

        if (IsTokenExpired(invitation.TokenExpiresAt))
        {
            throw new HttpResponseException(403, new
            {
                message = "Login link has expired.",
                error = "TOKEN_EXPIRED"
            });
        }

        if (!invitation.SupplierId.HasValue)
        {
            throw new HttpResponseException(400, new { message = "Associated user not found.", error = "USER_NOT_FOUND" });
        }

        var user = await _dbContext.Users.AsNoTracking()
            .Where(u => u.SupplierId == invitation.SupplierId.Value)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            throw new HttpResponseException(400, new { message = "Associated user not found.", error = "USER_NOT_FOUND" });
        }

        var tokenTtl = TimeSpan.FromHours(1);
        var issuedToken = IssueToken(user, invitation.RfqId, tokenTtl);

        await _sessionService.RegisterAsync(issuedToken, user.Id, ipAddress, userAgent);

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = user.Id,
            ActorName = user.Name,
            EntityType = "auth",
            EntityId = user.Id,
            Action = "auto_login_via_email_link",
            Changes = JsonSerializer.Serialize(new
            {
                rfqId = invitation.RfqId,
                token = $"{trimmedToken[..Math.Min(8, trimmedToken.Length)]}..."
            }),
            IpAddress = ipAddress
        });

        var responseUser = new PublicAutoLoginUser(
            user.Id,
            user.Name,
            user.Role,
            user.SupplierId);

        return new PublicAutoLoginResult(issuedToken, responseUser, invitation.RfqId);
    }

    private async Task<string?> ResolveInviterNameAsync(string? inviterId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inviterId))
        {
            return null;
        }

        var trimmed = inviterId.Trim();
        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == trimmed, cancellationToken);

        return user?.Name;
    }

    private string IssueToken(User user, long rfqId, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(_jwtSettings.Secret) || _jwtSettings.Secret.Trim().Length < 32)
        {
            throw new HttpResponseException(500, new { message = "JWT secret is missing or too short." });
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret.Trim()));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTimeOffset.UtcNow.Add(ttl);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim("role", user.Role),
            new Claim("autoLogin", "true"),
            new Claim("rfqId", rfqId.ToString(CultureInfo.InvariantCulture)),
        };

        if (user.SupplierId.HasValue)
        {
            claims.Add(new Claim("supplierId", user.SupplierId.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrWhiteSpace(user.TenantId))
        {
            claims.Add(new Claim("tenantId", user.TenantId));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return _tokenHandler.WriteToken(token);
    }

    private static bool IsTokenExpired(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            if (!DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out parsed))
            {
                return false;
            }
        }

        return parsed.UtcDateTime < DateTimeOffset.UtcNow.UtcDateTime;
    }

    private static string ResolveSecret(string? configured, string? envValue)
    {
        if (!string.IsNullOrWhiteSpace(configured) && !IsPlaceholder(configured))
        {
            return configured;
        }

        return envValue ?? string.Empty;
    }

    private static bool IsPlaceholder(string secret)
    {
        var trimmed = secret.Trim();
        if (trimmed.Equals("${JWT_SECRET}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return trimmed.StartsWith("${", StringComparison.OrdinalIgnoreCase) &&
               trimmed.EndsWith("}", StringComparison.OrdinalIgnoreCase);
    }

    private static JwtSettings BuildJwtSettings(IConfiguration configuration)
    {
        var settings = new JwtSettings();

        var secret = configuration["JwtSettings:Secret"];
        if (!string.IsNullOrWhiteSpace(secret))
        {
            settings.Secret = secret;
        }

        var issuer = configuration["JwtSettings:Issuer"];
        if (!string.IsNullOrWhiteSpace(issuer))
        {
            settings.Issuer = issuer;
        }

        var audience = configuration["JwtSettings:Audience"];
        if (!string.IsNullOrWhiteSpace(audience))
        {
            settings.Audience = audience;
        }

        var expiresIn = configuration["JwtSettings:ExpiresIn"];
        if (!string.IsNullOrWhiteSpace(expiresIn))
        {
            settings.ExpiresIn = expiresIn;
        }

        return settings;
    }
}
