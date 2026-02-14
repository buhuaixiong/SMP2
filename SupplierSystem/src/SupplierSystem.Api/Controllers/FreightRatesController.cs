using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/freight-rates")]
public sealed class FreightRatesController : NodeControllerBase
{
    private static readonly int[] SupportedYears = { 2025, 2024, 2023 };

    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<FreightRatesController> _logger;

    public FreightRatesController(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        IWebHostEnvironment environment,
        ILogger<FreightRatesController> logger) : base(environment)
    {
        _dbContext = dbContext;
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
            var routes = await LoadFreightRoutesAsync(cancellationToken);
            var data = routes
                .Select(route => new
                {
                    id = route.Id,
                    routeCode = route.RouteCode,
                    routeName = route.RouteName,
                    routeNameZh = route.RouteNameZh,
                    rate = GetRateForYear(route, year),
                    isActive = route.IsActive,
                })
                .OrderBy(route => route.routeCode)
                .ToList();

            return Ok(new
            {
                data = new
                {
                    year,
                    availableYears = SupportedYears,
                    routes = data,
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FreightRates] Failed to load freight rates.");
            return StatusCode(500, new { message = "Failed to load freight rates", error = ex.Message });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
    {
        var routeCode = Request.Query["route"].ToString();
        var year = ParseInt(Request.Query["year"], 0);
        var limit = ParseInt(Request.Query["limit"], 50);
        limit = Math.Max(1, Math.Min(200, limit));

        try
        {
            var query = _dbContext.FreightRateHistories.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(routeCode))
            {
                var code = routeCode.Trim();
                query = query.Where(h => h.RouteCode == code);
            }

            if (year > 0)
            {
                query = query.Where(h => h.Year == year);
            }

            var history = await query
                .OrderByDescending(h => h.CreatedAt)
                .ThenByDescending(h => h.Id)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return Ok(new { data = history });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FreightRates] Failed to load freight rate history.");
            return StatusCode(500, new { message = "Failed to load freight rate history", error = ex.Message });
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

        if (!body.TryGetProperty("routes", out var routesElement) || routesElement.ValueKind != JsonValueKind.Array)
        {
            return BadRequest(new { message = "Routes array is required." });
        }

        var updates = new List<FreightRateUpdate>();
        foreach (var entry in routesElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var routeCode = ReadString(entry, "routeCode", "route", "route_code", "code", "countryCode", "country_code");
            if (string.IsNullOrWhiteSpace(routeCode))
            {
                return BadRequest(new { message = "routeCode is required." });
            }

            if (!TryParseDecimal(entry, "rate", out var rate) || rate <= 0)
            {
                return BadRequest(new { message = $"Invalid rate for {routeCode}." });
            }

            var routeName = ReadString(entry, "routeName", "route_name", "name");
            var routeNameZh = ReadString(entry, "routeNameZh", "route_name_zh", "nameZh", "name_zh");

            updates.Add(new FreightRateUpdate
            {
                RouteCode = routeCode.Trim(),
                RouteName = string.IsNullOrWhiteSpace(routeName) ? null : routeName.Trim(),
                RouteNameZh = string.IsNullOrWhiteSpace(routeNameZh) ? null : routeNameZh.Trim(),
                Rate = rate,
            });
        }

        if (updates.Count == 0)
        {
            return BadRequest(new { message = "Routes array is required." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var notes = ReadString(body, "notes");
        var noteValue = string.IsNullOrWhiteSpace(notes) ? $"Manual update by {actor.Name}" : notes;
        var rateColumn = GetRateColumn(year);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var update in updates)
            {
                await UpsertFreightRateAsync(update, year, rateColumn, cancellationToken);
            }

            var historyEntries = updates.Select(update => new FreightRateHistory
            {
                RouteCode = update.RouteCode,
                RouteName = update.RouteName,
                RouteNameZh = update.RouteNameZh,
                Rate = update.Rate,
                Year = year,
                Source = "manual",
                Notes = noteValue,
                CreatedBy = actor.Id,
                CreatedAt = now,
            }).ToList();

            _dbContext.FreightRateHistories.AddRange(historyEntries);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = actor.Id,
                ActorName = actor.Name,
                EntityType = "freight_rate",
                EntityId = string.Join(",", historyEntries.Select(h => h.Id)),
                Action = "update",
                Changes = JsonSerializer.Serialize(new { year, routes = updates }),
            });

            return Ok(new
            {
                message = "Freight rates updated successfully.",
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
            _logger.LogWarning(ex, "[FreightRates] Failed to update freight rates.");
            return StatusCode(500, new { message = "Failed to update freight rates", error = ex.Message });
        }
    }

    private async Task<List<FreightRouteRow>> LoadFreightRoutesAsync(CancellationToken cancellationToken)
    {
        var rows = new List<FreightRouteRow>();
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
SELECT id, country_code, country_name, country_name_zh, rate_2025, rate_2024, rate_2023, is_active
FROM tariff_rates
WHERE product_group = 'FREIGHT' AND is_active = 1
ORDER BY country_code";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new FreightRouteRow
                {
                    Id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    RouteCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    RouteName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    RouteNameZh = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Rate2025 = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4)),
                    Rate2024 = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5)),
                    Rate2023 = reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetValue(6)),
                    IsActive = !reader.IsDBNull(7) && reader.GetInt32(7) == 1,
                });
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return rows;
    }

    private async Task UpsertFreightRateAsync(
        FreightRateUpdate update,
        int year,
        string rateColumn,
        CancellationToken cancellationToken)
    {
        var routeName = string.IsNullOrWhiteSpace(update.RouteName) ? update.RouteCode : update.RouteName;
        var routeNameZh = update.RouteNameZh;

        var updateSql = $@"
UPDATE tariff_rates
SET {rateColumn} = @rate,
    country_name = COALESCE(@name, country_name),
    country_name_zh = COALESCE(@nameZh, country_name_zh)
WHERE product_group = @group AND country_code = @code";

        var updateParams = new[]
        {
            new SqlParameter("@rate", update.Rate),
            new SqlParameter("@name", (object?)routeName ?? DBNull.Value),
            new SqlParameter("@nameZh", (object?)routeNameZh ?? DBNull.Value),
            new SqlParameter("@group", "FREIGHT"),
            new SqlParameter("@code", update.RouteCode),
        };

        var affected = await _dbContext.Database.ExecuteSqlRawAsync(updateSql, updateParams, cancellationToken);
        if (affected > 0)
        {
            return;
        }

        var insertSql = @"
INSERT INTO tariff_rates
    (country_code, country_name, country_name_zh, product_group, is_active, rate_2025, rate_2024, rate_2023)
VALUES
    (@code, @name, @nameZh, @group, 1, @rate2025, @rate2024, @rate2023)";

        var rate2025 = year == 2025 ? update.Rate : 0m;
        var rate2024 = year == 2024 ? update.Rate : 0m;
        var rate2023 = year == 2023 ? update.Rate : 0m;

        var insertParams = new[]
        {
            new SqlParameter("@code", update.RouteCode),
            new SqlParameter("@name", (object?)routeName ?? DBNull.Value),
            new SqlParameter("@nameZh", (object?)routeNameZh ?? DBNull.Value),
            new SqlParameter("@group", "FREIGHT"),
            new SqlParameter("@rate2025", rate2025),
            new SqlParameter("@rate2024", rate2024),
            new SqlParameter("@rate2023", rate2023),
        };

        await _dbContext.Database.ExecuteSqlRawAsync(insertSql, insertParams, cancellationToken);
    }

    private static decimal GetRateForYear(FreightRouteRow route, int year)
    {
        return year switch
        {
            2025 => route.Rate2025,
            2024 => route.Rate2024,
            2023 => route.Rate2023,
            _ => route.Rate2025,
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

    private sealed class FreightRouteRow
    {
        public int Id { get; set; }
        public string RouteCode { get; set; } = string.Empty;
        public string? RouteName { get; set; }
        public string? RouteNameZh { get; set; }
        public decimal Rate2025 { get; set; }
        public decimal Rate2024 { get; set; }
        public decimal Rate2023 { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class FreightRateUpdate
    {
        public string RouteCode { get; set; } = string.Empty;
        public string? RouteName { get; set; }
        public string? RouteNameZh { get; set; }
        public decimal Rate { get; set; }
    }
}
