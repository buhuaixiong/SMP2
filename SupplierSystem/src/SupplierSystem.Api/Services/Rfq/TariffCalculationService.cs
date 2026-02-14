using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Rfq;

public sealed class TariffCalculationService
{
    #region Constants

    private const string DefaultCurrency = "CNY";
    private const string DefaultProductGroup = "OTHERS";
    private const string DefaultProjectLocation = "HZ";
    private const string FreightProductGroup = "FREIGHT";

    // Freight route configurations
    private const string ChinaToHzRoute = "28(China Local)";
    private const string HkToHzRoute = "23(HK to HZ)";
    private const string HzToThRoute = "37(HZ to TH)";
    private const string HkToThRoute = "36(HK to TH)";

    // China local country codes
    private static readonly string[] ChinaCountryCodes = { "CN", "CHINA", "CHN" };
    private const string HkCountryCode = "HK";

    // Currency mappings
    private static readonly Dictionary<string, string> CurrencyMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RMB"] = "CNY",
        ["CN¥"] = "CNY",
        ["USDT"] = "USD",
    };

    #endregion

    #region Fallback Rates

    private static readonly Dictionary<string, Dictionary<string, decimal>> FallbackTariffRates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["HK"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["DEFAULT"] = 0.0153m,
            },
        };

    private static readonly Dictionary<string, decimal> SpecialTariffFallbacks =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["US"] = 0.10m,
        };

    // Default freight rates by route
    private static readonly Dictionary<(string Route, string Location), decimal> DefaultFreightRates = new()
    {
        [(ChinaToHzRoute, DefaultProjectLocation)] = 0.0370m,
        [(HkToHzRoute, DefaultProjectLocation)] = 0.0153m,
        [(HzToThRoute, "TH")] = 0.0287m,
        [(HkToThRoute, "TH")] = 0.0241m,
    };

    // Default fallback freight rate
    private const decimal DefaultFreightRate = 0.0153m;

    // Product groups fallback
    private static readonly string[] DefaultProductGroups = { "HARNESS", "HOUSING &CONNECTORS", "OTHERS", "WIRE" };

    // China local options for country list
    private static readonly (string Code, string Name, string NameZh)[] ChinaLocalOptions =
    {
        ("CN", "China Mainland", "中国内地"),
        ("HK", "Hong Kong", "香港"),
    };

    #endregion

    private readonly SupplierSystemDbContext _dbContext;
    private readonly ExchangeRateService _exchangeRateService;
    private readonly ILogger<TariffCalculationService> _logger;

    public TariffCalculationService(
        SupplierSystemDbContext dbContext,
        ExchangeRateService exchangeRateService,
        ILogger<TariffCalculationService> logger)
    {
        _dbContext = dbContext;
        _exchangeRateService = exchangeRateService;
        _logger = logger;
    }

    public async Task<decimal?> GetTariffRateAsync(
        string? countryCode,
        string? productGroup,
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(productGroup))
            return null;

        var normalizedCountry = countryCode.Trim().ToUpperInvariant();
        var normalizedGroup = productGroup.Trim().ToUpperInvariant();
        var targetYear = ResolveTariffYear(year ?? _exchangeRateService.GetConfig().DefaultYear);

        var sql = @"
SELECT TOP 1 rate_2025, rate_2024, rate_2023
FROM tariff_rates
WHERE country_code = @country AND product_group = @group AND is_active = 1";

        var rates = await ExecuteQueryFirstRowAsync(sql, new[]
        {
            CreateParam("@country", normalizedCountry),
            CreateParam("@group", normalizedGroup),
        }, cancellationToken);

        if (rates is null)
            return GetFallbackTariffRate(normalizedCountry, normalizedGroup);

        var (rate2025, rate2024, rate2023) = (rates[0], rates[1], rates[2]);
        var rate = targetYear switch
        {
            2025 when rate2025 > 0 => rate2025,
            2024 when rate2024 > 0 => rate2024,
            2023 when rate2023 > 0 => rate2023,
            _ => rate2025 > 0 ? rate2025 : (rate2024 > 0 ? rate2024 : rate2023),
        };

        return rate > 0 ? rate : GetFallbackTariffRate(normalizedCountry, normalizedGroup);
    }

    private static decimal? GetFallbackTariffRate(string countryCode, string productGroup)
    {
        if (!FallbackTariffRates.TryGetValue(countryCode, out var rates))
            return null;

        return rates.TryGetValue(productGroup, out var direct)
            ? direct
            : rates.TryGetValue("DEFAULT", out var fallback) ? fallback : null;
    }

    public async Task<decimal> GetSpecialTariffRateAsync(
        string? productOrigin,
        string? productGroup = null,
        string? destinationCountry = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productOrigin))
            return 0m;

        var originCode = productOrigin.Trim().ToUpperInvariant();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var sql = @"
SELECT TOP 1 rate
FROM special_tariffs
WHERE origin_country = @origin
  AND is_active = 1
  AND (effective_from IS NULL OR effective_from <= @today)
  AND (effective_to IS NULL OR effective_to >= @today)
  AND (
    (product_group = @productGroup AND destination_country = @destination)
    OR (product_group = @productGroup AND destination_country IS NULL)
    OR (product_group IS NULL AND destination_country IS NULL)
  )
ORDER BY
  CASE WHEN product_group IS NOT NULL AND destination_country IS NOT NULL THEN 1
       WHEN product_group IS NOT NULL THEN 2
       ELSE 3 END";

        var result = await ExecuteScalarQueryAsync(sql, new[]
        {
            CreateParam("@origin", originCode),
            CreateParam("@today", today),
            CreateParam("@productGroup", productGroup),
            CreateParam("@destination", destinationCountry),
        }, cancellationToken);

        return result is null
            ? SpecialTariffFallbacks.TryGetValue(originCode, out var fallback) ? fallback : 0m
            : result.Value;
    }

    public async Task<decimal> GetFreightRateAsync(
        string? shippingCountry,
        string? projectLocation = DefaultProjectLocation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shippingCountry) || string.IsNullOrWhiteSpace(projectLocation))
            return 0m;

        var country = shippingCountry.Trim().ToUpperInvariant();
        var location = projectLocation.Trim().ToUpperInvariant();

        if (IsChinaLocal(country))
            return location == DefaultProjectLocation
                ? await GetTariffRateAsync(ChinaToHzRoute, FreightProductGroup, cancellationToken: cancellationToken)
                  ?? 0.0370m
                : await GetTariffRateAsync(HzToThRoute, FreightProductGroup, cancellationToken: cancellationToken)
                  ?? 0.0287m;

        if (country == HkCountryCode)
            return location == DefaultProjectLocation
                ? await GetTariffRateAsync(HkToHzRoute, FreightProductGroup, cancellationToken: cancellationToken)
                  ?? DefaultFreightRate
                : await GetTariffRateAsync(HkToThRoute, FreightProductGroup, cancellationToken: cancellationToken)
                  ?? 0.0241m;

        // Default fallback for other countries
        return location == DefaultProjectLocation
            ? await GetTariffRateAsync(HkToHzRoute, FreightProductGroup, cancellationToken: cancellationToken)
              ?? DefaultFreightRate
            : await GetTariffRateAsync(HkToThRoute, FreightProductGroup, cancellationToken: cancellationToken)
              ?? 0.0241m;
    }

    private static bool IsChinaLocal(string countryCode) =>
        ChinaCountryCodes.Contains(countryCode);

    private static int ResolveTariffYear(int year)
    {
        return year switch
        {
            2025 => 2025,
            2024 => 2024,
            2023 => 2023,
            _ => 2025,
        };
    }

    public async Task<TariffCalculationResult> CalculateStandardCostAsync(
        decimal originalPrice,
        string? shippingCountry,
        string? productGroup,
        string? productOrigin,
        string? projectLocation = DefaultProjectLocation,
        string? deliveryTerms = "",
        string? currency = DefaultCurrency,
        CancellationToken cancellationToken = default)
    {
        if (originalPrice <= 0)
            return CreateErrorResult("Invalid original price", shippingCountry, productGroup, productOrigin, projectLocation, deliveryTerms, currency ?? DefaultCurrency);

        var normalizedCurrency = NormalizeCurrencyCode(currency) ?? DefaultCurrency;
        var normalizedCountry = shippingCountry?.Trim().ToUpperInvariant();
        var normalizedGroup = string.IsNullOrWhiteSpace(productGroup)
            ? DefaultProductGroup
            : productGroup.Trim().ToUpperInvariant();
        var normalizedLocation = string.IsNullOrWhiteSpace(projectLocation)
            ? DefaultProjectLocation
            : projectLocation.Trim().ToUpperInvariant();
        var normalizedTerms = string.IsNullOrWhiteSpace(deliveryTerms)
            ? string.Empty
            : deliveryTerms.Trim().ToUpperInvariant();

        var isDdp = normalizedTerms == "DDP";
        var isChinaLocal = !string.IsNullOrWhiteSpace(normalizedCountry) && (IsChinaLocal(normalizedCountry) || normalizedCountry == HkCountryCode);

        // Calculate rates in parallel where possible
        var specialTariffRateTask = GetSpecialTariffRateAsync(productOrigin, normalizedGroup, normalizedCountry, cancellationToken);
        var freightRateTask = isDdp || !isChinaLocal
            ? Task.FromResult(0m)
            : GetFreightRateAsync(normalizedCountry, normalizedLocation, cancellationToken);

        await Task.WhenAll(specialTariffRateTask, freightRateTask);

        var specialTariffRate = specialTariffRateTask.Result;
        var freightRate = freightRateTask.Result;
        var countryRate = 0m;
        var tariffRateMissing = false;

        if (!isDdp && !isChinaLocal && !string.IsNullOrWhiteSpace(normalizedCountry))
        {
            var countryTariff = await GetTariffRateAsync(normalizedCountry, "OTHERS", cancellationToken: cancellationToken);
            if (countryTariff.HasValue)
                countryRate = countryTariff.Value;
            else
                tariffRateMissing = true;

            freightRate = countryRate + freightRate;
        }

        var effectiveFreightRate = isDdp ? 0m : freightRate;
        var specialTariffAmount = originalPrice * specialTariffRate;
        var freightAmount = originalPrice * effectiveFreightRate;
        var standardCostLocal = originalPrice * (1 + effectiveFreightRate) + specialTariffAmount;

        var exchangeRate = _exchangeRateService.GetExchangeRate(normalizedCurrency);
        var toUsd = (decimal v) => _exchangeRateService.ConvertToUsd(v, normalizedCurrency);

        var totalTariffAmount = ToFixed(freightAmount + specialTariffAmount);
        var warnings = tariffRateMissing
            ? new List<TariffWarning> { new() { Code = "MISSING_TARIFF_RATE", Message = $"No tariff rate found for {normalizedCountry}/{normalizedGroup}. Using 0% tariff rate.", Severity = "warning" } }
            : null;

        return new TariffCalculationResult
        {
            OriginalPrice = ToFixed(originalPrice),
            OriginalCurrency = normalizedCurrency,
            ExchangeRate = exchangeRate,
            OriginalPriceUsd = toUsd(originalPrice) is var p && p.HasValue ? ToFixed(p.Value) : null,
            TariffRate = 0m,
            TariffRateMissing = tariffRateMissing,
            TariffRatePercent = "0.00%",
            FreightRate = effectiveFreightRate,
            FreightRatePercent = $"{(effectiveFreightRate * 100):0.00}%",
            SpecialTariffRate = specialTariffRate,
            SpecialTariffRatePercent = specialTariffRate > 0 ? $"{(specialTariffRate * 100):0.00}%" : null,
            TariffAmount = 0m,
            FreightAmount = ToFixed(freightAmount),
            SpecialTariffAmount = ToFixed(specialTariffAmount),
            TariffAmountUsd = null,
            FreightAmountUsd = toUsd(freightAmount) is var f && f.HasValue ? ToFixed(f.Value) : null,
            SpecialTariffAmountUsd = toUsd(specialTariffAmount) is var s && s.HasValue ? ToFixed(s.Value) : null,
            TotalTariffAmount = totalTariffAmount,
            TotalTariffAmountUsd = toUsd(freightAmount + specialTariffAmount) is var t && t.HasValue ? ToFixed(t.Value) : null,
            StandardCostLocal = ToFixed(standardCostLocal),
            StandardCostUsd = toUsd(standardCostLocal) is var u && u.HasValue ? ToFixed(u.Value) : null,
            StandardCost = toUsd(standardCostLocal) is var sc && sc.HasValue ? ToFixed(sc.Value) : ToFixed(standardCostLocal),
            StandardCostCurrency = toUsd(standardCostLocal) is var c && c.HasValue ? "USD" : normalizedCurrency,
            ShippingCountry = normalizedCountry,
            ProductGroup = normalizedGroup,
            ProductOrigin = productOrigin,
            ProjectLocation = normalizedLocation,
            DeliveryTerms = normalizedTerms,
            IsDdp = isDdp,
            HasSpecialTariff = specialTariffRate > 0,
            Warnings = warnings,
        };
    }

    private TariffCalculationResult CreateErrorResult(string error, string? country, string? group, string? origin, string? location, string? terms, string currency)
    {
        var normCurrency = NormalizeCurrencyCode(currency) ?? DefaultCurrency;
        return new()
        {
            OriginalPrice = 0m,
            OriginalCurrency = normCurrency,
            StandardCost = 0m,
            StandardCostCurrency = normCurrency,
            ShippingCountry = country?.Trim().ToUpperInvariant(),
            ProductGroup = string.IsNullOrWhiteSpace(group) ? DefaultProductGroup : group.Trim().ToUpperInvariant(),
            ProductOrigin = origin,
            Error = error,
        };
    }

    public async Task<List<Dictionary<string, object?>>> CalculateQuoteItemsCostsAsync(
        IEnumerable<Dictionary<string, object?>> quoteItems,
        string? shippingCountry,
        string? currency = DefaultCurrency,
        string? projectLocation = "HZ",
        string? deliveryTerms = "",
        CancellationToken cancellationToken = default)
    {
        var results = new List<Dictionary<string, object?>>();

        foreach (var item in quoteItems)
        {
            var unitPrice = GetDecimal(item, "unit_price") ?? GetDecimal(item, "unitPrice") ?? 0m;
            var productGroup = GetString(item, "product_group") ?? GetString(item, "productGroup") ?? DefaultProductGroup;
            var productOrigin = GetString(item, "product_origin") ?? GetString(item, "productOrigin");

            var calculation = await CalculateStandardCostAsync(
                unitPrice,
                shippingCountry,
                productGroup,
                productOrigin,
                projectLocation,
                deliveryTerms,
                currency,
                cancellationToken);

            var standardUnitCostUsd = calculation.StandardCostUsd ?? calculation.StandardCostLocal;

            var clone = new Dictionary<string, object?>(item, StringComparer.Ordinal)
            {
                ["tariffCalculation"] = calculation,
                ["standardUnitCost"] = standardUnitCostUsd,
                ["standardUnitCostLocal"] = calculation.StandardCostLocal,
                ["standardUnitCostUsd"] = calculation.StandardCostUsd,
            };

            results.Add(clone);
        }

        return results;
    }

    public async Task<List<Dictionary<string, object?>>> GetAvailableCountriesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = new List<Dictionary<string, object?>>();
        var knownCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = false;

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
                shouldClose = true;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT DISTINCT country_code, country_name, country_name_zh
FROM tariff_rates
WHERE is_active = 1 AND product_group != 'FREIGHT'
ORDER BY country_code";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["country_code"] = reader.IsDBNull(0) ? null : reader.GetString(0),
                    ["country_name"] = reader.IsDBNull(1) ? null : reader.GetString(1),
                    ["country_name_zh"] = reader.IsDBNull(2) ? null : reader.GetString(2),
                };
                rows.Add(row);
                if (row.TryGetValue("country_code", out var code) && code is string c)
                    knownCodes.Add(c);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Tariff] Error getting available countries.");
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        // Add fallback countries
        foreach (var code in FallbackTariffRates.Keys.Where(c => !knownCodes.Contains(c)))
        {
            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["country_code"] = code,
                ["country_name"] = code,
                ["country_name_zh"] = code,
            });
        }

        // Add China local options
        foreach (var (code, name, nameZh) in ChinaLocalOptions.Where(o => !knownCodes.Contains(o.Code)))
        {
            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["country_code"] = code,
                ["country_name"] = name,
                ["country_name_zh"] = nameZh,
            });
        }

        return rows.OrderBy(row => row.GetValueOrDefault("country_code")?.ToString()).ToList();
    }

    public async Task<List<string>> GetProductGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = new List<string>();
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = false;

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
                shouldClose = true;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT DISTINCT product_group
FROM tariff_rates
WHERE is_active = 1
ORDER BY product_group";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                if (!reader.IsDBNull(0))
                    groups.Add(reader.GetString(0));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Tariff] Error getting product groups.");
            groups.AddRange(DefaultProductGroups);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        return groups;
    }

    #region Database Helper Methods

    private async Task<decimal?> ExecuteScalarQueryAsync(string sql, IDbDataParameter[] parameters, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = false;

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
                shouldClose = true;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? null : Convert.ToDecimal(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Tariff] Database query failed: {Sql}", sql.Substring(0, Math.Min(100, sql.Length)));
            return null;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<decimal[]?> ExecuteQueryFirstRowAsync(string sql, IDbDataParameter[] parameters, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = false;

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
                shouldClose = true;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            var values = new decimal[3];
            for (int i = 0; i < 3; i++)
                values[i] = reader.IsDBNull(i) ? 0m : Convert.ToDecimal(reader.GetValue(i));

            return values;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Tariff] Database query failed: {Sql}", sql.Substring(0, Math.Min(100, sql.Length)));
            return null;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static IDbDataParameter CreateParam(string name, object? value)
    {
        var parameter = new SqlParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    #endregion

    #region Utility Methods

    private static string? NormalizeCurrencyCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var normalized = code.Trim().ToUpperInvariant();
        return CurrencyMappings.TryGetValue(normalized, out var mapped) ? mapped : normalized;
    }

    private static decimal ToFixed(decimal value, int digits = 2) =>
        Math.Round(value, digits, MidpointRounding.AwayFromZero);

    private static decimal? ToUsd(decimal? value, Func<decimal, decimal?> convert) =>
        value.HasValue ? convert(value.Value) : null;

    private static decimal? GetDecimal(Dictionary<string, object?> item, string key) =>
        item.TryGetValue(key, out var raw) && raw != null
            ? raw switch
            {
                decimal d => d,
                double d => Convert.ToDecimal(d),
                float f => Convert.ToDecimal(f),
                long l => l,
                int i => i,
                _ => decimal.TryParse(raw.ToString(), out var p) ? p : null
            }
            : null;

    private static string? GetString(Dictionary<string, object?> item, string key) =>
        item.TryGetValue(key, out var raw) ? raw?.ToString() : null;

    #endregion
}

public sealed class TariffCalculationResult
{
    public decimal OriginalPrice { get; set; }
    public string? OriginalCurrency { get; set; }
    public decimal? ExchangeRate { get; set; }
    public decimal? OriginalPriceUsd { get; set; }
    public decimal TariffRate { get; set; }
    public bool TariffRateMissing { get; set; }
    public string? TariffRatePercent { get; set; }
    public decimal FreightRate { get; set; }
    public string? FreightRatePercent { get; set; }
    public decimal SpecialTariffRate { get; set; }
    public string? SpecialTariffRatePercent { get; set; }
    public decimal TariffAmount { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal SpecialTariffAmount { get; set; }
    public decimal? TariffAmountUsd { get; set; }
    public decimal? FreightAmountUsd { get; set; }
    public decimal? SpecialTariffAmountUsd { get; set; }
    public decimal TotalTariffAmount { get; set; }
    public decimal? TotalTariffAmountUsd { get; set; }
    public decimal StandardCostLocal { get; set; }
    public decimal? StandardCostUsd { get; set; }
    public decimal StandardCost { get; set; }
    public string? StandardCostCurrency { get; set; }
    public string? ShippingCountry { get; set; }
    public string? ProductGroup { get; set; }
    public string? ProductOrigin { get; set; }
    public string? ProjectLocation { get; set; }
    public string? DeliveryTerms { get; set; }
    public bool IsDdp { get; set; }
    public bool HasSpecialTariff { get; set; }
    public List<TariffWarning>? Warnings { get; set; }
    public string? Error { get; set; }
}

public sealed class TariffWarning
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? Severity { get; set; }
}
