using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
[Route("api/ratings")]
public sealed class SupplierRatingsController : ControllerBase
{
    private static readonly string[] RatingPermissions =
    {
        Permissions.PurchaserSegmentManage,
        Permissions.ProcurementManagerRfqReview,
        Permissions.ProcurementDirectorRfqApprove,
        Permissions.ProcurementDirectorReportsView,
        Permissions.AdminRoleManage,
    };

    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<SupplierRatingsController> _logger;

    public SupplierRatingsController(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        ILogger<SupplierRatingsController> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> ListRatings(CancellationToken cancellationToken)
    {
        var permissionResult = RequireRatingAccess(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (!TryReadIntFromQuery(Request.Query, out var supplierId, "supplierId", "supplier_id"))
        {
            return BadRequest(new { message = "supplierId must be a valid integer." });
        }
        var from = ReadStringFromQuery(Request.Query, "from");
        var to = ReadStringFromQuery(Request.Query, "to");

        var query = _dbContext.SupplierRatings.AsNoTracking().AsQueryable();

        if (supplierId.HasValue)
        {
            query = query.Where(r => r.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(from))
        {
            query = query.Where(r => r.PeriodEnd == null || string.Compare(r.PeriodEnd, from) >= 0);
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            query = query.Where(r => r.PeriodStart == null || string.Compare(r.PeriodStart, to) <= 0);
        }

        var ratings = await query
            .OrderByDescending(r => r.PeriodEnd ?? r.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(new { data = ratings });
    }

    [HttpPost]
    public async Task<IActionResult> CreateRating([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireRatingAccess(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var supplierId = ReadInt(body, "supplierId", "supplier_id");
        if (!supplierId.HasValue)
        {
            return BadRequest(new { message = "supplierId is required." });
        }

        var supplierExists = await _dbContext.Suppliers.AsNoTracking()
            .AnyAsync(s => s.Id == supplierId.Value, cancellationToken);

        if (!supplierExists)
        {
            return NotFound(new { message = "Supplier not found." });
        }

        var onTimeDelivery = NormalizeScore(ReadDecimal(body, "onTimeDelivery", "on_time_delivery"));
        var qualityScore = NormalizeScore(ReadDecimal(body, "qualityScore", "quality_score"));
        var serviceScore = NormalizeScore(ReadDecimal(body, "serviceScore", "service_score"));
        var costScore = NormalizeScore(ReadDecimal(body, "costScore", "cost_score"));
        var overallScore = CalculateOverallScore(onTimeDelivery, qualityScore, serviceScore, costScore);

        var now = DateTimeOffset.UtcNow.ToString("o");
        var actor = HttpContext.GetAuthUser();
        var createdBy = Sanitize(ReadString(body, "createdBy", "created_by")) ?? Sanitize(actor?.Name) ?? "system";

        var rating = new SupplierRating
        {
            SupplierId = supplierId.Value,
            PeriodStart = ReadString(body, "periodStart", "period_start"),
            PeriodEnd = ReadString(body, "periodEnd", "period_end"),
            OnTimeDelivery = onTimeDelivery,
            QualityScore = qualityScore,
            ServiceScore = serviceScore,
            CostScore = costScore,
            OverallScore = overallScore,
            Notes = Sanitize(ReadString(body, "notes")),
            CreatedAt = now,
            CreatedBy = createdBy,
        };

        _dbContext.SupplierRatings.Add(rating);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(actor, "create", rating.Id.ToString(), body);

        return StatusCode(StatusCodes.Status201Created, new { data = rating });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRating(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequireRatingAccess(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid rating id." });
        }

        var rating = await _dbContext.SupplierRatings
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (rating == null)
        {
            return NotFound(new { message = "Rating not found." });
        }

        _dbContext.SupplierRatings.Remove(rating);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(HttpContext.GetAuthUser(), "delete", id.ToString(), rating);

        return NoContent();
    }

    private async Task LogAuditAsync(AuthUser? actor, string action, string entityId, object payload)
    {
        if (actor == null)
        {
            return;
        }

        try
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = actor.Id,
                ActorName = actor.Name,
                EntityType = "supplier_rating",
                EntityId = entityId,
                Action = action,
                Changes = JsonSerializer.Serialize(payload),
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SupplierRatings] Failed to write audit entry.");
        }
    }

    private static IActionResult? RequireRatingAccess(AuthUser? user)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (RatingPermissions.Any(granted.Contains))
        {
            return null;
        }

        return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
    }

    private static bool TryReadIntFromQuery(IQueryCollection query, out int? value, params string[] keys)
    {
        value = null;
        foreach (var key in keys)
        {
            if (query.TryGetValue(key, out var values))
            {
                if (int.TryParse(values.ToString(), out var parsed))
                {
                    value = parsed;
                    return true;
                }

                return false;
            }
        }

        return true;
    }

    private static string? ReadStringFromQuery(IQueryCollection query, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (query.TryGetValue(key, out var values))
            {
                var value = values.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static int? ReadInt(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (body.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
                {
                    return numeric;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric))
                {
                    return numeric;
                }
            }
        }

        return null;
    }

    private static decimal? ReadDecimal(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.CurrentCulture, out numeric))
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

    private static decimal? NormalizeScore(decimal? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var score = value.Value;
        if (score < 0)
        {
            score = 0;
        }
        else if (score > 5)
        {
            score = 5;
        }

        return score;
    }

    private static decimal? CalculateOverallScore(params decimal?[] scores)
    {
        var values = scores.Where(s => s.HasValue).Select(s => s!.Value).ToList();
        if (values.Count == 0)
        {
            return null;
        }

        var average = values.Sum() / values.Count;
        return Math.Round(average, 2, MidpointRounding.AwayFromZero);
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '\'' => "&#39;",
                '"' => "&quot;",
                '`' => "&#96;",
                _ => ch.ToString()
            });
        }

        return builder.ToString();
    }
}
