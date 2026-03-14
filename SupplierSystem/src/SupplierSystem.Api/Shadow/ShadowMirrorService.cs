using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SupplierSystem.Api.Shadow;

public class ShadowMirrorService
{
    private const string ShadowClientName = "ShadowClient";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ShadowOptions _options;
    private readonly ShadowDiffService _diffService;
    private readonly ILogger<ShadowMirrorService> _logger;
    private readonly SemaphoreSlim _concurrencyGuard;
    private readonly ConcurrentQueue<bool> _recentResults = new();
    private readonly object _breakerLock = new();
    private DateTime _breakerOpenUntil = DateTime.MinValue;

    public ShadowMirrorService(
        IHttpClientFactory httpClientFactory,
        IOptions<ShadowOptions> options,
        ShadowDiffService diffService,
        ILogger<ShadowMirrorService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _diffService = diffService;
        _logger = logger;
        _concurrencyGuard = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
    }

    public bool ShouldProcess(HttpContext context)
    {
        if (!_options.Enabled) return false;
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            Console.WriteLine($"[DEBUG] Method: {context.Request.Method}, ShadowWrite Config: {_options.ShadowWrite}");
        }
        if (!IsReadOrShadowWrite(context.Request.Method)) return false;

        var path = context.Request.Path.Value ?? string.Empty;
        if (!IsWhitelisted(path)) return false;
        if (IsRouteDisabled(path)) return false;

        if (IsBreakerOpen()) return false;

        if (!ShouldSample()) return false;

        return true;
    }

    public async Task MirrorAsync(ShadowRequestData requestData, string primaryBody, TimeSpan primaryLatency, CancellationToken cancellationToken)
    {
        if (!await _concurrencyGuard.WaitAsync(0, cancellationToken))
        {
            _logger.LogDebug("Shadow mirror skipped due to concurrency limit for {Path}", requestData.Path);
            return;
        }

        try
        {
            var baseUrl = string.IsNullOrWhiteSpace(_options.TargetUrl) ? _options.BaseUrl : _options.TargetUrl;
            var targetUrl = new Uri(new Uri(baseUrl), requestData.Path + requestData.QueryString);

            using var requestMessage = new HttpRequestMessage(new HttpMethod(requestData.Method), targetUrl);

            if (requestData.Body is not null)
            {
                var mediaType = GetMediaType(requestData.ContentType);
                requestMessage.Content = new StringContent(requestData.Body, Encoding.UTF8, mediaType);
            }

            CopyHeaders(requestData, requestMessage);

            var client = _httpClientFactory.CreateClient(ShadowClientName);
            client.Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs);

            var stopwatch = Stopwatch.StartNew();
            string shadowBody;
            int shadowStatus;
            try
            {
                using var response = await client.SendAsync(requestMessage, cancellationToken);
                shadowStatus = (int)response.StatusCode;
                shadowBody = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (TaskCanceledException)
            {
                RecordResult(false, true);
                _logger.LogWarning("Shadow call timed out for {Url}", targetUrl);
                return;
            }
            catch (Exception ex)
            {
                RecordResult(false, false);
                _logger.LogWarning(ex, "Shadow call failed for {Url}", targetUrl);
                return;
            }
            stopwatch.Stop();

            var primaryStatus = requestData.PrimaryStatusCode;
            var latencyDeltaMs = stopwatch.ElapsedMilliseconds - primaryLatency.TotalMilliseconds;

            DiffResult diff;
            try
            {
                diff = _options.CompareBody
                    ? _diffService.Compare(primaryBody, shadowBody)
                    : new DiffResult(true, Array.Empty<DiffEntry>());
                LogOutcome(requestData.Path, primaryStatus, shadowStatus, primaryLatency, stopwatch.Elapsed, latencyDeltaMs, diff);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Shadow diff/log failed for {Path}", requestData.Path);
                return;
            }

            RecordResult(diff.IsMatch && shadowStatus == primaryStatus, false);
        }
        finally
        {
            _concurrencyGuard.Release();
        }
    }

    private void CopyHeaders(ShadowRequestData requestData, HttpRequestMessage requestMessage)
    {
        if (requestData.Headers.TryGetValue("Authorization", out var auth))
        {
            var authValue = auth.ToString();
            var previewLength = Math.Min(authValue.Length, 15);
            var authPreview = previewLength > 0 ? authValue.Substring(0, previewLength) : string.Empty;
            _logger.LogInformation("[Shadow] Found Authorization header in primary request. Forwarding: {AuthPreview}...", authPreview);
            requestMessage.Headers.TryAddWithoutValidation("Authorization", authValue);
        }
        else
        {
            _logger.LogWarning("[Shadow] No Authorization header found in incoming request; shadow may return 401.");
        }

        if (requestData.Headers.TryGetValue("Accept", out var accept))
        {
            requestMessage.Headers.TryAddWithoutValidation("Accept", accept.ToString());
        }

        if (requestData.Headers.TryGetValue("Trace-Id", out var trace))
        {
            requestMessage.Headers.TryAddWithoutValidation("Trace-Id", trace.ToString());
        }

        if (requestData.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
        {
            requestMessage.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId.ToString());
        }
    }

    private bool IsReadOrShadowWrite(string method)
    {
        if (HttpMethods.IsGet(method)) return true;
        if (!_options.ShadowWrite) return false;

        return HttpMethods.IsPost(method) ||
               HttpMethods.IsPut(method) ||
               HttpMethods.IsPatch(method) ||
               HttpMethods.IsDelete(method);
    }

    private static string GetMediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return "application/json";
        }

        var trimmed = contentType.Trim();
        var separatorIndex = trimmed.IndexOf(';');
        return separatorIndex >= 0 ? trimmed.Substring(0, separatorIndex).Trim() : trimmed;
    }

    private bool IsWhitelisted(string path)
    {
        if (_options.RouteWhitelist == null || _options.RouteWhitelist.Count == 0) return false;
        return _options.RouteWhitelist.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsRouteDisabled(string path)
    {
        return _options.DisabledRoutes != null &&
               _options.DisabledRoutes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private bool ShouldSample()
    {
        if (_options.SamplingRate <= 0) return false;
        if (_options.SamplingRate >= 1) return true;
        var value = Random.Shared.NextDouble();
        return value <= _options.SamplingRate;
    }

    private bool IsBreakerOpen()
    {
        lock (_breakerLock)
        {
            if (DateTime.UtcNow < _breakerOpenUntil)
            {
                return true;
            }

            return false;
        }
    }

    private void RecordResult(bool success, bool timeout)
    {
        _recentResults.Enqueue(success);
        while (_recentResults.Count > _options.BreakerWindowSize && _recentResults.TryDequeue(out _)) { }

        if (_recentResults.Count < _options.BreakerWindowSize)
        {
            return;
        }

        var failureCount = _recentResults.Count(r => !r);
        var errorRate = (double)failureCount / _recentResults.Count;

        if (errorRate > _options.ErrorRateThreshold)
        {
            lock (_breakerLock)
            {
                _breakerOpenUntil = DateTime.UtcNow.AddSeconds(_options.BreakerOpenSeconds);
            }
            _logger.LogWarning("Shadow breaker opened for {Seconds}s due to error rate {ErrorRate:P2}", _options.BreakerOpenSeconds, errorRate);
        }
    }

    private void LogOutcome(string path, int primaryStatus, int shadowStatus, TimeSpan primaryLatency, TimeSpan shadowLatency, double latencyDeltaMs, DiffResult diff)
    {
        var truncatedDiff = diff.Details.Take(50).Select(MaskSensitive).ToArray();
        var diffPreview = string.Join(" | ", truncatedDiff.Select(d => $"{d.Path}:{d.Type}"));

        _logger.LogInformation("Shadow GET {Path} status {PrimaryStatus}->{ShadowStatus} latency {Primary}ms/{Shadow}ms delta {Delta}ms diffMatch={DiffMatch} diffSample={DiffSample}",
            path,
            primaryStatus,
            shadowStatus,
            primaryLatency.TotalMilliseconds,
            shadowLatency.TotalMilliseconds,
            latencyDeltaMs,
            diff.IsMatch,
            diffPreview);
    }

    private DiffEntry MaskSensitive(DiffEntry entry)
    {
        var isSensitive = _options.SensitiveFields != null &&
                          _options.SensitiveFields.Any(s => entry.Path.EndsWith(s, StringComparison.OrdinalIgnoreCase));

        if (!isSensitive) return entry;

        return entry with
        {
            PrimaryValue = "***",
            ShadowValue = "***"
        };
    }
}
