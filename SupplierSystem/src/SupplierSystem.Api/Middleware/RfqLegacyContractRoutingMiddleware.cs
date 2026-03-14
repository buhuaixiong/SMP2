using System.Globalization;
using System.Text.RegularExpressions;

namespace SupplierSystem.Api.Middleware;

public sealed class RfqLegacyContractRoutingMiddleware
{
    private static readonly DateTimeOffset DefaultSunsetUtc = new(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly Regex RfqByIdPattern = new("^/api/rfq/(?<id>\\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SupplierRfqByIdPattern = new("^/api/rfq/supplier/(?<id>\\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PublishPattern = new("^/api/rfq/(?<id>\\d+)/publish$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ClosePattern = new("^/api/rfq/(?<id>\\d+)/close$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex QuotesCreatePattern = new("^/api/rfq/(?<id>\\d+)/quotes$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex QuotesUpdatePattern = new("^/api/rfq/(?<id>\\d+)/quotes/(?<quoteId>\\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex QuotesWithdrawPattern = new("^/api/rfq/(?<id>\\d+)/quotes/(?<quoteId>\\d+)/withdraw$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ReviewPattern = new("^/api/rfq/(?<id>\\d+)/review$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex GeneratePrExcelPattern = new("^/api/rfq/(?<id>\\d+)/generate-pr-excel$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RfqLegacyContractRoutingMiddleware> _logger;

    public RfqLegacyContractRoutingMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<RfqLegacyContractRoutingMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsForwardingEnabled())
        {
            await _next(context);
            return;
        }

        var method = context.Request.Method;
        var originalPath = NormalizePath(context.Request.Path.Value);
        if (!ShouldHandle(method, originalPath))
        {
            await _next(context);
            return;
        }

        if (!TryResolveForwardPath(method, originalPath, out var targetPath))
        {
            await _next(context);
            return;
        }

        context.Items["LegacyContractForwardedFrom"] = originalPath;
        context.Items["LegacyContractForwardedTo"] = targetPath;
        context.Request.Path = new PathString(targetPath);

        context.Response.OnStarting(() =>
        {
            ApplyLegacyContractHeaders(context.Response, targetPath);
            return Task.CompletedTask;
        });

        _logger.LogInformation(
            "Forwarded legacy RFQ contract request from {OriginalPath} to {TargetPath}.",
            originalPath,
            targetPath);

        await _next(context);
    }

    private bool IsForwardingEnabled()
    {
        return _configuration.GetValue("ApiContracts:RfqLegacy:ForwardToWorkflowEnabled", true);
    }

    private static bool ShouldHandle(string method, string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith("/api/rfq", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/rfq-workflow", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.Contains("/line-items", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/purchase-orders", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HttpMethods.IsGet(method) ||
               HttpMethods.IsPost(method) ||
               HttpMethods.IsPut(method) ||
               HttpMethods.IsDelete(method);
    }

    private static bool TryResolveForwardPath(string method, string path, out string targetPath)
    {
        targetPath = string.Empty;

        if (HttpMethods.IsGet(method) && path.Equals("/api/rfq/categories", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = "/api/rfq-workflow/categories";
            return true;
        }

        if (HttpMethods.IsPost(method) && path.Equals("/api/rfq/import-excel", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = "/api/rfq-workflow/import-excel";
            return true;
        }

        if (HttpMethods.IsGet(method) && path.Equals("/api/rfq", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = "/api/rfq-workflow";
            return true;
        }

        if (HttpMethods.IsPost(method) && path.Equals("/api/rfq", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = "/api/rfq-workflow/create";
            return true;
        }

        if (HttpMethods.IsGet(method) && path.Equals("/api/rfq/supplier/invitations", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = "/api/rfq-workflow/supplier/invitations";
            return true;
        }

        if (HttpMethods.IsGet(method) && TryMatch(RfqByIdPattern, path, out var rfqId))
        {
            targetPath = $"/api/rfq-workflow/{rfqId}";
            return true;
        }

        if (HttpMethods.IsPut(method) && TryMatch(RfqByIdPattern, path, out rfqId))
        {
            targetPath = $"/api/rfq-workflow/{rfqId}";
            return true;
        }

        if (HttpMethods.IsDelete(method) && TryMatch(RfqByIdPattern, path, out rfqId))
        {
            targetPath = $"/api/rfq-workflow/{rfqId}";
            return true;
        }

        if (HttpMethods.IsGet(method) && TryMatch(SupplierRfqByIdPattern, path, out rfqId))
        {
            targetPath = $"/api/rfq-workflow/supplier/{rfqId}";
            return true;
        }

        if (HttpMethods.IsPost(method) && TryMatch(PublishPattern, path, out rfqId))
        {
            targetPath = $"/api/rfq-workflow/{rfqId}/publish";
            return true;
        }

        if (HttpMethods.IsPost(method) && TryMatch(ClosePattern, path, out rfqId))
        {
            targetPath = $"/api/rfq-workflow/{rfqId}/close";
            return true;
        }

        if (HttpMethods.IsPost(method) && TryMatch(QuotesCreatePattern, path, out rfqId))
        {
            targetPath = $"/api/rfq-workflow/{rfqId}/quotes";
            return true;
        }

        if (HttpMethods.IsPut(method) && TryMatchTwoSegments(QuotesUpdatePattern, path, out rfqId, out var quoteId))
        {
            targetPath = $"/api/rfq-workflow/{rfqId}/quotes/{quoteId}";
            return true;
        }

        if (HttpMethods.IsPut(method) && TryMatchTwoSegments(QuotesWithdrawPattern, path, out rfqId, out quoteId))
        {
            targetPath = $"/api/rfq-workflow/{rfqId}/quotes/{quoteId}/withdraw";
            return true;
        }

        if (HttpMethods.IsPost(method) && TryMatch(ReviewPattern, path, out rfqId))
        {
            targetPath = $"/api/rfq-workflow/{rfqId}/review";
            return true;
        }

        if (HttpMethods.IsPost(method) && TryMatch(GeneratePrExcelPattern, path, out rfqId))
        {
            targetPath = $"/api/rfq-workflow/{rfqId}/generate-pr-excel";
            return true;
        }

        return false;
    }

    private void ApplyLegacyContractHeaders(HttpResponse response, string targetPath)
    {
        if (response.HasStarted)
        {
            return;
        }

        var sunsetUtc = ResolveLegacyContractSunsetUtc();
        var successorBasePath = ResolveSuccessorBasePath();

        response.Headers["Deprecation"] = "true";
        response.Headers["Sunset"] = sunsetUtc.ToString("R", CultureInfo.InvariantCulture);
        response.Headers["X-Legacy-Contract"] = "api/rfq";
        response.Headers["X-Legacy-Contract-Forwarded-To"] = targetPath;
        if (!string.IsNullOrWhiteSpace(successorBasePath))
        {
            response.Headers["Link"] = $"<{successorBasePath}>; rel=\"successor-version\"";
        }
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

    private static bool TryMatch(Regex regex, string path, out string id)
    {
        var match = regex.Match(path);
        if (match.Success && match.Groups["id"].Success)
        {
            id = match.Groups["id"].Value;
            return true;
        }

        id = string.Empty;
        return false;
    }

    private static bool TryMatchTwoSegments(Regex regex, string path, out string id, out string quoteId)
    {
        var match = regex.Match(path);
        if (match.Success && match.Groups["id"].Success && match.Groups["quoteId"].Success)
        {
            id = match.Groups["id"].Value;
            quoteId = match.Groups["quoteId"].Value;
            return true;
        }

        id = string.Empty;
        quoteId = string.Empty;
        return false;
    }

    private static string NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Length > 1 && value.EndsWith("/", StringComparison.Ordinal))
        {
            return value.TrimEnd('/');
        }

        return value;
    }
}
