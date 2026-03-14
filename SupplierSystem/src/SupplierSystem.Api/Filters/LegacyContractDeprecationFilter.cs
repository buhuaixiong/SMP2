using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SupplierSystem.Api.Filters;

public sealed class LegacyContractDeprecationFilter : IAsyncActionFilter
{
    private static readonly DateTimeOffset DefaultSunsetUtc = new(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

    private readonly IConfiguration _configuration;
    private readonly ILogger<LegacyContractDeprecationFilter> _logger;

    public LegacyContractDeprecationFilter(
        IConfiguration configuration,
        ILogger<LegacyContractDeprecationFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception != null && !executed.ExceptionHandled)
        {
            return;
        }

        if (!IsLegacyDeprecationEnabled())
        {
            return;
        }

        var response = context.HttpContext.Response;
        if (response.HasStarted)
        {
            return;
        }

        var sunsetUtc = ResolveLegacyContractSunsetUtc();
        var successorBasePath = ResolveSuccessorBasePath();

        response.Headers["Deprecation"] = "true";
        response.Headers["Sunset"] = sunsetUtc.ToString("R", CultureInfo.InvariantCulture);
        response.Headers["X-Legacy-Contract"] = "api/rfq";
        if (!string.IsNullOrWhiteSpace(successorBasePath))
        {
            response.Headers["Link"] = $"<{successorBasePath}>; rel=\"successor-version\"";
        }

        _logger.LogWarning(
            "Legacy API contract hit: {Path}. Deprecation headers emitted with sunset at {SunsetUtc}.",
            context.HttpContext.Request.Path.Value,
            sunsetUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private bool IsLegacyDeprecationEnabled()
    {
        return _configuration.GetValue("ApiContracts:RfqLegacy:Enabled", true);
    }

    private DateTimeOffset ResolveLegacyContractSunsetUtc()
    {
        var configured =
            _configuration["ApiContracts:RfqLegacy:SunsetUtc"] ??
            Environment.GetEnvironmentVariable("RFQ_LEGACY_CONTRACT_SUNSET_UTC");

        if (!string.IsNullOrWhiteSpace(configured) &&
            DateTimeOffset.TryParse(
                configured,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return DefaultSunsetUtc;
    }

    private string ResolveSuccessorBasePath()
    {
        var configured =
            _configuration["ApiContracts:RfqLegacy:SuccessorBasePath"] ??
            Environment.GetEnvironmentVariable("RFQ_LEGACY_CONTRACT_SUCCESSOR_PATH");

        return string.IsNullOrWhiteSpace(configured)
            ? "/api/rfq-workflow"
            : configured.Trim();
    }
}
