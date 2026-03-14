using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/country-freight-rates")]
public sealed class CountryFreightRatesController : NodeControllerBase
{
    private static readonly int[] SupportedYears = { 2025, 2024, 2023 };

    private readonly CountryFreightRateDataService _countryFreightRateDataService;
    private readonly IAuditService _auditService;
    private readonly ILogger<CountryFreightRatesController> _logger;

    public CountryFreightRatesController(
        CountryFreightRateDataService countryFreightRateDataService,
        IAuditService auditService,
        IWebHostEnvironment environment,
        ILogger<CountryFreightRatesController> logger) : base(environment)
    {
        _countryFreightRateDataService = countryFreightRateDataService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetRates(CancellationToken cancellationToken)
    {
        var year = ParseInt(Request.Query["year"], SupportedYears[0]);
        if (!SupportedYears.Contains(year))
        {
            return BadRequest(new { message = $"Unsupported year: {year}" });
        }

        try
        {
            var rows = await _countryFreightRateDataService.LoadCountryRatesAsync(cancellationToken);
            var data = rows
                .Select(row => new
                {
                    countryCode = row.CountryCode,
                    countryName = row.CountryName,
                    countryNameZh = row.CountryNameZh,
                    productGroup = row.ProductGroup,
                    rate = GetRateForYear(row, year),
                    isActive = row.IsActive,
                })
                .OrderBy(row => row.countryCode)
                .ThenBy(row => row.productGroup)
                .ToList();

            var productGroups = rows
                .Select(row => row.ProductGroup)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group)
                .ToList();

            return Ok(new
            {
                data = new
                {
                    year,
                    availableYears = SupportedYears,
                    productGroups,
                    rates = data,
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CountryFreightRates] Failed to load country freight rates.");
            return StatusCode(500, new { message = "Failed to load country freight rates", error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateRates([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminSystemConfig);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var year = ReadInt(body, "year", "rateYear", "rate_year") ?? SupportedYears[0];
        if (!SupportedYears.Contains(year))
        {
            return BadRequest(new { message = $"Unsupported year: {year}" });
        }

        if (!body.TryGetProperty("rates", out var ratesElement) || ratesElement.ValueKind != JsonValueKind.Array)
        {
            return BadRequest(new { message = "Rates array is required." });
        }

        var updates = new List<CountryFreightRateUpdate>();
        foreach (var entry in ratesElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var countryCode = ReadString(entry, "countryCode", "country_code", "code");
            var productGroup = ReadString(entry, "productGroup", "product_group", "group");

            if (string.IsNullOrWhiteSpace(countryCode))
            {
                return BadRequest(new { message = "countryCode is required." });
            }

            if (string.IsNullOrWhiteSpace(productGroup))
            {
                return BadRequest(new { message = "productGroup is required." });
            }

            if (string.Equals(productGroup.Trim(), "FREIGHT", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "productGroup cannot be FREIGHT for country freight rates." });
            }

            if (!TryParseDecimal(entry, "rate", out var rate) || rate <= 0)
            {
                return BadRequest(new { message = $"Invalid rate for {countryCode}/{productGroup}." });
            }

            var countryName = ReadString(entry, "countryName", "country_name", "name");
            var countryNameZh = ReadString(entry, "countryNameZh", "country_name_zh", "nameZh", "name_zh");

            updates.Add(new CountryFreightRateUpdate
            {
                CountryCode = countryCode.Trim().ToUpperInvariant(),
                ProductGroup = productGroup.Trim().ToUpperInvariant(),
                CountryName = string.IsNullOrWhiteSpace(countryName) ? null : countryName.Trim(),
                CountryNameZh = string.IsNullOrWhiteSpace(countryNameZh) ? null : countryNameZh.Trim(),
                Rate = rate,
            });
        }

        if (updates.Count == 0)
        {
            return BadRequest(new { message = "Rates array is required." });
        }

        var notes = ReadString(body, "notes");
        var noteValue = string.IsNullOrWhiteSpace(notes) ? $"Manual update by {actor.Name}" : notes;
        var rateColumn = GetRateColumn(year);

        await using var transaction = await _countryFreightRateDataService.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var update in updates)
            {
                await _countryFreightRateDataService.UpsertCountryRateAsync(update, year, rateColumn, cancellationToken);
            }

            await _countryFreightRateDataService.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = actor.Id,
                ActorName = actor.Name,
                EntityType = "country_freight_rate",
                EntityId = string.Join(",", updates.Select(u => $"{u.CountryCode}/{u.ProductGroup}")),
                Action = "update",
                Changes = JsonSerializer.Serialize(new { year, rates = updates, notes = noteValue }),
            });

            return Ok(new
            {
                message = "Country freight rates updated successfully.",
                data = new
                {
                    year,
                    updatedCount = updates.Count,
                }
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "[CountryFreightRates] Failed to update country freight rates.");
            return StatusCode(500, new { message = "Failed to update country freight rates", error = ex.Message });
        }
    }

    private static decimal GetRateForYear(CountryRateRow row, int year)
    {
        return year switch
        {
            2025 => row.Rate2025,
            2024 => row.Rate2024,
            2023 => row.Rate2023,
            _ => row.Rate2025,
        };
    }

    private static string GetRateColumn(int year)
    {
        return year switch
        {
            2025 => "rate_2025",
            2024 => "rate_2024",
            2023 => "rate_2023",
            _ => "rate_2025",
        };
    }

    private static bool TryParseDecimal(JsonElement element, string key, out decimal value)
    {
        value = 0m;
        if (!element.TryGetProperty(key, out var raw))
        {
            return false;
        }

        if (raw.ValueKind == JsonValueKind.Number && raw.TryGetDecimal(out value))
        {
            return true;
        }

        if (raw.ValueKind == JsonValueKind.String && decimal.TryParse(raw.GetString(), out value))
        {
            return true;
        }

        return false;
    }

    private static int? ReadInt(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric))
            {
                return numeric;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (body.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
                {
                    return value.ToString();
                }
            }
        }

        return null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static IActionResult? RequirePermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.All(granted.Contains))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }

}
