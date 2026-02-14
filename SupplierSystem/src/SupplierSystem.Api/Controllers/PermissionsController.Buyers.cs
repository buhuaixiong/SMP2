using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class PermissionsController
{
    [HttpGet("buyers")]
    public async Task<IActionResult> GetAllBuyers([FromQuery] bool withStats, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var buyersQuery = _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Role == "purchaser")
            .OrderBy(u => u.Name);

        if (!withStats)
        {
            var buyers = await buyersQuery
                .Select(u => new { u.Id, u.Name, u.Email, u.Status, u.CreatedAt, u.Role })
                .ToListAsync(cancellationToken);

            return Ok(new { data = buyers });
        }

        var buyersWithStats = await buyersQuery
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Status,
                u.CreatedAt,
                u.Role,
                assignmentCount = _dbContext.BuyerSupplierAssignments.Count(a => a.BuyerId == u.Id),
            })
            .ToListAsync(cancellationToken);

        return Ok(new { data = buyersWithStats });
    }

    [HttpGet("buyers/{buyerId}/assignments")]
    public async Task<IActionResult> GetBuyerAssignments(string buyerId, CancellationToken cancellationToken)
    {
        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!string.Equals(actor.Id, buyerId, StringComparison.OrdinalIgnoreCase))
        {
            var permissionResult = RequirePermission(actor, Permissions.AdminRoleManage);
            if (permissionResult != null)
            {
                return permissionResult;
            }
        }

        var buyer = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == buyerId, cancellationToken);

        if (buyer == null)
        {
            return NotFound(new { message = "Buyer not found." });
        }

        if (!string.Equals(buyer.Role, "purchaser", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "User is not a purchaser." });
        }

        var assignments = await (from assignment in _dbContext.BuyerSupplierAssignments.AsNoTracking()
                                 join supplier in _dbContext.Suppliers.AsNoTracking()
                                     on assignment.SupplierId equals supplier.Id into supplierJoin
                                 from supplier in supplierJoin.DefaultIfEmpty()
                                 join permission in _dbContext.BuyerSupplierPermissions.AsNoTracking()
                                     on new { assignment.BuyerId, assignment.SupplierId }
                                     equals new { BuyerId = permission.BuyerId, SupplierId = permission.SupplierId }
                                     into permissionJoin
                                 from permission in permissionJoin.DefaultIfEmpty()
                                 where assignment.BuyerId == buyerId
                                 orderby supplier.CompanyName
                                 select new
                                 {
                                     assignment.SupplierId,
                                     assignment.BuyerId,
                                     assignment.CreatedAt,
                                     companyName = supplier.CompanyName,
                                     companyId = supplier.CompanyId,
                                     supplierStatus = supplier.Status,
                                     supplierStage = supplier.Stage,
                                     profileAccess = permission != null && permission.CanViewProfile,
                                     contractAlert = permission != null && permission.ReceiveContractAlerts,
                                 })
            .ToListAsync(cancellationToken);

        return Ok(new { data = assignments });
    }

    [HttpPost("buyers/{buyerId}/assignments")]
    public async Task<IActionResult> UpdateBuyerAssignments(
        string buyerId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var buyer = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == buyerId, cancellationToken);
        if (buyer == null)
        {
            return NotFound(new { message = "Buyer not found." });
        }

        if (!string.Equals(buyer.Role, "purchaser", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "User is not a purchaser." });
        }

        if (!body.TryGetProperty("assignments", out var assignmentsElement) || assignmentsElement.ValueKind != JsonValueKind.Array)
        {
            return BadRequest(new { message = "Assignments must be an array." });
        }

        var validAssignments = ParseAssignments(assignmentsElement);
        var targetSet = new HashSet<int>(validAssignments.Select(a => a.SupplierId));

        var currentSupplierIds = await _dbContext.BuyerSupplierAssignments
            .AsNoTracking()
            .Where(a => a.BuyerId == buyerId)
            .Select(a => a.SupplierId)
            .ToListAsync(cancellationToken);

        var currentSet = new HashSet<int>(currentSupplierIds);
        var toAdd = targetSet.Where(id => !currentSet.Contains(id)).ToList();
        var toRemove = currentSet.Where(id => !targetSet.Contains(id)).ToList();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (toRemove.Count > 0)
            {
                var removeAssignments = _dbContext.BuyerSupplierAssignments
                    .Where(a => a.BuyerId == buyerId && toRemove.Contains(a.SupplierId));
                _dbContext.BuyerSupplierAssignments.RemoveRange(removeAssignments);

                var removePermissions = _dbContext.BuyerSupplierPermissions
                    .Where(p => p.BuyerId == buyerId && toRemove.Contains(p.SupplierId));
                _dbContext.BuyerSupplierPermissions.RemoveRange(removePermissions);
            }

            if (toAdd.Count > 0)
            {
                var now = DateTimeOffset.UtcNow.ToString("o");
                foreach (var supplierId in toAdd)
                {
                    _dbContext.BuyerSupplierAssignments.Add(new BuyerSupplierAssignment
                    {
                        BuyerId = buyerId,
                        SupplierId = supplierId,
                        CreatedAt = now,
                        CreatedBy = actor.Name,
                    });
                }
            }

            foreach (var assignment in validAssignments)
            {
                var existing = await _dbContext.BuyerSupplierPermissions
                    .FirstOrDefaultAsync(p => p.BuyerId == buyerId && p.SupplierId == assignment.SupplierId, cancellationToken);

                var now = DateTimeOffset.UtcNow.ToString("o");
                if (existing == null)
                {
                    _dbContext.BuyerSupplierPermissions.Add(new BuyerSupplierPermission
                    {
                        BuyerId = buyerId,
                        SupplierId = assignment.SupplierId,
                        CanViewProfile = assignment.ProfileAccess,
                        ReceiveContractAlerts = assignment.ContractAlert,
                        CreatedAt = now,
                        UpdatedAt = now,
                        CreatedBy = actor.Name,
                        UpdatedBy = actor.Name,
                    });
                }
                else
                {
                    existing.CanViewProfile = assignment.ProfileAccess;
                    existing.ReceiveContractAlerts = assignment.ContractAlert;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = actor.Name;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "[Permissions] Failed to update buyer assignments.");
            return StatusCode(500, new { message = "Failed to update assignments." });
        }

        if (toAdd.Count > 0 || toRemove.Count > 0)
        {
            await LogAuditAsync(
                "buyer_assignment",
                buyerId,
                "update_assignments",
                new { added = toAdd, removed = toRemove, permissionsUpdated = validAssignments.Count },
                actor);
        }

        var updated = await GetBuyerAssignmentsPayloadAsync(buyerId, cancellationToken);
        return Ok(new { data = updated });
    }

    [HttpGet("buyers/{buyerId}/stats")]
    public async Task<IActionResult> GetBuyerStats(string buyerId, CancellationToken cancellationToken)
    {
        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!string.Equals(actor.Id, buyerId, StringComparison.OrdinalIgnoreCase))
        {
            var permissionResult = RequirePermission(actor, Permissions.AdminRoleManage);
            if (permissionResult != null)
            {
                return permissionResult;
            }
        }

        var buyer = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == buyerId, cancellationToken);

        if (buyer == null)
        {
            return NotFound(new { message = "Buyer not found." });
        }

        if (!string.Equals(buyer.Role, "purchaser", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "User is not a purchaser." });
        }

        var assignments = await (from assignment in _dbContext.BuyerSupplierAssignments.AsNoTracking()
                                 join supplier in _dbContext.Suppliers.AsNoTracking()
                                     on assignment.SupplierId equals supplier.Id into supplierJoin
                                 from supplier in supplierJoin.DefaultIfEmpty()
                                 where assignment.BuyerId == buyerId
                                 select new { supplier.Status, supplier.Stage })
            .ToListAsync(cancellationToken);

        var statusCounts = assignments
            .GroupBy(a => a.Status ?? "unknown")
            .ToDictionary(group => group.Key, group => group.Count());

        var stageCounts = assignments
            .GroupBy(a => a.Stage ?? "unknown")
            .ToDictionary(group => group.Key, group => group.Count());

        var totalSuppliers = await _dbContext.BuyerSupplierAssignments
            .AsNoTracking()
            .CountAsync(a => a.BuyerId == buyerId, cancellationToken);

        return Ok(new
        {
            data = new
            {
                buyerId,
                buyerName = buyer.Name,
                totalSuppliers,
                byStatus = statusCounts,
                byStage = stageCounts,
            }
        });
    }

    [HttpGet("suppliers/search")]
    public async Task<IActionResult> SearchAssignableSuppliers(CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var keyword = Request.Query["q"].ToString() ?? string.Empty;
        var limit = int.TryParse(Request.Query["limit"], out var parsed) ? Math.Max(1, parsed) : 20;
        limit = Math.Min(100, limit);

        var query = _dbContext.Suppliers.AsNoTracking()
            .Where(s => s.Status == null || (s.Status != "rejected" && s.Status != "deleted"));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            var term = $"%{trimmed}%";
            var parsedId = int.TryParse(trimmed, out var supplierId) ? supplierId : (int?)null;
            query = query.Where(s =>
                EF.Functions.Like(s.CompanyName, term) ||
                EF.Functions.Like(s.CompanyId, term) ||
                EF.Functions.Like(s.SupplierCode ?? string.Empty, term) ||
                (parsedId.HasValue && s.Id == parsedId.Value));
        }

        var results = await query
            .OrderBy(s => s.CompanyName)
            .Take(limit)
            .Select(s => new { s.Id, s.CompanyName, s.CompanyId, s.SupplierCode })
            .ToListAsync(cancellationToken);

        return Ok(new { data = results });
    }

    [HttpGet("suppliers/{supplierId:int}/buyers")]
    public async Task<IActionResult> GetSupplierAssignedBuyers(int supplierId, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var buyers = await (from assignment in _dbContext.BuyerSupplierAssignments.AsNoTracking()
                            join user in _dbContext.Users.AsNoTracking()
                                on assignment.BuyerId equals user.Id into userJoin
                            from user in userJoin.DefaultIfEmpty()
                            where assignment.SupplierId == supplierId
                            orderby user.Name
                            select new
                            {
                                assignment.BuyerId,
                                assignment.SupplierId,
                                assignment.CreatedAt,
                                buyerName = user.Name,
                                buyerEmail = user.Email,
                            })
            .ToListAsync(cancellationToken);

        return Ok(new { data = buyers });
    }

    [HttpPost("assignments")]
    public async Task<IActionResult> CreateAssignment([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var buyerId = GetString(body, "buyerId", "buyer_id");
        var supplierId = GetInt(body, "supplierId", "supplier_id");

        if (string.IsNullOrWhiteSpace(buyerId) || !supplierId.HasValue)
        {
            return BadRequest(new { message = "Buyer ID and supplier ID are required." });
        }

        var buyer = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == buyerId, cancellationToken);

        if (buyer == null)
        {
            return NotFound(new { message = "Buyer not found." });
        }

        if (!string.Equals(buyer.Role, "purchaser", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "User is not a purchaser." });
        }

        var exists = await _dbContext.BuyerSupplierAssignments
            .AnyAsync(a => a.BuyerId == buyerId && a.SupplierId == supplierId, cancellationToken);

        if (exists)
        {
            return BadRequest(new { message = "Assignment already exists." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var assignment = new BuyerSupplierAssignment
        {
            BuyerId = buyerId,
            SupplierId = supplierId.Value,
            CreatedAt = now,
            CreatedBy = actor.Name,
        };

        _dbContext.BuyerSupplierAssignments.Add(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "buyer_supplier_assignment",
            $"{buyerId}_{supplierId}",
            "create",
            new { buyerId, supplierId, buyerName = buyer.Name },
            actor);

        return StatusCode(201, new { data = assignment });
    }

    [HttpDelete("assignments/{buyerId}/{supplierId:int}")]
    public async Task<IActionResult> DeleteAssignment(string buyerId, int supplierId, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var assignment = await _dbContext.BuyerSupplierAssignments
            .FirstOrDefaultAsync(a => a.BuyerId == buyerId && a.SupplierId == supplierId, cancellationToken);

        if (assignment == null)
        {
            return NotFound(new { message = "Assignment not found." });
        }

        _dbContext.BuyerSupplierAssignments.Remove(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "buyer_supplier_assignment",
            $"{buyerId}_{supplierId}",
            "delete",
            new { buyerId, supplierId },
            actor);

        return Ok(new
        {
            success = true,
            buyerId,
            supplierId,
            message = "Assignment deleted successfully"
        });
    }

    [HttpPost("buyers/{buyerId}/batch-assign")]
    public async Task<IActionResult> BatchAssignSuppliers(string buyerId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var supplierIds = GetIntArray(body, "supplierIds", "supplier_ids");
        if (supplierIds.Count == 0)
        {
            return BadRequest(new { message = "Supplier IDs must be a non-empty array." });
        }

        var buyer = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == buyerId, cancellationToken);

        if (buyer == null)
        {
            return NotFound(new { message = "Buyer not found." });
        }

        if (!string.Equals(buyer.Role, "purchaser", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "User is not a purchaser." });
        }

        var existing = await _dbContext.BuyerSupplierAssignments
            .AsNoTracking()
            .Where(a => a.BuyerId == buyerId && supplierIds.Contains(a.SupplierId))
            .Select(a => a.SupplierId)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<int>(existing);
        var toAdd = supplierIds.Where(id => !existingSet.Contains(id)).ToList();

        if (toAdd.Count > 0)
        {
            var now = DateTimeOffset.UtcNow.ToString("o");
            foreach (var supplierId in toAdd)
            {
                _dbContext.BuyerSupplierAssignments.Add(new BuyerSupplierAssignment
                {
                    BuyerId = buyerId,
                    SupplierId = supplierId,
                    CreatedAt = now,
                    CreatedBy = actor.Name,
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await LogAuditAsync(
                "buyer_supplier_assignment",
                $"buyer_{buyerId}",
                "batch_assign",
                new
                {
                    buyerId,
                    buyerName = buyer.Name,
                    assignedCount = toAdd.Count,
                    skippedCount = existingSet.Count,
                    supplierIds,
                },
                actor);
        }

        return Ok(new
        {
            success = true,
            buyerId,
            assigned = toAdd.Count,
            skipped = existingSet.Count,
            message = $"Assigned {toAdd.Count} suppliers, skipped {existingSet.Count} (already assigned)"
        });
    }

    [HttpPost("buyers/{buyerId}/batch-unassign")]
    public async Task<IActionResult> BatchUnassignSuppliers(string buyerId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var supplierIds = GetIntArray(body, "supplierIds", "supplier_ids");
        if (supplierIds.Count == 0)
        {
            return BadRequest(new { message = "Supplier IDs must be a non-empty array." });
        }

        var buyer = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == buyerId, cancellationToken);

        if (buyer == null)
        {
            return NotFound(new { message = "Buyer not found." });
        }

        var deleteAssignments = _dbContext.BuyerSupplierAssignments
            .Where(a => a.BuyerId == buyerId && supplierIds.Contains(a.SupplierId));

        _dbContext.BuyerSupplierAssignments.RemoveRange(deleteAssignments);
        var deleted = await _dbContext.SaveChangesAsync(cancellationToken);

        if (deleted > 0)
        {
            await LogAuditAsync(
                "buyer_supplier_assignment",
                $"buyer_{buyerId}",
                "batch_unassign",
                new { buyerId, buyerName = buyer.Name, deletedCount = deleted, supplierIds },
                actor);
        }

        return Ok(new
        {
            success = true,
            buyerId,
            deleted,
            message = $"Removed {deleted} assignments"
        });
    }

    [HttpDelete("buyers/{buyerId}/assignments")]
    public async Task<IActionResult> DeleteAllBuyerAssignments(string buyerId, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var buyer = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == buyerId, cancellationToken);

        if (buyer == null)
        {
            return NotFound(new { message = "Buyer not found." });
        }

        var assignments = _dbContext.BuyerSupplierAssignments.Where(a => a.BuyerId == buyerId);
        _dbContext.BuyerSupplierAssignments.RemoveRange(assignments);
        var deleted = await _dbContext.SaveChangesAsync(cancellationToken);

        if (deleted > 0)
        {
            await LogAuditAsync(
                "buyer_supplier_assignment",
                $"buyer_{buyerId}",
                "delete_all",
                new { buyerId, buyerName = buyer.Name, deletedCount = deleted },
                actor);
        }

        return Ok(new
        {
            success = true,
            buyerId,
            deleted,
            message = $"Removed {deleted} assignments for buyer {buyer.Name}"
        });
    }

    [HttpDelete("suppliers/{supplierId:int}/assignments")]
    public async Task<IActionResult> DeleteAllSupplierAssignments(int supplierId, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var actor = HttpContext.GetAuthUser();
        if (actor == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var assignments = _dbContext.BuyerSupplierAssignments.Where(a => a.SupplierId == supplierId);
        _dbContext.BuyerSupplierAssignments.RemoveRange(assignments);
        var deleted = await _dbContext.SaveChangesAsync(cancellationToken);

        if (deleted > 0)
        {
            await LogAuditAsync(
                "buyer_supplier_assignment",
                $"supplier_{supplierId}",
                "delete_all",
                new { supplierId, deletedCount = deleted },
                actor);
        }

        return Ok(new
        {
            success = true,
            supplierId,
            deleted,
            message = $"Removed {deleted} assignments for supplier {supplierId}"
        });
    }

    private static List<AssignmentInput> ParseAssignments(JsonElement assignments)
    {
        var results = new List<AssignmentInput>();
        var seen = new HashSet<int>();

        foreach (var entry in assignments.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var supplierId = GetInt(entry, "supplierId", "supplier_id");
            if (!supplierId.HasValue || supplierId.Value <= 0)
            {
                continue;
            }

            if (!seen.Add(supplierId.Value))
            {
                continue;
            }

            var profileAccess = GetBool(entry, "profileAccess", "profile_access") ?? false;
            var contractAlert = GetBool(entry, "contractAlert", "contract_alert") ?? false;

            results.Add(new AssignmentInput
            {
                SupplierId = supplierId.Value,
                ProfileAccess = profileAccess,
                ContractAlert = contractAlert,
            });
        }

        return results;
    }

    private async Task<IReadOnlyList<object>> GetBuyerAssignmentsPayloadAsync(string buyerId, CancellationToken cancellationToken)
    {
        var assignments = await (from assignment in _dbContext.BuyerSupplierAssignments.AsNoTracking()
                                 join supplier in _dbContext.Suppliers.AsNoTracking()
                                     on assignment.SupplierId equals supplier.Id into supplierJoin
                                 from supplier in supplierJoin.DefaultIfEmpty()
                                 join permission in _dbContext.BuyerSupplierPermissions.AsNoTracking()
                                     on new { assignment.BuyerId, assignment.SupplierId }
                                     equals new { BuyerId = permission.BuyerId, SupplierId = permission.SupplierId }
                                     into permissionJoin
                                 from permission in permissionJoin.DefaultIfEmpty()
                                 where assignment.BuyerId == buyerId
                                 orderby supplier.CompanyName
                                 select new
                                 {
                                     assignment.SupplierId,
                                     assignment.BuyerId,
                                     assignment.CreatedAt,
                                     companyName = supplier.CompanyName,
                                     companyId = supplier.CompanyId,
                                     supplierStatus = supplier.Status,
                                     supplierStage = supplier.Stage,
                                     profileAccess = permission != null && permission.CanViewProfile,
                                     contractAlert = permission != null && permission.ReceiveContractAlerts,
                                 })
            .ToListAsync(cancellationToken);

        return assignments;
    }

    private sealed class AssignmentInput
    {
        public int SupplierId { get; set; }
        public bool ProfileAccess { get; set; }
        public bool ContractAlert { get; set; }
    }
}
