using System.Collections.Generic;

namespace SupplierSystem.Api.Shadow;

public sealed class ShadowOptions
{
    public bool Enabled { get; set; }
    public bool ShadowWrite { get; set; }
    public bool CompareBody { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public int TimeoutMs { get; set; } = 5000;
    public int MaxConcurrency { get; set; } = 10;
    public double SamplingRate { get; set; } = 1.0;
    public int BreakerWindowSize { get; set; } = 20;
    public double ErrorRateThreshold { get; set; } = 0.5;
    public int BreakerOpenSeconds { get; set; } = 30;
    public List<string>? RouteWhitelist { get; set; }
    public List<string>? DisabledRoutes { get; set; }
    public List<string>? SensitiveFields { get; set; }
}

public sealed class ShadowRequestData
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string QueryString { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? ContentType { get; set; }
    public int PrimaryStatusCode { get; set; }
    public IDictionary<string, Microsoft.Extensions.Primitives.StringValues> Headers { get; set; } =
        new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
}

public sealed record DiffEntry(string Path, string Type, string? PrimaryValue, string? ShadowValue);

public sealed record DiffResult(bool IsMatch, IReadOnlyList<DiffEntry> Details);

public sealed class ShadowDiffService
{
    public DiffResult Compare(string primaryBody, string shadowBody)
    {
        var isMatch = string.Equals(primaryBody, shadowBody, StringComparison.Ordinal);
        return new DiffResult(isMatch, Array.Empty<DiffEntry>());
    }
}
