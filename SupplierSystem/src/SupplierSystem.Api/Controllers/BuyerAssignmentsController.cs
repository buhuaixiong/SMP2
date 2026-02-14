using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Authorization;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/buyer-assignments")]
public sealed class BuyerAssignmentsController : NodeControllerBase
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<BuyerAssignmentsController> _logger;

    public BuyerAssignmentsController(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        IWebHostEnvironment environment,
        ILogger<BuyerAssignmentsController> logger) : base(environment)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost("by-tag")]
    [Authorize(Policy = AuthorizationPolicies.BuyerAssignmentsAdmin)]
    public async Task<IActionResult> AssignSuppliersByTag([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var buyerId = ReadString(body, "buyerId", "buyer_id");
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return BadRequest(new { message = "Buyer ID is required." });
        }

        var tagIds = ReadIntArray(body, "tagIds", "tag_ids");
        if (tagIds.Count == 0)
        {
            return BadRequest(new { message = "Tag IDs array is required." });
        }

        var buyer = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == buyerId, cancellationToken);
        if (buyer == null)
        {
            return NotFound(new { message = "Buyer not found." });
        }

        var suppliers = await (from tag in _dbContext.SupplierTags.AsNoTracking()
                               join supplier in _dbContext.Suppliers.AsNoTracking()
                                   on tag.SupplierId equals supplier.Id
                               where tagIds.Contains(tag.TagId)
                               select supplier)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (suppliers.Count == 0)
        {
            return Ok(new
            {
                message = "No suppliers found with the specified tags.",
                data = new { assignedCount = 0, supplierIds = Array.Empty<int>() }
            });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var createdBy = actor.Name ?? buyerId;

        var supplierIds = suppliers.Select(s => s.Id).ToList();
        var assignedCount = 0;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var supplierId in supplierIds)
            {
                var exists = await _dbContext.BuyerSupplierAssignments
                    .AnyAsync(a => a.BuyerId == buyerId && a.SupplierId == supplierId, cancellationToken);
                if (exists)
                {
                    continue;
                }

                _dbContext.BuyerSupplierAssignments.Add(new BuyerSupplierAssignment
                {
                    BuyerId = buyerId,
                    SupplierId = supplierId,
                    CreatedAt = now,
                    CreatedBy = createdBy,
                });
                assignedCount++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "[BuyerAssignments] Failed to assign suppliers by tag.");
            return StatusCode(500, new { message = "Failed to assign suppliers by tag." });
        }

        await LogAuditAsync(new AuditEntry
        {
            ActorId = actor.Id,
            ActorName = actor.Name,
            EntityType = "buyer_assignment",
            EntityId = buyerId,
            Action = "assign_by_tags",
            Changes = JsonSerializer.Serialize(new
            {
                buyerId,
                buyerName = buyer.Name,
                tagIds,
                supplierIds,
                assignedCount,
            }),
        });

        return Ok(new
        {
            message = $"Successfully assigned {assignedCount} supplier(s) to buyer {buyer.Name}.",
            data = new
            {
                assignedCount,
                totalSuppliers = suppliers.Count,
                supplierIds,
            }
        });
    }

    [HttpGet("suppliers")]
    [Authorize(Policy = AuthorizationPolicies.BuyerAssignmentsRead)]
    public async Task<IActionResult> GetAssignedSuppliers([FromQuery] string? buyerId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var targetBuyerId = string.IsNullOrWhiteSpace(buyerId) ? user.Id : buyerId;

        var assignments = await (from assignment in _dbContext.BuyerSupplierAssignments.AsNoTracking()
                                 join supplier in _dbContext.Suppliers.AsNoTracking()
                                     on assignment.SupplierId equals supplier.Id
                                 where assignment.BuyerId == targetBuyerId
                                 orderby assignment.CreatedAt descending
                                 select new
                                 {
                                     assignment.Id,
                                     assignment.BuyerId,
                                     assignment.SupplierId,
                                     assignment.CreatedAt,
                                     supplier.CompanyName,
                                     supplier.CompanyId,
                                     supplier.Category,
                                     supplier.Region,
                                     supplier.Status,
                                 })
            .ToListAsync(cancellationToken);

        return Ok(new { data = assignments });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.BuyerAssignmentsAdmin)]
    public async Task<IActionResult> DeleteAssignment(int id, CancellationToken cancellationToken)
    {
        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var assignment = await _dbContext.BuyerSupplierAssignments
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (assignment == null)
        {
            return NotFound(new { message = "Assignment not found." });
        }

        _dbContext.BuyerSupplierAssignments.Remove(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(new AuditEntry
        {
            ActorId = actor.Id,
            ActorName = actor.Name,
            EntityType = "buyer_assignment",
            EntityId = id.ToString(),
            Action = "remove_assignment",
            Changes = JsonSerializer.Serialize(new
            {
                assignment.BuyerId,
                assignment.SupplierId,
            }),
        });

        return NoContent();
    }

    [HttpGet("buyers")]
    [Authorize(Policy = AuthorizationPolicies.BuyerAssignmentsAdmin)]
    public async Task<IActionResult> GetBuyers(CancellationToken cancellationToken)
    {
        var buyers = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Role == "purchaser" || u.Role == "procurement_manager")
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Role })
            .ToListAsync(cancellationToken);

        return Ok(new { data = buyers });
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

    private static List<int> ReadIntArray(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var results = new List<int>();
            foreach (var entry in value.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var numeric))
                {
                    results.Add(numeric);
                    continue;
                }

                if (entry.ValueKind == JsonValueKind.String && int.TryParse(entry.GetString(), out numeric))
                {
                    results.Add(numeric);
                }
            }

            return results;
        }

        return new List<int>();
    }

    private async Task LogAuditAsync(AuditEntry entry)
    {
        try
        {
            await _auditService.LogAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BuyerAssignments] Failed to write audit entry.");
        }
    }
}
