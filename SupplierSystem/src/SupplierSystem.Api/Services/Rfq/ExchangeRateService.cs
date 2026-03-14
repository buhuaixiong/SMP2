using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace SupplierSystem.Api.Services.Rfq;

public sealed class ExchangeRateService
{
    private const int DefaultYear = 2025;
    private const string DefaultUpdatedAt = "2025-01-01";

    private static readonly ExchangeRateConfig DefaultConfig = new()
    {
        UpdatedAt = DefaultUpdatedAt,
        DefaultYear = DefaultYear,
        Rates = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal)
        {
            ["2025"] = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["RMB"] = 0.1388889m,
                ["HKD"] = 0.1282051m,
                ["EUR"] = 1.0989011m,
                ["USD"] = 1.0m,
                ["KRW"] = 0.0007576m,
                ["GBP"] = 1.35m,
                ["JPY"] = 0.0068376m,
                ["THB"] = 0.0277778m,
            }
        }
    };

    private readonly ExchangeRateConfig _config;

    public ExchangeRateService(IWebHostEnvironment environment, ILogger<ExchangeRateService> logger)
    {
        _config = LoadExchangeRateConfig(environment, logger);
    }

    public decimal? GetExchangeRate(string? currency, int? year = null)
    {
        var code = NormalizeCurrencyCode(currency);
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var targetYear = year ?? _config.DefaultYear;
        if (_config.Rates.TryGetValue(targetYear.ToString(), out var yearRates) &&
            yearRates.TryGetValue(code, out var rate))
        {
            return rate;
        }

        if (_config.Rates.TryGetValue(_config.DefaultYear.ToString(), out var defaultRates) &&
            defaultRates.TryGetValue(code, out rate))
        {
            return rate;
        }

        return null;
    }

    public decimal? ConvertToUsd(decimal amount, string? currency, int? year = null)
    {
        var numericAmount = amount;
        var rate = GetExchangeRate(currency, year);
        if (rate == null)
        {
            return null;
        }

        var usd = numericAmount * rate.Value;
        return Math.Round(usd, 6, MidpointRounding.AwayFromZero);
    }

    public IReadOnlyList<string> GetAvailableCurrencies(int? year = null)
    {
        var targetYear = year ?? _config.DefaultYear;
        if (_config.Rates.TryGetValue(targetYear.ToString(), out var yearRates))
        {
            return yearRates.Keys.ToList();
        }

        return Array.Empty<string>();
    }

    public ExchangeRateConfig GetConfig()
    {
        return _config;
    }

    private static ExchangeRateConfig LoadExchangeRateConfig(IWebHostEnvironment environment, ILogger logger)
    {
        var config = CloneConfig(DefaultConfig);

        var configPath = Environment.GetEnvironmentVariable("EXCHANGE_RATES_FILE");
        if (string.IsNullOrWhiteSpace(configPath))
        {
            var repoRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", ".."));
            configPath = Path.Combine(repoRoot, "app", "apps", "api", "config", "exchangeRates.json");
        }
        else
        {
            configPath = Path.GetFullPath(configPath.Trim());
        }

        if (File.Exists(configPath))
        {
            try
            {
                var raw = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(raw);
                config = MergeConfigs(config, ParseConfig(doc.RootElement));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[ExchangeRate] Failed to read config file, using defaults.");
            }
        }
        else
        {
            logger.LogWarning("[ExchangeRate] Config file not found at {Path}, using defaults.", configPath);
        }

        var overrideJson = Environment.GetEnvironmentVariable("EXCHANGE_RATES_JSON");
        if (!string.IsNullOrWhiteSpace(overrideJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(overrideJson);
                config = MergeConfigs(config, ParseConfig(doc.RootElement));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[ExchangeRate] Failed to parse EXCHANGE_RATES_JSON override.");
            }
        }

        NormalizeConfig(config);
        return config;
    }

    private static ExchangeRateConfig ParseConfig(JsonElement element)
    {
        var parsed = new ExchangeRateConfig
        {
            Rates = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal),
        };

        if (element.ValueKind != JsonValueKind.Object)
        {
            return parsed;
        }

        if (element.TryGetProperty("updatedAt", out var updatedAt) && updatedAt.ValueKind == JsonValueKind.String)
        {
            parsed.UpdatedAt = updatedAt.GetString();
        }

        if (element.TryGetProperty("defaultYear", out var defaultYear))
        {
            if (defaultYear.ValueKind == JsonValueKind.Number && defaultYear.TryGetInt32(out var year))
            {
                parsed.DefaultYear = year;
            }
            else if (defaultYear.ValueKind == JsonValueKind.String && int.TryParse(defaultYear.GetString(), out year))
            {
                parsed.DefaultYear = year;
            }
        }

        if (element.TryGetProperty("rates", out var ratesElement) && ratesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var yearProperty in ratesElement.EnumerateObject())
            {
                if (yearProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var yearRates = new Dictionary<string, decimal>(StringComparer.Ordinal);
                foreach (var rateProperty in yearProperty.Value.EnumerateObject())
                {
                    var numeric = ParseDecimal(rateProperty.Value);
                    if (numeric == null)
                    {
                        continue;
                    }
                    yearRates[rateProperty.Name] = numeric.Value;
                }

                if (yearRates.Count > 0)
                {
                    parsed.Rates[yearProperty.Name] = yearRates;
                }
            }
        }

        return parsed;
    }

    private static ExchangeRateConfig MergeConfigs(ExchangeRateConfig baseConfig, ExchangeRateConfig overrideConfig)
    {
        var merged = CloneConfig(baseConfig);

        if (!string.IsNullOrWhiteSpace(overrideConfig.UpdatedAt))
        {
            merged.UpdatedAt = overrideConfig.UpdatedAt;
        }

        if (overrideConfig.DefaultYear > 0)
        {
            merged.DefaultYear = overrideConfig.DefaultYear;
        }

        if (overrideConfig.Rates != null)
        {
            foreach (var (year, rates) in overrideConfig.Rates)
            {
                if (!merged.Rates.TryGetValue(year, out var existing))
                {
                    existing = new Dictionary<string, decimal>(StringComparer.Ordinal);
                    merged.Rates[year] = existing;
                }

                foreach (var (currency, rate) in rates)
                {
                    existing[currency] = rate;
                }
            }
        }

        return merged;
    }

    private static void NormalizeConfig(ExchangeRateConfig config)
    {
        var normalized = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal);

        foreach (var (year, rates) in config.Rates)
        {
            var normalizedYearRates = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var (currency, rate) in rates)
            {
                var code = NormalizeCurrencyCode(currency);
                if (string.IsNullOrWhiteSpace(code) || rate <= 0)
                {
                    continue;
                }

                normalizedYearRates[code] = rate;
            }

            if (normalizedYearRates.Count > 0)
            {
                normalized[year] = normalizedYearRates;
            }
        }

        config.Rates = normalized;

        if (config.DefaultYear == 0 || !normalized.ContainsKey(config.DefaultYear.ToString()))
        {
            var years = normalized.Keys
                .Select(year => int.TryParse(year, out var parsed) ? parsed : 0)
                .Where(parsed => parsed > 0)
                .OrderByDescending(parsed => parsed)
                .ToList();

            config.DefaultYear = years.Count > 0 ? years[0] : DefaultYear;
        }
    }

    private static ExchangeRateConfig CloneConfig(ExchangeRateConfig source)
    {
        var clone = new ExchangeRateConfig
        {
            UpdatedAt = source.UpdatedAt,
            DefaultYear = source.DefaultYear,
            Rates = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal),
        };

        foreach (var (year, rates) in source.Rates)
        {
            clone.Rates[year] = new Dictionary<string, decimal>(rates, StringComparer.Ordinal);
        }

        return clone;
    }

    private static decimal? ParseDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var numeric))
        {
            return numeric;
        }

        if (element.ValueKind == JsonValueKind.String &&
            decimal.TryParse(element.GetString(), out numeric))
        {
            return numeric;
        }

        return null;
    }

    private static string? NormalizeCurrencyCode(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized switch
        {
            "RMB" => "CNY",
            "CN¥" => "CNY",
            "CNY" => "CNY",
            "USDT" => "USD",
            "USD" => "USD",
            _ => normalized,
        };
    }
}

public sealed class ExchangeRateConfig
{
    public string? UpdatedAt { get; set; }
    public int DefaultYear { get; set; }
    public Dictionary<string, Dictionary<string, decimal>> Rates { get; set; } =
        new(StringComparer.Ordinal);
}
