using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using Konscious.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuthRepository _authRepository;
    private readonly IAuthPayloadService _payloadService;
    private readonly ITokenBlacklistService _tokenBlacklist;
    private readonly IAccountLockoutService _lockoutService;
    private readonly ILoginLockService _loginLock;
    private readonly ISessionService _sessionService;
    private readonly IAuditService _auditService;
    private readonly IRateLimitService _rateLimitService;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly bool _allowLegacyMd5;
    private readonly ILogger<AuthService> _logger;
    private const string LoginEndpoint = "login";
    private const int MaxLoginAttemptsPerMinute = 10;
    private const int Argon2MemoryKb = 65536;
    private const int Argon2Iterations = 3;
    private const int Argon2Parallelism = 2;
    private const int Argon2SaltSize = 16;
    private const int Argon2HashSize = 32;

    /// <summary>
    /// 支持的密码哈希算法类型
    /// </summary>
    private enum HashAlgorithmType
    {
        Unknown = 0,
        Argon2id = 1,
        Bcrypt = 2,
        LegacyMd5 = 3
    }

    public AuthService(
        SupplierSystemDbContext dbContext,
        IAuthRepository authRepository,
        IAuthPayloadService payloadService,
        ITokenBlacklistService tokenBlacklist,
        IAccountLockoutService lockoutService,
        ILoginLockService loginLock,
        ISessionService sessionService,
        IAuditService auditService,
        IRateLimitService rateLimitService,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _authRepository = authRepository;
        _payloadService = payloadService;
        _tokenBlacklist = tokenBlacklist;
        _lockoutService = lockoutService;
        _loginLock = loginLock;
        _sessionService = sessionService;
        _auditService = auditService;
        _rateLimitService = rateLimitService;
        _jwtSettings = BuildJwtSettings(configuration);
        _jwtSettings.Secret = ResolveSecret(_jwtSettings.Secret, configuration["JWT_SECRET"]);
        _allowLegacyMd5 = environment.IsDevelopment();
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new HttpResponseException(400, new { message = "username and password are required" });
        }

        // 速率限制 - 检查 IP 级别的请求频率
        if (_rateLimitService.ShouldBlock(ipAddress, LoginEndpoint))
        {
            _logger.LogWarning("Login rate limit exceeded for IP: {IpAddress}", ipAddress);
            throw new HttpResponseException(429, new
            {
                message = "Too many login attempts. Please try again later.",
                code = "RATE_LIMIT_EXCEEDED"
            });
        }

        _rateLimitService.RecordRequest(ipAddress, LoginEndpoint);

        var identifier = request.Username.Trim();
        var lockout = _lockoutService.Check(identifier);
        if (lockout.Locked)
        {
            _logger.LogWarning("Account locked for user: {Username}, remaining minutes: {RemainingMinutes}",
                identifier, lockout.RemainingTimeMinutes);
            throw new HttpResponseException(423, new
            {
                message = $"Account is temporarily locked due to too many failed login attempts. Please try again in {lockout.RemainingTimeMinutes} minute(s).",
                locked = true,
                remainingMinutes = lockout.RemainingTimeMinutes,
                retryAfter = lockout.RemainingTimeMinutes * 60
            });
        }

        var user = await _authRepository.FindByIdentifierAsync(identifier, CancellationToken.None).ConfigureAwait(false);
        if (user == null)
        {
            _logger.LogWarning("Login failed - user not found: {Username}, IP: {IpAddress}", identifier, ipAddress);
            _lockoutService.RecordFailedAttempt(identifier, ipAddress);
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = identifier,
                ActorName = identifier,
                EntityType = "auth",
                EntityId = identifier,
                Action = "login_failed",
                Changes = System.Text.Json.JsonSerializer.Serialize(new { reason = "invalid_credentials" }),
                IpAddress = ipAddress,
            }).ConfigureAwait(false);

            throw new HttpResponseException(401, new { message = "Invalid credentials" });
        }

        if (!_loginLock.TryAcquire(user.Id))
        {
            _logger.LogWarning("Login already in progress for user: {UserId}", user.Id);
            throw new HttpResponseException(429, new
            {
                message = "Login request is being processed. Please wait a moment.",
                code = "LOGIN_IN_PROGRESS"
            });
        }

        try
        {
            if (!VerifyPassword(request.Password, user, out var needsUpgrade))
            {
                var attempt = _lockoutService.RecordFailedAttempt(identifier, ipAddress);

                await _auditService.LogAsync(new AuditEntry
                {
                    ActorId = user.Id,
                    ActorName = user.Name,
                    EntityType = "auth",
                    EntityId = user.Id,
                    Action = "login_failed",
                    Changes = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        reason = "invalid_password",
                        remainingAttempts = attempt.RemainingAttempts,
                        willLockAfter = attempt.Locked ? 0 : attempt.RemainingAttempts,
                    }),
                    IpAddress = ipAddress,
                }).ConfigureAwait(false);

                if (attempt.Locked)
                {
                    _logger.LogWarning("Account locked due to too many failed attempts: {Username}, IP: {IpAddress}",
                        identifier, ipAddress);
                    throw new HttpResponseException(423, new
                    {
                        message = "Account has been locked due to too many failed login attempts. Please try again in 30 minutes.",
                        locked = true,
                        lockedUntil = attempt.LockedUntil?.ToString("o"),
                    });
                }

                _logger.LogWarning("Invalid password for user: {Username}, remaining attempts: {RemainingAttempts}",
                    identifier, attempt.RemainingAttempts);
                throw new HttpResponseException(401, new
                {
                    message = "Invalid credentials",
                    remainingAttempts = attempt.RemainingAttempts,
                    warning = attempt.RemainingAttempts <= 2
                        ? $"Warning: Account will be locked after {attempt.RemainingAttempts} more failed attempt(s)"
                        : null,
                });
            }

            if (string.Equals(user.Status, "frozen", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Login attempt on frozen account: {UserId}", user.Id);
                throw new HttpResponseException(403, new { message = "Account is frozen. Please contact administrator." });
            }

            if (string.Equals(user.Status, "deleted", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Login attempt on deleted account: {UserId}", user.Id);
                throw new HttpResponseException(403, new { message = "Account has been deleted." });
            }

            _lockoutService.Reset(identifier);
            _rateLimitService.Reset(ipAddress, LoginEndpoint);

            string? upgradedPassword = null;
            if (needsUpgrade)
            {
                upgradedPassword = HashPasswordArgon2id(request.Password);
            }

            var nowIso = DateTimeOffset.UtcNow.ToString("o");
            var trackedUser = AttachIfNotTracked(user);
            if (upgradedPassword != null)
            {
                trackedUser.Password = upgradedPassword;
            }

            trackedUser.LastLoginAt = nowIso;
            trackedUser.UpdatedAt = nowIso;
            await _dbContext.SaveChangesAsync().ConfigureAwait(false);
            user = trackedUser;

            var sessionResult = await _sessionService
                .InvalidateUserSessionsAsync(user.Id, "superseded", ipAddress, userAgent)
                .ConfigureAwait(false);

            var token = IssueToken(user);
            await _sessionService.RegisterAsync(token, user.Id, ipAddress, userAgent).ConfigureAwait(false);

            var authUser = await _payloadService.BuildAsync(user.Id).ConfigureAwait(false)
                           ?? throw new HttpResponseException(401, new { message = "User not found or inactive." });

            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = user.Id,
                ActorName = user.Name,
                EntityType = "auth",
                EntityId = user.Id,
                Action = "login",
                IpAddress = ipAddress,
            }).ConfigureAwait(false);

            var activeSessionCount = await _sessionService.CountActiveAsync(user.Id).ConfigureAwait(false);
            var expiresIn = DecodeExpiresIn(token);

            return new LoginResponse
            {
                Token = token,
                ExpiresIn = expiresIn,
                ExpiresInReadable = _jwtSettings.ExpiresIn,
                User = authUser,
                MustChangePassword = user.MustChangePassword,
                ActiveSessionCount = activeSessionCount,
                SessionInfo = sessionResult.Invalidated > 0
                    ? new SessionInfo { Invalidated = sessionResult.Invalidated, Reason = "Different IP and User-Agent detected" }
                    : null,
            };
        }
        finally
        {
            _loginLock.Release(user.Id);
        }
    }

    public async Task<AuthUser?> GetCurrentUserAsync(string userId)
    {
        return await _payloadService.BuildAsync(userId).ConfigureAwait(false);
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new HttpResponseException(400, new { message = "Current password and new password are required." });
        }

        if (request.NewPassword.Length < 8)
        {
            throw new HttpResponseException(400, new { message = "New password must be at least 8 characters." });
        }

        var user = await _authRepository.FindByIdAsync(userId, CancellationToken.None).ConfigureAwait(false);
        if (user == null)
        {
            _logger.LogWarning("Change password failed - user not found: {UserId}", userId);
            throw new HttpResponseException(404, new { message = "User not found." });
        }

        if (!VerifyPassword(request.CurrentPassword, user, out _))
        {
            _logger.LogWarning("Change password failed - invalid current password for user: {UserId}", userId);
            throw new HttpResponseException(401, new { message = "Current password is incorrect." });
        }

        if (VerifyPassword(request.NewPassword, user, out _))
        {
            _logger.LogWarning("Change password failed - new password same as current for user: {UserId}", userId);
            throw new HttpResponseException(400, new { message = "New password must be different from the current password." });
        }

        var trackedUser = AttachIfNotTracked(user);
        trackedUser.Password = HashPasswordArgon2id(request.NewPassword);
        trackedUser.AuthVersion = NextAuthVersion(trackedUser.AuthVersion);
        trackedUser.MustChangePassword = false;
        trackedUser.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);
        user = trackedUser;

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = user.Id,
            ActorName = user.Name,
            EntityType = "auth",
            EntityId = user.Id,
            Action = "change_password",
        }).ConfigureAwait(false);
    }

    public async Task LogoutAsync(string token, AuthUser user)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            await _tokenBlacklist.AddAsync(token, user.Id, "logout").ConfigureAwait(false);
            await _sessionService.RemoveAsync(token).ConfigureAwait(false);
        }

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = user.Id,
            ActorName = user.Name,
            EntityType = "auth",
            EntityId = user.Id,
            Action = "logout",
        }).ConfigureAwait(false);
    }

    private string IssueToken(Domain.Entities.User user)
    {
        if (string.IsNullOrWhiteSpace(_jwtSettings.Secret) || _jwtSettings.Secret.Trim().Length < 32)
        {
            _logger.LogError("JWT secret is missing or too short during token issuance");
            throw new HttpResponseException(500, new { message = "JWT secret is missing or too short." });
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret.Trim()));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTimeOffset.UtcNow.Add(ParseExpiresIn(_jwtSettings.ExpiresIn));

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim("role", user.Role),
                new Claim("supplierId", user.SupplierId?.ToString() ?? string.Empty),
                new Claim("tenantId", user.TenantId ?? string.Empty),
                new Claim("authVersion", user.AuthVersion.ToString(CultureInfo.InvariantCulture)),
            },
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return _tokenHandler.WriteToken(token);
    }

    private Domain.Entities.User AttachIfNotTracked(Domain.Entities.User user)
    {
        var tracked = _dbContext.Users.Local.FirstOrDefault(entity => entity.Id == user.Id);
        if (tracked != null)
        {
            return tracked;
        }

        _dbContext.Users.Attach(user);
        return user;
    }

    private static TimeSpan ParseExpiresIn(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeSpan.FromHours(8);
        }

        var trimmed = value.Trim().ToLowerInvariant();
        if (trimmed.EndsWith("h") && int.TryParse(trimmed[..^1], out var hours))
        {
            return TimeSpan.FromHours(hours);
        }

        if (trimmed.EndsWith("m") && int.TryParse(trimmed[..^1], out var minutes))
        {
            return TimeSpan.FromMinutes(minutes);
        }

        if (trimmed.EndsWith("s") && int.TryParse(trimmed[..^1], out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (trimmed.EndsWith("d") && int.TryParse(trimmed[..^1], out var days))
        {
            return TimeSpan.FromDays(days);
        }

        if (int.TryParse(trimmed, out var fallbackMinutes))
        {
            return TimeSpan.FromMinutes(fallbackMinutes);
        }

        return TimeSpan.FromHours(8);
    }

    private long? DecodeExpiresIn(string token)
    {
        var jwt = _tokenHandler.ReadJwtToken(token);
        return jwt.Payload.Expiration;
    }

    private static string ResolveSecret(string? configured, string? envValue)
    {
        if (!string.IsNullOrWhiteSpace(configured) && !IsPlaceholder(configured))
        {
            return configured;
        }

        return envValue ?? string.Empty;
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

    private bool VerifyPassword(string password, Domain.Entities.User user, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrWhiteSpace(user.Password))
        {
            return false;
        }

        var algorithm = DetectHashAlgorithm(user.Password);
        switch (algorithm)
        {
            case HashAlgorithmType.Argon2id:
                return VerifyArgon2idHash(password, user.Password);

            case HashAlgorithmType.Bcrypt:
                var bcryptValid = BCrypt.Net.BCrypt.Verify(password, user.Password);
                needsUpgrade = bcryptValid;
                return bcryptValid;

            case HashAlgorithmType.LegacyMd5:
                if (!_allowLegacyMd5)
                {
                    return false;
                }
                var md5Valid = VerifyLegacyMd5(password, user.Password);
                needsUpgrade = md5Valid;
                return md5Valid;

            case HashAlgorithmType.Unknown:
            default:
                return false;
        }
    }

    /// <summary>
    /// 检测密码哈希使用的算法类型
    /// </summary>
    private static HashAlgorithmType DetectHashAlgorithm(string hash)
    {
        if (IsArgon2idHash(hash))
            return HashAlgorithmType.Argon2id;

        if (IsBcryptHash(hash))
            return HashAlgorithmType.Bcrypt;

        if (IsLegacyMd5Hash(hash))
            return HashAlgorithmType.LegacyMd5;

        return HashAlgorithmType.Unknown;
    }

    private static bool IsArgon2idHash(string hash)
    {
        return hash.StartsWith("$argon2id$", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBcryptHash(string hash)
    {
        return hash.StartsWith("$2a$", StringComparison.OrdinalIgnoreCase) ||
               hash.StartsWith("$2b$", StringComparison.OrdinalIgnoreCase) ||
               hash.StartsWith("$2y$", StringComparison.OrdinalIgnoreCase) ||
               hash.StartsWith("$2x$", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyMd5Hash(string hash)
    {
        if (hash.Length != 32)
        {
            return false;
        }

        foreach (var ch in hash)
        {
            if (!IsHexDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool VerifyArgon2idHash(string password, string encoded)
    {
        if (!TryParseArgon2idHash(encoded, out var memoryKb, out var iterations, out var parallelism, out var salt, out var hash))
        {
            return false;
        }

        var computed = HashArgon2id(password, salt, memoryKb, iterations, parallelism, hash.Length);
        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }

    private static bool VerifyLegacyMd5(string password, string hash)
    {
        if (!TryDecodeHex(hash, out var expected))
        {
            return false;
        }

        var actual = MD5.HashData(Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string HashPasswordArgon2id(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(Argon2SaltSize);
        var hash = HashArgon2id(password, salt, Argon2MemoryKb, Argon2Iterations, Argon2Parallelism, Argon2HashSize);
        return FormatArgon2idHash(salt, hash, Argon2MemoryKb, Argon2Iterations, Argon2Parallelism);
    }

    private static byte[] HashArgon2id(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int hashLength)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        return argon2.GetBytes(hashLength);
    }

    private static string FormatArgon2idHash(byte[] salt, byte[] hash, int memoryKb, int iterations, int parallelism)
    {
        return $"$argon2id$v=19$m={memoryKb},t={iterations},p={parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool TryParseArgon2idHash(
        string encoded,
        out int memoryKb,
        out int iterations,
        out int parallelism,
        out byte[] salt,
        out byte[] hash)
    {
        memoryKb = 0;
        iterations = 0;
        parallelism = 0;
        salt = Array.Empty<byte>();
        hash = Array.Empty<byte>();

        if (!IsArgon2idHash(encoded))
        {
            return false;
        }

        var parts = encoded.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            return false;
        }

        var parameterPart = parts[2];
        if (!TryParseArgon2Parameters(parameterPart, out memoryKb, out iterations, out parallelism))
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[3]);
            hash = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }

    private static bool TryParseArgon2Parameters(string parameters, out int memoryKb, out int iterations, out int parallelism)
    {
        memoryKb = 0;
        iterations = 0;
        parallelism = 0;

        var segments = parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            if (segment.StartsWith("m=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(segment[2..], out var mValue))
            {
                memoryKb = mValue;
                continue;
            }

            if (segment.StartsWith("t=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(segment[2..], out var tValue))
            {
                iterations = tValue;
                continue;
            }

            if (segment.StartsWith("p=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(segment[2..], out var pValue))
            {
                parallelism = pValue;
            }
        }

        return memoryKb > 0 && iterations > 0 && parallelism > 0;
    }

    private static bool TryDecodeHex(string hex, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (hex.Length % 2 != 0)
        {
            return false;
        }

        bytes = new byte[hex.Length / 2];
        for (var i = 0; i < hex.Length; i += 2)
        {
            if (!TryParseHexByte(hex[i], hex[i + 1], out var value))
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            bytes[i / 2] = value;
        }

        return true;
    }

    private static bool TryParseHexByte(char high, char low, out byte value)
    {
        value = 0;
        var highNibble = ParseHexNibble(high);
        var lowNibble = ParseHexNibble(low);
        if (highNibble < 0 || lowNibble < 0)
        {
            return false;
        }

        value = (byte)((highNibble << 4) | lowNibble);
        return true;
    }

    private static int ParseHexNibble(char ch)
    {
        if (ch >= '0' && ch <= '9')
        {
            return ch - '0';
        }

        if (ch >= 'a' && ch <= 'f')
        {
            return ch - 'a' + 10;
        }

        if (ch >= 'A' && ch <= 'F')
        {
            return ch - 'A' + 10;
        }

        return -1;
    }

    private static bool IsHexDigit(char ch)
    {
        return (ch >= '0' && ch <= '9') ||
               (ch >= 'a' && ch <= 'f') ||
               (ch >= 'A' && ch <= 'F');
    }

    private static int NextAuthVersion(int current)
    {
        return current <= 0 ? 1 : current + 1;
    }
}
