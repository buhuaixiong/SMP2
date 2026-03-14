using Microsoft.EntityFrameworkCore;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Startup;

public sealed class AdminBootstrapStartupGuard
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminBootstrapStartupGuard> _logger;

    public AdminBootstrapStartupGuard(
        SupplierSystemDbContext dbContext,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<AdminBootstrapStartupGuard> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (!IsGuardEnabled())
        {
            _logger.LogInformation("Startup admin bootstrap guard skipped. Environment: {Environment}", _environment.EnvironmentName);
            return;
        }

        var hasActiveAdmin = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user =>
                    user.Role != null &&
                    user.Role.ToLower() == "admin" &&
                    (user.Status == null ||
                     (user.Status.ToLower() != "deleted" && user.Status.ToLower() != "frozen")),
                cancellationToken);

        if (hasActiveAdmin)
        {
            _logger.LogInformation("Startup admin bootstrap guard passed: active admin account exists.");
            return;
        }

        throw new InvalidOperationException(
            "Startup guard blocked startup because no active admin account was found. " +
            "Create an admin account before exposing this service.");
    }

    private bool IsGuardEnabled()
    {
        var configured = _configuration.GetValue<bool?>("StartupGuard:RequireInitializedAdmin");
        if (configured.HasValue)
        {
            return configured.Value;
        }

        var envValue = Environment.GetEnvironmentVariable("STARTUP_GUARD_REQUIRE_INITIALIZED_ADMIN");
        if (TryParseSwitch(envValue, out var parsed))
        {
            return parsed;
        }

        return _environment.IsProduction();
    }

    private static bool TryParseSwitch(string? value, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "1", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(trimmed, "0", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        return bool.TryParse(trimmed, out result);
    }
}

