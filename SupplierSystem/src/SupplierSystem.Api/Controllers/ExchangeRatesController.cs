using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exchange-rates")]
public sealed class ExchangeRatesController : NodeControllerBase
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly ExchangeRateService _exchangeRateService;
    private readonly IAuditService _auditService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ExchangeRatesController> _logger;

    public ExchangeRatesController(
        SupplierSystemDbContext dbContext,
        ExchangeRateService exchangeRateService,
        IAuditService auditService,
        IWebHostEnvironment environment,
        ILogger<ExchangeRatesController> logger) : base(environment)
    {
        _dbContext = dbContext;
        _exchangeRateService = exchangeRateService;
        _auditService = auditService;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        var config = _exchangeRateService.GetConfig();
        return Ok(new
        {
            data = new
            {
                updatedAt = config.UpdatedAt,
                defaultYear = config.DefaultYear,
                rates = config.Rates,
            }
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
    {
        var currency = Request.Query["currency"].ToString();
        var limit = ParseInt(Request.Query["limit"], 50);
        limit = Math.Max(1, Math.Min(200, limit));

        var query = _dbContext.ExchangeRateHistories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var code = currency.Trim().ToUpperInvariant();
            query = query.Where(h => h.Currency == code);
        }

        var history = await query
            .OrderByDescending(h => h.EffectiveDate)
            .ThenByDescending(h => h.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Ok(new { data = history });
    }

    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies(CancellationToken cancellationToken)
    {
        var currencies = await _dbContext.ExchangeRateHistories
            .AsNoTracking()
            .Select(h => h.Currency)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        return Ok(new { data = currencies });
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

        if (!body.TryGetProperty("rates", out var ratesElement) || ratesElement.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(new { message = "Rates object is required." });
        }

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in ratesElement.EnumerateObject())
        {
            var code = entry.Name?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (!TryParseDecimal(entry.Value, out var rate) || rate <= 0)
            {
                return BadRequest(new { message = $"Invalid rate for {code}: {entry.Value}" });
            }

            rates[code.ToUpperInvariant()] = rate;
        }

        if (rates.Count == 0)
        {
            return BadRequest(new { message = "Rates object is required." });
        }

        var effectiveDate = ReadString(body, "effectiveDate", "effective_date");
        var notes = ReadString(body, "notes");
        var effective = string.IsNullOrWhiteSpace(effectiveDate)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd")
            : effectiveDate;

        if (!DateTime.TryParse(effective, out var parsedDate))
        {
            return BadRequest(new { message = "Invalid effectiveDate format." });
        }

        var year = parsedDate.Year;
        var now = DateTimeOffset.UtcNow.ToString("o");

        var insertedIds = new List<int>();
        foreach (var entry in rates)
        {
            var record = new ExchangeRateHistory
            {
                Currency = entry.Key,
                Rate = entry.Value,
                EffectiveDate = parsedDate.ToString("yyyy-MM-dd"),
                Source = "manual",
                Notes = string.IsNullOrWhiteSpace(notes) ? $"Manual update by {actor.Name}" : notes,
                CreatedBy = actor.Id,
                CreatedAt = now,
            };

            _dbContext.ExchangeRateHistories.Add(record);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        insertedIds = await _dbContext.ExchangeRateHistories
            .AsNoTracking()
            .OrderByDescending(h => h.Id)
            .Take(rates.Count)
            .Select(h => h.Id)
            .ToListAsync(cancellationToken);

        try
        {
            UpdateExchangeRateConfig(year, rates);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ExchangeRate] Failed to update config file.");
        }

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = actor.Id,
            ActorName = actor.Name,
            EntityType = "exchange_rate",
            EntityId = string.Join(",", insertedIds),
            Action = "update",
            Changes = JsonSerializer.Serialize(new { rates, effectiveDate = parsedDate.ToString("yyyy-MM-dd") }),
        });

        return Ok(new
        {
            message = "Exchange rates updated successfully.",
            data = new
            {
                insertedCount = rates.Count,
                effectiveDate = parsedDate.ToString("yyyy-MM-dd"),
                year,
            }
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRate(int id, CancellationToken cancellationToken)
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

        var record = await _dbContext.ExchangeRateHistories.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (record == null)
        {
            return NotFound(new { message = "Exchange rate record not found." });
        }

        _dbContext.ExchangeRateHistories.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            ActorId = actor.Id,
            ActorName = actor.Name,
            EntityType = "exchange_rate",
            EntityId = id.ToString(),
            Action = "delete",
            Changes = JsonSerializer.Serialize(new { currency = record.Currency, rate = record.Rate }),
        });

        return Ok(new { message = "Exchange rate record deleted successfully." });
    }

    private void UpdateExchangeRateConfig(int year, IDictionary<string, decimal> rates)
    {
        var configPath = Environment.GetEnvironmentVariable("EXCHANGE_RATES_FILE");
        if (string.IsNullOrWhiteSpace(configPath))
        {
            var repoRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..", ".."));
            configPath = Path.Combine(repoRoot, "app", "apps", "api", "config", "exchangeRates.json");
        }
        else
        {
            configPath = Path.GetFullPath(configPath.Trim());
        }

        var config = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["rates"] = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase)
        };

        if (System.IO.File.Exists(configPath))
        {
            try
            {
                var raw = System.IO.File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("rates", out var ratesElement) && ratesElement.ValueKind == JsonValueKind.Object)
                {
                    var rateMap = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var yearEntry in ratesElement.EnumerateObject())
                    {
                        if (yearEntry.Value.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var inner = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                        foreach (var rateEntry in yearEntry.Value.EnumerateObject())
                        {
                            if (TryParseDecimal(rateEntry.Value, out var parsed))
                            {
                                inner[rateEntry.Name.ToUpperInvariant()] = parsed;
                            }
                        }

                        rateMap[yearEntry.Name] = inner;
                    }

                    config["rates"] = rateMap;
                }
            }
            catch
            {
                // ignore parse errors
            }
        }

        if (config["rates"] is not Dictionary<string, Dictionary<string, decimal>> rateConfig)
        {
            rateConfig = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);
            config["rates"] = rateConfig;
        }

        var yearKey = year.ToString();
        if (!rateConfig.TryGetValue(yearKey, out var yearRates))
        {
            yearRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            rateConfig[yearKey] = yearRates;
        }

        foreach (var entry in rates)
        {
            yearRates[entry.Key.ToUpperInvariant()] = entry.Value;
        }

        config["updatedAt"] = DateTimeOffset.UtcNow.ToString("o");
        config["defaultYear"] = year;

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? ".");
        System.IO.File.WriteAllText(configPath, json);
    }

    private static bool TryParseDecimal(JsonElement element, out decimal value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), out value))
        {
            return true;
        }

        value = 0m;
        return false;
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
